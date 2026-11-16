using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>風でゆれる（のぼり・風鈴・藤・番傘）。</summary>
    public class Sway : MonoBehaviour
    {
        public float angle = 4f, speed = 1f;
        public Vector3 axis = Vector3.forward;
        Quaternion _base;
        float _seed;

        void Start()
        {
            _base = transform.localRotation;
            _seed = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time * speed + _seed;
            float a = (Mathf.Sin(t) * 0.7f + Mathf.Sin(t * 2.3f) * 0.3f) * angle;
            transform.localRotation = _base * Quaternion.AngleAxis(a, axis);
        }
    }
}
