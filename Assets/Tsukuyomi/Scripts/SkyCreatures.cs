using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// ツクヨミの空を泳ぐもの：体が透けて光る魚の群れと、ゆっくり舞う大きなマンタ。
    /// 形はその場で作る（体＝細長い球、ひれ＝平たい球）。夜ほどはっきり光る（Holo シェーダー）。
    /// </summary>
    public class SkyCreatures : MonoBehaviour
    {
        public Material fishMat, fishMat2, mantaMat;
        public int schools = 9, fishPerSchool = 9, mantas = 4;
        public Vector3 center = new Vector3(0, 0, 150);
        public float radius = 140f;

        class Fish { public Transform t, tail; public Vector3 offset; public float phase; }
        class School { public Vector3 c; public float r, h, speed, ang, vy; public List<Fish> fish = new List<Fish>(); }
        class Manta { public Transform t, wl, wr; public float r, h, speed, ang, phase; public Vector3 c; }

        readonly List<School> _schools = new List<School>();
        readonly List<Manta> _mantas = new List<Manta>();

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        void Start()
        {
            for (int s = 0; s < schools; s++)
            {
                var sc = new School
                {
                    c = center + new Vector3(Random.Range(-radius, radius) * 0.6f, 0, Random.Range(-radius, radius) * 0.6f),
                    r = Random.Range(20f, 55f), h = Random.Range(48f, 80f), speed = Random.Range(0.05f, 0.12f) * (Random.value < 0.5f ? -1 : 1),
                    ang = Random.Range(0, Mathf.PI * 2), vy = Random.Range(0.2f, 0.5f),
                };
                var mat = s % 3 == 0 ? fishMat2 : fishMat;
                for (int i = 0; i < fishPerSchool; i++)
                {
                    var root = new GameObject("光る魚").transform;
                    root.SetParent(transform, false);
                    float size = Random.Range(0.7f, 1.4f);
                    Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, 0.42f, 1.3f) * size, mat);
                    Part(root, PrimitiveType.Sphere, new Vector3(0, 0.25f, -0.1f) * size, new Vector3(0.04f, 0.35f, 0.5f) * size, mat);
                    var tail = Part(root, PrimitiveType.Sphere, new Vector3(0, 0, -0.75f) * size, new Vector3(0.05f, 0.45f, 0.35f) * size, mat).transform;
                    // 透けて見える背骨
                    Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.03f, 0.03f, 1.1f) * size, mat);
                    sc.fish.Add(new Fish { t = root, tail = tail, offset = Random.insideUnitSphere * 4f, phase = Random.value * 10f });
                }
                _schools.Add(sc);
            }
            for (int m = 0; m < mantas; m++)
            {
                var root = new GameObject("空飛ぶマンタ").transform;
                root.SetParent(transform, false);
                Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(2.2f, 0.5f, 3.2f), mantaMat);
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, -3.2f), new Vector3(0.12f, 0.12f, 3.5f), mantaMat);
                foreach (float side in new[] { -1f, 1f })
                    Part(root, PrimitiveType.Sphere, new Vector3(side * 0.6f, 0, 1.75f), new Vector3(0.35f, 0.25f, 0.8f), mantaMat);
                Transform Wing(float side)
                {
                    var pivot = new GameObject("つばさ").transform;
                    pivot.SetParent(root, false);
                    pivot.localPosition = new Vector3(side * 0.9f, 0, 0);
                    var w = Part(pivot, PrimitiveType.Sphere, new Vector3(side * 2.4f, 0, -0.3f), new Vector3(5f, 0.22f, 2.6f), mantaMat);
                    w.transform.localRotation = Quaternion.Euler(0, side * -18f, 0);
                    return pivot;
                }
                _mantas.Add(new Manta
                {
                    t = root, wl = Wing(-1), wr = Wing(1),
                    c = center + new Vector3(Random.Range(-60f, 60f), 0, Random.Range(-60f, 60f)),
                    r = Random.Range(50f, 110f), h = Random.Range(70f, 95f), speed = Random.Range(0.035f, 0.06f) * (m % 2 == 0 ? 1 : -1),
                    ang = Random.Range(0, Mathf.PI * 2), phase = Random.value * 10f,
                });
            }
        }

        void Update()
        {
            float t = Time.time;
            foreach (var sc in _schools)
            {
                sc.ang += sc.speed * Time.deltaTime;
                var head = sc.c + new Vector3(Mathf.Cos(sc.ang) * sc.r, sc.h + Mathf.Sin(t * sc.vy) * 4f, Mathf.Sin(sc.ang) * sc.r * 0.7f);
                var tangent = new Vector3(-Mathf.Sin(sc.ang), 0, Mathf.Cos(sc.ang) * 0.7f) * Mathf.Sign(sc.speed);
                foreach (var f in sc.fish)
                {
                    var wobble = new Vector3(Mathf.Sin(t * 0.7f + f.phase), Mathf.Sin(t * 0.9f + f.phase * 1.3f) * 0.6f, Mathf.Cos(t * 0.6f + f.phase)) * 1.2f;
                    var p = head + f.offset + wobble;
                    f.t.position = p;
                    f.t.rotation = Quaternion.LookRotation(tangent + wobble * 0.08f);
                    f.tail.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 6f + f.phase) * 25f, 0);
                }
            }
            foreach (var m in _mantas)
            {
                m.ang += m.speed * Time.deltaTime;
                var p = m.c + new Vector3(Mathf.Cos(m.ang) * m.r, m.h + Mathf.Sin(t * 0.3f + m.phase) * 3f, Mathf.Sin(m.ang) * m.r);
                var tangent = new Vector3(-Mathf.Sin(m.ang), Mathf.Cos(t * 0.3f + m.phase) * 0.05f, Mathf.Cos(m.ang)) * Mathf.Sign(m.speed);
                m.t.position = p;
                m.t.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0, 0, -Mathf.Sign(m.speed) * 14f);
                float flap = Mathf.Sin(t * 1.4f + m.phase) * 22f;
                m.wl.localRotation = Quaternion.Euler(0, 0, -flap);
                m.wr.localRotation = Quaternion.Euler(0, 0, flap);
            }
        }
    }
}
