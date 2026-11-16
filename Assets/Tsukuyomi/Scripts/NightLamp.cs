using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>夜だけつく明かり（灯籠・提灯のそばのライト）。</summary>
    [RequireComponent(typeof(Light))]
    public class NightLamp : MonoBehaviour
    {
        Light _light;
        float _base;

        void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
        }

        void Update()
        {
            float n = RealTimeSky.Night;
            _light.enabled = n > 0.15f;
            _light.intensity = _base * n;
        }
    }
}
