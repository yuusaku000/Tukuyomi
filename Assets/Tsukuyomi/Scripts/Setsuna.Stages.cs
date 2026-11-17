using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SETSUNA の舞台。0 水上の闘技場・1 千本鳥居・2 月面（重力が軽い）・3 桜の丘（花びらが舞う）・4 夜の竹林（蛍）。
    /// 戦う床（半径 ArenaR の円）はどれも同じ。まわりの景色と、月面の重力だけがちがう。
    /// </summary>
    public partial class Setsuna
    {
        public static readonly string[] StageNames = { "水上の闘技場", "千本鳥居", "月面", "桜の丘", "夜の竹林" };

        int _stage;
        /// <summary>重力（月面は軽い）。</summary>
        float _grav = 22f;

        class Drift { public Transform tr; public Vector3 v; public float phase; }
        readonly List<Drift> _drift = new List<Drift>();
        int _driftKind; // 1 花びら・2 蛍

        static readonly Color Red = new(0.78f, 0.16f, 0.12f), Black = new(0.08f, 0.07f, 0.07f), Gold = new(1f, 0.78f, 0.4f), TealC = new(0.2f, 1f, 1f);

        void Torii(Transform parent, Vector3 pos, float yawDeg, float size)
        {
            var g = new GameObject("鳥居").transform; g.SetParent(parent, false);
            g.localPosition = pos; g.localRotation = Quaternion.Euler(0, yawDeg, 0); g.localScale = Vector3.one * size;
            foreach (float sx in new[] { -1f, 1f }) Prim(PrimitiveType.Cylinder, g, new Vector3(sx * 1.4f, 1.7f, 0), new Vector3(0.24f, 1.7f, 0.24f), Red);
            Prim(PrimitiveType.Cube, g, new Vector3(0, 3.55f, 0), new Vector3(3.9f, 0.24f, 0.32f), Black);
            Prim(PrimitiveType.Cube, g, new Vector3(0, 3.3f, 0), new Vector3(3.6f, 0.2f, 0.28f), Red);
            Prim(PrimitiveType.Cube, g, new Vector3(0, 2.75f, 0), new Vector3(3.2f, 0.15f, 0.2f), Red);
        }

        /// <summary>千本鳥居：石畳の床。まわりを小さな鳥居がぐるりと囲み、前と後ろへ鳥居のトンネルが続く。</summary>
        void StageTorii()
        {
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.3f, 0), new Vector3(21f, 0.3f, 21f), new Color(0.46f, 0.45f, 0.44f));
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.75f, 0), new Vector3(400f, 0.1f, 400f), new Color(0.12f, 0.1f, 0.09f));
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * 360f;
                var p = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 11.2f);
                Torii(_arena, p, a + 90f, 0.9f);
            }
            // 前後へのびる鳥居のトンネル（だんだん高く）
            foreach (float dir in new[] { 1f, -1f })
                for (int i = 0; i < 26; i++)
                {
                    float z = dir * (13.5f + i * 1.6f);
                    Prim(PrimitiveType.Cube, _arena, new Vector3(0, -0.25f, z), new Vector3(4.6f, 0.2f, 1.7f), new Color(0.4f, 0.39f, 0.38f));
                    Torii(_arena, new Vector3(0, -0.1f + i * 0.12f, z), 0, 1.25f);
                    if (i % 4 == 0) foreach (float sx in new[] { -1f, 1f }) Ball(_arena, new Vector3(sx * 2.3f, 1.2f + i * 0.12f, z), 0.32f, Gold);
                }
            // 空の朱い灯り
            var rng = new System.Random(9);
            for (int i = 0; i < 40; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 14f + (float)rng.NextDouble() * 26f;
                Ball(_arena, new Vector3(Mathf.Cos(a) * r, 4f + (float)rng.NextDouble() * 14f, Mathf.Sin(a) * r), 0.3f + (float)rng.NextDouble() * 0.3f, i % 4 == 0 ? TealC : new Color(1f, 0.45f, 0.3f));
            }
        }

        /// <summary>月面：灰色の地面とクレーター、空に大きな地球。重力が軽く、ジャンプがふわっと高い。</summary>
        void StageMoon()
        {
            _grav = 9f;
            var gray = new Color(0.55f, 0.55f, 0.58f);
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.3f, 0), new Vector3(21f, 0.3f, 21f), new Color(0.62f, 0.62f, 0.65f));
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.4f, 0), new Vector3(500f, 0.1f, 500f), gray);
            var rng = new System.Random(5);
            for (int i = 0; i < 70; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 12f + (float)rng.NextDouble() * 120f, s = 2f + (float)rng.NextDouble() * (r * 0.12f);
                var p = new Vector3(Mathf.Cos(a) * r, -0.35f, Mathf.Sin(a) * r);
                Prim(PrimitiveType.Cylinder, _arena, p, new Vector3(s * 1.15f, 0.12f, s * 1.15f), new Color(0.68f, 0.68f, 0.7f)); // ふち
                Prim(PrimitiveType.Cylinder, _arena, p + Vector3.up * 0.02f, new Vector3(s, 0.12f, s), new Color(0.42f, 0.42f, 0.45f));
                if (i % 5 == 0) Prim(PrimitiveType.Sphere, _arena, p + new Vector3(s * 0.6f, 0.2f, 0), new Vector3(1.4f, 0.9f, 1.2f) * (0.6f + (float)rng.NextDouble()), new Color(0.5f, 0.5f, 0.52f));
            }
            // 地球（青い海と緑の陸と白い雲）
            var earth = new GameObject("地球").transform; earth.SetParent(_arena, false);
            earth.localPosition = new Vector3(-60f, 70f, 160f);
            Prim(PrimitiveType.Sphere, earth, Vector3.zero, Vector3.one * 60f, new Color(0.15f, 0.4f, 0.95f), true);
            for (int i = 0; i < 9; i++)
            {
                var d = Random.onUnitSphere; if (d.z > 0) d.z = -d.z;
                Prim(PrimitiveType.Sphere, earth, d * 26f, new Vector3(22f, 14f, 6f) * (0.6f + Random.value * 0.6f), i % 3 == 0 ? new Color(0.9f, 0.95f, 1f) : new Color(0.2f, 0.75f, 0.35f), true, Quaternion.LookRotation(d).eulerAngles);
            }
            // 宇宙の星（地面より上だけ）
            for (int i = 0; i < 60; i++)
            {
                var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.9f + 0.1f;
                Ball(_arena, d * 220f, 0.8f + Random.value * 1.2f, Color.white);
            }
            // 旗と着陸船（ツクヨミらしく、鳥居の形の旗）
            Torii(_arena, new Vector3(0, -0.3f, 13.5f), 0, 1.4f);
            Prim(PrimitiveType.Cube, _arena, new Vector3(12f, 1f, -9f), new Vector3(2.4f, 1.6f, 2.4f), new Color(0.85f, 0.75f, 0.35f));
            foreach (var o in new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) })
                Prim(PrimitiveType.Cylinder, _arena, new Vector3(12f, 0.2f, -9f) + o * 1.3f, new Vector3(0.1f, 0.6f, 0.1f), new Color(0.7f, 0.7f, 0.72f), false, new Vector3(o.z * 20, 0, -o.x * 20));
        }

        /// <summary>桜の丘：草の床、まわりに桜の木。花びらが舞う。</summary>
        void StageSakura()
        {
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.3f, 0), new Vector3(21f, 0.3f, 21f), new Color(0.36f, 0.5f, 0.28f));
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.4f, 0), new Vector3(400f, 0.1f, 400f), new Color(0.3f, 0.45f, 0.24f));
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.28f, 0), new Vector3(17.4f, 0.3f, 17.4f), new Color(0.95f, 0.78f, 0.84f)); // 花びらのじゅうたん
            var rng = new System.Random(3);
            var pinks = new[] { new Color(1f, 0.78f, 0.86f), new Color(1f, 0.7f, 0.8f), new Color(1f, 0.86f, 0.9f) };
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2 + (float)rng.NextDouble() * 0.15f, r = 12.5f + (float)rng.NextDouble() * 9f;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                float h = 3.2f + (float)rng.NextDouble() * 2f;
                Prim(PrimitiveType.Cylinder, _arena, p + Vector3.up * h * 0.5f, new Vector3(0.45f, h * 0.5f, 0.45f), new Color(0.28f, 0.18f, 0.16f));
                for (int k = 0; k < 6; k++)
                {
                    var o = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() - 0.5f) * 4f;
                    Prim(PrimitiveType.Sphere, _arena, p + Vector3.up * (h + 0.6f) + o, Vector3.one * (2.2f + (float)rng.NextDouble() * 1.4f), pinks[k % 3]);
                }
            }
            // 千年桜（大きな1本）
            var big = new Vector3(0, 0, 24f);
            Prim(PrimitiveType.Cylinder, _arena, big + Vector3.up * 4f, new Vector3(1.6f, 4f, 1.6f), new Color(0.26f, 0.16f, 0.14f));
            for (int k = 0; k < 14; k++)
            {
                var o = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() - 0.5f) * 11f;
                Prim(PrimitiveType.Sphere, _arena, big + Vector3.up * 9.5f + o, Vector3.one * (4.5f + (float)rng.NextDouble() * 3f), pinks[k % 3], k % 4 == 0);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60 + 30) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 10.2f;
                Prim(PrimitiveType.Cylinder, _arena, p + Vector3.up * 1.2f, new Vector3(0.08f, 1.2f, 0.08f), Black);
                Ball(_arena, p + Vector3.up * 2.5f, 0.45f, new Color(1f, 0.6f, 0.7f));
            }
            _driftKind = 1;
            for (int i = 0; i < 160; i++)
            {
                var g = Prim(PrimitiveType.Cube, _arena, RandAround(18f, 0.5f, 12f), new Vector3(0.09f, 0.015f, 0.07f), pinks[i % 3]);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _drift.Add(new Drift { tr = g.transform, v = new Vector3(0.6f, -0.7f, 0.25f), phase = Random.value * 10f });
            }
        }

        /// <summary>夜の竹林：苔の床、まわりに竹、灯籠、蛍。</summary>
        void StageBamboo()
        {
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.3f, 0), new Vector3(21f, 0.3f, 21f), new Color(0.2f, 0.27f, 0.18f));
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.4f, 0), new Vector3(400f, 0.1f, 400f), new Color(0.14f, 0.2f, 0.13f));
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.28f, 0), new Vector3(17.4f, 0.3f, 17.4f), new Color(0.33f, 0.3f, 0.24f)); // 土
            var rng = new System.Random(8);
            var greens = new[] { new Color(0.35f, 0.58f, 0.32f), new Color(0.28f, 0.5f, 0.3f), new Color(0.42f, 0.62f, 0.36f) };
            for (int i = 0; i < 260; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 11f + (float)rng.NextDouble() * 16f;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                float h = 9f + (float)rng.NextDouble() * 9f, w = 0.18f + (float)rng.NextDouble() * 0.12f;
                Prim(PrimitiveType.Cylinder, _arena, p + Vector3.up * h * 0.5f, new Vector3(w, h * 0.5f, w), greens[i % 3], false, new Vector3(((float)rng.NextDouble() - 0.5f) * 8f, 0, ((float)rng.NextDouble() - 0.5f) * 8f));
                if (i % 3 == 0) Prim(PrimitiveType.Sphere, _arena, p + Vector3.up * (h + 0.5f), new Vector3(2.4f, 1.2f, 2.4f), greens[(i + 1) % 3]);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 10.2f;
                Prim(PrimitiveType.Cube, _arena, p + Vector3.up * 0.4f, new Vector3(0.45f, 0.8f, 0.45f), new Color(0.45f, 0.45f, 0.47f));
                Prim(PrimitiveType.Cube, _arena, p + Vector3.up * 1.05f, new Vector3(0.65f, 0.5f, 0.65f), new Color(0.5f, 0.5f, 0.52f));
                Ball(_arena, p + Vector3.up * 1.05f, 0.4f, Gold);
            }
            _driftKind = 2;
            for (int i = 0; i < 70; i++)
            {
                var g = Prim(PrimitiveType.Sphere, _arena, RandAround(16f, 0.4f, 5f), Vector3.one * 0.07f, new Color(0.75f, 1f, 0.45f), true);
                _drift.Add(new Drift { tr = g.transform, v = Vector3.zero, phase = Random.value * 10f });
            }
        }

        static Vector3 RandAround(float r, float yMin, float yMax)
        {
            var c = Random.insideUnitCircle * r;
            return new Vector3(c.x, Random.Range(yMin, yMax), c.y);
        }

        /// <summary>花びらと蛍を動かす（Fx から毎フレーム）。</summary>
        void StageFx(float udt)
        {
            if (_driftKind == 0) return;
            float tm = Time.time;
            foreach (var d in _drift)
            {
                if (d.tr == null) continue;
                var p = d.tr.localPosition;
                if (_driftKind == 1)
                {
                    p += (d.v + new Vector3(Mathf.Sin(tm * 1.3f + d.phase) * 0.5f, 0, Mathf.Cos(tm * 1.1f + d.phase) * 0.4f)) * udt;
                    d.tr.localRotation = Quaternion.Euler(tm * 90f + d.phase * 40f, d.phase * 50f, tm * 70f);
                    if (p.y < 0.02f) p = RandAround(18f, 9f, 12f) - new Vector3(4f, 0, 2f);
                }
                else
                {
                    p += new Vector3(Mathf.Sin(tm * 0.7f + d.phase), Mathf.Sin(tm * 1.1f + d.phase * 2f) * 0.5f, Mathf.Cos(tm * 0.6f + d.phase)) * (0.4f * udt);
                    d.tr.localScale = Vector3.one * (0.05f + 0.04f * Mathf.Max(0, Mathf.Sin(tm * 2.2f + d.phase * 3f)));
                }
                d.tr.localPosition = p;
            }
        }
    }
}
