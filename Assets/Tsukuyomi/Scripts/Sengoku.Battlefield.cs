using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の試合用マップ（東西 168m・南北 116m、西と東は左右対称）。
    ///  城（x=±66）：城壁と城門、二の丸の広場（大将落としの場所）、石垣の上の天守閣。
    ///  城下町（x=±12〜46）：町家の並ぶ通りと路地。屋根の上も歩ける。火の見やぐらと蔵。
    ///  中央の深い谷（x=-9〜9、底は y=-10）：北と南の吊り橋（z=±18）。谷の上に浮かぶ岩。両側の壁ぞいに登る坂。
    ///  北の山（z=34〜58）：2段の台地を坂で登る。上に北の櫓。
    ///  南の湖（z=-58〜-34）：浮き島に南の櫓。桟橋で渡る。傘なら水の上を浮いて渡れる。
    /// </summary>
    public partial class Sengoku
    {
        /// <summary>マップの広さの倍率。組み立ては元の数で行い、最後に横（x・z）を KX 倍、高さ（y）を KY 倍する。</summary>
        public const float KX = 2.7f, KY = 1.35f;
        /// <summary>元の数の位置を、試合場の位置にする。</summary>
        public static Vector3 Sx(Vector3 v) => new(v.x * KX, v.y * KY, v.z * KX);
        static readonly Vector3 N0 = new(0, 12, 47), S0 = new(0, 1, -46);
        public static readonly Vector3 NorthCapture = Sx(N0), SouthCapture = Sx(S0);
        static Vector3 Castle0(int team) => new(team == 1 ? -66 : 66, 0, 0);
        public static Vector3 CastleOf(int team) => Sx(Castle0(team));
        public static Vector3 DarumaSpot(int team) => Sx(new Vector3(team == 1 ? -55 : 55, 0, 0));
        public static Vector3 SpawnOf(int team, int i) => Sx(new Vector3(team == 1 ? -60 : 60, 0.2f, (i - 1) * 4.5f + 8f));
        /// <summary>自分の城（本陣）の城壁の内側か。ここにいると回復する。</summary>
        public static bool InBase(int team, Vector3 p) => (team == 1 ? p.x < -48f * KX : p.x > 48f * KX) && Mathf.Abs(p.z) < 22f * KX && p.y > -1f;

        static readonly Color Tile = new(0.2f, 0.22f, 0.27f), Plaster = new(0.9f, 0.88f, 0.82f), Timber = new(0.32f, 0.2f, 0.13f),
            Road = new(0.52f, 0.46f, 0.38f), Cliff = new(0.42f, 0.37f, 0.32f), Pink = new(1f, 0.75f, 0.84f), Bamboo = new(0.38f, 0.6f, 0.34f);

        void BuildBattlefield()
        {
            _map = new GameObject("合戦場").transform;
            _map.SetParent(transform, false);
            _rampRoot = new GameObject("合戦場の坂").transform;
            _rampRoot.SetParent(transform, false);
            _rs = new Vector3(KX, KY, KX);

            // ── 地面（谷と湖のところはあける）──
            foreach (float s in new[] { -1f, 1f })
            {
                Box(new Vector3(s * 55, -0.5f, 0), new Vector3(58, 1, 116), Grass);           // x 26〜84
                Box(new Vector3(s * 17.5f, -0.5f, 12), new Vector3(17, 1, 92), Grass);        // x 9〜26、z -34〜58
            }
            // 谷の底と、両側の壁ぞいの坂
            Box(new Vector3(0, -10.5f, 0), new Vector3(18, 1, 68), new Color(0.36f, 0.32f, 0.27f));
            Block(PrimitiveType.Cube, _map, new Vector3(0, -9.9f, 0), new Vector3(3, 0.05f, 68), Water, false);
            Ramp(new Vector3(-7.4f, -10, -22), new Vector3(-7.4f, 0.05f, 10), 3.2f, Road);
            Ramp(new Vector3(7.4f, -10, 22), new Vector3(7.4f, 0.05f, -10), 3.2f, Road);
            foreach (float s in new[] { -1f, 1f })
                Box(new Vector3(s * 9.6f, -5f, 0), new Vector3(1.2f, 10, 68), Cliff); // 谷の壁（見た目）
            // 谷の上の浮かぶ岩（移動わざの近道）
            foreach (var p in new[] { new Vector3(0, 3, 0), new Vector3(-3, 7, 8), new Vector3(3, 10, -7), new Vector3(0, 5, 27), new Vector3(0, 5, -27) })
            {
                Box(p, new Vector3(4.5f, 1.2f, 4.5f), Cliff);
                Box(p + Vector3.up * 0.62f, new Vector3(4.5f, 0.05f, 4.5f), Grass);
                Block(PrimitiveType.Sphere, _map, p + Vector3.down * 1.1f, new Vector3(4, 2.5f, 4), Cliff, false);
            }
            // 吊り橋（まん中が少したわむ）
            foreach (float z in new[] { 18f, -18f })
            {
                for (int i = 0; i < 7; i++)
                {
                    float x0 = -10.5f + i * 3f, x1 = x0 + 3f;
                    float y0 = -0.2f - Mathf.Sin(i / 7f * Mathf.PI) * 0.9f, y1 = -0.2f - Mathf.Sin((i + 1) / 7f * Mathf.PI) * 0.9f;
                    Ramp(new Vector3(x0, y0, z), new Vector3(x1, y1, z), 3f, Timber);
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float sx in new[] { -1f, 1f }) Block(PrimitiveType.Cylinder, _map, new Vector3(sx * 10.5f, 1.2f, z + side * 1.7f), new Vector3(0.3f, 1.4f, 0.3f), Red);
                    for (int i = 0; i < 12; i++)
                    {
                        float x = -10f + i * 1.8f;
                        Block(PrimitiveType.Cube, _map, new Vector3(x, 0.6f - Mathf.Sin((i + 0.5f) / 12f * Mathf.PI) * 0.9f, z + side * 1.55f), new Vector3(1.85f, 0.06f, 0.06f), new Color(0.75f, 0.65f, 0.45f), false);
                    }
                }
            }

            Castle2(1); Castle2(2);
            Town(-1); Town(1);
            BigFeatures(-1); BigFeatures(1);
            ValleySpires();
            Mountain();
            Lake();
            BaseMarks(1); BaseMarks(2);

            // まわりの山（見た目）・見えない壁・ずっと下の地面
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * Mathf.PI * 2;
                Block(PrimitiveType.Sphere, _map, new Vector3(Mathf.Cos(a) * 150f, -4, Mathf.Sin(a) * 118f), new Vector3(75, 45 + (i % 5) * 10, 60), new Color(0.27f, 0.34f, 0.3f), false);
            }
            foreach (var (c, s) in new[] { (new Vector3(85, 30, 0), new Vector3(2, 90, 120)), (new Vector3(-85, 30, 0), new Vector3(2, 90, 120)),
                                           (new Vector3(0, 30, 59), new Vector3(172, 90, 2)), (new Vector3(0, 30, -59), new Vector3(172, 90, 2)) })
                Box(c, s, Color.white).GetComponent<Renderer>().enabled = false;
            Block(PrimitiveType.Cube, _map, new Vector3(0, -16f, 0), new Vector3(600, 0.2f, 600), new Color(0.2f, 0.28f, 0.24f), false);
            // 月明かり（夜でも試合場が見えるように、島の空とは別に弱い光をあてる）
            var moon = new GameObject("試合場の月明かり").AddComponent<Light>();
            moon.transform.SetParent(_map, false); moon.transform.rotation = Quaternion.Euler(48, -35, 0);
            moon.type = LightType.Directional; moon.intensity = 1.15f; moon.color = new Color(0.75f, 0.82f, 1f); moon.shadows = LightShadows.None;
            foreach (float s in new[] { -1f, 1f })
            {
                var l = new GameObject("灯り").AddComponent<Light>();
                l.transform.SetParent(_map, false); l.transform.localPosition = new Vector3(s * 35, 18, 0);
                l.type = LightType.Point; l.range = 150; l.intensity = 1.1f; l.color = new Color(1f, 0.85f, 0.65f);
            }
            _map.localScale = _rs;
        }

        /// <summary>城（team 1 西・2 東）。城壁と城門、二の丸の広場、石垣と天守閣。</summary>
        void Castle2(int team)
        {
            float s = team == 1 ? -1 : 1;
            var c = Castle0(team);
            var col = team == 1 ? new Color(0.1f, 0.6f, 0.62f) : Red;
            // 城壁（高さ5・上を歩ける）。町側（内側）の真ん中に城門
            float inner = c.x - s * 18f; // 町側の壁の x
            Box(new Vector3(c.x + s * 16f, 2.5f, 0), new Vector3(2, 5, 46), Stone);                       // 外側
            Box(new Vector3(c.x, 2.5f, 22), new Vector3(34, 5, 2), Stone);
            Box(new Vector3(c.x, 2.5f, -22), new Vector3(34, 5, 2), Stone);
            Box(new Vector3(inner, 2.5f, 13.5f), new Vector3(2, 5, 19), Stone);                           // 町側（門の左右）
            Box(new Vector3(inner, 2.5f, -13.5f), new Vector3(2, 5, 19), Stone);
            // 城門（屋根つき）
            Box(new Vector3(inner, 6.2f, 0), new Vector3(3, 2.4f, 10), Plaster);
            Box(new Vector3(inner, 7.6f, 0), new Vector3(4.4f, 0.5f, 12), Tile);
            foreach (float z in new[] { -4.3f, 4.3f }) Block(PrimitiveType.Cylinder, _map, new Vector3(inner, 2.5f, z), new Vector3(0.6f, 2.5f, 0.6f), Timber);
            // 壁の上へ上がる坂（内側）
            Ramp(new Vector3(c.x - s * 6f, 0.05f, 19.5f), new Vector3(c.x - s * 14f, 5f, 19.5f), 3f, Stone);
            Ramp(new Vector3(c.x - s * 6f, 0.05f, -19.5f), new Vector3(c.x - s * 14f, 5f, -19.5f), 3f, Stone);
            // 石垣と天守閣
            var b = new Vector3(c.x + s * 6f, 0, 0);
            Box(b + Vector3.up * 3f, new Vector3(14, 6, 16), Stone);
            Ramp(new Vector3(b.x - s * 1f, 0.05f, 17f), new Vector3(b.x - s * 1f, 6f, 8.2f), 3.5f, Stone);
            float y = 6; float[] w = { 10, 7.5f, 5 }, h = { 4.5f, 3.8f, 3.2f };
            for (int i = 0; i < 3; i++)
            {
                Box(b + Vector3.up * (y + h[i] * 0.5f), new Vector3(w[i], h[i], w[i] * 1.1f), Plaster);
                Box(b + Vector3.up * (y + h[i] + 0.3f), new Vector3(w[i] + 3f, 0.6f, w[i] * 1.1f + 3f), Tile);
                for (int k = -1; k <= 1; k++) Block(PrimitiveType.Cube, _map, b + new Vector3(-s * (w[i] * 0.5f + 0.02f), y + h[i] * 0.55f, k * w[i] * 0.3f), new Vector3(0.05f, 0.8f, 0.6f), Timber, false);
                y += h[i] + 0.6f;
            }
            foreach (float z in new[] { -1.2f, 1.2f }) Block(PrimitiveType.Sphere, _map, b + new Vector3(0, y + 0.5f, z), new Vector3(0.6f, 0.8f, 0.4f), GoldC, false, null, true);
            // のぼりと篝火
            for (int k = -2; k <= 2; k++)
            {
                var p = new Vector3(inner - s * 2.5f, 0, k * 4.5f);
                if (Mathf.Abs(k) < 1) continue;
                Box(p + Vector3.up * 3.2f, new Vector3(0.15f, 6.4f, 0.15f), Timber, false);
                Box(p + new Vector3(0, 4.2f, 0.55f), new Vector3(0.04f, 3.6f, 1f), col, false);
            }
            foreach (float z in new[] { -6f, 6f })
            {
                Block(PrimitiveType.Cylinder, _map, new Vector3(inner - s * 3f, 0.6f, z), new Vector3(0.6f, 0.6f, 0.6f), Timber);
                Block(PrimitiveType.Sphere, _map, new Vector3(inner - s * 3f, 1.4f, z), new Vector3(0.7f, 0.9f, 0.7f), new Color(1f, 0.55f, 0.2f), false, null, true);
            }
        }

        /// <summary>城下町（s=-1 西・1 東）。通り：z=0（城門〜谷）・z=±18（橋へ）・x=±36 と ±20（南北）。</summary>
        void Town(float s)
        {
            var rng = new System.Random(s < 0 ? 31 : 31); // 左右で同じ並び（公平に）
            // 通り（少し色のちがう地面）
            Block(PrimitiveType.Cube, _map, new Vector3(s * 29, 0.01f, 0), new Vector3(34, 0.02f, 7), Road, false);
            foreach (float z in new[] { 18f, -18f }) Block(PrimitiveType.Cube, _map, new Vector3(s * 28, 0.01f, z), new Vector3(36, 0.02f, 5), Road, false);
            foreach (float x in new[] { 36f, 20f }) Block(PrimitiveType.Cube, _map, new Vector3(s * x, 0.01f, 0), new Vector3(5, 0.02f, 64), Road, false);

            // 町家の区画（x の範囲, z の範囲）
            var blocks = new[]
            {
                (44f, 39f, 4.5f, 15.5f), (33f, 23f, 4.5f, 15.5f), (17f, 12f, 4.5f, 15.5f),
                (44f, 39f, 20.5f, 31f),  (33f, 23f, 20.5f, 31f),  (17f, 12f, 20.5f, 31f),
                (44f, 39f, -15.5f, -4.5f), (33f, 23f, -15.5f, -4.5f), (17f, 12f, -15.5f, -4.5f),
                (44f, 39f, -31f, -20.5f),  (33f, 23f, -31f, -20.5f),  (17f, 12f, -31f, -20.5f),
            };
            int bi = 0;
            foreach (var (xa, xb, za, zb) in blocks)
            {
                bi++;
                float x0 = s * xa, x1 = s * xb;
                float cx = (x0 + x1) * 0.5f, wx = Mathf.Abs(x0 - x1);
                if (bi == 2) { FireTower(new Vector3(cx, 0, (za + zb) * 0.5f)); continue; }
                if (bi == 8) { Kura(new Vector3(cx, 0, (za + zb) * 0.5f)); continue; }
                if (bi == 5 || bi == 7 || bi == 10) continue; // 神木の社・五重塔・物見櫓の場所
                // 区画を2〜3軒に分ける（間に1.2mのすき間＝跳べる）
                int n = (zb - za) > 9 ? 2 : 1;
                float seg = (zb - za) / n;
                for (int k = 0; k < n; k++)
                {
                    float z0 = za + k * seg + 0.6f, z1 = za + (k + 1) * seg - 0.6f;
                    float hgt = 3.6f + (float)rng.NextDouble() * 2.2f;
                    Machiya(new Vector3(cx, 0, (z0 + z1) * 0.5f), new Vector3(wx - 0.8f, hgt, z1 - z0), s);
                }
            }
            // 通りの灯籠・屋台・桜
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(s * (14 + i * 5.5f), 0, i % 2 == 0 ? 3.9f : -3.9f);
                Box(p + Vector3.up * 0.6f, new Vector3(0.35f, 1.2f, 0.35f), new Color(0.5f, 0.5f, 0.52f));
                Block(PrimitiveType.Sphere, _map, p + Vector3.up * 1.45f, Vector3.one * 0.45f, new Color(1f, 0.75f, 0.4f), false, null, true);
            }
            foreach (var p in new[] { new Vector3(s * 28, 0, 2.6f), new Vector3(s * 40, 0, -2.6f) })
            {
                Box(p + Vector3.up * 0.5f, new Vector3(2.4f, 1f, 1.2f), Timber);
                Box(p + Vector3.up * 2.1f, new Vector3(2.8f, 0.12f, 1.6f), s < 0 ? new Color(0.1f, 0.6f, 0.62f) : Red, false);
                foreach (float dx in new[] { -1.2f, 1.2f }) Box(p + new Vector3(dx, 1.5f, 0.6f), new Vector3(0.08f, 1.2f, 0.08f), Timber, false);
            }
            foreach (var p in new[] { new Vector3(s * 44, 0, 34f), new Vector3(s * 44, 0, -34f) })
            {
                Block(PrimitiveType.Cylinder, _map, p + Vector3.up * 1.8f, new Vector3(0.4f, 1.8f, 0.4f), Timber);
                for (int k = 0; k < 5; k++) Block(PrimitiveType.Sphere, _map, p + new Vector3(Mathf.Cos(k) * 1.4f, 4.2f + (k % 2) * 0.6f, Mathf.Sin(k) * 1.4f), Vector3.one * 2.4f, Pink, false);
            }
        }

        /// <summary>町家（白い壁・格子・瓦の平らな屋根＝上を歩ける）。</summary>
        void Machiya(Vector3 at, Vector3 size, float s)
        {
            Box(at + Vector3.up * size.y * 0.5f, size, Plaster);
            Box(at + Vector3.up * (size.y + 0.2f), new Vector3(size.x + 0.6f, 0.4f, size.z + 0.4f), Tile);
            Block(PrimitiveType.Cube, _map, at + Vector3.up * (size.y + 0.55f), new Vector3(size.x * 0.9f, 0.3f, 0.35f), new Color(0.15f, 0.16f, 0.2f), false);
            // 通り側の格子と暖簾
            float fx = -s * (size.x * 0.5f + 0.02f);
            Block(PrimitiveType.Cube, _map, at + new Vector3(fx, size.y * 0.35f, 0), new Vector3(0.05f, size.y * 0.5f, size.z * 0.7f), Timber, false);
            Block(PrimitiveType.Cube, _map, at + new Vector3(fx * 1.01f, size.y * 0.55f, 0), new Vector3(0.04f, 0.6f, Mathf.Min(1.6f, size.z * 0.3f)), Random.value < 0.5f ? new Color(0.15f, 0.25f, 0.5f) : Red, false);
            Block(PrimitiveType.Cube, _map, at + new Vector3(0, size.y * 0.75f, 0), new Vector3(size.x + 0.3f, 0.12f, size.z + 0.3f), Tile, false); // 庇
        }

        /// <summary>火の見やぐら（高さ12m、ワイヤーの引っかけ場所・狙撃の場所）。坂で上の台へ。</summary>
        void FireTower(Vector3 at)
        {
            foreach (float dx in new[] { -1.4f, 1.4f }) foreach (float dz in new[] { -1.4f, 1.4f })
                Box(at + new Vector3(dx, 6, dz), new Vector3(0.3f, 12, 0.3f), Timber);
            Box(at + Vector3.up * 12.1f, new Vector3(4, 0.3f, 4), Timber);
            Box(at + Vector3.up * 14.5f, new Vector3(4.6f, 0.3f, 4.6f), Tile);
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * 13.3f, Vector3.one * 0.6f, GoldC, false, null, true);
            for (int i = 0; i < 4; i++) Box(at + Vector3.up * (2.5f + i * 2.7f), new Vector3(3, 0.12f, 0.12f), Timber, false);
        }

        /// <summary>蔵（白壁・黒い腰・高さ8m）。</summary>
        void Kura(Vector3 at)
        {
            Box(at + Vector3.up * 4f, new Vector3(6, 8, 7), Plaster);
            Box(at + Vector3.up * 1.2f, new Vector3(6.1f, 2.4f, 7.1f), new Color(0.12f, 0.12f, 0.14f));
            Box(at + Vector3.up * 8.3f, new Vector3(7, 0.6f, 8), Tile);
        }

        /// <summary>北の山：1段目（y 6）を西と東の坂から、2段目（y 12）を南の坂から登る。上に北の櫓。</summary>
        void Mountain()
        {
            Box(new Vector3(0, -2, 46), new Vector3(44, 16, 24), Cliff);
            Box(new Vector3(0, 6.02f, 46), new Vector3(44, 0.05f, 24), Grass);
            Box(new Vector3(0, 6, 50), new Vector3(20, 12, 16), Cliff);
            Box(new Vector3(0, 12.02f, 50), new Vector3(20, 0.05f, 16), Grass);
            foreach (float s in new[] { -1f, 1f })
            {
                Ramp(new Vector3(s * 35, 0.05f, 44), new Vector3(s * 22, 6, 44), 4.5f, Road);
                Ramp(new Vector3(s * 7, 6, 35), new Vector3(s * 7, 12, 42.2f), 3.5f, Road);
                // 山の竹と岩
                for (int k = 0; k < 6; k++)
                {
                    var p = new Vector3(s * (13 + (k % 3) * 2.5f), 6, 52 + (k / 3) * 3);
                    Block(PrimitiveType.Cylinder, _map, p + Vector3.up * 4f, new Vector3(0.3f, 4, 0.3f), Bamboo);
                }
                Block(PrimitiveType.Sphere, _map, new Vector3(s * 16, 6.6f, 38), new Vector3(3, 2, 2.6f), Stone);
            }
            Tower2(N0, new Color(0.9f, 0.9f, 0.9f));
        }

        /// <summary>南の湖：浅い水（y -0.6）、浮き島（y 1）に南の櫓、西と東から桟橋。</summary>
        void Lake()
        {
            Box(new Vector3(0, -5.8f, -46), new Vector3(52, 8.4f, 24), new Color(0.3f, 0.32f, 0.3f));  // 湖の底（y -1.6）
            Block(PrimitiveType.Cube, _map, new Vector3(0, -0.6f, -46), new Vector3(52, 0.05f, 24), Water, false);
            Box(new Vector3(0, -0.3f, -46), new Vector3(12, 2.6f, 12), Cliff);
            Box(new Vector3(0, 1.02f, -46), new Vector3(12, 0.05f, 12), Grass);
            foreach (float s in new[] { -1f, 1f })
            {
                // 桟橋（ひと続き）と、飛び石
                Ramp(new Vector3(s * 26.5f, 0.05f, -42), new Vector3(s * 5.8f, 1.0f, -42), 3.2f, Timber);
                // 桟橋の欄干（落ちにくいように）
                foreach (float side in new[] { -1.65f, 1.65f })
                {
                    var a = new Vector3(s * 24f, 0.6f, -42 + side); var b = new Vector3(s * 6.2f, 1.45f, -42 + side);
                    var rail = Block(PrimitiveType.Cube, _map, (a + b) * 0.5f, new Vector3(0.15f, 0.9f, (b - a).magnitude), Red);
                    rail.transform.rotation = Quaternion.LookRotation(b - a);
                }
                Ramp(new Vector3(s * 11.5f, -1.6f, -48.5f), new Vector3(s * 5.9f, 1.0f, -48.5f), 3f, Cliff); // 湖から浮き島へ上がる坂
                for (int i = 0; i < 6; i++) Box(new Vector3(s * (10 + i * 2.6f), -0.25f, -42), new Vector3(0.25f, 1f, 0.25f), Timber, false);
                for (int i = 0; i < 4; i++) Box(new Vector3(s * (9 + i * 4.2f), 0.0f, -51 + (i % 2) * 1.5f), new Vector3(2.2f, 0.4f, 2.2f), Stone);
                // 小舟
                Box(new Vector3(s * 14, -0.35f, -49), new Vector3(1.6f, 0.5f, 4.4f), Timber);
                // 岸から上がる坂
                Ramp(new Vector3(s * 23.5f, -1.6f, -54), new Vector3(s * 28.5f, 0.05f, -54), 4f, Road);
            }
            Tower2(S0, new Color(0.9f, 0.9f, 0.9f));
        }

        /// <summary>試合の櫓（低めの物見台。坂で上がれる）。まわりの円が占拠の場所。</summary>
        void Tower2(Vector3 at, Color c)
        {
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                Box(at + new Vector3(sx * 2.4f, 2.5f, sz * 2.4f), new Vector3(0.5f, 5, 0.5f), Timber);
            Box(at + Vector3.up * 5.2f, new Vector3(6, 0.4f, 6), Timber);
            Box(at + Vector3.up * 8f, new Vector3(7.5f, 0.5f, 7.5f), Tile);
            foreach (float sx in new[] { -1f, 1f }) Box(at + new Vector3(sx * 2.4f, 6.6f, -2.4f), new Vector3(0.25f, 2.8f, 0.25f), Timber, false);
            if (at.z > 0) Ramp(at + new Vector3(0, 0.05f, 10.5f), at + new Vector3(0, 5.4f, 3f), 2.4f, Timber); // 北の櫓だけ、うしろの坂で上の台へ
            // 占拠の円（地面の光る輪）
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2;
                Block(PrimitiveType.Cube, _map, at + new Vector3(Mathf.Cos(a) * CaptureR / KX, 0.06f, Mathf.Sin(a) * CaptureR / KX), new Vector3(0.9f, 0.04f, 0.12f), Color.white, false, new Vector3(0, -a * Mathf.Rad2Deg + 90, 0), true);
            }
        }

        const float CaptureR0 = 6.5f;
        /// <summary>櫓の占拠の円の半径（試合場の m）。</summary>
        public const float CaptureR = 10f;

        // ───────── 移動わざが活きる、大きな物（神木・五重塔・物見櫓・杉林・大岩・土塁・谷の岩の柱）─────────
        //  ここの数は「元の数」。横は KX＝2.7倍、高さは KY＝1.35倍になるので、細い物は横を小さめに書く。

        /// <summary>神木（太い幹と大きな葉のかたまり。しめ縄つき）。</summary>
        void SacredTree(Vector3 at, float h)
        {
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * h * 0.5f, new Vector3(0.9f, h * 0.5f, 0.9f), new Color(0.32f, 0.22f, 0.15f));
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * 2.2f, new Vector3(1.05f, 0.08f, 1.05f), new Color(0.95f, 0.92f, 0.8f), false);
            var greens = new[] { new Color(0.22f, 0.42f, 0.22f), new Color(0.28f, 0.5f, 0.26f), new Color(0.2f, 0.38f, 0.2f) };
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.PI * 2;
                var off = new Vector3(Mathf.Cos(a) * 2.4f, (k % 3) * 1.2f, Mathf.Sin(a) * 2.4f);
                Block(PrimitiveType.Sphere, _map, at + Vector3.up * (h - 1f) + off, new Vector3(3.6f, 5f, 3.6f), greens[k % 3]);
            }
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * (h + 1.5f), new Vector3(4.2f, 6f, 4.2f), greens[1]);
        }

        /// <summary>杉（細い幹と、円すいの葉を3段）。</summary>
        void Cedar(Vector3 at, float h)
        {
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * h * 0.5f, new Vector3(0.3f, h * 0.5f, 0.3f), new Color(0.35f, 0.24f, 0.16f));
            for (int k = 0; k < 3; k++)
            {
                var c = new GameObject("杉の葉"); c.transform.SetParent(_map, false);
                c.transform.localPosition = at + Vector3.up * (h * 0.45f + k * h * 0.17f);
                float w = 1.6f - k * 0.4f;
                c.transform.localScale = new Vector3(w, h * 0.32f, w);
                c.AddComponent<MeshFilter>().sharedMesh = Doll.Cone;
                c.AddComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.16f, 0.32f, 0.2f));
                c.AddComponent<MeshCollider>().sharedMesh = Doll.Cone;
            }
        }

        /// <summary>五重塔（町の中の高い建物。屋根ごとに足場になる）。</summary>
        void TownPagoda(Vector3 at)
        {
            float y = 0;
            for (int i = 0; i < 5; i++)
            {
                float w = 3.4f - i * 0.4f;
                Box(at + Vector3.up * (y + 1.8f), new Vector3(w, 3.6f, w), i % 2 == 0 ? Red : Timber);
                Box(at + Vector3.up * (y + 3.8f), new Vector3(w + 1.6f, 0.4f, w + 1.6f), Tile);
                y += 4.2f;
            }
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * (y + 2.5f), new Vector3(0.15f, 2.5f, 0.15f), GoldC);
        }

        /// <summary>物見櫓（高い足場。坂で上がれる）。</summary>
        void Watchtower(Vector3 at, float s)
        {
            foreach (float dx in new[] { -1.2f, 1.2f }) foreach (float dz in new[] { -1.2f, 1.2f })
                Box(at + new Vector3(dx, 6, dz), new Vector3(0.25f, 12, 0.25f), Timber);
            Box(at + Vector3.up * 12.2f, new Vector3(3.6f, 0.4f, 3.6f), Timber);
            Box(at + Vector3.up * 15f, new Vector3(4.2f, 0.4f, 4.2f), Tile);
            foreach (float dx in new[] { -1.6f, 1.6f }) Box(at + new Vector3(dx, 13.1f, 0), new Vector3(0.15f, 1.4f, 3.6f), Red);
        }

        void BigFeatures(float s)
        {
            // 城下町：神木の社（区画5）・五重塔（区画7）・物見櫓（区画10）
            SacredTree(new Vector3(s * 28, 0, 26), 18f);
            Box(new Vector3(s * 28, 0.15f, 26), new Vector3(9.5f, 0.3f, 9.5f), Stone);
            foreach (float dx in new[] { -1.3f, 1.3f }) Block(PrimitiveType.Cylinder, _map, new Vector3(s * 28 + dx, 2.4f, 21.4f), new Vector3(0.25f, 2.4f, 0.25f), Red);
            Box(new Vector3(s * 28, 4.9f, 21.4f), new Vector3(3.6f, 0.3f, 0.4f), Red);
            TownPagoda(new Vector3(s * 41.5f, 0, -10f));
            Watchtower(new Vector3(s * 41.5f, 0, -26f), s);
            // 城の外の野原：杉林・大岩・土塁（北と南）
            var rng = new System.Random(77);
            foreach (float sz in new[] { 1f, -1f })
            {
                for (int k = 0; k < 14; k++)
                {
                    float x = 52 + (float)rng.NextDouble() * 28f, z = sz * (27 + (float)rng.NextDouble() * 28f);
                    Cedar(new Vector3(s * x, 0, z), 12f + (float)rng.NextDouble() * 6f);
                }
                for (int k = 0; k < 4; k++)
                {
                    float x = 54 + k * 7f, z = sz * (30 + (k % 2) * 14f);
                    Block(PrimitiveType.Sphere, _map, new Vector3(s * x, 0.6f, z), new Vector3(3f, 3.2f, 2.6f), new Color(0.48f, 0.46f, 0.44f));
                }
                // 土塁（盛り土の壁。かくれる場所）
                Box(new Vector3(s * 60, 1.1f, sz * 40), new Vector3(10f, 2.2f, 1.4f), new Color(0.42f, 0.36f, 0.28f));
                Box(new Vector3(s * 72, 1.1f, sz * 34), new Vector3(1.4f, 2.2f, 8f), new Color(0.42f, 0.36f, 0.28f));
            }
            // 山の上の大きな杉
            Cedar(new Vector3(s * 8, 12, 56), 16f);
            // 湖の大岩（水面から出ている）
            Block(PrimitiveType.Sphere, _map, new Vector3(s * 14, -1f, -52f), new Vector3(2.6f, 3.6f, 2.4f), new Color(0.45f, 0.44f, 0.42f));
        }

        /// <summary>谷底からそびえる岩の柱（地面より上まで出ている。谷を渡るときの引っかけ場所）。</summary>
        void ValleySpires()
        {
            foreach (var (x, z) in new[] { (-3f, 8f), (3f, -8f), (-2.5f, -24f), (2.5f, 24f) })
            {
                Block(PrimitiveType.Cylinder, _map, new Vector3(x, -2f, z), new Vector3(1.8f, 8f, 1.8f), Cliff);
                Block(PrimitiveType.Cylinder, _map, new Vector3(x, 6.05f, z), new Vector3(2f, 0.1f, 2f), Grass);
            }
        }


        /// <summary>本陣のしるし：城門の内側の地面に光る帯と、回復の灯り。</summary>
        void BaseMarks(int team)
        {
            float s = team == 1 ? -1 : 1;
            var col = team == 1 ? new Color(0.3f, 1f, 1f) : new Color(1f, 0.45f, 0.35f);
            for (int i = 0; i < 18; i++)
                Block(PrimitiveType.Cube, _map, new Vector3(s * 50.5f, 0.05f, -8.5f + i), new Vector3(0.25f, 0.04f, 0.6f), col, false, null, true);
            foreach (float z in new[] { -11f, 11f })
            {
                Block(PrimitiveType.Cylinder, _map, new Vector3(s * 52, 0.8f, z), new Vector3(0.25f, 0.8f, 0.25f), Timber);
                Block(PrimitiveType.Sphere, _map, new Vector3(s * 52, 1.9f, z), Vector3.one * 0.6f, col, false, null, true);
            }
        }
    }
}
