using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>夜だけつく明かり（灯籠・提灯のそばのライト）。</summary>
    [RequireComponent(typeof(Light))]
    public class NightLamp : MonoBehaviour
    {
        Light _light;
        float _base, _checkT;
        bool _near = true;
        static Transform _cam;
        /// <summary>これより遠い明かりは消す（軽くするため。遠くの明かりは窓や提灯の光る色で見える）。</summary>
        public static float Range = 70f;

        void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
        }

        void Update()
        {
            float n = RealTimeSky.Night;
            _checkT -= Time.deltaTime;
            if (_checkT <= 0)
            {
                _checkT = 0.4f + Random.value * 0.3f;
                if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
                if (_cam != null) _near = (transform.position - _cam.position).sqrMagnitude < (Range + _light.range) * (Range + _light.range);
            }
            _light.enabled = n > 0.15f && _near;
            _light.intensity = _base * n;
        }
    }
}
