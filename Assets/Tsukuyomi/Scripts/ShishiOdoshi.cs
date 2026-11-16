using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>鹿威し：竹の筒に水がたまってゆっくり傾き、水をこぼすと戻って石を打つ（カコーン）。tube は支点のまわりで回す。</summary>
    public class ShishiOdoshi : MonoBehaviour
    {
        public Transform tube;
        public float period = 11f;
        float _t;
        AudioSource _src;

        void Start()
        {
            _t = Random.Range(0f, period);
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = Synth.Kokon();
            _src.spatialBlend = 1f; _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 4f; _src.maxDistance = 45f; _src.volume = 0.9f; _src.playOnAwake = false;
        }

        void Update()
        {
            if (tube == null) return;
            float prev = _t;
            _t += Time.deltaTime;
            float fill = period - 1.2f;
            if (prev < fill + 0.53f && _t >= fill + 0.53f && _src != null && !Ts.ShotMode) { _src.pitch = Random.Range(0.97f, 1.03f); _src.Play(); }
            float ang;
            if (_t < fill) ang = Mathf.Lerp(-14f, 6f, Mathf.Pow(_t / fill, 2.2f));          // 水がたまって、先が下がっていく
            else if (_t < fill + 0.35f) ang = Mathf.Lerp(6f, 32f, (_t - fill) / 0.35f);       // 水がこぼれる
            else ang = Mathf.Lerp(32f, -14f, Mathf.Clamp01((_t - fill - 0.35f) / 0.18f));    // もどって石を打つ
            if (_t >= period) _t = 0;
            tube.localRotation = Quaternion.Euler(ang, 0, 0);
        }
    }
}
