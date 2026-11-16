using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>名所と、雰囲気を出すもの。</summary>
    public static class Landmarks
    {
        public static Material Holo(string name, Color c, float day = 0.35f, float night = 1.6f, float core = 0.12f)
        {
            var m = Shaded(name, "Tsukuyomi/Holo");
            m.SetColor("_Color", c); m.SetFloat("_Day", day); m.SetFloat("_Night", night); m.SetFloat("_Core", core);
            return m;
        }

        /// <summary>五重塔。高さ約34m。</summary>
        public static void Pagoda(Transform parent, Vector3 pos, float rotY)
        {
            var g = Group(parent, "五重塔", pos, rotY);
            Box(g, "基壇", new Vector3(0, 0.2f, 0), new Vector3(12, 1.6f, 12), Stone, true);
            float y = 1f, w = 8.4f;
            for (int i = 0; i < 5; i++)
            {
                float h = i == 0 ? 4.2f : 3.1f;
                Box(g, "層", new Vector3(0, y + h / 2, 0), new Vector3(w, h, w), Vermilion, true);
                Box(g, "白壁", new Vector3(0, y + h * 0.5f, 0), new Vector3(w + 0.06f, h * 0.55f, w * 0.55f), WhiteWall);
                Box(g, "白壁", new Vector3(0, y + h * 0.5f, 0), new Vector3(w * 0.55f, h * 0.55f, w + 0.06f), WhiteWall);
                Box(g, "高欄", new Vector3(0, y + 0.25f, 0), new Vector3(w + 1.2f, 0.2f, w + 1.2f), Vermilion);
                float rw = w + 5.2f - i * 0.4f;
                MeshObj(g, "屋根", Roof(rw, rw, 1.5f, 1.1f), new Vector3(0, y + h - 0.05f, 0), Buildings.Kawara);
                Chochin(g, new Vector3(rw / 2 - 0.6f, y + h - 0.4f, rw / 2 - 0.6f), GlowWarm);
                Chochin(g, new Vector3(-rw / 2 + 0.6f, y + h - 0.4f, -rw / 2 + 0.6f), GlowWarm);
                y += h + 0.9f;
                w -= 0.85f;
            }
            Cyl(g, "相輪", new Vector3(0, y + 4f, 0), 0.22f, 8f, Gold);
            for (int k = 0; k < 9; k++) Cyl(g, "輪", new Vector3(0, y + 1.2f + k * 0.62f, 0), 0.65f - k * 0.03f, 0.12f, Gold);
            Ball(g, "宝珠", new Vector3(0, y + 8.3f, 0), Vector3.one * 0.7f, GlowGold);
            Lamp(g, new Vector3(0, 6, 7), new Color(1f, 0.6f, 0.4f), 16f, 2.5f);
        }

        /// <summary>竹林と、中を通る小道。</summary>
        public static void Bamboo(Transform parent, Vector2 center, float radius, Vector2 pathDir)
        {
            var g = Group(parent, "竹林", Vector3.zero);
            var stalk = Lit("bamboo", new Color(0.42f, 0.62f, 0.3f), 0.45f);
            var leaf = Lit("bamboo_leaf", new Color(0.3f, 0.5f, 0.22f), 0.1f);
            pathDir.Normalize();
            var side = new Vector2(-pathDir.y, pathDir.x);
            for (int i = 0; i < 320; i++)
            {
                var p = center + Random2() * radius;
                float off = Vector2.Dot(p - center, side);
                if (Mathf.Abs(off) < 2.2f) continue; // 小道
                if (Town.OnAnyStreet(p, 0.8f) || Island.NearestWater(p.x, p.y) < 1.5f) continue;
                if (Mathf.Abs(Island.TerraceHeight(p.x, p.y) - Island.LevelTop(2)) > 0.2f) continue;
                float h = R(9f, 15f);
                float y = Island.Height(p.x, p.y);
                var s = Cyl(g, "竹", new Vector3(p.x, y + h / 2, p.y), R(0.07f, 0.12f), h, stalk);
                s.transform.localEulerAngles = new Vector3(R(-4, 4), 0, R(-4, 4));
                if (i % 3 == 0) Ball(g, "笹", new Vector3(p.x, y + h - 0.5f, p.y), new Vector3(R(1.5f, 2.6f), 0.7f, R(1.5f, 2.6f)), leaf);
            }
            for (float t = -radius; t <= radius; t += 5f)
            {
                var p = center + pathDir * t;
                foreach (float sd in new[] { -1f, 1f })
                {
                    var q = p + side * sd * 1.9f;
                    if (Town.OnAnyStreet(q, 0.3f)) continue;
                    var b = Group(g, "竹の灯り", new Vector3(q.x, Island.Height(q.x, q.y), q.y));
                    Cyl(b, "柄", new Vector3(0, 0.4f, 0), 0.08f, 0.8f, stalk);
                    Cyl(b, "灯", new Vector3(0, 0.9f, 0), 0.12f, 0.25f, GlowWarm);
                }
            }
        }

        static Vector2 Random2()
        {
            var a = R(0, Mathf.PI * 2);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Sqrt(R(0, 1));
        }

        /// <summary>清水の舞台のように、崖からせり出した展望台。edge は崖のふち、outward は外向き。</summary>
        public static Vector3 Lookout(Transform parent, Vector2 edge, Vector2 outward)
        {
            outward.Normalize();
            var side = new Vector2(-outward.y, outward.x);
            float top = Island.TerraceHeight(edge.x - outward.x * 4, edge.y - outward.y * 4);
            var g = Group(parent, "懸造りの展望台", Vector3.zero);
            float yaw = Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg;
            const float depth = 14f, width = 22f;
            var c = edge + outward * (depth / 2 - 3f);
            Box(g, "舞台", new Vector3(c.x, top - 0.15f, c.y), new Vector3(width, 0.3f, depth), Wood, true, new Vector3(0, yaw, 0));
            // 下を支える柱の格子
            for (float a = -width / 2 + 1; a <= width / 2 - 1; a += 3.5f)
                for (float b = 0; b <= depth - 1; b += 3.5f)
                {
                    var p = edge + outward * (b - 2.5f) + side * a;
                    float ground = Island.Height(p.x, p.y);
                    if (top - ground < 1f) continue;
                    Cyl(g, "柱", new Vector3(p.x, (top + ground) / 2, p.y), 0.32f, top - ground, Wood);
                    for (float y = ground + 2.5f; y < top - 0.5f; y += 3f)
                    {
                        Box(g, "貫", new Vector3(p.x, y, p.y), new Vector3(0.18f, 0.25f, 3.5f), Wood, false, new Vector3(0, yaw, 0));
                        Box(g, "貫", new Vector3(p.x, y, p.y), new Vector3(3.5f, 0.25f, 0.18f), Wood, false, new Vector3(0, yaw, 0));
                    }
                }
            // 手すり（見えない柵つき）
            var front = edge + outward * (depth - 3f);
            for (int k = -1; k <= 1; k += 2)
            {
                var sp = c + side * k * width / 2;
                Box(g, "高欄", new Vector3(sp.x, top + 0.9f, sp.y), new Vector3(0.14f, 0.12f, depth), Vermilion, false, new Vector3(0, yaw, 0));
                Box(g, "見えない柵", new Vector3(sp.x, top + 0.7f, sp.y), new Vector3(0.3f, 1.4f, depth), Vermilion, true, new Vector3(0, yaw, 0)).GetComponent<Renderer>().enabled = false;
            }
            Box(g, "高欄", new Vector3(front.x, top + 0.9f, front.y), new Vector3(width, 0.12f, 0.14f), Vermilion, false, new Vector3(0, yaw, 0));
            Box(g, "見えない柵", new Vector3(front.x, top + 0.7f, front.y), new Vector3(width, 1.4f, 0.3f), Vermilion, true, new Vector3(0, yaw, 0)).GetComponent<Renderer>().enabled = false;
            for (int k = 0; k <= 8; k++)
            {
                var pp = front + side * (-width / 2 + k * width / 8);
                Cyl(g, "親柱", new Vector3(pp.x, top + 0.5f, pp.y), 0.08f, 1f, Vermilion);
            }
            // 後ろの本堂
            var hall = edge - outward * 6f;
            Buildings.TownHouse(g, new Vector3(hall.x, top, hall.y), yaw, 16f, 9f, 1, 0, false, 3);
            Lamp(g, new Vector3(c.x, top + 2.5f, c.y), new Color(1f, 0.65f, 0.45f), 18f, 2f);
            return new Vector3(front.x - outward.x * 2, top + 0.1f, front.y - outward.y * 2);
        }

        /// <summary>神楽殿（四方が開いた舞の舞台）。</summary>
        public static Transform KaguraDen(Transform parent, Vector3 pos, float rotY)
        {
            var g = Group(parent, "神楽殿", pos, rotY);
            Box(g, "基壇", new Vector3(0, 0.2f, 0), new Vector3(11, 1.4f, 9), Stone, true);
            Box(g, "床", new Vector3(0, 1.0f, 0), new Vector3(9.5f, 0.2f, 7.5f), Wood, true);
            foreach (float x in new[] { -4.5f, -1.5f, 1.5f, 4.5f })
                foreach (float z in new[] { -3.5f, 3.5f })
                    Cyl(g, "柱", new Vector3(x, 2.9f, z), 0.17f, 3.8f, Vermilion);
            Box(g, "梁", new Vector3(0, 4.7f, 3.5f), new Vector3(10f, 0.35f, 0.35f), Vermilion);
            Box(g, "梁", new Vector3(0, 4.7f, -3.5f), new Vector3(10f, 0.35f, 0.35f), Vermilion);
            MeshObj(g, "屋根", Roof(13f, 11f, 3.6f, 1.1f), new Vector3(0, 4.85f, 0), RoofBark);
            Box(g, "幕", new Vector3(0, 4.3f, 3.62f), new Vector3(9.6f, 0.6f, 0.04f), ColorMat(new Color(0.55f, 0.15f, 0.45f)));
            for (int i = 0; i < 6; i++) Chochin(g, new Vector3(-4f + i * 1.6f, 3.8f, 3.85f), i % 2 == 0 ? GlowRed : GlowWhite);
            Lamp(g, new Vector3(0, 3.5f, 0), new Color(1f, 0.7f, 0.45f), 14f, 2.4f);
            return g;
        }

        /// <summary>鐘楼。鐘の場所を返す（毎正時に鳴る）。</summary>
        public static Transform BellTower(Transform parent, Vector3 pos, float rotY)
        {
            var g = Group(parent, "鐘楼", pos, rotY);
            Box(g, "基壇", new Vector3(0, 0.3f, 0), new Vector3(7, 1.6f, 7), Stone, true);
            foreach (float x in new[] { -2.6f, 2.6f })
                foreach (float z in new[] { -2.6f, 2.6f })
                    Cyl(g, "柱", new Vector3(x, 3.6f, z), 0.2f, 5.2f, Vermilion);
            MeshObj(g, "屋根", Roof(8.6f, 8.6f, 3.2f, 0.9f), new Vector3(0, 6.2f, 0), Buildings.Kawara);
            var bronze = Lit("bronze", new Color(0.32f, 0.27f, 0.18f), 0.55f, 0.85f);
            Cyl(g, "鐘", new Vector3(0, 4.2f, 0), 1.0f, 2.2f, bronze);
            Ball(g, "鐘の頭", new Vector3(0, 5.3f, 0), new Vector3(2f, 0.8f, 2f), bronze);
            Cyl(g, "撞木", new Vector3(-2.4f, 4.0f, 0), 0.14f, 2.6f, Wood, false, new Vector3(0, 0, 90));
            var bell = new GameObject("鐘の音").transform;
            bell.SetParent(g, false);
            bell.localPosition = new Vector3(0, 4.2f, 0);
            return bell;
        }

        /// <summary>光る藤棚。</summary>
        public static void Wisteria(Transform envParent, Transform decoParent, Vector3 pos, float rotY, float length)
        {
            var g = Group(envParent, "藤棚", pos, rotY);
            for (float z = -length / 2; z <= length / 2 + 0.01f; z += 3f)
                foreach (float x in new[] { -2.2f, 2.2f })
                    Cyl(g, "柱", new Vector3(x, 1.6f, z), 0.1f, 3.2f, WoodDark);
            Box(g, "棚", new Vector3(0, 3.25f, 0), new Vector3(5f, 0.12f, length + 0.5f), WoodDark);
            var d = Group(decoParent, "藤の花", pos, rotY);
            var purple = Glow("glow_wisteria", new Color(0.72f, 0.5f, 1f), 0.55f, 2.2f);
            var purple2 = Glow("glow_wisteria2", new Color(0.9f, 0.62f, 1f), 0.55f, 2.2f);
            for (float z = -length / 2; z <= length / 2; z += 0.55f)
                for (float x = -2.1f; x <= 2.1f; x += 0.6f)
                {
                    if (Rng.NextDouble() < 0.35) continue;
                    float len = R(0.6f, 1.5f);
                    var strand = Group(d, "房", new Vector3(x + R(-0.15f, 0.15f), 3.2f, z), 0);
                    Ball(strand, "花", new Vector3(0, -len / 2, 0), new Vector3(0.22f, len, 0.22f), Rng.NextDouble() < 0.5 ? purple : purple2);
                    var s = strand.gameObject.AddComponent<Sway>(); s.angle = 5f; s.speed = R(0.6f, 1.1f); s.axis = Vector3.right;
                }
        }

        /// <summary>番傘を頭上に並べた通り（pts にそって）。</summary>
        public static void Umbrellas(Transform decoParent, List<Vector2> pts, float height)
        {
            var g = Group(decoParent, "番傘の空", Vector3.zero);
            var colors = new[] { new Color(0.85f, 0.15f, 0.2f), new Color(0.95f, 0.55f, 0.15f), new Color(0.95f, 0.85f, 0.3f), new Color(0.3f, 0.6f, 0.9f), new Color(0.9f, 0.4f, 0.65f), new Color(0.35f, 0.75f, 0.5f) };
            var cone = SaveMesh("umbrella", UmbrellaMesh());
            for (int i = 0; i < pts.Count; i += 2)
                for (int k = -1; k <= 1; k++)
                {
                    var dir = (pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                    var p = pts[i] + new Vector2(-dir.y, dir.x) * k * 1.6f + new Vector2(R(-0.4f, 0.4f), R(-0.4f, 0.4f));
                    float y = Island.Height(p.x, p.y) + height + R(-0.4f, 0.4f);
                    var u = Group(g, "番傘", new Vector3(p.x, y, p.y), R(0, 360));
                    var m = MeshObj(u, "傘", cone, Vector3.zero, Lit("umb_" + ((i + k + 30) % colors.Length), colors[(i + k + 30) % colors.Length], 0.2f, 0f, true));
                    m.transform.localScale = Vector3.one * R(0.7f, 0.9f);
                    m.transform.localRotation = Quaternion.Euler(R(-12, 12), 0, R(-12, 12));
                    var s = u.gameObject.AddComponent<Sway>(); s.angle = 3f; s.speed = R(0.5f, 0.9f);
                }
        }

        static Mesh UmbrellaMesh()
        {
            const int n = 24;
            var v = new List<Vector3> { new Vector3(0, 0.35f, 0) };
            var t = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                float r = 1.05f * (1 + 0.04f * Mathf.Cos(a * 12)); // 骨のでこぼこ
                v.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            }
            for (int i = 1; i <= n; i++) t.AddRange(new[] { 0, i + 1, i });
            var m = new Mesh { name = "umbrella" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals();
            return m;
        }

        /// <summary>風鈴の回廊（屋根つきの渡り廊下に風鈴がならぶ）。風鈴の音の場所を返す。</summary>
        public static List<Transform> ChimeCorridor(Transform envParent, Transform decoParent, Vector2 a, Vector2 b)
        {
            var dir = (b - a); float len = dir.magnitude; dir /= len;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            var mid = (a + b) / 2;
            float y = Island.Height(mid.x, mid.y);
            var g = Group(envParent, "風鈴の回廊", new Vector3(mid.x, y, mid.y), yaw);
            Box(g, "床", new Vector3(0, 0.15f, 0), new Vector3(4f, 0.3f, len), Wood, true);
            for (float z = -len / 2; z <= len / 2 + 0.01f; z += 3f)
                foreach (float x in new[] { -1.8f, 1.8f })
                    Cyl(g, "柱", new Vector3(x, 1.75f, z), 0.12f, 3.2f, Vermilion);
            MeshObj(g, "屋根", Roof(6f, len + 2f, 1.4f, 0.4f), new Vector3(0, 3.35f, 0), RoofBark);
            var d = Group(decoParent, "風鈴", new Vector3(mid.x, y, mid.y), yaw);
            var glass = Holo("holo_chime", new Color(0.5f, 0.85f, 1f), 0.5f, 1.4f, 0.25f);
            var glass2 = Holo("holo_chime2", new Color(1f, 0.6f, 0.8f), 0.5f, 1.4f, 0.25f);
            var points = new List<Transform>();
            for (float z = -len / 2 + 1; z <= len / 2 - 1; z += 1.2f)
                foreach (float x in new[] { -1.5f, 1.5f })
                {
                    var c = Group(d, "風鈴", new Vector3(x, 3.1f, z), 0);
                    Ball(c, "鈴", new Vector3(0, -0.2f, 0), new Vector3(0.22f, 0.2f, 0.22f), Rng.NextDouble() < 0.5 ? glass : glass2);
                    Box(c, "短冊", new Vector3(0, -0.55f, 0), new Vector3(0.1f, 0.4f, 0.01f), ColorMat(NorenColors[Ri(0, NorenColors.Length)]));
                    var s = c.gameObject.AddComponent<Sway>(); s.angle = 10f; s.speed = R(1.2f, 2f);
                }
            foreach (float t in new[] { -0.3f, 0.3f })
            {
                var p = new GameObject("風鈴の音").transform;
                p.SetParent(d, false);
                p.localPosition = new Vector3(0, 2.8f, t * len);
                points.Add(p);
            }
            return points;
        }

        /// <summary>屋形船。水路を行ったり来たりする。</summary>
        public static void Yakatabune(Transform decoParent, Island.Channel ch, float startAt, int seed)
        {
            var pts = Island.Resample(ch.pts, 2f);
            var path = new List<Vector3>();
            foreach (var p in pts)
            {
                if (Island.Inside(p.x, p.y) < 4f) continue; // 海に出ない
                path.Add(new Vector3(p.x, Mathf.Max(0.05f, Island.SurfaceNoWater(p.x, p.y) - 0.75f) + 0.05f, p.y));
            }
            if (path.Count < 4) return;
            var g = Group(decoParent, "屋形船", path[0]);
            var hull = Lit("boat_hull", new Color(0.3f, 0.2f, 0.14f), 0.3f);
            Box(g, "船体", new Vector3(0, 0.15f, 0), new Vector3(2.6f, 0.6f, 7.5f), hull);
            Box(g, "へさき", new Vector3(0, 0.35f, 4f), new Vector3(1.6f, 0.4f, 1.4f), hull, false, new Vector3(-12, 0, 0));
            Box(g, "床", new Vector3(0, 0.48f, 0), new Vector3(2.4f, 0.06f, 7f), Wood);
            Box(g, "障子", new Vector3(0, 1.25f, -0.4f), new Vector3(2.3f, 1.4f, 5.2f), Glow("glow_shoji", new Color(1f, 0.85f, 0.6f), 0.7f, 2.2f));
            foreach (float x in new[] { -1.15f, 1.15f })
                for (float z = -2.9f; z <= 2.2f; z += 1.3f)
                    Box(g, "桟", new Vector3(x * 1.01f, 1.25f, z), new Vector3(0.04f, 1.42f, 0.05f), WoodDark);
            MeshObj(g, "屋根", Roof(3.2f, 6.2f, 0.7f, 0.2f), new Vector3(0, 1.95f, -0.4f), RoofBark);
            for (int i = 0; i < 6; i++) Chochin(g, new Vector3(1.35f, 1.7f, -2.8f + i * 1.0f), i % 2 == 0 ? GlowRed : GlowWhite);
            for (int i = 0; i < 6; i++) Chochin(g, new Vector3(-1.35f, 1.7f, -2.8f + i * 1.0f), i % 2 == 1 ? GlowRed : GlowWhite);
            Lamp(g, new Vector3(0, 2.2f, 0), new Color(1f, 0.7f, 0.45f), 8f, 1.8f);
            var mover = g.gameObject.AddComponent<PathMover>();
            mover.points = path.ToArray();
            mover.speed = 1.4f + seed * 0.2f;
            mover.startAt = startAt;
        }

        /// <summary>
        /// 海の上の AR ライブステージ（土台）。結晶の舞台・まわりを回る鳥居の輪・光の柱・
        /// スポーンの高舞台から大鳥居をくぐってのびる光の橋と、水面の広場。
        /// </summary>
        public static Vector3 SeaStage(Transform envParent, Transform decoParent, Vector2 center, float bridgeFromZ)
        {
            var crystal = Holo("holo_crystal", new Color(0.55f, 0.85f, 1f), 0.5f, 1.8f, 0.2f);
            var crystalPink = Holo("holo_crystal_pink", new Color(1f, 0.55f, 0.85f), 0.5f, 1.8f, 0.2f);
            var floorMat = Lit("glass_floor", new Color(0.1f, 0.25f, 0.35f), 0.95f, 0.2f);
            var g = Group(envParent, "ARライブステージ", new Vector3(center.x, 0, center.y));
            // 水面の広場（ガラスの床）
            float plazaR = 30f;
            var plaza = Cyl(g, "水面の広場", new Vector3(0, Island.Ground - 0.1f, 0), plazaR, 0.2f, floorMat);
            plaza.AddComponent<MeshCollider>();
            for (int i = 0; i < 3; i++)
            {
                var ring = Cyl(g, "光の輪", new Vector3(0, Island.Ground + 0.02f, 0), plazaR - i * 9f, 0.02f, GlowCyan);
                var inner = Cyl(g, "床", new Vector3(0, Island.Ground + 0.025f, 0), plazaR - i * 9f - 0.25f, 0.02f, floorMat);
            }
            // 結晶の舞台（六角）
            var hex = SaveMesh("hex_prism", HexPrism());
            var stage = MeshObj(g, "結晶の舞台", hex, new Vector3(0, Island.Ground, 6f), Lit("crystal_solid", new Color(0.35f, 0.55f, 0.75f), 0.95f, 0.3f), true);
            stage.transform.localScale = new Vector3(13f, 2.2f, 13f);
            var stageGlow = MeshObj(g, "結晶の光", hex, new Vector3(0, Island.Ground, 6f), crystal);
            stageGlow.transform.localScale = new Vector3(13.1f, 2.25f, 13.1f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var p = new Vector3(Mathf.Cos(a) * 16f, Island.Ground, 6f + Mathf.Sin(a) * 16f);
                var shard = MeshObj(g, "結晶", hex, p, i % 2 == 0 ? crystal : crystalPink);
                shard.transform.localScale = new Vector3(R(1.2f, 2f), R(5f, 11f), R(1.2f, 2f));
                shard.transform.localRotation = Quaternion.Euler(R(-10, 10), R(0, 60), R(-10, 10));
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3 + 0.3f;
                Box(g, "光の柱", new Vector3(Mathf.Cos(a) * 24f, 30f, 6f + Mathf.Sin(a) * 24f), new Vector3(0.6f, 60f, 0.6f), i % 2 == 0 ? GlowCyan : GlowPink);
            }
            // まわりを回る鳥居の輪
            var rings = Group(decoParent, "鳥居の輪", new Vector3(center.x, Island.Ground + 3.5f, center.y + 6f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2 / 12;
                var t = Group(rings, "鳥居", new Vector3(Mathf.Cos(a) * 21f, R(-0.5f, 2.5f), Mathf.Sin(a) * 21f), -a * Mathf.Rad2Deg);
                Town.MiniTorii(t, 3.4f, 4.2f);
            }
            rings.gameObject.AddComponent<Floaty>().spin = 4f;
            var rings2 = Group(decoParent, "鳥居の輪2", new Vector3(center.x, Island.Ground + 14f, center.y + 6f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2 / 8;
                var t = Group(rings2, "鳥居", new Vector3(Mathf.Cos(a) * 12f, 0, Mathf.Sin(a) * 12f), -a * Mathf.Rad2Deg);
                Town.MiniTorii(t, 2.6f, 3.2f);
            }
            var f2 = rings2.gameObject.AddComponent<Floaty>(); f2.spin = -7f; f2.amplitude = 1.2f;
            // 光の橋（高舞台 → 大鳥居 → 広場）
            float z0 = bridgeFromZ, z1 = center.y + plazaR - 1f;
            var bridge = Box(envParent, "光の橋", new Vector3(center.x, Island.Ground - 0.1f, (z0 + z1) / 2), new Vector3(5f, 0.2f, Mathf.Abs(z0 - z1)), floorMat, true);
            foreach (float x in new[] { -2.3f, 2.3f })
            {
                Box(envParent, "光の線", new Vector3(center.x + x, Island.Ground + 0.02f, (z0 + z1) / 2), new Vector3(0.12f, 0.04f, Mathf.Abs(z0 - z1)), GlowCyan);
                Box(envParent, "見えない柵", new Vector3(center.x + x * 1.12f, Island.Ground + 0.7f, (z0 + z1) / 2), new Vector3(0.3f, 1.4f, Mathf.Abs(z0 - z1)), Stone, true).GetComponent<Renderer>().enabled = false;
            }
            // 広場のふちの見えない柵（橋の入口だけあける）
            for (int i = 0; i < 36; i++)
            {
                float a = i * Mathf.PI * 2 / 36;
                var p = new Vector3(Mathf.Cos(a) * plazaR, Island.Ground + 0.7f, Mathf.Sin(a) * plazaR);
                if (p.z > plazaR - 4f && Mathf.Abs(p.x) < 4f) continue;
                Box(g, "見えない柵", p, new Vector3(5.4f, 1.4f, 0.3f), Stone, true, new Vector3(0, -a * Mathf.Rad2Deg + 90, 0)).GetComponent<Renderer>().enabled = false;
            }
            return new Vector3(center.x, Island.Ground + 0.1f, center.y + plazaR - 6f);
        }

        static Mesh HexPrism()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * Mathf.PI / 3, a1 = (i + 1) * Mathf.PI / 3;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                int k = v.Count;
                v.AddRange(new[] { p0, p1, p0 + Vector3.up, p1 + Vector3.up });
                t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                int c = v.Count;
                v.AddRange(new[] { Vector3.up, p0 + Vector3.up, p1 + Vector3.up });
                t.AddRange(new[] { c, c + 2, c + 1 });
            }
            var m = new Mesh { name = "hex_prism" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>空を泳ぐ魚とマンタ、夜の花火、川べりの蛍、朝もや。</summary>
        public static void SkyAndNight(Transform decoParent)
        {
            var sky = new GameObject("空を泳ぐもの");
            sky.transform.SetParent(decoParent, false);
            var sc = sky.AddComponent<SkyCreatures>();
            sc.fishMat = Holo("holo_fish", new Color(0.35f, 0.85f, 1f), 0.45f, 1.5f, 0.15f);
            sc.fishMat2 = Holo("holo_fish_pink", new Color(1f, 0.5f, 0.85f), 0.45f, 1.5f, 0.15f);
            sc.mantaMat = Holo("holo_manta", new Color(0.55f, 0.6f, 1f), 0.4f, 1.3f, 0.12f);
            sc.center = new Vector3(0, 0, 130);

            var spark = Shaded("spark", "Tsukuyomi/Spark");
            var fw = new GameObject("花火");
            fw.transform.SetParent(decoParent, false);
            var f = fw.AddComponent<Fireworks>();
            f.sparkMat = spark;
            f.area = new Vector3(0, 0, -170);

            // 蛍：天の川の下流ぞい
            var ch = Island.Channels[0];
            for (int i = 8; i < ch.pts.Length; i += 2)
            {
                var p = ch.pts[i];
                var go = new GameObject("蛍");
                go.transform.SetParent(decoParent, false);
                go.transform.position = new Vector3(p.x, Island.SurfaceNoWater(p.x, p.y) + 1f, p.y);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
                main.startSpeed = 0.2f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
                main.startColor = new Color(0.75f, 1f, 0.35f);
                main.maxParticles = 80;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(18, 2, 18);
                var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.4f;
                var col = ps.colorOverLifetime; col.enabled = true;
                var gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                           new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0.2f, 0.5f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
                col.color = gr;
                go.GetComponent<ParticleSystemRenderer>().sharedMaterial = spark;
                go.AddComponent<NightParticles>().rate = 8f;
                ps.Play();
            }

            // 朝もや：島の上に低くただよう
            var mist = new GameObject("朝もや");
            mist.transform.SetParent(decoParent, false);
            mist.transform.position = new Vector3(0, 6f, 150);
            var mp = mist.AddComponent<ParticleSystem>();
            mp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var mm = mp.main;
            mm.startLifetime = 20f; mm.startSpeed = 0.3f;
            mm.startSize = new ParticleSystem.MinMaxCurve(18f, 30f);
            mm.startColor = new Color(0.9f, 0.92f, 1f, 0.12f);
            mm.maxParticles = 300;
            var msh = mp.shape; msh.shapeType = ParticleSystemShapeType.Box; msh.scale = new Vector3(300, 6, 300);
            var mr = mist.GetComponent<ParticleSystemRenderer>();
            mr.sharedMaterial = Shaded("mist", "Tsukuyomi/Petal");
            mr.sharedMaterial.SetColor("_Color", new Color(0.95f, 0.95f, 1f, 1f));
            var np = mist.AddComponent<NightParticles>(); np.morningOnly = true; np.rate = 12f;
            mp.Play();
        }
    }
}
