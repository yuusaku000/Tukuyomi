using System;
using System.Collections;
using System.Collections.Generic;

using System.Globalization;
using System.IO;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// ログインとログアウト。初回は大鳥居から、2回目からは前回ログアウトした場所から始める。
    /// 20秒ごとと、終わるとき・ほかの画面に切りかえたときに、いまの場所を保存する。
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public PlayerController player;
        public OrbitCamera cam;
        public Transform spawn;

        SaveData _data;
        float _saveTimer;
        bool _shotMode;

        public static GameSession Instance { get; private set; }

        /// <summary>持ち物と着ているもの（ショップ・着がえから使う）。</summary>
        public SaveData Data => _data;

        /// <summary>買い物や着がえのあと、すぐ保存する（確認用の起動でも持ち物は残す）。</summary>
        public void SaveItems()
        {
            if (_data == null) return;
            SaveStore.Save(_data);
        }

        void Awake() => Instance = this;

        void Start()
        {
            _shotMode = Ts.ShotMode;
            // ふつうに起動したときは、この PC の画面の大きさの全画面で開く（確認用の小さな窓の設定が残っていても。
            // 16:9 以外の画面で、絵がのびたり小さな窓になったりしないように）
            if (!_shotMode && Ts.Arg("-screen-width") == null && Ts.Arg("-screen-fullscreen") == null)
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            Quality.Load();
            SoundPrefs.Apply();
            Sfx.Ensure();
            Application.runInBackground = true;
            // --fresh は「保存した場所を使わない」。--fresh-all は持ち物もふくめて初めから（確認用）
            _data = Ts.Flag("--fresh-all") ? new SaveData() : SaveStore.Load();
            if (Ts.Flag("--fresh")) _data.has = false;
            if (_data.owned == null) _data.owned = new List<string>();
            if (_data.wearing == null) _data.wearing = new List<string>();
            if (_data.fish == null) _data.fish = new List<FishRecord>();
            FishBook.RollWeek(_data);
            if (Catalog.Get(_data.clothes) == null) _data.clothes = Catalog.StartClothes;
            bool first = !_data.has;
            // 演出を作る前から遊んでいた人は、初めてのログインの演出をしない
            if (_data.has && !_data.introStarted && !_data.introDone) _data.introDone = true;
            bool intro = !_data.introDone && !_shotMode;
            var ward = player.GetComponent<Wardrobe>();
            if (ward != null) ward.Wear(_data.clothes, _data.wearing);
            if (ward != null && _data.hairSet) ward.SetHair(_data.hairColor, _data.hairStyle);

            var warp = Ts.Arg("--warp");
            var z = warp != null ? Zone.Find(warp) : null;
            if (z != null)
                z.WarpTo(player, cam);
            else if (_data.has && _data.y > -5f && Mathf.Abs(_data.x) < 600 && Mathf.Abs(_data.z) < 600)
            {
                player.Teleport(new Vector3(_data.x, _data.y + 0.1f, _data.z), _data.yaw);
                cam.yaw = _data.camYaw; cam.pitch = _data.camPitch; cam.distance = _data.camDist; cam.firstPerson = _data.firstPerson;
                cam.Snap();
            }
            else
                player.Teleport(spawn.position, spawn.eulerAngles.y);

            ApplyCam(Ts.Arg("--cam"));

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (first) _data.firstLogin = now;
            _data.lastLogin = now;
            _data.logins++;
            if (!intro) Hud.Welcome(first, Ts.Arg("--from"));
            // 初ログインボーナスは、月見商店街に着いたときに受け取る（Hud.Intro）
            Hud.KeepNightOnStart(_data);
            if (intro) Hud.StartIntro();

            Vr.OnGameStart(); // 画面か VR か（--vr・--vr-sim・--screen、なければえらぶ板）
            if (Ts.Arg("--shot") != null) StartCoroutine(Shot(Ts.Arg("--shot"), Ts.ArgFloat("--shot-delay", 6f)));
            if (Ts.Arg("--tour") != null) StartCoroutine(Tour(Ts.Arg("--tour"), Ts.Arg("--tour-dir") ?? Ts.DataDir));
            else if (Ts.Flag("--check")) BeginCheck();
        }

        void ApplyCam(string c)
        {
            if (string.IsNullOrEmpty(c)) return;
            var p = c.Split(',');
            if (p.Length >= 1 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) cam.yaw = y;
            if (p.Length >= 2 && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pi)) cam.pitch = pi;
            if (p.Length >= 3 && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) cam.distance = d;
            cam.Snap();
        }

        /// <summary>
        /// 確認用：いくつもの場所を1回の起動でまわって画面を保存する。
        /// --tour "torii|13:00|0,10,7;shopping|20:30;plaza|19:00||map" --tour-dir 保存先
        /// </summary>
        // ───────── 一括確認（--check）─────────
        // ほぼ全部の場面を1回の起動で撮り、場面ごとの速さ（毎秒の回数・CPU・GPU の時間）と、出たエラーを一覧（report.md）にする。
        // 記録は %APPDATA%\Tsukuyomi\check\（ふだんの save.json の写しで遊ぶ）。撮った絵と一覧は check\report_日時\。
        // --vr-sim を足すと VR の場面（手・板を指す・ワープ・回る）も撮る。--check-only plaza,live で場面をしぼれる（場所か動きの名前）。

        static readonly string[] CheckScenes =
        {
            "torii|20:00", "torii|20:00||talk", "shopping|20:00", "shopping|20:00||shop:yukata", "shopping|20:00||wear", "shopping|20:00||shop:komono",
            "plaza|21:00", "plaza|21:00||jet|2.2", "plaza|21:00||map", "plaza|21:00||menu:0", "plaza|21:00||menu:7", "plaza|21:00||guide:0", "plaza|21:00||exchange",
            "fishing|16:00", "fishing|16:00||fish:bite", "fishing|16:00||book", "fishing|16:00||rank", "fishing|16:00||sit",
            "sakura|12:00", "upper|18:00", "bamboo|12:00", "lookout|21:00", "onsen|21:00", "palace|21:00", "shitamachi|21:00",
            "stage|21:00||live:s1:chorus:1", "stage|21:00||live:s2:drop:1:c3", "stage|21:00||live:s3:chorus:2:auto",
            "bamboo|12:00||kassen", "bamboo|12:00||setsuna:0:1:fight", "bamboo|12:00||setsuna:2:2:ko:3",
            "bamboo|12:00||sengoku:0:view", "bamboo|12:00||sengokum:fight", "bamboo|12:00||sengokum:overview",
            "stage|21:00||live:s1:chorus:1:audioreset", "stage|21:00||livestop",
            "shopping|20:00||intro:dress", // はじめての着がえ（板が残るので最後に）
        };
        static readonly string[] CheckVrScenes = { "plaza|21:00||vr:hands", "plaza|21:00||vr:point", "plaza|21:00||vr:warp", "plaza|21:00||vr:turn" };

        readonly List<string> _checkRows = new List<string>();
        readonly List<string> _checkErrors = new List<string>();
        string _checkScene = "起動";
        bool _checking;

        void BeginCheck()
        {
            _checking = true;
            var list = new List<string>(CheckScenes);
            if (Vr.On) list.AddRange(CheckVrScenes);
            var only = Ts.Arg("--check-only");
            if (!string.IsNullOrEmpty(only))
            {
                var keep = new HashSet<string>(only.Split(','));
                list.RemoveAll(x =>
                {
                    var q = x.Split('|');
                    return !keep.Contains(q[0]) && !(q.Length > 3 && keep.Contains(q[3].Split(':')[0]));
                });
            }
            var dir = Ts.Arg("--tour-dir") ?? Path.Combine(Ts.DataDir, "report_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            Debug.Log($"[Check] {list.Count} 場面を撮ります → {dir}");
            StartCoroutine(Tour(string.Join(";", list), dir));
        }

        void OnEnable() => Application.logMessageReceived += CheckLog;
        void OnDisable() => Application.logMessageReceived -= CheckLog;

        void CheckLog(string msg, string stack, LogType type)
        {
            if (!_checking || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            if (msg.Contains("OpenXR-Loader") || msg.Contains("D3D11 device creation")) return; // VR の機器がない PC で毎回出るもの
            string first = msg.Split('\n')[0];
            string at = type == LogType.Exception && !string.IsNullOrEmpty(stack) ? "　" + stack.Split('\n')[0].Trim() : "";
            if (_checkErrors.Count < 300) _checkErrors.Add($"| {_checkScene} | {type} | {(first + at).Replace("|", "／")} |");
        }

        void WriteCheckReport(string dir)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# ツクヨミ 一括確認 {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"- 画面 {Screen.width}×{Screen.height}　{(Vr.On ? "VR" : "画面モード")}　{SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"- 場面 {_checkRows.Count}　エラー {_checkErrors.Count}");
            sb.AppendLine();
            sb.AppendLine("## 場面ごとの速さ");
            sb.AppendLine();
            sb.AppendLine("| # | 場面 | 毎秒 | CPU ms | GPU ms | エラー |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in _checkRows) sb.AppendLine(r);
            sb.AppendLine();
            sb.AppendLine("## エラー");
            sb.AppendLine();
            if (_checkErrors.Count == 0) sb.AppendLine("なし");
            else { sb.AppendLine("| 場面 | 種類 | 内容 |"); sb.AppendLine("|---|---|---|"); foreach (var e in _checkErrors) sb.AppendLine(e); }
            var path = Path.Combine(dir, "report.md");
            File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));
            Debug.Log($"[Check] 一覧 → {path}　エラー {_checkErrors.Count}");
        }

        IEnumerator Tour(string spec, string dir)
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(4f);
            int i = 0;
            foreach (var item in spec.Split(';'))
            {
                var p = item.Split('|');
                _checkScene = item;
                int err0 = _checkErrors.Count;
                Hud.CloseShop();
                Hud.CloseTalk();
                Hud.CloseGuide(); Hud.CloseExchange();
                Kassen.ResetForTour();
                Hud.CloseBook();
                Fishing.I?.Stop();
                Rest.I?.Stand();

                var z = Zone.Find(p[0]);
                if (z != null) z.WarpTo(player, cam);
                else if (p[0].StartsWith("@"))
                {
                    var xz = p[0].Substring(1).Split(',');
                    float x = float.Parse(xz[0], CultureInfo.InvariantCulture), zz = float.Parse(xz[1], CultureInfo.InvariantCulture);
                    float ground = Island.Height(x, zz);
                    // "@x,z,y" なら、その高さから下を見る（建物の中の階など）
                    float from = xz.Length > 2 ? float.Parse(xz[2], CultureInfo.InvariantCulture) + 1.5f : 200f;
                    if (Physics.Raycast(new Vector3(x, from, zz), Vector3.down, out var hit, 400, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
                    player.Teleport(new Vector3(x, ground + 0.1f, zz), 0);
                }
                if (p.Length > 1 && p[1].Length > 0) Ts.SetTime(p[1]);
                if (p.Length > 2) ApplyCam(p[2]);
                Hud.OpenMap(p.Length > 3 && p[3] == "map");
                if (p.Length > 3 && p[3].StartsWith("menu:") && int.TryParse(p[3].Substring(5), out var mst)) Hud.MenuStart = mst;
                Hud.OpenMenu(p.Length > 3 && p[3].StartsWith("menu"));
                // shop:gofuku[:品物] でお店の画面、wear で着がえ、buy:品物 で本当に買う（ツクヨミのふじゅ〜で）
                if (p.Length > 3 && p[3].StartsWith("shop:")) { var a = p[3].Split(':'); Hud.OpenShopFor(a[1], a.Length > 2 ? a[2] : null, true); }
                if (p.Length > 3 && p[3] == "wear") Hud.OpenWardrobe();
                if (p.Length > 3 && p[3].StartsWith("exchange")) Hud.OpenExchange(p[3].StartsWith("exchange:") ? int.Parse(p[3].Substring(9)) : 0);   // exchange[:金額] で両替の板（金額があれば本当に両替）
                // KASSEN：kassen で入口の板、setsuna:相手:強さ[:fight|combo|just|ko|result] で試合のその場面
                if (p.Length > 3 && p[3] == "kassen") Kassen.OpenMenu();
                if (p.Length > 3 && p[3].StartsWith("sengokum:")) { var a = p[3].Split(':'); Kassen.BeginSengoku(true, a.Length > 2 ? int.Parse(a[2]) : 1, a.Length > 3 ? int.Parse(a[3]) : -1); Sengoku.TestMatch(a[1]); }
                if (p.Length > 3 && p[3].StartsWith("sengoku:")) { var a = p[3].Split(':'); Kassen.BeginSengoku(); Sengoku.TestPose(a.Length > 1 ? int.Parse(a[1]) : 0, a.Length > 2 ? a[2] : "view"); }
                if (p.Length > 3 && p[3].StartsWith("setsuna:")) { var a = p[3].Split(':'); Kassen.BeginMatch(int.Parse(a[1]), int.Parse(a[2]), a.Length > 4 ? int.Parse(a[4]) : 0); if (a.Length > 3) Setsuna.TestPose(a[3]); }
                if (p.Length > 3 && p[3].StartsWith("buy:")) Hud.TestBuy(p[3].Substring(4));
                // 段階3：fish:ready|charge|wait|bite|fight|catch[:魚] で釣り、book / rank で図鑑と番付、sit[:秒] で近くの席に座る
                bool again = false;
                if (p.Length > 3 && p[3].StartsWith("fish:")) { Fishing.I?.Test(p[3].Substring(5)); again = true; }
                if (p.Length > 3 && p[3] == "talk") Hud.TestTalk();
                if (p.Length > 3 && p[3] == "reset") { _shotMode = false; ResetData(true); _shotMode = true; } // 確認用：記録を消す（起動しなおさない）
                // 初めてのログイン：intro（ヤチヨのセリフ）・intro:dress（無料の着がえ）・intro:bye（夜の景色のあと）・bonus（初ログインボーナス）
                if (p.Length > 3 && (p[3].StartsWith("intro") || p[3] == "bonus")) Hud.TestIntro(p[3] == "bonus" ? "bonus" : p[3].Length > 6 ? p[3].Substring(6) : "");
                if (p.Length > 3 && p[3] == "night") YachiyoNpc.I?.MakeNight();
                // jet：ハンマーのジェットを2秒噴いて、どれだけ上がったかを記録（--check のときだけ解放する。ふだんの記録は変えない）
                if (p.Length > 3 && p[3] == "jet") StartCoroutine(JetTest());
                // audioreset：音の出口が変わったときと同じように、音の仕組みを作りなおす（AudioRecover が鳴らしなおすかの確認）
                if (p.Length > 3 && p[3] == "audioreset") StartCoroutine(AudioResetTest());
                // livestop：ライブ中にメニューの「つづける → ライブをやめる → はい」
                if (p.Length > 3 && p[3] == "livestop") { Hud.OpenMenu(true); LiveShow.StopNow(); Hud.OpenMenu(false); Debug.Log($"[Tour] livestop running={LiveShow.Running}"); }
                // 段階4：live:秒[:c番号][:tip][:ask] ／ live:s1:chorus:2[+秒]… でライブのその場面へ（音は出さない）
                if (p.Length > 3 && p[3].StartsWith("live:") && LiveShow.I != null)
                {
                    var raw = p[3].Substring(5).Split(':');
                    // set=0,2,1（曲順）・fx=dragon（演出）・open=gate（登場）を先に読む
                    var rest = new System.Collections.Generic.List<string>();
                    foreach (var x in raw)
                    {
                        if (x.StartsWith("set=")) LiveShow.ForceSet = LiveShow.ParseSet(x.Substring(4));
                        else if (x.StartsWith("fx=")) LiveShow.ForceFx = x.Substring(3);
                        else if (x.StartsWith("open=")) LiveShow.ForceOpen = x.Substring(5);
                        else rest.Add(x);
                    }
                    var a = rest.ToArray();
                    int k = 0; float at;
                    if (a[0] == "s1" || a[0] == "s2" || a[0] == "s3")
                    {
                        string liveSpec = a[0] + ":" + a[1] + ":" + (a.Length > 2 && int.TryParse(a[2].Split('+')[0], out var nth) ? nth : 1);
                        float plus = 0;
                        var plusPart = a.Length > 2 ? a[2] : a[1];
                        if (plusPart.Contains("+")) float.TryParse(plusPart.Split('+')[1], NumberStyles.Float, CultureInfo.InvariantCulture, out plus);
                        at = LiveShow.I.TimeOf(liveSpec, plus);
                        k = 3;
                    }
                    else { float.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out at); k = 1; }
                    LiveShow.I.Test(at);
                    LiveShow.I.Replan(); LiveShow.I.Test(at);
                    LiveShow.ForceShot = -1;
                    LiveShow.SetAutoCam(false);
                    for (; k < a.Length; k++)
                    {
                        if (a[k].StartsWith("c") && int.TryParse(a[k].Substring(1), out var shot)) { LiveShow.SetAutoCam(true); LiveShow.ForceShot = shot; }
                        else if (a[k] == "auto") LiveShow.SetAutoCam(true);
                        else if (a[k] == "tip") LiveShow.I.TestTip(1000);
                        else if (a[k] == "ask") Hud.TestTipAsk();
                        else if (a[k] == "audioreset") StartCoroutine(AudioResetTest());
                    }
                    again = true;
                }
                if (p.Length > 3 && (p[3] == "book" || p[3] == "rank")) Hud.OpenBook(p[3] == "book" ? 0 : 1);
                if (p.Length > 3 && p[3] == "menutest") Hud.TestMenu();
                if (p.Length > 3 && p[3] == "mapclose") Hud.TestCloseMap();
                if (p.Length > 3 && p[3].StartsWith("guide")) Hud.OpenGuide(p[3].Length > 6 && int.TryParse(p[3].Substring(6), out var gpg) ? gpg : 0);
                if (p.Length > 3 && p[3].StartsWith("sit")) { Rest.I?.TestSit(p[3].Length > 4 && float.TryParse(p[3].Substring(4), NumberStyles.Float, CultureInfo.InvariantCulture, out var si) ? si : 0f); again = true; }
                if (again && p.Length > 2) ApplyCam(p[2]);
                // VR（--vr-sim）：vr:hands|point|click|warp|go|turn|smooth|walk|settings
                if (p.Length > 3 && p[3].StartsWith("vr:")) Vr.Test(p[3].Substring(3));
                if (p.Length > 3 && p[3].StartsWith("choice")) Vr.TestChoice(p[3] == "choice:vr"); // 起動時の「画面／VR」の板
                cam.firstPerson = p.Length > 4 && p[4] == "fp";
                if (p.Length > 3 && p[3] == "perf") { yield return new WaitForSeconds(2f); yield return Perf.Run(p[0]); } // 重さをはかる（物を1つずつ消してくらべる）
                // 6つ目で待ち秒数を変えられる（食べている途中を撮るときなど）
                float wait = p.Length > 5 && float.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var wv) ? wv : 3.5f;
                yield return new WaitForSeconds(Mathf.Max(0.2f, wait - 1f));
                int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
                yield return new WaitForSeconds(Mathf.Min(1f, wait));

                FrameTimingManager.CaptureFrameTimings();
                var ft = new FrameTiming[1];
                string tm = FrameTimingManager.GetLatestTimings(1, ft) > 0 ? $" cpu={ft[0].cpuFrameTime:0.0}ms main={ft[0].cpuMainThreadFrameTime:0.0}ms render={ft[0].cpuRenderThreadFrameTime:0.0}ms gpu={ft[0].gpuFrameTime:0.0}ms" : "";
                float fps = (Time.frameCount - f0) / (Time.realtimeSinceStartup - t0);
                Debug.Log($"[Tour] {p[0]} fps={fps:0}{tm}");
                bool hasFt = tm.Length > 0;
                _checkRows.Add($"| {i:00} | {item} | {fps:0} | {(hasFt ? ft[0].cpuFrameTime.ToString("0.0") : "-")} | {(hasFt ? ft[0].gpuFrameTime.ToString("0.0") : "-")} | {(_checkErrors.Count > err0 ? (_checkErrors.Count - err0).ToString() : "")} |");
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{i:00}_{p[0].Replace("@", "at").Replace(",", "_")}.png"));
                yield return new WaitForSeconds(0.8f);
                i++;
            }
            if (_checking) { _checkScene = "終わり"; WriteCheckReport(dir); }
            Application.Quit();
        }

        IEnumerator JetTest()
        {
            if (Ts.Flag("--check") && _data != null) _data.hammerJet = true;
            yield return new WaitForSeconds(0.5f);
            float y0 = player.Position.y;
            PlayerController.Fuel = 1f; PlayerController.TestJetT = 2f;
            yield return new WaitForSeconds(2.1f);
            Debug.Log($"[Tour] jet 解放={Kassen.JetUnlocked} 上がった高さ {player.Position.y - y0:0.0}m 燃料 {PlayerController.Fuel * 100:0}%");
        }

        IEnumerator AudioResetTest()
        {
            yield return new WaitForSeconds(1.5f);
            int Count() { int n = 0; foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (a.isPlaying) n++; return n; }
            int before = Count();
            var was = new List<AudioSource>();
            foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (a.isPlaying) was.Add(a);
            AudioSettings.Reset(AudioSettings.GetConfiguration());
            int right = Count();
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[Tour] audioreset 鳴っていた音 {before} → 作りなおした直後 {right} → 2秒後 {Count()}");
            foreach (var a in was)
                if (a != null) Debug.Log($"[Tour] audioreset 音 {a.name} clip={(a.clip ? a.clip.name : "なし")} load={(a.clip ? a.clip.loadState.ToString() : "-")} 鳴っている={a.isPlaying} 有効={a.isActiveAndEnabled} time={a.time:0.0} vol={a.volume:0.00}");
        }

        IEnumerator Shot(string path, float delay)
        {
            yield return new WaitForSeconds(delay);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(1.5f);
            Application.Quit();
        }

        void Update()
        {
            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer > 20f) Save();
        }

        public void Save()
        {
            _saveTimer = 0;
            if (_shotMode || player == null || _resetting) return;
            var p = player.Position;
            if (player.InWater) return; // 水の中は保存しない（岸にもどされる途中のため）
            if (Rest.Sitting || (SkyShip.I != null && SkyShip.I.OnDeck(p))) { SaveStore.Save(_data); return; } // 遊覧船の上も場所は保存しない // 座っている間は場所を変えない（ソファの中から始まらないように）
            _data.has = true;
            _data.x = p.x; _data.y = p.y; _data.z = p.z; _data.yaw = player.Yaw;
            _data.camYaw = cam.yaw; _data.camPitch = cam.pitch; _data.camDist = cam.distance; _data.firstPerson = cam.firstPerson;
            SaveStore.Save(_data);
        }

        bool _resetting;

        /// <summary>
        /// データの初期化：遊んだ記録（save.json）だけを消して、ツクヨミを起動しなおす（初めてのログインから）。
        /// 音量・画質・操作・VR の設定（PlayerPrefs）は残す。写真も残す。
        /// </summary>
        public void ResetData(bool test = false)
        {
            _resetting = true;
            _data = new SaveData();
            SaveStore.Save(_data);
            try
            {
                // 今のツクヨミが終わってから起動しなおす（2秒待つ）。VR で遊んでいたら VR で
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                var args = new System.Collections.Generic.List<string>();
                var all = Environment.GetCommandLineArgs();
                for (int i = 1; i < all.Length; i++) if (all[i] != "--vr" && all[i] != "--screen") args.Add("\"" + all[i].Replace("\"", "") + "\"");
                args.Add(Vr.On && !Vr.Sim ? "--vr" : "--screen");
                var cmd = $"/c ping -n 3 127.0.0.1 >nul & start \"\" \"{exe}\" {string.Join(" ", args)}";
                Debug.Log("[Reset] 記録を消しました。起動しなおす命令: cmd.exe " + cmd);
                if (test) return; // 確認用（tour の reset）：起動しなおさない
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", cmd)
                { CreateNoWindow = true, UseShellExecute = false, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception e) { Debug.LogWarning("[Reset] 起動しなおせませんでした: " + e.Message); }
            if (!test) Application.Quit();
        }

        public void Logout()
        {
            Save();
            Application.Quit();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) Save();
            // ほかの画面を見ている間は、PCに負担をかけない
            Application.targetFrameRate = focus || _shotMode ? -1 : 20;
        }

        void OnApplicationQuit() => Save();
    }
}
