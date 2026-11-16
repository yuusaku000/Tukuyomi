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
            go.transform.position = area + Vector3.up * 90f;
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.loop = true; main.playOnAwake = false; // 止まってしまうと、出した火花が描かれなくなる
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(3.5f, 5.5f); // 島から 200m ほど離れて見えるので大きめに
            main.gravityModifier = 0.35f;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = _ps.emission; em.rateOverTime = 0;
            var sh = _ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var drag = _ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 0.7f;
            drag.multiplyDragByParticleSize = false; drag.multiplyDragByParticleVelocity = false; // 大きさ・速さをかけると一瞬で止まって点になっていた drag.limit = 100f;
            var col = _ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = _ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = sparkMat;
            r.maxParticleSize = 0.5f;
            _ps.Play(); // 止まったままだと、出した火花が動かず見えないことがある
            _audio = go.AddComponent<AudioSource>();
            _audio.spatialBlend = 0.4f; _audio.volume = 0.7f; _audio.priority = 60;
            _boom = Synth.Boom();
            Debug.Log($"[Fireworks] ready mat={(sparkMat != null ? sparkMat.shader.name : "null")} area={area}");
        }

        void Update()
        {
            var h = Ts.Now.Hour;
            if (h < 19 || h >= 23) return;
            _next -= Time.deltaTime;
            if (_next > 0) return;
            _next = Random.Range(3f, 9f) * (Random.value < 0.15f ? 0.3f : 1f);
            if (Ts.Flag("--fireworks")) _next = 0.5f; // 確認用：たくさん上げる
            var pos = area + new Vector3(Random.Range(-spread, spread), Random.Range(70f, 120f), Random.Range(-spread * 0.4f, spread * 0.4f));
            var c = Colors[Random.Range(0, Colors.Length)];
            // 1つずつ外向きの速さをつけて出す（まん丸に広がる）
            int n = Random.Range(180, 260);
            float sp = Random.Range(22f, 30f);
            var c2 = Colors[Random.Range(0, Colors.Length)];
            for (int i = 0; i < n; i++)
            {
                var dir = Random.onUnitSphere;
                var ep = new ParticleSystem.EmitParams { position = pos, velocity = dir * sp * Random.Range(0.85f, 1f), startColor = i % 4 == 0 ? c2 : c, startLifetime = Random.Range(1.8f, 2.8f) };
                _ps.Emit(ep, 1);
            }
            if (Ts.ShotMode) Debug.Log($"[Fireworks] boom at {pos} particles={_ps.particleCount} playing={_ps.isPlaying}");
            _audio.transform.position = pos;
            if (!Ts.ShotMode) _audio.PlayOneShot(_boom, 0.8f * SoundPrefs.Ambient);
        }
    }
}
