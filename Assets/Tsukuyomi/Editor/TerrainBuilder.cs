using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Tsukuyomi.EditorTools.Kit;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 地形を作る。
    ///   ・Island.Height の高さで地面のメッシュ（区画ごと）。通りの下は平らにならす（道が埋まったり傾いたりしない）
    ///   ・段のふちには石垣（少し内側へ傾いた石の壁と、上のふた）。石段の通る所はあける
    ///   ・川・水路の両岸には石の護岸
    ///   ・水面
    /// </summary>
    public static class TerrainBuilder
    {
        const float Cell = 0.8f;
        const int ChunkCells = 64;
        static readonly Rect Bounds = new Rect(-180, -16, 360, 350);
        const string TexDir = "Assets/Tsukuyomi/Imported/Textures/";

        static Texture2D Tex(string name, bool normal)
        {
            string path = TexDir + name + (normal ? "_nor" : "_diff") + ".jpg";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && normal && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material TexMat(string name, string tex, Color tint, float tile, float smooth = 0.1f)
        {
            var m = LitTex(name, Tex(tex, false), false, smooth);
            var n = Tex(tex, true);
            if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f); }
            m.SetColor("_BaseColor", tint);
            m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material TopSand => Lit("t_sand", new Color(0.84f, 0.8f, 0.7f), 0.08f);
        public static Material TopEarth => TexMat("t_earth_tex", "grass_path_2", new Color(0.95f, 0.92f, 0.88f), 1.6f);
        public static Material TopGrass => TexMat("t_grass_tex", "leafy_grass", new Color(0.95f, 1f, 0.95f), 1.2f);
        public static Material Bed => Lit("t_bed", new Color(0.22f, 0.27f, 0.26f), 0.2f);
        public static Material StoneWall => TexMat("t_ishigaki", "stone_wall_05", new Color(0.92f, 0.9f, 0.86f), 1f, 0.15f);
        public static Material PavingTex => TexMat("t_paving", "stone_tiles_03", new Color(0.95f, 0.93f, 0.9f), 1f, 0.2f);
        public static Material AlleyTex => TexMat("t_alley_stone", "stone_tiles_03", new Color(0.78f, 0.7f, 0.6f), 1f, 0.15f);

        // ───────── 地面 ─────────

        public static void Build(Transform parent)
        {
            var root = Group(parent, "地形", Vector3.zero);
            int nx = Mathf.CeilToInt(Bounds.width / Cell), nz = Mathf.CeilToInt(Bounds.height / Cell);
            var H = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                    H[i, j] = Island.Height(Bounds.xMin + i * Cell, Bounds.yMin + j * Cell);
            FlattenUnderStreets(H, nx, nz);
            CarveStairs(H, nx, nz);

            int count = 0;
            for (int ci = 0; ci < nx; ci += ChunkCells)
                for (int cj = 0; cj < nz; cj += ChunkCells)
                    if (Chunk(root, H, ci, cj, Mathf.Min(ci + ChunkCells, nx), Mathf.Min(cj + ChunkCells, nz))) count++;
            Debug.Log($"[Tsukuyomi] terrain chunks: {count}");

            TerraceWalls(parent);
            Seawall(parent);
            Embankments(parent);
            Waters(parent);
        }

        /// <summary>通りの下の地面を、その通りの段の高さにそろえる（水の上と崖はさわらない）。</summary>
        static void FlattenUnderStreets(float[,] H, int nx, int nz)
        {
            foreach (var s in Town.Streets)
            {
                if (s.level < 0) continue;
                float target = Island.LevelTop(s.level);
                float reach = s.half + 1.2f;
                foreach (var p in s.pts)
                {
                    if (Island.NearestWater(p.x, p.y) < s.half + 0.5f) continue;
                    int i0 = Mathf.FloorToInt((p.x - reach - Bounds.xMin) / Cell), i1 = Mathf.CeilToInt((p.x + reach - Bounds.xMin) / Cell);
                    int j0 = Mathf.FloorToInt((p.y - reach - Bounds.yMin) / Cell), j1 = Mathf.CeilToInt((p.y + reach - Bounds.yMin) / Cell);
                    for (int i = Mathf.Max(0, i0); i <= Mathf.Min(nx, i1); i++)
                        for (int j = Mathf.Max(0, j0); j <= Mathf.Min(nz, j1); j++)
                        {
                            float x = Bounds.xMin + i * Cell, z = Bounds.yMin + j * Cell;
                            float d = Vector2.Distance(new Vector2(x, z), p);
                            if (d > reach) continue;
                            if (Mathf.Abs(Island.TerraceHeight(x, z) - target) > 0.3f) continue; // 崖の斜面・丘・ちがう段はさわらない（石段の上が埋まる原因）
                            if (Island.NearestWater(x, z) < 0.6f) continue;
                            float k = 1 - Ts.Smooth(s.half + 0.3f, reach, d);
                            H[i, j] = Mathf.Lerp(H[i, j], target, k);
                        }
                }
            }
        }

        /// <summary>石段の下の地面を、段より低く彫る（崖の上の数段が土に埋まらないように）。</summary>
        static void CarveStairs(float[,] H, int nx, int nz)
        {
            foreach (var c in Town.StairCuts)
            {
                var side = new Vector2(c.dir.y, -c.dir.x);
                float reach = c.width / 2 + 0.8f;
                var a = c.start + c.dir * c.tBottom; var b = c.start + c.dir * (c.tTop + 0.2f);
                float xmin = Mathf.Min(a.x, b.x) - reach - 1, xmax = Mathf.Max(a.x, b.x) + reach + 1;
                float zmin = Mathf.Min(a.y, b.y) - reach - 1, zmax = Mathf.Max(a.y, b.y) + reach + 1;
                for (int i = Mathf.Max(0, Mathf.FloorToInt((xmin - Bounds.xMin) / Cell)); i <= Mathf.Min(nx, Mathf.CeilToInt((xmax - Bounds.xMin) / Cell)); i++)
                    for (int j = Mathf.Max(0, Mathf.FloorToInt((zmin - Bounds.yMin) / Cell)); j <= Mathf.Min(nz, Mathf.CeilToInt((zmax - Bounds.yMin) / Cell)); j++)
                    {
                        var p = new Vector2(Bounds.xMin + i * Cell, Bounds.yMin + j * Cell) - c.start;
                        float t = Vector2.Dot(p, c.dir), sd = Mathf.Abs(Vector2.Dot(p, side));
                        if (sd > reach || t < c.tBottom || t > c.tTop + 0.2f) continue;
                        int k = Mathf.Clamp(Mathf.FloorToInt((t - c.tBottom) / c.run), 0, c.n - 1);
                        float limit = c.h0 + k * (c.h1 - c.h0) / c.n - 0.12f;   // ひとつ下の段の面より下
                        if (H[i, j] > limit) H[i, j] = limit;
                    }
            }
        }

        static Vector3 V(float[,] H, int i, int j) => new Vector3(Bounds.xMin + i * Cell, H[i, j], Bounds.yMin + j * Cell);

        static int Kind(Vector3 c, Vector3 n)
        {
            if (n.y < 0.55f) return 3;                                     // 崖（石垣の裏にかくれる）
            if (!float.IsNaN(Island.WaterSurface(c.x, c.z)) && Island.Inside(c.x, c.z) > -1f) return 3;
            if (Vector2.Distance(new Vector2(c.x, c.z), Island.HillCenter) < Island.HillRadius + 2) return 2;

            if (c.y > Island.LevelTop(2) - 0.5f) return 2;                 // 上段・頂上は苔の庭
            return 1;
        }

        static bool Chunk(Transform root, float[,] H, int i0, int j0, int i1, int j1)
        {
            var tv = new List<Vector3>(); var tuv = new List<Vector2>();
            var tris = new List<int>[4] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var idx = new Dictionary<int, int>();
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
                if (a.y < -3.3f && b.y < -3.3f && c.y < -3.3f) return;
                var n = Vector3.Cross(b - a, c - a).normalized;
                int kind = Kind((a + b + c) / 3, n);
                tris[kind].Add(Top(ia, ja)); tris[kind].Add(Top(ib, jb)); tris[kind].Add(Top(ic, jc));
            }
            for (int i = i0; i < i1; i++)
                for (int j = j0; j < j1; j++)
                {
                    Tri(i, j, i, j + 1, i + 1, j);
                    Tri(i + 1, j, i, j + 1, i + 1, j + 1);
                }
            if (tv.Count == 0) return false;
            string key = $"terrain_{i0}_{j0}";
            var ch = Group(root, key, Vector3.zero);
            var mats = new[] { TopSand, TopEarth, TopGrass, Bed };
            var m = new Mesh { name = key + "_top", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(tv); m.SetUVs(0, tuv);
            var used = new List<Material>(); var subs = new List<List<int>>();
            for (int s = 0; s < 4; s++) if (tris[s].Count > 0) { subs.Add(tris[s]); used.Add(mats[s]); }
            m.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            m = SaveMeshForce(key + "_top", m);
            var go = MeshObj(ch, "地面", m, Vector3.zero, used[0], true);
            go.GetComponent<MeshRenderer>().sharedMaterials = used.ToArray();
            return true;
        }

        // ───────── 石垣 ─────────

        /// <summary>壁の帯（下 → 上の面と、上のふた）を足していく道具。</summary>
        class WallBuilder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            float _along;

            /// <summary>p0→p1 の区間。out は壁の外向き（低い側）。</summary>
            public void Segment(Vector2 p0, Vector2 p1, Vector2 out0, Vector2 out1, float bot0, float bot1, float top0, float top1, float batter, float cap)
            {
                float len = Vector2.Distance(p0, p1);
                // 壁の面（上ほど内側へ）
                var b0 = new Vector3(p0.x, bot0, p0.y); var b1 = new Vector3(p1.x, bot1, p1.y);
                var u0 = p0 - out0 * batter * (top0 - bot0); var u1 = p1 - out1 * batter * (top1 - bot1);
                var t0 = new Vector3(u0.x, top0, u0.y); var t1 = new Vector3(u1.x, top1, u1.y);
                int k = v.Count;
                v.AddRange(new[] { b0, b1, t0, t1 });
                uv.AddRange(new[] { new Vector2(_along / 2.05f, bot0 / 2.05f), new Vector2((_along + len) / 2.05f, bot1 / 2.05f), new Vector2(_along / 2.05f, top0 / 2.05f), new Vector2((_along + len) / 2.05f, top1 / 2.05f) });
                // 外から見える向き（out 側が表）
                var nrm = Vector3.Cross(b1 - b0, t0 - b0);
                if (Vector3.Dot(nrm, new Vector3(out0.x, 0, out0.y)) > 0) t.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                else t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                // 上のふた（内側へ cap m）
                var c0 = u0 - out0 * cap; var c1 = u1 - out1 * cap;
                var q0 = new Vector3(c0.x, top0, c0.y); var q1 = new Vector3(c1.x, top1, c1.y);
                k = v.Count;
                v.AddRange(new[] { t0, t1, q0, q1 });
                uv.AddRange(new[] { new Vector2(_along / 2.05f, 0), new Vector2((_along + len) / 2.05f, 0), new Vector2(_along / 2.05f, cap / 2.05f), new Vector2((_along + len) / 2.05f, cap / 2.05f) });
                var up = Vector3.Cross(t1 - t0, q0 - t0);
                if (up.y > 0) t.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                else t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                _along += len;
            }

            public void Emit(Transform parent, string name, string key, Material mat)
            {
                if (t.Count == 0) return;
                var m = new Mesh { name = key, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
                m = SaveMeshForce(key, m);
                MeshObj(parent, name, m, Vector3.zero, mat, true);
            }
        }

        public static bool NearStairs(Vector2 p, float margin)
        {
            foreach (var s in Town.StairsList)
            {
                var ab = s.top - s.bottom;
                float t = Mathf.Clamp01(Vector2.Dot(p - s.bottom, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(p, s.bottom + ab * t) < s.width / 2 + margin) return true;
            }
            return false;
        }

        static void TerraceWalls(Transform parent)
        {
            var g = Group(parent, "石垣", Vector3.zero);
            for (int li = 0; li < Island.Levels.Length; li++)
            {
                var L = Island.Levels[li];
                float top = Island.LevelTop(li + 1) + 0.04f;
                var wb = new WallBuilder();
                int n = Mathf.CeilToInt(2 * Mathf.PI * L.r / 1.0f);
                Vector2 P(int i) { float a = i * 2 * Mathf.PI / n; return L.c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * L.RadiusAt(a); }
                Vector2 Out(int i) { var a = P(i - 1); var b = P(i + 1); var d = (b - a).normalized; return new Vector2(d.y, -d.x); }
                for (int i = 0; i < n; i++)
                {
                    var p0 = P(i); var p1 = P(i + 1);
                    var mid = (p0 + p1) / 2;
                    if (NearStairs(mid, 0.6f)) continue;
                    if (Island.NearestWater(mid.x, mid.y) < 0.6f) continue;   // 滝のところ（石垣が川をふさがない）
                    var o0 = Out(i); var o1 = Out(i + 1);
                    // 外（低い側）の地面の高さ
                    float bot0 = Island.Height(p0.x + o0.x * 1.2f, p0.y + o0.y * 1.2f) - 0.6f;
                    float bot1 = Island.Height(p1.x + o1.x * 1.2f, p1.y + o1.y * 1.2f) - 0.6f;
                    if (top - Mathf.Max(bot0, bot1) < 1f) continue;   // 丘などで段差がない所
                    wb.Segment(p0, p1, o0, o1, bot0, bot1, top, top, 0.1f, 1.6f);
                }
                wb.Emit(g, "石垣" + li, "ishigaki_" + li, StoneWall);
            }
        }

        /// <summary>島のまわりの石の護岸（大鳥居からの回廊・水路と川の出口はあける）。</summary>
        static void Seawall(Transform parent)
        {
            var g = Group(parent, "島の護岸", Vector3.zero);
            var wb = new WallBuilder();
            var c = Island.Center;
            int n = Mathf.CeilToInt(2 * Mathf.PI * Island.Radius / 1.0f);
            Vector2 P(int i) { float a = i * 2 * Mathf.PI / n; return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Island.EdgeRadius(a); }
            Vector2 Out(int i) { var a = P(i - 1); var b = P(i + 1); var d = (b - a).normalized; return new Vector2(d.y, -d.x); }
            float top = Island.Ground + 0.04f;
            for (int i = 0; i < n; i++)
            {
                var p0 = P(i); var p1 = P(i + 1);
                var mid = (p0 + p1) / 2;
                if (Mathf.Abs(mid.x) < 4.2f && mid.y < 10f) continue;           // 海の回廊
                if (Island.NearestWater(mid.x, mid.y) < 0.5f) continue;        // 水路・川の出口
                wb.Segment(p0, p1, Out(i), Out(i + 1), -3.6f, -3.6f, top, top, 0.06f, 1.7f);
            }
            wb.Emit(g, "島の護岸", "seawall", StoneWall);
        }

        static void Embankments(Transform parent)
        {
            var g = Group(parent, "護岸", Vector3.zero);
            int ci = 0;
            foreach (var ch in Island.Channels)
            {
                var pts = Island.Resample(ch.pts, 1f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var wb = new WallBuilder();
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        var c0 = pts[i]; var c1 = pts[i + 1];
                        var d = (c1 - c0).normalized;
                        var nrm = new Vector2(-d.y, d.x) * side;    // 岸の外向き（陸の方）
                        var p0 = c0 + nrm * ch.half; var p1 = c1 + nrm * ch.half;
                        if (Island.Inside(p0.x, p0.y) < 1f || Island.Inside(p1.x, p1.y) < 1f) continue;
                        float top0 = Island.SurfaceNoWater(p0.x, p0.y) + 0.03f, top1 = Island.SurfaceNoWater(p1.x, p1.y) + 0.03f;
                        if (Mathf.Abs(top0 - top1) > 0.6f) continue;                 // 滝の段差（石垣の切れ目）は作らない
                        float bot0 = top0 - ch.depth - 0.4f, bot1 = top1 - ch.depth - 0.4f;
                        // 壁の表は水の側（-nrm）。壁は陸の方へ少し傾け、上のふたは陸の側へ 0.9m
                        wb.Segment(p0, p1, -nrm, -nrm, bot0, bot1, top0, top1, 0.04f, 0.9f);
                    }
                    wb.Emit(g, ch.name + "の護岸", $"embank_{ci}_{(side > 0 ? "l" : "r")}", StoneWall);
                }
                ci++;
            }
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
            int ci = 0;
            foreach (var ch in Island.Channels)
            {
                var pts = Island.Resample(ch.pts, 0.8f);
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                float along = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i];
                    var dir = (i < pts.Count - 1 ? pts[i + 1] - p : p - pts[i - 1]).normalized;
                    var side = new Vector2(-dir.y, dir.x) * (ch.half + 0.05f);
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
                m = SaveMeshForce("water_" + ci, m);
                var go = MeshObj(g, ch.name, m, Vector3.zero, river);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                ci++;
            }
        }
    }
}
