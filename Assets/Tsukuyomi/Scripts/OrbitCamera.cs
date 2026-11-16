using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>自分の人形を後ろから見るカメラ。マウスで見回し、ホイールで近づく。壁にめりこまない。</summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float yaw, pitch = 14f, distance = 6f;
        public float minDistance = 1.6f, maxDistance = 16f, height = 1.45f, sensitivity = 2.4f;

        float _cur;

        void Start() => _cur = distance;

        void LateUpdate()
        {
            if (target == null) return;
            if (!Hud.Blocked && !Ts.ShotMode && Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * sensitivity;
                pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            }
            if (!Hud.Blocked)
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.8f, minDistance, maxDistance);
            pitch = Mathf.Clamp(pitch, -25f, 75f);

            var pivot = target.position + Vector3.up * height;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var dir = rot * Vector3.back;
            float want = distance;
            if (Physics.SphereCast(pivot, 0.25f, dir, out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
                want = Mathf.Max(0.5f, hit.distance - 0.1f);
            _cur = want < _cur ? want : Mathf.Lerp(_cur, want, 1 - Mathf.Exp(-6f * Time.deltaTime));
            transform.SetPositionAndRotation(pivot + dir * _cur, rot);
        }

        public void Snap()
        {
            _cur = distance;
            LateUpdate();
        }
    }
}
