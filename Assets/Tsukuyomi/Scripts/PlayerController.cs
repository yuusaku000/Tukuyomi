using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>WASD で歩く・Shift で走る・Space でジャンプ。海や川に落ちたら、少しして岸にもどす。</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public OrbitCamera cam;
        public DollAnimator doll;
        public float walkSpeed = 4.6f, runSpeed = 9.5f, jumpSpeed = 6.5f, gravity = -22f;

        CharacterController _cc;
        Vector3 _move;
        float _vy, _wetTime, _airT;
        Vector3 _safePos;
        float _safeYaw;

        /// <summary>デバッグ用の飛行モード（F4）。壁もすりぬける。Space で上、Ctrl / Q で下、Shift で速く。</summary>
        public static bool Fly;

        public Vector3 Position => transform.position;
        public float Yaw => transform.eulerAngles.y;
        public bool InWater => transform.position.y < 0.25f || Island.InWater(transform.position);

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _safePos = transform.position;
        }

        /// <summary>釣りの最中・座っている間は歩かない（向きは Fishing / Rest が決める）。</summary>
        public static bool Busy => Fishing.Busy || Rest.Sitting;

        void Update()
        {
            if (Busy) { if (doll != null) doll.speed = 0; return; }
            Vector2 input = Vector2.zero;
            if (!Hud.Blocked && !Ts.ShotMode && !LiveShow.AutoCam)
            {
                input = Act.Move.ReadValue<Vector2>();
            }
            input = Vector2.ClampMagnitude(input, 1);
            if (!Hud.Blocked && !Ts.ShotMode && Input.GetKeyDown(KeyCode.F4))
            {
                Fly = !Fly;
                _cc.enabled = !Fly;
                _vy = 0;
                Hud.Toast(Fly ? "飛行モード（F4 でもどる）：Space 上・Ctrl/Q 下・Shift 速く" : "歩きにもどりました");
            }
            if (Fly)
            {
                float yawF = cam != null ? cam.MoveYaw : 0;
                var f = Quaternion.Euler(0, yawF, 0) * Vector3.forward;
                var rgt = Quaternion.Euler(0, yawF, 0) * Vector3.right;
                float up = 0;
                if (!Hud.Blocked && !Ts.ShotMode)
                {
                    if (Act.Jump.Held || Act.Interact.Held) up += 1;
                    if (Act.Descend.Held) up -= 1;
                }
                float sp = Act.Run.Held ? 40f : 12f;
                transform.position += (f * input.y + rgt * input.x + Vector3.up * up) * sp * Time.deltaTime;
                transform.rotation = Quaternion.Euler(0, yawF, 0);
                if (doll != null) doll.speed = 0;
                return;
            }
            float camYaw = cam != null ? cam.MoveYaw : 0;
            var fwd = Quaternion.Euler(0, camYaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, camYaw, 0) * Vector3.right;
            bool run = Act.Run.Held;
            float speed = (run ? runSpeed : walkSpeed) * (InWater ? 0.45f : 1f);
            var want = (fwd * input.y + right * input.x) * speed;
            _move = Vector3.Lerp(_move, want, 1 - Mathf.Exp(-12f * Time.deltaTime));

            if (cam != null && (cam.firstPerson || Vr.On))
                transform.rotation = Quaternion.Euler(0, cam.MoveYaw, 0);
            else if (want.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(new Vector3(want.x, 0, want.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, face, 1 - Mathf.Exp(-12f * Time.deltaTime));
            }

            bool jumped = false;
            if (_cc.isGrounded) _airT = 0; else _airT += Time.deltaTime;
            if (_cc.isGrounded || (_airT < 0.18f && _vy <= 0))
            {
                // 走って段の角を越えたときに少し浮いても、地面に吸いつける（浮くと段を登れなくなっていた）
                if (_vy < 0) _vy = -7f;
                if (!Hud.Blocked && Act.Jump.Down && !InWater) { _vy = jumpSpeed; jumped = true; _airT = 1f; }
                if (transform.position.y > 0.6f && !InWater && (SkyShip.I == null || !SkyShip.I.OnDeck(transform.position))) { _safePos = transform.position; _safeYaw = transform.eulerAngles.y; }
            }
            // ライブの「無重力」：会場では、決まった高さまでふわっと浮く
            float fh = LiveShow.FloatHeight;
            if (fh > 0.05f && LiveShow.PlayerInVenue) _vy = Mathf.Clamp((Island.Ground + 0.1f + fh - transform.position.y) * 2.2f, -4f, 5f);
            else _vy += gravity * Time.deltaTime;
            var before = transform.position;
            var flags = _cc.Move((_move + Vector3.up * _vy) * Time.deltaTime);
            // 段にぶつかって止まったら、段の高さぶん持ち上げてもう一度すすむ（走っていても石段を登れるように）
            var flat = new Vector3(_move.x, 0, _move.z);
            if ((flags & CollisionFlags.Sides) != 0 && !jumped && _airT < 0.2f && flat.sqrMagnitude > 1f)
            {
                var moved = transform.position - before; moved.y = 0;
                if (moved.magnitude < flat.magnitude * Time.deltaTime * 0.5f)
                {
                    var from = transform.position;
                    _cc.Move(Vector3.up * 0.55f);
                    _cc.Move(flat.normalized * Mathf.Max(0.12f, flat.magnitude * Time.deltaTime));
                    _cc.Move(Vector3.down * 0.7f);
                    float up = transform.position.y - from.y;
                    var gone = transform.position - from; gone.y = 0;
                    if (up < 0.02f || up > 0.56f || gone.magnitude < 0.05f)
                    {
                        _cc.enabled = false; transform.position = from; _cc.enabled = true;
                    }
                }
            }

            if (doll != null) doll.speed = new Vector3(_move.x, 0, _move.z).magnitude;

            // 水に落ちたら、少しして最後に立っていた陸へもどす
            _wetTime = InWater ? _wetTime + Time.deltaTime : 0;
            if (_wetTime > 2.5f || transform.position.y < -20f)
            {
                _wetTime = 0;
                Teleport(_safePos + Vector3.up * 0.2f, _safeYaw);
                Hud.Toast("水に落ちちゃった！ 岸にもどりました");
            }
        }

        /// <summary>乗り物（遊覧船）といっしょに動かす：歩く勢いは消さない。</summary>
        public void Carry(Vector3 delta, float yawDelta)
        {
            transform.position += delta;
            if (Mathf.Abs(yawDelta) > 0.0001f)
            {
                transform.Rotate(0, yawDelta, 0, Space.World);
                if (cam != null) cam.yaw += yawDelta;
            }
            Physics.SyncTransforms();
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            _cc.enabled = !Fly;
            _move = Vector3.zero; _vy = 0;
            _safePos = pos; _safeYaw = yaw;
            if (cam != null) { cam.yaw = yaw; cam.Snap(); }
        }
    }
}
