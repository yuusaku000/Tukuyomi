using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// 表示の「板」（uGUI）。画面モードと VR モードで同じ板を使う（VRChat のように）。
    ///  画面モード：画面に重ねて出す（Screen Space Overlay）。
    ///  VR モード ：目の前の空間に浮かべる（World Space）。頭の動きに少し遅れてついてくる。
    /// 新しく足す表示はこれで作る。IMGUI（Hud の OnGUI）の表示は VR モードを作るときに順に移す。
    /// 確かめるときは --board-world で、画面モードでも空間に浮かべて表示できる。
    /// </summary>
    public class Board : MonoBehaviour
    {
        public enum Mode { Screen, World }

        /// <summary>いまの表示のしかた。VR モードを作ったら、起動時の選択でここを World にする。</summary>
        public static Mode Current = Mode.Screen;

        // 色（Hud と同じ）
        public static readonly Color Cyan = new(0.45f, 0.95f, 1f);
        public static readonly Color Pink = new(1f, 0.5f, 0.8f);
        public static readonly Color Gold = new(1f, 0.88f, 0.6f);
        public static readonly Color Glass = new(0.02f, 0.06f, 0.13f, 0.72f);

        /// <summary>画面の基準の大きさ（Hud と同じく 1080 の高さで作る）。</summary>
        const float RefH = 1080f;
        /// <summary>空間に浮かべるとき、1ピクセルを何メートルにするか。</summary>
        const float MetersPerPixel = 0.0011f;

        public Canvas canvas;
        public RectTransform root;
        public CanvasGroup group;

        /// <summary>画面モードの置き場所（0〜1。0.5,0 なら下のまん中）と、そこからのずれ（ピクセル）。</summary>
        public Vector2 screenAnchor = new(0.5f, 0.5f), screenOffset;
        /// <summary>VR モードの置き場所（目から見て、右・上・前 のメートル）。</summary>
        public Vector3 worldOffset = new(0, 0, 1.3f);
        /// <summary>空間の決まった場所に置く板（看板など）。画面モードでも空間に出る。左右だけカメラの方を向く。</summary>
        public bool pinned;
        public Vector3 pinPos;
        public float pinScale = 0.01f;

        float _alpha = 1f;
        Vector3 _pos; Quaternion _rot; bool _placed;

        /// <summary>板の大きさはピクセル（1920×1080 の画面のつもり）で決める。</summary>
        public static Board Create(string name, Vector2 size, int order = 0)
        {
            EnsureEventSystem();
            var go = new GameObject("板：" + name);
            DontDestroyOnLoad(go);
            var b = go.AddComponent<Board>();
            b.canvas = go.AddComponent<Canvas>();
            b.canvas.sortingOrder = order;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            b.group = go.AddComponent<CanvasGroup>();

            var r = new GameObject("中身", typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(go.transform, false);
            r.sizeDelta = size;
            b.root = r;
            b.Apply();
            return b;
        }

        /// <summary>表示のしかた（画面／空間）を今の Current に合わせる。</summary>
        public void Apply()
        {
            var sc = GetComponent<CanvasScaler>();
            var ct = (RectTransform)canvas.transform;
            if (pinned)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                sc.dynamicPixelsPerUnit = 3f;
                ct.sizeDelta = root.sizeDelta;
                ct.localScale = Vector3.one * pinScale;
                ct.position = pinPos;
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
            }
            else if (Current == Mode.Screen)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, RefH);
                sc.matchWidthOrHeight = 1f;
                root.anchorMin = root.anchorMax = screenAnchor;
                root.pivot = screenAnchor;
                root.anchoredPosition = screenOffset;
            }
            else
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                sc.dynamicPixelsPerUnit = 3f; // 近くで見ても文字がぼやけないように
                ct.sizeDelta = root.sizeDelta;
                ct.localScale = Vector3.one * MetersPerPixel;
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
                _placed = false;
            }
        }

        /// <summary>見える度合い（0〜1）。0 にすると消える。</summary>
        public float Alpha
        {
            get => _alpha;
            set { _alpha = value; if (group != null) group.alpha = value; if (canvas != null) canvas.enabled = value > 0.001f; }
        }

        void LateUpdate()
        {
            if (!canvas.enabled) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (pinned)
            {
                var d = transform.position - cam.transform.position; d.y = 0;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
                return;
            }
            if (Current != Mode.World) return;
            var head = cam.transform;
            // 左右の向きだけついてくる（上下を向いても板は水平のまま）
            var yaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
            var want = head.position + yaw * worldOffset;
            var face = Quaternion.LookRotation(want - head.position, Vector3.up);
            if (!_placed) { _pos = want; _rot = face; _placed = true; }
            else
            {
                float k = 1 - Mathf.Exp(-4f * Time.unscaledDeltaTime);
                _pos = Vector3.Lerp(_pos, want, k);
                _rot = Quaternion.Slerp(_rot, face, k);
            }
            transform.SetPositionAndRotation(_pos, _rot);
        }

        // ───────── 部品 ─────────

        /// <summary>半透明の地（Hud のパネルと同じ色）。</summary>
        public static Image Panel(RectTransform parent, Rect r, Color? color = null)
        {
            var img = Node<Image>(parent, "地", r);
            img.color = color ?? Glass;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>四隅のかぎ（Hud のパネルと同じ見た目）。</summary>
        public static void Corners(RectTransform parent, Rect r, Color c, float len = 18, float th = 2)
        {
            Panel(parent, new Rect(r.x, r.y, len, th), c); Panel(parent, new Rect(r.x, r.y, th, len), c);
            Panel(parent, new Rect(r.xMax - len, r.y, len, th), c); Panel(parent, new Rect(r.xMax - th, r.y, th, len), c);
            Panel(parent, new Rect(r.x, r.yMax - th, len, th), c); Panel(parent, new Rect(r.x, r.yMax - len, th, len), c);
            Panel(parent, new Rect(r.xMax - len, r.yMax - th, len, th), c); Panel(parent, new Rect(r.xMax - th, r.yMax - len, th, len), c);
        }

        // ───────── ツクヨミ風の部品（扇の飾り・縦書きの名札・丸い輪・光る大きな文字）─────────

        public static readonly Color Teal = new(0.13f, 0.86f, 0.86f);
        public static readonly Color Vermilion = new(0.9f, 0.32f, 0.24f);
        public static readonly Color Paper = new(0.96f, 0.95f, 0.92f);
        public static readonly Color Ink = new(0.08f, 0.08f, 0.09f);
        public static readonly Color Night = new(0.03f, 0.05f, 0.12f, 0.82f);

        /// <summary>角の扇（細い線が放射状に広がる）。corner：0 左上・1 右上・2 左下・3 右下。</summary>
        public static void Fan(RectTransform parent, Rect r, int corner, Color c, float len = 70, int rays = 7)
        {
            bool right = corner == 1 || corner == 3, bottom = corner >= 2;
            var o = new Vector2(right ? r.xMax : r.x, bottom ? r.yMax : r.y);
            for (int i = 0; i < rays; i++)
            {
                float a = i / (float)(rays - 1) * 90f; // 0＝横、90＝縦
                bool edge = i == 0 || i == rays - 1;
                float l = edge ? len : len * (0.55f + 0.25f * Mathf.Sin(i * 1.7f));
                var img = Node<Image>(parent, "扇", new Rect(o.x, o.y, l, edge ? 1.6f : 1f));
                img.color = c; img.raycastTarget = false;
                var rt = (RectTransform)img.transform;
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = new Vector2(o.x, -o.y);
                // 画面の座標は上が＋。左上の角なら、右（0度）から下（-90度）へ広げる
                float deg = right ? 180f + a : -a;
                if (bottom) deg = right ? 180f - a : a;
                rt.localEulerAngles = new Vector3(0, 0, deg);
            }
            var dot = Node<Image>(parent, "要", new Rect(o.x - 3, o.y - 3, 6, 6));
            dot.sprite = DiscSprite; dot.color = c; dot.raycastTarget = false;
            for (int i = 1; i < 8; i++)
            {
                float a = i / 8f * 90f * Mathf.Deg2Rad;
                float x = Mathf.Cos(a) * len * 1.12f, y = Mathf.Sin(a) * len * 1.12f;
                var d = Node<Image>(parent, "弧", new Rect(o.x + (right ? -x : x) - 1.5f, o.y + (bottom ? -y : y) - 1.5f, 3, 3));
                d.sprite = DiscSprite; d.color = new Color(c.r, c.g, c.b, c.a * 0.6f); d.raycastTarget = false;
            }
        }

        /// <summary>四隅すべてに扇。</summary>
        public static void Fans(RectTransform parent, Rect r, Color c, float len = 70)
        {
            for (int i = 0; i < 4; i++) Fan(parent, r, i, c, len);
        }

        /// <summary>縦書きの白い名札（黒い明朝の字）。横に色の帯。barLeft で帯を左に。</summary>
        public static Text NameTag(RectTransform parent, Rect r, string text, Color bar, bool barLeft = false, int size = 30)
        {
            Panel(parent, r, Paper);
            Panel(parent, barLeft ? new Rect(r.x - 9, r.y, 9, r.height) : new Rect(r.xMax, r.y, 9, r.height), bar);
            Panel(parent, new Rect(r.x + 4, r.y + 4, r.width - 8, 1.2f), new Color(Ink.r, Ink.g, Ink.b, 0.5f));
            Panel(parent, new Rect(r.x + 4, r.yMax - 5, r.width - 8, 1.2f), new Color(Ink.r, Ink.g, Ink.b, 0.5f));
            var t = Label(parent, new Rect(r.x, r.y + 10, r.width, r.height - 20), Vertical(text), size, Ink, TextAnchor.UpperCenter);
            t.font = Mincho; t.lineSpacing = 0.92f; t.fontStyle = FontStyle.Bold;
            Destroy(t.GetComponent<Shadow>());
            return t;
        }

        /// <summary>縦書き用に1文字ずつ改行する。</summary>
        public static string Vertical(string s)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++) { if (i > 0) sb.Append('\n'); sb.Append(s[i] == 'ー' ? '｜' : s[i]); }
            return sb.ToString();
        }

        /// <summary>光る大きな斜体の文字（「12 HIT」「ROUND 1」など）。外側に同じ色のにじみ。</summary>
        public static Text Display(RectTransform parent, Rect r, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = Label(parent, r, text, size, c, align);
            t.font = DisplayFont;
            t.fontStyle = FontStyle.BoldAndItalic;
            Destroy(t.GetComponent<Shadow>());
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(c.r, c.g, c.b, 0.35f); o.effectDistance = new Vector2(3, -3);
            var o2 = t.gameObject.AddComponent<Outline>();
            o2.effectColor = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.4f, 0.8f); o2.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        /// <summary>細い輪（タイマーや丸いボタンの枠）。dotted で内側に点の輪も。</summary>
        public static void Ring(RectTransform parent, Vector2 center, float radius, Color c, bool dotted = true)
        {
            var a = Node<Image>(parent, "輪", new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2));
            a.sprite = RingSprite; a.color = c; a.raycastTarget = false;
            if (!dotted) return;
            int n = Mathf.Clamp((int)(radius * 0.5f), 12, 48);
            float r2 = radius * 0.8f;
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * Mathf.PI * 2;
                var d = Node<Image>(parent, "点", new Rect(center.x + Mathf.Cos(ang) * r2 - 1.5f, center.y + Mathf.Sin(ang) * r2 - 1.5f, 3, 3));
                d.sprite = DiscSprite; d.color = new Color(c.r, c.g, c.b, c.a * 0.7f); d.raycastTarget = false;
            }
        }

        /// <summary>丸いボタン（二重の輪と中の文字）。</summary>
        public static UnityEngine.UI.Button RoundButton(RectTransform parent, Vector2 center, float radius, string text, int size, Action onClick)
        {
            var img = Node<Image>(parent, "丸ボタン", new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2));
            img.sprite = DiscSprite; img.color = new Color(0.04f, 0.1f, 0.18f, 0.85f);
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.5f, 1.6f, 1.6f, 1f); cb.selectedColor = cb.highlightedColor; b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var rt = (RectTransform)img.transform;
            Ring(rt, new Vector2(radius, radius), radius, new Color(Paper.r, Paper.g, Paper.b, 0.9f), false);
            Ring(rt, new Vector2(radius, radius), radius * 0.86f, new Color(Teal.r, Teal.g, Teal.b, 0.8f), false);
            Label(rt, new Rect(0, 0, radius * 2, radius * 2), text, size, Color.white, TextAnchor.MiddleCenter);
            return b;
        }

        /// <summary>ツクヨミ風の大きな枠：夜の地・細い白い縁・四隅の扇。</summary>
        public static void Frame(RectTransform parent, Rect r)
        {
            Panel(parent, r, Night);
            var w = new Color(Paper.r, Paper.g, Paper.b, 0.55f);
            Panel(parent, new Rect(r.x + 10, r.y + 10, r.width - 20, 1), w);
            Panel(parent, new Rect(r.x + 10, r.yMax - 11, r.width - 20, 1), w);
            Panel(parent, new Rect(r.x + 10, r.y + 10, 1, r.height - 20), w);
            Panel(parent, new Rect(r.xMax - 11, r.y + 10, 1, r.height - 20), w);
            Fans(parent, new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), new Color(Paper.r, Paper.g, Paper.b, 0.85f), Mathf.Min(80, r.height * 0.22f));
        }

        static Font _display, _mincho;
        /// <summary>大きな斜体の文字（英字はゲームらしい Bahnschrift、なければ游ゴシック）。</summary>
        public static Font DisplayFont => _display != null ? _display : (_display = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Yu Gothic UI", "Meiryo" }, 64));
        /// <summary>名札の明朝。</summary>
        public static Font Mincho => _mincho != null ? _mincho : (_mincho = Font.CreateDynamicFontFromOSFont(new[] { "Yu Mincho", "游明朝", "MS Mincho" }, 48));

        static Sprite _ring, _disc;
        public static Sprite RingSprite => _ring != null ? _ring : (_ring = MakeCircle(256, 0.985f, 0.955f));
        public static Sprite DiscSprite => _disc != null ? _disc : (_disc = MakeCircle(128, 1f, 0f));

        static Sprite MakeCircle(int n, float outer, float inner)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float h = n / 2f, aa = 1.5f / h;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude / h;
                    float a = Mathf.Clamp01((outer - d) / aa) * (inner <= 0 ? 1 : Mathf.Clamp01((d - inner) / aa));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>文字。r は親の左上からの位置（ピクセル）。</summary>
        public static Text Label(RectTransform parent, Rect r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool mono = false, FontStyle style = FontStyle.Normal)
        {
            var t = Node<Text>(parent, "文字", r);
            t.font = mono ? Mono : Ts.Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        /// <summary>押せるボタン（マウス・Act.Confirm・あとで VR のレーザーでも押せる）。</summary>
        public static UnityEngine.UI.Button ButtonNode(RectTransform parent, Rect r, string text, int size, Action onClick)
        {
            var img = Node<Image>(parent, "ボタン", r);
            img.color = new Color(Cyan.r * 0.25f, Cyan.g * 0.25f, Cyan.b * 0.3f, 0.85f);
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = cb.highlightedColor;
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            Label((RectTransform)img.transform, new Rect(0, 0, r.width, r.height), text, size, Color.white, TextAnchor.MiddleCenter);
            return b;
        }

        static T Node<T>(RectTransform parent, string name, Rect r) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
            return go.AddComponent<T>();
        }

        static Font _mono;
        static Font Mono => _mono != null ? _mono : (_mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New" }, 24));

        /// <summary>ボタンを押すための EventSystem（Input System 用）を1つだけ作る。</summary>
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            var m = go.AddComponent<InputSystemUIInputModule>();
            m.AssignDefaultActions();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (Ts.Flag("--board-world")) Current = Mode.World;
        }
    }
}
