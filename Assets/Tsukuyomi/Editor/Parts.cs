using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;

namespace Tsukuyomi.EditorTools
{
    /// <summary>平安ふうの建物・鳥居・橋・灯籠・木などの部品。</summary>
    public static class Parts
    {
        public static System.Random Rng = new System.Random(8000);
        public static float R(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
        public static int Ri(int a, int b) => Rng.Next(a, b);

        // ───── 色 ─────
        public static Material Vermilion => Lit("vermilion", new Color(0.82f, 0.2f, 0.11f), 0.35f);
        public static Material WhiteWall => Lit("wall_white", new Color(0.93f, 0.91f, 0.86f), 0.1f);
        public static Material WoodDark => Lit("wood_dark", new Color(0.2f, 0.13f, 0.09f), 0.2f);
        public static Material Wood => Lit("wood", new Color(0.48f, 0.32f, 0.2f), 0.2f);
        public static Material Black => Lit("lacquer_black", new Color(0.05f, 0.05f, 0.06f), 0.6f);
        public static Material Gold => Lit("gold", new Color(1f, 0.78f, 0.35f), 0.75f, 1f);
        public static Material Stone => Lit("stone", new Color(0.56f, 0.55f, 0.52f), 0.15f);
        public static Material Paving => Lit("paving", new Color(0.6f, 0.58f, 0.55f), 0.2f);
        public static Material Sand => Lit("sand_white", new Color(0.86f, 0.83f, 0.75f), 0.1f);
        public static Material Grass => Lit("grass", new Color(0.33f, 0.5f, 0.27f), 0.1f);
        public static Material RoofBark => Lit("roof_bark", new Color(0.33f, 0.21f, 0.15f), 0.2f, 0f, true);
        public static Material RoofTile => Lit("roof_tile", new Color(0.2f, 0.22f, 0.27f), 0.45f, 0f, true);
        public static Material RoofCopper => Lit("roof_copper", new Color(0.33f, 0.62f, 0.55f), 0.45f, 0.3f, true);
        public static Material Lattice => Lit("lattice", new Color(0.27f, 0.17f, 0.11f), 0.2f);
        public static Material Paper => Lit("paper", new Color(0.96f, 0.93f, 0.85f), 0.05f);
        public static Material GlowWarm => Glow("glow_warm", new Color(1f, 0.62f, 0.3f), 0.8f, 3.2f);
        public static Material GlowRed => Glow("glow_red", new Color(1f, 0.25f, 0.18f), 0.7f, 3.0f);
        public static Material GlowWhite => Glow("glow_white", new Color(1f, 0.92f, 0.8f), 0.8f, 2.6f);
        public static Material GlowCyan => Glow("glow_cyan", new Color(0.3f, 0.9f, 1f), 0.9f, 3.5f, 1f);
        public static Material GlowPink => Glow("glow_pink", new Color(1f, 0.45f, 0.75f), 0.9f, 3.5f, 1f);
        public static Material GlowGold => Glow("glow_gold", new Color(1f, 0.8f, 0.35f), 0.8f, 3.0f);
        public static Material GlowSkyLantern => Glow("glow_sky_lantern", new Color(1f, 0.6f, 0.28f), 0.18f, 3.4f);
        public static Material GlowSkyPink => Glow("glow_sky_pink", new Color(1f, 0.45f, 0.75f), 0.18f, 3.4f);
        public static Material GlowSakura => Glow("glow_sakura", new Color(1f, 0.7f, 0.82f), 0.6f, 1.6f);

        public static readonly Color[] NorenColors =
        {
            new Color(0.12f, 0.2f, 0.45f), new Color(0.6f, 0.12f, 0.15f), new Color(0.15f, 0.35f, 0.25f),
            new Color(0.45f, 0.2f, 0.45f), new Color(0.85f, 0.85f, 0.8f), new Color(0.7f, 0.45f, 0.15f),
        };

        static Material RoofMat(int kind) => kind == 0 ? RoofBark : kind == 1 ? RoofTile : RoofCopper;

        /// <summary>
        /// 平安ふうの建物。pos は地面の高さ、正面はローカルの +z。shop なら正面に縁側・暖簾・品物の台。
        /// </summary>
        public static Transform Building(Transform parent, string name, Vector3 pos, float rotY, float w, float d, float h,
            int roofKind, bool shop, bool twoStory = false, Color? noren = null)
        {
            var g = Group(parent, name, pos, rotY);
            Box(g, "基壇", new Vector3(0, 0.25f, 0), new Vector3(w + 0.8f, 0.5f, d + 0.8f), Stone, true);
            Box(g, "床", new Vector3(0, 0.56f, 0), new Vector3(w, 0.12f, d), WoodDark);
            float y0 = 0.62f;
            float front = shop ? 1.7f : 0.45f;
            float wallD = d - front - 0.3f;
            float wallZ = (-d / 2 + 0.15f + d / 2 - front) / 2;
            Box(g, "壁", new Vector3(0, y0 + h / 2, wallZ), new Vector3(w - 0.3f, h, wallD), WhiteWall, true);
            // 正面の格子（蔀戸）と腰の板
            Box(g, "格子", new Vector3(0, y0 + h * 0.62f, wallZ + wallD / 2 + 0.03f), new Vector3(w - 0.6f, h * 0.5f, 0.06f), Lattice);
            Box(g, "腰板", new Vector3(0, y0 + h * 0.16f, wallZ + wallD / 2 + 0.03f), new Vector3(w - 0.6f, h * 0.3f, 0.06f), WoodDark);
            for (int i = 1; i < 4; i++)
                Box(g, "格子線", new Vector3(0, y0 + h * (0.37f + 0.125f * i), wallZ + wallD / 2 + 0.07f), new Vector3(w - 0.6f, 0.05f, 0.03f), Paper);

            // 柱（朱）
            int nx = Mathf.Max(2, Mathf.RoundToInt(w / 2.6f) + 1);
            for (int i = 0; i < nx; i++)
            {
                float x = -w / 2 + 0.2f + i * (w - 0.4f) / (nx - 1);
                Cyl(g, "柱", new Vector3(x, y0 + h / 2, d / 2 - 0.2f), 0.15f, h, Vermilion);
                Cyl(g, "柱", new Vector3(x, y0 + h / 2, -d / 2 + 0.2f), 0.15f, h, Vermilion);
            }
            Box(g, "梁", new Vector3(0, y0 + h - 0.12f, d / 2 - 0.2f), new Vector3(w + 0.3f, 0.32f, 0.34f), Vermilion);
            Box(g, "梁", new Vector3(0, y0 + h - 0.12f, -d / 2 + 0.2f), new Vector3(w + 0.3f, 0.32f, 0.34f), Vermilion);
            Box(g, "長押", new Vector3(0, y0 + h * 0.42f, d / 2 - 0.2f), new Vector3(w + 0.1f, 0.14f, 0.3f), Vermilion);

            float roofY = y0 + h + 0.02f;
            float rw = w + 2.4f, rd = d + 2.4f;
            float rh = Mathf.Min(w, d) * (twoStory ? 0.22f : 0.42f);
            MeshObj(g, "屋根", Roof(rw, rd, rh, 0.75f), new Vector3(0, roofY, 0), RoofMat(roofKind));
            if (!twoStory) Ridge(g, roofY + rh, w, d);

            if (twoStory)
            {
                float w2 = w - 1.6f, d2 = d - 1.6f, h2 = h * 0.85f, y2 = roofY + rh * 0.8f;
                Box(g, "二階", new Vector3(0, y2 + h2 / 2, 0), new Vector3(w2, h2, d2), WhiteWall, true);
                Box(g, "二階格子", new Vector3(0, y2 + h2 * 0.55f, d2 / 2 + 0.03f), new Vector3(w2 - 0.6f, h2 * 0.55f, 0.06f), Lattice);
                Box(g, "高欄", new Vector3(0, y2 + 0.5f, d2 / 2 + 0.6f), new Vector3(w2 + 0.6f, 0.12f, 0.12f), Vermilion);
                Box(g, "縁", new Vector3(0, y2 + 0.02f, 0), new Vector3(w2 + 1.4f, 0.14f, d2 + 1.4f), WoodDark);
                for (int i = 0; i < 2; i++)
                    for (int k = 0; k < 2; k++)
                        Cyl(g, "柱", new Vector3((i == 0 ? -1 : 1) * (w2 / 2 - 0.1f), y2 + h2 / 2, (k == 0 ? -1 : 1) * (d2 / 2 - 0.1f)), 0.14f, h2, Vermilion);
                Box(g, "梁", new Vector3(0, y2 + h2 - 0.1f, d2 / 2), new Vector3(w2 + 0.3f, 0.3f, 0.3f), Vermilion);
                float rh2 = Mathf.Min(w2, d2) * 0.45f;
                MeshObj(g, "屋根2", Roof(w2 + 2.6f, d2 + 2.6f, rh2, 0.8f), new Vector3(0, y2 + h2 + 0.02f, 0), RoofMat(roofKind));
                Ridge(g, y2 + h2 + 0.02f + rh2, w2, d2);
            }

            if (shop)
            {
                var nc = noren ?? NorenColors[Ri(0, NorenColors.Length)];
                var nm = ColorMat(nc);
                int pieces = Mathf.Max(2, Mathf.RoundToInt((w - 1.6f) / 1.1f));
                float pw = (w - 1.6f) / pieces;
                for (int i = 0; i < pieces; i++)
                    Box(g, "暖簾", new Vector3(-w / 2 + 0.8f + pw * (i + 0.5f), y0 + h - 0.75f, d / 2 - 0.05f), new Vector3(pw - 0.06f, 0.95f, 0.03f), nm);
                Box(g, "台", new Vector3(0, y0 + 0.45f, wallZ + wallD / 2 + 0.6f), new Vector3(w - 1.6f, 0.9f, 0.8f), Wood);
                int goods = Ri(3, 7);
                for (int i = 0; i < goods; i++)
                {
                    var c = Color.HSVToRGB(R(0f, 1f), R(0.3f, 0.8f), R(0.6f, 1f));
                    float gx = -w / 2 + 1.2f + (w - 2.4f) * (i + 0.5f) / goods;
                    if (Rng.NextDouble() < 0.5)
                        Box(g, "品物", new Vector3(gx, y0 + 1.05f, wallZ + wallD / 2 + 0.6f), new Vector3(R(0.25f, 0.5f), R(0.2f, 0.4f), R(0.25f, 0.4f)), ColorMat(c), false, new Vector3(0, R(-20, 20), 0));
                    else
                        Ball(g, "品物", new Vector3(gx, y0 + 1.08f, wallZ + wallD / 2 + 0.6f), Vector3.one * R(0.22f, 0.36f), ColorMat(c));
                }
                // 提灯
                Chochin(g, new Vector3(-w / 2 + 0.3f, y0 + h - 0.85f, d / 2 + 0.25f), Rng.NextDouble() < 0.5 ? GlowRed : GlowWhite);
                Chochin(g, new Vector3(w / 2 - 0.3f, y0 + h - 0.85f, d / 2 + 0.25f), Rng.NextDouble() < 0.5 ? GlowRed : GlowWhite);
            }
            return g;
        }

        static void Ridge(Transform g, float y, float w, float d)
        {
            bool alongX = w >= d;
            float len = Mathf.Abs(w - d) + 0.8f;
            var size = alongX ? new Vector3(len, 0.32f, 0.38f) : new Vector3(0.38f, 0.32f, len);
            Box(g, "棟", new Vector3(0, y + 0.08f, 0), size, Black);
            var end = alongX ? new Vector3(len / 2, 0, 0) : new Vector3(0, 0, len / 2);
            Box(g, "鴟尾", new Vector3(end.x, y + 0.4f, end.z), new Vector3(0.3f, 0.6f, 0.3f), Gold, false, new Vector3(0, 0, alongX ? -12 : 0));
            Box(g, "鴟尾", new Vector3(-end.x, y + 0.4f, -end.z), new Vector3(0.3f, 0.6f, 0.3f), Gold, false, new Vector3(0, 0, alongX ? 12 : 0));
        }

        public static void Chochin(Transform p, Vector3 pos, Material m)
        {
            var g = Group(p, "提灯", pos);
            Ball(g, "火袋", Vector3.zero, new Vector3(0.42f, 0.58f, 0.42f), m);
            Cyl(g, "上", new Vector3(0, 0.3f, 0), 0.13f, 0.06f, Black);
            Cyl(g, "下", new Vector3(0, -0.3f, 0), 0.13f, 0.06f, Black);
        }

        /// <summary>石灯籠。light なら夜だけ周りを照らす。</summary>
        public static void Toro(Transform p, Vector3 pos, bool light, float scale = 1f)
        {
            var g = Group(p, "石灯籠", pos);
            g.localScale = Vector3.one * scale;
            Cyl(g, "台", new Vector3(0, 0.12f, 0), 0.36f, 0.24f, Stone, true);
            Cyl(g, "竿", new Vector3(0, 0.75f, 0), 0.13f, 1.1f, Stone);
            Box(g, "中台", new Vector3(0, 1.35f, 0), new Vector3(0.55f, 0.14f, 0.55f), Stone);
            Box(g, "火袋", new Vector3(0, 1.62f, 0), new Vector3(0.38f, 0.42f, 0.38f), GlowWarm);
            MeshObj(g, "笠", Roof(0.95f, 0.95f, 0.32f, 0.08f), new Vector3(0, 1.84f, 0), Stone);
            Ball(g, "宝珠", new Vector3(0, 2.22f, 0), Vector3.one * 0.16f, Stone);
            if (light) Lamp(g, new Vector3(0, 1.7f, 0), new Color(1f, 0.7f, 0.4f), 9f, 2.2f);
        }

        public static void Lamp(Transform p, Vector3 pos, Color c, float range, float intensity)
        {
            var go = new GameObject("夜の明かり");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.AddComponent<NightLamp>();
        }

        /// <summary>大鳥居（両部鳥居）。pos は水面、s は大きさ。</summary>
        public static Transform Torii(Transform p, Vector3 pos, float s)
        {
            var g = Group(p, "大鳥居", pos);
            float H = 16f * s;
            foreach (float x in new[] { -7f * s, 7f * s })
            {
                Cyl(g, "主柱", new Vector3(x, H / 2 - 1.5f, 0), 0.95f * s, H + 3f, Vermilion, true);
                Cyl(g, "根巻", new Vector3(x, 0.3f, 0), 1.05f * s, 1.4f, Black);
                foreach (float z in new[] { -4.6f * s, 4.6f * s })
                {
                    Cyl(g, "控柱", new Vector3(x, 4.5f * s - 1.5f, z), 0.5f * s, 9f * s + 3f, Vermilion, true);
                    Cyl(g, "控根巻", new Vector3(x, 0.2f, z), 0.58f * s, 1.2f, Black);
                }
                Box(g, "控貫", new Vector3(x, 6.4f * s, 0), new Vector3(0.6f * s, 0.7f * s, 9.6f * s), Vermilion);
                Box(g, "控貫", new Vector3(x, 2.2f * s, 0), new Vector3(0.5f * s, 0.6f * s, 9.6f * s), Vermilion);
                Box(g, "控屋根", new Vector3(x, 8.0f * s, 0), new Vector3(1.6f * s, 0.4f * s, 10.6f * s), Black);
            }
            Box(g, "貫", new Vector3(0, 11.2f * s, 0), new Vector3(19.5f * s, 1.0f * s, 0.8f * s), Vermilion);
            Box(g, "島木", new Vector3(0, 14.5f * s, 0), new Vector3(19f * s, 0.9f * s, 1.3f * s), Vermilion);
            Box(g, "笠木", new Vector3(0, 15.45f * s, 0), new Vector3(17f * s, 1.0f * s, 1.6f * s), Black);
            foreach (float side in new[] { -1f, 1f })
                Box(g, "笠木端", new Vector3(side * 10.4f * s, 15.75f * s, 0), new Vector3(5.2f * s, 1.0f * s, 1.6f * s), Black, false, new Vector3(0, 0, side * 8f));
            Box(g, "額束", new Vector3(0, 12.85f * s, 0), new Vector3(0.9f * s, 2.4f * s, 0.6f * s), Vermilion);
            Box(g, "扁額", new Vector3(0, 12.9f * s, 0), new Vector3(2.4f * s, 3.0f * s, 0.9f * s), Black);
            Box(g, "扁額金", new Vector3(0, 12.9f * s, -0.47f * s), new Vector3(1.9f * s, 2.5f * s, 0.05f), GlowGold);
            Box(g, "扁額金", new Vector3(0, 12.9f * s, 0.47f * s), new Vector3(1.9f * s, 2.5f * s, 0.05f), GlowGold);
            return g;
        }

        /// <summary>反り橋。a から b へ、幅 width、真ん中の高さ arch。両端の床の高さは a.y / b.y。</summary>
        public static Transform Bridge(Transform p, string name, Vector3 a, Vector3 b, float width, float arch, int segs = 14)
        {
            var g = Group(p, name, Vector3.zero);
            var dir = b - a; dir.y = 0;
            float len = dir.magnitude;
            var fwd = dir / len;
            var right = Vector3.Cross(Vector3.up, fwd);
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            Vector3 At(float t) => Vector3.Lerp(a, b, t) + Vector3.up * (arch * Mathf.Sin(t * Mathf.PI));
            for (int i = 0; i < segs; i++)
            {
                var p0 = At(i / (float)segs);
                var p1 = At((i + 1) / (float)segs);
                var mid = (p0 + p1) / 2;
                var seg = p1 - p0;
                float pitch = -Mathf.Atan2(seg.y, new Vector2(seg.x, seg.z).magnitude) * Mathf.Rad2Deg;
                float l = seg.magnitude + 0.08f;
                Box(g, "床板", mid + Vector3.down * 0.2f, new Vector3(width, 0.4f, l), Wood, true, new Vector3(pitch, yaw, 0));
                Box(g, "桁", mid + Vector3.down * 0.5f, new Vector3(width + 0.3f, 0.3f, l), Vermilion, false, new Vector3(pitch, yaw, 0));
                foreach (float side in new[] { -1f, 1f })
                {
                    var rp = mid + right * side * (width / 2 - 0.1f);
                    Box(g, "高欄", rp + Vector3.up * 0.95f, new Vector3(0.14f, 0.12f, l), Vermilion, false, new Vector3(pitch, yaw, 0));
                    Box(g, "高欄", rp + Vector3.up * 0.45f, new Vector3(0.1f, 0.1f, l), Vermilion, false, new Vector3(pitch, yaw, 0));
                    Box(g, "見えない柵", rp + Vector3.up * 0.7f, new Vector3(0.2f, 1.4f, l), Vermilion, true, new Vector3(pitch, yaw, 0)).GetComponent<Renderer>().enabled = false;
                    if (i % 2 == 0)
                    {
                        Cyl(g, "親柱", p0 + right * side * (width / 2 - 0.1f) + Vector3.up * 0.5f, 0.09f, 1.1f, Vermilion);
                        if (i == 0 || i == segs - 2)
                            Ball(g, "擬宝珠", p0 + right * side * (width / 2 - 0.1f) + Vector3.up * 1.15f, Vector3.one * 0.26f, Gold);
                    }
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Cyl(g, "親柱", b + right * side * (width / 2 - 0.1f) + Vector3.up * 0.5f, 0.09f, 1.1f, Vermilion);
                Ball(g, "擬宝珠", b + right * side * (width / 2 - 0.1f) + Vector3.up * 1.15f, Vector3.one * 0.26f, Gold);
            }
            return g;
        }

        public static void Sakura(Transform p, Vector3 pos, float s, bool glow)
        {
            var g = Group(p, "桜", pos);
            var bark = Lit("bark", new Color(0.24f, 0.16f, 0.14f), 0.15f);
            Cyl(g, "幹", new Vector3(0, 3f * s, 0), 0.9f * s, 6f * s, bark, true);
            var pinks = new[]
            {
                Lit("sakura1", new Color(1f, 0.78f, 0.86f), 0.1f), Lit("sakura2", new Color(0.98f, 0.68f, 0.8f), 0.1f),
                Lit("sakura3", new Color(1f, 0.86f, 0.9f), 0.1f), Lit("sakura4", new Color(0.95f, 0.6f, 0.75f), 0.1f),
            };
            int branches = 7;
            for (int i = 0; i < branches; i++)
            {
                float ang = i * Mathf.PI * 2 / branches + R(-0.2f, 0.2f);
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                float tilt = R(35, 55);
                var basePos = new Vector3(0, 5.5f * s, 0);
                var br = Cyl(g, "枝", basePos + dir * 2.2f * s + Vector3.up * 1.8f * s, 0.35f * s, 5.5f * s, bark);
                br.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90 - tilt, 0, 0);
            }
            int clumps = (int)(46 * s / 1.5f);
            for (int i = 0; i < clumps; i++)
            {
                float ang = R(0, Mathf.PI * 2);
                float rad = Mathf.Sqrt(R(0.05f, 1f)) * 9f * s;
                float y = 8.5f * s + R(-1.2f, 3.2f) * s - rad * 0.18f;
                var c = Ball(g, "花", new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad),
                    new Vector3(R(3.2f, 5f), R(2.4f, 3.6f), R(3.2f, 5f)) * s, glow && i % 6 == 0 ? GlowSakura : pinks[Ri(0, pinks.Length)]);
            }
        }

        public static void Pine(Transform p, Vector3 pos, float s)
        {
            var g = Group(p, "松", pos, R(0, 360));
            var bark = Lit("pine_bark", new Color(0.28f, 0.2f, 0.16f), 0.1f);
            var leaf = Lit("pine_leaf", new Color(0.13f, 0.3f, 0.2f), 0.1f);
            var t = Cyl(g, "幹", new Vector3(0, 2.5f * s, 0), 0.3f * s, 5f * s, bark, true);
            t.transform.localEulerAngles = new Vector3(R(-6, 6), 0, R(-6, 6));
            for (int i = 0; i < 4; i++)
                Ball(g, "葉", new Vector3(R(-1.5f, 1.5f) * s, (3.4f + i * 1.0f) * s, R(-1.5f, 1.5f) * s), new Vector3(R(3f, 4.2f), 1.1f, R(2.6f, 3.6f)) * s * (1 - i * 0.15f), leaf);
        }

        public static void Willow(Transform p, Vector3 pos, float s)
        {
            var g = Group(p, "柳", pos, R(0, 360));
            var bark = Lit("bark", new Color(0.24f, 0.16f, 0.14f), 0.15f);
            var leaf = Lit("willow_leaf", new Color(0.48f, 0.66f, 0.3f), 0.1f);
            Cyl(g, "幹", new Vector3(0, 2.8f * s, 0), 0.35f * s, 5.6f * s, bark, true);
            for (int i = 0; i < 10; i++)
            {
                float ang = i * Mathf.PI * 2 / 10;
                Ball(g, "垂れ葉", new Vector3(Mathf.Cos(ang) * 1.8f * s, 4.2f * s, Mathf.Sin(ang) * 1.8f * s), new Vector3(1.3f, 4.2f, 1.3f) * s, leaf);
            }
            Ball(g, "葉", new Vector3(0, 6.2f * s, 0), new Vector3(4.5f, 2f, 4.5f) * s, leaf);
        }

        /// <summary>野点傘と縁台（茶屋の前）。</summary>
        public static void TeaSeat(Transform p, Vector3 pos, float rotY)
        {
            var g = Group(p, "縁台", pos, rotY);
            var red = Lit("felt_red", new Color(0.75f, 0.1f, 0.12f), 0.05f);
            Box(g, "台", new Vector3(0, 0.45f, 0), new Vector3(2.0f, 0.12f, 0.8f), Wood, true);
            Box(g, "毛氈", new Vector3(0, 0.52f, 0), new Vector3(2.0f, 0.03f, 0.82f), red);
            for (int i = 0; i < 4; i++)
                Box(g, "脚", new Vector3((i % 2 == 0 ? -0.9f : 0.9f), 0.2f, (i < 2 ? -0.32f : 0.32f)), new Vector3(0.08f, 0.4f, 0.08f), WoodDark);
            Cyl(g, "傘の柄", new Vector3(0, 1.6f, -0.6f), 0.04f, 3.2f, WoodDark);
            Cyl(g, "野点傘", new Vector3(0, 3.1f, -0.6f), 1.5f, 0.12f, red);
        }
    }
}
