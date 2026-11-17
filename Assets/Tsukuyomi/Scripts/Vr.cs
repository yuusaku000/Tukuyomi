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
        static int _q = -1, _snap = -1, _vig = -1, _warp = -1; static float _h = float.NaN;
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
            QualityLevel = 0; SnapTurn = true; Vignette = true; Warp = true; Height = 0f;
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
        static IEnumerable<InputAction> Reads => new[] { _headPos, _headRot, _lPos, _lRot, _rPos, _rRot, _rStick, _lGrip, _rGrip, _lTrig, _rTrig };

        static Quaternion Q(InputAction a)
        {
            var q = a.ReadValue<Quaternion>();
            return q.x == 0 && q.y == 0 && q.z == 0 && q.w == 0 ? Quaternion.identity : q;
        }
        /// <summary>頭の位置（床からの、遊ぶ場所の中の位置）。</summary>
        public static Vector3 HeadPos => _headPos.ReadValue<Vector3>();
        public static Quaternion HeadRot => Q(_headRot);
        /// <summary>頭の左右の向き（体の向きからのずれ）。歩く向きに使う。</summary>
        public static float HeadYaw => On ? HeadRot.eulerAngles.y : 0f;

        /// <summary>遊ぶ場所の原点が床か（false なら目の高さが原点）。</summary>
        static bool _floor = true;

        // ───────── 始める ─────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            foreach (var a in Reads) a.Enable();
            Act.AddXrBindings();
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
        public static Vector3 OriginPos(OrbitCamera cam) => cam.target.position + Vector3.up * (Height + (_floor ? 0f : cam.eyeHeight));
        public static Quaternion OriginRot(OrbitCamera cam) => Quaternion.Euler(0, cam.yaw, 0);

        /// <summary>カメラを頭の位置と向きに置く。</summary>
        public static void PlaceCamera(OrbitCamera cam)
        {
            if (cam == null || cam.target == null) return;
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
            var cam = Cam; var pl = Player;
            if (cam == null || pl == null) return;
            bool free = !Hud.Blocked && (!Ts.ShotMode || _simByTour) && !PlayerController.Busy && !Kassen.Busy && !Choosing;
            var rs = _rStick.ReadValue<Vector2>();

            // 回る
            bool turning = false;
            if (!_aiming && Mathf.Abs(rs.x) > Mathf.Abs(rs.y))
            {
                if (SnapTurn)
                {
                    if (Mathf.Abs(rs.x) > 0.7f && !_turnHeld) { Turn(cam, 45f * Mathf.Sign(rs.x)); _turnHeld = true; _flash = 0.25f; }
                }
                else if (Mathf.Abs(rs.x) > 0.2f) { Turn(cam, rs.x * 100f * Time.deltaTime); turning = true; }
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
            Curl(_lFingers, _lGrip.ReadValue<float>(), _lTrig.ReadValue<float>());
            Curl(_rFingers, _rGrip.ReadValue<float>(), _rTrig.ReadValue<float>());
        }

        static void Pose(Transform t, Vector3 p, Quaternion r) { t.localPosition = p; t.localRotation = r; }

        void Turn(OrbitCamera cam, float deg)
        {
            // 頭のまわりで回る（体のまわりだと、頭が横にずれて酔いやすい）
            var head = cam.transform.position;
            cam.yaw += deg;
            var after = OriginPos(cam) + OriginRot(cam) * HeadPos;
            var shift = head - after; shift.y = 0;
            if (shift.sqrMagnitude > 1e-6f && Player != null && shift.sqrMagnitude < 1f) Player.Carry(shift, 0);
            if (Sim && Mathf.Abs(deg) > 10f) Log($"回った {deg:0.#}度 → 向き {Mathf.Repeat(cam.yaw, 360):0}");
        }

        /// <summary>今いる板を、光の線で指す（板にあたったところに点）。</summary>
        void UpdateRay()
        {
            bool show = Choosing || Hud.Blocked || Kassen.Busy;
            var o = _rHand.position; var dir = _rHand.forward;
            float best = 4f; bool hit = false;
            if (show)
                foreach (var b in Board.All)
                {
                    if (b == null || b.passive || b.canvas == null || !b.canvas.enabled || b.canvas.renderMode != RenderMode.WorldSpace || b.Alpha < 0.01f) continue;
                    var ct = (RectTransform)b.canvas.transform;
                    var plane = new Plane(ct.forward, ct.position);
                    if (!plane.Raycast(new Ray(o, dir), out var t) || t <= 0 || t >= best) continue;
                    var lp = ct.InverseTransformPoint(o + dir * t);
                    var sz = ct.sizeDelta;
                    if (Mathf.Abs(lp.x) <= sz.x * 0.5f && Mathf.Abs(lp.y) <= sz.y * 0.5f) { best = t; hit = true; _hitBoard = b; _hitPoint = o + dir * t; }
                }
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
            UnityEngine.UI.Selectable over = null;
            if (b != null)
                foreach (var s in b.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                {
                    if (!s.IsInteractable()) continue;
                    var rt = (RectTransform)s.transform;
                    var lp = rt.InverseTransformPoint(_hitPoint);
                    if (rt.rect.Contains(new Vector2(lp.x, lp.y))) over = s; // 後ろに並ぶもの（上に描かれるもの）を優先
                }
            var es = EventSystem.current;
            var ev = es != null ? new PointerEventData(es) { button = PointerEventData.InputButton.Left } : null;
            if (over != _hover && ev != null)
            {
                if (_hover != null) ExecuteEvents.Execute(_hover.gameObject, ev, ExecuteEvents.pointerExitHandler);
                if (over != null) ExecuteEvents.Execute(over.gameObject, ev, ExecuteEvents.pointerEnterHandler);
            }
            _hover = over;
            float tr = _rTrig.ReadValue<float>();
            bool pressed = tr > 0.6f && _trigPrev <= 0.6f;
            _trigPrev = tr;
            if (pressed && over != null && ev != null)
            {
                ExecuteEvents.Execute(over.gameObject, ev, ExecuteEvents.pointerClickHandler);
                if (Sim) Log($"押した：{over.name}");
            }
        }

        /// <summary>光の線が板にあたっている（確認用）。</summary>
        public static bool RayHit;

        // ───────── ワープ ─────────

        void AimArc(OrbitCamera cam)
        {
            var pts = new List<Vector3>();
            var p = _rHand.position; var vel = _rHand.forward * 9f;
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
            var off = OriginRot(cam) * HeadPos; off.y = 0;
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
