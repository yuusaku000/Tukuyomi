using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 空の遊覧船「月の舟」と、海の桟橋・切符売り場（海と月見台）。
    ///   船体：三日月の弧（へさきとともが月の角のように反り上がる）。月白と金のふち
    ///   甲板：木の床・金の高欄・小さな屋形・光の帆と三日月の紋・提灯の綱
    ///   船底：光る青海波の模様と、下を照らす明かり（下から見上げても楽しい）
    /// 船は原点で組み立ててから、部品をまとめて（軽くするため）桟橋へ動かす。
    /// </summary>
    public static class ShipBuilder
    {
        const float G = Island.Ground;

        public static void Build(Transform env, Transform deco, Transform lookDeck, GameObject lookGate)
        {
            var keep = Rng;
            Rng = new System.Random(7700);

            // ───── 船 ─────
            var ship = new GameObject("遊覧船「月の舟」").transform;
            var hullMat = Lit("ship_moon_hull", new Color(0.93f, 0.9f, 0.8f), 0.6f, 0.25f);
            var trim = Gold;
            const float R = 12f, CY = 9.5f;
            for (int i = 0; i <= 60; i++)
            {
                float a = Mathf.Lerp(-76f, 76f, i / 60f);
                float ar = a * Mathf.Deg2Rad, f = Mathf.Abs(a) / 76f;
                var p = new Vector3(0, CY - R * Mathf.Cos(ar), R * Mathf.Sin(ar));
                float thick = Mathf.Lerp(2.6f, 0.45f, f), w = Mathf.Lerp(5.6f, 0.7f, Mathf.Pow(f, 1.4f));
                Box(ship, "船体", p, new Vector3(w, thick, 0.85f), hullMat, false, new Vector3(-a, 0, 0));
                Box(ship, "金のふち", p + new Vector3(0, thick / 2 * Mathf.Cos(ar) - 0.02f, -thick / 2 * Mathf.Sin(ar)), new Vector3(w + 0.12f, 0.12f, 0.86f), trim, false, new Vector3(-a, 0, 0));
                // 船底の光る青海波（弧の外がわ＝下面に）
                if (f < 0.7f && i % 4 == 0)
                {
                    var under = p - new Vector3(0, thick / 2 * Mathf.Cos(ar) + 0.03f, -thick / 2 * Mathf.Sin(ar));
                    foreach (float x in new[] { -1.6f, 0f, 1.6f })
                    {
                        if (Mathf.Abs(x) > w / 2 - 0.5f) continue;
                        for (int k = 0; k < 7; k++)
                        {
                            float t = Mathf.PI * k / 6f;
                            var arc = under + new Vector3(x + Mathf.Cos(t) * 0.7f, 0, Mathf.Sin(t) * 0.7f - 0.35f);
                            var seg = Box(ship, "青海波", arc, new Vector3(0.36f, 0.05f, 0.08f), (i / 4 + k) % 3 == 0 ? GlowPink : GlowCyan);
                            seg.transform.localRotation = Quaternion.Euler(-a, -t * Mathf.Rad2Deg + 90f, 0);
                        }
                    }
                }
            }
            // 月の角の先に光る玉
            foreach (float s in new[] { -1f, 1f })
            {
                float ar = 76f * Mathf.Deg2Rad;
                Ball(ship, "角の玉", new Vector3(0, CY - R * Mathf.Cos(ar) + 0.4f, s * R * Mathf.Sin(ar)), Vector3.one * 0.9f, GlowGold);
            }
            // 船腹（甲板の下を埋める）と甲板
            Box(ship, "船腹", new Vector3(0, -0.9f, 0), new Vector3(5.2f, 1.6f, 14.5f), hullMat);
            Box(ship, "甲板", new Vector3(0, -0.12f, 0), new Vector3(5.2f, 0.25f, 15.6f), Wood, true);
            for (int k = 0; k < 9; k++) Box(ship, "板の目", new Vector3(-2.2f + k * 0.55f, 0.005f, 0), new Vector3(0.03f, 0.01f, 15.4f), WoodDark);
            // 高欄（左舷のまんなかは乗り口）と見えない柵
            foreach (float sx in new[] { -1f, 1f })
                foreach (var (z0, z1) in sx < 0 ? new[] { (-7.7f, -1.3f), (1.3f, 7.7f) } : new[] { (-7.7f, 7.7f) })
                {
                    float zc = (z0 + z1) / 2, zl = z1 - z0;
                    Box(ship, "高欄", new Vector3(sx * 2.5f, 0.95f, zc), new Vector3(0.1f, 0.1f, zl), trim);
                    Box(ship, "高欄", new Vector3(sx * 2.5f, 0.5f, zc), new Vector3(0.06f, 0.06f, zl), trim);
                    for (float z = z0; z <= z1 + 0.01f; z += 1.6f) Cyl(ship, "高欄の柱", new Vector3(sx * 2.5f, 0.5f, z), 0.05f, 1f, trim);
                    Box(ship, "見えない柵", new Vector3(sx * 2.55f, 0.75f, zc), new Vector3(0.25f, 1.5f, zl), Stone, true).GetComponent<Renderer>().enabled = false;
                }
            foreach (float sz in new[] { -1f, 1f })
                Box(ship, "見えない柵", new Vector3(0, 0.75f, sz * 7.75f), new Vector3(5.2f, 1.5f, 0.25f), Stone, true).GetComponent<Renderer>().enabled = false;
            var gate = Box(ship, "乗り口の柵", new Vector3(-2.55f, 0.75f, 0), new Vector3(0.25f, 1.5f, 2.6f), Stone, true);
            gate.GetComponent<Renderer>().enabled = false;
            foreach (float z in new[] { -1.3f, 1.3f }) Cyl(ship, "乗り口の柱", new Vector3(-2.5f, 1.1f, z), 0.08f, 2.2f, Vermilion);
            Box(ship, "乗り口の飾り", new Vector3(-2.5f, 2.25f, 0), new Vector3(0.15f, 0.25f, 2.9f), GlowGold);
            // 屋形（とものほう）
            var cabin = Group(ship, "屋形", new Vector3(0, 0, -4.6f));
            Box(cabin, "壁", new Vector3(0, 1.1f, 0), new Vector3(2.8f, 2.2f, 4.2f), Buildings.RedLacquer, true);
            foreach (float sx in new[] { -1f, 1f })
                for (int k = 0; k < 3; k++) Box(cabin, "窓", new Vector3(sx * 1.42f, 1.3f, -1.4f + k * 1.4f), new Vector3(0.05f, 0.8f, 1f), Buildings.WindowGlow);
            MeshObj(cabin, "屋根", Roof(3.8f, 5.2f, 1.4f, 0.6f), new Vector3(0, 2.2f, 0), RoofCopper);
            for (int k = 0; k < 4; k++) Chochin(cabin, new Vector3(-1.5f + k, 2.0f, 2.35f), k % 2 == 0 ? GlowRed : GlowWhite);
            // 帆柱と光の帆（三日月の紋）
            Cyl(ship, "帆柱", new Vector3(0, 4f, 1.8f), 0.14f, 8f, WoodDark);
            Box(ship, "帆げた", new Vector3(0, 7.3f, 1.8f), new Vector3(0.12f, 0.12f, 5.4f), trim);
            var sail = Landmarks.Holo("holo_ship_sail", new Color(0.6f, 0.85f, 1f), 0.4f, 1.6f, 0.15f);
            Box(ship, "光の帆", new Vector3(0, 4.9f, 1.8f), new Vector3(0.05f, 4.6f, 5f), sail);
            for (int k = 0; k < 14; k++)
            {
                float t = Mathf.Lerp(-1.1f, 1.1f, k / 13f) + Mathf.PI / 2;
                Box(ship, "三日月の紋", new Vector3(0.06f, 5f + Mathf.Sin(t) * 1.3f, 1.8f + Mathf.Cos(t) * 1.3f), new Vector3(0.03f, 0.3f, 0.3f), GlowGold);
                Box(ship, "三日月の紋", new Vector3(-0.06f, 5f + Mathf.Sin(t) * 1.3f, 1.8f + Mathf.Cos(t) * 1.3f), new Vector3(0.03f, 0.3f, 0.3f), GlowGold);
            }
            // 帆柱の先から、へさきとともの角へ提灯の綱
            var top = new Vector3(0, 8f, 1.8f);
            foreach (float s in new[] { -1f, 1f })
            {
                float ar = 70f * Mathf.Deg2Rad;
                var tip = new Vector3(0, CY - R * Mathf.Cos(ar) + 0.3f, s * R * Mathf.Sin(ar));
                for (int k = 1; k < 8; k++)
                {
                    float u = k / 8f;
                    var p = Vector3.Lerp(top, tip, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * 1.2f;
                    Chochin(ship, p + Vector3.down * 0.35f, k % 2 == 0 ? GlowRed : GlowGold);
                }
            }
            // 甲板の長椅子（座るのは止まっているときの見物用。船が動くので Seat はつけない）
            foreach (float sx in new[] { -1.7f, 1.7f })
            {
                Box(ship, "長椅子", new Vector3(sx, 0.42f, 4.6f), new Vector3(0.6f, 0.08f, 2.4f), Wood, true);
                Box(ship, "毛氈", new Vector3(sx, 0.47f, 4.6f), new Vector3(0.62f, 0.02f, 2.42f), Lit("felt_red", new Color(0.75f, 0.1f, 0.12f), 0.05f));
            }
            // 下を照らす明かり
            Lamp(ship, new Vector3(0, -3.5f, 0), new Color(0.5f, 0.85f, 1f), 30f, 2.5f);
            Lamp(ship, new Vector3(0, 3f, 0), new Color(1f, 0.75f, 0.5f), 14f, 1.6f);

            // 光の粒のしっぽ（飛んでいる間だけ）
            var tr = new GameObject("光の粒");
            tr.transform.SetParent(ship, false);
            tr.transform.localPosition = new Vector3(0, -1.5f, -9f);
            var ps = tr.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = new Color(0.7f, 0.9f, 1f);
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 50f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(3f, 1f, 2f);
            tr.GetComponent<ParticleSystemRenderer>().sharedMaterial = Shaded("spark", "Tsukuyomi/Spark");
            ps.Play();

            // 軽くする：同じ色の部品をまとめる（当たり判定は残る）
            Combiner.Run(ship, 1000f);
            ship.SetParent(deco, true);

            // ───── 海の桟橋（回廊の東 z -40 から東へ）─────
            var pier = Group(env, "遊覧船の桟橋", Vector3.zero);
            const float pz = -40f, px0 = 3.5f, px1 = 26.6f;
            Box(pier, "桟橋", new Vector3((px0 + px1) / 2, G - 0.2f, pz), new Vector3(px1 - px0, 0.4f, 3f), Wood, true);
            foreach (float sz in new[] { -1f, 1f })
            {
                Box(pier, "高欄", new Vector3((px0 + px1) / 2, G + 0.9f, pz + sz * 1.45f), new Vector3(px1 - px0, 0.1f, 0.1f), Vermilion);
                Box(pier, "見えない柵", new Vector3((px0 + px1) / 2, G + 0.7f, pz + sz * 1.5f), new Vector3(px1 - px0, 1.4f, 0.25f), Stone, true).GetComponent<Renderer>().enabled = false;
                for (float x = px0 + 1f; x < px1; x += 3f)
                {
                    Cyl(pier, "脚", new Vector3(x, -0.9f, pz + sz * 1.4f), 0.18f, 3.4f, Vermilion);
                    if (((int)x) % 2 == 0) { Cyl(pier, "灯の柱", new Vector3(x, G + 1.2f, pz + sz * 1.45f), 0.05f, 2.4f, WoodDark); Chochin(pier, new Vector3(x, G + 2.3f, pz + sz * 1.45f), GlowWhite); }
                }
            }
            var seaBooth = Booth(pier, new Vector3(9f, G, pz - 2.6f), 0f, false);
            Box(pier, "光の線", new Vector3((px0 + px1) / 2, G + 0.02f, pz), new Vector3(px1 - px0, 0.02f, 0.1f), GlowCyan);

            // ───── 月見台の切符売り場 ─────
            Vector3 lookDock = Vector3.zero; float lookYaw = 0;
            if (lookDeck != null)
            {
                Booth(lookDeck, lookDeck.TransformPoint(new Vector3(4.6f, 0, 2.6f)), lookDeck.eulerAngles.y + 180f, true);
                lookYaw = lookDeck.eulerAngles.y - 90f;
                lookDock = lookDeck.TransformPoint(new Vector3(0, 0, 4.6f + 2.75f));
            }

            var sky = ship.gameObject.AddComponent<SkyShip>();
            sky.seaDock = new Vector3(px1 + 2.75f, G, pz);
            sky.seaYaw = 0f;
            sky.lookDock = lookDock; sky.lookYaw = lookYaw;
            sky.lookGate = lookGate; sky.shipGate = gate;
            sky.trail = ps;
            ship.SetPositionAndRotation(sky.seaDock, Quaternion.identity);
            Rng = keep;
            Debug.Log($"[Tsukuyomi] sky ship: sea dock {sky.seaDock}, look dock {lookDock} yaw {lookYaw:0}");
        }

        /// <summary>切符売り場（小さな屋根と看板）。正面は local +z。</summary>
        static Transform Booth(Transform parent, Vector3 worldPos, float yaw, bool look)
        {
            var b = new GameObject("遊覧船の切符売り場").transform;
            b.SetParent(parent, true);
            b.SetPositionAndRotation(worldPos, Quaternion.Euler(0, yaw, 0));
            Box(b, "台", new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1f, 0.7f), Wood, true);
            foreach (float x in new[] { -0.75f, 0.75f }) Cyl(b, "柱", new Vector3(x, 1.25f, -0.3f), 0.05f, 2.5f, Vermilion);
            Box(b, "屋根", new Vector3(0, 2.55f, 0), new Vector3(2.1f, 0.1f, 1.3f), RoofCopper, false, new Vector3(-10, 0, 0));
            Box(b, "看板", new Vector3(0, 1.9f, -0.32f), new Vector3(1.4f, 0.6f, 0.06f), GlowGold);
            Ball(b, "月", new Vector3(0, 1.9f, -0.26f), new Vector3(0.4f, 0.4f, 0.04f), GlowWhite);
            var bo = b.gameObject.AddComponent<ShipBooth>();
            var st = b.TransformPoint(new Vector3(0, 0, 1.3f));
            bo.stand = st;
            bo.sign = b.TransformPoint(new Vector3(0, 3.3f, 0));
            bo.atLookout = look;
            return b;
        }
    }
}
