using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 休憩：ソファ・縁台・長椅子の前で E → 座る（E・WASD・Space で立つ）。
    /// 座っている間は町のざわめきが小さくなり、しばらく操作しないと画面の表示が消えて景色だけになる。
    /// </summary>
    public class Rest : MonoBehaviour
    {
        public static Rest I { get; private set; }
        public static bool Sitting => I != null && I._sitting;
        /// <summary>落ち着き具合 0〜1（座ると上がる。音と画面の表示に使う）。</summary>
        public static float Calm { get; private set; }
        /// <summary>座ってから何も操作していない秒数。</summary>
        public static float Idle => I != null && I._sitting ? I._idle : 0f;

        PlayerController _pc;
        DollAnimator _doll;
        OrbitCamera _cam;
        bool _sitting, _camSaved;
        Seat _seat;
        int _idx;
        float _yaw, _idle, _sitT, _savedPitch, _savedDist;
        Vector3 _spot;

        void Awake()
        {
            I = this;
            _pc = GetComponent<PlayerController>();
            _doll = _pc != null ? _pc.doll : GetComponentInChildren<DollAnimator>();
            _cam = _pc != null ? _pc.cam : null;
        }

        public bool Near(out Seat s, out int i)
        {
            s = null; i = -1;
            if (_sitting || Fishing.Busy || PlayerController.Fly) return false;
            return Seat.Near(_pc.Position, 1.7f, out s, out i);
        }

        public void Sit(Seat s, int i)
        {
            if (s == null || i < 0 || i >= s.spots.Length) return;
            _seat = s; _idx = i;
            _spot = s.spots[i];
            _yaw = s.yaw;
            if (s.bothSides)
            {
                var f = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                var toMe = _pc.Position - _spot; toMe.y = 0;
                if (Vector3.Dot(toMe, f) < 0) _yaw += 180f;
            }
            var fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(_spot + Vector3.down * 0.48f - fwd * 0.06f, Quaternion.Euler(0, _yaw, 0));
            if (_doll != null) { _doll.sit = true; _doll.speed = 0; }
            if (_cam != null)
            {
                _savedPitch = _cam.pitch; _savedDist = _cam.distance; _camSaved = true;
                _cam.yaw = _yaw;
                if (!_cam.firstPerson) { _cam.pitch = 9f; _cam.distance = 3.8f; }
                else _cam.pitch = 2f;
                _cam.Snap();
            }
            _sitting = true; _idle = 0; _sitT = 0;
            Hud.Toast("座りました。ゆっくりしていってね（E・WASD で立つ）");
        }

        public void Stand()
        {
            if (!_sitting) return;
            _sitting = false;
            if (_doll != null) _doll.sit = false;
            var fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            var p = _spot + fwd * 0.85f;
            float ground = _spot.y - 0.5f;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
            float camYaw = _cam != null ? _cam.yaw : _yaw;
            _pc.Teleport(new Vector3(p.x, ground + 0.08f, p.z), _yaw);
            if (_cam != null && _camSaved)
            {
                _cam.yaw = camYaw;
                if (!_cam.firstPerson) { _cam.pitch = _savedPitch; _cam.distance = _savedDist; }
                _camSaved = false;
                _cam.Snap();
            }
            var d = GameSession.Instance != null ? GameSession.Instance.Data : null;
            if (d != null && _sitT > 5f) { d.restMinutes += _sitT / 60f; GameSession.Instance.SaveItems(); }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Calm = Mathf.MoveTowards(Calm, _sitting ? 1f : 0f, dt * (_sitting ? 0.35f : 1.2f));
            if (!_sitting) return;
            _sitT += dt;
            _idle += dt;
            if (Ts.ShotMode) return;
            if (Input.anyKey || Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.05f || Input.mouseScrollDelta.y != 0) _idle = 0;
            if (Hud.Blocked) return;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Space))
                Stand();
        }

        /// <summary>確認用：いちばん近い席に座る（--tour の sit）。</summary>
        public void TestSit(float idle)
        {
            if (Seat.Near(_pc.Position, 60f, out var s, out int i)) { Sit(s, i); _idle = idle; Calm = 1f; }
            else Debug.LogWarning("[Tour] no seat near");
        }
    }
}
