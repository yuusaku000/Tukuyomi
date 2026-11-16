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

        public static void Build(Transform env, Transform decoRoot)
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
                    if (acc < 8f) continue;
                    acc = 0;
                    var c = s.pts[i];
                    if (Island.NearestWater(c.x, c.y) < 2f || Island.Inside(c.x, c.y) < 4f) continue;
                    bool near = false;
                    foreach (var u in used) if ((u - c).sqrMagnitude < 7f * 7f) { near = true; break; }
                    if (near) continue;
                    var d = (s.pts[i + 1] - s.pts[i - 1]).normalized;
                    var side = new Vector3(-d.y, 0, d.x);
                    float gy = Island.Height(c.x, c.y);
                    if (Physics.Raycast(new Vector3(c.x, gy + 30f, c.y), Vector3.down, out var gh, 60f, ~0, QueryTriggerInteraction.Ignore)) gy = gh.point.y;
                    float hy = gy + R(5.2f, 6.2f);
                    var mid = new Vector3(c.x, hy, c.y);
                    bool wa = Wall(mid, side, s.half + 7f, out var a), wb = Wall(mid, -side, s.half + 7f, out var b);
                    // 片がわだけ壁なら、もう片がわの道ばたに柱を立てて綱をかける
                    if (wa != wb)
                    {
                        float sd = wa ? -1f : 1f;
                        var pp = c + new Vector2(side.x, side.z) * sd * (s.half + 0.5f);
                        float py = Island.Height(pp.x, pp.y);
                        if (!Physics.CheckSphere(new Vector3(pp.x, py + 1.5f, pp.y), 0.4f, ~0, QueryTriggerInteraction.Ignore) && Island.NearestWater(pp.x, pp.y) > 1.5f)
                        {
                            float ph = hy - py + 0.3f;
                            Cyl(g, "飾りの柱", new Vector3(pp.x, py + ph / 2, pp.y), 0.09f, ph, Vermilion, true);
                            Ball(g, "柱の玉", new Vector3(pp.x, py + ph + 0.15f, pp.y), Vector3.one * 0.3f, GlowGold);
                            var pole = new Vector3(pp.x, hy, pp.y);
                            if (wa) b = pole; else a = pole;
                            wa = wb = true;
                        }
                    }
                    if (wa && wb && Vector3.Distance(a, b) < 22f)
                    {
                        used.Add(c);
                        int kind = deco % 6;
                        if (kind == 0) Drape(g, a, b);
                        else if (kind == 1) Streamers(g, a, b);
                        else if (kind == 2) Umbrellas(g, a, b);
                        else if (kind == 3) KingyoRope(g, a, b);
                        else if (kind == 4) FloatLanterns(g, a, b); // 動かさずに町といっしょにまとめる（軽くするため）
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
            int fronts = ShopFronts(g);
            int bon = Bonbori(g);
            Rng = keep;
            Debug.Log($"[Tsukuyomi] glitter: {deco} street decorations, {trees} trees, {fronts} lit shop fronts, {bon} bonbori");
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

        static readonly Color[] Neon =
        {
            new Color(1f, 0.35f, 0.75f), new Color(0.3f, 0.95f, 1f), new Color(0.7f, 0.45f, 1f), new Color(1f, 0.75f, 0.3f), new Color(0.45f, 1f, 0.6f),
        };

        /// <summary>光る金魚ちょうちん（赤と橙の金魚の形）。</summary>
        static void KingyoRope(Transform g, Vector3 a, Vector3 b)
        {
            float sag = Vector3.Distance(a, b) * 0.06f;
            Rope(g, a, b, sag, Black, 0.04f);
            var red = Glow("kingyo_red", new Color(1f, 0.3f, 0.2f), 0.8f, 3f);
            var org = Glow("kingyo_orange", new Color(1f, 0.6f, 0.2f), 0.8f, 3f);
            int n = Mathf.Max(3, Mathf.RoundToInt(Vector3.Distance(a, b) / 1.8f));
            var dir = (b - a).normalized;
            for (int i = 1; i < n; i++)
            {
                var p = Sag(a, b, i / (float)n, sag) + Vector3.down * 0.55f;
                var f = Group(g, "金魚ちょうちん", p, 0);
                f.rotation = Quaternion.LookRotation(Quaternion.Euler(0, i % 2 == 0 ? 90 : -90, 0) * dir);
                var m = i % 2 == 0 ? red : org;
                Ball(f, "体", Vector3.zero, new Vector3(0.38f, 0.42f, 0.6f), m);
                var tail = Box(f, "尾", new Vector3(0, 0.02f, -0.38f), new Vector3(0.03f, 0.4f, 0.3f), m);
                tail.transform.localRotation = Quaternion.Euler(45, 0, 0);
                Box(f, "背びれ", new Vector3(0, 0.22f, -0.05f), new Vector3(0.03f, 0.12f, 0.22f), m);
                foreach (float sx in new[] { -1f, 1f }) Ball(f, "目", new Vector3(sx * 0.13f, 0.06f, 0.22f), Vector3.one * 0.07f, Black);
            }
        }

        /// <summary>通りの上に浮かぶ灯籠（ゆっくり上下する。動くので deco に）。</summary>
        static void FloatLanterns(Transform deco, Vector3 a, Vector3 b)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(a, b) / 2.5f), 2, 6);
            for (int i = 0; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, (i + 0.5f) / n) + Vector3.up * R(0.8f, 2.6f);
                var l = Group(deco, "浮かぶ灯籠", p, R(0, 90));
                Box(l, "灯", Vector3.zero, new Vector3(0.55f, 0.75f, 0.55f), i % 3 == 0 ? GlowPink : GlowWarm);
                Box(l, "ふち", new Vector3(0, 0.4f, 0), new Vector3(0.62f, 0.06f, 0.62f), WoodDark);
                Box(l, "ふち", new Vector3(0, -0.4f, 0), new Vector3(0.62f, 0.06f, 0.62f), WoodDark);
                R(0f, 1f); R(0f, 1f); R(0f, 1f); // 乱数の進み方を前と同じにする
            }
        }

        /// <summary>店の正面：軒下のネオン管・光の暖簾・光る格子看板（どれか）。</summary>
        static int ShopFronts(Transform g)
        {
            int n = 0, i = 0;
            foreach (var lot in Town.Lots)
            {
                if (!lot.facing) continue;
                i++;
                var rot = Quaternion.Euler(0, lot.rotY, 0);
                Vector3 At(float x, float y, float z) { var v = lot.pos + rot * new Vector3(x, 0, z); v.y = lot.pos.y + y; return v; }
                int kind = i % 5;
                var col = Glow("neon_tube_" + (i % Neon.Length), Neon[i % Neon.Length], 1.3f, 4f);
                if (kind == 0 || kind == 3)
                {
                    var tube = Box(g, "ネオン管", At(0, 4.1f, lot.d / 2 + 0.35f), new Vector3(lot.w - 0.6f, 0.06f, 0.06f), col);
                    tube.transform.rotation = rot;
                    if (kind == 3)
                        for (int k = 0; k < 8; k++)
                        {
                            float t = k / 7f;
                            var seg = Box(g, "ネオンの波", At(-lot.w / 2 + 0.6f + t * (lot.w - 1.2f), 3.85f + Mathf.Sin(t * Mathf.PI * 4) * 0.12f, lot.d / 2 + 0.36f), new Vector3((lot.w - 1.2f) / 8f + 0.05f, 0.05f, 0.05f), col);
                            seg.transform.rotation = rot * Quaternion.Euler(0, 0, Mathf.Cos(t * Mathf.PI * 4) * 25f);
                        }
                    n++;
                }
                else if (kind == 1)
                {
                    int pieces = Mathf.Max(2, Mathf.RoundToInt((lot.w - 2f) / 1.1f));
                    for (int k = 0; k < pieces; k++)
                    {
                        float x = -lot.w / 2 + 1f + (lot.w - 2f) * (k + 0.5f) / pieces;
                        var nr = Box(g, "光の暖簾", At(x, 3.45f, lot.d / 2 + 0.25f), new Vector3((lot.w - 2f) / pieces - 0.08f, 0.9f, 0.03f), Glow("glow_noren_" + ((i + k) % 3), Neon[(i + k) % 3], 0.6f, 1.8f));
                        nr.transform.rotation = rot;
                    }
                    n++;
                }
                else if (kind == 2 && lot.w >= 5f)
                {
                    float sx = (i % 2 == 0 ? -1 : 1) * (lot.w / 2 - 1.1f);
                    var panel = Box(g, "格子の看板", At(sx, 5.3f, lot.d / 2 + 0.45f), new Vector3(1.4f, 1.0f, 0.05f), col);
                    panel.transform.rotation = rot;
                    for (int k = 0; k < 5; k++)
                    {
                        var v = Box(g, "格子", At(sx - 0.56f + k * 0.28f, 5.3f, lot.d / 2 + 0.49f), new Vector3(0.04f, 1.0f, 0.03f), Black); v.transform.rotation = rot;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        var h = Box(g, "格子", At(sx, 4.95f + k * 0.35f, lot.d / 2 + 0.49f), new Vector3(1.4f, 0.04f, 0.03f), Black); h.transform.rotation = rot;
                    }
                    n++;
                }
            }
            return n;
        }

        /// <summary>道ばたのぼんぼりの列（建物や木とぶつからない所だけ）。</summary>
        static int Bonbori(Transform g)
        {
            int n = 0;
            var paper = Glow("bonbori_paper", new Color(1f, 0.85f, 0.75f), 0.7f, 2.4f);
            var pink = Glow("bonbori_pink", new Color(1f, 0.6f, 0.75f), 0.7f, 2.4f);
            foreach (var s in Town.Streets)
            {
                if (s.half < 1.6f || s.level == -1) continue;
                float acc = 0;
                for (int i = 1; i < s.pts.Count - 1; i++)
                {
                    acc += Vector2.Distance(s.pts[i - 1], s.pts[i]);
                    if (acc < 6f) continue;
                    acc = 0;
                    var d = (s.pts[i + 1] - s.pts[i - 1]).normalized;
                    var side = new Vector2(-d.y, d.x) * ((i / 3) % 2 == 0 ? 1 : -1);
                    var p = s.pts[i] + side * (s.half + 0.35f);
                    if (Island.NearestWater(p.x, p.y) < 1.2f || TerrainBuilder.NearStairs(p, 0.6f)) continue;
                    float y = Island.Height(p.x, p.y);
                    if (Mathf.Abs(y - Island.TerraceHeight(p.x, p.y)) > 0.3f) continue;
                    if (Physics.CheckBox(new Vector3(p.x, y + 1f, p.y), new Vector3(0.3f, 0.8f, 0.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var b = Group(g, "ぼんぼり", new Vector3(p.x, y, p.y));
                    Cyl(b, "柄", new Vector3(0, 0.7f, 0), 0.04f, 1.4f, Black);
                    Cyl(b, "火袋", new Vector3(0, 1.6f, 0), 0.2f, 0.5f, n % 4 == 0 ? pink : paper);
                    Cyl(b, "笠", new Vector3(0, 1.88f, 0), 0.24f, 0.06f, Black);
                    n++;
                    if (n > 260) return n;
                }
            }
            return n;
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
