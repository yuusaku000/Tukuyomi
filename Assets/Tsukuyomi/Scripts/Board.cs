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
            if (Current == Mode.Screen)
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
            if (Current != Mode.World || !canvas.enabled) return;
            var cam = Camera.main;
            if (cam == null) return;
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
