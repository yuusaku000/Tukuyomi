using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 森と自然の合戦場（舞台 1）。城下町とはまったく別の形：両端の高台（陣地・高さ12＝今までの3倍）から
    /// 段々に下って、まん中が深い森の谷底になる「すり鉢」。段の境目は岩の崖と滝。
    ///  陣地 → 崖をジグザグに下る坂 → 段の上をくねる小道 → 北・中央・南の3つの下り口 → 谷底。
    ///  北の櫓は谷底にそびえる岩山の上、南の櫓は大樹の立つ台地の上。谷底のまん中に小川と飛び石と丸太の橋。
    /// ここの数は「元の数」（横は KX 倍、高さは KY 倍になる）。武者の道すじもこの地形専用。
    /// </summary>
    public partial class Sengoku
    {
        /// <summary>いまの試合の舞台（位置の計算に使う）。0 城下町・1 森。</summary>
        public static int ThemeNow;

        // 森の舞台の要所（元の数）
        static readonly Vector3 FN_N = new(0, 6, 42), FN_S = new(0, 7, -40);
        const float FBaseX = 64f, FBaseZ = 26f, FBaseH = 12f;

        // 武者の道すじ（片側。西は x を負に）。ramp で結ぶ所は、坂の端と同じ位置にする
        static readonly (string id, Vector3 p)[] FNodes =
        {
            ("SP", new(76, 12, 6)), ("DAR", new(69, 12, 0)), ("GIN", new(66.5f, 12, 3.5f)), ("GATE", new(62.5f, 12, 3.5f)),
            ("RB", new(62.5f, 8, -12.5f)), ("TBM", new(56, 8, -6)), ("TBN", new(55.5f, 8, 16)), ("TBS", new(55.5f, 8, -17)),
            ("TAN", new(43.5f, 4, 20)), ("TAS", new(43.5f, 4, -22)), ("TAC", new(35, 4, 3)), ("TANN", new(35, 4, 29)), ("TASS", new(35, 4, -27)),
            ("BC", new(21, 0, 3)), ("BN", new(21, 0, 37)), ("BS", new(21, 0, -35)), ("HN", new(19, 0, 42)), ("TS", new(19, 0, -40)),
        };
        static readonly (string a, string b)[] FEdges =
        {
            ("SP", "DAR"), ("DAR", "GIN"), ("GIN", "GATE"), ("GATE", "RB"), ("RB", "TBM"), ("RB", "TBS"), ("TBM", "TBN"), ("TBM", "TBS"),
            ("TBN", "TAN"), ("TBS", "TAS"), ("TAN", "TAC"), ("TAS", "TAC"), ("TAN", "TANN"), ("TAS", "TASS"),
            ("TAC", "BC"), ("TANN", "BN"), ("TASS", "BS"), ("BN", "HN"), ("BS", "TS"), ("BC", "BN"), ("BC", "BS"),
        };
        static readonly (string id, Vector3 p)[] FShared = { ("HT", new(0, 6, 40)), ("TT", new(0, 7, -40)), ("C0", new(0, 0, 0)), ("NB", new(0, 0, 12)), ("SB", new(0, 0, -12)) };
        static readonly (string side, string shared)[] FLinks = { ("HN", "HT"), ("TS", "TT"), ("BC", "C0"), ("BC", "NB"), ("BC", "SB"), ("BN", "NB"), ("BS", "SB") };

        /// <summary>森の舞台の地面の高さ（元の数）。</summary>
        static float FHeightAt(float x, float z)
        {
            float ax = Mathf.Abs(x);
            if (ax >= FBaseX && Mathf.Abs(z) <= FBaseZ) return FBaseH;
            if (ax >= 52f) return 8f;
            float edge = z > 12 ? 29f : z >= -12 ? 31f : 30f;
            if (ax >= edge) return 4f;
            if (ax <= 8f && z >= 32f && z <= 54f) return 6f;
            if (ax <= 7f && z >= -49f && z <= -35f) return 7f;
            return 0f;
        }

        /// <summary>森の舞台で、木や岩を置いてよい場所か（道すじ・坂・陣地・岩山と台地の上・小川からはなれている）。</summary>
        static bool FFree(Vector2 p, float clear)
        {
            if (Mathf.Abs(p.x) > 82f || Mathf.Abs(p.y) > 56f) return false;
            if (Mathf.Abs(p.x) >= FBaseX - 1 && Mathf.Abs(p.y) <= FBaseZ + 1) return false;
            if (Mathf.Abs(p.x) <= 10f && p.y >= 30f && p.y <= 56f) return false;
            if (Mathf.Abs(p.x) <= 9f && p.y >= -51f && p.y <= -33f) return false;
            if (Mathf.Abs(p.x) <= 4f && Mathf.Abs(p.y) <= 31f) return false;
            foreach (float s in new[] { -1f, 1f })
                foreach (var (a, b) in FEdges)
                {
                    var pa = FN(a, s); var pb = FN(b, s);
                    if (DistToSeg(p, new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z)) < clear) return false;
                }
            foreach (float s in new[] { -1f, 1f })
                foreach (var (a, b) in FLinks)
                {
                    var pa = FN(a, s); var pb = FS(b);
                    if (DistToSeg(p, new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z)) < clear) return false;
                }
            return true;
        }

        static Vector3 FN(string id, float s) { foreach (var (i, p) in FNodes) if (i == id) return new Vector3(p.x * s, p.y, p.z); return Vector3.zero; }
        static Vector3 FS(string id) { foreach (var (i, p) in FShared) if (i == id) return p; return Vector3.zero; }

        // ───────── 組み立て ─────────

        void BuildForestField()
        {
            _map = new GameObject("森の合戦場").transform;
            _map.SetParent(transform, false);
            _rampRoot = new GameObject("森の坂").transform;
            _rampRoot.SetParent(transform, false);
            _rs = new Vector3(KX, KY, KX);
            var rock = new Color(0.4f, 0.38f, 0.35f); var rock2 = new Color(0.34f, 0.33f, 0.31f);
            var grass = new Color(0.22f, 0.4f, 0.19f); var grassHi = new Color(0.27f, 0.45f, 0.22f); var dirt = new Color(0.3f, 0.24f, 0.17f);

            // 谷底
            Box(new Vector3(0, -0.5f, 0), new Vector3(168, 1, 116), new Color(0.2f, 0.36f, 0.18f));
            void Tier(float x0, float x1, float z0, float z1, float top, Color side, Color face)
            {
                var c = new Vector3((x0 + x1) * 0.5f, (top - 1f) * 0.5f, (z0 + z1) * 0.5f);
                Box(c, new Vector3(Mathf.Abs(x1 - x0), top + 1f, Mathf.Abs(z1 - z0)), side);
                Box(new Vector3(c.x, top + 0.02f, c.z), new Vector3(Mathf.Abs(x1 - x0), 0.06f, Mathf.Abs(z1 - z0)), face);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                // 段A（高さ4）：へりは場所によってずらす（崖の形を自然に）
                Tier(s * 29, s * 52, 12, 58, 4, rock, grass); Tier(s * 31, s * 52, -12, 12, 4, rock, grass); Tier(s * 30, s * 52, -58, -12, 4, rock, grass);
                // 段B（高さ8）
                Tier(s * 52, s * 64, -58, 58, 8, rock2, grassHi);
                Tier(s * 64, s * 84, FBaseZ, 58, 8, rock2, grassHi); Tier(s * 64, s * 84, -58, -FBaseZ, 8, rock2, grassHi);
                // 陣地の高台（高さ12）
                Tier(s * FBaseX, s * 84, -FBaseZ, FBaseZ, FBaseH, rock, new Color(0.3f, 0.45f, 0.24f));
                // 坂：陣地から崖ぞいにジグザグに下りる → 段Bの小道 → 段Aへ → 谷底へ（北・中央・南）
                Ramp(new Vector3(s * 62.5f, FBaseH, 4f), new Vector3(s * 62.5f, 8f, -10f), 3f, dirt);
                Ramp(FN("TBN", s) + new Vector3(0, 0, 0), FN("TAN", s), 3.2f, dirt);
                Ramp(FN("TBS", s), FN("TAS", s), 3.2f, dirt);
                Ramp(new Vector3(s * 33, 4, 3), new Vector3(s * 23, 0.05f, 3), 3.4f, dirt);
                Ramp(new Vector3(s * 33, 4, 30), new Vector3(s * 23, 0.05f, 36), 3.2f, dirt);
                Ramp(new Vector3(s * 33, 4, -28), new Vector3(s * 23, 0.05f, -34), 3.2f, dirt);
                // 岩山（北の櫓）と大樹の台地（南の櫓）へ上がる坂
                Ramp(new Vector3(s * 18, 0.05f, 42), new Vector3(s * 8, 6, 42), 3f, dirt);
                Ramp(new Vector3(s * 18, 0.05f, -40), new Vector3(s * 7, 7, -40), 3f, dirt);
                // 小道（段の上の土の道。道すじに沿って細く）
                foreach (var (a, b) in FEdges)
                {
                    var pa = FN(a, s); var pb = FN(b, s);
                    if (Mathf.Abs(pa.y - pb.y) > 0.5f) continue;
                    var d = pb - pa; var mid = (pa + pb) * 0.5f;
                    Block(PrimitiveType.Cube, _map, mid + Vector3.up * 0.04f, new Vector3(2.2f, 0.02f, new Vector2(d.x, d.z).magnitude + 2f), dirt, false, new Vector3(0, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0));
                }
                ForestFort(s);
                // 滝（段の崖に、白く光る水の帯と、下の水たまり）
                foreach (var (x, top, z) in new[] { (52f, 8f, 44f), (52f, 8f, -46f), (30f, 4f, 48f), (30f, 4f, -50f) })
                {
                    Block(PrimitiveType.Cube, _map, new Vector3(s * (x - 0.05f), top * 0.5f, z), new Vector3(0.2f, top, 2.4f), new Color(0.75f, 0.92f, 1f), false, null, true);
                    Block(PrimitiveType.Cylinder, _map, new Vector3(s * (x - 2f), top > 6 ? 4.03f : 0.03f, z), new Vector3(4f, 0.02f, 4f), new Color(0.3f, 0.6f, 0.8f), false);
                }
            }
            // 岩山（北の櫓）
            Box(new Vector3(0, 2.5f, 43), new Vector3(16, 7, 22), rock);
            Box(new Vector3(0, 6.02f, 43), new Vector3(16, 0.06f, 22), grassHi);
            Tower2(FN_N, Color.white);
            // 大樹の台地（南の櫓）：台地と、そびえる大樹、木の上の物見台
            Box(new Vector3(0, 3f, -42), new Vector3(14, 8, 14), rock);
            Box(new Vector3(0, 7.02f, -42), new Vector3(14, 0.06f, 14), grassHi);
            SacredTree(new Vector3(0, 7, -47.5f), 22f);
            Box(new Vector3(0, 19f, -47.5f), new Vector3(6, 0.4f, 6), Timber);
            CaptureRing(FN_S);
            // 谷底のまん中：小川・飛び石・丸太の橋
            Block(PrimitiveType.Cube, _map, new Vector3(0, 0.03f, 0), new Vector3(4.5f, 0.03f, 62), Water, false);
            for (int i = -2; i <= 2; i++) Block(PrimitiveType.Cylinder, _map, new Vector3(i * 0.9f, 0.08f, (i % 2) * 0.6f), new Vector3(0.7f, 0.1f, 0.7f), rock, false);
            foreach (float z in new[] { 12f, -12f })
                for (int k = -1; k <= 1; k++)
                {
                    var log = Block(PrimitiveType.Cylinder, _map, new Vector3(0, 0.25f, z + k * 0.5f), new Vector3(0.25f, 3.2f, 0.25f), Bark, false);
                    log.transform.localEulerAngles = new Vector3(0, 0, 90);
                }
            // 谷をかこむ高い岩壁（北と南）・見えない壁
            for (int i = 0; i < 12; i++)
                foreach (float sz in new[] { 1f, -1f })
                {
                    float x = -80 + i * 14.5f, h = 14f + (i % 3) * 5f;
                    Box(new Vector3(x, h * 0.5f - 1f, sz * 60.5f), new Vector3(15f, h, 5f), i % 2 == 0 ? rock : rock2);
                }
            foreach (var (c, s2) in new[] { (new Vector3(85, 30, 0), new Vector3(2, 90, 120)), (new Vector3(-85, 30, 0), new Vector3(2, 90, 120)),
                                            (new Vector3(0, 30, 59), new Vector3(172, 90, 2)), (new Vector3(0, 30, -59), new Vector3(172, 90, 2)) })
                Box(c, s2, Color.white).GetComponent<Renderer>().enabled = false;
            Block(PrimitiveType.Cube, _map, new Vector3(0, -16f, 0), new Vector3(600, 0.2f, 600), new Color(0.2f, 0.28f, 0.24f), false);

            ForestScatter();

            // 光
            var moon = new GameObject("森の月明かり").AddComponent<Light>();
            moon.transform.SetParent(_map, false); moon.transform.rotation = Quaternion.Euler(48, -35, 0);
            moon.type = LightType.Directional; moon.intensity = 1.15f; moon.color = new Color(0.78f, 0.86f, 1f); moon.shadows = LightShadows.None;
            _map.localScale = _rs;
            Physics.SyncTransforms();
        }

        /// <summary>占拠の光る輪だけ（南の台地用）。</summary>
        void CaptureRing(Vector3 at)
        {
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2;
                Block(PrimitiveType.Cube, _map, at + new Vector3(Mathf.Cos(a) * CaptureR / KX, 0.06f, Mathf.Sin(a) * CaptureR / KX), new Vector3(0.9f, 0.04f, 0.12f), Color.white, false, new Vector3(0, -a * Mathf.Rad2Deg + 90, 0), true);
            }
        }

        /// <summary>森の砦：高台のへりに柵（低い壁と杭）、門、物見櫓、のぼり、回復の灯り。</summary>
        void ForestFort(float s)
        {
            var wood = new Color(0.4f, 0.29f, 0.19f);
            var col = s < 0 ? new Color(0.1f, 0.6f, 0.62f) : Red;
            void Wall(Vector3 a, Vector3 b)
            {
                var d = b - a; var mid = (a + b) * 0.5f;
                Box(mid + Vector3.up * (FBaseH + 1.1f), new Vector3(Mathf.Abs(d.x) + 0.6f, 2.2f, Mathf.Abs(d.z) + 0.6f), wood);
                int n = Mathf.Max(2, (int)(d.magnitude / 1.4f));
                for (int i = 0; i <= n; i++) Block(PrimitiveType.Cylinder, _map, Vector3.Lerp(a, b, i / (float)n) + Vector3.up * (FBaseH + 2.7f), new Vector3(0.2f, 0.6f, 0.2f), wood, false);
            }
            Wall(new Vector3(s * 83.5f, 0, -FBaseZ), new Vector3(s * 83.5f, 0, FBaseZ));
            Wall(new Vector3(s * FBaseX, 0, FBaseZ), new Vector3(s * 83.5f, 0, FBaseZ));
            Wall(new Vector3(s * FBaseX, 0, -FBaseZ), new Vector3(s * 83.5f, 0, -FBaseZ));
            Wall(new Vector3(s * (FBaseX + 0.3f), 0, 7.5f), new Vector3(s * (FBaseX + 0.3f), 0, FBaseZ));
            Wall(new Vector3(s * (FBaseX + 0.3f), 0, -FBaseZ), new Vector3(s * (FBaseX + 0.3f), 0, 0f));
            foreach (float z in new[] { 0.2f, 7.3f }) Block(PrimitiveType.Cylinder, _map, new Vector3(s * (FBaseX + 0.3f), FBaseH + 2.8f, z), new Vector3(0.35f, 2.8f, 0.35f), Red);
            Box(new Vector3(s * (FBaseX + 0.3f), FBaseH + 5.7f, 3.75f), new Vector3(1.2f, 0.4f, 8.6f), new Color(0.12f, 0.1f, 0.1f));
            Watchtower(new Vector3(s * 78, FBaseH, 0), s);
            for (int k = -2; k <= 2; k++)
            {
                if (k == 0) continue;
                var p = new Vector3(s * 70, FBaseH, k * 9f);
                Box(p + Vector3.up * 3.2f, new Vector3(0.15f, 6.4f, 0.15f), Timber, false);
                Box(p + new Vector3(0, 4.2f, 0.55f), new Vector3(0.04f, 3.6f, 1f), col, false);
            }
            var heal = s < 0 ? new Color(0.3f, 1f, 1f) : new Color(1f, 0.45f, 0.35f);
            foreach (float z in new[] { -9f, 9f })
            {
                Block(PrimitiveType.Cylinder, _map, new Vector3(s * 67, FBaseH + 0.8f, z), new Vector3(0.25f, 0.8f, 0.25f), Timber);
                Block(PrimitiveType.Sphere, _map, new Vector3(s * 67, FBaseH + 1.9f, z), Vector3.one * 0.6f, heal, false, null, true);
            }
        }

        /// <summary>森をうめつくす：木・岩・崖・倒木（道すじからはなし、地面の高さに合わせる）。まわりの雪山と森。</summary>
        void ForestScatter()
        {
            var rng = new System.Random(8128);
            int placed = 0;
            for (int tries = 0; tries < 5000 && placed < 330; tries++)
            {
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 82f, (float)(rng.NextDouble() * 2 - 1) * 56f);
                if (!FFree(p2, 3.2f)) continue;
                var at = new Vector3(p2.x, FHeightAt(p2.x, p2.y), p2.y);
                double r = rng.NextDouble();
                if (r < 0.5) Cedar(at, 10f + (float)rng.NextDouble() * 10f);
                else if (r < 0.9) RoundTree(at, 6f + (float)rng.NextDouble() * 5f, rng);
                else SacredTree(at, 14f + (float)rng.NextDouble() * 6f);
                placed++;
            }
            for (int k = 0, tries = 0; k < 34 && tries < 1500; tries++)
            {
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 80f, (float)(rng.NextDouble() * 2 - 1) * 54f);
                if (!FFree(p2, 4.5f)) continue;
                Boulders(new Vector3(p2.x, FHeightAt(p2.x, p2.y), p2.y), rng); k++;
            }
            for (int k = 0, tries = 0; k < 10 && tries < 1500; tries++)
            {
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 78f, (float)(rng.NextDouble() * 2 - 1) * 52f);
                float yaw = (float)rng.NextDouble() * 180f, len = 9f + (float)rng.NextDouble() * 7f;
                var dir = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                if (!FFree(p2, 6f) || !FFree(p2 + dir * len * 0.4f, 4.5f) || !FFree(p2 - dir * len * 0.4f, 4.5f)) continue;
                float y = FHeightAt(p2.x, p2.y);
                if (Mathf.Abs(FHeightAt((p2 + dir * len * 0.4f).x, (p2 + dir * len * 0.4f).y) - y) > 0.1f || Mathf.Abs(FHeightAt((p2 - dir * len * 0.4f).x, (p2 - dir * len * 0.4f).y) - y) > 0.1f) continue;
                CliffWall(new Vector3(p2.x, y, p2.y), len, 5f + (float)rng.NextDouble() * 6f, yaw); k++;
            }
            for (int k = 0, tries = 0; k < 16 && tries < 600; tries++)
            {
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 80f, (float)(rng.NextDouble() * 2 - 1) * 54f);
                if (!FFree(p2, 4f)) continue;
                var log = Block(PrimitiveType.Cylinder, _map, new Vector3(p2.x, FHeightAt(p2.x, p2.y) + 0.5f, p2.y), new Vector3(0.5f, 2.5f + (float)rng.NextDouble() * 2f, 0.5f), Bark);
                log.transform.localEulerAngles = new Vector3(90, (float)rng.NextDouble() * 180f, 0); k++;
            }
            // まわり：雪をいただく尖った山並み（北は高く）と、谷の上の森
            for (int i = 0; i < 34; i++)
            {
                float a = i / 34f * Mathf.PI * 2;
                bool north = Mathf.Sin(a) > 0.3f;
                var p = new Vector3(Mathf.Cos(a) * 125f, -6, Mathf.Sin(a) * 100f);
                float h = north ? 80f + (i % 4) * 14f : 44f + (i % 5) * 8f, w = 34f + (i % 3) * 8f;
                var m = new GameObject("山"); m.transform.SetParent(_map, false); m.transform.localPosition = p; m.transform.localScale = new Vector3(w, h, w);
                m.AddComponent<MeshFilter>().sharedMesh = Doll.Cone; m.AddComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.32f, 0.36f, 0.34f));
                var cap = new GameObject("雪の頂"); cap.transform.SetParent(_map, false); cap.transform.localPosition = p + Vector3.up * h * (north ? 0.55f : 0.68f); cap.transform.localScale = new Vector3(w * (north ? 0.5f : 0.36f), h * (north ? 0.32f : 0.22f), w * (north ? 0.5f : 0.36f));
                cap.AddComponent<MeshFilter>().sharedMesh = Doll.Cone; cap.AddComponent<MeshRenderer>().sharedMaterial = Mat(Snow);
            }
            for (int i = 0; i < 110; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = 1.04f + (float)rng.NextDouble() * 0.18f;
                var p = new Vector3(Mathf.Cos(a) * 88f * rr, 14f, Mathf.Sin(a) * 63f * rr);
                float h = 10f + (float)rng.NextDouble() * 8f;
                var t = new GameObject("谷の上の森"); t.transform.SetParent(_map, false); t.transform.localPosition = p; t.transform.localScale = new Vector3(2.2f, h, 2.2f);
                t.AddComponent<MeshFilter>().sharedMesh = Doll.Cone; t.AddComponent<MeshRenderer>().sharedMaterial = Mat(i % 2 == 0 ? new Color(0.15f, 0.3f, 0.19f) : new Color(0.2f, 0.36f, 0.2f));
            }
        }

        /// <summary>森の舞台の武者の道すじ。</summary>
        void BuildForestGraph()
        {
            var ids = new Dictionary<string, int>();
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (var (id, p) in FNodes) ids[id + s] = Node(new Vector3(p.x * s, p.y, p.z));
                foreach (var (a, b) in FEdges) Link(ids[a + s], ids[b + s]);
            }
            foreach (var (id, p) in FShared) ids[id] = Node(p);
            foreach (float s in new[] { -1f, 1f }) foreach (var (a, b) in FLinks) Link(ids[a + s], ids[b]);
            Link(ids["NB"], ids["C0"]); Link(ids["SB"], ids["C0"]);
        }

        /// <summary>森の舞台の見張りの持ち場（元の数。片側）。</summary>
        static readonly (Vector3, bool)[] FGuardPosts =
        {
            (new(43.5f, 4, 20), false), (new(43.5f, 4, -22), false), (new(35, 4, 3), true), (new(21, 0, 37), false), (new(21, 0, -35), false),
            (new(21, 0, 3), true), (new(56, 8, 16), true), (new(56, 8, -17), true), (new(56, 8, -6), false), (new(35, 4, 29), false), (new(35, 4, -27), false),
        };
    }
}
