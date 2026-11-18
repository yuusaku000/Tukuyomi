using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 画面の表示（IMGUI）。スマコン（AR）ごしに見ているような見た目：
    /// 画面の四隅のかぎ、上の方位の帯（名所の向きと距離）、左上の接続表示、左下の座標、右下のキー、
    /// 一人称の照準、場所に入ったときの名前、お知らせ、Esc のメニュー、M の地図とワープ。
    /// </summary>
    public partial class Hud : MonoBehaviour
    {
        public PlayerController player;
        public OrbitCamera cam;
        public Vector2 worldMin = new Vector2(-185, -170), worldMax = new Vector2(185, 330);

        /// <summary>メニューか地図を開いている間は true（歩けない・見回せない）。</summary>
        public static bool Blocked { get; private set; }

        /// <summary>一人称のまん中の照準（メニューでオン・オフ。PlayerPrefs に残す）。</summary>
        /// <summary>上の方位の帯（メニューでオン・オフ。PlayerPrefs に残す）。</summary>
        public static bool CompassOn
        {
            get => PlayerPrefs.GetInt("tsukuyomi_compass", 1) == 1;
            set { PlayerPrefs.SetInt("tsukuyomi_compass", value ? 1 : 0); PlayerPrefs.Save(); }
        }


        public static bool CrosshairOn
        {
            get => PlayerPrefs.GetInt("tsukuyomi_crosshair", 1) == 1;
            set { PlayerPrefs.SetInt("tsukuyomi_crosshair", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static Hud _i;
        static string _toast, _bannerTitle, _bannerSub;
        static float _toastT, _bannerT, _bannerLen = 3.5f;

        bool _menu, _map, _clicked;
        static bool _guideAuto;
        Zone _zone;
        Texture2D _px, _mapTex;
        GUIStyle _label;
        Font _mono;
        float _hintT = 14f;
        bool _debug = Ts.Flag("--debug");
        float _fps, _fpsT;
        int _fpsFrames;

        /// <summary>F12：画面をそのまま写真にする（ピクチャ\Tsukuyomi）。デバッグ表示も写る。</summary>
        void TakePhoto()
        {
            try
            {
                var dir = PhotoDir;
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, "tsukuyomi_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg");
                StartCoroutine(SavePhoto(path));
                _photoMsg = "写真を保存しました ▸ " + path;
                _photoT = 0.3f;
            }
            catch (System.Exception e) { Toast("写真を保存できませんでした: " + e.Message); }
        }

        /// <summary>写真は横 1280 までに縮めて JPEG で保存する（Claude に見せるときの量を減らすため）。</summary>
        System.Collections.IEnumerator SavePhoto(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = null, small = null; RenderTexture rt = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                int w = Mathf.Min(1280, shot.width), h = Mathf.RoundToInt(shot.height * (w / (float)shot.width));
                rt = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(shot, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0); small.Apply();
                RenderTexture.active = prev;
                System.IO.File.WriteAllBytes(path, small.EncodeToJPG(88));
            }
            catch (System.Exception e) { Toast("写真を保存できませんでした: " + e.Message); }
            finally
            {
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (shot != null) Destroy(shot);
                if (small != null) Destroy(small);
            }
        }
        /// <summary>写真の置き場所。ピクチャはウイルスバスターのフォルダ保護で止められるので、保存データと同じ AppData にする。</summary>
        public static string PhotoDir => System.IO.Path.Combine(Ts.DataDir, "photos");

        string _photoMsg;
        float _photoT;

        static string TerraceName(float x, float z)
        {
            float h = Island.TerraceHeight(x, z);
            if (Island.Inside(x, z) < 0) return "海";
            for (int i = 3; i >= 0; i--)
                if (Mathf.Abs(h - Island.LevelTop(i)) < 0.3f) return i == 0 ? "L0 下町" : i == 1 ? "L1 中段" : i == 2 ? "L2 上段" : "L3 頂上";
            return h > Island.LevelTop(1) + 0.3f && h < Island.LevelTop(2) - 0.3f ? "丘/崖" : "崖";
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            var cur = t;
            while (cur != null && parts.Count < 4)
            {
                parts.Insert(0, cur.parent != null && cur.parent.parent != null ? cur.name + "#" + cur.GetSiblingIndex() : cur.name);
                cur = cur.parent;
            }
            return string.Join("/", parts);
        }

        void DrawDebug()
        {
            if (player == null || cam == null) return;
            var p = player.Position;
            var ct = cam.transform;
            string look = "（なし）";
            var hits = Physics.RaycastAll(ct.position, ct.forward, 400f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(player.transform)) continue;
                look = $"x {hit.point.x:0.0}  y {hit.point.y:0.00}  z {hit.point.z:0.0}   {hit.distance:0.0}m  ▸ {PathOf(hit.transform)}";
                break;
            }
            float ws = Island.WaterSurface(p.x, p.z);
            var zone = Zone.At(p);
            var lines = new[]
            {
                $"DEBUG [F3]   ver {Application.version}   fps {_fps:0}   gfx {Quality.Name}   {Screen.width}x{Screen.height}" + (PlayerController.Fly ? "   FLY [F4]" : "   飛ぶ [F4]"),
                $"PLAYER  x {p.x:0.0}   y {p.y:0.00}   z {p.z:0.0}   向き {player.Yaw:0}°",
                $"CAMERA  yaw {Mathf.Repeat(cam.yaw, 360):0}°   pitch {cam.pitch:0}°   {(cam.firstPerson ? "一人称" : "三人称")}   距離 {cam.distance:0.0}",
                $"GROUND  地面 {Island.Height(p.x, p.z):0.00}   段 {TerraceName(p.x, p.z)}   水面 {(float.IsNaN(ws) ? "なし" : ws.ToString("0.00"))}   島のふちまで {Island.Inside(p.x, p.z):0.0}m",
                $"AREA    {(zone != null ? zone.id + " / " + zone.title : "（なし）")}",
                $"LOOK    {look}",
                $"TIME    {Ts.Now:yyyy-MM-dd HH:mm}   太陽 {RealTimeSky.SunElevation:0.0}°   夜 {RealTimeSky.Night:0.00}",
            };
            var r = new Rect(34, 214, 980, lines.Length * 24 + 20);
            Fill(r, new Color(0, 0, 0, 0.62f));
            Frame(r, new Color(1f, 0.85f, 0.3f, 0.8f), 1.5f);
            for (int i = 0; i < lines.Length; i++)
                Text(new Rect(r.x + 12, r.y + 10 + i * 24, r.width - 20, 24), lines[i], 17, i == 0 ? new Color(1f, 0.85f, 0.3f) : Color.white, TextAnchor.UpperLeft, false, FontStyle.Normal, false);
            // 画面のまんなか（LOOK の場所）
            var c = new Vector2(W / 2, H / 2);
            var col = new Color(1f, 0.85f, 0.3f, 0.95f);
            Fill(new Rect(c.x - 12, c.y - 1, 24, 2), col);
            Fill(new Rect(c.x - 1, c.y - 12, 2, 24), col);
        }

        static readonly Color Cyan = new Color(0.45f, 0.95f, 1f);
        static readonly Color Pink = new Color(1f, 0.5f, 0.8f);
        static readonly Color Gold = new Color(1f, 0.88f, 0.6f);
        static readonly Color Glass = new Color(0.02f, 0.06f, 0.13f, 0.5f);

        void Awake() => _i = this;

        public static void OpenMap(bool open)
        {
            if (_i != null) { _i._map = open; _i._menu = false; }
        }

        public static void OpenMenu(bool open)
        {
            if (_i != null && open) { _i._menu = true; _i._map = false; }
            else if (_i != null) _i._menu = false;
        }

        /// <summary>画面の表示をぜんぶ消す（H・メニュー）。景色とライブを見るため。</summary>
        public static bool HideAll;
        static float _hideHintT;

        public static void Toast(string text)
        {
            if (LiveShow.Running) return; // ライブの間はお知らせを出さない
            _toast = text;
            _toastT = 4f;
        }

        public static void Banner(string title, string sub, float seconds = 3.5f)
        {
            _bannerTitle = title;
            _bannerSub = sub;
            _bannerT = _bannerLen = seconds;
        }

        public static void Welcome(bool first, string from)
        {
            string via = from == "contact" ? "SMART CONTACT ▸ FULL DIVE" : from == "iroha" ? "IROHA PHONE ▸ LINK" : "TSUKUYOMI ▸ LOGIN";
            Banner(first ? "ツクヨミへようこそ" : "おかえりなさい", via, 5f);
            if (_i != null) _i._zone = null;
        }

        // ───────── 地図の絵 ─────────

        Texture2D MapTexture()
        {
            const int W = 210, H = 291;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[W * H];
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    float x = Mathf.Lerp(worldMin.x, worldMax.x, (i + 0.5f) / W);
                    float z = Mathf.Lerp(worldMin.y, worldMax.y, (j + 0.5f) / H);
                    float h = Island.Height(x, z);
                    Color c;
                    if (!float.IsNaN(Island.WaterSurface(x, z)) || h < 0.3f) c = h < -1f && Island.Inside(x, z) < -2 ? new Color(0.03f, 0.1f, 0.18f) : new Color(0.15f, 0.45f, 0.65f);
                    else if (h < 3f) c = new Color(0.3f, 0.36f, 0.42f);
                    else if (h < 12f) c = new Color(0.36f, 0.42f, 0.5f);
                    else if (h < 25f) c = new Color(0.44f, 0.5f, 0.58f);
                    else c = new Color(0.55f, 0.6f, 0.68f);
                    float hl = Island.Height(x - 1.2f, z + 1.2f);
                    c *= Mathf.Clamp(1f + (hl - h) * 0.1f, 0.7f, 1.3f);
                    // 段のふちを光らせる
                    if (Mathf.Abs(hl - h) > 2f) c = Color.Lerp(c, Cyan, 0.6f);
                    c.a = 1;
                    cols[j * W + i] = c;
                }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        // ───────── 操作 ─────────

        void Update()
        {
            if (Kassen.Busy)
            {
                // KASSEN の入口の板・試合のあいだは、島の表示と操作を止める（Esc なども KASSEN が使う）
                Blocked = true; _menu = _map = false;
                _toastT -= Time.unscaledDeltaTime;
                NoticeTick(); ExchangeTick(); GuideTick(); MenuTick(); BoardsTick();
                return;
            }
            Vr.ChoiceTick();
            if (Ts.ShotMode)
            {
                Blocked = ShopOpen || _book || _tipOpen || _exchange || _guide || _menu || _intro || _bonusOpen;
                GuideTick(); MenuTick();

                if (_menu || _map) { }
                Cursor.lockState = CursorLockMode.None;
                UpdateZone();
                return;
            }
            // 初めて島に入ったとき：遊び方の板を1回だけ開く（画面か VR かをえらんだあと）
            if (!_guideAuto && !Vr.Choosing && !_intro && GameSession.Instance != null && GameSession.Instance.Data != null)
            {
                _guideAuto = true;
                if (!GameSession.Instance.Data.guideSeen) OpenGuide();
            }
            // 初めてのログインの演出と、初ログインボーナスの板の間：ほかの操作はしない（人形を回すのだけ）
            if (_intro || _bonusOpen)
            {
                Blocked = true;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                TurnTick();
                UpdateZone();
                return;
            }
            if (Act.Back.Down && !Vr.Choosing)
            {
                if (_guide) CloseGuide();
                else if (_tipOpen) { if (!_tipBusy) _tipOpen = false; }
                else if (LiveShow.AutoCam) LiveShow.SetAutoCam(false);
                else if (_exchange) CloseExchange();
                else if (_talk) CloseTalk();
                else if (_shipAsk != null) { if (!_shipBusy) _shipAsk = null; }
                else if (ShopOpen) CloseShop();
                else if (_map) _map = false;
                else if (_book) _book = false;
                else if (_menu && MenuBack()) { }
                else if (!_menu && Fishing.Busy) { Fishing.I.Stop(); Toast("竿をしまいました"); }
                else if (!_menu && Rest.Sitting) Rest.I.Stand();
                else _menu = !_menu;
            }
            if (Act.Map.Down && !_menu && !_exchange && !_guide) { _map = !_map; _book = false; }
            if (Input.GetKeyDown(KeyCode.F3)) { _debug = !_debug; Toast(_debug ? "デバッグ表示オン（F3 で消す・F12 で写真）" : "デバッグ表示オフ"); }
            if (Input.GetKeyDown(KeyCode.F12)) TakePhoto();
            // 視点のリセット（画面モード）：カメラを自分のうしろへもどす（VR は Vr.Recenter）
            if (!Vr.On && Act.Recenter.Down && cam != null && player != null && !Kassen.Busy) { cam.yaw = player.transform.eulerAngles.y; cam.pitch = 14f; cam.Snap(); Toast("視点をリセットしました"); }
            if (Act.HideHud.Down && !_exchange && !_guide && !_menu && !_map && !ShopOpen && !_book && !_talk && !_tipOpen && _shipAsk == null) { HideAll = !HideAll; _hideHintT = HideAll ? 2.5f : 0; }
            _hideHintT -= Time.unscaledDeltaTime;
            bool usedC = !_exchange && !_guide && !_menu && LiveKeys();
            bool usedE = !_exchange && !_guide && !_menu && ActivityKeys();
            if (!_exchange && !_guide && !_menu) ShopKeys(usedE, usedC);
            Blocked = _menu || _map || ShopOpen || _book || _shipAsk != null || _talk || _tipOpen || _exchange || _guide || Vr.Choosing;

            if (Blocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (Act.ClickDown && GUIUtility.hotControl == 0) _clicked = true;
                bool lockIt = _clicked && Application.isFocused;
                Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !lockIt;
            }
            if (!Application.isFocused) _clicked = false;
            UpdateZone();
        }

        void UpdateZone()
        {
            EatCamTick();

            if (player != null && _bannerT <= 0)
            {
                var z = Zone.At(player.Position);
                if (z != _zone)
                {
                    _zone = z;
                    if (z != null) Banner(z.title, "AREA ▸ " + z.english);
                }
            }
            _toastT -= Time.unscaledDeltaTime;
            NoticeTick();
            ExchangeTick();
            BoardsTick();
            GuideTick();
            MenuTick();
            _bannerT -= Time.unscaledDeltaTime;
            _fpsFrames++; _fpsT += Time.unscaledDeltaTime;
            if (_fpsT > 0.5f) { _fps = _fpsFrames / _fpsT; _fpsFrames = 0; _fpsT = 0; }
            if (_photoT > 0) { _photoT -= Time.unscaledDeltaTime; if (_photoT <= 0) Toast(_photoMsg); }
            _hintT -= Time.unscaledDeltaTime;
        }

        // ───────── 描く道具 ─────────

        float S => Screen.height / 1080f;
        float W => Screen.width / S;
        const float H = 1080f;

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = old;
        }

        void Frame(Rect r, Color c, float t = 1.5f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        void Corners(Rect r, Color c, float len = 18f, float t = 3f)
        {
            Fill(new Rect(r.x, r.y, len, t), c); Fill(new Rect(r.x, r.y, t, len), c);
            Fill(new Rect(r.xMax - len, r.y, len, t), c); Fill(new Rect(r.xMax - t, r.y, t, len), c);
            Fill(new Rect(r.x, r.yMax - t, len, t), c); Fill(new Rect(r.x, r.yMax - len, t, len), c);
            Fill(new Rect(r.xMax - len, r.yMax - t, len, t), c); Fill(new Rect(r.xMax - t, r.yMax - len, t, len), c);
        }

        void Panel(Rect r, float a = 1f)
        {
            Fill(r, new Color(Glass.r, Glass.g, Glass.b, Glass.a * a));
            Frame(r, new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f * a), 1f);
            Corners(r, new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f * a));
            // うっすら走査線
            for (float y = r.y + 3; y < r.yMax; y += 6) Fill(new Rect(r.x + 2, y, r.width - 4, 1), new Color(Cyan.r, Cyan.g, Cyan.b, 0.025f * a));
        }

        void Text(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool mono = false, FontStyle style = FontStyle.Normal, bool glow = true)
        {
            _label.font = mono ? _mono : Ts.Font;
            _label.fontSize = size;
            _label.alignment = a;
            _label.fontStyle = style;
            if (glow)
            {
                _label.normal.textColor = new Color(c.r * 0.4f, c.g * 0.6f, c.b, c.a * 0.35f);
                GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), s, _label);
            }
            _label.normal.textColor = c;
            GUI.Label(r, s, _label);
        }

        bool Button(Rect r, string s, int size = 26)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f) : new Color(0.02f, 0.05f, 0.12f, 0.65f));
            Frame(r, new Color(Cyan.r, Cyan.g, Cyan.b, hover ? 0.95f : 0.45f), hover ? 2f : 1f);
            Fill(new Rect(r.x, r.y, 4, r.height), hover ? Pink : new Color(Pink.r, Pink.g, Pink.b, 0.6f));
            Text(new Rect(r.x + 8, r.y, r.width - 8, r.height), s, size, Color.white, TextAnchor.MiddleCenter);
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (hit) Sfx.Click();
            return hit;
        }

        // ───────── 画面 ─────────

        void OnGUI()
        {
            if (Kassen.Busy) return;
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            if (_mono == null) _mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New" }, 24);
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { font = Ts.Font, wordWrap = false, richText = false };
            GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1));
            float t = Time.unscaledTime;
            // 表示を消しているとき：開いている画面（メニューなど）だけ出す
            if (HideAll && !(_map || _menu || ShopOpen || _book || _shipAsk != null || _talk || _tipOpen))
            {
                if (_hideHintT > 0) Text(new Rect(0, H - 60, W, 30), "表示を消しました（H でもどす）", 18, new Color(1, 1, 1, Mathf.Clamp01(_hideHintT)), TextAnchor.MiddleCenter);
                GUI.matrix = Matrix4x4.identity;
                return;
            }

            // 板（メニュー・遊び方・両替）を開いている間は、画面に直接描く表示を出さない（板の上に重なるため）
            if (_menu || _guide || _exchange || _map || _book || _shipAsk != null || _tipOpen || _intro || _bonusOpen || _talk) { GUI.matrix = Matrix4x4.identity; return; }

            // 画面の四隅のかぎ（スマコンの視野）
            Corners(new Rect(14, 14, W - 28, H - 28), new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f), 70, 2);

            // 左上：接続表示・時刻・場所（左下とおなじ、小さな文字だけ）
            bool blink = Mathf.Repeat(t, 1.2f) < 0.8f;
            Fill(new Rect(38, 38, 7, 7), blink ? new Color(0.4f, 1f, 0.6f, 0.85f) : new Color(0.2f, 0.5f, 0.3f, 0.85f));
            Text(new Rect(52, 31, 500, 22), "TSUKUYOMI AR ▸ LINK OK", 15, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f), TextAnchor.UpperLeft, true);
            var now = Ts.Now;
            Text(new Rect(36, 55, 120, 24), now.ToString("HH:mm"), 19, new Color(Gold.r, Gold.g, Gold.b, 0.85f), TextAnchor.UpperLeft, true);
            Text(new Rect(102, 56, 120, 24), RealTimeSky.PeriodName(now), 17, new Color(Gold.r, Gold.g, Gold.b, 0.8f));
            Text(new Rect(36, 82, 500, 24), "▸ " + (_zone != null ? _zone.title : "ツクヨミのどこか"), 17, new Color(1, 1, 1, 0.75f));

            // 座ってしばらくすると、表示を消して景色だけにする
            bool calm = Rest.Idle > 6f || LiveShow.AutoCam; // おまかせカメラの間も表示を消して景色だけにする
            if (!Blocked && calm && !LiveShow.AutoCam && !BoardHints) DrawActivity();
            if (!Blocked && !calm)
            {
                if (CompassOn && !BoardHints) Compass();
                // 釣り・座る・番付の立て札（VR では板：Hud.VrHints）
                if (!BoardHints) DrawActivity();
                // 左下：座標
                if (player != null)
                {
                    var p = player.Position;
                    Text(new Rect(36, H - 74, 600, 22), $"POS  X {p.x,7:0.0}   Z {p.z,7:0.0}   ALT {p.y,5:0.0}m", 16, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f), TextAnchor.UpperLeft, true);
                    Text(new Rect(36, H - 50, 600, 22), $"VIEW {(cam != null && cam.firstPerson ? "1ST PERSON" : "3RD PERSON")}   GFX {Quality.Name}", 16, new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f), TextAnchor.UpperLeft, true);
                }
                // 右下：キー
                Keys();
                // お店の AR 看板と「E で入る」
                if (!BoardHints) DrawShopSigns();
                // 近くの人の吹き出し
                if (!BoardHints) DrawBubbles();

                // 一人称の照準
                if (cam != null && cam.firstPerson && CrosshairOn)
                {
                    var c = new Vector2(W / 2, H / 2);
                    var col = new Color(Cyan.r, Cyan.g, Cyan.b, 0.85f);
                    Fill(new Rect(c.x - 2, c.y - 2, 4, 4), col);
                    Fill(new Rect(c.x - 18, c.y - 1, 9, 2), col); Fill(new Rect(c.x + 9, c.y - 1, 9, 2), col);
                    Fill(new Rect(c.x - 1, c.y - 18, 2, 9), col); Fill(new Rect(c.x - 1, c.y + 9, 2, 9), col);
                }
                // まだ操作していないとき
                if (Cursor.lockState != CursorLockMode.Locked && Mathf.Repeat(t, 1.4f) < 1.0f)
                    Text(new Rect(0, H - 150, W, 30), "CLICK TO SYNC  ▸  クリックで操作開始", 22, Gold, TextAnchor.MiddleCenter);
            }

            // 場所の名前（小さく、右上に）
            if (_bannerT > 0 && !string.IsNullOrEmpty(_bannerTitle) && !BoardHints)
            {
                float a = Mathf.Clamp01(_bannerT / 0.6f) * Mathf.Clamp01((_bannerLen - _bannerT) / 0.3f + 0.2f) * 0.9f;
                float open = Mathf.Clamp01((_bannerLen - _bannerT) / 0.5f);
                float y = 34, xr = W - 40, bw = 520;
                Text(new Rect(xr - bw, y, bw, 20), _bannerSub, 14, new Color(Cyan.r, Cyan.g, Cyan.b, a * 0.8f), TextAnchor.MiddleRight, true);
                Text(new Rect(xr - bw, y + 20, bw, 40), _bannerTitle, 28, new Color(1, 1, 1, a), TextAnchor.MiddleRight, false, FontStyle.Bold);
                Fill(new Rect(xr - 220 * open, y + 62, 220 * open, 1.5f), new Color(Pink.r, Pink.g, Pink.b, a * 0.8f));
            }

            // お知らせは板（Hud.Notice）で出す

            if (_debug) DrawDebug();
            if (_book || _shipAsk != null) { }
            GUI.matrix = Matrix4x4.identity;
        }

        /// <summary>上の方位の帯：方角と、名所の向き・距離。</summary>
        void Compass()
        {
            if (cam == null || player == null) return;
            float cw = 520, cx = W / 2, y = 30;
            var bar = new Rect(cx - cw / 2, y, cw, 30);
            Fill(bar, new Color(0.02f, 0.06f, 0.13f, 0.35f));
            Fill(new Rect(bar.x, bar.yMax - 1, bar.width, 1), new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f));
            float yaw = cam.transform.eulerAngles.y;
            const float fov = 120f;
            float Px(float bearing) => cx + Mathf.DeltaAngle(yaw, bearing) / (fov / 2) * (cw / 2);
            for (int d = 0; d < 360; d += 15)
            {
                float dd = Mathf.DeltaAngle(yaw, d);
                if (Mathf.Abs(dd) > fov / 2) continue;
                float x = Px(d);
                bool major = d % 90 == 0;
                float a = 1 - Mathf.Abs(dd) / (fov / 2);
                Fill(new Rect(x - 0.75f, bar.yMax - (major ? 10 : 5), 1.5f, major ? 10 : 5), new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f * a));
                if (major) Text(new Rect(x - 20, y + 1, 40, 18), d == 0 ? "N" : d == 90 ? "E" : d == 180 ? "S" : "W", 14, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, true);
            }
            var marks = new List<(Zone z, float dd, float dist, float bearing)>();
            foreach (var z in Zone.All)
            {
                if (!z.warpable || z == _zone) continue;
                var v = z.transform.position - player.Position; v.y = 0;
                float dist = v.magnitude;
                if (dist < 8 || dist > 450) continue;
                float bearing = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                float dd = Mathf.DeltaAngle(yaw, bearing);
                if (Mathf.Abs(dd) > fov / 2 - 4) continue;
                marks.Add((z, dd, dist, bearing));
            }
            marks.Sort((p, q) => Mathf.Abs(p.dd).CompareTo(Mathf.Abs(q.dd)));
            var placed = new List<(float x, int row)>();
            for (int i = 0; i < marks.Count; i++)
            {
                var (z, dd, dist, bearing) = marks[i];
                float x = Px(bearing);
                float a = 1 - Mathf.Abs(dd) / (fov / 2) * 0.7f;
                var c = z.mapColor; c.a = a;
                Fill(new Rect(x - 4, bar.yMax + 4, 8, 8), c);
                if (i >= 3) continue;
                int row = 0;
                foreach (var pl in placed) if (Mathf.Abs(pl.x - x) < 150 && pl.row == row) row++;
                if (row > 1) continue;
                placed.Add((x, row));
                float ly = bar.yMax + 8 + row * 30;
                if (row > 0) Fill(new Rect(x - 0.5f, bar.yMax + 8, 1, row * 30), new Color(c.r, c.g, c.b, a * 0.5f));
                Text(new Rect(x - 80, ly, 160, 18), z.title, 12, new Color(1, 1, 1, a * 0.85f), TextAnchor.UpperCenter);
                Text(new Rect(x - 80, ly + 14, 160, 15), $"{dist:0}m", 11, new Color(Cyan.r, Cyan.g, Cyan.b, a * 0.8f), TextAnchor.UpperCenter, true);
            }
            Fill(new Rect(cx - 1, y - 6, 2, 12), Pink);
        }

        void Keys()
        {
            var keys = new[] { ("WASD", "移動"), ("SHIFT", "走る"), ("SPACE", "ジャンプ"), ("V", "視点"), ("M", "地図"), ("C", "着がえ"), ("B", "図鑑"), ("H", "表示を消す"), ("ESC", "メニュー") };
            float a = _hintT > 0 ? 0.95f : 0.5f;
            float x = W - 36;
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                var (k, label) = keys[i];
                float kw = 12 + k.Length * 10, lw = label.Length * 17 + 8;
                x -= lw;
                Text(new Rect(x, H - 62, lw, 26), label, 17, new Color(1, 1, 1, a), TextAnchor.MiddleLeft);
                x -= kw + 6;
                var kr = new Rect(x, H - 62, kw, 26);
                Frame(kr, new Color(Cyan.r, Cyan.g, Cyan.b, a * 0.8f), 1f);
                Text(kr, k, 14, new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.MiddleCenter, true, FontStyle.Normal, false);
                x -= 18;
            }
        }

        void Warp(Zone z)
        {
            _returnItem = -1; // ワープしたら、メニューにはもどらない
            z.WarpTo(player, cam);
            _map = false;
            _zone = null;
            _bannerT = 0;
            Toast($"{z.title} にワープしました");
        }
    }
}
