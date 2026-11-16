using System.Collections.Generic;
using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 空き地つぶし：できあがった町を 1m のマス目で調べ、何も建っていない広い所を見つけて、
    ///   広い所 → 通りを向いた家（崖の下なら、ときどき竹やぶ）
    ///   中くらい → 小さな広場（石畳・松・長椅子）・竹やぶ・物置小屋・菜園
    /// にする。「灯籠だけがぽつんと立つ砂地」をなくすため。
    /// </summary>
    public static class Infill
    {
        const float X0 = -172f, Z0 = -12f;
        const int NX = 344, NZ = 340;

        /// <summary>地形・道・壁など、「建っているもの」に数えないグループ。</summary>
        static readonly HashSet<string> SkipRoots = new HashSet<string> { "地形", "通り", "石垣", "島の護岸", "護岸", "川と水路", "海", "空に浮かぶ灯籠", "浮かぶ看板" };

        static Vector2 CellPos(int i, int j) => new Vector2(X0 + i + 0.5f, Z0 + j + 0.5f);

        static int LevelAt(Vector2 p)
        {
            float th = Island.TerraceHeight(p.x, p.y);
            for (int k = 0; k <= 2; k++) if (Mathf.Abs(th - Island.LevelTop(k)) < 0.25f) return k;
            return -1;
        }

        static string Path(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.name);
            return string.Join("/", parts.GetRange(0, Mathf.Min(2, parts.Count))) + "…" + t.name;
        }

        static bool Skipped(Transform t, Transform root)
        {
            for (var x = t; x != null && x != root; x = x.parent) if (SkipRoots.Contains(x.name)) return true;
            return false;
        }

        public static void Run(Transform houses, Transform[] roots, List<(Vector2 c, float r)> keepOut, ref int seed)
        {
            Physics.SyncTransforms();
            var occ = new bool[NX, NZ];
            var lvl = new int[NX, NZ];
            var why = new byte[NX, NZ];   // 確認用：0 空き 1 使えない所 2 道・石段 3 建っているもの
            // 1) 使えない所：島のふち・海ぞいの遊歩道・水・段の斜面・丘・頂上・名所
            for (int i = 0; i < NX; i++)
                for (int j = 0; j < NZ; j++)
                {
                    var p = CellPos(i, j);
                    int lv = LevelAt(p);
                    lvl[i, j] = lv;
                    bool bad = lv < 0 || Island.Inside(p.x, p.y) < 3.5f || Island.NearestWater(p.x, p.y) < 1.6f;
                    if (!bad && lv == 0 && Island.Inside(p.x, p.y) < 13f) bad = true;            // 海ぞいは別に作る
                    if (!bad && Vector2.Distance(p, Island.HillCenter) < Island.HillRadius + 3f) bad = true;
                    if (!bad) foreach (var (c, r) in Town.Landmarks) if ((p - c).sqrMagnitude < (r + 1.5f) * (r + 1.5f)) { bad = true; break; }
                    if (!bad) foreach (var (c, r) in keepOut) if ((p - c).sqrMagnitude < r * r) { bad = true; break; }
                    occ[i, j] = bad;
                    if (bad) why[i, j] = 1;
                }
            void MarkCircle(Vector2 c, float r)
            {
                int i0 = Mathf.FloorToInt(c.x - r - X0), i1 = Mathf.CeilToInt(c.x + r - X0);
                int j0 = Mathf.FloorToInt(c.y - r - Z0), j1 = Mathf.CeilToInt(c.y + r - Z0);
                for (int i = Mathf.Max(0, i0); i <= Mathf.Min(NX - 1, i1); i++)
                    for (int j = Mathf.Max(0, j0); j <= Mathf.Min(NZ - 1, j1); j++)
                        if ((CellPos(i, j) - c).sqrMagnitude <= r * r) { if (!occ[i, j]) why[i, j] = 2; occ[i, j] = true; }
            }
            // 2) 道と石段
            foreach (var s in Town.Streets) foreach (var q in s.pts) MarkCircle(q, s.half + 0.9f);
            foreach (var st in Town.StairsList)
            {
                float len = Vector2.Distance(st.bottom, st.top);
                for (float t = 0; t <= len; t += 1f) MarkCircle(Vector2.Lerp(st.bottom, st.top, t / Mathf.Max(0.01f, len)), st.width / 2 + 2f);
            }
            // 3) 建っているもの（家・名所・小物）
            var blame = new Dictionary<string, int>();
            foreach (var root in roots)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (Skipped(r.transform, root)) continue;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!r.enabled) continue;
                    var b = r.bounds;
                    if (b.size.x > 32 || b.size.z > 32) continue;
                    float gy = Island.Height(b.center.x, b.center.z);
                    if (b.max.y < gy + 0.25f || b.min.y > gy + 4.5f) continue;   // 地面に敷いたもの・頭の上のものは数えない
                    int i0 = Mathf.FloorToInt(b.min.x - 0.4f - X0), i1 = Mathf.CeilToInt(b.max.x + 0.4f - X0);
                    int j0 = Mathf.FloorToInt(b.min.z - 0.4f - Z0), j1 = Mathf.CeilToInt(b.max.z + 0.4f - Z0);
                    for (int i = Mathf.Max(0, i0); i <= Mathf.Min(NX - 1, i1); i++)
                        for (int j = Mathf.Max(0, j0); j <= Mathf.Min(NZ - 1, j1); j++) { if (!occ[i, j]) { why[i, j] = 3; string key = Path(r.transform, root); blame[key] = (blame.TryGetValue(key, out int v) ? v : 0) + 1; } occ[i, j] = true; }
                }

            DebugImage(why, "infill_before.png");

            // 4) 空いたマスから、いちばん近い「使えない所」までの距離
            var clr = Clearance(occ);
            var cand = new List<(int i, int j, float c)>();
            for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++) if (clr[i, j] >= 2.8f) cand.Add((i, j, clr[i, j]));
            cand.Sort((a, b) => b.c.CompareTo(a.c));

            var g = Group(houses, "すきまの町", Vector3.zero);
            int nHouse = 0, nPlaza = 0, nBamboo = 0, nShed = 0, nGarden = 0, nLounge = 0;
            foreach (var (ci, cj, c0) in cand)
            {
                if (occ[ci, cj]) continue;
                // 置いたものでマスがうまったかもしれないので、半径をはかり直す
                float r = FreeRadius(occ, ci, cj, c0) - 0.5f;
                if (r < 2.3f) continue;
                var p = CellPos(ci, cj);
                int lv = lvl[ci, cj];
                float y = Island.LevelTop(lv);
                bool cliffFoot = CliffFoot(p, lv);
                double roll = Rng.NextDouble();
                bool placed = false;
                float used = r;

                // 広い所のいくつかは、中でくつろげる茶屋の座敷に（ソファは通りの方を向く）
                if (r >= 5.4f && nLounge < 6 && !cliffFoot && Rng.NextDouble() < 0.3)
                {
                    var (sp, _) = NearestStreet(p);
                    var f = (sp - p).normalized;
                    float ly = Mathf.Atan2(-f.x, -f.y) * Mathf.Rad2Deg;
                    if (Town.Flat(p, 8f, 6f, ly, out float hy0, 0.3f))
                    {
                        Palace.Lounge(g, new Vector3(p.x, hy0, p.y), ly, 8f, 6f);
                        nLounge++; used = 6.4f; placed = true;
                    }
                }
                if (!placed && r >= 3.7f && !(cliffFoot && roll < 0.4))
                {
                    float w = Mathf.Clamp(r * 1.35f, 5f, 9.5f), d = Mathf.Clamp(r * 1.25f, 5f, 9f);
                    var (sp, sdist) = NearestStreet(p);
                    float rotY;
                    if (sdist < 16f) { var f = (sp - p).normalized; rotY = Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg; }
                    else rotY = new[] { 0f, 90f, 180f, -90f }[Ri(0, 4)];
                    if (Town.Flat(p, w, d, rotY, out float hy, 0.45f) && !TerrainBuilder.NearStairs(p, Mathf.Max(w, d) * 0.5f + 1.4f))
                    {
                        seed++;
                        double k = Rng.NextDouble();
                        bool shop = sdist < 9f;
                        int floors = lv == 1 ? (k < 0.15 ? Ri(4, 6) : k < 0.45 ? 3 : k < 0.85 ? 2 : 1) : lv == 2 ? (k < 0.3 ? Ri(3, 6) : 2) : (k < 0.4 ? 2 : k < 0.6 ? 3 : 1);
                        if (k > 0.9 && !shop) Buildings.Kura(g, new Vector3(p.x, hy, p.y), rotY, Mathf.Min(w, 7.5f), Mathf.Min(d, 7f));
                        else Buildings.TownHouse(g, new Vector3(p.x, hy, p.y), rotY, w, d, floors, Ri(0, 4), shop, seed);
                        Town.Reserve(p, Mathf.Max(w, d) * 0.5f);
                        Town.Lots.Add(new Town.Lot { pos = new Vector3(p.x, hy, p.y), rotY = rotY, w = w, d = d, facing = shop });
                        used = Mathf.Sqrt(w * w + d * d) / 2 + 0.6f;
                        nHouse++; placed = true;
                    }
                }
                if (!placed)
                {
                    float rr = Mathf.Min(r, 6.5f);
                    if (!Town.Flat(p, rr * 1.4f, rr * 1.4f, 0, out float hy, 0.35f)) { MarkCircle(p, 1.5f); continue; }
                    if (cliffFoot || roll < 0.22) { Landmarks.BambooPatch(g, p, rr - 0.2f, hy); nBamboo++; used = rr; }
                    else if (roll < 0.55) { SmallPlaza(g, new Vector3(p.x, hy, p.y), rr - 0.3f); nPlaza++; used = rr; }
                    else if (roll < 0.78) { Shed(g, new Vector3(p.x, hy, p.y), R(0, 360)); nShed++; used = 2.8f; }
                    else { Garden(g, new Vector3(p.x, hy, p.y), R(0, 360), rr); nGarden++; used = Mathf.Min(rr * 1.2f, 6f) * 0.72f; }
                    Town.Reserve(p, used);
                }
                MarkCircle(p, used + 0.5f);
            }
            Debug.Log($"[Tsukuyomi] infill: houses {nHouse}, lounges {nLounge}, plazas {nPlaza}, bamboo {nBamboo}, sheds {nShed}, gardens {nGarden}");
        }

        /// <summary>確認用の絵（環境変数 TSUKUYOMI_INFILL_DEBUG のフォルダへ）。灰=使えない 青=道 赤=建物 白=空き</summary>
        static void DebugImage(byte[,] why, string name)
        {
            string dir = System.Environment.GetEnvironmentVariable("TSUKUYOMI_INFILL_DEBUG");
            if (string.IsNullOrEmpty(dir)) return;
            var tex = new Texture2D(NX, NZ, TextureFormat.RGB24, false);
            var cols = new[] { Color.white, new Color(0.45f, 0.45f, 0.45f), new Color(0.3f, 0.5f, 1f), new Color(0.9f, 0.25f, 0.2f) };
            for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++) tex.SetPixel(i, j, cols[why[i, j]]);
            tex.Apply();
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name), tex.EncodeToPNG());
        }

        static float[,] Clearance(bool[,] occ)
        {
            var d = new float[NX, NZ];
            const float big = 1e6f, D = 1.4142f;
            for (int i = 0; i < NX; i++) for (int j = 0; j < NZ; j++) d[i, j] = occ[i, j] ? 0 : big;
            for (int i = 0; i < NX; i++)
                for (int j = 0; j < NZ; j++)
                {
                    if (d[i, j] == 0) continue;
                    float v = d[i, j];
                    if (i > 0) v = Mathf.Min(v, d[i - 1, j] + 1);
                    if (j > 0) v = Mathf.Min(v, d[i, j - 1] + 1);
                    if (i > 0 && j > 0) v = Mathf.Min(v, d[i - 1, j - 1] + D);
                    if (i < NX - 1 && j > 0) v = Mathf.Min(v, d[i + 1, j - 1] + D);
                    if (i == 0 || j == 0 || i == NX - 1) v = Mathf.Min(v, 1);
                    d[i, j] = v;
                }
            for (int i = NX - 1; i >= 0; i--)
                for (int j = NZ - 1; j >= 0; j--)
                {
                    if (d[i, j] == 0) continue;
                    float v = d[i, j];
                    if (i < NX - 1) v = Mathf.Min(v, d[i + 1, j] + 1);
                    if (j < NZ - 1) v = Mathf.Min(v, d[i, j + 1] + 1);
                    if (i < NX - 1 && j < NZ - 1) v = Mathf.Min(v, d[i + 1, j + 1] + D);
                    if (i > 0 && j < NZ - 1) v = Mathf.Min(v, d[i - 1, j + 1] + D);
                    if (j == NZ - 1) v = Mathf.Min(v, 1);
                    d[i, j] = v;
                }
            return d;
        }

        static float FreeRadius(bool[,] occ, int ci, int cj, float max)
        {
            int R = Mathf.CeilToInt(max) + 1;
            float best = max;
            for (int i = ci - R; i <= ci + R; i++)
                for (int j = cj - R; j <= cj + R; j++)
                {
                    if (i < 0 || j < 0 || i >= NX || j >= NZ) { best = Mathf.Min(best, Mathf.Sqrt((i - ci) * (i - ci) + (j - cj) * (j - cj))); continue; }
                    if (!occ[i, j]) continue;
                    best = Mathf.Min(best, Mathf.Sqrt((i - ci) * (i - ci) + (j - cj) * (j - cj)));
                }
            return best;
        }

        static (Vector2 p, float d) NearestStreet(Vector2 p)
        {
            Vector2 best = p; float bd = float.MaxValue;
            foreach (var s in Town.Streets)
                for (int k = 0; k < s.pts.Count; k += 2)
                {
                    float d = Vector2.Distance(s.pts[k], p) - s.half;
                    if (d < bd) { bd = d; best = s.pts[k]; }
                }
            return (best, bd);
        }

        /// <summary>上の段の石垣のすぐ下（崖ぎわ）か。竹やぶが似合う所。</summary>
        static bool CliffFoot(Vector2 p, int lv)
        {
            if (lv < 0 || lv > 2) return false;
            var L = Island.Levels[lv];
            var d = p - L.c;
            float out_ = d.magnitude - L.RadiusAt(Mathf.Atan2(d.y, d.x));
            return out_ > 0 && out_ < 9f;
        }

        // ───────── 小さなもの ─────────

        static Material Soil => Lit("soil", new Color(0.32f, 0.22f, 0.15f), 0.05f);
        static Material Leaf => Lit("veg_leaf", new Color(0.3f, 0.55f, 0.24f), 0.1f);

        /// <summary>小さな広場：まるい石畳に、まん中の松と石の縁、まわりに長椅子と提灯の柱。</summary>
        static void SmallPlaza(Transform parent, Vector3 c, float r)
        {
            var g = Group(parent, "小さな広場", c, R(0, 360));
            Cyl(g, "石畳", new Vector3(0, 0.04f, 0), r, 0.12f, Paving);
            Cyl(g, "縁石", new Vector3(0, 0.02f, 0), r + 0.25f, 0.1f, Stone);
            Cyl(g, "植えますの縁", new Vector3(0, 0.2f, 0), 1.2f, 0.4f, Stone, true);
            Cyl(g, "植えますの土", new Vector3(0, 0.38f, 0), 1.05f, 0.06f, Soil);
            if (Rng.NextDouble() < 0.5) Pine(g, new Vector3(0, 0.4f, 0), R(0.6f, 0.85f));
            else Sakura(g, new Vector3(0, 0.4f, 0), R(0.45f, 0.6f), false);
            int benches = r > 3.2f ? 3 : 2;
            for (int i = 0; i < benches; i++)
            {
                float a = i * 360f / benches + 30f;
                var q = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, r - 0.9f);
                var b = Group(g, "長椅子", q, a);
                Box(b, "座面", new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.1f, 0.5f), Wood, true);
                foreach (float x in new[] { -0.75f, 0.75f }) Box(b, "脚", new Vector3(x, 0.2f, 0), new Vector3(0.1f, 0.4f, 0.45f), WoodDark);
            }
            var lp = Quaternion.Euler(0, 360f / benches / 2 + 30f, 0) * new Vector3(0, 0, r - 0.5f);
            Cyl(g, "灯りの柱", lp + new Vector3(0, 1.2f, 0), 0.06f, 2.4f, WoodDark);
            Chochin(g, lp + new Vector3(0, 2.3f, 0), GlowWhite);
        }

        /// <summary>物置小屋：板壁と板葺きの屋根、わきに樽とまき。</summary>
        static void Shed(Transform parent, Vector3 c, float yaw)
        {
            var g = Group(parent, "物置小屋", c, yaw);
            Box(g, "床", new Vector3(0, 0.15f, 0), new Vector3(3.2f, 0.3f, 2.6f), Stone, true);
            Box(g, "板壁", new Vector3(0, 1.35f, 0), new Vector3(3f, 2.1f, 2.4f), WoodDark, true);
            Box(g, "戸", new Vector3(0.4f, 1.2f, 1.22f), new Vector3(1.1f, 1.8f, 0.05f), Wood);
            MeshObj(g, "屋根", Roof(3.8f, 3.2f, 0.9f, 0.25f), new Vector3(0, 2.4f, 0), RoofBark);
            for (int i = 0; i < 2; i++) Cyl(g, "樽", new Vector3(1.9f, 0.45f, -0.6f + i * 0.8f), 0.32f, 0.9f, Wood, true);
            for (int i = 0; i < 6; i++) Box(g, "まき", new Vector3(-1.9f, 0.15f + (i / 3) * 0.22f, -0.6f + (i % 3) * 0.4f), new Vector3(0.6f, 0.2f, 0.2f), Wood);
        }

        /// <summary>菜園：土の畝と野菜、まわりに低い竹垣。</summary>
        static void Garden(Transform parent, Vector3 c, float yaw, float r)
        {
            float s = Mathf.Min(r * 1.2f, 6f);
            var g = Group(parent, "菜園", c, yaw);
            int rows = Mathf.Max(2, Mathf.FloorToInt(s / 1.3f));
            for (int i = 0; i < rows; i++)
            {
                float z = -s / 2 + 0.6f + i * (s - 1.2f) / Mathf.Max(1, rows - 1);
                Box(g, "畝", new Vector3(0, 0.12f, z), new Vector3(s - 0.8f, 0.24f, 0.7f), Soil);
                for (float x = -s / 2 + 0.8f; x <= s / 2 - 0.8f; x += 0.7f)
                    Ball(g, "野菜", new Vector3(x, 0.38f, z), new Vector3(0.45f, 0.35f, 0.45f), Leaf);
            }
            var fence = Lit("bamboo", new Color(0.42f, 0.62f, 0.3f), 0.45f);
            foreach (float sd in new[] { -1f, 1f })
            {
                Box(g, "竹垣", new Vector3(0, 0.45f, sd * s / 2), new Vector3(s, 0.06f, 0.06f), fence);
                Box(g, "竹垣", new Vector3(sd * s / 2, 0.45f, 0), new Vector3(0.06f, 0.06f, s), fence);
                for (float t = -s / 2; t <= s / 2; t += 1f)
                {
                    Cyl(g, "杭", new Vector3(t, 0.35f, sd * s / 2), 0.04f, 0.7f, fence);
                    Cyl(g, "杭", new Vector3(sd * s / 2, 0.35f, t), 0.04f, 0.7f, fence);
                }
            }
            // かかし
            Cyl(g, "かかし", new Vector3(0, 0.9f, 0), 0.05f, 1.8f, WoodDark);
            Box(g, "かかしの腕", new Vector3(0, 1.4f, 0), new Vector3(1.2f, 0.06f, 0.06f), WoodDark);
            Ball(g, "かかしの頭", new Vector3(0, 1.85f, 0), Vector3.one * 0.3f, Paper);
            Box(g, "かかしの着物", new Vector3(0, 1.2f, 0), new Vector3(0.7f, 0.6f, 0.2f), Lit("scarecrow_cloth", new Color(0.3f, 0.35f, 0.6f), 0.05f));
        }
    }
}
