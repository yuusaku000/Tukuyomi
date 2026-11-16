using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tsukuyomi
{
    /// <summary>
    /// 段階4：海の AR ライブステージで、ヤチヨのライブ（3曲・約10分）。
    ///   始まり：ヤチヨに「ライブをお願い」（Live.Requested）でいつでも／毎晩22時の定期公演
    ///   開演 → 登場（毎回ちがう）→ 曲 → MC → 曲 → MC → 曲 → おわりのあいさつ
    ///   曲は5曲からランダムに3曲。サビごとの「ありえない演出」もランダム（LiveShow.Wow）
    ///   客席は自由に歩ける。C で「おまかせカメラ」、1〜8 でペンライトの色、T で投げ銭（本物のふじゅ〜）
    /// 画面（字幕・投げ銭）は Hud.Live、光・レーザー・客席・カメラは LiveShow.Fx、ありえない演出は LiveShow.Wow。
    /// </summary>
    [DefaultExecutionOrder(500)] // おまかせカメラ・空や霧の上書きは、ほかのあとで動かす
    public partial class LiveShow : MonoBehaviour
    {
        // ───── 組み立て（Editor/LiveStage.cs）で入れるもの ─────
        public Transform performer;            // 舞台の上のヤチヨ（ふだんは隠す）
        public Transform yBody, yHead, yArmL, yArmR;
        public Vector3 spot;                   // ヤチヨが立つ所（舞台の上）
        public Vector3 stageCenter;            // 結晶の舞台のまんなか（床の高さ）
        public Transform rig;                  // ライブの間だけ出すもの（光の筋・レーザーなど）
        public Transform[] heads;              // ムービングライト（+z が光の向き）
        public Renderer[] beams;
        public Renderer entranceBeam;          // 登場の光の柱
        public MeshFilter lasers, pens, arms;
        public Vector3[] laserFrom;
        public Renderer[] ledRing, ledFloor;
        public Renderer moonDisc;
        public Camera closeCam;
        public Light[] spots;
        public Light wash;
        public AudioSource music, cheer;
        public Material sparkMat, penMat, holoMat, glowMat, beamMat, waterMat;
        public Vector2 seatMin = new Vector2(-18, -158.5f), seatMax = new Vector2(18, -139f);

        public enum Phase { Idle, Opening, Song, MC, Ending }

        public static LiveShow I { get; private set; }
        public static bool Running => I != null && I._phase != Phase.Idle;
        public static Phase Now => I != null ? I._phase : Phase.Idle;

        /// <summary>確認用：曲順をきめる（"--live-set 2,4,1" ／ tour の set=）。</summary>
        public static int[] ForceSet;
        public const int SetSize = 3;

        Phase _phase;
        float _pt;            // いまの段階の時間（秒）
        float _songT;         // 曲の中の時間
        readonly List<LiveSong> _all = new List<LiveSong>();
        readonly List<LiveSong> _set = new List<LiveSong>();
        int _si;              // いま何曲目か（0 から）
        AudioClip[] _clips = new AudioClip[SetSize];
        float[][] _data = new float[SetSize][];
        readonly bool[] _ready = new bool[SetSize], _going = new bool[SetSize];
        float _readyAt = -1;
        bool _requested;
        string _lastAutoDay = "";
        bool _announced;
        PlayerController _player;
        OrbitCamera _cam;

        public LiveSong Song => _phase == Phase.Song && _si < _set.Count ? _set[_si] : null;
        public LiveSong NextSong => _phase == Phase.MC && _si + 1 < _set.Count ? _set[_si + 1] : null;
        public int SongNo => _si + 1;
        public IReadOnlyList<LiveSong> SetList => _set;
        public float SongTime => _songT;
        public float PhaseTime => _pt;

        /// <summary>開演までの秒（曲の準備ができていなければ -1）。</summary>
        public float CountDown => _phase != Phase.Opening || _readyAt < 0 ? -1 : _readyAt + 3.2f - _pt;

        /// <summary>いまの区切り（曲の外なら null）。</summary>
        public LiveSec Sec => Song?.At(_songT);

        // ───────── 始まり ─────────

        void Awake()
        {
            I = this;
            Live.Requested = OnRequested;
            _all.AddRange(LiveMusic.All());
            if (performer) performer.gameObject.SetActive(false);
            if (rig) rig.gameObject.SetActive(false);
            if (closeCam) closeCam.enabled = false;
            var fs = Ts.Arg("--live-set");
            if (fs != null) ForceSet = ParseSet(fs);
        }

        public static int[] ParseSet(string s)
        {
            var a = s.Split(',');
            var r = new List<int>();
            foreach (var x in a) if (int.TryParse(x, out var v)) r.Add(v);
            return r.ToArray();
        }

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
            _cam = FindFirstObjectByType<OrbitCamera>();
            FxStart();
            WowStart();
            PenColor = Mathf.Clamp(PlayerPrefs.GetInt("tsukuyomi_pen", 1), 0, PenColors.Length - 1);
            _lastAutoDay = PlayerPrefs.GetString("tsukuyomi_live_day", "");
            if (Ts.Arg("--live") != null) Invoke(nameof(StartFromArg), 2.5f);
        }

        void StartFromArg() => Test(Ts.ArgFloat("--live", 0));

        /// <summary>ヤチヨに「ライブをお願い」と頼まれた。</summary>
        void OnRequested()
        {
            ToSeats();
            if (Running) { Hud.Toast("ライブの真っ最中だよ！ いっしょに光ろう！（ヤチヨ）"); return; }
            Begin(true);
            Hud.Toast("ライブ、はじめるね！ 客席で待っててね（ヤチヨ）");
        }

        /// <summary>客席のまんなかの通路へワープ（舞台のほうを向く）。</summary>
        public void ToSeats()
        {
            if (_player == null) return;
            _player.Teleport(new Vector3(0, Island.Ground + 0.15f, -147f), 180f);
            if (_cam != null) { _cam.yaw = 180; _cam.pitch = 6; _cam.distance = 4.5f; _cam.firstPerson = false; _cam.Snap(); }
        }

        /// <summary>曲順を決める：5曲からランダムに3曲。バラードは1曲目にしない。</summary>
        void PickSet()
        {
            _set.Clear();
            if (ForceSet != null && ForceSet.Length > 0)
            {
                foreach (var i in ForceSet) if (i >= 0 && i < _all.Count && _set.Count < SetSize) _set.Add(_all[i]);
            }
            if (_set.Count == 0)
            {
                var pool = new List<LiveSong>(_all);
                while (_set.Count < SetSize && pool.Count > 0)
                {
                    int k = Random.Range(0, pool.Count);
                    if (_set.Count == 0 && !pool[k].Hype) continue; // 1曲目は盛り上がる曲
                    _set.Add(pool[k]); pool.RemoveAt(k);
                }
            }
            var names = new List<string>(); foreach (var s in _set) names.Add(s.title);
            Debug.Log("[Live] set list: " + string.Join(" / ", names));
        }

        public void Begin(bool requested)
        {
            if (Running) return;
            _requested = requested;
            PickSet();
            _phase = Phase.Opening; _pt = 0; _songT = 0; _readyAt = -1; _si = 0;
            for (int i = 0; i < SetSize; i++) { _ready[i] = _going[i] = false; _data[i] = null; }
            _thanksQ.Clear(); Comments.Clear(); _sayT = 0;
            StartRender(0);
            if (rig) rig.gameObject.SetActive(true);
            if (performer) performer.gameObject.SetActive(false);
            if (closeCam) closeCam.enabled = true;
            if (YachiyoNpc.I != null) YachiyoNpc.I.gameObject.SetActive(false);
            FxBegin();
            WowBegin();
            Debug.Log("[Live] begin " + (requested ? "(requested)" : "(22:00)"));
        }

        void End()
        {
            _phase = Phase.Idle;
            if (music) music.Stop();
            if (rig) rig.gameObject.SetActive(false);
            if (performer) performer.gameObject.SetActive(false);
            if (closeCam) closeCam.enabled = false;
            if (YachiyoNpc.I != null) YachiyoNpc.I.gameObject.SetActive(true);
            SetAutoCam(false);
            FxEnd();
            WowEnd();
            // 曲の音は大きいので、終わったらすてる（次のライブでまた作る）
            for (int i = 0; i < SetSize; i++) { if (_clips[i]) Destroy(_clips[i]); _clips[i] = null; _data[i] = null; _ready[i] = _going[i] = false; }
            Debug.Log("[Live] end");
        }

        // ───────── 曲の音を作る（別のスレッドで） ─────────

        void StartRender(int i)
        {
            if (i < 0 || i >= _set.Count || _going[i] || _ready[i] || _clips[i]) return;
            _going[i] = true;
            var s = _set[i];
            var th = new Thread(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                float[] d = null;
                try { d = LiveMusic.Render(s); }
                catch (Exception e) { Debug.LogError("[Live] render failed: " + e); }
                _data[i] = d; _ready[i] = true;
                Debug.Log($"[Live] rendered '{s.title}' {s.length:0.0}s in {sw.ElapsedMilliseconds}ms");
            }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal };
            th.Start();
        }

        AudioClip Clip(int i)
        {
            if (_clips[i] == null && _ready[i] && _data[i] != null)
            {
                _clips[i] = AudioClip.Create("live_" + i, _data[i].Length, 1, LiveMusic.Rate, false);
                _clips[i].SetData(_data[i], 0);
                LogLevels(_set[i], _data[i]);
                _data[i] = null;
            }
            return _clips[i];
        }

        static void LogLevels(LiveSong s, float[] d)
        {
            if (!Ts.ShotMode) return;
            var sb = new System.Text.StringBuilder($"[Live] levels '{s.title}':");
            foreach (var sec in s.secs) sb.Append($" {sec.name}={LiveMusic.Rms(d, sec.t0, sec.t1):0.000}");
            Debug.Log(sb.ToString());
        }

        bool Ready(int i) => i < SetSize && (_clips[i] != null || _ready[i]);

        // ───────── 進行 ─────────

        static readonly string[] McCheer = { "みんな〜！ ありがとう〜！", "すご〜い！ みんなの声、ちゃんと届いてるよ！", "はぁ…はぁ… たのしい〜っ！", "ありがと〜！ まだまだいけるよね〜！？" };
        static readonly string[] McTalk =
        {
            "ペンライト、すっごくきれい…　ここからだと、光の海みたいだよ",
            "ここは現実じゃないから、なんだってできるんだよ。重力も、空も、ぜんぶ自由！",
            "1〜8のキーで、ペンライトの色を変えられるんだって。いろんな色、見せてね！",
            "きょうのステージは、きょうだけの特別製。おなじライブは、二度とないんだよ",
            "空の上にいる人も、海の中にいる人も、みんな見えてるからね〜！",
        };
        const float McLen = 17f;
        static readonly (float t, string s)[] EndLines =
        {
            (1f, "ありがとう〜〜！！"),
            (4.5f, "きょう、いっしょに光ってくれて、ほんとうにうれしかった"),
            (10f, "勉強も、毎日も、むりしすぎないでね。つかれたら、またツクヨミにおいで"),
            (16.5f, "みんなの光、空にのぼっていくよ…　また、月の下で会おうね！"),
        };
        const float EndLen = 24f;
        string _mc1, _mc2;

        string OpeningLine(float songT)
        {
            if (songT > 5.5f && songT < 10f)
                return Ts.Now.Hour < 4 || Ts.Now.Hour >= 17 ? "ヤオヨロ〜！ ツクヨミのみんな、こんばんは〜！" : "ヤオヨロ〜！ ツクヨミのみんな、来てくれてありがとう！";
            if (songT >= 10f && songT < 14.5f) return $"1曲目、『{_set[0].title}』！　{_set[0].mc}";
            return null;
        }

        /// <summary>いま話していること（字幕の上に出す）。</summary>
        public string Talk(out float alpha)
        {
            alpha = 0;
            if (_sayT > 0) { alpha = Mathf.Clamp01(_sayT / 0.4f) * Mathf.Clamp01((_sayLen - _sayT) / 0.25f + 0.0001f); return _say; }
            if (_phase == Phase.Song && _si == 0 && _songT < 15f) { var s = OpeningLine(_songT); if (s != null) { alpha = 1; return s; } }
            if (_phase == Phase.MC)
            {
                var next = NextSong;
                (float t, string s)[] lines = { (0.8f, _mc1), (5f, _mc2), (11f, next != null ? $"次の曲は『{next.title}』！　{next.mc}" : "") };
                return Line(lines, McLen, out alpha);
            }
            if (_phase == Phase.Ending) return Line(EndLines, EndLen, out alpha);
            return null;
        }

        string Line((float t, string s)[] lines, float len, out float alpha)
        {
            alpha = 0;
            for (int i = lines.Length - 1; i >= 0; i--)
                if (_pt >= lines[i].t)
                {
                    float end = i + 1 < lines.Length ? lines[i + 1].t : len - 0.5f;
                    if (_pt > end) return null;
                    alpha = Mathf.Clamp01((_pt - lines[i].t) / 0.3f) * Mathf.Clamp01((end - _pt) / 0.3f);
                    return lines[i].s;
                }
            return null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Schedule();
            if (_phase == Phase.Idle) { PenTick(false); return; }
            _pt += dt;
            switch (_phase)
            {
                case Phase.Opening:
                    if (Ready(0) && _readyAt < 0) _readyAt = Mathf.Max(_pt, 6f);
                    if (_readyAt >= 0 && _pt >= _readyAt + 3.2f) StartSong(0);
                    break;
                case Phase.Song:
                {
                    var s = Song;
                    // 音の時計はとびとびに進むので、自分の時計を少しずつ音に合わせる
                    _songT += dt;
                    if (music && music.isPlaying && music.clip != null)
                    {
                        float at = music.timeSamples / (float)music.clip.frequency;
                        if (Mathf.Abs(at - _songT) > 0.12f) _songT = at; else _songT += (at - _songT) * 0.1f;
                    }
                    if (_songT >= s.length - 0.05f || (music && music.clip != null && !music.isPlaying && _songT > 2f && !Ts.ShotMode))
                    {
                        Cheer(1f);
                        if (_si + 1 < _set.Count)
                        {
                            _phase = Phase.MC; _pt = 0;
                            _mc1 = McCheer[Random.Range(0, McCheer.Length)];
                            _mc2 = McTalk[Random.Range(0, McTalk.Length)];
                        }
                        else { _phase = Phase.Ending; _pt = 0; FxFinale(); }
                    }
                    break;
                }
                case Phase.MC:
                    if (_pt >= McLen && (Ready(_si + 1) || Ts.ShotMode)) StartSong(_si + 1);
                    break;
                case Phase.Ending:
                    if (_pt >= EndLen) End();
                    break;
            }
            if (_phase == Phase.Idle) return;
            _sayT -= dt;
            TipsTick(dt);
            Perform(dt);
            FxTick(dt);
            WowTick(dt);
            PenTick(InVenue(_player != null ? _player.Position : Vector3.zero));
            AudioTick();
        }

        void StartSong(int i)
        {
            // ひとつ前の曲の音はすてる
            if (i > 0 && _clips[i - 1]) { if (music && music.clip == _clips[i - 1]) music.clip = null; Destroy(_clips[i - 1]); _clips[i - 1] = null; }
            _phase = Phase.Song; _si = i;
            _pt = 0; _songT = 0;
            var clip = Clip(i);
            if (music && clip && !Ts.ShotMode)
            {
                music.clip = clip; music.timeSamples = 0;
                music.Play();
            }
            StartRender(i + 1); // 次の曲はいまのうちに作っておく
            Cheer(0.8f);
            WowSongStart();
            Debug.Log($"[Live] song {i + 1} '{_set[i].title}' start (clip {(clip ? clip.length.ToString("0.0") : "none")}s)");
        }

        /// <summary>確認用（--live 秒 ／ --tour の live:秒）：ライブ全体の何秒目かへ飛ぶ。音は出さない。</summary>
        public void Test(float t)
        {
            if (!Running) Begin(true);
            ToSeats();
            EnsureFans(true);
            float open = 9.2f;
            if (t < open) { _phase = Phase.Opening; _pt = t; _readyAt = 6f; return; }
            t -= open;
            for (int i = 0; i < _set.Count; i++)
            {
                var s = _set[i];
                if (t < s.length)
                {
                    if (music) music.Stop();
                    if (_phase != Phase.Song || _si != i) { _si = i; _phase = Phase.Song; WowSongStart(); }
                    _songT = t; _pt = t;
                    WowSeek();
                    return;
                }
                t -= s.length;
                if (i + 1 < _set.Count)
                {
                    if (t < McLen) { _si = i; _phase = Phase.MC; _pt = t; _mc1 = McCheer[0]; _mc2 = McTalk[1]; return; }
                    t -= McLen;
                }
            }
            _phase = Phase.Ending; _pt = Mathf.Min(t, EndLen - 1f);
        }

        /// <summary>確認用："s2:chorus:2" なら2曲目の2回目のサビの頭の秒（ライブ全体）。</summary>
        public float TimeOf(string spec, float plus)
        {
            var a = spec.Split(':');
            int si = a[0] == "s3" ? 2 : a[0] == "s2" ? 1 : 0;
            if (!Running) PickSetIfNeeded();
            int nth = a.Length > 2 && int.TryParse(a[2], out var n) ? n : 1;
            float baseT = 9.2f;
            for (int i = 0; i < si && i < _set.Count; i++) baseT += _set[i].length + McLen;
            if (si >= _set.Count) return baseT + plus;
            if (a[1] == "mc") return baseT - McLen + plus;
            if (a[1] == "end") return baseT + _set[si].length + plus;   // その曲のおわり（最後の曲ならおわりのあいさつ）
            foreach (var sec in _set[si].secs) if (sec.name == a[1] && --nth == 0) return baseT + sec.t0 + plus;
            return baseT + plus;
        }

        void PickSetIfNeeded() { if (!Running) Begin(true); }

        // ───────── 毎晩22時の定期公演 ─────────

        void Schedule()
        {
            if (Ts.ShotMode || Ts.Shifting) return;
            var now = Ts.Now;
            string day = now.ToString("yyyy-MM-dd");
            if (now.Hour == 21 && now.Minute >= 50 && !_announced && _lastAutoDay != day && !Running)
            {
                _announced = true;
                Hud.Toast("22時から、海のステージでヤチヨのライブ！（M の地図からワープできます）");
            }
            if (now.Hour == 22 && now.Minute < 5 && _lastAutoDay != day && !Running)
            {
                _lastAutoDay = day;
                PlayerPrefs.SetString("tsukuyomi_live_day", day); PlayerPrefs.Save();
                _announced = false;
                Begin(false);
                if (_player != null && !InVenue(_player.Position))
                    Hud.Toast("ヤチヨの定期ライブがはじまりました！ 地図（M）から「ARライブステージ」へ");
            }
        }

        // ───────── 客席・音 ─────────

        /// <summary>ライブの会場（水面の広場と光の橋の先）にいるか。浮いていても会場。</summary>
        public static bool InVenue(Vector3 p)
        {
            if (I == null) return false;
            var d = new Vector2(p.x - I.stageCenter.x, p.z - (I.stageCenter.z - 8.7f));
            return d.magnitude < 47f && p.y < 40f;
        }

        public static bool PlayerInVenue => I != null && I._player != null && InVenue(I._player.Position);

        float _cheer;

        /// <summary>客席の歓声（本物の人混みの録音を大きくする）。</summary>
        public void Cheer(float k) => _cheer = Mathf.Max(_cheer, k);

        void AudioTick()
        {
            var ear = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float d = Vector2.Distance(new Vector2(ear.x, ear.z), new Vector2(stageCenter.x, stageCenter.z));
            float near = 1f - Mathf.Clamp01((d - 45f) / 40f); // 会場の中は耳もとで鳴る。離れると舞台から聞こえる
            if (music)
            {
                music.volume = SoundPrefs.Music;
                music.spatialBlend = 1f - near * 0.85f;
            }
            if (cheer)
            {
                _cheer = Mathf.MoveTowards(_cheer, 0, Time.deltaTime * 0.22f);
                float want = (0.08f + _cheer * 0.9f) * SoundPrefs.Ambient * (_phase == Phase.Idle ? 0 : 1);
                if (!cheer.isPlaying && want > 0.01f && cheer.clip != null && !Ts.ShotMode) cheer.Play();
                cheer.volume = Mathf.MoveTowards(cheer.volume, want, Time.deltaTime * 1.5f);
                cheer.spatialBlend = 1f - near * 0.6f;
            }
        }

        // ───────── ヤチヨのふるまい ─────────

        float _spinT = -1, _waveT, _jumpT;

        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

        static bool HypeSec(LiveSec s) => s != null && (s.name == "chorus" || s.name == "drop");

        void Perform(float dt)
        {
            if (performer == null) return;
            bool show = _phase != Phase.Opening;
            // 登場（毎回ちがう。Wow が場所を決める）・おわりは下がっていく
            Vector3 basePos = spot;
            bool entering = _phase == Phase.Song && _si == 0 && EntranceActive(_songT);
            if (entering) { basePos = EntrancePos(_songT, out bool vis); show = vis; }
            if (_phase == Phase.Ending) basePos = spot + Vector3.down * Ease((_pt - (EndLen - 3.5f)) / 3f) * 2.8f;
            basePos += PerformerOffset; // 空を飛ぶ・浮く（Wow）
            if (performer.gameObject.activeSelf != show) performer.gameObject.SetActive(show);
            if (!show) return;

            var s = Song; var sec = Sec;
            float beat = s != null ? s.Beat(_songT) : _pt * 1.6f;
            int energy = sec != null ? sec.energy : 0;
            bool ballad = s != null && s.style == 1;
            bool chorus = HypeSec(sec);

            float yaw = 0;
            if (_spinT >= 0) { _spinT += dt; yaw = Ease(_spinT / 0.7f) * 360f; if (_spinT > 0.75f) _spinT = -1; }
            else yaw = Mathf.Sin(beat * Mathf.PI / 8f) * (ballad ? 10f : 22f);
            yaw += PerformerYaw;
            _waveT -= dt; _jumpT -= dt;

            float bounce = energy >= 2 && !ballad ? Mathf.Abs(Mathf.Sin(beat * Mathf.PI)) * 0.1f : Mathf.Abs(Mathf.Sin(beat * Mathf.PI * 0.5f)) * 0.03f;
            if (_jumpT > 0) bounce += Mathf.Sin((1 - _jumpT / 0.6f) * Mathf.PI) * 0.35f;
            performer.SetPositionAndRotation(basePos + Vector3.up * bounce, Quaternion.Euler(0, yaw, 0));

            Vector3 body = new Vector3(0, 0, Mathf.Sin(beat * Mathf.PI * 0.5f) * (ballad ? 3f : 6f));
            Vector3 head = new Vector3(Mathf.Sin(beat * Mathf.PI) * 3f, 0, Mathf.Sin(beat * Mathf.PI * 0.5f) * 7f);
            Vector3 armR = new Vector3(-100f, 12f, -32f);   // 右手はマイク
            Vector3 armL;
            float b4 = Mathf.Repeat(beat, 16f);
            if (_phase == Phase.MC || _phase == Phase.Ending || (_phase == Phase.Song && _si == 0 && _songT < 15f))
            {
                armL = new Vector3(-60f + Mathf.Sin(_pt * 2.5f) * 10f, 0, -35f);   // 話しながら、手をさしのべる
                if (_phase == Phase.Ending && _pt > EndLen - 7f && _pt < EndLen - 4f) body = new Vector3(40f * Mathf.Sin(Mathf.Clamp01((_pt - (EndLen - 7f)) / 3f) * Mathf.PI), 0, 0);
            }
            else if (ballad)
                armL = chorus ? new Vector3(-75f + Mathf.Sin(beat * Mathf.PI * 0.25f) * 15f, 0, -55f - Mathf.Sin(beat * Mathf.PI * 0.125f) * 20f)
                              : new Vector3(-55f, 0, 25f + Mathf.Sin(beat * Mathf.PI * 0.25f) * 6f);
            else if (chorus)
                armL = b4 < 14f ? new Vector3(Mathf.Sin(beat * Mathf.PI) * 15f, 0, -150f + Mathf.Sin(beat * Mathf.PI) * 22f) : new Vector3(-95f, 0, -10f);
            else if (energy >= 2)
                armL = new Vector3(-20f + Mathf.Sin(beat * Mathf.PI) * 30f, 0, -95f + Mathf.Sin(beat * Mathf.PI * 0.5f) * 35f);
            else
                armL = new Vector3(Mathf.Sin(beat * Mathf.PI * 0.5f) * 25f, 0, -25f - Mathf.Abs(Mathf.Sin(beat * Mathf.PI * 0.5f)) * 25f);
            if (_waveT > 0) armL = new Vector3(0, 0, -150f + Mathf.Sin(_waveT * 18f) * 28f);  // お礼：左手を大きくふる

            float k = 1 - Mathf.Exp(-14f * dt);
            if (yBody) { yBody.localRotation = Quaternion.Slerp(yBody.localRotation, Quaternion.Euler(body), k); yBody.localPosition = Vector3.zero; }
            if (yHead) yHead.localRotation = Quaternion.Slerp(yHead.localRotation, Quaternion.Euler(head), k);
            if (yArmL) yArmL.localRotation = Quaternion.Slerp(yArmL.localRotation, Quaternion.Euler(armL), k);
            if (yArmR) yArmR.localRotation = Quaternion.Slerp(yArmR.localRotation, Quaternion.Euler(armR), k);
        }

        // ───────── 投げ銭とコメント（コメントは空に流れる：Wow） ─────────

        public class Comment { public string name, text; public int amount; public bool mine; public float t; }
        public static readonly List<Comment> Comments = new List<Comment>();
        public static event Action<Comment> OnComment;
        readonly Queue<(int amt, bool mine, string name)> _thanksQ = new Queue<(int, bool, string)>();
        string _say; float _sayT, _sayLen;
        float _commentT, _fakeTipT = 25f, _thanksGap;

        static readonly string[] Names =
        {
            "月見だんご", "ねこ舟", "しずく", "夜更かし学生", "サカバンバスピス推し", "からあげ", "ルナ", "ほしくず",
            "釣り堀の主", "提灯ゆれる", "ひよこ豆", "まひる", "8000年待った", "ことり", "温泉帰り", "みかん箱",
            "宿題おわった", "もちもち", "てるてる", "はるか", "遊覧船の常連", "ペンライト職人", "ゆずぽん", "そら",
        };
        static readonly string[] CommonC = { "ヤチヨ〜！", "ヤオヨロ〜！", "かわいい", "最高！", "きた〜！", "888888", "ペンライトふってる", "ここから見てる！", "音いい〜", "ツクヨミ最高" };
        static readonly string[] ChorusC = { "サビきた！！", "うおおお", "光の海すごい", "鳥肌", "浮いてる！？", "なにこれすごい", "ピンクにした！", "ジャンプ！", "神曲", "演出やばい" };
        static readonly string[] BalladC = { "泣いてる", "やさしい…", "歌詞しみる", "月きれい", "ずっと聞いていたい", "海の中みたい", "（静かにペンライトふる）", "勉強がんばる" };
        static readonly string[] McC = { "ありがとう〜！", "まってた！", "次の曲なに？", "光の海きれい", "水色にした", "ヤチヨちゃん大好き" };
        static readonly string[] FakeTipMsg = { "いつもありがとう！", "初ライブおめでとう！", "テスト終わったので！", "新曲すき！", "ささやかですが…", "ツクヨミに乾杯" };

        void TipsTick(float dt)
        {
            var sec = Sec;
            float rate = _phase == Phase.Opening ? 1.6f : _phase == Phase.MC ? 1.8f : HypeSec(sec) ? 0.7f : 1.3f;
            _commentT -= dt;
            if (_commentT <= 0)
            {
                _commentT = rate * Random.Range(0.6f, 1.4f);
                var pool = _phase == Phase.MC ? McC : Song != null && Song.style == 1 ? BalladC : HypeSec(sec) ? ChorusC : CommonC;
                if (Random.value < 0.35f) pool = CommonC;
                Add(new Comment { name = Names[Random.Range(0, Names.Length)], text = pool[Random.Range(0, pool.Length)] });
            }
            _fakeTipT -= dt;
            if (_fakeTipT <= 0 && _phase != Phase.Opening)
            {
                _fakeTipT = Random.Range(35f, 70f);
                int[] amts = { 100, 300, 500, 1000, 2000 };
                var n = Names[Random.Range(0, Names.Length)];
                int amt = amts[Random.Range(0, amts.Length)];
                Add(new Comment { name = n, text = FakeTipMsg[Random.Range(0, FakeTipMsg.Length)], amount = amt });
            }
            // 投げ銭：ヤチヨはお礼を言わない（ライブを止めない）。コメントが空で光り、自分の投げ銭ならステージに招待される
            while (_thanksQ.Count > 0)
            {
                var (amt, mine, _) = _thanksQ.Dequeue();
                if (mine) { Cheer(0.5f); FxThanks(amt); BeamUp(); }
            }
        }

        public static void Add(Comment c)
        {
            c.t = Time.unscaledTime;
            Comments.Add(c);
            if (Comments.Count > 40) Comments.RemoveAt(0);
            OnComment?.Invoke(c);
        }

        public void Say(string s, float seconds) { _say = s; _sayT = _sayLen = seconds; }

        /// <summary>投げ銭が届いた（Hud.Live で払えたあと）。</summary>
        public void Tipped(int amount, string text, bool mine)
        {
            Add(new Comment { name = mine ? "あなた" : "だれか", text = text, amount = amount, mine = mine });
            _thanksQ.Enqueue((amount, mine, ""));
            if (mine) _thanksGap = Mathf.Min(_thanksGap, 0.6f);
        }

        /// <summary>確認用：自分の投げ銭が届いたことにする（ふじゅ〜は使わない）。</summary>
        public void TestTip(int amount) { Tipped(amount, "最高〜！", true); _sayT = 0; _thanksGap = 0; TipsTick(0); }
    }
}
