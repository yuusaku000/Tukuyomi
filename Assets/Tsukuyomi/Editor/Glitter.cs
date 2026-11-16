using System.Collections.Generic;
using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 町をきらびやかにする（公式の絵のような、通りの上の飾り）と、町なかの松と桜。
    ///   通りの上：両がわの建物の壁から壁へ、反物の飾り幕・吹き流し・さかさの和傘・提灯の綱を交互にわたす
    ///   木：通りのわきの空いた所に、松と小さな桜
    /// 町の並びが変わらないように、自分専用の乱数を使う。
    /// </summary>
    public static class Glitter
    {
        static readonly Color[] Cloth =
        {
            new Color(0.95f, 0.25f, 0.45f), new Color(1f, 0.72f, 0.25f), new Color(0.98f, 0.45f, 0.7f), new Color(0.35f, 0.8f, 0.85f),
            new Color(0.6f, 0.4f, 0.9f), new Color(0.95f, 0.92f, 0.85f), new Color(0.85f, 0.15f, 0.15f),
        };

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>まわりの建物の壁（横向きの光線が当たった所）。</summary>
        static bool Wall(Vector3 from, Vector3 dir, float max, out Vector3 at)
        {
            at = default;
            if (!Physics.Raycast(from, dir, out var hit, max, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.normal.y) > 0.4f) return false;
            at = hit.point - dir * 0.05f;
            return true;
        }

        public static void Build(Transform env)
        {
            var keep = Rng;
            Rng = new System.Random(9100);
            Physics.SyncTransforms();
            var g = Group(env, "通りの飾り", Vector3.zero);
            int deco = 0, trees = 0;
            var used = new List<Vector2>();
            foreach (var s in Town.Streets)
            {
                if (s.half < 1.6f || s.pts.Count < 4) continue;
                float acc = 0;
                for (int i = 1; i < s.pts.Count - 1; i++)
                {
                    acc += Vector2.Distance(s.pts[i - 1], s.pts[i]);
                    if (acc < 11f) continue;
                    acc = 0;
                    var c = s.pts[i];
                    if (Island.NearestWater(c.x, c.y) < 2f || Island.Inside(c.x, c.y) < 4f) continue;
                    bool near = false;
                    foreach (var u in used) if ((u - c).sqrMagnitude < 9f * 9f) { near = true; break; }
                    if (near) continue;
                    var d = (s.pts[i + 1] - s.pts[i - 1]).normalized;
                    var side = new Vector3(-d.y, 0, d.x);
                    float gy = Island.Height(c.x, c.y);
                    if (Physics.Raycast(new Vector3(c.x, gy + 30f, c.y), Vector3.down, out var gh, 60f, ~0, QueryTriggerInteraction.Ignore)) gy = gh.point.y;
                    float hy = gy + R(5.2f, 6.2f);
                    var mid = new Vector3(c.x, hy, c.y);
                    if (Wall(mid, side, s.half + 7f, out var a) && Wall(mid, -side, s.half + 7f, out var b) && Vector3.Distance(a, b) < 22f)
                    {
                        used.Add(c);
                        int kind = deco % 4;
                        if (kind == 0) Drape(g, a, b);
                        else if (kind == 1) Streamers(g, a, b);
                        else if (kind == 2) Umbrellas(g, a, b);
                        else LanternRope(g, a, b);
                        deco++;
                    }
                    else if (trees < 70 && s.level != -1)
                    {
                        // 壁がない所（片がわが空いている）には木を植える
                        foreach (float sd in new[] { 1f, -1f })
                        {
                            var p = c + new Vector2(side.x, side.z) * sd * (s.half + 2.4f);
                            if (Town.OnAnyStreet(p, 1.2f) || !Town.Free(p, 1.8f) || Island.NearestWater(p.x, p.y) < 2.5f || TerrainBuilder.NearStairs(p, 1.5f)) continue;
                            float py = Island.Height(p.x, p.y);
                            if (Mathf.Abs(py - Island.TerraceHeight(p.x, p.y)) > 0.3f) continue;
                            if (Physics.CheckBox(new Vector3(p.x, py + 2.5f, p.y), new Vector3(1.6f, 2f, 1.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                            if (trees % 3 == 1) Sakura(g, new Vector3(p.x, py, p.y), R(0.32f, 0.42f), trees % 2 == 0);
                            else Pine(g, new Vector3(p.x, py, p.y), R(0.8f, 1.15f));
                            Town.Reserve(p, 1.8f);
                            used.Add(c);
                            trees++;
                            break;
                        }
                    }
                }
            }
            Rng = keep;
            Debug.Log($"[Tsukuyomi] glitter: {deco} street decorations, {trees} trees");
        }

        /// <summary>a から b へ、たるんだ線の上の点（k = 0〜1）。</summary>
        static Vector3 Sag(Vector3 a, Vector3 b, float k, float sag) => Vector3.Lerp(a, b, k) + Vector3.down * sag * 4f * k * (1 - k);

        static void Rope(Transform g, Vector3 a, Vector3 b, float sag, Material m, float thick = 0.05f)
        {
            const int n = 8;
            for (int i = 0; i < n; i++)
            {
                var p0 = Sag(a, b, i / (float)n, sag); var p1 = Sag(a, b, (i + 1) / (float)n, sag);
                var seg = Box(g, "綱", (p0 + p1) / 2, new Vector3(thick, thick, Vector3.Distance(p0, p1) + 0.02f), m);
                seg.transform.rotation = Quaternion.LookRotation(p1 - p0);
            }
        }

        /// <summary>反物の飾り幕（ゆるく垂れた幅の広い布。2色のしま）。</summary>
        static void Drape(Transform g, Vector3 a, Vector3 b)
        {
            var c1 = ColorMat(Cloth[Ri(0, Cloth.Length)]);
            var c2 = Glow("drape_glow_" + Ri(0, 3), Cloth[Ri(0, 4)], 0.5f, 1.4f);
            const int n = 14;
            float sag = Vector3.Distance(a, b) * 0.08f;
            for (int i = 0; i < n; i++)
            {
                var p0 = Sag(a, b, i / (float)n, sag); var p1 = Sag(a, b, (i + 1) / (float)n, sag);
                var seg = Box(g, "飾り幕", (p0 + p1) / 2 + Vector3.down * 0.35f, new Vector3(0.03f, 0.8f, Vector3.Distance(p0, p1) + 0.03f), i % 3 == 1 ? c2 : c1);
                seg.transform.rotation = Quaternion.LookRotation(p1 - p0);
            }
        }

        /// <summary>吹き流し：綱から色とりどりの細い布が下がる。</summary>
        static void Streamers(Transform g, Vector3 a, Vector3 b)
        {
            float sag = Vector3.Distance(a, b) * 0.06f;
            Rope(g, a, b, sag, Black, 0.04f);
            int n = Mathf.Max(5, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.9f));
            for (int i = 1; i < n; i++)
            {
                var p = Sag(a, b, i / (float)n, sag);
                float len = R(1.0f, 2.2f);
                var m = i % 4 == 0 ? Glow("streamer_glow", new Color(1f, 0.55f, 0.8f), 0.6f, 1.8f) : ColorMat(Cloth[(i + Ri(0, 2)) % Cloth.Length]);
                var strip = Box(g, "吹き流し", p + Vector3.down * len / 2, new Vector3(0.22f, len, 0.02f), m);
                strip.transform.rotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(0, 90, 0);
            }
        }

        /// <summary>さかさにつるした和傘（ひとつは光る）。</summary>
        static void Umbrellas(Transform g, Vector3 a, Vector3 b)
        {
            float sag = Vector3.Distance(a, b) * 0.05f;
            Rope(g, a, b, sag, Black, 0.04f);
            int n = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(a, b) / 3f), 2, 5);
            for (int i = 0; i < n; i++)
            {
                var p = Sag(a, b, (i + 0.5f) / n, sag) + Vector3.down * R(0.6f, 1.2f);
                var u = Group(g, "和傘", p, R(0, 360));
                u.localRotation = Quaternion.Euler(R(150f, 170f), R(0, 360), 0);
                var cone = MeshObj(u, "傘", Doll.Cone, Vector3.zero, i == n / 2 ? GlowSakura : ColorMat(Cloth[Ri(0, Cloth.Length)]));
                cone.transform.localScale = new Vector3(2.6f, 0.7f, 2.6f);
                Cyl(u, "柄", new Vector3(0, 0.5f, 0), 0.03f, 1.2f, WoodDark);
            }
        }

        /// <summary>提灯の綱（赤と白をかわりばんこ）。</summary>
        static void LanternRope(Transform g, Vector3 a, Vector3 b)
        {
            float sag = Vector3.Distance(a, b) * 0.07f;
            Rope(g, a, b, sag, Black, 0.04f);
            int n = Mathf.Max(4, Mathf.RoundToInt(Vector3.Distance(a, b) / 1.5f));
            for (int i = 1; i < n; i++)
                Chochin(g, Sag(a, b, i / (float)n, sag) + Vector3.down * 0.4f, i % 2 == 0 ? GlowRed : GlowWhite);
        }
    }
}
