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
        float _vy, _wetTime;
        Vector3 _safePos;
        float _safeYaw;

        public Vector3 Position => transform.position;
        public float Yaw => transform.eulerAngles.y;
        public bool InWater => transform.position.y < 0.25f || Island.InWater(transform.position);

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _safePos = transform.position;
        }

        void Update()
        {
            Vector2 input = Vector2.zero;
            if (!Hud.Blocked && !Ts.ShotMode)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
            }
            input = Vector2.ClampMagnitude(input, 1);
            float camYaw = cam != null ? cam.yaw : 0;
            var fwd = Quaternion.Euler(0, camYaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, camYaw, 0) * Vector3.right;
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = (run ? runSpeed : walkSpeed) * (InWater ? 0.45f : 1f);
            var want = (fwd * input.y + right * input.x) * speed;
            _move = Vector3.Lerp(_move, want, 1 - Mathf.Exp(-12f * Time.deltaTime));

            if (cam != null && cam.firstPerson)
                transform.rotation = Quaternion.Euler(0, cam.yaw, 0);
            else if (want.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(new Vector3(want.x, 0, want.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, face, 1 - Mathf.Exp(-12f * Time.deltaTime));
            }

            if (_cc.isGrounded)
            {
                if (_vy < 0) _vy = -2f;
                if (!Hud.Blocked && Input.GetKeyDown(KeyCode.Space) && !InWater) _vy = jumpSpeed;
                if (transform.position.y > 0.6f && !InWater) { _safePos = transform.position; _safeYaw = transform.eulerAngles.y; }
            }
            _vy += gravity * Time.deltaTime;
            _cc.Move((_move + Vector3.up * _vy) * Time.deltaTime);

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

        public void Teleport(Vector3 pos, float yaw)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            _cc.enabled = true;
            _move = Vector3.zero; _vy = 0;
            _safePos = pos; _safeYaw = yaw;
            if (cam != null) { cam.yaw = yaw; cam.Snap(); }
        }
    }
}
