using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>夜だけ出る粒（川べりの蛍）。朝だけ出す使い方もできる（朝もや）。</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class NightParticles : MonoBehaviour
    {
        public float rate = 30f;
        public bool morningOnly;
        ParticleSystem _ps;

        void Awake() => _ps = GetComponent<ParticleSystem>();

        void Update()
        {
            float k;
            if (morningOnly)
            {
                var h = Ts.Now.TimeOfDay.TotalHours;
                k = (float)(h >= 5 && h < 9 ? 1 - System.Math.Abs(h - 7) / 2 : 0);
            }
            else k = Mathf.Clamp01((RealTimeSky.Night - 0.4f) / 0.6f);
            var em = _ps.emission;
            em.rateOverTime = rate * k;
        }
    }
}
