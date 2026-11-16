using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// カメラ。三人称（自分の人形を後ろから）と一人称（自分の目線）を V キーで切りかえる。
    /// 三人称でホイールをいちばん近くまで回しても一人称になり、一人称でホイールを引くと三人称にもどる。
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float yaw, pitch = 14f, distance = 6f;
        public float minDistance = 1.6f, maxDistance = 16f, height = 1.45f, sensitivity = 2.4f;
        public bool firstPerson;
        public float eyeHeight = 1.52f;

        float _cur;
        readonly List<Renderer> _body = new List<Renderer>();
        bool _bodyHidden;

        void Start()
        {
            _cur = distance;
            if (target != null) target.GetComponentsInChildren(true, _body);
        }

        public void Toggle()
        {
            firstPerson = !firstPerson;
            if (!firstPerson && distance < 2.5f) distance = 4.5f;
            Hud.Toast(firstPerson ? "一人称にしました（V でもどす）" : "三人称にしました（V で一人称）");
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (!Hud.Blocked && !Ts.ShotMode && Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * sensitivity;
                pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            }
            if (!Hud.Blocked && !Ts.ShotMode)
            {
                if (Input.GetKeyDown(KeyCode.V)) Toggle();
                float wheel = Input.mouseScrollDelta.y;
                if (firstPerson)
                {
                    if (wheel < -0.1f) { firstPerson = false; distance = 3f; }
                }
                else if (wheel != 0)
                {
                    distance = Mathf.Clamp(distance - wheel * 0.8f, minDistance, maxDistance);
                    if (wheel > 0 && distance <= minDistance + 0.01f) { firstPerson = true; Hud.Toast("一人称にしました（V でもどす）"); }
                }
            }

            if (firstPerson)
            {
                pitch = Mathf.Clamp(pitch, -80f, 80f);
                var rot = Quaternion.Euler(pitch, yaw, 0);
                transform.SetPositionAndRotation(target.position + Vector3.up * eyeHeight + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 0.15f, rot);
                _cur = 0.5f;
                SetBodyHidden(true);
                return;
            }
            SetBodyHidden(false);
            pitch = Mathf.Clamp(pitch, -25f, 75f);
            var pivot = target.position + Vector3.up * height;
            var r = Quaternion.Euler(pitch, yaw, 0);
            var dir = r * Vector3.back;
            float want = distance;
            if (Physics.SphereCast(pivot, 0.25f, dir, out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
                want = Mathf.Max(0.5f, hit.distance - 0.1f);
            _cur = want < _cur ? want : Mathf.Lerp(_cur, want, 1 - Mathf.Exp(-6f * Time.deltaTime));
            transform.SetPositionAndRotation(pivot + dir * _cur, r);
        }

        /// <summary>着がえで人形の部品が増えたり減ったりしたら呼ぶ（一人称で隠す部品を数えなおす）。</summary>
        public void RefreshBody()
        {
            if (target == null) return;
            _body.Clear();
            target.GetComponentsInChildren(true, _body);
            foreach (var b in _body) if (b) b.enabled = !_bodyHidden;
        }

        void SetBodyHidden(bool hide)

        {
            if (_bodyHidden == hide) return;
            _bodyHidden = hide;
            foreach (var b in _body) if (b) b.enabled = !hide;
        }

        public void Snap()
        {
            _cur = distance;
            LateUpdate();
        }
    }
}
