using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>釣り堀の鯉：子の鯉がそれぞれゆっくり池の中を泳ぐ（近くにいるときだけ動かす）。</summary>
    public class PondFish : MonoBehaviour
    {
        Transform[] _fish;
        float[] _ph, _sp, _y;
        Transform _cam;

        void Start()
        {
            int n = transform.childCount;
            _fish = new Transform[n]; _ph = new float[n]; _sp = new float[n]; _y = new float[n];
            for (int i = 0; i < n; i++)
            {
                _fish[i] = transform.GetChild(i);
                _ph[i] = i * 2.39f;
                _sp[i] = 0.05f + (i % 5) * 0.012f;
                _y[i] = _fish[i].position.y;
            }
        }

        void Update()
        {
            if (_cam == null) { if (Camera.main == null) return; _cam = Camera.main.transform; }
            var p = Island.Pond;
            var c = p.center;
            if ((new Vector2(_cam.position.x, _cam.position.z) - c).sqrMagnitude > 90f * 90f) return;
            float t = Time.time;
            float ax = p.width / 2 - 1.6f, az = p.height / 2 - 1.4f;
            for (int i = 0; i < _fish.Length; i++)
            {
                float a = t * _sp[i] + _ph[i];
                float x = Mathf.Sin(a) * ax * (0.55f + 0.4f * Mathf.Sin(_ph[i] * 1.7f));
                float z = Mathf.Sin(a * 1.6f + _ph[i]) * az * (0.5f + 0.45f * Mathf.Cos(_ph[i]));
                float dx = Mathf.Cos(a) * ax * (0.55f + 0.4f * Mathf.Sin(_ph[i] * 1.7f)) * _sp[i];
                float dz = Mathf.Cos(a * 1.6f + _ph[i]) * 1.6f * az * (0.5f + 0.45f * Mathf.Cos(_ph[i])) * _sp[i];
                var f = _fish[i];
                f.position = new Vector3(c.x + x, _y[i], c.y + z);
                if (dx * dx + dz * dz > 1e-6f)
                    f.rotation = Quaternion.LookRotation(new Vector3(dx, 0, dz)) * Quaternion.Euler(0, Mathf.Sin(t * 3f + _ph[i]) * 10f, 0);
            }
        }
    }
}
