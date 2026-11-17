using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU のマップ（東西 170m・南北 120m）。どれも当たり判定つきの基本の形。
    ///  東西の端に天守閣（x=±66）、南北に櫓（z=±40）、まん中の丘に五重塔（ワイヤーの引っかけ場所）、
    ///  x=±24 に南北に流れる川（橋は z=±14）、四隅に崖の台地、空に浮かぶ島（ジェット・ワイヤー・矢で行ける）。
    /// </summary>
    public partial class Sengoku
    {
        public const float HalfX = 84f, HalfZ = 58f;
        public static readonly Vector3 WestCastle = new(-66, 0, 0), EastCastle = new(66, 0, 0), NorthTower = new(0, 0, 40), SouthTower = new(0, 0, -40);

        readonly Dictionary<(Color, bool), Material> _mats = new Dictionary<(Color, bool), Material>();
        Transform _map;

        static readonly Color Grass = new(0.33f, 0.48f, 0.27f), Dirt = new(0.45f, 0.36f, 0.26f), Stone = new(0.55f, 0.54f, 0.52f), Wall = new(0.93f, 0.92f, 0.88f),
            Roof = new(0.16f, 0.2f, 0.26f), Wood = new(0.36f, 0.23f, 0.15f), Red = new(0.78f, 0.16f, 0.12f), GoldC = new(1f, 0.8f, 0.35f), Water = new(0.15f, 0.42f, 0.6f);

        Material Mat(Color c, bool glow = false)
        {
            if (_mats.TryGetValue((c, glow), out var m)) return m;
            var w = GameSession.Instance != null ? GameSession.Instance.player.GetComponent<Wardrobe>() : null;
            var tmpl = glow && w != null && w.glowTemplate != null ? w.glowTemplate : w != null ? w.litTemplate : null;
            m = tmpl != null ? new Material(tmpl) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (glow && w != null && w.glowTemplate != null) m.SetColor("_Color", c);
            else { m.SetColor("_BaseColor", c); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null); }
            _mats[(c, glow)] = m;
            return m;
        }

        /// <summary>形を置く。solid＝ぶつかる（コライダーを残す）。</summary>
        GameObject Block(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, bool solid = true, Vector3? euler = null, bool glow = false)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!solid) Destroy(g.GetComponent<Collider>());
            else if (t == PrimitiveType.Cylinder)
            {
                // 円柱の当たり判定は丸いカプセルになってしまうので、形どおりにする
                DestroyImmediate(g.GetComponent<Collider>());
                g.AddComponent<MeshCollider>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
            }
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Mat(c, glow);
            if (glow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        GameObject Box(Vector3 center, Vector3 size, Color c, bool solid = true, float yaw = 0) => Block(PrimitiveType.Cube, _map, center, size, c, solid, new Vector3(0, yaw, 0));

        /// <summary>坂（from の高さ h0 から to の高さ h1 へ、幅 w）。</summary>
        void Ramp(Vector3 from, Vector3 to, float w, Color c)
        {
            var d = to - from;
            float len = d.magnitude;
            var g = Block(PrimitiveType.Cube, _map, (from + to) * 0.5f - Vector3.up * 0.25f, new Vector3(w, 0.5f, len), c);
            g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            // 高い坂は、下を段々の箱で埋める（下にもぐりこんで引っかからないように）
            float rise = Mathf.Abs(to.y - from.y);
            if (rise < 1.5f) return;
            float low = Mathf.Min(from.y, to.y);
            var flat = new Vector3(d.x, 0, d.z);
            var yaw = Quaternion.LookRotation(flat.normalized, Vector3.up).eulerAngles.y;
            const int n = 6;
            for (int k = 0; k < n; k++)
            {
                float t0 = k / (float)n, t1 = (k + 1) / (float)n;
                float top = Mathf.Min(from.y + d.y * t0, from.y + d.y * t1) - 0.4f;
                if (top - low < 0.3f) continue;
                var mid = from + d * ((t0 + t1) * 0.5f);
                Block(PrimitiveType.Cube, _map, new Vector3(mid.x, (low + top) * 0.5f, mid.z), new Vector3(w * 0.96f, top - low, flat.magnitude / n), c * 0.8f, true, new Vector3(0, yaw, 0));
            }
        }

        void BuildMap()
        {
            _map = new GameObject("戦場").transform;
            _map.SetParent(transform, false);

            // 地面（川のところは低い）
            Box(new Vector3(-56, -0.5f, 0), new Vector3(56, 1, HalfZ * 2), Grass);
            Box(new Vector3(56, -0.5f, 0), new Vector3(56, 1, HalfZ * 2), Grass);
            Box(new Vector3(0, -0.5f, 0), new Vector3(40, 1, HalfZ * 2), Grass);
            foreach (float sx in new[] { -1f, 1f })
            {
                Box(new Vector3(sx * 24, -3f, 0), new Vector3(8, 1, HalfZ * 2), Dirt);                       // 川底
                Block(PrimitiveType.Cube, _map, new Vector3(sx * 24, -1.3f, 0), new Vector3(8, 0.1f, HalfZ * 2), Water, false); // 水面（通りぬける）
                foreach (float sz in new[] { -1f, 1f })
                {
                    // 橋（朱塗りの欄干）
                    var bz = sz * 14f;
                    Box(new Vector3(sx * 24, 0.25f, bz), new Vector3(10, 0.5f, 4.5f), Wood);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Box(new Vector3(sx * 24, 1.0f, bz + side * 2.1f), new Vector3(10, 0.15f, 0.15f), Red);
                        for (int i = -2; i <= 2; i++) Box(new Vector3(sx * 24 + i * 2.4f, 0.6f, bz + side * 2.1f), new Vector3(0.2f, 0.9f, 0.2f), Red);
                    }
                    // 川の端の坂（川から上がれる）
                    Ramp(new Vector3(sx * 24, -2.5f, sz * 44f), new Vector3(sx * 24, 0.05f, sz * 54f), 7.5f, Dirt);
                }
            }

            Castle(WestCastle, 1);
            Castle(EastCastle, -1);
            Tower(NorthTower, -1);
            Tower(SouthTower, 1);

            // まん中の丘と五重塔
            Block(PrimitiveType.Cylinder, _map, new Vector3(0, 1f, 0), new Vector3(22, 1f, 22), Stone);
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                Ramp(d * 17f, d * 10.4f + Vector3.up * 2f, 5f, Stone);
            Pagoda(new Vector3(0, 2f, 0));

            // 四隅の崖の台地
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var c = new Vector3(sx * 42, 0, sz * 30);
                    Box(c + Vector3.up * 3.5f, new Vector3(16, 7, 14), new Color(0.48f, 0.42f, 0.36f));
                    Box(c + Vector3.up * 7.05f, new Vector3(16, 0.1f, 14), Grass);
                    Ramp(c + new Vector3(-sx * 8f, 7f, -sz * 2f), c + new Vector3(-sx * 20f, 0.05f, -sz * 2f), 4.5f, Dirt);
                    // 小さな社
                    Box(c + new Vector3(sx * 3, 8.2f, sz * 2), new Vector3(3, 2.2f, 3), Wood);
                    Box(c + new Vector3(sx * 3, 9.6f, sz * 2), new Vector3(4.2f, 0.4f, 4.2f), Roof);
                }

            // 空に浮かぶ島（上に小さな鳥居）
            var isles = new[] { new Vector3(-13, 15, 14), new Vector3(13, 17, -14), new Vector3(-40, 14, 0), new Vector3(40, 14, 0), new Vector3(0, 20, 26), new Vector3(0, 20, -26) };
            foreach (var p in isles)
            {
                Box(p, new Vector3(8, 1.4f, 8), Stone);
                Box(p + Vector3.up * 0.72f, new Vector3(8, 0.06f, 8), Grass);
                foreach (float sx in new[] { -1f, 1f }) Box(p + new Vector3(sx * 1.6f, 2.2f, 0), new Vector3(0.3f, 3f, 0.3f), Red);
                Box(p + new Vector3(0, 3.8f, 0), new Vector3(4.4f, 0.3f, 0.45f), Red);
                Block(PrimitiveType.Sphere, _map, p + Vector3.down * 1.4f, new Vector3(7, 3, 7), new Color(0.42f, 0.38f, 0.34f), false);
                Block(PrimitiveType.Sphere, _map, p + Vector3.up * 1.6f, Vector3.one * 0.5f, new Color(0.3f, 1f, 1f), false, null, true);
            }

            // 岩と竹やぶ（かくれる場所）
            var rng = new System.Random(21);
            for (int i = 0; i < 46; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * 74f, z = (float)(rng.NextDouble() * 2 - 1) * 50f;
                if (Mathf.Abs(Mathf.Abs(x) - 24) < 6 || new Vector2(x, z).magnitude < 13 || Mathf.Abs(x) > 52 && Mathf.Abs(z) < 14) continue;
                if (Mathf.Abs(x) < 6 && Mathf.Abs(Mathf.Abs(z) - 40) < 8) continue;
                if (i % 3 == 0)
                    for (int k = 0; k < 7; k++)
                    {
                        float h = 6f + (float)rng.NextDouble() * 5f;
                        Block(PrimitiveType.Cylinder, _map, new Vector3(x + (float)(rng.NextDouble() - 0.5) * 3f, h * 0.5f, z + (float)(rng.NextDouble() - 0.5) * 3f), new Vector3(0.3f, h * 0.5f, 0.3f), new Color(0.38f, 0.6f, 0.34f));
                    }
                else
                    Block(PrimitiveType.Sphere, _map, new Vector3(x, 0.6f, z), new Vector3(2.5f + (float)rng.NextDouble() * 2.5f, 1.8f + (float)rng.NextDouble() * 1.6f, 2.5f + (float)rng.NextDouble() * 2.5f), new Color(0.5f, 0.5f, 0.5f));
            }

            // まわりの山（見た目）と、見えない壁
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a) * 150f, 0, Mathf.Sin(a) * 120f);
                Block(PrimitiveType.Sphere, _map, p, new Vector3(70, 40 + (i % 4) * 12, 60), new Color(0.3f, 0.38f, 0.32f), false);
            }
            foreach (var (c, s) in new[] { (new Vector3(HalfX + 1, 30, 0), new Vector3(2, 80, HalfZ * 2 + 4)), (new Vector3(-HalfX - 1, 30, 0), new Vector3(2, 80, HalfZ * 2 + 4)),
                                           (new Vector3(0, 30, HalfZ + 1), new Vector3(HalfX * 2 + 4, 80, 2)), (new Vector3(0, 30, -HalfZ - 1), new Vector3(HalfX * 2 + 4, 80, 2)) })
            {
                var g = Box(c, s, Color.white);
                g.GetComponent<Renderer>().enabled = false;
            }
            Block(PrimitiveType.Cube, _map, new Vector3(0, -6f, 0), new Vector3(600, 0.2f, 600), new Color(0.2f, 0.3f, 0.24f), false);

            var sun = new GameObject("戦場の灯り").AddComponent<Light>();
            sun.transform.SetParent(_map, false); sun.transform.localPosition = new Vector3(0, 30, 0);
            sun.type = LightType.Point; sun.range = 90; sun.intensity = 1.2f; sun.color = new Color(1f, 0.92f, 0.8f);
        }

        /// <summary>天守閣（石垣の台・白壁の3層・黒い屋根）。face は広場（中央）の向き（+1 は東向き）。</summary>
        void Castle(Vector3 at, float face)
        {
            Box(at + Vector3.up * 2f, new Vector3(18, 4, 18), Stone);
            Ramp(at + new Vector3(face * 9f, 4f, 0), at + new Vector3(face * 19f, 0.05f, 0), 6f, Stone);
            float y = 4;
            float[] w = { 12, 9, 6 }, h = { 5, 4, 3.5f };
            for (int i = 0; i < 3; i++)
            {
                Box(at + Vector3.up * (y + h[i] * 0.5f), new Vector3(w[i], h[i], w[i]), Wall);
                Box(at + Vector3.up * (y + h[i] + 0.25f), new Vector3(w[i] + 3.2f, 0.5f, w[i] + 3.2f), Roof);
                y += h[i] + 0.5f;
            }
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * (y + 0.6f), new Vector3(1.4f, 1.2f, 1.4f), GoldC, false, null, true);
            // のぼり（東は青緑・西は朱）
            var col = face > 0 ? Red : new Color(0.1f, 0.6f, 0.62f);
            foreach (float sz in new[] { -1f, 1f })
            {
                Box(at + new Vector3(face * 8f, 7f, sz * 8f), new Vector3(0.2f, 6f, 0.2f), Wood, false);
                Box(at + new Vector3(face * 8f, 8f, sz * 8f + 0.6f), new Vector3(0.05f, 3.5f, 1f), col, false);
            }
        }

        /// <summary>櫓（4本の脚・物見台・屋根）。坂で登れる。side は坂を下ろす向き（z）。</summary>
        void Tower(Vector3 at, float side)
        {
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Box(at + new Vector3(sx * 3f, 4f, sz * 3f), new Vector3(0.7f, 8, 0.7f), Wood);
            Box(at + Vector3.up * 8.3f, new Vector3(8, 0.6f, 8), Wood);
            foreach (float sx in new[] { -1f, 1f }) { Box(at + new Vector3(sx * 3.9f, 9.1f, 0), new Vector3(0.2f, 1f, 8), Red); }
            Box(at + new Vector3(0, 9.1f, -side * 3.9f), new Vector3(8, 1f, 0.2f), Red);
            foreach (float sx in new[] { -1f, 1f }) Box(at + new Vector3(sx * 3.3f, 10.5f, -side * 3.3f), new Vector3(0.3f, 3.6f, 0.3f), Wood);
            Box(at + Vector3.up * 12.4f, new Vector3(9.5f, 0.6f, 9.5f), Roof);
            Ramp(at + new Vector3(0, 8.6f, side * 4f), at + new Vector3(0, 0.05f, side * 19f), 3.5f, Wood);
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * 13.4f, Vector3.one * 0.9f, new Color(0.3f, 1f, 1f), false, null, true);
        }

        void Pagoda(Vector3 at)
        {
            float y = 0;
            for (int i = 0; i < 5; i++)
            {
                float w = 8f - i * 1.1f;
                Box(at + Vector3.up * (y + 1.6f), new Vector3(w, 3.2f, w), i % 2 == 0 ? Red : Wood);
                Box(at + Vector3.up * (y + 3.45f), new Vector3(w + 3.5f, 0.5f, w + 3.5f), Roof);
                y += 3.7f;
            }
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * (y + 3f), new Vector3(0.35f, 3f, 0.35f), GoldC, true);
            for (int i = 0; i < 5; i++) Block(PrimitiveType.Cylinder, _map, at + Vector3.up * (y + 1.2f + i * 0.9f), new Vector3(1.1f - i * 0.12f, 0.08f, 1.1f - i * 0.12f), GoldC, false, null, true);
        }

        // ───────── 練習場のわら人形 ─────────

        void Dummy(Vector3 at, float hp = 300)
        {
            var root = new GameObject("わら人形");
            root.transform.SetParent(_map, false);
            root.transform.position = at;
            var col = root.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 1f, 0); col.height = 2f; col.radius = 0.45f;
            var straw = new Color(0.86f, 0.74f, 0.42f);
            Block(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.5f, 0), new Vector3(0.12f, 0.5f, 0.12f), Wood, false);
            Block(PrimitiveType.Capsule, root.transform, new Vector3(0, 1.25f, 0), new Vector3(0.7f, 0.5f, 0.5f), straw, false);
            Block(PrimitiveType.Cube, root.transform, new Vector3(0, 1.4f, 0), new Vector3(1.6f, 0.14f, 0.14f), straw, false);
            Block(PrimitiveType.Sphere, root.transform, new Vector3(0, 1.95f, 0), Vector3.one * 0.5f, straw, false);
            Block(PrimitiveType.Cylinder, root.transform, new Vector3(0, 1.32f, 0.26f), new Vector3(0.35f, 0.02f, 0.35f), Red, false, new Vector3(90, 0, 0));
            var t = root.AddComponent<SengokuTarget>();
            t.maxHp = t.hp = hp; t.team = 0; t.respawn = 3f;
        }

        void PlaceDummies()
        {
            var spots = new[]
            {
                new Vector3(-44, 0, 4), new Vector3(-44, 0, -4), new Vector3(-36, 0, 0), new Vector3(-30, 0, 9),
                new Vector3(-8, 2, 6), new Vector3(8, 2, -6), new Vector3(0, 8.6f, 40), new Vector3(0, 8.6f, -40),
                new Vector3(-42, 7.1f, 30), new Vector3(42, 7.1f, -30), new Vector3(-13, 15.7f, 14), new Vector3(13, 17.7f, -14),
                new Vector3(40, 14.7f, 0), new Vector3(24, 0.5f, 14), new Vector3(50, 0, 6), new Vector3(60, 4, 6),
            };
            foreach (var s in spots) Dummy(s);
        }
    }
}
