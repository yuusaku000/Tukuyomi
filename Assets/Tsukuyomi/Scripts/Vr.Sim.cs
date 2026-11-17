using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace Tsukuyomi
{
    /// <summary>
    /// まねる機器（--vr-sim）。XR Interaction Toolkit の XR Device Simulator と同じ「まねの頭とコントローラー」を作り、
    /// マウスとキーで動かす。本物の機器と同じ道すじ（Input System の XR の入力）で読まれるので、VR の操作をそのまま確かめられる。
    ///  マウス：見回す（クリックで操作開始）　左クリック：右手のトリガー（板を押す・竿）
    ///  右クリックを押している間：右スティックを前に（ワープの弧）、離すとワープ　Z・X：右スティックを左右（回る）
    ///  WASD など：いつものキー（Act にそのまま入る）
    /// tour からは Vr.Test で動かす。
    /// </summary>
    public partial class Vr
    {
        static XRSimulatedHMD _hmd;
        static XRSimulatedController _lc, _rc;
        /// <summary>まねる頭の左右と上下の向き（度）。</summary>
        public static float SimYaw, SimPitch = 5f;
        /// <summary>tour から：右スティック・トリガー・グリップをこの値にしておく。</summary>
        /// <summary>確認用：右手を左右に振りつづける（KASSEN の振る攻撃）。</summary>
        public static bool SimSwing;
        public static Vector2 SimStick; public static float SimTrigger, SimGrip;
        static bool _simByTour;

        static void SimStart()
        {
            _hmd = InputSystem.AddDevice<XRSimulatedHMD>();
            _lc = InputSystem.AddDevice<XRSimulatedController>();
            _rc = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(_lc, UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(_rc, UnityEngine.InputSystem.CommonUsages.RightHand);
            Debug.Log("[VR] まねる機器を足しました（頭・左手・右手）");
        }

        void SimTick()
        {
            if (_hmd == null) return;
            var cam = Cam;
            Vector2 stick = SimStick; float trig = SimTrigger, grip = SimGrip;
            if (Ts.ShotMode)
            {
                // tour：頭の上下は「カメラ」の2つ目の値（cam.pitch）に合わせる
                if (!_simByTour && cam != null) SimPitch = cam.pitch;
            }
            else
            {
                if (Cursor.lockState == CursorLockMode.Locked && !Hud.Blocked)
                {
                    var m = Act.LookMouse * 2.4f;
                    SimYaw += m.x; SimPitch = Mathf.Clamp(SimPitch - m.y, -80, 80);
                }
                else if (Hud.Blocked || Choosing)
                {
                    // 板を開いている間は、カーソルの位置で指す向きを変える
                    var mp = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                    _pointYaw = (mp.x / Mathf.Max(1, Screen.width) - 0.5f) * 70f;
                    _pointPitch = -(mp.y / Mathf.Max(1, Screen.height) - 0.5f) * 45f;
                }
                var ms = Mouse.current; var k = Keyboard.current;
                if (ms != null && ms.leftButton.isPressed) trig = 1;
                if (ms != null && ms.rightButton.isPressed) stick.y = 1;
                if (k != null && k.zKey.isPressed) stick.x = -1;
                if (k != null && k.xKey.isPressed) stick.x = 1;
            }
            if (!Hud.Blocked && !Choosing) { _pointYaw = 0; _pointPitch = 0; }

            var headRot = Quaternion.Euler(SimPitch, SimYaw, 0);
            var head = new Vector3(0, 1.55f, 0);
            var hs = new XRSimulatedHMDState();
            hs.Reset();
            hs.centerEyePosition = hs.devicePosition = head;
            hs.centerEyeRotation = hs.deviceRotation = headRot;
            hs.leftEyePosition = head + headRot * new Vector3(-0.032f, 0, 0); hs.leftEyeRotation = headRot;
            hs.rightEyePosition = head + headRot * new Vector3(0.032f, 0, 0); hs.rightEyeRotation = headRot;
            hs.isTracked = true; hs.trackingState = 3;
            InputSystem.QueueStateEvent(_hmd, hs);

            // 手は頭の前の下あたり。右手は見ている先（板を開いているときはカーソルの先）を指す
            var yawOnly = Quaternion.Euler(0, SimYaw, 0);
            var aim = head + Quaternion.Euler(SimPitch + _pointPitch, SimYaw + _pointYaw, 0) * Vector3.forward * 3f;
            var rp = head + yawOnly * new Vector3(0.17f, -0.32f, 0.33f);
            // tour：板の上の決まった点を指す（遊ぶ場所の中の位置に直す）
            if (SimAimAt != null && cam != null && SimAimAt.canvas != null)
            {
                var ct = (RectTransform)SimAimAt.canvas.transform; var sz = ct.sizeDelta;
                var w = ct.TransformPoint(new Vector3(SimAimPx.x - sz.x / 2, sz.y / 2 - SimAimPx.y, 0));
                aim = Quaternion.Inverse(OriginRot(cam)) * (w - OriginPos(cam));
            }
            if (SimSwing) rp += yawOnly * new Vector3(Mathf.Sin(Time.time * 12f) * 0.35f, 0, 0); // 確認用：右手を左右に振る（最大 4m/秒）
            var lp = head + yawOnly * new Vector3(-0.19f, -0.36f, 0.3f);
            var rr = Quaternion.LookRotation(aim - rp, Vector3.up);
            var lr = Quaternion.Euler(SimPitch * 0.5f + 10f, SimYaw - 8f, 6f);
            QueueHand(_rc, rp, rr, stick, trig, grip);
            QueueHand(_lc, lp, lr, Vector2.zero, 0, 0.2f);
        }
        static float _pointYaw, _pointPitch;
        /// <summary>tour：この板の、この点（左上からのピクセル）を右手で指す。</summary>
        static Board SimAimAt; static Vector2 SimAimPx;

        static void QueueHand(XRSimulatedController c, Vector3 p, Quaternion r, Vector2 stick, float trig, float grip)
        {
            var s = new XRSimulatedControllerState();
            s.Reset();
            s.devicePosition = p; s.deviceRotation = r;
            s.primary2DAxis = stick; s.trigger = trig; s.grip = grip;
            s = s.WithButton(ControllerButton.TriggerButton, trig > 0.5f).WithButton(ControllerButton.GripButton, grip > 0.5f);
            s.isTracked = true; s.trackingState = 3;
            InputSystem.QueueStateEvent(c, s);
        }

        // ───────── tour の確かめ（vr:動き）─────────

        /// <summary>
        /// tour の4つ目「vr:動き」。hands（手を見る）・point（メニューを指す）・click（指してトリガーで押す）・
        /// warp（ワープの弧）・go（ワープする）・turn（一気に回る）・smooth（少しずつ回る）・walk（歩いてふちが暗くなる）・settings（設定を試して記録）
        /// </summary>
        public static void Test(string what)
        {
            if (I == null) { Debug.Log("[VR] VR モードではありません（--vr-sim を付けて起動）"); return; }
            I.StopAllCoroutines();
            I.StartCoroutine(I.TestRun(what));
        }

        IEnumerator TestRun(string what)
        {
            _simByTour = true; SimAimAt = null;
            SimStick = Vector2.zero; SimTrigger = 0; SimGrip = 0; SimYaw = 0;
            var cam = Cam;
            switch (what)
            {
                case "hands":
                    SimPitch = 28f; SimGrip = 0.9f;
                    break;
                case "point":
                case "click":
                    Hud.MenuStart = 7; Hud.OpenMenu(true);
                    SimPitch = 3f;
                    yield return new WaitForSeconds(0.6f);
                    // メニューの「VR」の分類の、1行目（画質）を指す
                    foreach (var b in Board.All) if (b != null && b.name.Contains("メニュー")) SimAimAt = b;
                    SimAimPx = new Vector2(760, 302);
                    yield return new WaitForSeconds(0.5f);
                    Log($"光の線が板にあたっている={RayHit}");
                    if (what == "click")
                    {
                        int before = QualityLevel;
                        SimTrigger = 1; yield return new WaitForSeconds(0.25f); SimTrigger = 0; yield return new WaitForSeconds(0.3f);
                        Log($"トリガーで押した：VR の画質 {QNames[before]} → {QualityName}");
                    }
                    break;
                case "warp":
                case "go":
                    SimPitch = 20f; SimStick = new Vector2(0, 1);
                    yield return new WaitForSeconds(0.6f);
                    Log($"ワープの弧：行ける={_aimOk} 行き先=({_aimPoint.x:0.0}, {_aimPoint.y:0.0}, {_aimPoint.z:0.0})");
                    if (what == "go")
                    {
                        var from = Player.Position;
                        SimStick = Vector2.zero; yield return new WaitForSeconds(0.3f);
                        Log($"ワープ：{(Player.Position - from).magnitude:0.0}m 動いた");
                        SimPitch = 10f;
                    }
                    break;
                case "turn":
                case "smooth":
                    {
                        bool keep = SnapTurn; SnapTurn = what == "turn";
                        float y0 = cam.yaw;
                        SimStick = new Vector2(1, 0); yield return new WaitForSeconds(what == "turn" ? 0.2f : 0.9f);
                        SimStick = Vector2.zero; yield return new WaitForSeconds(0.2f);
                        if (what == "turn") { SimStick = new Vector2(1, 0); yield return new WaitForSeconds(0.2f); SimStick = Vector2.zero; }
                        Log($"{(SnapTurn ? "一気に" : "少しずつ")}回る：{Mathf.DeltaAngle(y0, cam.yaw):0.0}度");
                        SnapTurn = keep;
                        break;
                    }
                case "walk":
                    // 歩いている間の視界のふち（左スティックの代わりに、Move の値を直接は変えられないので、体を少しずつ前へ）
                    for (float t = 0; t < 1.5f; t += Time.deltaTime)
                    {
                        Player.Carry(Quaternion.Euler(0, cam.yaw, 0) * Vector3.forward * (3.5f * Time.deltaTime), 0);
                        yield return null;
                    }
                    Log($"歩いている間の視界のふち：強さ {VignetteNow:0.00}（設定 {(Vignette ? "オン" : "オフ")}）");
                    for (float t = 0; t < 3f; t += Time.deltaTime)
                    {
                        Player.Carry(Quaternion.Euler(0, cam.yaw, 0) * Vector3.forward * (3.5f * Time.deltaTime), 0);
                        yield return null;
                    }
                    break;
                case "settings":
                    Hud.TestVrSettings();
                    break;
            }
        }
    }
}
