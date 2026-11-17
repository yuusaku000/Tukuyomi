using UnityEngine;
using UnityEngine.InputSystem;

namespace Tsukuyomi
{
    /// <summary>
    /// 操作を「動作」の名前で読むところ（Input System）。
    /// コードはキーを直接見ずに Act.Interact.Down のように動作で読む。
    /// キーボード・マウス・ゲームパッドの割り当てはここに1か所にまとめる。
    /// VR のコントローラーは、あとで各動作に Bind を足すだけで使えるようにする。
    /// F3・F4・F12・T などの開発用キーは、ここに入れずに直接読む。
    /// </summary>
    public static class Act
    {
        /// <summary>1つの動作。Down＝押した瞬間、Held＝押している間、Up＝離した瞬間。</summary>
        public sealed class Button
        {
            public readonly InputAction action;
            public Button(string name, params string[] paths)
            {
                action = new InputAction(name, InputActionType.Button);
                foreach (var p in paths) action.AddBinding(p);
            }
            public bool Down => action.WasPressedThisFrame();
            public bool Held => action.IsPressed();
            public bool Up => action.WasReleasedThisFrame();
            /// <summary>割り当てを足す（VR コントローラー用など）。例："&lt;XRController&gt;{RightHand}/primaryButton"</summary>
            public void Bind(string path) { action.Disable(); action.AddBinding(path); action.Enable(); }
        }

        // ── 動かす ──
        /// <summary>移動（WASD・矢印・左スティック）。-1〜1 の2次元。</summary>
        public static readonly InputAction Move = NewMove();
        /// <summary>ジャンプ（Space）。飛行モードでは上昇。</summary>
        public static readonly Button Jump = new("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        /// <summary>走る（Shift）。</summary>
        public static readonly Button Run = new("Run", "<Keyboard>/leftShift", "<Keyboard>/rightShift", "<Gamepad>/leftStickPress", "<Gamepad>/leftTrigger");
        /// <summary>飛行モードで下がる（Ctrl・Q）。</summary>
        public static readonly Button Descend = new("Descend", "<Keyboard>/leftCtrl", "<Keyboard>/q", "<Gamepad>/leftShoulder");
        /// <summary>一人称と三人称の切りかえ（V）。</summary>
        public static readonly Button View = new("View", "<Keyboard>/v", "<Gamepad>/rightStickPress");

        // ── 世界とのやりとり ──
        /// <summary>話す・調べる・お店に入る・座る（E）。飛行モードでは上昇にも使う。</summary>
        public static readonly Button Interact = new("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
        /// <summary>釣りの竿を振る・引く（左クリック・Space）。</summary>
        public static readonly Button Cast = new("Cast", "<Mouse>/leftButton", "<Keyboard>/space", "<Gamepad>/rightTrigger", "<Gamepad>/buttonSouth");
        /// <summary>着がえ（C）。ライブ会場ではおまかせカメラ。</summary>
        public static readonly Button Wardrobe = new("Wardrobe", "<Keyboard>/c", "<Gamepad>/buttonNorth");

        // ── KASSEN（格闘） ──
        /// <summary>弱攻撃（左クリック）。</summary>
        public static readonly Button Light = new("Light", "<Mouse>/leftButton", "<Keyboard>/j", "<Gamepad>/buttonWest");
        /// <summary>強攻撃（右クリック）。</summary>
        public static readonly Button Heavy = new("Heavy", "<Mouse>/rightButton", "<Keyboard>/k", "<Gamepad>/buttonNorth");
        /// <summary>必殺技（E）。前後のキーと組み合わせて出す。</summary>
        public static readonly Button Special = new("Special", "<Keyboard>/e", "<Gamepad>/rightShoulder");
        /// <summary>投げ（F）。</summary>
        public static readonly Button Throw = new("Throw", "<Keyboard>/f", "<Gamepad>/leftShoulder");
        /// <summary>超必殺（R）。</summary>
        public static readonly Button Super = new("Super", "<Keyboard>/r", "<Gamepad>/rightTrigger");
        /// <summary>しゃがむ（Ctrl）。</summary>
        public static readonly Button Crouch = new("Crouch", "<Keyboard>/leftCtrl", "<Keyboard>/c", "<Gamepad>/leftTrigger");

        // ── 画面 ──
        /// <summary>決定（Enter・ゲームパッドの A）。板のボタン用。</summary>
        public static readonly Button Confirm = new("Confirm", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
        /// <summary>もどる・閉じる・メニュー（Esc）。開いているものを1つ閉じ、何もなければメニューを開く。</summary>
        public static readonly Button Back = new("Back", "<Keyboard>/escape", "<Gamepad>/buttonEast", "<Gamepad>/start");
        /// <summary>地図（M）。</summary>
        public static readonly Button Map = new("Map", "<Keyboard>/m", "<Gamepad>/select");
        /// <summary>図鑑（B）。</summary>
        public static readonly Button Book = new("Book", "<Keyboard>/b", "<Gamepad>/dpad/up");
        /// <summary>画面の表示をぜんぶ消す（H）。</summary>
        public static readonly Button HideHud = new("HideHud", "<Keyboard>/h", "<Gamepad>/dpad/down");
        /// <summary>戦況を見る（Tab）。SENGOKU で、目の前にホログラムの板が開く。</summary>
        public static readonly Button Info = new("Info", "<Keyboard>/tab", "<Gamepad>/dpad/right");
        /// <summary>左右に回す（お店の人形など）。Move の左右を使う。</summary>
        public static float TurnX => Move.ReadValue<Vector2>().x;

        // ── マウス（画面モードだけのもの） ──
        /// <summary>左クリックした瞬間（操作開始・画面のボタン）。</summary>
        public static bool ClickDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool ClickHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;

        /// <summary>マウスで見回す量（以前の Input.GetAxis("Mouse X/Y") と同じ大きさ）。</summary>
        public static Vector2 LookMouse => Mouse.current != null ? Tune(Mouse.current.delta.ReadValue() * 0.1f) : Vector2.zero;

        static float _lookSpeed = -1; static int _invert = -1;
        /// <summary>見回す速さ（0.3〜2.0。メニューの「操作」で変える）。</summary>
        public static float LookSpeed
        {
            get { if (_lookSpeed < 0) _lookSpeed = PlayerPrefs.GetFloat("tsukuyomi_look", 1f); return _lookSpeed; }
            set { _lookSpeed = Mathf.Clamp(value, 0.3f, 2f); PlayerPrefs.SetFloat("tsukuyomi_look", _lookSpeed); PlayerPrefs.Save(); }
        }
        /// <summary>上下を逆にする。</summary>
        public static bool InvertY
        {
            get { if (_invert < 0) _invert = PlayerPrefs.GetInt("tsukuyomi_invert", 0); return _invert == 1; }
            set { _invert = value ? 1 : 0; PlayerPrefs.SetInt("tsukuyomi_invert", _invert); PlayerPrefs.Save(); }
        }
        static Vector2 Tune(Vector2 v) => new Vector2(v.x, InvertY ? -v.y : v.y) * LookSpeed;
        /// <summary>右スティックで見回す量（マウスと同じ単位にそろえた、このフレームの分）。</summary>
        public static Vector2 LookStick => Gamepad.current != null ? Tune(Gamepad.current.rightStick.ReadValue() * (Time.unscaledDeltaTime * 30f)) : Vector2.zero;

        /// <summary>ホイール（1目盛りで ±1。以前の Input.mouseScrollDelta.y と同じ）。</summary>
        public static float Scroll
        {
            get
            {
                if (Mouse.current == null) return 0;
                float v = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(v) >= 10f) v /= 120f;   // Windows は1目盛り 120 で届くことがある
                return v;
            }
        }

        /// <summary>数字キー 1〜9（ライブのペンライトの色）。i は 0 から。</summary>
        public static bool Number(int i)
        {
            var k = Keyboard.current;
            if (k == null || i < 0 || i > 8) return false;
            return k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame;
        }

        /// <summary>何か操作したか（座ったまま放っておいた時間をはかる）。</summary>
        public static bool AnyActivity
        {
            get
            {
                if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
                var m = Mouse.current;
                if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.delta.ReadValue().sqrMagnitude > 0.25f || m.scroll.ReadValue().y != 0)) return true;
                var g = Gamepad.current;
                if (g != null && (g.leftStick.ReadValue().sqrMagnitude > 0.04f || g.rightStick.ReadValue().sqrMagnitude > 0.04f || g.buttonSouth.isPressed || g.buttonEast.isPressed)) return true;
                return false;
            }
        }

        static InputAction NewMove()
        {
            var a = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            a.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            a.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            a.AddBinding("<Gamepad>/leftStick");
            return a;
        }

        /// <summary>
        /// VR のコントローラーの割り当て（OpenXR とまねる機器の共通の名前）。
        ///  左スティック：歩く　左グリップ：走る　X：地図　Y・メニュー：もどる／メニュー
        ///  右トリガー：竿・板を押す　A：調べる・話す・座る　B：ジャンプ
        ///  右スティック（回る・ワープ）は Vr が読む。
        /// </summary>
        public static void AddXrBindings()
        {
            Move.Disable(); Move.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}"); Move.Enable();
            Run.Bind("<XRController>{LeftHand}/{GripButton}");
            Map.Bind("<XRController>{LeftHand}/{PrimaryButton}");
            Back.Bind("<XRController>{LeftHand}/{SecondaryButton}");
            Back.Bind("<XRController>{LeftHand}/{MenuButton}");
            Cast.Bind("<XRController>{RightHand}/{TriggerButton}");
            Interact.Bind("<XRController>{RightHand}/{PrimaryButton}");
            Jump.Bind("<XRController>{RightHand}/{SecondaryButton}");
        }

        static Button[] All => new[] { Jump, Run, Descend, View, Interact, Cast, Wardrobe, Confirm, Back, Map, Book, HideHud, Info, Light, Heavy, Special, Throw, Super, Crouch };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Move.Enable();
            foreach (var b in All) b.action.Enable();
        }
    }
}
