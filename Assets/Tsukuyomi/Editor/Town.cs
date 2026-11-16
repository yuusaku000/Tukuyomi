using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 通り・石段・橋・建物を、島の形（Island）に合わせて自動で並べる。
    /// 場所の取りあいは「円」で管理する（名所 → 通り → 石段 → 建物 の順に場所を取る）。
    /// </summary>
    public static class Town
    {
        static readonly List<(Vector2 c, float r)> Taken = new List<(Vector2, float)>();

        /// <summary>置いた建物（正面はローカル +z）。小物を店先や家のわきに置くのに使う。</summary>
        public struct Lot { public Vector3 pos; public float rotY, w, d; public bool facing; }
        public static readonly List<Lot> Lots = new List<Lot>();

        public static void Reset() { Taken.Clear(); Lots.Clear(); }

        public static void Reserve(Vector2 c, float r) => Taken.Add((c, r));

        public static bool Free(Vector2 c, float r)
        {
            foreach (var t in Taken)
                if ((t.c - c).sqrMagnitude < (t.r + r) * (t.r + r)) return false;
            return true;
        }

        public static float H(Vector2 p) => Island.Height(p.x, p.y);
        public static Vector3 At(Vector2 p, float lift = 0) => new Vector3(p.x, H(p) + lift, p.y);

        /// <summary>そこが平らで、水や崖にかからないか。y に置く高さを返す。</summary>
        public static bool Flat(Vector2 c, float w, float d, float rotY, out float y, float tol = 0.35f)
        {
            var rot = Quaternion.Euler(0, rotY, 0);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    var o = rot * new Vector3(i * (w / 2 + 0.4f), 0, j * (d / 2 + 0.4f));
                    var p = new Vector2(c.x + o.x, c.y + o.z);
                    if (Island.NearestWater(p.x, p.y) < 1.2f || Island.Inside(p.x, p.y) < 3f) { y = 0; return false; }
                    float h = H(p);
                    lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                }
            y = hi;
            return hi - lo < tol && lo > 0.8f;
        }

        // ───────── 通り ─────────

        public class Street
        {
            public string name;
            public List<Vector2> pts;
            public float half;
            public int level; // -1 = どの段でもよい
        }

        public static readonly List<Street> Streets = new List<Street>();

        static bool OnLevel(Vector2 p, int level)
        {
            if (level < 0) return true;
            float want = Island.LevelTop(level);
            float h = Island.TerraceHeight(p.x, p.y);
            return Mathf.Abs(h - want) < 0.25f && Island.Inside(p.x, p.y) > 2f;
        }

        public static void AddStreet(string name, IEnumerable<Vector2> pts, float half, int level)
        {
            var arr = new List<Vector2>(pts).ToArray();
            Streets.Add(new Street { name = name, pts = Island.Resample(arr, 1f), half = half, level = level });
        }

        public static List<Vector2> Ring(Vector2 c, System.Func<float, float> radius, float a0Deg, float a1Deg, float stepDeg = 3f)
        {
            var list = new List<Vector2>();
            for (float a = a0Deg; a <= a1Deg + 0.01f; a += stepDeg)
            {
                float r = a * Mathf.Deg2Rad;
                list.Add(c + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * radius(r));
            }
            return list;
        }

        /// <summary>石畳を地面に沿って敷く。段の外・水の上は切る。水を渡るところには橋をかける。</summary>
        public static void BuildStreets(Transform parent, Material paving)
        {
            var g = Group(parent, "通り", Vector3.zero);
            int si = 0;
            foreach (var s in Streets)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                bool prevOk = false;
                int waterStart = -1;
                float along = 0;
                for (int i = 0; i < s.pts.Count; i++)
                {
                    var p = s.pts[i];
                    if (i > 0) along += Vector2.Distance(s.pts[i - 1], p);
                    bool water = Island.NearestWater(p.x, p.y) < 0.6f;
                    bool ok = OnLevel(p, s.level) && !water;
                    // 水の上：橋の始まりと終わりを覚えておく
                    if (water && OnLevel(p, s.level) || water && s.level < 0)
                    {
                        if (waterStart < 0) waterStart = Mathf.Max(0, i - 1);
                    }
                    else if (waterStart >= 0)
                    {
                        var a = s.pts[waterStart]; var b = s.pts[Mathf.Min(s.pts.Count - 1, i)];
                        var dir = (b - a).normalized;
                        a -= dir * 1.5f; b += dir * 1.5f;
                        var a3 = new Vector3(a.x, Island.SurfaceNoWater(a.x, a.y), a.y);
                        var b3 = new Vector3(b.x, Island.SurfaceNoWater(b.x, b.y), b.y);
                        Bridge(g, s.name + "の橋", a3, b3, s.half * 2 + 0.6f, Mathf.Min(1.6f, Vector2.Distance(a, b) * 0.12f), 10);
                        waterStart = -1;
                    }
                    if (!ok) { prevOk = false; continue; }
                    var dirv = (i < s.pts.Count - 1 ? s.pts[i + 1] - p : p - s.pts[i - 1]).normalized;
                    var side = new Vector2(-dirv.y, dirv.x) * s.half;
                    var l = p + side; var r = p - side;
                    v.Add(new Vector3(l.x, H(l) + 0.05f, l.y)); v.Add(new Vector3(r.x, H(r) + 0.05f, r.y));
                    uv.Add(new Vector2(0, along / 3f)); uv.Add(new Vector2(s.half * 2 / 3f, along / 3f));
                    if (prevOk)
                    {
                        int k = v.Count - 4;
                        t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                    }
                    prevOk = true;
                    if (i % 2 == 0) Reserve(p, s.half + 0.4f);
                }
                if (t.Count == 0) continue;
                var m = new Mesh { name = "street_" + si, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                m = SaveMeshForce("street_" + si, m);
                var go = MeshObj(g, s.name, m, Vector3.zero, paving);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                si++;
            }
        }

        // ───────── 石段 ─────────

        /// <summary>
        /// start から dir の方へ進んで、最初の崖に石段をかける。torii なら千本鳥居。戻り値は上りきった場所。
        /// </summary>
        public static Vector2 Stairs(Transform parent, string name, Vector2 start, Vector2 dir, float width, bool torii, Material stone)
        {
            dir.Normalize();
            float h0 = Island.TerraceHeight(start.x, start.y);
            float tEdge = -1;
            for (float t = 0; t < 60; t += 0.1f)
            {
                var p = start + dir * t;
                if (Island.TerraceHeight(p.x, p.y) > h0 + 0.5f) { tEdge = t; break; }
            }
            if (tEdge < 0) { Debug.LogWarning("[Tsukuyomi] stairs: no cliff for " + name); return start; }
            var topP = start + dir * (tEdge + 3f);
            float h1 = Island.TerraceHeight(topP.x, topP.y);
            float rise = h1 - h0;
            int n = Mathf.CeilToInt(rise / 0.3f);
            const float run = 0.45f;
            float tTop = tEdge + 0.9f;
            var g = Group(parent, name, Vector3.zero);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            var side = new Vector2(dir.y, -dir.x);
            for (int i = 0; i < n; i++)
            {
                float top = h0 + (i + 1) * rise / n;
                float tc = tTop - (n - i) * run + run / 2;
                var c = start + dir * tc;
                float bottom = h0 - 0.6f;
                Box(g, "段", new Vector3(c.x, (top + bottom) / 2, c.y), new Vector3(width, top - bottom, run + 0.03f), stone, true, new Vector3(0, yaw, 0));
                // 両わきの石の手すり
                foreach (float sd in new[] { -1f, 1f })
                {
                    var e = c + side * sd * (width / 2 + 0.35f);
                    Box(g, "袖", new Vector3(e.x, (top + 0.7f + bottom) / 2, e.y), new Vector3(0.7f, top + 0.7f - bottom, run + 0.03f), stone, true, new Vector3(0, yaw, 0));
                }
                if (torii && i % 3 == 1)
                {
                    float s = width / 14f;
                    var tg = Group(g, "鳥居", new Vector3(c.x, top, c.y), yaw + 90);
                    MiniTorii(tg, width + 0.4f, 3.6f + width * 0.15f);
                }
                if (i % 6 == 0) Reserve(c, width / 2 + 0.8f);
            }
            // 上と下に灯籠
            var bottomC = start + dir * (tTop - n * run - 1f);
            var topC = start + dir * (tTop + 1.2f);
            foreach (float sd in new[] { -1f, 1f })
            {
                Toro(g, At(bottomC + side * sd * (width / 2 + 1.6f)), true, 0.9f);
                Toro(g, At(topC + side * sd * (width / 2 + 1.6f)), false, 0.9f);
            }
            Reserve(bottomC, width / 2 + 2f);
            Reserve(topC, width / 2 + 2f);
            return topC;
        }

        /// <summary>小さな朱の鳥居（千本鳥居用）。ローカルの x 方向にまたぐ。</summary>
        public static void MiniTorii(Transform g, float span, float height)
        {
            float x = span / 2;
            Cyl(g, "柱", new Vector3(0, height / 2, -x), 0.13f, height, Vermilion);
            Cyl(g, "柱", new Vector3(0, height / 2, x), 0.13f, height, Vermilion);
            Box(g, "貫", new Vector3(0, height * 0.78f, 0), new Vector3(0.18f, 0.16f, span + 0.3f), Vermilion);
            Box(g, "笠木", new Vector3(0, height + 0.1f, 0), new Vector3(0.3f, 0.22f, span + 0.9f), Black);
            Box(g, "島木", new Vector3(0, height - 0.08f, 0), new Vector3(0.24f, 0.16f, span + 0.6f), Vermilion);
        }

        // ───────── 建物を通りぞいに並べる ─────────

        public static int FillAlongStreets(Transform parent, System.Func<Street, int, Vector2, float, float, float, float, bool> place)
        {
            int placed = 0;
            foreach (var s in Streets)
            {
                foreach (float sideSign in new[] { 1f, -1f })
                {
                    float acc = 0;
                    int i = 1;
                    while (i < s.pts.Count - 1)
                    {
                        var p = s.pts[i];
                        var dir = (s.pts[i + 1] - s.pts[i - 1]).normalized;
                        var normal = new Vector2(-dir.y, dir.x) * sideSign;
                        float w = R(6f, 9.5f), d = R(7.5f, 10f);
                        var c = p + normal * (s.half + 0.7f + d / 2);
                        // 建物の正面（ローカル +z）を通りへ向ける
                        var face = -normal;
                        float rotY = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                        float rr = Mathf.Max(w, d) * 0.5f;
                        if (OnLevel(p, s.level) && Free(c, rr * 0.85f) && Flat(c, w, d, rotY, out float y))
                        {
                            if (place(s, placed, c, y, rotY, w, d))
                            {
                                Reserve(c, rr * 0.85f);
                                Lots.Add(new Lot { pos = new Vector3(c.x, y, c.y), rotY = rotY, w = w, d = d, facing = true });
                                placed++;
                                i += Mathf.CeilToInt(w + 0.6f);
                                continue;
                            }
                        }
                        i += 2;
                        acc += 2;
                    }
                }
            }
            return placed;
        }

        /// <summary>空いている平らな場所に、通りに関係なく家をつめる（町を密にする）。</summary>
        public static int FillGaps(Transform parent, Rect area, int tries, System.Func<int, Vector2, float, float, float, float, bool> place)
        {
            int placed = 0;
            for (int k = 0; k < tries; k++)
            {
                var c = new Vector2(R(area.xMin, area.xMax), R(area.yMin, area.yMax));
                float w = R(5.5f, 9f), d = R(5.5f, 8f);
                float rotY = new[] { 0f, 90f, 180f, -90f }[Ri(0, 4)] + R(-8, 8);
                float rr = Mathf.Max(w, d) * 0.5f;
                if (!Free(c, rr * 0.86f) || !Flat(c, w, d, rotY, out float y, 0.45f)) continue;
                if (place(placed, c, y, rotY, w, d))
                {
                    Reserve(c, rr * 0.86f);
                    Lots.Add(new Lot { pos = new Vector3(c.x, y, c.y), rotY = rotY, w = w, d = d, facing = false });
                    placed++;
                }
            }
            return placed;
        }
    }
}
