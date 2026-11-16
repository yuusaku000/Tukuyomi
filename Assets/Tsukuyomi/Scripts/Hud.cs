using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 画面の表示（IMGUI）：左上のロゴと時刻と場所、場所に入ったときの大きな名前、
    /// Esc のメニュー（つづける・地図・ログアウト）、M の地図とワープ、ちょっとしたお知らせ。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public PlayerController player;
        public OrbitCamera cam;
        public Vector2 worldMin = new Vector2(-150, -100), worldMax = new Vector2(150, 262);

        /// <summary>メニューか地図を開いている間は true（歩けない・見回せない）。</summary>
        public static bool Blocked { get; private set; }

        static Hud _i;
        static string _toast, _bannerTitle, _bannerSub;
        static float _toastT, _bannerT;

        bool _menu, _map, _clicked;
        Zone _zone;
        Texture2D _px;
        GUIStyle _label;
        float _hintT = 12f;

        void Awake() => _i = this;

        public static void OpenMap(bool open)
        {
            if (_i != null) { _i._map = open; _i._menu = false; }
        }

        public static void Toast(string text)
        {
            _toast = text;
            _toastT = 4f;
        }

        public static void Banner(string title, string sub, float seconds = 3.5f)
        {
            _bannerTitle = title;
            _bannerSub = sub;
            _bannerT = seconds;
        }

        public static void Welcome(bool first, string from)
        {
            string via = from == "contact" ? "スマコンからフルダイブ" : from == "iroha" ? "彩葉のスマホから接続" : "TSUKUYOMI  ─  LOGIN";
            Banner(first ? "ツクヨミへようこそ" : "おかえりなさい", via, 5f);
            if (_i != null) _i._zone = null;
        }

        void Update()
        {
            if (Ts.ShotMode)
            {
                Blocked = false;
                Cursor.lockState = CursorLockMode.None;
                UpdateZone();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_map) _map = false;
                else _menu = !_menu;
            }
            if (Input.GetKeyDown(KeyCode.M) && !_menu) _map = !_map;
            Blocked = _menu || _map;

            if (Blocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0) _clicked = true;
                bool lockIt = _clicked && Application.isFocused;
                Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !lockIt;
            }
            if (!Application.isFocused) _clicked = false;
            UpdateZone();
        }

        void UpdateZone()
        {
            if (player != null && _bannerT <= 0)
            {
                var z = Zone.At(player.Position);
                if (z != _zone)
                {
                    _zone = z;
                    if (z != null) Banner(z.title, z.english);
                }
            }
            _toastT -= Time.unscaledDeltaTime;
            _bannerT -= Time.unscaledDeltaTime;
            _hintT -= Time.unscaledDeltaTime;
        }

        // ───────── 描画 ─────────

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

        void Text(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool shadow = true, FontStyle style = FontStyle.Normal)
        {
            _label.fontSize = size;
            _label.alignment = a;
            _label.fontStyle = style;
            if (shadow)
            {
                _label.normal.textColor = new Color(0, 0, 0, c.a * 0.65f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, _label);
            }
            _label.normal.textColor = c;
            GUI.Label(r, s, _label);
        }

        bool Button(Rect r, string s, int size = 30)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? new Color(1f, 0.55f, 0.75f, 0.92f) : new Color(0.12f, 0.1f, 0.3f, 0.88f));
            Fill(new Rect(r.x, r.yMax - 3, r.width, 3), new Color(0.5f, 0.95f, 1f, 0.8f));
            Text(r, s, size, Color.white, TextAnchor.MiddleCenter, false);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void OnGUI()
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { font = Ts.Font, wordWrap = false, richText = false };
            GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1));

            // 左上：ロゴ・時刻・場所
            Fill(new Rect(24, 24, 360, 128), new Color(0.05f, 0.04f, 0.16f, 0.62f));
            Fill(new Rect(24, 24, 6, 128), new Color(1f, 0.5f, 0.75f, 0.95f));
            Text(new Rect(44, 30, 340, 56), "ツクヨミ", 44, new Color(1f, 0.93f, 0.75f), TextAnchor.UpperLeft, true, FontStyle.Bold);
            var now = Ts.Now;
            Text(new Rect(46, 86, 340, 30), $"{RealTimeSky.PeriodName(now)}  {now:HH:mm}", 24, new Color(0.75f, 0.95f, 1f));
            Text(new Rect(46, 114, 340, 30), _zone != null ? _zone.title : "ツクヨミのどこか", 22, new Color(1f, 1f, 1f, 0.85f));

            // 右上：操作のヒント
            if (!Blocked)
            {
                string keys = Cursor.lockState == CursorLockMode.Locked
                    ? "WASD 移動   Shift 走る   Space ジャンプ   M 地図   Esc メニュー"
                    : "クリックで操作開始   M 地図   Esc メニュー";
                Text(new Rect(W - 1024, 30, 1000, 30), keys, 22, new Color(1, 1, 1, _hintT > 0 || Cursor.lockState != CursorLockMode.Locked ? 0.9f : 0.45f), TextAnchor.UpperRight);
            }

            // 場所の名前（大きく）
            if (_bannerT > 0 && !string.IsNullOrEmpty(_bannerTitle))
            {
                float a = Mathf.Clamp01(_bannerT / 0.6f);
                Text(new Rect(0, H * 0.26f, W, 90), _bannerTitle, 76, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, true, FontStyle.Bold);
                Fill(new Rect(W / 2 - 220, H * 0.26f + 96, 440, 2), new Color(1f, 0.6f, 0.8f, a * 0.9f));
                Text(new Rect(0, H * 0.26f + 104, W, 40), _bannerSub, 26, new Color(1f, 0.9f, 0.6f, a), TextAnchor.MiddleCenter);
            }

            // お知らせ
            if (_toastT > 0 && !string.IsNullOrEmpty(_toast))
            {
                float a = Mathf.Clamp01(_toastT / 0.5f);
                var r = new Rect(W / 2 - 420, H - 190, 840, 64);
                Fill(r, new Color(0.08f, 0.06f, 0.2f, 0.8f * a));
                Text(r, _toast, 28, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, false);
            }

            if (_map) DrawMap();
            else if (_menu) DrawMenu();
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawMenu()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.08f, 0.55f));
            var r = new Rect(W / 2 - 300, H / 2 - 290, 600, 580);
            Fill(r, new Color(0.07f, 0.06f, 0.2f, 0.94f));
            Fill(new Rect(r.x, r.y, r.width, 6), new Color(1f, 0.55f, 0.78f));
            Text(new Rect(r.x, r.y + 30, r.width, 70), "ツクヨミ", 56, new Color(1f, 0.93f, 0.75f), TextAnchor.MiddleCenter, true, FontStyle.Bold);
            Text(new Rect(r.x, r.y + 100, r.width, 30), "TSUKUYOMI  ─  MENU", 20, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleCenter);
            if (Button(new Rect(r.x + 90, r.y + 160, 420, 72), "つづける")) _menu = false;
            if (Button(new Rect(r.x + 90, r.y + 250, 420, 72), "地図とワープ（M）")) { _menu = false; _map = true; }
            if (Button(new Rect(r.x + 90, r.y + 340, 420, 72), "ログアウト（終わる）")) GameSession.Instance?.Logout();
            Text(new Rect(r.x, r.y + 440, r.width, 120),
                "WASD・矢印 歩く　Shift 走る　Space ジャンプ\nマウス 見回す　ホイール 近づく・離れる\nログアウトした場所から、次回は始まります",
                20, new Color(1, 1, 1, 0.75f), TextAnchor.UpperCenter, false);
        }

        void DrawMap()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.08f, 0.7f));
            float mh = 860f;
            float mw = mh * (worldMax.x - worldMin.x) / (worldMax.y - worldMin.y);
            var m = new Rect(W / 2 - (mw + 420) / 2, (H - mh) / 2, mw, mh);
            Fill(new Rect(m.x - 8, m.y - 8, m.width + 16, m.height + 16), new Color(0.6f, 0.9f, 1f, 0.5f));
            Fill(m, new Color(0.05f, 0.18f, 0.3f, 1f)); // 海

            Rect ToMap(Rect w)
            {
                float x0 = m.x + (w.xMin - worldMin.x) / (worldMax.x - worldMin.x) * m.width;
                float x1 = m.x + (w.xMax - worldMin.x) / (worldMax.x - worldMin.x) * m.width;
                float y0 = m.yMax - (w.yMax - worldMin.y) / (worldMax.y - worldMin.y) * m.height;
                float y1 = m.yMax - (w.yMin - worldMin.y) / (worldMax.y - worldMin.y) * m.height;
                return new Rect(x0, y0, x1 - x0, y1 - y0);
            }
            Vector2 ToMapP(Vector3 p) => new Vector2(
                m.x + (p.x - worldMin.x) / (worldMax.x - worldMin.x) * m.width,
                m.yMax - (p.z - worldMin.y) / (worldMax.y - worldMin.y) * m.height);

            // 島と川と回廊
            Fill(ToMap(new Rect(-130, 0, 260, 100)), new Color(0.82f, 0.78f, 0.68f));
            Fill(ToMap(new Rect(-130, 114, 260, 136)), new Color(0.82f, 0.78f, 0.68f));
            Fill(ToMap(new Rect(-130, 100, 260, 14)), new Color(0.2f, 0.5f, 0.75f));
            Fill(ToMap(new Rect(-3.5f, -74, 7, 74)), new Color(0.75f, 0.3f, 0.22f));
            Fill(ToMap(new Rect(-8, -74, 16, 12)), new Color(0.75f, 0.3f, 0.22f));
            Fill(ToMap(new Rect(-6, 8, 12, 92)), new Color(0.65f, 0.62f, 0.58f));
            Fill(ToMap(new Rect(-110, 40, 220, 8)), new Color(0.65f, 0.62f, 0.58f));

            Zone hover = null;
            foreach (var z in Zone.All)
            {
                if (!z.warpable) continue;
                var r = ToMap(z.Area);
                var c = z.mapColor; c.a = 0.55f;
                Fill(r, c);
                if (r.Contains(Event.current.mousePosition)) { hover = z; Fill(r, new Color(1, 1, 1, 0.25f)); }
                Text(r, z.title, 20, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            }

            // 大鳥居
            var t = ToMapP(new Vector3(0, 0, -82));
            Fill(new Rect(t.x - 14, t.y - 3, 28, 6), new Color(1f, 0.3f, 0.2f));
            Text(new Rect(t.x - 80, t.y + 6, 160, 26), "大鳥居", 18, Color.white, TextAnchor.UpperCenter);

            // 自分
            if (player != null)
            {
                var p = ToMapP(player.Position);
                float pulse = 9 + Mathf.Sin(Time.unscaledTime * 6) * 3;
                Fill(new Rect(p.x - pulse, p.y - pulse, pulse * 2, pulse * 2), new Color(1f, 0.4f, 0.7f, 0.5f));
                Fill(new Rect(p.x - 6, p.y - 6, 12, 12), Color.white);
                Text(new Rect(p.x - 60, p.y - 40, 120, 26), "いまここ", 18, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleCenter);
            }

            // 右：ワープの一覧
            var list = new Rect(m.xMax + 30, m.y, 380, mh);
            Fill(list, new Color(0.07f, 0.06f, 0.2f, 0.94f));
            Text(new Rect(list.x, list.y + 18, list.width, 50), "ワープ", 40, new Color(1f, 0.93f, 0.75f), TextAnchor.MiddleCenter, true, FontStyle.Bold);
            float y = list.y + 90;
            var zones = new System.Collections.Generic.List<Zone>(Zone.All);
            zones.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var z in zones)
            {
                if (!z.warpable) continue;
                if (Button(new Rect(list.x + 24, y, list.width - 48, 62), z.title, 26)) Warp(z);
                y += 74;
            }
            if (hover != null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Warp(hover);
                Event.current.Use();
            }
            Text(new Rect(list.x, list.yMax - 60, list.width, 40), "M・Esc で閉じる　地図の場所をクリックでも飛べます", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, false);
        }

        void Warp(Zone z)
        {
            z.WarpTo(player, cam);
            _map = false;
            _zone = null;
            _bannerT = 0;
            Toast($"{z.title} にワープしました");
        }
    }
}
