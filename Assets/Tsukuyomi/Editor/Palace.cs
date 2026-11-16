using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 中に入れる建物：月の御殿の正殿と、茶屋のくつろぎ場（屋根とソファだけの開いた座敷）。
    /// </summary>
    public static class Palace
    {
        static Material Sofa => Lit("sofa_red", new Color(0.62f, 0.12f, 0.16f), 0.15f);
        static Material Cushion => Lit("sofa_cushion", new Color(0.92f, 0.82f, 0.6f), 0.1f);
        static Material Tatami => Lit("tatami", new Color(0.72f, 0.7f, 0.45f), 0.1f);
        static Material Kinbyobu => Lit("kinbyobu", new Color(0.86f, 0.68f, 0.32f), 0.55f, 0.6f);

        /// <summary>ソファ（座ると local -z を向く）。</summary>
        public static void SofaSet(Transform parent, Vector3 pos, float yaw, float width = 3.2f)
        {
            var s = Group(parent, "ソファ", pos, yaw);
            Box(s, "座面", new Vector3(0, 0.25f, 0), new Vector3(width, 0.5f, 1.1f), Sofa, true);
            Box(s, "背もたれ", new Vector3(0, 0.65f, 0.5f), new Vector3(width, 0.8f, 0.25f), Sofa);
            Box(s, "ひじかけ", new Vector3(-width / 2 + 0.05f, 0.5f, 0), new Vector3(0.2f, 0.5f, 1.1f), Sofa);
            Box(s, "ひじかけ", new Vector3(width / 2 - 0.05f, 0.5f, 0), new Vector3(0.2f, 0.5f, 1.1f), Sofa);
            int n = Mathf.Max(2, Mathf.RoundToInt(width - 0.2f));
            for (int k = 0; k < n; k++) Box(s, "クッション", new Vector3(-width / 2 + 0.6f + k * (width - 1.2f) / Mathf.Max(1, n - 1), 0.62f, 0.25f), new Vector3(0.7f, 0.4f, 0.18f), Cushion, false, new Vector3(-12, 0, 0));
            // 段階3：座れる（2か所。座ると local -z を向く）
            var seat = s.gameObject.AddComponent<Seat>();
            seat.yaw = s.eulerAngles.y + 180f;
            seat.spots = width >= 3f
                ? new[] { s.TransformPoint(new Vector3(-width / 4, 0.5f, 0.05f)), s.TransformPoint(new Vector3(width / 4, 0.5f, 0.05f)) }
                : new[] { s.TransformPoint(new Vector3(0, 0.5f, 0.05f)) };
            var t = Group(s, "低い机", new Vector3(0, 0, -1.8f), 0);
            Box(t, "天板", new Vector3(0, 0.38f, 0), new Vector3(Mathf.Min(1.8f, width - 1f), 0.08f, 0.9f), WoodDark, true);
            foreach (float lx in new[] { -0.7f, 0.7f }) foreach (float lz in new[] { -0.35f, 0.35f }) Box(t, "脚", new Vector3(lx * Mathf.Min(1f, (width - 1f) / 1.8f), 0.17f, lz), new Vector3(0.08f, 0.34f, 0.08f), WoodDark);
            var cup = ColorMat(new Color(0.3f, 0.5f, 0.35f));
            Ball(t, "茶わん", new Vector3(0.35f, 0.47f, 0.1f), new Vector3(0.14f, 0.08f, 0.14f), cup);
            Ball(t, "茶わん", new Vector3(-0.35f, 0.47f, -0.1f), new Vector3(0.14f, 0.08f, 0.14f), cup);
            Ball(t, "団子", new Vector3(0, 0.46f, 0.15f), new Vector3(0.3f, 0.06f, 0.12f), ColorMat(new Color(0.95f, 0.75f, 0.8f)));
        }

        /// <summary>
        /// 茶屋のくつろぎ場：屋根だけの開いた座敷にソファ・低い机・お茶・提灯・すだれ。ソファは local -z を向く。
        /// </summary>
        public static void Lounge(Transform parent, Vector3 pos, float rotY, float w = 10f, float d = 7f)
        {
            var g = Group(parent, "茶屋のくつろぎ場", pos, rotY);
            Box(g, "床", new Vector3(0, 0.3f, 0), new Vector3(w, 0.6f, d), Wood, true);
            Box(g, "畳", new Vector3(0, 0.61f, 0), new Vector3(w - 0.8f, 0.02f, d - 0.8f), Tatami);
            // 上がり段（前とうしろ）
            foreach (float sd in new[] { -1f, 1f })
                Box(g, "上がり段", new Vector3(0, 0.15f, sd * (d / 2 + 0.3f)), new Vector3(Mathf.Min(3f, w - 2f), 0.3f, 0.6f), Stone, true);
            foreach (float x in new[] { -w / 2 + 0.3f, w / 2 - 0.3f })
                foreach (float z in new[] { -d / 2 + 0.2f, d / 2 - 0.2f })
                    Cyl(g, "柱", new Vector3(x, 2.1f, z), 0.13f, 3f, Vermilion);
            MeshObj(g, "屋根", Roof(w + 2.5f, d + 2.5f, 2.6f * Mathf.Min(1f, d / 7f), 0.7f), new Vector3(0, 3.6f, 0), RoofBark);
            if (w >= 8f) { SofaSet(g, new Vector3(-w / 4, 0.62f, 1.6f * d / 7f), 0); SofaSet(g, new Vector3(w / 4, 0.62f, 1.6f * d / 7f), 0); }
            else SofaSet(g, new Vector3(0, 0.62f, 1.4f * d / 7f), 0, 3.6f);
            foreach (float x in new[] { -w / 4, w / 4 }) Box(g, "座布団", new Vector3(x, 0.66f, -d / 2 + 1.4f), new Vector3(0.7f, 0.1f, 0.7f), Cushion);
            var sudare = Lit("sudare", new Color(0.72f, 0.6f, 0.38f), 0.1f);
            Box(g, "すだれ", new Vector3(0, 2.9f, d / 2 - 0.25f), new Vector3(w - 1f, 1f, 0.04f), sudare);
            int nc = Mathf.Max(2, Mathf.RoundToInt(w / 2.5f));
            for (int i = 0; i < nc; i++) Chochin(g, new Vector3(-w / 2 + 1.4f + i * (w - 2.8f) / Mathf.Max(1, nc - 1), 2.9f, -d / 2 + 0.2f), i % 2 == 0 ? GlowRed : GlowWhite);
            Lamp(g, new Vector3(0, 2.6f, 0), new Color(1f, 0.7f, 0.45f), 10f, 1.8f);
            Town.Reserve(new Vector2(pos.x, pos.z), Mathf.Sqrt(w * w + d * d) / 2 + 0.5f);
        }

        /// <summary>まわりに道・石段・水がなければ、くつろぎ場を置く。</summary>
        public static bool TryLounge(Transform parent, Vector2 c0, float rotY, float w, float d)
        {
            for (float r = 0; r <= 8f; r += 2f)
                for (float a = 0; a < 360f; a += r == 0 ? 360f : 45f)
                {
                    var c = c0 + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                    if (LoungeFits(c, rotY, w, d, out float y)) { Lounge(parent, new Vector3(c.x, y, c.y), rotY, w, d); return true; }
                }
            Debug.Log($"[Tsukuyomi] lounge skipped at ({c0.x:0},{c0.y:0})");
            return false;
        }

        static bool LoungeFits(Vector2 c, float rotY, float w, float d, out float y)
        {
            y = 0;
            var rot = Quaternion.Euler(0, rotY, 0);
            for (float x = -w / 2 - 1.2f; x <= w / 2 + 1.2f; x += 1f)
                for (float z = -d / 2 - 1.2f; z <= d / 2 + 1.2f; z += 1f)
                {
                    var o = rot * new Vector3(x, 0, z);
                    var q = c + new Vector2(o.x, o.z);
                    if (Town.OnAnyStreet(q, 0.2f) || TerrainBuilder.NearStairs(q, 0.5f) || Island.NearestWater(q.x, q.y) < 0.8f) return false;
                }
            if (!Town.Flat(c, w, d, rotY, out y, 0.3f)) return false;
            Physics.SyncTransforms();
            if (Physics.CheckBox(new Vector3(c.x, y + 1.6f, c.y), new Vector3(w / 2 + 0.6f, 1.2f, d / 2 + 0.6f), rot, ~0, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        /// <summary>
        /// 月の御殿の正殿：1階は中へ入れる広間（神鏡・金屏風・ソファ）、奥の階段で最上階の望楼（屋根つきの見晴らし台）へ上がれる。
        /// pos は基壇の上の中心。正面は local -z（庭の方）。
        /// </summary>
        public static Transform Seiden(Transform parent, Vector3 pos)
        {
            const float W = 20f, D = 9f, H1 = 4.4f;
            const float upY = 0.2f + H1 + 0.25f;   // 望楼の床の高さ
            var g = Group(parent, "正殿", pos);
            // 1階の床と柱
            Box(g, "床", new Vector3(0, 0.1f, 0), new Vector3(W, 0.2f, D), Wood, true);
            Box(g, "畳", new Vector3(-2f, 0.21f, -0.3f), new Vector3(12f, 0.02f, 5.4f), Tatami);
            for (float x = -W / 2 + 0.2f; x <= W / 2 - 0.1f; x += (W - 0.4f) / 8f)
                foreach (float z in new[] { -D / 2 + 0.2f, D / 2 - 0.2f })
                    Cyl(g, "柱", new Vector3(x, 0.2f + H1 / 2, z), 0.2f, H1, Vermilion, true);
            // 奥は金屏風の壁、両わきは奥半分だけ白壁（前は開いていて、庭と月が見える）
            Box(g, "金屏風の壁", new Vector3(0, 0.2f + H1 / 2, D / 2 - 0.05f), new Vector3(W, H1, 0.2f), Kinbyobu, true);
            foreach (float sx in new[] { -1f, 1f })
                Box(g, "わきの壁", new Vector3(sx * (W / 2 - 0.05f), 0.2f + H1 / 2, D / 4), new Vector3(0.2f, H1, D / 2), WhiteWall, true);
            Box(g, "欄間", new Vector3(0, 0.2f + H1 - 0.45f, -D / 2 + 0.15f), new Vector3(W, 0.8f, 0.12f), Lattice);
            Box(g, "梁", new Vector3(0, 0.2f + H1 - 0.02f, -D / 2 + 0.2f), new Vector3(W + 0.4f, 0.35f, 0.4f), Vermilion);
            // 神鏡の祭壇（奥のまん中）
            Box(g, "祭壇", new Vector3(0, 0.7f, D / 2 - 1f), new Vector3(3f, 1f, 1.2f), Black, true);
            var mirror = Cyl(g, "神鏡", new Vector3(0, 2.4f, D / 2 - 0.9f), 0.75f, 0.08f, GlowGold);
            mirror.transform.localEulerAngles = new Vector3(90, 0, 0);
            foreach (float x in new[] { -1.9f, 1.9f })
            {
                Cyl(g, "ぼんぼりの柄", new Vector3(x, 0.9f, D / 2 - 1f), 0.05f, 1.4f, Black);
                Cyl(g, "ぼんぼり", new Vector3(x, 1.8f, D / 2 - 1f), 0.25f, 0.6f, GlowWhite);
            }
            // くつろぐ席（庭の方を向く）
            SofaSet(g, new Vector3(-6f, 0.2f, 0.6f), 0);
            SofaSet(g, new Vector3(-1.8f, 0.2f, -0.6f), 0, 2.6f);
            for (int i = 0; i < 6; i++) Chochin(g, new Vector3(-8f + i * 3.2f, 0.2f + H1 - 1.1f, -D / 2 + 0.5f), i % 2 == 0 ? GlowRed : GlowWhite);
            Lamp(g, new Vector3(-3f, 3.4f, 0), new Color(1f, 0.75f, 0.5f), 14f, 2.2f);

            // 望楼への階段（奥の東、金屏風にそって +x へ上がる）
            const float run = 0.42f, sx0 = 2.4f, sz = D / 2 - 1.2f, sw = 1.4f;
            int n = Mathf.CeilToInt((upY - 0.2f) / 0.29f);
            float rise = (upY - 0.2f) / n;
            for (int i = 0; i < n; i++)
            {
                float top = 0.2f + (i + 1) * rise;
                Box(g, "階段", new Vector3(sx0 + i * run + run / 2, (0.2f + top) / 2, sz), new Vector3(run + 0.02f, top - 0.2f, sw), WoodDark, true);
            }
            float sx1 = sx0 + n * run;


            // 1階の屋根（まわりの庇）
            var copper = RoofCopper;
            foreach (float sd in new[] { -1f, 1f })
            {
                Box(g, "庇", new Vector3(0, upY - 0.75f, sd * (D / 2 + 1.05f)), new Vector3(W + 2.6f, 0.14f, 1.8f), copper, false, new Vector3(sd * 18f, 0, 0));
                Box(g, "庇", new Vector3(sd * (W / 2 + 1.05f), upY - 0.75f, 0), new Vector3(1.8f, 0.14f, D + 2.6f), copper, false, new Vector3(0, 0, -sd * 18f));
            }

            // 望楼の床（階段の上は吹き抜け）
            float holeZ0 = sz - sw / 2 - 0.05f;
            Box(g, "望楼の床", new Vector3(0, upY - 0.125f, (-D / 2 + holeZ0) / 2), new Vector3(W, 0.25f, holeZ0 + D / 2), Wood, true);
            Box(g, "望楼の床", new Vector3((-W / 2 + sx0) / 2, upY - 0.125f, (holeZ0 + D / 2) / 2), new Vector3(sx0 + W / 2, 0.25f, D / 2 - holeZ0), Wood, true);
            if (W / 2 - sx1 > 0.2f) Box(g, "望楼の床", new Vector3((sx1 + W / 2) / 2, upY - 0.125f, (holeZ0 + D / 2) / 2), new Vector3(W / 2 - sx1, 0.25f, D / 2 - holeZ0), Wood, true);
            Box(g, "望楼の畳", new Vector3(-4f, upY + 0.01f, -1.2f), new Vector3(9f, 0.02f, 4.2f), Tatami);
            // 吹き抜けのまわりの手すり（上がり口は東の端にあける）
            Box(g, "吹き抜けの手すり", new Vector3((sx0 + sx1 - 1.6f) / 2, upY + 0.9f, holeZ0), new Vector3(sx1 - 1.6f - sx0, 0.1f, 0.1f), Vermilion);
            Box(g, "見えない柵", new Vector3((sx0 + sx1 - 1.6f) / 2, upY + 0.7f, holeZ0), new Vector3(sx1 - 1.6f - sx0, 1.4f, 0.2f), Vermilion, true).GetComponent<Renderer>().enabled = false;
            Box(g, "見えない柵", new Vector3(sx0 - 0.1f, upY + 0.7f, (holeZ0 + D / 2) / 2), new Vector3(0.2f, 1.4f, D / 2 - holeZ0), Vermilion, true).GetComponent<Renderer>().enabled = false;
            // 望楼の柱・手すり・屋根
            const float H2 = 3.6f;
            for (float x = -W / 2 + 0.3f; x <= W / 2 - 0.2f; x += (W - 0.6f) / 4f)
                foreach (float z in new[] { -D / 2 + 0.3f, D / 2 - 0.3f })
                    Cyl(g, "望楼の柱", new Vector3(x, upY + H2 / 2, z), 0.17f, H2, Vermilion, true);
            foreach (float sd in new[] { -1f, 1f })
            {
                Box(g, "高欄", new Vector3(0, upY + 0.9f, sd * (D / 2 - 0.15f)), new Vector3(W, 0.12f, 0.12f), Vermilion);
                Box(g, "見えない柵", new Vector3(0, upY + 0.7f, sd * (D / 2 - 0.15f)), new Vector3(W, 1.4f, 0.25f), Vermilion, true).GetComponent<Renderer>().enabled = false;
                Box(g, "高欄", new Vector3(sd * (W / 2 - 0.15f), upY + 0.9f, 0), new Vector3(0.12f, 0.12f, D), Vermilion);
                Box(g, "見えない柵", new Vector3(sd * (W / 2 - 0.15f), upY + 0.7f, 0), new Vector3(0.25f, 1.4f, D), Vermilion, true).GetComponent<Renderer>().enabled = false;
            }
            for (float x = -W / 2 + 1f; x < W / 2; x += 1.25f)
                foreach (float sd in new[] { -1f, 1f }) Cyl(g, "高欄の小柱", new Vector3(x, upY + 0.45f, sd * (D / 2 - 0.15f)), 0.04f, 0.9f, Vermilion);
            MeshObj(g, "望楼の屋根", Roof(W + 3.2f, D + 3.2f, 3.8f, 1.1f), new Vector3(0, upY + H2, 0), copper);
            Box(g, "棟", new Vector3(0, upY + H2 + 3.8f, 0), new Vector3(W * 0.55f, 0.35f, 0.45f), Black);
            foreach (float sx in new[] { -1f, 1f }) Ball(g, "鴟尾", new Vector3(sx * W * 0.28f, upY + H2 + 4.1f, 0), new Vector3(0.5f, 0.8f, 0.4f), Gold);
            // 望楼の席：月見のソファと遠眼鏡
            SofaSet(g, new Vector3(-6.5f, upY, 1.0f), 0);
            SofaSet(g, new Vector3(-2.2f, upY, 1.0f), 0, 2.6f);
            var scope = Group(g, "月見の遠眼鏡", new Vector3(5.5f, upY, -2.8f), 200);
            Cyl(scope, "三脚", new Vector3(0, 0.6f, 0), 0.05f, 1.2f, WoodDark);
            var tube = Cyl(scope, "筒", new Vector3(0, 1.35f, 0.1f), 0.11f, 1.1f, Gold);
            tube.transform.localEulerAngles = new Vector3(55, 0, 0);
            for (int i = 0; i < 6; i++) Chochin(g, new Vector3(-8f + i * 3.2f, upY + H2 - 0.6f, -D / 2 + 0.3f), i % 2 == 0 ? GlowWhite : GlowRed);
            Lamp(g, new Vector3(0, upY + 2.6f, 0), new Color(1f, 0.78f, 0.55f), 14f, 2f);
            // 扁額
            Box(g, "扁額", new Vector3(0, upY + H2 - 0.5f, -D / 2 - 0.1f), new Vector3(3f, 1f, 0.12f), Black);
            Box(g, "扁額金", new Vector3(0, upY + H2 - 0.5f, -D / 2 - 0.17f), new Vector3(2.6f, 0.75f, 0.03f), GlowGold);
            return g;
        }
    }
}
