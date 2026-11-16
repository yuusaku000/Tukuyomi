using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>決まった道を行ったり来たりする（屋形船）。向きは進む方へ。水面に合わせて少しゆれる。</summary>
    public class PathMover : MonoBehaviour
    {
        public Vector3[] points;
        public float speed = 1.6f, startAt;
        public float bob = 0.05f;

        float _len, _pos;
        int _dir = 1;
        float[] _acc;

        void Start()
        {
            if (points == null || points.Length < 2) { enabled = false; return; }
            _acc = new float[points.Length];
            for (int i = 1; i < points.Length; i++) _acc[i] = _acc[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            _len = _acc[points.Length - 1];
            _pos = Mathf.Repeat(startAt, _len);
        }

        Vector3 Sample(float s)
        {
            s = Mathf.Clamp(s, 0, _len);
            int i = 1;
            while (i < points.Length - 1 && _acc[i] < s) i++;
            float seg = _acc[i] - _acc[i - 1];
            float t = seg > 0 ? (s - _acc[i - 1]) / seg : 0;
            return Vector3.Lerp(points[i - 1], points[i], t);
        }

        void Update()
        {
            _pos += _dir * speed * Time.deltaTime;
            if (_pos > _len - 2) { _pos = _len - 2; _dir = -1; }
            if (_pos < 2) { _pos = 2; _dir = 1; }
            var p = Sample(_pos);
            var ahead = Sample(_pos + _dir * 3f);
            var fwd = ahead - p; fwd.y = 0;
            p.y += Mathf.Sin(Time.time * 1.3f + startAt) * bob;
            transform.position = p;
            if (fwd.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.9f) * 1.5f), 1 - Mathf.Exp(-2f * Time.deltaTime));
        }
    }
}
