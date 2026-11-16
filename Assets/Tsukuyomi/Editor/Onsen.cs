using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 中段の西の空き地に、雰囲気重視の露天風呂「月見の湯」。
    ///   岩でかこんだ乳白色のお湯・湯けむり・竹垣・暖簾の湯屋・石灯籠と行灯・お湯の上の提灯・紅葉と松・湯桶
    ///   お湯のふちで E → つかる（座るのと同じしくみ）。お金はかからない
    /// 通り・石段・水・ほかの物とぶつからない所を、まわりから探して置く。町の並びが変わらないように、自分専用の乱数を使う。
    /// </summary>
    public static class Onsen
    {
        const float W = 15f, D = 12f;

        static int Fits(Vector2 c, float rotY, float level)
        {
            var rot = Quaternion.Euler(0, rotY, 0);
            int bad = 0;
            for (float x = -W / 2; x <= W / 2; x += 1f)
                for (float z = -D / 2; z <= D / 2; z += 1f)
                {
                    var o = rot * new Vector3(x, 0, z);
                    var q = c + new Vector2(o.x, o.z);
                    if (TerrainBuilder.NearStairs(q, 0.8f) || Island.NearestWater(q.x, q.y) < 1.5f) return 999;
                    if (Mathf.Abs(Island.TerraceHeight(q.x, q.y) - level) > 0.2f || Island.Inside(q.x, q.y) < 4f) return 999;
                    if (Town.OnAnyStreet(q, -0.2f)) bad++;   // 道にかかる点の数
                }
            if (!Town.Free(c, 3f)) return 999;
            Physics.SyncTransforms();
            return Physics.CheckBox(new Vector3(c.x, level + 1.6f, c.y), new Vector3(W / 2, 1.2f, D / 2), rot, ~0, QueryTriggerInteraction.Ignore) ? 999 : bad;
        }

        /// <summary>置けたら中心（と、正面の向き）を返す。</summary>
        public static bool Build(Transform env, Transform deco, Vector2 want, out Vector3 center, out float yaw)
        {
            center = default; yaw = 0;
            float level = Island.LevelTop(1);
            // 道にまったくかからない所をさがす。なければ、かかる所がいちばん少ない所（道のはしを少しふさぐだけ）
            Vector2 c = want; int best = 999;
            for (float r = 0; r <= 30f && best > 0; r += 2f)
                for (float a = 0; a < 360f && best > 0; a += r == 0 ? 360f : 20f)
                    foreach (float ry in new[] { 0f, 90f, 180f, 270f, 45f, 135f, 225f, 315f })
                    {
                        var q = want + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                        int bad = Fits(q, ry, level);
                        if (bad < best) { best = bad; c = q; yaw = ry; if (bad == 0) break; }
                    }
            if (best >= 999) { Debug.LogWarning($"[Tsukuyomi] onsen: no room near {want} (best {best})"); return false; }
            Debug.Log($"[Tsukuyomi] onsen: street points covered {best}");
            var keep = Rng;
            Rng = new System.Random(5150);
            float y = Island.Height(c.x, c.y);
            var g = Group(env, "月見の湯", new Vector3(c.x, y, c.y), yaw);
            var moving = Group(deco, "月見の湯の湯けむり", new Vector3(c.x, y, c.y), yaw);
            moving.gameObject.AddComponent<NoWalkArea>().size = new Vector2(W, D); // 道の上にかかっても、町の人は入らない

            // 石の床（湯船のまわり）
            var flag = Lit("onsen_flagstone", new Color(0.5f, 0.48f, 0.45f), 0.35f);
            Box(g, "石の床", new Vector3(0, 0.04f, 0), new Vector3(W - 1f, 0.08f, D - 1f), flag);
            // 湯船：だ円の岩組みと、乳白色のお湯
            const float ax = 4.6f, az = 3.1f;
            var water = Shaded("water_onsen", "Tsukuyomi/Water");
            water.SetColor("_Deep", new Color(0.35f, 0.62f, 0.62f)); water.SetColor("_Shallow", new Color(0.72f, 0.88f, 0.86f));
            water.SetFloat("_Scale", 0.5f); water.SetFloat("_Speed", 0.15f); water.SetFloat("_Alpha", 0.9f); water.SetFloat("_Grid", 0f);
            var pc = new Vector3(0, 0, -0.8f);
            var wq = Prim(PrimitiveType.Quad, g, "お湯", pc + new Vector3(0, 0.42f, 0), new Vector3(ax * 2f, az * 2f, 1), water, false, new Vector3(90, 0, 0));
            wq.transform.localScale = new Vector3(ax * 2f - 0.3f, az * 2f - 0.3f, 1);
            Cyl(g, "湯船の底", pc + new Vector3(0, 0.12f, 0), 1f, 0.2f, Lit("onsen_bottom", new Color(0.3f, 0.36f, 0.36f), 0.3f)).transform.localScale = new Vector3(ax * 2f, 0.1f, az * 2f);
            var rock = Lit("onsen_rock", new Color(0.36f, 0.34f, 0.32f), 0.45f);
            var rock2 = Lit("onsen_rock2", new Color(0.46f, 0.43f, 0.4f), 0.4f);
            int nr = 30;
            for (int i = 0; i < nr; i++)
            {
                float a = i * Mathf.PI * 2 / nr;
                var p = pc + new Vector3(Mathf.Cos(a) * ax, 0.3f, Mathf.Sin(a) * az);
                Ball(g, "岩", p, new Vector3(R(0.9f, 1.4f), R(0.55f, 0.85f), R(0.8f, 1.2f)), i % 3 == 0 ? rock2 : rock).transform.localRotation = Quaternion.Euler(0, R(0, 180), 0);
            }
            // 奥の大きな岩と、お湯の落ちる口
            Ball(g, "大岩", pc + new Vector3(-2.2f, 0.9f, -az - 0.6f), new Vector3(2.6f, 2.2f, 1.8f), rock);
            Ball(g, "大岩", pc + new Vector3(0.6f, 0.7f, -az - 0.8f), new Vector3(1.8f, 1.6f, 1.5f), rock2);
            Box(g, "湯口の竹", pc + new Vector3(-1.2f, 1.3f, -az + 0.2f), new Vector3(0.12f, 0.12f, 1.2f), Lit("bamboo_green", new Color(0.45f, 0.62f, 0.3f), 0.35f), false, new Vector3(-20, 0, 0));
            // つかれない所に入らないよう、岩のまわりに見えない柵
            for (int i = 0; i < 16; i++)
            {
                float a0 = i * Mathf.PI * 2 / 16, a1 = (i + 1) * Mathf.PI * 2 / 16;
                var p0 = pc + new Vector3(Mathf.Cos(a0) * ax, 0, Mathf.Sin(a0) * az);
                var p1 = pc + new Vector3(Mathf.Cos(a1) * ax, 0, Mathf.Sin(a1) * az);
                var f = Box(g, "見えない柵", (p0 + p1) / 2 + Vector3.up * 0.6f, new Vector3(0.4f, 1.2f, Vector3.Distance(p0, p1) + 0.1f), Stone, true);
                f.transform.localRotation = Quaternion.LookRotation(p1 - p0);
                f.GetComponent<Renderer>().enabled = false;
            }
            // つかる場所（お湯の内がわ、まん中を向く）
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2 / 8;
                var lp = pc + new Vector3(Mathf.Cos(a) * (ax - 0.9f), 0.4f, Mathf.Sin(a) * (az - 0.8f));
                var sgo = Group(g, "湯につかる所", lp, 0);
                var seat = sgo.gameObject.AddComponent<Seat>();
                var inward = pc - lp; inward.y = 0;
                var wdir = g.TransformDirection(inward);
                seat.yaw = Mathf.Atan2(wdir.x, wdir.z) * Mathf.Rad2Deg;
                seat.spots = new[] { sgo.position };
                seat.standBehind = true;
                seat.sitText = "温泉につかりました。あったか〜い…（E・WASD で出る）";
            }
            // 湯けむり（いつも少し、夜はよく見える）
            for (int k = 0; k < 2; k++)
            {
                var st = new GameObject("湯けむり");
                st.transform.SetParent(moving, false);
                st.transform.localPosition = pc + new Vector3(k == 0 ? -1.5f : 1.8f, 0.5f, 0);
                var ps = st.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
                main.startColor = new Color(0.55f, 0.6f, 0.62f, 0.22f);
                main.maxParticles = 60;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission; em.rateOverTime = 8f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(4f, 0.2f, 3f);
                var col = ps.colorOverLifetime; col.enabled = true;
                var gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                           new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(0, 1) });
                col.color = gr;
                var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
                st.GetComponent<ParticleSystemRenderer>().sharedMaterial = Shaded("spark", "Tsukuyomi/Spark");
                ps.Play();
            }
            // 竹垣（うしろと両わき。正面 +z はあける）
            var bamboo = Lit("bamboo_fence", new Color(0.62f, 0.55f, 0.32f), 0.3f);
            var cord = Lit("fence_cord", new Color(0.2f, 0.12f, 0.08f), 0.2f);
            void Fence(Vector3 a, Vector3 b)
            {
                float len = Vector3.Distance(a, b);
                int n = Mathf.RoundToInt(len / 0.16f);
                for (int i = 0; i <= n; i++) Cyl(g, "竹", Vector3.Lerp(a, b, i / (float)n) + Vector3.up * 0.95f, 0.07f, 1.9f, bamboo);
                foreach (float h in new[] { 0.5f, 1.4f })
                {
                    var bar = Box(g, "胴縁", (a + b) / 2 + Vector3.up * h, new Vector3(0.06f, 0.08f, len), cord);
                    bar.transform.localRotation = Quaternion.LookRotation(b - a);
                }
                var wall = Box(g, "竹垣の当たり", (a + b) / 2 + Vector3.up * 0.95f, new Vector3(0.25f, 1.9f, len), Stone, true);
                wall.transform.localRotation = Quaternion.LookRotation(b - a);
                wall.GetComponent<Renderer>().enabled = false;
            }
            Fence(new Vector3(-W / 2 + 0.6f, 0, -D / 2 + 0.6f), new Vector3(W / 2 - 0.6f, 0, -D / 2 + 0.6f));
            Fence(new Vector3(W / 2 - 0.6f, 0, -D / 2 + 0.6f), new Vector3(W / 2 - 0.6f, 0, 1.5f));
            Fence(new Vector3(-W / 2 + 0.6f, 0, -D / 2 + 0.6f), new Vector3(-W / 2 + 0.6f, 0, -2f));
            // 湯屋（左の手前）と、大きな暖簾
            var yu = Buildings.TownHouse(g, new Vector3(-5.6f, 0, 4.4f), 0, 5.5f, 3.6f, 1, 0, false, 77);
            yu.name = "湯屋";
            var noren = Lit("onsen_noren", new Color(0.14f, 0.2f, 0.45f), 0.1f);
            for (int i = 0; i < 3; i++)
                Box(yu, "湯の暖簾", new Vector3(-1.4f + i * 1.4f, 2.8f, 2.05f), new Vector3(1.3f, 1.2f, 0.04f), noren);
            Ball(yu, "ゆの字", new Vector3(0, 2.9f, 2.09f), new Vector3(0.7f, 0.7f, 0.02f), Paper);
            Box(yu, "ゆの字", new Vector3(0.05f, 2.9f, 2.11f), new Vector3(0.08f, 0.5f, 0.02f), noren);
            Chochin(yu, new Vector3(-2.4f, 2.9f, 2.3f), GlowRed); Chochin(yu, new Vector3(2.4f, 2.9f, 2.3f), GlowRed);
            // 石灯籠・行灯・お湯の上の提灯
            Toro(g, pc + new Vector3(ax + 1.6f, 0, -1f), true, 0.9f);
            Toro(g, pc + new Vector3(-ax - 1.4f, 0, 0.6f), true, 0.8f);
            foreach (var p in new[] { new Vector3(3.5f, 0, 4.5f), new Vector3(6.5f, 0, 3f), new Vector3(-1f, 0, 5f) })
            {
                Box(g, "行灯の台", p + new Vector3(0, 0.3f, 0), new Vector3(0.12f, 0.6f, 0.12f), WoodDark);
                Box(g, "行灯", p + new Vector3(0, 0.85f, 0), new Vector3(0.4f, 0.55f, 0.4f), GlowWhite);
            }
            var pa = pc + new Vector3(-ax - 0.8f, 0, -az - 0.4f); var pb = pc + new Vector3(ax + 0.8f, 0, -az - 0.4f);
            Cyl(g, "提灯の竿", pa + Vector3.up * 1.9f, 0.06f, 3.8f, WoodDark); Cyl(g, "提灯の竿", pb + Vector3.up * 1.9f, 0.06f, 3.8f, WoodDark);
            for (int i = 1; i < 8; i++)
            {
                float u = i / 8f;
                Chochin(g, Vector3.Lerp(pa, pb, u) + Vector3.up * (3.4f - Mathf.Sin(u * Mathf.PI) * 0.6f), i % 2 == 0 ? GlowRed : GlowWarm);
            }
            Lamp(g, pc + new Vector3(0, 2.5f, 0), new Color(1f, 0.72f, 0.45f), 14f, 1.8f);
            // 紅葉と松・湯桶・縁台
            var leaves = Lit("momiji1", new Color(0.85f, 0.18f, 0.1f), 0.1f);
            var mj = Group(g, "紅葉", new Vector3(W / 2 - 2.2f, 0, -D / 2 + 2.2f));
            Cyl(mj, "幹", new Vector3(0, 1.5f, 0), 0.16f, 3f, Lit("bark", new Color(0.24f, 0.16f, 0.14f), 0.15f), true);
            for (int i = 0; i < 6; i++) Ball(mj, "葉", new Vector3(R(-1.3f, 1.3f), R(3f, 4.2f), R(-1.3f, 1.3f)), new Vector3(R(1.6f, 2.3f), R(1.1f, 1.5f), R(1.6f, 2.3f)), i % 2 == 0 ? leaves : Lit("momiji2", new Color(0.95f, 0.45f, 0.12f), 0.1f));
            Pine(g, new Vector3(-W / 2 + 2f, 0, -D / 2 + 2f), 0.9f);
            for (int i = 0; i < 3; i++)
            {
                var pr = Clutter.Prop(g, i == 1 ? "wooden_bucket_02" : "wooden_bucket_01", g.TransformPoint(pc + new Vector3(ax + 0.9f, 0, 1.4f + i * 0.5f)), R(0, 360), 0.8f);
                if (pr != null) pr.name = "湯桶";
            }
            TeaSeat(g, new Vector3(4.5f, 0, 5.2f), 180f);
            // 風鈴の音とお湯の音の場所
            center = g.TransformPoint(pc + new Vector3(0, 0.5f, 0));
            yaw = g.eulerAngles.y;
            Town.Reserve(c, Mathf.Max(W, D) * 0.6f);
            Rng = keep;
            Debug.Log($"[Tsukuyomi] onsen at ({c.x:0.0},{c.y:0.0}) yaw {yaw:0}");
            return true;
        }
    }
}
