using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tsukuyomi
{
    /// <summary>
    /// 段階4：海の上の AR ライブステージで、ヤチヨのライブ（2曲・約8分）。
    ///   始まり：ヤチヨに「ライブをお願い」（Live.Requested）でいつでも／毎晩22時の定期公演
    ///   開演 → 1曲目「月灯りパレード」→ MC → 2曲目「八千夜のてがみ」→ おわりのあいさつ
    ///   客席は自由に歩ける。C で「おまかせカメラ」、1〜8 でペンライトの色、T で投げ銭（本物のふじゅ〜）
    /// 画面（字幕・コメント・投げ銭）は Hud.Live、光・レーザー・客席・カメラは LiveShow.Fx。
    /// </summary>
    [DefaultExecutionOrder(500)] // おまかせカメラは OrbitCamera のあとで動かす
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
        public Material sparkMat, penMat;
        public Vector2 seatMin = new Vector2(-18, -158.5f), seatMax = new Vector2(18, -139f);

        public enum Phase { Idle, Opening, Song1, MC, Song2, Ending }

        public static LiveShow I { get; private set; }
        public static bool Running => I != null && I._phase != Phase.Idle;
        public static Phase Now => I != null ? I._phase : Phase.Idle;

        Phase _phase;
        float _pt;            // いまの段階の時間（秒）
        float _songT;         // 曲の中の時間
        LiveSong _s1, _s2;
        AudioClip _c1, _c2;
        float[] _d1, _d2;
        volatile bool _r1, _r2;
        bool _g1, _g2;        // 作りはじめた（同じ曲を何度も作らない）
        float _readyAt = -1;
        bool _requested;
        string _lastAutoDay = "";
        bool _announced;
        PlayerController _player;
        OrbitCamera _cam;

        public LiveSong Song => _phase == Phase.Song1 ? _s1 : _phase == Phase.Song2 ? _s2 : null;
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
            _s1 = LiveMusic.Parade();
            _s2 = LiveMusic.Letter();
            if (performer) performer.gameObject.SetActive(false);
            if (rig) rig.gameObject.SetActive(false);
            if (closeCam) closeCam.enabled = false;
        }

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
            _cam = FindFirstObjectByType<OrbitCamera>();
            FxStart();
            PenColor = Mathf.Clamp(PlayerPrefs.GetInt("tsukuyomi_pen", 1), 0, PenColors.Length - 1);
            _lastAutoDay = PlayerPrefs.GetString("tsukuyomi_live_day", "");
            var t = Ts.Arg("--live");
            if (t != null && float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var at))
                Invoke(nameof(StartFromArg), 2.5f);
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

        public void Begin(bool requested)
        {
            if (Running) return;
            _requested = requested;
            _phase = Phase.Opening; _pt = 0; _songT = 0; _readyAt = -1;
            _thanksQ.Clear(); Comments.Clear(); _sayT = 0;
            StartRender(_s1, 1);
            if (rig) rig.gameObject.SetActive(true);
            if (performer) performer.gameObject.SetActive(false);
            if (closeCam) closeCam.enabled = true;
            if (YachiyoNpc.I != null) YachiyoNpc.I.gameObject.SetActive(false);
            FxBegin();
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
            // 曲の音は大きいので、終わったらすてる（次のライブでまた作る）
            if (_c1) { Destroy(_c1); _c1 = null; }
            if (_c2) { Destroy(_c2); _c2 = null; }
            _d1 = _d2 = null; _r1 = _r2 = false; _g1 = _g2 = false;
            Debug.Log("[Live] end");
        }

        // ───────── 曲の音を作る（別のスレッドで） ─────────

        void StartRender(LiveSong s, int which)
        {
            if (which == 1) { if (_g1 || _r1 || _c1) return; _g1 = true; }
            else { if (_g2 || _r2 || _c2) return; _g2 = true; }
            var th = new Thread(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                float[] d = null;
                try { d = LiveMusic.Render(s); }
                catch (Exception e) { Debug.LogError("[Live] render failed: " + e); }
                if (which == 1) { _d1 = d; _r1 = true; } else { _d2 = d; _r2 = true; }
                Debug.Log($"[Live] rendered '{s.title}' {s.length:0.0}s in {sw.ElapsedMilliseconds}ms");
            }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal };
            th.Start();
        }

        AudioClip Clip(int which)
        {
            if (which == 1)
            {
                if (_c1 == null && _r1 && _d1 != null) { _c1 = AudioClip.Create("live_parade", _d1.Length, 1, LiveMusic.Rate, false); _c1.SetData(_d1, 0); LogLevels(_s1, _d1); _d1 = null; }
                return _c1;
            }
            if (_c2 == null && _r2 && _d2 != null) { _c2 = AudioClip.Create("live_letter", _d2.Length, 1, LiveMusic.Rate, false); _c2.SetData(_d2, 0); LogLevels(_s2, _d2); _d2 = null; }
            return _c2;
        }

        static void LogLevels(LiveSong s, float[] d)
        {
            if (!Ts.ShotMode) return;
            var sb = new System.Text.StringBuilder($"[Live] levels '{s.title}':");
            foreach (var sec in s.secs) sb.Append($" {sec.name}={LiveMusic.Rms(d, sec.t0, sec.t1):0.000}");
            Debug.Log(sb.ToString());
        }

        bool Ready(int which) => which == 1 ? (_c1 != null || _r1) : (_c2 != null || _r2);

        // ───────── 進行 ─────────

        // MC（1曲目と2曲目のあいだ）とおわりのあいさつ：（話しはじめる秒, ことば）
        static readonly (float t, string s)[] McLines =
        {
            (1.5f, "みんな〜！ ありがとう〜！"),
            (5.5f, "ペンライト、すっごくきれい…　ここからだと、光の海みたいだよ"),
            (11.5f, "1〜8のキーで、ペンライトの色を変えられるんだって。いろんな色、見せてね！"),
            (18f, "次の曲は、ずっと前から、みんなに聞いてほしかった歌です"),
            (24f, "ゆっくり聞いてね。　──『八千夜のてがみ』"),
        };
        const float McLen = 29f;
        static readonly (float t, string s)[] EndLines =
        {
            (1f, "ありがとう〜〜！！"),
            (4.5f, "きょう、いっしょに光ってくれて、ほんとうにうれしかった"),
            (10f, "勉強も、毎日も、むりしすぎないでね。つかれたら、またツクヨミにおいで"),
            (16.5f, "また、月の下で会おうね！　ヤオヨロ〜！"),
        };
        const float EndLen = 23f;

        string OpeningLine(float songT)
        {
            // 1曲目のイントロの間のあいさつ
            if (songT > 3.5f && songT < 8.5f)
                return Ts.Now.Hour < 4 || Ts.Now.Hour >= 17 ? "ヤオヨロ〜！ ツクヨミのみんな、こんばんは〜！" : "ヤオヨロ〜！ ツクヨミのみんな、来てくれてありがとう！";
            if (songT >= 8.5f && songT < 13.5f) return "きょうはいっしょに、思いっきり光ろうね！　1曲目、『月灯りパレード』！";
            return null;
        }

        /// <summary>いま話していること（字幕の上に出す）。</summary>
        public string Talk(out float alpha)
        {
            alpha = 0;
            if (_sayT > 0) { alpha = Mathf.Clamp01(_sayT / 0.4f) * Mathf.Clamp01((_sayLen - _sayT) / 0.25f + 0.0001f); return _say; }
            if (_phase == Phase.Song1 && _songT < 14f) { var s = OpeningLine(_songT); if (s != null) { alpha = 1; return s; } }
            var lines = _phase == Phase.MC ? McLines : _phase == Phase.Ending ? EndLines : null;
            if (lines == null) return null;
            for (int i = lines.Length - 1; i >= 0; i--)
                if (_pt >= lines[i].t)
                {
                    float end = i + 1 < lines.Length ? lines[i + 1].t : (_phase == Phase.MC ? McLen : EndLen) - 0.5f;
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
                    if (Ready(1) && _readyAt < 0) _readyAt = Mathf.Max(_pt, 6f);
                    if (_readyAt >= 0 && _pt >= _readyAt + 3.2f) StartSong(1);
                    break;
                case Phase.Song1:
                case Phase.Song2:
                {
                    var s = Song;
                    // 音の時計はとびとびに進むので、自分の時計を少しずつ音に合わせる
                    _songT += dt;
                    if (music && music.isPlaying && music.clip != null)
                    {
                        float at = music.timeSamples / (float)music.clip.frequency;
                        if (Mathf.Abs(at - _songT) > 0.12f) _songT = at; else _songT += (at - _songT) * 0.1f;
                    }
                    if (_phase == Phase.Song1 && _songT > 20f) StartRender(_s2, 2); // 2曲目はいまのうちに作っておく
                    if (_songT >= s.length - 0.05f || (music && music.clip != null && !music.isPlaying && _songT > 2f && !Ts.ShotMode))
                    {
                        if (_phase == Phase.Song1) { _phase = Phase.MC; _pt = 0; Cheer(1f); }
                        else { _phase = Phase.Ending; _pt = 0; Cheer(1f); FxFinale(); }
                    }
                    break;
                }
                case Phase.MC:
                    if (_pt >= McLen && (Ready(2) || Ts.ShotMode)) StartSong(2);
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
            PenTick(InVenue(_player != null ? _player.Position : Vector3.zero));
            AudioTick();
        }

        void StartSong(int which)
        {
            _phase = which == 1 ? Phase.Song1 : Phase.Song2;
            _pt = 0; _songT = 0;
            var clip = Clip(which);
            if (music && clip && !Ts.ShotMode)
            {
                music.clip = clip; music.timeSamples = 0;
                music.Play();
            }
            if (which == 1) Cheer(0.8f);
            Debug.Log($"[Live] song {which} start (clip {(clip ? clip.length.ToString("0.0") : "none")}s)");
        }

        /// <summary>確認用（--live 秒 ／ --tour の live:秒）：ライブ全体の何秒目かへ飛ぶ。音は出さない。</summary>
        public void Test(float t)
        {
            if (!Running) Begin(true);
            ToSeats();
            float open = 9.2f;
            if (t < open) { _phase = Phase.Opening; _pt = t; _readyAt = 6f; return; }
            t -= open;
            if (t < _s1.length) { _phase = Phase.Song1; _songT = t; _pt = t; if (music) music.Stop(); EnsureFans(true); return; }
            t -= _s1.length;
            if (t < McLen) { _phase = Phase.MC; _pt = t; EnsureFans(true); return; }
            t -= McLen;
            if (t < _s2.length) { _phase = Phase.Song2; _songT = t; _pt = t; EnsureFans(true); return; }
            t -= _s2.length;
            _phase = Phase.Ending; _pt = Mathf.Min(t, EndLen - 1f); EnsureFans(true);
        }

        /// <summary>確認用：区切りの名前と番号（"s1:chorus:2" なら1曲目の2回目のサビ）から、その頭の秒（ライブ全体）を出す。</summary>
        public float TimeOf(string spec, float plus)
        {
            var a = spec.Split(':');
            var s = a[0] == "s2" ? _s2 : _s1;
            int nth = a.Length > 2 && int.TryParse(a[2], out var n) ? n : 1;
            float baseT = 9.2f + (s == _s2 ? _s1.length + McLen : 0);
            foreach (var sec in s.secs) if (sec.name == a[1] && --nth == 0) return baseT + sec.t0 + plus;
            return baseT + plus;
        }

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

        /// <summary>ライブの会場（水面の広場と光の橋の先）にいるか。</summary>
        public static bool InVenue(Vector3 p)
        {
            if (I == null) return false;
            var d = new Vector2(p.x - I.stageCenter.x, p.z - (I.stageCenter.z - 8.7f));
            return d.magnitude < 47f && p.y < 30f;
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

        float _spinT = -1, _waveT, _bowT, _jumpT;

        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

        void Perform(float dt)
        {
            if (performer == null) return;
            // 登場：1曲目のはじめに舞台の下からせり上がる。おわりは下がっていく
            bool show = _phase != Phase.Opening || _pt > 99f;
            float rise = 1f;
            if (_phase == Phase.Song1) rise = Ease((_songT - 0.6f) / 3.2f);
            if (_phase == Phase.Ending) rise = 1f - Ease((_pt - (EndLen - 3.5f)) / 3f);
            if (_phase == Phase.Opening) show = false;
            if (performer.gameObject.activeSelf != show) performer.gameObject.SetActive(show);
            if (!show) return;

            var s = Song; var sec = Sec;
            float beat = s != null ? s.Beat(_songT) : _pt * 1.6f;
            int energy = sec != null ? sec.energy : 0;
            bool ballad = s != null && s.style == 1;
            bool chorus = sec != null && sec.name == "chorus";

            // サビの頭でくるっと1回転（_spinT は FxTick で 0 にする）
            float yaw = 0;
            if (_spinT >= 0) { _spinT += dt; yaw = Ease(_spinT / 0.7f) * 360f; if (_spinT > 0.75f) _spinT = -1; }
            else yaw = Mathf.Sin(beat * Mathf.PI / 8f) * (ballad ? 10f : 22f);
            _waveT -= dt; _bowT -= dt; _jumpT -= dt;

            float bounce = energy >= 2 && !ballad ? Mathf.Abs(Mathf.Sin(beat * Mathf.PI)) * 0.1f : Mathf.Abs(Mathf.Sin(beat * Mathf.PI * 0.5f)) * 0.03f;
            if (_jumpT > 0) bounce += Mathf.Sin((1 - _jumpT / 0.6f) * Mathf.PI) * 0.35f;
            var basePos = spot + Vector3.down * (1f - rise) * 2.8f;
            performer.SetPositionAndRotation(basePos + Vector3.up * (bounce * rise), Quaternion.Euler(0, yaw, 0));

            Vector3 body = new Vector3(0, 0, Mathf.Sin(beat * Mathf.PI * 0.5f) * (ballad ? 3f : 6f));
            Vector3 head = new Vector3(Mathf.Sin(beat * Mathf.PI) * 3f, 0, Mathf.Sin(beat * Mathf.PI * 0.5f) * 7f);
            // 右手はマイク。左手は曲に合わせて
            Vector3 armR = new Vector3(-100f, 12f, -32f);
            Vector3 armL;
            float b4 = Mathf.Repeat(beat, 16f);
            if (_phase == Phase.MC || _phase == Phase.Ending || (_phase == Phase.Song1 && _songT < 14f))
            {
                armL = new Vector3(-60f + Mathf.Sin(_pt * 2.5f) * 10f, 0, -35f);   // 話しながら、手をさしのべる
                if (_phase == Phase.Ending && _pt > EndLen - 7f && _pt < EndLen - 4f) { body = new Vector3(40f * Mathf.Sin(Mathf.Clamp01((_pt - (EndLen - 7f)) / 3f) * Mathf.PI), 0, 0); }
            }
            else if (ballad)
            {
                // バラード：ゆっくり手をのばしたり、胸にあてたり
                armL = chorus ? new Vector3(-75f + Mathf.Sin(beat * Mathf.PI * 0.25f) * 15f, 0, -55f - Mathf.Sin(beat * Mathf.PI * 0.125f) * 20f)
                              : new Vector3(-55f, 0, 25f + Mathf.Sin(beat * Mathf.PI * 0.25f) * 6f);
            }
            else if (chorus)
            {
                // サビ：手をあげてふる。4小節ごとに客席をゆびさす
                armL = b4 < 14f ? new Vector3(Mathf.Sin(beat * Mathf.PI) * 15f, 0, -150f + Mathf.Sin(beat * Mathf.PI) * 22f)
                                 : new Vector3(-95f, 0, -10f);
            }
            else if (energy >= 2)
                armL = new Vector3(-20f + Mathf.Sin(beat * Mathf.PI) * 30f, 0, -95f + Mathf.Sin(beat * Mathf.PI * 0.5f) * 35f);
            else
                armL = new Vector3(Mathf.Sin(beat * Mathf.PI * 0.5f) * 25f, 0, -25f - Mathf.Abs(Mathf.Sin(beat * Mathf.PI * 0.5f)) * 25f);
            // お礼：左手を大きくふる
            if (_waveT > 0) armL = new Vector3(0, 0, -150f + Mathf.Sin(_waveT * 18f) * 28f);

            float k = 1 - Mathf.Exp(-14f * dt);
            if (yBody) { yBody.localRotation = Quaternion.Slerp(yBody.localRotation, Quaternion.Euler(body), k); yBody.localPosition = Vector3.zero; }
            if (yHead) yHead.localRotation = Quaternion.Slerp(yHead.localRotation, Quaternion.Euler(head), k);
            if (yArmL) yArmL.localRotation = Quaternion.Slerp(yArmL.localRotation, Quaternion.Euler(armL), k);
            if (yArmR) yArmR.localRotation = Quaternion.Slerp(yArmR.localRotation, Quaternion.Euler(armR), k);
        }

        // ───────── 投げ銭とコメント ─────────

        public class Comment { public string name, text; public int amount; public bool mine; public float t; }
        public static readonly List<Comment> Comments = new List<Comment>();
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
        static readonly string[] ChorusC = { "サビきた！！", "うおおお", "光の海すごい", "鳥肌", "いっしょに歌ってる", "ピンクにした！", "ジャンプ！", "神曲" };
        static readonly string[] BalladC = { "泣いてる", "やさしい…", "歌詞しみる", "月きれい", "ずっと聞いていたい", "てがみ…", "（静かにペンライトふる）", "勉強がんばる" };
        static readonly string[] McC = { "ありがとう〜！", "まってた！", "次の曲なに？", "光の海きれい", "水色にした", "ヤチヨちゃん大好き" };
        static readonly string[] FakeTipMsg = { "いつもありがとう！", "初ライブおめでとう！", "テスト終わったので！", "新曲すき！", "ささやかですが…", "ツクヨミに乾杯" };

        void TipsTick(float dt)
        {
            // コメントが流れる（盛り上がるほど多く）
            var sec = Sec;
            float rate = _phase == Phase.Opening ? 1.2f : _phase == Phase.MC ? 1.6f : sec != null && sec.energy >= 3 ? 0.35f : sec != null && sec.energy >= 2 ? 0.6f : 0.9f;
            _commentT -= dt;
            if (_commentT <= 0)
            {
                _commentT = rate * Random.Range(0.6f, 1.4f);
                var pool = _phase == Phase.MC ? McC : Song != null && Song.style == 1 ? BalladC : sec != null && sec.name == "chorus" ? ChorusC : CommonC;
                if (Random.value < 0.4f) pool = CommonC;
                Add(new Comment { name = Names[Random.Range(0, Names.Length)], text = pool[Random.Range(0, pool.Length)] });
            }
            // ほかの人の投げ銭（ときどき）
            _fakeTipT -= dt;
            if (_fakeTipT <= 0 && _phase != Phase.Opening)
            {
                _fakeTipT = Random.Range(35f, 70f);
                int[] amts = { 100, 300, 500, 1000, 2000 };
                var n = Names[Random.Range(0, Names.Length)];
                int amt = amts[Random.Range(0, amts.Length)];
                Add(new Comment { name = n, text = FakeTipMsg[Random.Range(0, FakeTipMsg.Length)], amount = amt });
                _thanksQ.Enqueue((amt, false, n));
            }
            // お礼（曲の間は短く。重ならないように順番に）
            _thanksGap -= dt;
            if (_thanksQ.Count > 0 && _thanksGap <= 0 && _sayT <= 0)
            {
                var (amt, mine, name) = _thanksQ.Dequeue();
                string line = mine
                    ? (amt >= 1000 ? $"わぁっ…{amt:N0}ふじゅ〜も！？ ほんとうにありがとう！ 大好き〜！" : $"{amt:N0}ふじゅ〜、ありがとう〜！ ちゃんと届いてるよ！")
                    : $"{name}さん、{amt:N0}ふじゅ〜ありがとう〜！";
                Say(line, mine ? 5f : 3.6f);
                _thanksGap = mine ? 5.5f : 8f;
                if (mine) { _waveT = 2.2f; _jumpT = 0.6f; Cheer(0.7f); FxThanks(amt); }
                else _waveT = 1.2f;
            }
        }

        public static void Add(Comment c)
        {
            c.t = Time.unscaledTime;
            Comments.Add(c);
            if (Comments.Count > 40) Comments.RemoveAt(0);
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
