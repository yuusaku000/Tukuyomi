using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Tsukuyomi.EditorTools.Kit;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// Island.Height から地形のメッシュを作る。区画ごとに「上の面（土・砂・草）」と「崖（石垣）」に分ける。
    /// 川・水路・池の水面も作る。
    /// </summary>
    public static class TerrainBuilder
    {
        const float Cell = 0.8f;
        const int ChunkCells = 64;
        static readonly Rect Bounds = new Rect(-172, -16, 344, 330);

        public static Material TopSand => Lit("t_sand", new Color(0.84f, 0.8f, 0.7f), 0.08f);
        public static Material TopEarth => Lit("t_earth", new Color(0.74f, 0.68f, 0.56f), 0.08f);
        public static Material TopGrass => Lit("t_grass", new Color(0.36f, 0.52f, 0.28f), 0.06f);
        public static Material Bed => Lit("t_bed", new Color(0.22f, 0.27f, 0.26f), 0.2f);
        public static Material Cliff => Lit("t_cliff", new Color(0.55f, 0.53f, 0.5f), 0.12f);

        public static void Build(Transform parent)
        {
            var root = Group(parent, "地形", Vector3.zero);
            int nx = Mathf.CeilToInt(Bounds.width / Cell), nz = Mathf.CeilToInt(Bounds.height / Cell);
            // 高さを先に全部計算しておく
            var H = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                    H[i, j] = Island.Height(Bounds.xMin + i * Cell, Bounds.yMin + j * Cell);

            int count = 0;
            for (int ci = 0; ci < nx; ci += ChunkCells)
                for (int cj = 0; cj < nz; cj += ChunkCells)
                    if (Chunk(root, H, ci, cj, Mathf.Min(ci + ChunkCells, nx), Mathf.Min(cj + ChunkCells, nz))) count++;
            Debug.Log($"[Tsukuyomi] terrain chunks: {count}");

            Waters(parent);
        }

        static Vector3 V(float[,] H, int i, int j) => new Vector3(Bounds.xMin + i * Cell, H[i, j], Bounds.yMin + j * Cell);

        static int Kind(Vector3 c, Vector3 n)
        {
            if (n.y < 0.55f) return 4;                              // 崖
            if (!float.IsNaN(Island.WaterSurface(c.x, c.z)) && Island.Inside(c.x, c.z) > -1f) return 3; // 川底
            if (Vector2.Distance(new Vector2(c.x, c.z), Island.HillCenter) < Island.HillRadius + 2) return 2;
            if (Island.Inside(c.x, c.z) < 6f) return 0;             // 浜
            if (c.y > Island.LevelTop(2) - 0.5f) return 2;          // 上段・頂上は苔むした庭
            return c.y > Island.Ground + 1f ? 1 : 0;
        }

        static bool Chunk(Transform root, float[,] H, int i0, int j0, int i1, int j1)
        {
            // 上の面：頂点を共有、地面の種類ごとにサブメッシュ
            var tv = new List<Vector3>(); var tuv = new List<Vector2>();
            var tris = new List<int>[4] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var idx = new Dictionary<int, int>();
            // 崖：三角ごとに頂点を分けて、縦向きの UV
            var cv = new List<Vector3>(); var cuv = new List<Vector2>(); var ct = new List<int>();
            int W = j1 - j0 + 1;

            int Top(int i, int j)
            {
                int key = (i - i0) * W + (j - j0);
                if (idx.TryGetValue(key, out var k)) return k;
                var p = V(H, i, j);
                k = tv.Count; tv.Add(p); tuv.Add(new Vector2(p.x / 4f, p.z / 4f));
                idx[key] = k;
                return k;
            }

            void Tri(int ia, int ja, int ib, int jb, int ic, int jc)
            {
                var a = V(H, ia, ja); var b = V(H, ib, jb); var c = V(H, ic, jc);
                if (a.y < -3.3f && b.y < -3.3f && c.y < -3.3f) return; // 深い海の底は作らない
                var n = Vector3.Cross(b - a, c - a).normalized;
                var center = (a + b + c) / 3;
                int kind = Kind(center, n);
                if (kind == 4)
                {
                    bool useX = Mathf.Abs(n.z) > Mathf.Abs(n.x);
                    int k = cv.Count;
                    foreach (var p in new[] { a, b, c })
                    {
                        cv.Add(p);
                        cuv.Add(new Vector2((useX ? p.x : p.z) / 3f, p.y / 3f));
                    }
                    ct.Add(k); ct.Add(k + 1); ct.Add(k + 2);
                }
                else
                {
                    tris[kind].Add(Top(ia, ja)); tris[kind].Add(Top(ib, jb)); tris[kind].Add(Top(ic, jc));
                }
            }

            for (int i = i0; i < i1; i++)
                for (int j = j0; j < j1; j++)
                {
                    Tri(i, j, i, j + 1, i + 1, j);
                    Tri(i + 1, j, i, j + 1, i + 1, j + 1);
                }
            if (tv.Count == 0 && cv.Count == 0) return false;

            string key = $"terrain_{i0}_{j0}";
            var ch = Group(root, key, Vector3.zero);
            var mats = new[] { TopSand, TopEarth, TopGrass, Bed };
            if (tv.Count > 0)
            {
                var m = new Mesh { name = key + "_top", indexFormat = IndexFormat.UInt32 };
                m.SetVertices(tv); m.SetUVs(0, tuv);
                var used = new List<Material>();
                var subs = new List<List<int>>();
                for (int s = 0; s < 4; s++) if (tris[s].Count > 0) { subs.Add(tris[s]); used.Add(mats[s]); }
                m.subMeshCount = subs.Count;
                for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
                m.RecalculateNormals(); m.RecalculateBounds();
                m = SaveMeshForce(key + "_top", m);
                var go = MeshObj(ch, "地面", m, Vector3.zero, used[0], true);
                go.GetComponent<MeshRenderer>().sharedMaterials = used.ToArray();
            }
            if (cv.Count > 0)
            {
                var m = new Mesh { name = key + "_cliff", indexFormat = IndexFormat.UInt32 };
                m.SetVertices(cv); m.SetUVs(0, cuv); m.SetTriangles(ct, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                m = SaveMeshForce(key + "_cliff", m);
                MeshObj(ch, "石垣", m, Vector3.zero, Cliff, true);
            }
            return true;
        }

        // ───────── 水面 ─────────

        static void Waters(Transform parent)
        {
            var g = Group(parent, "川と水路", Vector3.zero);
            var river = Shaded("water_river", "Tsukuyomi/Water");
            river.SetColor("_Deep", new Color(0.05f, 0.2f, 0.32f));
            river.SetColor("_Shallow", new Color(0.12f, 0.5f, 0.6f));
            river.SetFloat("_Scale", 0.25f);
            river.SetFloat("_Alpha", 0.82f);
            river.SetVector("_Flow", new Vector4(0.3f, 0.2f, 0, 0));
            foreach (var ch in Island.Channels)
            {
                var pts = Island.Resample(ch.pts, 0.8f);
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                float along = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i];
                    var dir = (i < pts.Count - 1 ? pts[i + 1] - p : p - pts[i - 1]).normalized;
                    var side = new Vector2(-dir.y, dir.x) * (ch.half - 0.15f);
                    float y = Mathf.Max(0.05f, Island.SurfaceNoWater(p.x, p.y) - 0.75f);
                    if (i > 0) along += Vector2.Distance(pts[i - 1], p);
                    var l = p + side; var r = p - side;
                    v.Add(new Vector3(l.x, y, l.y)); v.Add(new Vector3(r.x, y, r.y));
                    uv.Add(new Vector2(0, along)); uv.Add(new Vector2(1, along));
                    if (i > 0)
                    {
                        int k = v.Count - 4;
                        t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                    }
                }
                var m = new Mesh { name = "water_" + ch.name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                m = SaveMeshForce("water_" + System.Array.IndexOf(Island.Channels, ch), m);
                var go = MeshObj(g, ch.name, m, Vector3.zero, river);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
