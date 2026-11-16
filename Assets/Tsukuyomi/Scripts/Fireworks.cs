using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>夜（19時〜23時）、ときどき海の上に花火が上がる。音は少し遅れて届く。</summary>
    public class Fireworks : MonoBehaviour
    {
        public Material sparkMat;
        public Vector3 area = new Vector3(0, 0, -150);
        public float spread = 120f;

        ParticleSystem _ps;
        AudioSource _audio;
        AudioClip _boom;
        float _next = 6f;

        static readonly Color[] Colors =
        {
            new Color(1f, 0.4f, 0.7f), new Color(0.4f, 0.9f, 1f), new Color(1f, 0.85f, 0.4f), new Color(0.7f, 0.5f, 1f), new Color(1f, 0.5f, 0.3f), new Color(0.5f, 1f, 0.6f),
        };

        void Start()
        {
            var go = new GameObject("花火の火花");
            go.transform.SetParent(transform, false);
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = _ps.emission; em.rateOverTime = 0;
            var sh = _ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var drag = _ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.6f; drag.limit = 100f;
            var col = _ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = _ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = sparkMat;
            _audio = go.AddComponent<AudioSource>();
            _audio.spatialBlend = 0.4f; _audio.volume = 0.7f;
            _boom = Synth.Boom();
        }

        void Update()
        {
            var h = Ts.Now.Hour;
            if (h < 19 || h >= 23) return;
            _next -= Time.deltaTime;
            if (_next > 0) return;
            _next = Random.Range(3f, 9f) * (Random.value < 0.15f ? 0.3f : 1f);
            var pos = area + new Vector3(Random.Range(-spread, spread), Random.Range(55f, 95f), Random.Range(-spread * 0.4f, spread * 0.4f));
            var c = Colors[Random.Range(0, Colors.Length)];
            var ep = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true, startColor = c };
            _ps.Emit(ep, Random.Range(160, 260));
            _audio.transform.position = pos;
            if (!Ts.ShotMode) _audio.PlayOneShot(_boom, 0.8f);
        }
    }
}
