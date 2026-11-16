using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>ふわふわ上下する（空に浮かぶ灯籠・ホログラムの看板など）。spin で回る。</summary>
    public class Floaty : MonoBehaviour
    {
        public float amplitude = 0.4f, speed = 0.6f, spin;
        Vector3 _base;
        float _seed;

        void Start()
        {
            _base = transform.localPosition;
            _seed = Random.value * 20f;
        }

        void Update()
        {
            transform.localPosition = _base + Vector3.up * (Mathf.Sin(Time.time * speed + _seed) * amplitude);
            if (spin != 0) transform.Rotate(0, spin * Time.deltaTime, 0, Space.World);
        }
    }
}
