using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 森の舞台の作りこみ：
    ///  まとめ描き（たくさんの木を、色ごとに1つの形にまとめて描く。当たり判定は幹だけ）で木を約800本、
    ///  北の端から南の端までくねって流れる川（道と交わる所に木の橋、岸に岩）、小さい山・こんもりした丘、
    ///  家ほどの大岩、茅葺きの小屋。どれも武者の道すじからはなして置く。
    /// </summary>
    public partial class Sengoku
    {
        // ───────── まとめ描き ─────────

        readonly Dictionary<Color, List<CombineInstance>> _batch = new Dictionary<Color, List<CombineInstance>>();
        static readonly Dictionary<PrimitiveType, Mesh> _primMesh = new Dictionary<PrimitiveType, Mesh>();

        static Mesh PrimMesh(PrimitiveType t)
        {
            if (_primMesh.TryGetValue(t, out var m) && m != null) return m;
            var g = GameObject.CreatePrimitive(t);
            m = g.GetComponent<MeshFilter>().sharedMesh;
            Destroy(g);
            _primMesh[t] = m;
            return m;
        }

        void BAdd(Mesh mesh, Vector3 pos, Vector3 scale, Quaternion rot, Color c)
        {
            if (!_batch.TryGetValue(c, out var list)) { list = new List<CombineInstance>(); _batch[c] = list; }
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) });
        }

        /// <summary>まとめた形を、色ごとに1つの見た目にする（影は落とさない＝軽く）。</summary>
        void BFlush()
        {
            foreach (var kv in _batch)
            {
                var list = kv.Value;
                for (int start = 0; start < list.Count; start += 1500)
                {
                    var part = list.GetRange(start, Mathf.Min(1500, list.Count - start));
                    var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "まとめ描き" };
                    mesh.CombineMeshes(part.ToArray(), true, true);
                    var go = new GameObject("まとめ描き");
                    go.transform.SetParent(_map, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Mat(kv.Key);
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            _batch.Clear();
        }

        /// <summary>幹の当たり判定（見た目はまとめ描き。立てた円柱＝カプセルで十分）。</summary>
        void TrunkCollider(Vector3 pos, Vector3 scale)
        {
            var g = new GameObject("幹"); g.transform.SetParent(_map, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            g.AddComponent<CapsuleCollider>();
        }

        void BCedar(Vector3 at, float h)
        {
            var trunk = new Vector3(0.3f, h * 0.5f, 0.3f);
            BAdd(PrimMesh(PrimitiveType.Cylinder), at + Vector3.up * h * 0.5f, trunk, Quaternion.identity, Bark);
            TrunkCollider(at + Vector3.up * h * 0.5f, trunk);
            var greens = new[] { new Color(0.16f, 0.32f, 0.2f), new Color(0.13f, 0.28f, 0.18f) };
            for (int k = 0; k < 3; k++)
            {
                float w = 1.6f - k * 0.4f;
                BAdd(Doll.Cone, at + Vector3.up * (h * 0.45f + k * h * 0.17f), new Vector3(w, h * 0.32f, w), Quaternion.identity, greens[k % 2]);
            }
        }

        void BRound(Vector3 at, float h, System.Random rng)
        {
            var trunk = new Vector3(0.32f, h * 0.4f, 0.32f);
            BAdd(PrimMesh(PrimitiveType.Cylinder), at + Vector3.up * h * 0.4f, trunk, Quaternion.identity, Bark);
            TrunkCollider(at + Vector3.up * h * 0.4f, trunk);
            int n = 2 + rng.Next(2);
            for (int k = 0; k < n; k++)
            {
                var off = new Vector3((float)(rng.NextDouble() - 0.5) * 1.6f, h * (0.75f + k * 0.15f), (float)(rng.NextDouble() - 0.5) * 1.6f);
                BAdd(PrimMesh(PrimitiveType.Sphere), at + off, new Vector3(2.2f, 3.4f, 2.2f) * (1f - k * 0.15f), Quaternion.identity, k % 2 == 0 ? Leaf : Leaf2);
            }
        }

        /// <summary>茂み（低い、通りぬけられる）。</summary>
        void BBush(Vector3 at, System.Random rng)
        {
            float s = 0.8f + (float)rng.NextDouble() * 0.8f;
            BAdd(PrimMesh(PrimitiveType.Sphere), at + Vector3.up * 0.4f * s, new Vector3(1.4f, 1.1f, 1.4f) * s, Quaternion.identity, rng.NextDouble() < 0.5 ? Leaf : Leaf2);
        }

        // ───────── 川（北の端から南の端まで、くねって流れる）─────────

        static readonly Vector2[] River =
        {
            new(-5, 60), new(-12, 52), new(-14, 44), new(-12, 36), new(-6, 28), new(3, 20), new(7, 11), new(4, 2),
            new(-4, -6), new(-7, -15), new(-2, -24), new(9, -30), new(14, -38), new(13, -48), new(7, -54), new(4, 60 * -1f),
        };
        const float RiverW = 4.2f;

        static float RiverDist(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < River.Length - 1; i++) best = Mathf.Min(best, DistToSeg(p, River[i], River[i + 1]));
            return best;
        }

        static bool SegCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 hit)
        {
            hit = default;
            var r = b - a; var s = d - c;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-5f) return false;
            float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / den, u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / den;
            if (t < 0 || t > 1 || u < 0 || u > 1) return false;
            hit = a + r * t; return true;
        }

        void BuildRiver(System.Random rng)
        {
            var water = new Color(0.22f, 0.5f, 0.68f);
            for (int i = 0; i < River.Length - 1; i++)
            {
                var a = River[i]; var b = River[i + 1]; var d = b - a; var mid = (a + b) * 0.5f;
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                float y = 0.07f;
                Block(PrimitiveType.Cube, _map, new Vector3(mid.x, y, mid.y), new Vector3(RiverW, 0.04f, d.magnitude), water, false, new Vector3(0, yaw, 0));
                Block(PrimitiveType.Cylinder, _map, new Vector3(b.x, y, b.y), new Vector3(RiverW, 0.02f, RiverW), water, false);
                // 岸の岩と葦
                for (int k = 0; k < 3; k++)
                {
                    float t = (k + 0.5f) / 3f;
                    var side = new Vector2(d.y, -d.x).normalized * (RiverW * 0.55f) * (k % 2 == 0 ? 1 : -1);
                    var p = a + d * t + side;
                    if (rng.NextDouble() < 0.6) BAdd(PrimMesh(PrimitiveType.Sphere), new Vector3(p.x, 0.25f, p.y), new Vector3(0.9f, 0.7f, 0.8f), Quaternion.identity, Rock);
                    else BAdd(PrimMesh(PrimitiveType.Cylinder), new Vector3(p.x, 0.8f, p.y), new Vector3(0.06f, 0.8f, 0.06f), Quaternion.identity, new Color(0.55f, 0.6f, 0.3f));
                }
            }
            // 道と交わる所に木の橋（すこし高い板と欄干。通りぬけはできる）
            var paths = new List<(Vector2, Vector2)>();
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (var (a, b) in FEdges) { var pa = FN(a, s); var pb = FN(b, s); if (pa.y < 0.5f && pb.y < 0.5f) paths.Add((new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z))); }
                foreach (var (a, b) in FLinks) { var pa = FN(a, s); var pb = FS(b); if (pa.y < 0.5f && pb.y < 0.5f) paths.Add((new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z))); }
            }
            foreach (var (pa, pb) in paths)
                for (int i = 0; i < River.Length - 1; i++)
                    if (SegCross(pa, pb, River[i], River[i + 1], out var hit))
                    {
                        var d = pb - pa; float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                        Block(PrimitiveType.Cube, _map, new Vector3(hit.x, 0.16f, hit.y), new Vector3(2.6f, 0.12f, RiverW + 1.6f), Timber, false, new Vector3(0, yaw, 0));
                        var q = Quaternion.Euler(0, yaw, 0);
                        foreach (float sd in new[] { -1.2f, 1.2f })
                        {
                            var rp = new Vector3(hit.x, 0.6f, hit.y) + q * new Vector3(sd, 0, 0);
                            Block(PrimitiveType.Cube, _map, rp, new Vector3(0.1f, 0.06f, RiverW + 1.4f), Red, false, new Vector3(0, yaw, 0));
                        }
                    }
        }

        // ───────── 小さい山・大岩・小屋 ─────────

        readonly List<(Vector2 c, float r)> _spots = new List<(Vector2, float)>();
        bool SpotFree(Vector2 p, float r) { foreach (var (c, rr) in _spots) if (Vector2.Distance(p, c) < rr + r) return false; return true; }

        bool FFree2(Vector2 p, float clear) => FFree(p, clear) && RiverDist(p) > RiverW * 0.5f + clear * 0.6f;

        /// <summary>こんもりした丘（上を歩ける）。</summary>
        void Mound(Vector3 at, float r, float h)
        {
            Block(PrimitiveType.Sphere, _map, at + Vector3.down * h * 0.35f, new Vector3(r * 2f, h * 2.6f, r * 2f), new Color(0.24f, 0.42f, 0.2f));
        }

        /// <summary>小さい山（岩の山肌に、こんもりした頂上と木）。</summary>
        void SmallMountain(Vector3 at, float r, System.Random rng)
        {
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * 0.5f, new Vector3(r * 2f, r * 1.9f, r * 2f), Rock);
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * (r * 0.75f), new Vector3(r * 1.3f, r * 0.9f, r * 1.3f), new Color(0.25f, 0.42f, 0.21f));
            for (int k = 0; k < 3; k++)
                BCedar(at + new Vector3((float)(rng.NextDouble() - 0.5) * r * 0.6f, r * 1.05f, (float)(rng.NextDouble() - 0.5) * r * 0.6f), 6f + (float)rng.NextDouble() * 4f);
        }

        /// <summary>家ほどの大岩（苔のせ）。</summary>
        void GiantRock(Vector3 at, float s, System.Random rng)
        {
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * s * 0.45f, new Vector3(s * 1.1f, s * 1.5f, s), Rock);
            Block(PrimitiveType.Sphere, _map, at + new Vector3(s * 0.4f, s * 0.25f, s * 0.2f), new Vector3(s * 0.6f, s * 0.8f, s * 0.55f), new Color(0.36f, 0.35f, 0.32f));
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * s * 1.15f, new Vector3(s * 0.8f, s * 0.35f, s * 0.7f), Moss, false);
        }

        /// <summary>茅葺きの小屋（壁は当たり判定あり）。</summary>
        void Hut(Vector3 at, float yaw, System.Random rng)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var wood = new Color(0.45f, 0.33f, 0.22f); var thatch = new Color(0.55f, 0.45f, 0.26f);
            Block(PrimitiveType.Cube, _map, at + Vector3.up * 1.3f, new Vector3(2.4f, 2.6f, 2f), wood, true, new Vector3(0, yaw, 0));
            Block(PrimitiveType.Cube, _map, at + q * new Vector3(0, 0.95f, 1.01f), new Vector3(0.7f, 1.6f, 0.04f), new Color(0.12f, 0.09f, 0.07f), false, new Vector3(0, yaw, 0));
            // 屋根：茅を重ねた段（横ずれしない形で）
            for (int k = 0; k < 4; k++)
                Block(PrimitiveType.Cube, _map, at + Vector3.up * (2.75f + k * 0.42f), new Vector3(3.2f - k * 0.75f, 0.42f, 2.8f - k * 0.6f), k % 2 == 0 ? thatch : thatch * 0.9f, true, new Vector3(0, yaw, 0));
            Block(PrimitiveType.Cube, _map, at + Vector3.up * 4.5f, new Vector3(0.3f, 0.25f, 0.9f), new Color(0.3f, 0.24f, 0.15f), false, new Vector3(0, yaw, 0));
            // 薪の山と、灯り
            var wp = at + q * new Vector3(1.7f, 0.35f, 0.4f);
            for (int k = 0; k < 3; k++) BAdd(PrimMesh(PrimitiveType.Cylinder), wp + Vector3.up * k * 0.2f, new Vector3(0.12f, 0.5f, 0.12f), q * Quaternion.Euler(0, 0, 90), Bark);
            Block(PrimitiveType.Sphere, _map, at + q * new Vector3(-0.8f, 1.9f, 1.15f), Vector3.one * 0.25f, new Color(1f, 0.75f, 0.4f), false, null, true);
        }

        void ForestDetails()
        {
            var rng = new System.Random(1717);
            BuildRiver(rng);
            Vector3 On(Vector2 p) => new Vector3(p.x, FHeightAt(p.x, p.y), p.y);
            // 小さい山（谷底と段の上）
            for (int k = 0, t = 0; k < 9 && t < 3000; t++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 78f, (float)(rng.NextDouble() * 2 - 1) * 52f);
                float r = 3.5f + (float)rng.NextDouble() * 3f;
                if (!FFree2(p, r + 2f) || !SpotFree(p, r + 2f)) continue;
                var c = On(p);
                if (Mathf.Abs(FHeightAt(p.x + r, p.y) - c.y) > 0.1f || Mathf.Abs(FHeightAt(p.x - r, p.y) - c.y) > 0.1f || Mathf.Abs(FHeightAt(p.x, p.y + r) - c.y) > 0.1f || Mathf.Abs(FHeightAt(p.x, p.y - r) - c.y) > 0.1f) continue;
                SmallMountain(c, r, rng); _spots.Add((p, r)); k++;
            }
            // こんもりした丘（上を歩ける）
            for (int k = 0, t = 0; k < 26 && t < 3000; t++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 80f, (float)(rng.NextDouble() * 2 - 1) * 54f);
                float r = 2.5f + (float)rng.NextDouble() * 3f;
                if (!FFree2(p, r + 1.5f) || !SpotFree(p, r + 1f)) continue;
                var c = On(p);
                if (Mathf.Abs(FHeightAt(p.x + r, p.y) - c.y) > 0.1f || Mathf.Abs(FHeightAt(p.x - r, p.y) - c.y) > 0.1f) continue;
                Mound(c, r, 1.5f + (float)rng.NextDouble() * 1.5f); _spots.Add((p, r)); k++;
            }
            // 家ほどの大岩
            for (int k = 0, t = 0; k < 14 && t < 3000; t++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 80f, (float)(rng.NextDouble() * 2 - 1) * 54f);
                float s = 2.8f + (float)rng.NextDouble() * 2.6f;
                if (!FFree2(p, s + 1.5f) || !SpotFree(p, s)) continue;
                GiantRock(On(p), s, rng); _spots.Add((p, s)); k++;
            }
            // 小屋（道のそばの少し奥）
            for (int k = 0, t = 0; k < 12 && t < 4000; t++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 80f, (float)(rng.NextDouble() * 2 - 1) * 54f);
                if (!FFree2(p, 3.5f) || FFree2(p, 7f) || !SpotFree(p, 3f)) continue; // 道から 3.5〜7 の所（道ぞい）
                var c = On(p);
                if (Mathf.Abs(FHeightAt(p.x + 2, p.y) - c.y) > 0.1f || Mathf.Abs(FHeightAt(p.x - 2, p.y) - c.y) > 0.1f) continue;
                Hut(c, (float)rng.NextDouble() * 360f, rng); _spots.Add((p, 3f)); k++;
            }
        }

        /// <summary>森の木と茂み（まとめ描き）。道・川・山・岩・小屋をよける。</summary>
        void ForestTrees()
        {
            var rng = new System.Random(9091);
            int placed = 0;
            for (int t = 0; t < 14000 && placed < 820; t++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 82f, (float)(rng.NextDouble() * 2 - 1) * 56f);
                if (!FFree2(p, 2.6f) || !SpotFree(p, 0.8f)) continue;
                var at = new Vector3(p.x, FHeightAt(p.x, p.y), p.y);
                if (rng.NextDouble() < 0.55) BCedar(at, 9f + (float)rng.NextDouble() * 11f);
                else BRound(at, 5.5f + (float)rng.NextDouble() * 5f, rng);
                placed++;
            }
            for (int k = 0, t = 0; k < 300 && t < 6000; t++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * 82f, (float)(rng.NextDouble() * 2 - 1) * 56f);
                if (!FFree2(p, 1.8f)) continue;
                BBush(new Vector3(p.x, FHeightAt(p.x, p.y), p.y), rng); k++;
            }
        }
    }
}
