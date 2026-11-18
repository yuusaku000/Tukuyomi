using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace Tsukuyomi
{
    /// <summary>
    /// VR モード（OpenXR）。
    ///  ・起動時に「画面で遊ぶ／VR で遊ぶ」をえらぶ（Vr.Choice の板）。--vr で VR、--vr-sim でまねる機器、--screen で画面。
    ///  ・頭の動きにカメラが合わせて動き、左右の手はコントローラーに合わせて動く。
    ///  ・板（Board）は目の前に浮かび、右手の光の線とトリガーで押す（Input System の UI がそのまま受けとる）。
    ///  ・左スティックで歩く。右スティックの左右で回る（一気に／少しずつ）、前に倒すとワープの弧、離すとワープ。
    ///  ・動いている間は視界のふちを暗くする（酔い防止）。
    /// 設定はメニューの「設定 → VR」。機器がないときは --vr-sim で XR Interaction Toolkit のまねる機器を使って確かめる。
    /// </summary>
    public partial class Vr : MonoBehaviour
    {
        /// <summary>VR モードで遊んでいる（本物でも、まねる機器でも）。</summary>
        public static bool On { get; private set; }
        /// <summary>まねる機器（XR Device Simulator の機器）で動かしている。</summary>
        public static bool Sim { get; private set; }
        public static Vr I { get; private set; }

        // ───────── 設定（メニューの「設定 → VR」）─────────

        static readonly string[] QNames = { "軽い", "ふつう", "きれい" };
        static int _q = -1, _snap = -1, _vig = -1, _warp = -1, _kBody = -1, _kShake = -1, _turnLv = -1, _layout = -1; static float _ui = float.NaN; static float _h = float.NaN;
        static int GetI(string k, int d) { try { return PlayerPrefs.GetInt(k, d); } catch { return d; } }
        static void SetI(string k, int v) { try { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); } catch { } }

        /// <summary>VR の画質（0 軽い・1 ふつう・2 きれい）。画面モードの画質とは別に覚える。</summary>
        public static int QualityLevel
        {
            get { if (_q < 0) _q = Mathf.Clamp(GetI("vr_quality", 0), 0, 2); return _q; }
            set { _q = Mathf.Clamp(value, 0, 2); SetI("vr_quality", _q); if (On) Quality.ApplyVr(_q); }
        }
        public static string QualityName => QNames[QualityLevel];
        /// <summary>回り方：true＝一気に（45度ずつ）、false＝少しずつ。</summary>
        public static bool SnapTurn
        {
            get { if (_snap < 0) _snap = GetI("vr_snap", 1); return _snap == 1; }
            set { _snap = value ? 1 : 0; SetI("vr_snap", _snap); }
        }
        /// <summary>動いている間、視界のふちを暗くする。</summary>
        public static bool Vignette
        {
            get { if (_vig < 0) _vig = GetI("vr_vignette", 1); return _vig == 1; }
            set { _vig = value ? 1 : 0; SetI("vr_vignette", _vig); }
        }
        /// <summary>ワープ移動（右スティックを前に倒す）。</summary>
        public static bool Warp
        {
            get { if (_warp < 0) _warp = GetI("vr_warp", 1); return _warp == 1; }
            set { _warp = value ? 1 : 0; SetI("vr_warp", _warp); }
        }
        /// <summary>KASSEN（SETSUNA）の酔い防止：体だけ相手へ向き、視界は自分で回す（相手が視界の外へ出たら一気に向きなおす）。</summary>
        public static bool FightBodyOnly
        {
            get { if (_kBody < 0) _kBody = GetI("vr_fight_body", 0); return _kBody == 1; }
            set { _kBody = value ? 1 : 0; SetI("vr_fight_body", _kBody); }
        }
        /// <summary>KASSEN（SETSUNA）の酔い防止：当たったときの視界の揺れを止める。</summary>
        public static bool FightNoShake
        {
            get { if (_kShake < 0) _kShake = GetI("vr_fight_noshake", 0); return _kShake == 1; }
            set { _kShake = value ? 1 : 0; SetI("vr_fight_noshake", _kShake); }
        }
        /// <summary>空間に浮かぶ板の大きさ（0.5〜1.3 倍。2026-11-18 ユーザーの指示「UI が大きすぎる」で、はじめは 0.8）。</summary>
        public static float UiScale
        {
            get { if (float.IsNaN(_ui)) { try { _ui = PlayerPrefs.GetFloat("vr_ui", 0.8f); } catch { _ui = 0.8f; } } return _ui; }
            set
            {
                _ui = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.5f, 1.3f);
                try { PlayerPrefs.SetFloat("vr_ui", _ui); PlayerPrefs.Save(); } catch { }
                Board.WorldMul = _ui;
                foreach (var b in Board.All) if (b != null && !b.pinned) b.Apply();
            }
        }

        static readonly float[] SnapDeg = { 30f, 45f, 60f, 90f }, SmoothDeg = { 60f, 100f, 150f, 220f };
        /// <summary>回る速さ（0〜3）。一気に回るときは1回の角度（30・45・60・90 度）、少しずつのときは毎秒の角度。</summary>
        public static int TurnLevel
        {
            get { if (_turnLv < 0) _turnLv = Mathf.Clamp(GetI("vr_turn", 1), 0, 3); return _turnLv; }
            set { _turnLv = Mathf.Clamp(value, 0, 3); SetI("vr_turn", _turnLv); }
        }
        public static float SnapAngle => SnapDeg[TurnLevel];
        public static float SmoothSpeed => SmoothDeg[TurnLevel];
        public static string TurnText => SnapTurn ? $"{SnapAngle:0} 度ずつ" : $"毎秒 {SmoothSpeed:0} 度";

        /// <summary>ボタン配置（0 標準：A 調べる・移動わざ／B ジャンプ　1 入れかえ：A ジャンプ／B 調べる・移動わざ）。</summary>
        public static int ButtonLayout
        {
            get { if (_layout < 0) _layout = Mathf.Clamp(GetI("vr_layout", 0), 0, 1); return _layout; }
            set { _layout = Mathf.Clamp(value, 0, 1); SetI("vr_layout", _layout); Act.ApplyXrLayout(_layout); }
        }
        public static string ButtonLayoutName => ButtonLayout == 0 ? "A 調べる・B ジャンプ" : "A ジャンプ・B 調べる";
        /// <summary>いまの配置での、調べる・移動わざのボタンと、ジャンプのボタンの名前。</summary>
        public static string KeyUse => ButtonLayout == 0 ? "A" : "B";
        public static string KeyJump => ButtonLayout == 0 ? "B" : "A";

        /// <summary>一気に向きを変えたときなど、少しのあいだ視界のふちを暗くする（「視界のふち」がオンのとき）。</summary>
        public static void Flash(float seconds = 0.25f) { if (I != null) I._flash = Mathf.Max(I._flash, seconds); }

        /// <summary>背の高さ（目の高さに足すメートル。-0.5〜+0.8）。座って遊ぶときは上げる。</summary>
        public static float Height
        {
            get { if (float.IsNaN(_h)) { try { _h = PlayerPrefs.GetFloat("vr_height", 0f); } catch { _h = 0; } } return _h; }
            set { _h = Mathf.Clamp(Mathf.Round(value * 100f) / 100f, -0.5f, 0.8f); try { PlayerPrefs.SetFloat("vr_height", _h); PlayerPrefs.Save(); } catch { } }
        }
        public static string HeightText => Height == 0 ? "そのまま" : $"{(Height > 0 ? "＋" : "−")}{Mathf.Abs(Height) * 100:0} cm";

        /// <summary>いまの頭の高さを、人形の目の高さ（1.52m）に合わせる。</summary>
        public static void FitHeight()
        {
            // 原点が床なら「人形の目 − いまの頭」、目の高さが原点なら何も足さない
            Height = _floor && On ? 1.52f - HeadPos.y : 0f;
            Hud.Toast($"背の高さを合わせました（{HeightText}）");
        }

        public static void ResetSettings()
        {
            QualityLevel = 0; SnapTurn = true; Vignette = true; Warp = true; Height = 0f; FightBodyOnly = false; FightNoShake = false; UiScale = 0.8f; TurnLevel = 1; ButtonLayout = 0;
        }

        // ───────── 頭と手の読みとり ─────────

        static readonly InputAction _headPos = Val("HeadPos", "<XRHMD>/centerEyePosition");
        static readonly InputAction _headRot = Val("HeadRot", "<XRHMD>/centerEyeRotation", true);
        static readonly InputAction _lPos = Val("LPos", "<XRController>{LeftHand}/devicePosition");
        static readonly InputAction _lRot = Val("LRot", "<XRController>{LeftHand}/deviceRotation", true);
        static readonly InputAction _rPos = Val("RPos", "<XRController>{RightHand}/devicePosition");
        static readonly InputAction _rRot = Val("RRot", "<XRController>{RightHand}/deviceRotation", true);
        static readonly InputAction _rStick = Val("RStick", "<XRController>{RightHand}/{Primary2DAxis}", false, "Vector2");
        static readonly InputAction _lGrip = Val("LGrip", "<XRController>{LeftHand}/{Grip}", false, "Axis");
        static readonly InputAction _rGrip = Val("RGrip", "<XRController>{RightHand}/{Grip}", false, "Axis");
        static readonly InputAction _lTrig = Val("LTrig", "<XRController>{LeftHand}/{Trigger}", false, "Axis");
        static readonly InputAction _rTrig = Val("RTrig", "<XRController>{RightHand}/{Trigger}", false, "Axis");

        static InputAction Val(string name, string path, bool rot = false, string type = null)
        {
            var a = new InputAction(name, InputActionType.Value, path, expectedControlType: type ?? (rot ? "Quaternion" : "Vector3"));
            return a;
        }
        static readonly InputAction _rAimPos = Val("RAimPos", "<XRController>{RightHand}/pointerPosition");
        static readonly InputAction _rAimRot = Val("RAimRot", "<XRController>{RightHand}/pointerRotation", true);
        static readonly InputAction _recenter = new InputAction("Recenter", InputActionType.Button, "<XRController>{RightHand}/{Primary2DAxisClick}");
        static readonly InputAction _rHaptic = Haptic("RHaptic", "RightHand");
        static readonly InputAction _lHaptic = Haptic("LHaptic", "LeftHand");
        /// <summary>ふるえを送る先（OpenXR のコントローラーの haptic。名前と使い道の両方で探す）。</summary>
        static InputAction Haptic(string name, string hand)
        {
            var a = new InputAction(name, InputActionType.PassThrough, $"<XRController>{{{hand}}}/haptic");
            a.AddBinding($"<XRController>{{{hand}}}/{{Haptic}}");
            return a;
        }
        static IEnumerable<InputAction> Reads => new[] { _rAimPos, _rAimRot, _recenter, _rHaptic, _lHaptic, _headPos, _headRot, _lPos, _lRot, _rPos, _rRot, _rStick, _lGrip, _rGrip, _lTrig, _rTrig };

        static Quaternion Q(InputAction a)
        {
            var q = a.ReadValue<Quaternion>();
            return q.x == 0 && q.y == 0 && q.z == 0 && q.w == 0 ? Quaternion.identity : q;
        }
        /// <summary>頭の位置（床からの、遊ぶ場所の中の位置）。</summary>
        public static Vector3 HeadPos => _headPos.ReadValue<Vector3>();
        public static Quaternion HeadRot => Q(_headRot);
        /// <summary>頭の左右の向き（体の向きからのずれ）。歩く向きに使う。</summary>
        public static float HeadYaw => On ? HeadRot.eulerAngles.y - _yawOff : 0f;

        // ───────── 視点のリセット（2026-11-18）─────────
        // いま見ている向きを「正面」に、いまの頭の位置を遊ぶ場所の「まん中」にする（座って遊ぶ・コードがねじれた・見失ったあとなど）。
        // 遊ぶ場所（_origin）を、向きは _yawOff だけ回し、位置は _ctr だけずらす。頭・手・板・ワープはこの遊ぶ場所を通して置くので、ずれない。
        static float _yawOff;
        static Vector2 _ctr;
        /// <summary>遊ぶ場所のまん中から見た頭の位置（リセットしたまん中を引いたもの）。</summary>
        static Vector3 HeadLocal => HeadPos - new Vector3(_ctr.x, 0, _ctr.y);

        /// <summary>視点のリセット（メニューの「設定 → VR」・右スティックの押しこみ・キーボードの Home）。</summary>
        public static void Recenter()
        {
            if (!On) return;
            _yawOff = HeadRot.eulerAngles.y;
            _ctr = new Vector2(HeadPos.x, HeadPos.z);
            Board.ReplaceAll();
            if (I != null) I._flash = 0.3f;
            Hud.Toast("視点をリセットしました（いま見ている向きが正面）");
            Debug.Log($"[VR] 視点のリセット：向き {_yawOff:0}度・まん中 ({_ctr.x:0.00}, {_ctr.y:0.00})");
        }

        /// <summary>遊ぶ場所の原点が床か（false なら目の高さが原点）。</summary>
        static bool _floor = true;

        // ───────── 始める ─────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            foreach (var a in Reads) a.Enable();
            Act.AddXrBindings();
            Act.ApplyXrLayout(ButtonLayout);
            Board.WorldMul = UiScale;
            // コントローラーがつなぎなおされたら（電池・スリープ・Link が切れた など）、割り当てを読みなおす
            InputSystem.onDeviceChange += (d, ch) =>
            {
                if (!(d is UnityEngine.InputSystem.XR.XRController || d is UnityEngine.InputSystem.XR.XRHMD)) return;
                if (ch != InputDeviceChange.Added && ch != InputDeviceChange.Reconnected && ch != InputDeviceChange.UsageChanged && ch != InputDeviceChange.Enabled) return;
                foreach (var a in Reads) { a.Disable(); a.Enable(); }
                Act.Refresh();
                Debug.Log($"[VR] 機器がつながった：{d.displayName}（{ch}）→ 割り当てを読みなおした");
            };
        }

        /// <summary>GameSession の Start から呼ぶ：起動時の指定を見て、VR で始めるか、えらぶ板を出す。</summary>
        public static void OnGameStart()
        {
            if (Ts.Flag("--vr-sim")) { Begin(true); return; }
            if (Ts.Flag("--vr")) { Host.StartCoroutine(StartXr()); return; }
            if (Ts.Flag("--screen") || Ts.ShotMode) return;
            ShowChoice();
        }

        static MonoBehaviour _host;
        static MonoBehaviour Host
        {
            get
            {
                if (_host == null) { var go = new GameObject("VR の係"); DontDestroyOnLoad(go); _host = go.AddComponent<VrHost>(); }
                return _host;
            }
        }
        class VrHost : MonoBehaviour { }

        /// <summary>本物の VR 機器を使い始める（OpenXR）。見つからなければ画面モードのまま。</summary>
        static IEnumerator StartXr()
        {
            SetChoiceStatus("VR 機器をさがしています…");
            yield return null;
            var gs = XRGeneralSettings.Instance;
            var m = gs != null ? gs.Manager : null;
            if (m == null) { Fail("VR の部品が見つかりませんでした"); yield break; }
            if (m.activeLoader == null) yield return m.InitializeLoader();
            if (m.activeLoader == null) { Fail("VR 機器が見つかりませんでした。つないでから、もう一度えらんでください（Quest は Link でつなぐ）"); yield break; }
            m.StartSubsystems();
            // 床を原点にする（目の高さ＝本当の背の高さになる）
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            _floor = false;
            foreach (var s in subs) if (s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)) _floor = true;
            Debug.Log($"[VR] 始めました loader={m.activeLoader.name} floor={_floor} device={XRSettings.loadedDeviceName}");
            Begin(false);
        }

        static void Fail(string msg)
        {
            Debug.Log("[VR] " + msg);
            if (_choice != null) SetChoiceStatus(msg);
            else Hud.Toast(msg);
        }

        /// <summary>VR モードにする。sim＝まねる機器で。</summary>
        public static void Begin(bool sim)
        {
            if (On) return;
            On = true; Sim = sim;
            // PC の窓が手前でなくなっても（Oculus のアプリを触った・ヘッドセットを外した など）、コントローラーの入力を止めない。
            // ふつうは Unity が「よそ見中」として入力を止め、もどってもボタンが効かないことがあった（2026-11-18）
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            CloseChoice();
            if (sim) { _floor = true; SimStart(); }
            Board.Current = Board.Mode.World;
            foreach (var b in Board.All) if (b != null) b.Apply();
            var go = new GameObject("VR");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Vr>();
            Quality.ApplyVr(QualityLevel);
            Debug.Log($"[VR] VR モード sim={sim} 画質={QualityName} 回り方={(SnapTurn ? "一気に" : "少しずつ")} ふち={Vignette} ワープ={Warp} 背={Height:0.00}");
        }

        // ───────── カメラ（OrbitCamera から呼ぶ）─────────

        /// <summary>遊ぶ場所の原点（足もと）。体の位置と向き＋背の高さ。</summary>
        public static Vector3 OriginPos(OrbitCamera cam) => (Body != null ? Body.Feet + Vector3.up * (Height + (_floor ? 0f : 1.6f)) : cam.target.position + Vector3.up * (Height + (_floor ? 0f : cam.eyeHeight)))
                                                           - OriginRot(cam) * new Vector3(_ctr.x, 0, _ctr.y);
        public static Quaternion OriginRot(OrbitCamera cam) => Quaternion.Euler(0, (Body != null ? Body.Yaw : cam.yaw) - _yawOff, 0);

        // ───────── KASSEN などの体（島の人形の代わり）─────────

        /// <summary>
        /// 島の人形の代わりに VR の体になるもの（KASSEN の試合中の自分）。入れている間は、頭と手はこの足もとと向きに合わせ、
        /// 右スティックでこの向きを回す（ワープはしない）。光の線は Pointer のときだけ出す。
        /// </summary>
        public interface IBody
        {
            /// <summary>足もと（遊ぶ場所の原点）。</summary>
            Vector3 Feet { get; }
            /// <summary>体の左右の向き（度）。</summary>
            float Yaw { get; set; }
            /// <summary>回ったときに頭がずれないよう、体をこれだけ動かす。</summary>
            void Carry(Vector3 shift);
            /// <summary>板を押すための光の線を出す（一時停止・武器えらび・結果など）。</summary>
            bool Pointer { get; }
            /// <summary>カメラを試合のほうで動かす（倒れたあとの観戦など）。</summary>
            bool CameraFree { get; }
        }
        public static IBody Body;
        /// <summary>右スティック（KASSEN の練習場で武器を持ちかえる）。</summary>
        public static Vector2 RightStick => _rStick.ReadValue<Vector2>();

        /// <summary>右手・左手（見た目の手。位置と向きは世界の座標）。VR でないときは null。</summary>
        public static Transform RightHand => I != null ? I._rHand : null;
        /// <summary>右手の「指す向き」（光の線・ワープ・KASSEN のねらい）。本物の機器は OpenXR の aim、まねる機器などは手と同じ。</summary>
        public static Transform RightAim => I != null ? (I._rAim != null ? I._rAim : I._rHand) : null;
        Transform _rAim;
        public static Transform LeftHand => I != null ? I._lHand : null;
        /// <summary>手の速さ（メートル／秒、遊ぶ場所の中の動きだけ。振る攻撃に使う）。</summary>
        public static Vector3 RightVel, LeftVel;
        static Vector3 _rPrev, _lPrev; static bool _velInit;

        static void HandVel()
        {
            // 時間の流れを止めた・遅くした間（一時停止・刹那ガードのスロー）も手の本当の速さになるように、実時間で割る
            float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
            var r = _rPos.ReadValue<Vector3>(); var l = _lPos.ReadValue<Vector3>();
            // 毎秒 9m より速いのは、手の速さではなく読み取りの飛び（見失った・起動の直後など）とみなして使わない
            if (_velInit)
            {
                var rv = (r - _rPrev) / dt; var lv = (l - _lPrev) / dt;
                RightVel = rv.magnitude > 9f ? Vector3.zero : Vector3.Lerp(RightVel, rv, 0.6f);
                LeftVel = lv.magnitude > 9f ? Vector3.zero : Vector3.Lerp(LeftVel, lv, 0.6f);
            }
            _rPrev = r; _lPrev = l; _velInit = true;
        }

        /// <summary>コントローラーをふるわせる（当たった感じ）。amp 0〜1。</summary>
        public static void Buzz(bool right, float amp, float seconds)
        {
            if (!On) return;
            // 本物の機器（OpenXR）：コントローラーの haptic へ送る。まねる機器などは下の送り方
            try { UnityEngine.XR.OpenXR.Input.OpenXRInput.SendHapticImpulse(right ? _rHaptic : _lHaptic, Mathf.Clamp01(amp), seconds); } catch { }
            var want = right ? UnityEngine.InputSystem.CommonUsages.RightHand : UnityEngine.InputSystem.CommonUsages.LeftHand;
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
            {
                if (!(d is UnityEngine.InputSystem.XR.XRControllerWithRumble rc)) continue;
                bool hand = false;
                foreach (var u in d.usages) if (u == want) hand = true;
                if (hand) rc.SendImpulse(Mathf.Clamp01(amp), seconds);
            }
        }

        /// <summary>カメラを頭の位置と向きに置く。</summary>
        public static void PlaceCamera(OrbitCamera cam)
        {
            if (cam == null || cam.target == null || (Body != null && Body.CameraFree)) return;
            var r = OriginRot(cam);
            cam.transform.SetPositionAndRotation(OriginPos(cam) + r * HeadPos, r * HeadRot);
        }

        // ───────── 毎フレーム ─────────

        Transform _origin, _lHand, _rHand;
        Transform[] _lFingers, _rFingers;
        LineRenderer _ray, _arc, _ringLine;
        Transform _dot;
        Transform _vigT; Material _vigMat; float _vigNow;
        float _comfort, _flash, _trigPrev;
        UnityEngine.UI.Selectable _hover; Board _hitBoard; Vector3 _hitPoint;
        Vector3 _lastPos; bool _turnHeld, _aiming, _aimOk; Vector3 _aimPoint;

        OrbitCamera Cam => GameSession.Instance != null ? GameSession.Instance.cam : null;
        PlayerController Player => GameSession.Instance != null ? GameSession.Instance.player : null;

        void Start()
        {
            _origin = new GameObject("VR の床").transform;
            _origin.SetParent(transform, false);
            _lHand = MakeHand(true, out _lFingers);
            _rHand = MakeHand(false, out _rFingers);
            _rAim = new GameObject("右手の指す向き").transform;
            _rAim.SetParent(_rHand.parent, false);
            _ray = Line("光の線", 0.006f, new Color(0.45f, 0.95f, 1f, 0.85f));
            _arc = Line("ワープの弧", 0.02f, new Color(0.45f, 0.95f, 1f, 0.9f));
            _ringLine = Line("ワープの輪", 0.025f, new Color(0.45f, 0.95f, 1f, 0.9f));
            _ringLine.loop = true;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.name = "光の点"; dot.transform.SetParent(transform, false); dot.transform.localScale = Vector3.one * 0.018f;
            dot.GetComponent<Renderer>().sharedMaterial = UiMat(new Color(1f, 1f, 1f, 1f));
            dot.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            _dot = dot.transform;
            // 視界のふち：目の前に置く、まん中が抜けた黒い輪（後処理を使わないので「軽い」でも使える）
            _vigT = MakeVignette(out _vigMat);
            // 板は Vr が光の線で押す（Input System の UI の、コントローラーで押すしくみは使わない＝二重に押さないように）
            Board.EnsureEventSystem();
            if (EventSystem.current != null && EventSystem.current.TryGetComponent<InputSystemUIInputModule>(out var mod))
            {
                mod.trackedDevicePosition = null; mod.trackedDeviceOrientation = null;
                mod.xrTrackingOrigin = _origin;
            }
            if (Player != null) _lastPos = Player.Position;
            Application.onBeforeRender += BeforeRender;
        }

        void OnDestroy() { Application.onBeforeRender -= BeforeRender; }

        void BeforeRender()
        {
            // 描く直前にもう一度、頭と手を最新の位置へ（遅れを小さく）
            var cam = Cam;
            if (cam == null) return;
            PlaceCamera(cam);
            PlaceRig(cam);
            PlaceVignette(cam.GetComponent<Camera>());
        }

        void Update()
        {
            if (Sim) SimTick();
            HandVel();
            if (_recenter.WasPressedThisFrame() || Act.Recenter.Down) Recenter();
            var cam = Cam; var pl = Player;
            if (cam == null || pl == null) return;
            if (Body != null) { BodyTick(cam); return; }
            bool free = !Hud.Blocked && (!Ts.ShotMode || _simByTour) && !PlayerController.Busy && !Kassen.Busy && !Choosing;
            var rs = _rStick.ReadValue<Vector2>();

            // 回る
            bool turning = false;
            if (!_aiming && Mathf.Abs(rs.x) > Mathf.Abs(rs.y))
            {
                if (SnapTurn)
                {
                    if (Mathf.Abs(rs.x) > 0.7f && !_turnHeld) { Turn(cam, SnapAngle * Mathf.Sign(rs.x)); _turnHeld = true; _flash = 0.25f; }
                }
                else if (Mathf.Abs(rs.x) > 0.2f) { Turn(cam, rs.x * SmoothSpeed * Time.deltaTime); turning = true; }
            }
            if (Mathf.Abs(rs.x) < 0.3f) _turnHeld = false;

            // ワープ
            if (free && Warp && rs.y > 0.6f && Mathf.Abs(rs.y) > Mathf.Abs(rs.x)) _aiming = true;
            if (_aiming)
            {
                AimArc(cam);
                if (rs.magnitude < 0.25f || !free || !Warp)
                {
                    if (_aimOk && free && Warp) { WarpTo(pl, cam, _aimPoint); }
                    _aiming = false;
                }
            }
            _arc.enabled = _ringLine.enabled = _aiming;

            // 酔い防止：動いている間だけ、視界のふちを暗く
            var p = pl.Position; var d = p - _lastPos; d.y = 0; _lastPos = p;
            float speed = d.magnitude / Mathf.Max(1e-4f, Time.deltaTime);
            bool moving = speed > 0.6f && speed < 60f;
            float want = Vignette && (moving || turning) ? 1f : 0f;
            _comfort = Mathf.MoveTowards(_comfort, want, Time.deltaTime * (want > _comfort ? 4f : 2f));
            _flash = Mathf.Max(0, _flash - Time.deltaTime);
            _vigNow = Vignette ? Mathf.Max(_comfort, _flash > 0 ? 1f : 0f) : 0f;
        }

        /// <summary>KASSEN の体：回るのと、酔い防止のふちだけ（ワープはしない）。</summary>
        void BodyTick(OrbitCamera cam)
        {
            var rs = _rStick.ReadValue<Vector2>();
            bool turning = false;
            if (!Body.Pointer && Mathf.Abs(rs.x) > Mathf.Abs(rs.y))
            {
                if (SnapTurn) { if (Mathf.Abs(rs.x) > 0.7f && !_turnHeld) { Turn(cam, SnapAngle * Mathf.Sign(rs.x)); _turnHeld = true; _flash = 0.25f; } }
                else if (Mathf.Abs(rs.x) > 0.2f) { Turn(cam, rs.x * SmoothSpeed * Time.deltaTime); turning = true; }
            }
            if (Mathf.Abs(rs.x) < 0.3f) _turnHeld = false;
            _aiming = false; _arc.enabled = _ringLine.enabled = false;
            var p = Body.Feet; var d = p - _lastPos; d.y = 0; _lastPos = p;
            float speed = d.magnitude / Mathf.Max(1e-4f, Time.deltaTime);
            bool moving = speed > 0.6f && speed < 60f;
            float want = Vignette && (moving || turning) ? 1f : 0f;
            _comfort = Mathf.MoveTowards(_comfort, want, Time.deltaTime * (want > _comfort ? 4f : 2f));
            _flash = Mathf.Max(0, _flash - Time.deltaTime);
            _vigNow = Vignette ? Mathf.Max(_comfort, _flash > 0 ? 1f : 0f) : 0f;
        }

        /// <summary>視界のふちの強さ（0〜1。確認用）。</summary>
        public float VignetteNow => _vigNow;

        /// <summary>まん中が抜けた輪（内側は透明、外へ向かって黒）。半径1の内側から 1.7 までぼかし、外は 8 まで。</summary>
        Transform MakeVignette(out Material mat)
        {
            const int n = 48;
            var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
            float[] rad = { 1f, 1.7f, 8f }; float[] al = { 0f, 1f, 1f };
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2;
                    v.Add(new Vector3(Mathf.Cos(a) * rad[ring], Mathf.Sin(a) * rad[ring], 0));
                    c.Add(new Color(0, 0, 0, al[ring]));
                }
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < n; i++)
                {
                    int a0 = ring * n + i, a1 = ring * n + (i + 1) % n, b0 = a0 + n, b1 = a1 + n;
                    tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            var mesh = new Mesh { name = "視界のふち" };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(tri, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100);
            var go = new GameObject("視界のふち");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            mat = UiMat(Color.white); mat.renderQueue = 4000;
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        /// <summary>輪を目の前に置く（強いほど内側が狭くなる）。</summary>
        void PlaceVignette(Camera c)
        {
            bool on = _vigNow > 0.01f && c != null;
            if (_vigT.gameObject.activeSelf != on) _vigT.gameObject.SetActive(on);
            if (!on) return;
            const float d = 0.3f;
            float halfH = d * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * Mathf.Max(1f, c.aspect);
            // 0 で画面の角より外、1 で縦の半分の 0.85 倍まで寄せる
            float inner = Mathf.Lerp(Mathf.Sqrt(halfW * halfW + halfH * halfH) * 1.05f, halfH * 0.85f, _vigNow);
            _vigT.SetPositionAndRotation(c.transform.position + c.transform.forward * d, c.transform.rotation);
            _vigT.localScale = Vector3.one * inner;
            _vigMat.color = new Color(1, 1, 1, Mathf.Clamp01(_vigNow * 2.5f));
        }

        void LateUpdate()
        {
            var cam = Cam;
            if (cam == null) return;
            PlaceRig(cam);
            UpdateRay();
            PlaceVignette(cam.GetComponent<Camera>());
        }

        void PlaceRig(OrbitCamera cam)
        {
            _origin.SetPositionAndRotation(OriginPos(cam), OriginRot(cam));
            Pose(_lHand, _lPos.ReadValue<Vector3>(), Q(_lRot));
            Pose(_rHand, _rPos.ReadValue<Vector3>(), Q(_rRot));
            var aq = _rAimRot.controls.Count > 0 ? _rAimRot.ReadValue<Quaternion>() : default;
            if (_rAim != null)
            {
                if (aq.x == 0 && aq.y == 0 && aq.z == 0 && aq.w == 0) Pose(_rAim, _rHand.localPosition, _rHand.localRotation);
                else Pose(_rAim, _rAimPos.ReadValue<Vector3>(), aq);
            }
            Curl(_lFingers, _lGrip.ReadValue<float>(), _lTrig.ReadValue<float>());
            Curl(_rFingers, _rGrip.ReadValue<float>(), _rTrig.ReadValue<float>());
        }

        static void Pose(Transform t, Vector3 p, Quaternion r) { t.localPosition = p; t.localRotation = r; }

        void Turn(OrbitCamera cam, float deg)
        {
            // 頭のまわりで回る（体のまわりだと、頭が横にずれて酔いやすい）
            var head = OriginPos(cam) + OriginRot(cam) * HeadPos;
            if (Body != null) Body.Yaw += deg; else cam.yaw += deg;
            var after = OriginPos(cam) + OriginRot(cam) * HeadPos;
            var shift = head - after; shift.y = 0;
            if (shift.sqrMagnitude > 1e-6f && shift.sqrMagnitude < 1f) { if (Body != null) Body.Carry(shift); else if (Player != null) Player.Carry(shift, 0); }
            if (Sim && Mathf.Abs(deg) > 10f) Log($"回った {deg:0.#}度 → 向き {Mathf.Repeat(cam.yaw, 360):0}");
        }

        /// <summary>今いる板を、光の線で指す（板にあたったところに点）。</summary>
        void UpdateRay()
        {
            bool show = Body != null ? Body.Pointer : Choosing || Hud.Blocked || Kassen.Busy;
            var aim = RightAim; var o = aim.position; var dir = aim.forward;
            // あたった板のうち、光の線の先に押せるボタンがある板を優先する（2026-11-18：KASSEN では目の前いっぱいの表示の板が
            // 一時停止や武器えらびの板より手前にあり、線がそこで止まってボタンが押しにくかった）。ボタンがなければ、いちばん近い板
            float best = 4f, bestBtn = 4f; bool hit = false, btn = false;
            Board nb = null; Vector3 np = default;
            if (show)
                foreach (var b in Board.All)
                {
                    if (b == null || b.passive || b.canvas == null || !b.canvas.enabled || b.canvas.renderMode != RenderMode.WorldSpace || b.Alpha < 0.01f) continue;
                    var ct = (RectTransform)b.canvas.transform;
                    var plane = new Plane(ct.forward, ct.position);
                    if (!plane.Raycast(new Ray(o, dir), out var t) || t <= 0 || t >= 4f) continue;
                    var at = o + dir * t;
                    var lp = ct.InverseTransformPoint(at);
                    var sz = ct.sizeDelta;
                    if (Mathf.Abs(lp.x) > sz.x * 0.5f || Mathf.Abs(lp.y) > sz.y * 0.5f) continue;
                    if (t < best) { best = t; nb = b; np = at; }
                    if (t < bestBtn && Over(b, at) != null) { bestBtn = t; btn = true; _hitBoard = b; _hitPoint = at; }
                }
            if (btn) { hit = true; best = bestBtn; }
            else if (nb != null) { hit = true; _hitBoard = nb; _hitPoint = np; }
            _ray.enabled = show;
            _dot.gameObject.SetActive(show && hit);
            Press(show && hit ? _hitBoard : null);
            if (!show) return;
            float len = hit ? best : 1.2f;
            _ray.SetPosition(0, o); _ray.SetPosition(1, o + dir * len);
            if (hit) _dot.position = o + dir * (best - 0.003f);
            RayHit = hit;
        }
        /// <summary>光の線の先のボタンを光らせ、右手のトリガーを引いたら押す。</summary>
        void Press(Board b)
        {
            var over = b != null ? Over(b, _hitPoint) : null;
            var es = EventSystem.current;
            var ev = es != null ? new PointerEventData(es) { button = PointerEventData.InputButton.Left } : null;
            if (over != _hover && ev != null)
            {
                if (_hover != null) ExecuteEvents.Execute(_hover.gameObject, ev, ExecuteEvents.pointerExitHandler);
                if (over != null) { ExecuteEvents.Execute(over.gameObject, ev, ExecuteEvents.pointerEnterHandler); Buzz(true, 0.12f, 0.02f); }
            }
            _hover = over;
            float tr = _rTrig.ReadValue<float>();
            bool pressed = tr > 0.6f && _trigPrev <= 0.6f;
            _trigPrev = tr;
            if (pressed && over != null && ev != null)
            {
                ExecuteEvents.Execute(over.gameObject, ev, ExecuteEvents.pointerClickHandler);
                Buzz(true, 0.35f, 0.04f);
                if (Sim) Log($"押した：{over.name}");
            }
        }

        /// <summary>板の、その点にある押せるもの（後ろに並ぶもの＝上に描かれるものを優先）。</summary>
        static UnityEngine.UI.Selectable Over(Board b, Vector3 at)
        {
            UnityEngine.UI.Selectable over = null;
            foreach (var s in b.GetComponentsInChildren<UnityEngine.UI.Selectable>())
            {
                if (!s.IsInteractable() || !s.isActiveAndEnabled) continue;
                var rt = (RectTransform)s.transform;
                var lp = rt.InverseTransformPoint(at);
                if (rt.rect.Contains(new Vector2(lp.x, lp.y))) over = s;
            }
            return over;
        }

        /// <summary>光の線が板にあたっている（確認用）。</summary>
        public static bool RayHit;

        // ───────── ワープ ─────────

        void AimArc(OrbitCamera cam)
        {
            var pts = new List<Vector3>();
            var p = RightAim.position; var vel = RightAim.forward * 9f;
            pts.Add(p);
            _aimOk = false;
            for (int i = 0; i < 60; i++)
            {
                var next = p + vel * 0.04f; vel += Physics.gravity * 0.04f;
                var seg = next - p;
                if (Physics.Raycast(p, seg.normalized, out var h, seg.magnitude, ~0, QueryTriggerInteraction.Ignore) && !(h.collider is CharacterController))
                {
                    pts.Add(h.point);
                    _aimPoint = h.point;
                    _aimOk = h.normal.y > 0.6f && h.point.y > 0.25f && !Island.InWater(h.point) && (h.point - cam.target.position).magnitude > 0.8f;
                    break;
                }
                pts.Add(next); p = next;
                if (p.y < -30f) break;
            }
            var c = _aimOk ? new Color(0.45f, 0.95f, 1f, 0.9f) : new Color(1f, 0.35f, 0.35f, 0.8f);
            _arc.startColor = _arc.endColor = c; _ringLine.startColor = _ringLine.endColor = c;
            _arc.positionCount = pts.Count; _arc.SetPositions(pts.ToArray());
            _ringLine.enabled = _aimOk;
            if (_aimOk)
            {
                _ringLine.positionCount = 24;
                for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2; _ringLine.SetPosition(i, _aimPoint + new Vector3(Mathf.Cos(a) * 0.35f, 0.03f, Mathf.Sin(a) * 0.35f)); }
            }
        }

        void WarpTo(PlayerController pl, OrbitCamera cam, Vector3 at)
        {
            // 頭が行き先の真上に来るように（遊ぶ場所の中で歩いたずれを引く）
            var off = OriginRot(cam) * HeadLocal; off.y = 0;
            pl.Teleport(at - off + Vector3.up * 0.1f, cam.yaw);
            _lastPos = pl.Position;
            _flash = 0.2f;
            Log($"ワープしました ({at.x:0.0}, {at.y:0.0}, {at.z:0.0})");
        }

        // ───────── 手の形 ─────────

        static Material _skin, _band;

        Transform MakeHand(bool left, out Transform[] fingers)
        {
            if (_skin == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                _skin = new Material(sh) { color = new Color(0.96f, 0.86f, 0.8f) };
                _band = new Material(sh) { color = new Color(0.1f, 0.5f, 0.55f) };
                _band.EnableKeyword("_EMISSION"); _band.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 1f) * 1.2f);
            }
            var root = new GameObject(left ? "左手" : "右手").transform;
            root.SetParent(_origin, false);
            float s = left ? -1 : 1;
            Part(root, PrimitiveType.Sphere, new Vector3(0, 0, 0.0f), Vector3.zero, new Vector3(0.08f, 0.03f, 0.095f), _skin);   // てのひら
            Part(root, PrimitiveType.Cylinder, new Vector3(0, 0, -0.06f), new Vector3(90, 0, 0), new Vector3(0.062f, 0.012f, 0.05f), _band); // 手首の光る帯
            fingers = new Transform[5];
            for (int i = 0; i < 4; i++)
            {
                var piv = new GameObject("指").transform;
                piv.SetParent(root, false);
                piv.localPosition = new Vector3(s * (0.027f - i * 0.018f), 0.0f, 0.042f);
                float len = i == 0 ? 0.075f : i == 3 ? 0.055f : 0.07f; // 人さし指・中指・薬指・小指
                if (i == 1) len = 0.08f;
                Part(piv, PrimitiveType.Capsule, new Vector3(0, 0, len * 0.5f), new Vector3(90, 0, 0), new Vector3(0.017f, len * 0.5f, 0.017f), _skin);
                fingers[i] = piv;
            }
            var th = new GameObject("親指").transform;
            th.SetParent(root, false);
            th.localPosition = new Vector3(s * 0.04f, -0.005f, 0.0f);
            th.localRotation = Quaternion.Euler(0, s * 40f, 0);
            Part(th, PrimitiveType.Capsule, new Vector3(0, 0, 0.03f), new Vector3(90, 0, 0), new Vector3(0.019f, 0.03f, 0.019f), _skin);
            fingers[4] = th;
            return root;
        }

        static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localEulerAngles = euler; go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>握り（グリップ）で中指〜小指、トリガーで人さし指を曲げる。</summary>
        static void Curl(Transform[] f, float grip, float trig)
        {
            if (f == null) return;
            f[0].localRotation = Quaternion.Euler(trig * 75f, 0, 0);
            for (int i = 1; i < 4; i++) f[i].localRotation = Quaternion.Euler(grip * 85f, 0, 0);
        }

        static Material UiMat(Color c)
        {
            var m = new Material(Shader.Find("UI/Default")) { color = c };
            m.renderQueue = 3100;
            return m;
        }

        LineRenderer Line(string name, float width, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = true; l.positionCount = 2; l.widthMultiplier = width;
            l.sharedMaterial = UiMat(Color.white);
            l.startColor = l.endColor = c;
            l.shadowCastingMode = ShadowCastingMode.Off; l.receiveShadows = false;
            l.numCapVertices = 2;
            l.enabled = false;
            return l;
        }

        static void Log(string s) => Debug.Log("[VR] " + s);
    }
}
