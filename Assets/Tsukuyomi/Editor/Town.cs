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

        public static void Reset() { Taken.Clear(); Lots.Clear(); BridgesMade.Clear(); StairsList.Clear(); Landmarks.Clear(); }

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

        /// <summary>p がどこかの通りの石畳の上（margin だけ広げて）にあるか。灯籠やのぼりを道に置かないため。</summary>
        public static bool OnAnyStreet(Vector2 p, float margin)
        {
            foreach (var s in Streets)
                for (int i = 0; i < s.pts.Count; i++)
                    if ((s.pts[i] - p).sqrMagnitude < (s.half + margin) * (s.half + margin)) return true;
            return false;
        }

        public static void AddStreet(string name, IEnumerable<Vector2> pts, float half, int level)
        {
            var arr = new List<Vector2>(pts).ToArray();
            Streets.Add(new Street { name = name, pts = Island.Resample(arr, 1f), half = half, level = level });
        }

        /// <summary>
        /// 路地を足す。段の外・丘・名所にかかる所で切り、切れた端は connect（外の環状通りの点を返す）へつないで、行き止まりをなくす。
        /// </summary>
        public static int AddLanes(string name, List<Vector2> raw, float half, int level, System.Func<Vector2, Vector2> connect)
        {
            var pts = Island.Resample(raw.ToArray(), 1f);
            bool Good(Vector2 p)
            {
                if (!OnLevel(p, level)) return false;
                if (Island.NearestWater(p.x, p.y) < half + 1f && Island.PondDistance(p.x, p.y) < half + 1.5f) return false;
                foreach (var (c, r) in Landmarks) if (Vector2.Distance(c, p) < r + half + 1f) return false;
                return true;
            }
            int pieces = 0;
            var cur = new List<Vector2>();
            void Flush()
            {
                if (cur.Count >= 8)
                {
                    Streets.Add(new Street { name = name + pieces, pts = new List<Vector2>(cur), half = half, level = level });
                    if (connect != null)
                        foreach (var end in new[] { cur[0], cur[cur.Count - 1] })
                        {
                            var to = connect(end);
                            bool clear = true;
                            for (float t = 0; t <= 1f; t += 0.05f) { var q = Vector2.Lerp(end, to, t); foreach (var (lc, lr) in Landmarks) if (Vector2.Distance(lc, q) < lr + half + 1f) clear = false; }
                            if (clear && Vector2.Distance(end, to) > 2f && Vector2.Distance(end, to) < 40f)
                                Streets.Add(new Street { name = name + pieces + "のつなぎ", pts = Island.Resample(new[] { end, to }, 1f), half = half, level = level });
                        }
                    pieces++;
                }
                cur.Clear();
            }
            foreach (var p in pts) { if (Good(p)) cur.Add(p); else Flush(); }
            Flush();
            return pieces;
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
        public static void BuildStreets(Transform parent, Material paving, Material alley)
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
                    if (water)
                    {
                        if (waterStart < 0) waterStart = Mathf.Max(0, i - 1);
                    }
                    else if (waterStart >= 0)
                    {
                        TryBridge(g, s, waterStart, Mathf.Min(s.pts.Count - 1, i));
                        waterStart = -1;
                    }
                    if (!ok) { prevOk = false; continue; }
                    var dirv = (i < s.pts.Count - 1 ? s.pts[i + 1] - p : p - s.pts[i - 1]).normalized;
                    var side = new Vector2(-dirv.y, dirv.x) * s.half;
                    var l = p + side; var r = p - side;
                    float sy = s.level >= 0 ? Island.LevelTop(s.level) + 0.05f : H(p) + 0.05f;
                    v.Add(new Vector3(l.x, sy, l.y)); v.Add(new Vector3(r.x, sy, r.y));
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
                var go = MeshObj(g, s.name, m, Vector3.zero, s.half <= 2.0f ? alley : paving);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                si++;
            }
        }

        static readonly List<(Vector2 a, Vector2 b)> BridgesMade = new List<(Vector2, Vector2)>();

        /// <summary>通りが水を渡る所に橋をかける。まっすぐ渡れて、両岸の高さが同じで、近くに別の橋がないときだけ。</summary>
        static void TryBridge(Transform g, Street s, int i0, int i1)
        {
            var a = s.pts[i0]; var b = s.pts[i1];
            var dir = (b - a); float len = dir.magnitude;
            if (len < 0.5f) return;
            dir /= len;
            // 渡る水の流れの向き
            Island.Channel ch = null; float best = float.MaxValue;
            var mid = (a + b) / 2;
            foreach (var c in Island.Channels) { float d = Island.DistToChannel(c, mid.x, mid.y, out _); if (d < best) { best = d; ch = c; } }
            if (ch == null || best > ch.half + 1f) return; // 池などは渡らない
            var flow = Vector2.zero;
            for (int k = 0; k < ch.pts.Length - 1; k++)
            {
                var p0 = ch.pts[k]; var p1 = ch.pts[k + 1];
                var t = Mathf.Clamp01(Vector2.Dot(mid - p0, p1 - p0) / (p1 - p0).sqrMagnitude);
                if (Vector2.Distance(mid, Vector2.Lerp(p0, p1, t)) <= best + 0.01f) { flow = (p1 - p0).normalized; break; }
            }
            if (Mathf.Abs(Vector2.Dot(flow, dir)) > 0.92f) return;          // 川ぞいを走っている（渡っていない）
            a -= dir * 1.8f; b += dir * 1.8f;
            float ya = Island.SurfaceNoWater(a.x, a.y), yb = Island.SurfaceNoWater(b.x, b.y);
            if (Mathf.Abs(ya - yb) > 0.4f) return;                           // 滝のところ
            float span = Vector2.Distance(a, b);
            if (span < 3f || span > 26f) return;
            foreach (var (pa, pb) in BridgesMade) if (Vector2.Distance((pa + pb) / 2, (a + b) / 2) < 5f) return;
            BridgesMade.Add((a, b));
            float y = Mathf.Max(ya, yb);
            // 屋形船が通る水路は、船がくぐれる高い太鼓橋に
            float arch = ch.name == "下町の水路" ? Mathf.Max(2.6f, span * 0.2f) : Mathf.Min(1.5f, span * 0.1f);
            Bridge(g, s.name + "の橋", new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y), s.half * 2 + 0.6f, arch, 16);
            Reserve((a + b) / 2, span / 2 + 1f);
        }

        // ───────── 石段 ─────────

        public struct StairInfo { public bool ok; public Vector2 bottom, top; public float width; }
        public static readonly List<StairInfo> StairsList = new List<StairInfo>();
        /// <summary>名所の場所（路地が通らないように）。</summary>
        public static readonly List<(Vector2 c, float r)> Landmarks = new List<(Vector2, float)>();

        /// <summary>
        /// start から dir の方へ進んで、最初の崖に石段をかける。torii なら千本鳥居。
        /// 下と上におどり場をつけて、地面とすきまなくつなぐ。水にかかるなら作らない（ok = false）。
        /// </summary>
        public static StairInfo Stairs(Transform parent, string name, Vector2 start, Vector2 dir, float width, bool torii, Material stone)
        {
            dir.Normalize();
            var none = new StairInfo { ok = false, bottom = start, top = start, width = width };
            float h0 = Island.TerraceHeight(start.x, start.y);
            float tEdge = -1;
            for (float t = 0; t < 80; t += 0.1f)
            {
                var p = start + dir * t;
                if (Island.TerraceHeight(p.x, p.y) > h0 + 0.3f) { tEdge = t; break; }
            }
            if (tEdge < 0) { Debug.LogWarning("[Tsukuyomi] stairs: no cliff for " + name); return none; }
            var topP = start + dir * (tEdge + 4f);
            float h1 = Island.TerraceHeight(topP.x, topP.y);
            float rise = h1 - h0;
            if (rise < 2f) return none;
            int n = Mathf.CeilToInt(rise / 0.3f);
            const float run = 0.45f;
            float tTop = tEdge + 1.8f;                // いちばん上の段は、崖を上りきった平らな所にかかる
            float tBottom = tTop - n * run;
            var side = new Vector2(dir.y, -dir.x);
            // 水・島の外にかかるなら作らない
            for (float t = tBottom - 2f; t <= tTop + 2f; t += 1f)
                foreach (float sd in new[] { -1f, 0f, 1f })
                {
                    var q = start + dir * t + side * sd * (width / 2 + 0.8f);
                    if (Island.NearestWater(q.x, q.y) < 0.8f || Island.Inside(q.x, q.y) < 2f) { Debug.LogWarning("[Tsukuyomi] stairs over water: " + name); return none; }
                }
            var g = Group(parent, name, Vector3.zero);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            float bottomY = h0 - 1.2f;
            for (int i = 0; i < n; i++)
            {
                float top = h0 + (i + 1) * rise / n;
                float tc = tTop - (n - i) * run + run / 2;
                var c = start + dir * tc;
                Box(g, "段", new Vector3(c.x, (top + bottomY) / 2, c.y), new Vector3(width, top - bottomY, run + 0.03f), stone, true, new Vector3(0, yaw, 0));

                if (torii && i % 3 == 1)
                {
                    var tg = Group(g, "鳥居", new Vector3(c.x, top, c.y), yaw + 90);
                    MiniTorii(tg, width + 0.4f, 3.6f + width * 0.15f);
                }
                if (i % 4 == 0) Reserve(c, width / 2 + 0.9f);
            }
            // わきの壁（斜めの手すりつき）
            foreach (float sd in new[] { -1f, 1f })
            {
                var a0 = start + dir * tBottom + side * sd * (width / 2 + 0.35f);
                var a1 = start + dir * tTop + side * sd * (width / 2 + 0.35f);
                SideWall(g, a0, a1, h0, h1, bottomY, stone);
            }
            // 上のおどり場（平らな地面にしっかりのせる）
            var land = start + dir * (tTop + 1.2f);
            Box(g, "上のおどり場", new Vector3(land.x, (h1 + h1 - 1.5f) / 2 + 0.01f, land.y), new Vector3(width + 1.4f, 1.5f, 2.4f), stone, true, new Vector3(0, yaw, 0));
            // 下のおどり場
            var landB = start + dir * (tBottom - 1f);
            Box(g, "下のおどり場", new Vector3(landB.x, h0 - 0.55f + 0.01f, landB.y), new Vector3(width + 1.4f, 1.1f, 2f), stone, true, new Vector3(0, yaw, 0));
            var topC = start + dir * (tTop + 2.6f);
            var bottomC = start + dir * (tBottom - 2.2f);
            foreach (float sd in new[] { -1f, 1f })
            {
                var lb = landB + side * sd * (width / 2 + 1.3f);
                var lt = land + side * sd * (width / 2 + 1.3f);
                Toro(g, new Vector3(lb.x, h0, lb.y), sd > 0, 0.85f);
                Toro(g, new Vector3(lt.x, h1, lt.y), false, 0.85f);
            }
            Reserve(landB, width / 2 + 2f);
            Reserve(land, width / 2 + 2f);
            var info = new StairInfo { ok = true, bottom = start + dir * (tBottom - 2.2f), top = start + dir * (tTop + 2.4f), width = width + 1.4f };
            StairsList.Add(info);
            return new StairInfo { ok = true, bottom = bottomC, top = topC, width = width };
        }

        /// <summary>石段のわきの壁：下は地面、上は段にそって斜め（+0.8m の手すり）。</summary>
        static void SideWall(Transform g, Vector2 a, Vector2 b, float h0, float h1, float bottomY, Material m)
        {
            var dir = (b - a).normalized;
            var side = new Vector2(dir.y, -dir.x) * 0.35f;
            Vector3 P(Vector2 p, Vector2 off, float y) => new Vector3(p.x + off.x, y, p.y + off.y);
            float r0 = h0 + 0.8f, r1 = h1 + 0.8f;
            var v = new List<Vector3>(); var t = new List<int>();
            // 8つの角：左右（±side）× 下・上 × 始め・終わり
            var L0 = P(a, side, bottomY); var L1 = P(b, side, bottomY); var L2 = P(a, side, r0); var L3 = P(b, side, r1);
            var R0 = P(a, -side, bottomY); var R1 = P(b, -side, bottomY); var R2 = P(a, -side, r0); var R3 = P(b, -side, r1);
            void Quad(Vector3 q0, Vector3 q1, Vector3 q2, Vector3 q3)
            {
                // 表と裏で頂点を分ける（同じ頂点を両面で使うと法線が消えて、光が壊れる）
                int k = v.Count; v.AddRange(new[] { q0, q1, q2, q3 });
                t.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                k = v.Count; v.AddRange(new[] { q0, q1, q2, q3 });
                t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }
            Quad(L0, L1, L2, L3); Quad(R0, R1, R2, R3); Quad(L2, L3, R2, R3); Quad(L0, L2, R0, R2); Quad(L1, L3, R1, R3);
            var mesh = new Mesh { name = "stair_side" };
            mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("袖");
            go.transform.SetParent(g, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Box(g, "手すり", (P(a, Vector2.zero, r0 + 0.08f) + P(b, Vector2.zero, r1 + 0.08f)) / 2 + new Vector3(side.x, 0, side.y) * 0f,
                new Vector3(0.8f, 0.16f, Vector3.Distance(P(a, Vector2.zero, r0), P(b, Vector2.zero, r1))), Vermilion, false,
                new Vector3(-Mathf.Atan2(r1 - r0, Vector2.Distance(a, b)) * Mathf.Rad2Deg, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0));
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
