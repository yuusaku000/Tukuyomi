using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 町の建物いろいろ。正面はローカルの +z。pos.y は地面の高さ。
    ///   TownHouse：1〜3階建ての町家・店（上の階は少し小さく、階ごとに屋根）
    ///   Kura：白壁の蔵　Ryotei：縁側と提灯の料亭　Yatai：屋台
    /// どれも夜は窓が灯り、ときどき光る縦の看板（デジタルサイネージ）がつく。
    /// </summary>
    public static class Buildings
    {
        public static Material WindowGlow => Glow("glow_window", new Color(1f, 0.72f, 0.42f), 0.25f, 2.2f);
        public static Material Kawara => Lit("roof_kawara", new Color(0.24f, 0.26f, 0.3f), 0.5f, 0f, true);
        public static Material Namako => Lit("namako", new Color(0.18f, 0.19f, 0.22f), 0.3f);
        static readonly Color[] SignColors =
        {
            new Color(1f, 0.35f, 0.7f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.75f, 0.3f), new Color(0.6f, 0.45f, 1f), new Color(0.4f, 1f, 0.6f),
        };

        static Material RoofOf(int k) => k == 0 ? RoofBark : k == 1 ? Kawara : k == 2 ? RoofCopper : RoofTile;

        public static Material Signage(int i)
        {
            var m = Shaded("signage_" + i, "Tsukuyomi/Signage");
            m.SetColor("_Color", SignColors[i % SignColors.Length]);
            m.SetFloat("_Seed", i * 1.37f);
            return m;
        }

        static void Base(Transform g, float w, float d)
        {
            Box(g, "基壇", new Vector3(0, -0.25f, 0), new Vector3(w + 0.8f, 1.5f, d + 0.8f), Stone, true);
            Box(g, "床", new Vector3(0, 0.56f, 0), new Vector3(w, 0.12f, d), WoodDark);
        }

        static void Pillars(Transform g, float w, float d, float y0, float h)
        {
            int nx = Mathf.Max(2, Mathf.RoundToInt(w / 2.6f) + 1);
            for (int i = 0; i < nx; i++)
            {
                float x = -w / 2 + 0.2f + i * (w - 0.4f) / (nx - 1);
                Cyl(g, "柱", new Vector3(x, y0 + h / 2, d / 2 - 0.2f), 0.14f, h, Vermilion);
            }
            Box(g, "梁", new Vector3(0, y0 + h - 0.12f, d / 2 - 0.2f), new Vector3(w + 0.3f, 0.3f, 0.32f), Vermilion);
        }

        static void Ridge(Transform g, float y, float w, float d)
        {
            bool alongX = w >= d;
            float len = Mathf.Abs(w - d) + 0.8f;
            Box(g, "棟", new Vector3(0, y + 0.08f, 0), alongX ? new Vector3(len, 0.3f, 0.36f) : new Vector3(0.36f, 0.3f, len), Black);
            var e = alongX ? new Vector3(len / 2, 0, 0) : new Vector3(0, 0, len / 2);
            Box(g, "鴟尾", new Vector3(e.x, y + 0.38f, e.z), new Vector3(0.28f, 0.55f, 0.28f), Gold);
            Box(g, "鴟尾", new Vector3(-e.x, y + 0.38f, -e.z), new Vector3(0.28f, 0.55f, 0.28f), Gold);
        }

        static void Windows(Transform g, float w, float y, float z, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = -w / 2 + w * (i + 0.5f) / count;
                if (Rng.NextDouble() < 0.25) continue;
                Box(g, "窓", new Vector3(x, y, z), new Vector3(Mathf.Min(1.4f, w / count - 0.5f), 0.9f, 0.05f), WindowGlow);
                Box(g, "格子", new Vector3(x, y, z + 0.04f), new Vector3(0.05f, 0.9f, 0.03f), WoodDark);
            }
        }

        static void Sign(Transform g, float x, float y, float z, float h, int seed)
        {
            Box(g, "看板の枠", new Vector3(x, y, z), new Vector3(0.95f, h + 0.2f, 0.18f), Black);
            Box(g, "看板", new Vector3(x, y, z + 0.1f), new Vector3(0.8f, h, 0.04f), Signage(seed));
        }

        public static Transform TownHouse(Transform parent, Vector3 pos, float rotY, float w, float d, int floors, int roofKind, bool shop, int seed)
        {
            var g = Group(parent, floors >= 3 ? "楼閣" : floors == 2 ? "二階建ての店" : "店", pos, rotY);
            Base(g, w, d);
            float y0 = 0.62f, h = R(3.4f, 3.9f);
            float front = shop ? 1.6f : 0.4f;
            float wallD = d - front - 0.3f, wallZ = (-d / 2 + 0.15f + d / 2 - front) / 2;
            Box(g, "壁", new Vector3(0, y0 + h / 2, wallZ), new Vector3(w - 0.3f, h, wallD), WhiteWall, true);
            Box(g, "格子", new Vector3(0, y0 + h * 0.62f, wallZ + wallD / 2 + 0.03f), new Vector3(w - 0.6f, h * 0.5f, 0.06f), Lattice);
            Box(g, "腰板", new Vector3(0, y0 + h * 0.16f, wallZ + wallD / 2 + 0.03f), new Vector3(w - 0.6f, h * 0.3f, 0.06f), WoodDark);
            Pillars(g, w, d, y0, h);
            if (shop)
            {
                var nm = ColorMat(NorenColors[Ri(0, NorenColors.Length)]);
                int pieces = Mathf.Max(2, Mathf.RoundToInt((w - 1.6f) / 1.1f));
                float pw = (w - 1.6f) / pieces;
                for (int i = 0; i < pieces; i++)
                    Box(g, "暖簾", new Vector3(-w / 2 + 0.8f + pw * (i + 0.5f), y0 + h - 0.75f, d / 2 - 0.05f), new Vector3(pw - 0.06f, 0.95f, 0.03f), nm);
                Box(g, "台", new Vector3(0, y0 + 0.45f, wallZ + wallD / 2 + 0.6f), new Vector3(w - 1.6f, 0.9f, 0.8f), Wood);
                int goods = Ri(4, 8);
                for (int i = 0; i < goods; i++)
                {
                    var c = Color.HSVToRGB(R(0f, 1f), R(0.3f, 0.8f), R(0.6f, 1f));
                    float gx = -w / 2 + 1.2f + (w - 2.4f) * (i + 0.5f) / goods;
                    if (Rng.NextDouble() < 0.5)
                        Box(g, "品物", new Vector3(gx, y0 + 1.05f, wallZ + wallD / 2 + 0.6f), new Vector3(R(0.25f, 0.5f), R(0.2f, 0.4f), R(0.25f, 0.4f)), ColorMat(c), false, new Vector3(0, R(-20, 20), 0));
                    else
                        Ball(g, "品物", new Vector3(gx, y0 + 1.08f, wallZ + wallD / 2 + 0.6f), Vector3.one * R(0.22f, 0.36f), ColorMat(c));
                }
                Chochin(g, new Vector3(-w / 2 + 0.3f, y0 + h - 0.85f, d / 2 + 0.25f), Rng.NextDouble() < 0.5 ? GlowRed : GlowWhite);
                Chochin(g, new Vector3(w / 2 - 0.3f, y0 + h - 0.85f, d / 2 + 0.25f), Rng.NextDouble() < 0.5 ? GlowRed : GlowWhite);
                if (Rng.NextDouble() < 0.6)
                    Box(g, "日除け", new Vector3(0, y0 + h * 0.78f, d / 2 + 0.55f), new Vector3(w - 0.3f, 0.06f, 1.5f), ColorMat(NorenColors[Ri(0, NorenColors.Length)]), false, new Vector3(16, 0, 0));
            }
            else
            {
                // すだれと鉢植え
                var sudare = Lit("sudare", new Color(0.72f, 0.6f, 0.38f), 0.1f);
                Box(g, "すだれ", new Vector3(0, y0 + h * 0.62f, wallZ + wallD / 2 + 0.08f), new Vector3(w - 1.2f, h * 0.45f, 0.03f), sudare);
            }

            float y = y0 + h;
            float cw = w, cd = d;
            for (int f = 1; f < floors; f++)
            {
                // 下の階の屋根（裳階）と、少し小さい上の階
                float skirtH = Mathf.Min(cw, cd) * 0.16f;
                MeshObj(g, "屋根", Roof(cw + 2.2f, cd + 2.2f, skirtH, 0.6f), new Vector3(0, y + 0.02f, 0), RoofOf(roofKind));
                float nw = cw - 1.4f, nd = cd - 1.4f, nh = h * 0.85f;
                float ny = y + skirtH * 0.75f;
                Box(g, "縁", new Vector3(0, ny + 0.02f, 0), new Vector3(nw + 1.2f, 0.14f, nd + 1.2f), WoodDark);
                Box(g, "上の階", new Vector3(0, ny + nh / 2, 0), new Vector3(nw, nh, nd), WhiteWall, true);
                Box(g, "高欄", new Vector3(0, ny + 0.5f, nd / 2 + 0.55f), new Vector3(nw + 0.8f, 0.12f, 0.12f), Vermilion);
                Windows(g, nw, ny + nh * 0.55f, nd / 2 + 0.03f, Mathf.Max(2, Mathf.RoundToInt(nw / 2.2f)));
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        Cyl(g, "柱", new Vector3(sx * (nw / 2 - 0.05f), ny + nh / 2, sz * (nd / 2 - 0.05f)), 0.13f, nh, Vermilion);
                Box(g, "梁", new Vector3(0, ny + nh - 0.1f, nd / 2), new Vector3(nw + 0.3f, 0.28f, 0.3f), Vermilion);
                y = ny + nh; cw = nw; cd = nd;
            }
            float rh = Mathf.Min(cw, cd) * 0.42f;
            MeshObj(g, "屋根の頂", Roof(cw + 2.3f, cd + 2.3f, rh, 0.75f), new Vector3(0, y + 0.02f, 0), RoofOf(roofKind));
            Ridge(g, y + 0.02f + rh, cw, cd);

            if (Rng.NextDouble() < 0.4)
                Sign(g, (Rng.NextDouble() < 0.5 ? -1 : 1) * (w / 2 - 0.3f), y0 + h + 1.4f, d / 2 + 0.25f, floors >= 2 ? 3.2f : 1.8f, seed);
            return g;
        }

        public static Transform Kura(Transform parent, Vector3 pos, float rotY, float w, float d)
        {
            var g = Group(parent, "蔵", pos, rotY);
            Base(g, w, d);
            float h = R(5.2f, 6.2f);
            Box(g, "白壁", new Vector3(0, 0.62f + h / 2, 0), new Vector3(w - 0.2f, h, d - 0.2f), WhiteWall, true);
            Box(g, "なまこ壁", new Vector3(0, 0.62f + 0.9f, 0), new Vector3(w, 1.8f, d), Namako);
            for (int i = 0; i < 4; i++)
                Box(g, "目地", new Vector3(-w / 2 + w * (i + 0.5f) / 4, 0.62f + 0.9f, d / 2 + 0.01f), new Vector3(0.06f, 1.7f, 0.02f), WhiteWall, false, new Vector3(0, 0, 45));
            Box(g, "扉", new Vector3(0, 0.62f + 1.6f, d / 2 - 0.05f), new Vector3(1.6f, 2.4f, 0.2f), Black);
            Box(g, "窓", new Vector3(0, 0.62f + h - 1.2f, d / 2 - 0.05f), new Vector3(0.9f, 0.7f, 0.2f), Black);
            float rh = Mathf.Min(w, d) * 0.45f;
            MeshObj(g, "屋根", Roof(w + 1.6f, d + 1.6f, rh, 0.4f), new Vector3(0, 0.62f + h, 0), Kawara);
            Ridge(g, 0.62f + h + rh, w, d);
            return g;
        }

        public static Transform Ryotei(Transform parent, Vector3 pos, float rotY, float w, float d, int seed)
        {
            var g = TownHouse(parent, pos, rotY, w, d, 2, 0, false, seed);
            g.name = "料亭";
            // 正面の縁側と提灯の列
            Box(g, "縁側", new Vector3(0, 0.75f, d / 2 + 0.5f), new Vector3(w, 0.15f, 1.2f), Wood, true);
            for (int i = 0; i < 5; i++)
                Chochin(g, new Vector3(-w / 2 + w * (i + 0.5f) / 5, 3.7f, d / 2 + 0.9f), GlowWhite);
            Lamp(g, new Vector3(0, 2.5f, d / 2 + 1.5f), new Color(1f, 0.65f, 0.4f), 9f, 1.6f);
            return g;
        }

        public static Transform Yatai(Transform parent, Vector3 pos, float rotY, int seed)
        {
            var g = Group(parent, "屋台", pos, rotY);
            var cloth = ColorMat(NorenColors[seed % NorenColors.Length]);
            Box(g, "台", new Vector3(0, 0.5f, 0.2f), new Vector3(2.6f, 1f, 1.2f), Wood, true);
            foreach (float x in new[] { -1.2f, 1.2f })
                foreach (float z in new[] { -0.5f, 0.8f })
                    Box(g, "柱", new Vector3(x, 1.2f, z), new Vector3(0.1f, 2.4f, 0.1f), WoodDark);
            Box(g, "屋根", new Vector3(0, 2.45f, 0.15f), new Vector3(3f, 0.12f, 1.8f), cloth, false, new Vector3(-8, 0, 0));
            Box(g, "幕", new Vector3(0, 2.2f, 1.02f), new Vector3(2.8f, 0.5f, 0.03f), cloth);
            Chochin(g, new Vector3(1.3f, 1.9f, 1.1f), GlowRed);
            for (int i = 0; i < 4; i++)
                Ball(g, "品物", new Vector3(-0.9f + i * 0.6f, 1.12f, 0.4f), Vector3.one * 0.25f, ColorMat(Color.HSVToRGB(R(0f, 1f), 0.6f, 0.95f)));
            return g;
        }

        /// <summary>大通りの「のぼり」。</summary>
        public static void Nobori(Transform parent, Vector3 pos, float rotY, int seed)
        {
            var g = Group(parent, "のぼり", pos, rotY);
            Cyl(g, "竿", new Vector3(0, 2.4f, 0), 0.04f, 4.8f, WoodDark);
            var flag = Box(g, "旗", new Vector3(0.32f, 2.6f, 0), new Vector3(0.6f, 3.2f, 0.02f), ColorMat(NorenColors[seed % NorenColors.Length]));
            Box(g, "旗の帯", new Vector3(0.32f, 4.05f, 0.012f), new Vector3(0.62f, 0.3f, 0.02f), ColorMat(new Color(0.95f, 0.92f, 0.85f)));
            var sway = g.gameObject.AddComponent<Sway>();
            sway.angle = 3f; sway.speed = R(0.8f, 1.3f);
        }
    }
}
