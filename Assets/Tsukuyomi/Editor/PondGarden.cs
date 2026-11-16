using System.Collections.Generic;
using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 段階3：釣り堀のまわりを、まったり落ち着ける庭にする。
    ///   釣り台（池にせり出した木の台）・蓮の葉と花・柳と紅葉・池を向いた長椅子（座れる）・飛び石・鹿威しとつくばい・
    ///   大物番付の立て札・夜の蛍・茶屋を甘味の店に。
    /// 置く前に、通り・石段・水・ほかの物とぶつからないかを確かめる。町の並びが変わらないように、自分専用の乱数を使う。
    /// </summary>
    public static class PondGarden
    {
        static float L1, WY;

        static bool Fits(Vector2 c, float r, bool allowStreet = false)
        {
            if (!allowStreet && Town.OnAnyStreet(c, r * 0.7f)) return false;
            if (TerrainBuilder.NearStairs(c, r)) return false;
            if (Island.NearestWater(c.x, c.y) < r * 0.6f + 0.5f) return false;
            if (Mathf.Abs(Island.TerraceHeight(c.x, c.y) - L1) > 0.2f) return false;
            Physics.SyncTransforms();
            return !Physics.CheckBox(new Vector3(c.x, L1 + 1.5f, c.y), new Vector3(r, 0.9f, r), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        static Material Moss => Lit("moss", new Color(0.24f, 0.4f, 0.22f), 0.15f);
        static Material Felt => Lit("felt_red", new Color(0.75f, 0.1f, 0.12f), 0.05f);
        static Material Bamboo => Lit("bamboo_green", new Color(0.45f, 0.62f, 0.3f), 0.35f);

        public static void Build(Transform env, Transform deco, float l1, Transform teahouse, float teaW, float teaD)
        {
            var keep = Rng;
            Rng = new System.Random(3300);
            L1 = l1;
            var pond = Island.Pond;
            WY = Island.WaterSurface(pond.center.x, pond.center.y);
            if (float.IsNaN(WY)) WY = l1 - 0.55f;
            var g = Group(env, "釣り堀の庭", Vector3.zero);
            int n = 0;

            // 釣り台（池の南に2つ・東西に1つずつ）
            var decks = new List<Vector2>();
            foreach (var (x, z, dir) in new[]
            {
                (pond.center.x - 6f, pond.yMin, Vector2.up), (pond.center.x + 6f, pond.yMin, Vector2.up),
                (pond.xMax, pond.center.y + 2f, Vector2.left), (pond.xMin, pond.center.y - 2f, Vector2.right),
            })
            {
                var edge = new Vector2(x, z);
                var shore = edge - dir * 1.4f;
                if (!Fits(shore, 0.9f, true)) continue;
                Deck(g, edge, dir);
                decks.Add(edge + dir * 1.6f);
                n++;
            }

            // 蓮の葉と花（池のふちに寄せて。釣り台のまわりはあける）
            var pad = Lit("lotus_leaf", new Color(0.2f, 0.42f, 0.2f), 0.4f);
            var pad2 = Lit("lotus_leaf2", new Color(0.3f, 0.52f, 0.24f), 0.4f);
            var bloom = Glow("lotus_bloom", new Color(1f, 0.62f, 0.78f), 0.5f, 1.6f);
            int pads = 0;
            for (int t = 0; t < 200 && pads < 26; t++)
            {
                var p = new Vector2(R(pond.xMin + 0.8f, pond.xMax - 0.8f), R(pond.yMin + 0.8f, pond.yMax - 0.8f));
                if (Island.PondDistance(p.x, p.y) < -3.6f) continue;
                bool bad = false;
                foreach (var d in decks) if ((d - p).sqrMagnitude < 2.8f * 2.8f) bad = true;
                if (bad) continue;
                float r = R(0.28f, 0.55f);
                Cyl(g, "蓮の葉", new Vector3(p.x, WY + 0.012f + pads * 0.0006f, p.y), r, 0.02f, pads % 3 == 0 ? pad2 : pad);
                if (pads % 4 == 0)
                {
                    var fl = Group(g, "蓮の花", new Vector3(p.x + r * 0.3f, WY + 0.05f, p.y), R(0, 360));
                    for (int k = 0; k < 6; k++)
                    {
                        var pt = Ball(fl, "花びら", Quaternion.Euler(0, k * 60, 0) * new Vector3(0, 0.08f, 0.08f), new Vector3(0.08f, 0.16f, 0.06f), bloom);
                        pt.transform.localRotation = Quaternion.Euler(-35, k * 60, 0);
                    }
                    Ball(fl, "花芯", new Vector3(0, 0.08f, 0), Vector3.one * 0.07f, GlowGold);
                }
                pads++;
            }

            // 柳と紅葉（池のかど）
            int trees = 0;
            foreach (var c in new[]
            {
                new Vector2(pond.xMin - 3f, pond.yMin - 2.5f), new Vector2(pond.xMax + 3f, pond.yMin - 2.5f),
                new Vector2(pond.xMin - 3f, pond.yMax + 2.5f), new Vector2(pond.xMax + 3f, pond.yMax + 2.5f),
                new Vector2(pond.xMin - 4f, pond.center.y + 4f), new Vector2(pond.xMax + 4f, pond.center.y - 4f),
                new Vector2(pond.center.x - 12f, pond.yMin - 4f), new Vector2(pond.center.x + 12f, pond.yMin - 4f),
            })
            {
                if (!Fits(c, 1.2f)) continue;
                var at = new Vector3(c.x, Island.Height(c.x, c.y), c.y);
                if (trees % 2 == 0) Willow(g, at, R(0.75f, 0.9f)); else Momiji(g, at, R(0.8f, 1f));
                Cyl(g, "苔", at + Vector3.up * 0.02f, R(1.6f, 2.4f), 0.03f, Moss);
                Town.Reserve(c, 1.6f);
                trees++;
            }

            // 池を向いた長椅子（座れる）。となりに小さな行灯
            int benches = 0;
            foreach (var (c, yaw) in new[]
            {
                (new Vector2(pond.center.x - 10f, pond.yMin - 3.2f), 0f), (new Vector2(pond.center.x + 10f, pond.yMin - 3.2f), 0f),
                (new Vector2(pond.center.x - 2.5f, pond.yMin - 3.4f), 0f),
                (new Vector2(pond.xMax + 3.2f, pond.center.y - 3f), -90f), (new Vector2(pond.xMax + 3.2f, pond.center.y + 4.5f), -90f),
                (new Vector2(pond.xMin - 3.2f, pond.center.y + 3f), 90f), (new Vector2(pond.xMin - 3.2f, pond.center.y - 4.5f), 90f),
            })
            {
                if (!Fits(c, 1.3f)) continue;
                Bench(g, new Vector3(c.x, Island.Height(c.x, c.y), c.y), yaw);
                Town.Reserve(c, 1.4f);
                benches++;
            }

            // 飛び石（池のまわりをひとまわり。置けない所はとばす）
            var stone = Lit("stepping_stone", new Color(0.62f, 0.6f, 0.56f), 0.2f);
            int stones = 0;
            float ax = pond.width / 2 + 2.1f, az = pond.height / 2 + 2.1f;
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2;
                var c = pond.center + new Vector2(Mathf.Cos(a) * ax, Mathf.Sin(a) * az);
                if (Town.OnAnyStreet(c, 0.2f) || TerrainBuilder.NearStairs(c, 0.4f) || Island.NearestWater(c.x, c.y) < 0.9f) continue;
                if (Physics.CheckBox(new Vector3(c.x, L1 + 0.8f, c.y), new Vector3(0.35f, 0.5f, 0.35f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                var st = Cyl(g, "飛び石", new Vector3(c.x, Island.Height(c.x, c.y) + 0.03f, c.y), R(0.32f, 0.45f), 0.06f, stone);
                st.transform.localScale = Vector3.Scale(st.transform.localScale, new Vector3(1f, 1f, R(0.75f, 1f)));
                st.transform.localRotation = Quaternion.Euler(0, R(0, 180), 0);
                stones++;
            }

            // 鹿威しとつくばい（池のかど）
            foreach (var c in new[] { new Vector2(pond.xMax + 1.8f, pond.yMax + 1.6f), new Vector2(pond.xMin - 1.8f, pond.yMax + 1.6f), new Vector2(pond.xMax + 1.8f, pond.yMin - 1.6f) })
            {
                if (!Fits(c, 0.9f, true)) continue;
                Shishi(g, deco, new Vector3(c.x, Island.Height(c.x, c.y), c.y), Mathf.Atan2(pond.center.x - c.x, pond.center.y - c.y) * Mathf.Rad2Deg);
                Town.Reserve(c, 1f);
                n++;
                break;
            }

            // 大物番付の立て札（ワープしてくる所の近く、池の南）
            foreach (var c in new[] { new Vector2(pond.center.x + 4.5f, pond.yMin - 4.5f), new Vector2(pond.center.x - 4.5f, pond.yMin - 4.5f), new Vector2(pond.center.x + 7f, pond.yMin - 2.6f) })
            {
                if (!Fits(c, 1.1f, true)) continue;
                Board(g, new Vector3(c.x, Island.Height(c.x, c.y), c.y), 180f);
                Town.Reserve(c, 1.2f);
                n++;
                break;
            }

            // 夜の蛍
            var ff = new GameObject("釣り堀の蛍");
            ff.transform.SetParent(deco, false);
            ff.transform.position = new Vector3(pond.center.x, WY + 1.2f, pond.center.y);
            var ps = ff.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            main.startColor = new Color(0.75f, 1f, 0.4f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(pond.width + 6f, 1.5f, pond.height + 6f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.4f;
            ff.GetComponent<ParticleSystemRenderer>().sharedMaterial = Shaded("spark", "Tsukuyomi/Spark");
            ff.AddComponent<NightParticles>().rate = 7f;

            // 茶屋を甘味の店に（品ぞろえは甘味処と同じ）
            if (teahouse != null) TeaShop(teahouse, teaW, teaD);

            Rng = keep;
            Debug.Log($"[Tsukuyomi] pond garden: decks {decks.Count}, pads {pads}, trees {trees}, benches {benches}, stones {stones}, extras {n}");
        }

        /// <summary>釣り台：岸から池へ 3m ほどせり出した木の台（岸がわに1段の踏み台）。</summary>
        static void Deck(Transform g, Vector2 edge, Vector2 dir)
        {
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            var d = Group(g, "釣り台", new Vector3(edge.x, L1, edge.y), yaw);
            const float W = 2.2f, Len = 3.4f;
            float top = 0.5f;
            Box(d, "床", new Vector3(0, top - 0.1f, Len / 2 - 0.5f), new Vector3(W, 0.2f, Len), Wood, true);
            for (int i = 0; i < 6; i++) Box(d, "板の目", new Vector3(0, top + 0.002f, -0.45f + i * 0.55f), new Vector3(W - 0.05f, 0.005f, 0.03f), WoodDark);
            Box(d, "踏み台", new Vector3(0, 0.12f, -0.85f), new Vector3(W, 0.25f, 0.6f), Wood, true);
            foreach (float x in new[] { -W / 2 + 0.12f, W / 2 - 0.12f })
                foreach (float z in new[] { 0.9f, Len - 0.7f })
                    Cyl(d, "脚", new Vector3(x, -0.5f, z), 0.09f, 2f, WoodDark);
            // 先の両わきに低い杭と、魚籠のかわりの手おけ
            foreach (float x in new[] { -W / 2 + 0.1f, W / 2 - 0.1f })
                Cyl(d, "杭", new Vector3(x, top + 0.25f, Len - 0.6f), 0.07f, 0.5f, WoodDark);
            var b = Clutter.Prop(d, "wooden_bucket_02", d.TransformPoint(new Vector3(W / 2 - 0.35f, top, 0.1f)), yaw + 30f);
            if (b != null) b.name = "手おけ";
        }

        static void Willow(Transform g, Vector3 at, float s) => Parts.Willow(g, at, s);

        /// <summary>紅葉（赤と橙の葉のかたまり）。</summary>
        static void Momiji(Transform p, Vector3 pos, float s)
        {
            var g = Group(p, "紅葉", pos, R(0, 360));
            var bark = Lit("bark", new Color(0.24f, 0.16f, 0.14f), 0.15f);
            var leaves = new[] { Lit("momiji1", new Color(0.85f, 0.18f, 0.1f), 0.1f), Lit("momiji2", new Color(0.95f, 0.45f, 0.12f), 0.1f), Lit("momiji3", new Color(0.75f, 0.1f, 0.15f), 0.1f) };
            Cyl(g, "幹", new Vector3(0, 1.6f * s, 0), 0.18f * s, 3.2f * s, bark, true);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.7f;
                float r = i == 0 ? 0 : R(0.8f, 1.8f) * s;
                Ball(g, "葉", new Vector3(Mathf.Cos(a) * r, (3.4f + R(-0.4f, 0.9f)) * s, Mathf.Sin(a) * r), new Vector3(R(1.6f, 2.4f), R(1.1f, 1.6f), R(1.6f, 2.4f)) * s, leaves[i % 3]);
            }
        }

        /// <summary>背もたれつきの長椅子（座ると local +z ＝池のほうを向く）と行灯。</summary>
        static void Bench(Transform g, Vector3 pos, float yaw)
        {
            var b = Group(g, "長椅子", pos, yaw);
            Box(b, "座面", new Vector3(0, 0.44f, 0), new Vector3(2.2f, 0.08f, 0.55f), Wood, true);
            Box(b, "毛氈", new Vector3(0, 0.49f, 0), new Vector3(2.2f, 0.02f, 0.57f), Felt);
            Box(b, "背もたれ", new Vector3(0, 0.82f, -0.3f), new Vector3(2.2f, 0.5f, 0.06f), Wood, false, new Vector3(-8, 0, 0));
            foreach (float x in new[] { -0.95f, 0.95f })
                foreach (float z in new[] { -0.22f, 0.22f })
                    Box(b, "脚", new Vector3(x, 0.2f, z), new Vector3(0.07f, 0.4f, 0.07f), WoodDark);
            var andon = Group(b, "行灯", new Vector3(1.55f, 0, 0.1f));
            Box(andon, "台", new Vector3(0, 0.25f, 0), new Vector3(0.12f, 0.5f, 0.12f), WoodDark);
            Box(andon, "火袋", new Vector3(0, 0.75f, 0), new Vector3(0.36f, 0.5f, 0.36f), GlowWhite);
            Box(andon, "笠", new Vector3(0, 1.03f, 0), new Vector3(0.44f, 0.06f, 0.44f), WoodDark);
            var seat = b.gameObject.AddComponent<Seat>();
            seat.yaw = yaw;
            seat.spots = new[] { b.TransformPoint(new Vector3(-0.5f, 0.5f, 0.02f)), b.TransformPoint(new Vector3(0.5f, 0.5f, 0.02f)) };
        }

        /// <summary>鹿威し（竹の筒は動くので deco に）とつくばい。yaw は池のほう。</summary>
        static void Shishi(Transform g, Transform deco, Vector3 pos, float yaw)
        {
            var root = Group(g, "つくばい", pos, yaw);
            Cyl(root, "手水鉢", new Vector3(0, 0.3f, 0.6f), 0.4f, 0.6f, Stone, true);
            Cyl(root, "水", new Vector3(0, 0.58f, 0.6f), 0.32f, 0.02f, Lit("basin_water", new Color(0.15f, 0.35f, 0.4f), 0.95f));
            Ball(root, "役石", new Vector3(0.6f, 0.15f, 0.3f), new Vector3(0.5f, 0.3f, 0.4f), Stone);
            Ball(root, "役石", new Vector3(-0.55f, 0.12f, 0.5f), new Vector3(0.4f, 0.25f, 0.35f), Stone);
            Cyl(root, "筧", new Vector3(0, 0.95f, 0.15f), 0.04f, 0.9f, Bamboo, false, new Vector3(80, 0, 0));
            Box(root, "受け石", new Vector3(0, 0.1f, -0.75f), new Vector3(0.3f, 0.2f, 0.3f), Stone);
            foreach (float x in new[] { -0.18f, 0.18f })
                Cyl(root, "支え", new Vector3(x, 0.35f, -0.2f), 0.035f, 0.7f, Bamboo);
            var pivot = Group(deco, "鹿威し", root.TransformPoint(new Vector3(0, 0.62f, -0.2f)), yaw);
            var tube = Group(pivot, "竹筒", Vector3.zero);
            Cyl(tube, "筒", new Vector3(0, 0, 0.05f), 0.07f, 1.3f, Bamboo, false, new Vector3(90, 0, 0));
            Cyl(tube, "節", new Vector3(0, 0, -0.3f), 0.075f, 0.04f, Lit("bamboo_node", new Color(0.36f, 0.5f, 0.24f), 0.3f), false, new Vector3(90, 0, 0));
            var so = pivot.gameObject.AddComponent<ShishiOdoshi>();
            so.tube = tube;
        }

        /// <summary>大物番付の立て札。正面は local +z。</summary>
        static void Board(Transform g, Vector3 pos, float yaw)
        {
            var b = Group(g, "大物番付の立て札", pos, yaw);
            foreach (float x in new[] { -0.9f, 0.9f }) Cyl(b, "柱", new Vector3(x, 1.1f, 0), 0.07f, 2.2f, WoodDark, true);
            Box(b, "板", new Vector3(0, 1.45f, 0.02f), new Vector3(1.9f, 1.1f, 0.06f), Wood);
            Box(b, "紙", new Vector3(0, 1.45f, 0.06f), new Vector3(1.7f, 0.95f, 0.02f), Paper);
            for (int i = 0; i < 6; i++) Box(b, "字", new Vector3(0.65f - i * 0.26f, 1.48f, 0.075f), new Vector3(0.04f, 0.7f, 0.01f), Black);
            Box(b, "屋根", new Vector3(0, 2.15f, 0.02f), new Vector3(2.3f, 0.08f, 0.5f), RoofBark, false, new Vector3(0, 0, 0));
            Box(b, "赤い札", new Vector3(0, 1.98f, 0.08f), new Vector3(0.8f, 0.14f, 0.02f), GlowRed);
            var rb = b.gameObject.AddComponent<RankBoard>();
            var p = b.TransformPoint(new Vector3(0, 0, 1.3f));
            p.y = Island.Height(p.x, p.z) + 0.03f;
            rb.stand = p;
            rb.sign = b.TransformPoint(new Vector3(0, 3.1f, 0));
        }

        /// <summary>茶屋：甘味処と同じ品ぞろえの店にする（正面は local +z）。</summary>
        static void TeaShop(Transform shop, float w, float dep)
        {
            var st = shop.gameObject.AddComponent<ShopStand>();
            st.id = "kanmi_teahouse"; st.group = "kanmi";
            st.title = "月見茶屋"; st.english = "TEA HOUSE"; st.kind = ItemKind.Food;
            st.hello = "いらっしゃい。ゆっくりしていってね";
            Vector3 Wp(Vector3 l) { var p = shop.TransformPoint(l); p.y = Island.Height(p.x, p.z) + 0.03f; return p; }
            st.stand = Wp(new Vector3(0, 0, dep / 2 + 1.6f));
            st.standYaw = shop.eulerAngles.y + 180f;
            st.keeper = Wp(new Vector3(-w / 2 + 1.1f, 0, dep / 2 + 0.35f));
            st.keeperYaw = shop.eulerAngles.y + 25f;
            st.sign = shop.TransformPoint(new Vector3(0, 4.9f, dep / 2 + 0.6f));
        }
    }
}
