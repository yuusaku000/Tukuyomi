using System.Collections.Generic;
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
        /// <summary>陣地（城の中）の高台の高さ（元の数。まわりより高い）。</summary>
        public const float CampH = 4f;
        public static Vector3 DarumaSpot(int team) => Sx(new Vector3(team == 1 ? -55 : 55, CampH, 0));
        public static Vector3 SpawnOf(int team, int i) => Sx(new Vector3(team == 1 ? -60 : 60, CampH + 0.2f, (i - 1) * 4.5f + 8f));
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
                Box(new Vector3(s * 55, -0.5f, 0), new Vector3(58, 1, 116), CGrass);           // x 26〜84
                Box(new Vector3(s * 17.5f, -0.5f, 12), new Vector3(17, 1, 92), CGrass);        // x 9〜26、z -34〜58
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
                Box(p + Vector3.up * 0.62f, new Vector3(4.5f, 0.05f, 4.5f), CGrass);
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
            if (_theme == 1) { OnPlateau(() => { Palisade(1); Palisade(2); }); LakeNature(); ForestNature(); }
            ValleySpires();
            Mountain();
            Lake();
            OnPlateau(() => { BaseMarks(1); BaseMarks(2); });

            // まわりの山（見た目）・見えない壁・ずっと下の地面
            for (int i = 0; i < (_theme == 1 ? 0 : 28); i++)
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
            Physics.SyncTransforms(); // 広げたあとの当たり判定を、すぐの足もと調べ（見張りの置き場所）に使えるように
        }

        /// <summary>城（team 1 西・2 東）。城壁と城門、二の丸の広場、石垣と天守閣。</summary>
        /// <summary>陣地の高台の上に作る（a の中の Box や坂は、高台の高さだけ上がる）。</summary>
        void OnPlateau(System.Action a)
        {
            var keepMap = _map; var keepRamp = _rampRoot;
            var hi = new GameObject("高台").transform; hi.SetParent(_map, false); hi.localPosition = Vector3.up * CampH; _map = hi;
            if (_rampRoot != null) { var hr = new GameObject("高台の坂").transform; hr.SetParent(_rampRoot, false); hr.localPosition = Vector3.up * CampH * KY; _rampRoot = hr; }
            try { a(); } finally { _map = keepMap; _rampRoot = keepRamp; }
        }

        /// <summary>陣地：高台（三方は崖、町側は城門へ上がる広い坂）と、その上の城。</summary>
        void Castle2(int team)
        {
            float s = team == 1 ? -1 : 1;
            Box(new Vector3(s * 65.5f, (CampH - 1f) * 0.5f, 0), new Vector3(37, CampH + 1f, 48), Cliff);
            Box(new Vector3(s * 65.5f, CampH + 0.01f, 0), new Vector3(37, 0.05f, 48), CGrass);
            Ramp(new Vector3(s * 37.2f, 0.05f, 0), new Vector3(s * 47.2f, CampH, 0), 8f, Road);
            OnPlateau(() => Castle2Body(team));
        }

        void Castle2Body(int team)
        {
            float s = team == 1 ? -1 : 1;
            var c = Castle0(team);
            var col = team == 1 ? new Color(0.1f, 0.6f, 0.62f) : Red;
            // 城壁（高さ5・上を歩ける）。町側（内側）の真ん中に城門
            float inner = c.x - s * 18f; // 町側の壁の x
            Box(new Vector3(c.x + s * 16f, 2.5f, 0), new Vector3(2, 5, 46), CStone);                       // 外側
            Box(new Vector3(c.x, 2.5f, 22), new Vector3(34, 5, 2), CStone);
            Box(new Vector3(c.x, 2.5f, -22), new Vector3(34, 5, 2), CStone);
            Box(new Vector3(inner, 2.5f, 13.5f), new Vector3(2, 5, 19), CStone);                           // 町側（門の左右）
            Box(new Vector3(inner, 2.5f, -13.5f), new Vector3(2, 5, 19), CStone);
            // 城門（屋根つき）
            Box(new Vector3(inner, 6.2f, 0), new Vector3(3, 2.4f, 10), CWall);
            Box(new Vector3(inner, 7.6f, 0), new Vector3(4.4f, 0.5f, 12), CRoof);
            foreach (float z in new[] { -4.3f, 4.3f }) Block(PrimitiveType.Cylinder, _map, new Vector3(inner, 2.5f, z), new Vector3(0.6f, 2.5f, 0.6f), Timber);
            // 壁の上へ上がる坂（内側）
            Ramp(new Vector3(c.x - s * 6f, 0.05f, 19.5f), new Vector3(c.x - s * 14f, 5f, 19.5f), 3f, CStone);
            Ramp(new Vector3(c.x - s * 6f, 0.05f, -19.5f), new Vector3(c.x - s * 14f, 5f, -19.5f), 3f, CStone);
            // 石垣と天守閣
            var b = new Vector3(c.x + s * 6f, 0, 0);
            Box(b + Vector3.up * 3f, new Vector3(14, 6, 16), CStone);
            Ramp(new Vector3(b.x - s * 1f, 0.05f, 17f), new Vector3(b.x - s * 1f, 6f, 8.2f), 3.5f, CStone);
            float y = 6; float[] w = { 10, 7.5f, 5 }, h = { 4.5f, 3.8f, 3.2f };
            for (int i = 0; i < 3; i++)
            {
                Box(b + Vector3.up * (y + h[i] * 0.5f), new Vector3(w[i], h[i], w[i] * 1.1f), CWall);
                Box(b + Vector3.up * (y + h[i] + 0.3f), new Vector3(w[i] + 3f, 0.6f, w[i] * 1.1f + 3f), CRoof);
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
            if (_theme == 1) { Forest(s); return; }
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
            Box(at + Vector3.up * size.y * 0.5f, size, CWall);
            Box(at + Vector3.up * (size.y + 0.2f), new Vector3(size.x + 0.6f, 0.4f, size.z + 0.4f), CRoof);
            Block(PrimitiveType.Cube, _map, at + Vector3.up * (size.y + 0.55f), new Vector3(size.x * 0.9f, 0.3f, 0.35f), new Color(0.15f, 0.16f, 0.2f), false);
            // 通り側の格子と暖簾
            float fx = -s * (size.x * 0.5f + 0.02f);
            Block(PrimitiveType.Cube, _map, at + new Vector3(fx, size.y * 0.35f, 0), new Vector3(0.05f, size.y * 0.5f, size.z * 0.7f), Timber, false);
            Block(PrimitiveType.Cube, _map, at + new Vector3(fx * 1.01f, size.y * 0.55f, 0), new Vector3(0.04f, 0.6f, Mathf.Min(1.6f, size.z * 0.3f)), Random.value < 0.5f ? new Color(0.15f, 0.25f, 0.5f) : Red, false);
            Block(PrimitiveType.Cube, _map, at + new Vector3(0, size.y * 0.75f, 0), new Vector3(size.x + 0.3f, 0.12f, size.z + 0.3f), CRoof, false); // 庇
        }

        /// <summary>火の見やぐら（高さ12m、ワイヤーの引っかけ場所・狙撃の場所）。坂で上の台へ。</summary>
        void FireTower(Vector3 at)
        {
            foreach (float dx in new[] { -1.4f, 1.4f }) foreach (float dz in new[] { -1.4f, 1.4f })
                Box(at + new Vector3(dx, 6, dz), new Vector3(0.3f, 12, 0.3f), Timber);
            Box(at + Vector3.up * 12.1f, new Vector3(4, 0.3f, 4), Timber);
            Box(at + Vector3.up * 14.5f, new Vector3(4.6f, 0.3f, 4.6f), CRoof);
            Block(PrimitiveType.Sphere, _map, at + Vector3.up * 13.3f, Vector3.one * 0.6f, GoldC, false, null, true);
            for (int i = 0; i < 4; i++) Box(at + Vector3.up * (2.5f + i * 2.7f), new Vector3(3, 0.12f, 0.12f), Timber, false);
        }

        /// <summary>蔵（白壁・黒い腰・高さ8m）。</summary>
        void Kura(Vector3 at)
        {
            Box(at + Vector3.up * 4f, new Vector3(6, 8, 7), CWall);
            Box(at + Vector3.up * 1.2f, new Vector3(6.1f, 2.4f, 7.1f), new Color(0.12f, 0.12f, 0.14f));
            Box(at + Vector3.up * 8.3f, new Vector3(7, 0.6f, 8), CRoof);
        }

        /// <summary>北の山：1段目（y 6）を西と東の坂から、2段目（y 12）を南の坂から登る。上に北の櫓。</summary>
        void Mountain()
        {
            Box(new Vector3(0, -2, 46), new Vector3(44, 16, 24), Cliff);
            Box(new Vector3(0, 6.02f, 46), new Vector3(44, 0.05f, 24), CGrass);
            Box(new Vector3(0, 6, 50), new Vector3(20, 12, 16), Cliff);
            Box(new Vector3(0, 12.02f, 50), new Vector3(20, 0.05f, 16), CGrass);
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
                Block(PrimitiveType.Sphere, _map, new Vector3(s * 16, 6.6f, 38), new Vector3(3, 2, 2.6f), CStone);
            }
            Tower2(N0, new Color(0.9f, 0.9f, 0.9f));
        }

        /// <summary>南の湖：浅い水（y -0.6）、浮き島（y 1）に南の櫓、西と東から桟橋。</summary>
        void Lake()
        {
            Box(new Vector3(0, -5.8f, -46), new Vector3(52, 8.4f, 24), new Color(0.3f, 0.32f, 0.3f));  // 湖の底（y -1.6）
            Block(PrimitiveType.Cube, _map, new Vector3(0, -0.6f, -46), new Vector3(52, 0.05f, 24), Water, false);
            Box(new Vector3(0, -0.3f, -46), new Vector3(12, 2.6f, 12), Cliff);
            Box(new Vector3(0, 1.02f, -46), new Vector3(12, 0.05f, 12), CGrass);
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
                for (int i = 0; i < 4; i++) Box(new Vector3(s * (9 + i * 4.2f), 0.0f, -51 + (i % 2) * 1.5f), new Vector3(2.2f, 0.4f, 2.2f), CStone);
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
            Box(at + Vector3.up * 8f, new Vector3(7.5f, 0.5f, 7.5f), CRoof);
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

        // ───────── 森の舞台の高低差（段々の丘・岩の台地）─────────

        readonly List<(Vector2 c, float r)> _hills = new List<(Vector2, float)>();

        /// <summary>段々の丘：levels 段の岩の台（1段の高さ 3）。頂上に木。1段目までは自然な坂。</summary>
        void TerraceHill(Vector3 at, float r, int levels, System.Random rng)
        {
            float yaw = (float)rng.NextDouble() * 90f;
            var q = Quaternion.Euler(0, yaw, 0);
            float y = 0;
            Vector3 top = at;
            for (int k = 0; k < levels; k++)
            {
                float w = r * 2f * (1f - k * 0.24f), d = r * 1.7f * (1f - k * 0.22f);
                var off = q * new Vector3((float)(rng.NextDouble() - 0.5) * r * 0.3f * k, 0, (float)(rng.NextDouble() - 0.5) * r * 0.3f * k);
                var c = at + off;
                Block(PrimitiveType.Cube, _map, c + Vector3.up * (y + 1.5f), new Vector3(w, 3f, d), k % 2 == 0 ? Rock : new Color(0.4f, 0.38f, 0.34f), true, new Vector3(0, yaw + k * 9f, 0));
                Block(PrimitiveType.Cube, _map, c + Vector3.up * (y + 3.04f), new Vector3(w, 0.1f, d), CGrass, true, new Vector3(0, yaw + k * 9f, 0));
                if (k == 0)
                {
                    // 1段目へ上がる坂（丘の長い辺の外側から）
                    var side = q * Vector3.forward;
                    var foot = c + side * (d * 0.5f + 7.5f);
                    var lip = c + side * (d * 0.5f - 0.6f) + Vector3.up * 3f;
                    Ramp(new Vector3(foot.x, 0.05f, foot.z), lip, 3.2f, CGrass);
                }
                y += 3f; top = c;
            }
            // 頂上の木と岩
            int n = 1 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                var p = top + new Vector3((float)(rng.NextDouble() - 0.5) * r * 0.6f, y, (float)(rng.NextDouble() - 0.5) * r * 0.6f);
                if (rng.NextDouble() < 0.6) Cedar(p, 8f + (float)rng.NextDouble() * 6f); else RoundTree(p, 5f + (float)rng.NextDouble() * 3f, rng);
            }
            _hills.Add((new Vector2(at.x, at.z), r + 1.5f));
        }

        bool InHill(Vector2 p) { foreach (var (c, r) in _hills) if (Vector2.Distance(p, c) < r) return true; return false; }

        void ForestHills()
        {
            var rng = new System.Random(4242);
            int made = 0;
            for (int tries = 0; tries < 3000 && made < 16; tries++)
            {
                float r = 5f + (float)rng.NextDouble() * 4.5f;
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 78f, (float)(rng.NextDouble() * 2 - 1) * 52f);
                if (!FreeSpot(p2, r + 2.5f) || InHill(p2 + Vector2.zero) || !FreeSpot(p2 + new Vector2(r, 0), 2.5f) || !FreeSpot(p2 - new Vector2(r, 0), 2.5f) || !FreeSpot(p2 + new Vector2(0, r), 2.5f) || !FreeSpot(p2 - new Vector2(0, r), 2.5f)) continue;
                bool clash = false; foreach (var (c, rr) in _hills) if (Vector2.Distance(c, p2) < rr + r + 2f) clash = true;
                if (clash) continue;
                TerraceHill(new Vector3(p2.x, 0, p2.y), r, 2 + rng.Next(3), rng);
                made++;
            }
        }


        // ───────── 森の舞台：全体を自然の中に（たくさんの木・岩と崖・雪山・まわりの山並み）─────────

        static readonly Color Snow = new(0.94f, 0.96f, 1f), Leaf = new(0.26f, 0.46f, 0.22f), Leaf2 = new(0.34f, 0.52f, 0.24f), Bark = new(0.33f, 0.23f, 0.16f);

        /// <summary>武者の通り道（元の数の線分）。木や岩はここから離して置く。</summary>
        static readonly (Vector2 a, Vector2 b)[] PathLines = MakePathLines();

        static (Vector2, Vector2)[] MakePathLines()
        {
            var l = new List<(Vector2, Vector2)>();
            foreach (float s in new[] { -1f, 1f })
            {
                l.Add((new Vector2(s * 36, -34), new Vector2(s * 36, 34))); l.Add((new Vector2(s * 20, -30), new Vector2(s * 20, 30)));
                l.Add((new Vector2(s * 9, 0), new Vector2(s * 48, 0)));
                l.Add((new Vector2(s * 9, 18), new Vector2(s * 46, 18))); l.Add((new Vector2(s * 9, -18), new Vector2(s * 46, -18)));
                l.Add((new Vector2(s * 36, 33), new Vector2(s * 37, 44))); l.Add((new Vector2(s * 37, 44), new Vector2(s * 20, 44)));
                l.Add((new Vector2(s * 19, 44), new Vector2(s * 12.5f, 34.8f))); l.Add((new Vector2(s * 12.5f, 34.8f), new Vector2(s * 7, 34.4f)));
                l.Add((new Vector2(s * 7, 34.4f), new Vector2(s * 7, 43.5f)));
                l.Add((new Vector2(s * 36, -33), new Vector2(s * 29, -42))); l.Add((new Vector2(s * 29, -42), new Vector2(s * 6, -42)));
                l.Add((new Vector2(s * 29, -42), new Vector2(s * 30, -54))); l.Add((new Vector2(s * 20, 11), new Vector2(s * 9, 11)));
            }
            l.Add((new Vector2(-7, 43.5f), new Vector2(7, 43.5f))); l.Add((new Vector2(0, 43), new Vector2(0, 58)));
            return l.ToArray();
        }

        static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>木や岩を置いてよい場所か（通り道・谷・湖・陣地・山の上の道からはなれている）。</summary>
        static bool FreeSpot(Vector2 p, float clear)
        {
            if (Mathf.Abs(p.x) < 11.5f) return false;                                         // 谷とそのふち
            if (Mathf.Abs(p.x) > 45.5f && Mathf.Abs(p.y) < 26f) return false;                 // 陣地の高台
            if (Mathf.Abs(p.x) < 28f && p.y < -32f) return false;                             // 湖
            if (Mathf.Abs(p.x) < 24f && p.y > 32f) return false;                              // 山（別に置く）
            if (Mathf.Abs(p.x) > 82f || Mathf.Abs(p.y) > 56f) return false;
            foreach (var (a, b) in PathLines) if (DistToSeg(p, a, b) < clear) return false;
            return true;
        }

        /// <summary>広葉樹（太めの幹と、丸い葉のかたまり2〜3つ）。</summary>
        void RoundTree(Vector3 at, float h, System.Random rng)
        {
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * h * 0.4f, new Vector3(0.35f, h * 0.4f, 0.35f), Bark);
            int n = 2 + rng.Next(2);
            for (int k = 0; k < n; k++)
            {
                var off = new Vector3((float)(rng.NextDouble() - 0.5) * 1.6f, h * (0.75f + k * 0.15f), (float)(rng.NextDouble() - 0.5) * 1.6f);
                Block(PrimitiveType.Sphere, _map, at + off, new Vector3(2.2f, 3.4f, 2.2f) * (1f - k * 0.15f), k % 2 == 0 ? Leaf : Leaf2);
            }
        }

        /// <summary>雪をかぶった杉。</summary>
        void SnowCedar(Vector3 at, float h)
        {
            Cedar(at, h);
            var c = new GameObject("雪"); c.transform.SetParent(_map, false);
            c.transform.localPosition = at + Vector3.up * (h * 0.45f + 2 * h * 0.17f + h * 0.12f);
            c.transform.localScale = new Vector3(0.75f, h * 0.2f, 0.75f);
            c.AddComponent<MeshFilter>().sharedMesh = Doll.Cone;
            c.AddComponent<MeshRenderer>().sharedMaterial = Mat(Snow);
        }

        /// <summary>大岩の群れ（3〜5個）。</summary>
        void Boulders(Vector3 at, System.Random rng)
        {
            int n = 3 + rng.Next(3);
            for (int k = 0; k < n; k++)
            {
                var off = new Vector3((float)(rng.NextDouble() - 0.5) * 4f, (float)rng.NextDouble() * 1.5f, (float)(rng.NextDouble() - 0.5) * 4f);
                float sz = 1.6f + (float)rng.NextDouble() * 2.2f;
                Block(PrimitiveType.Sphere, _map, at + off, new Vector3(sz, sz * 1.3f, sz * 0.9f), k % 3 == 0 ? Moss : Rock);
            }
        }

        /// <summary>岩の崖（細長い岩の壁。上に立てる）。</summary>
        void CliffWall(Vector3 at, float len, float h, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            for (int k = 0; k < 4; k++)
            {
                float t = (k / 3f - 0.5f) * len * 0.8f;
                var p = at + q * new Vector3(0, 0, t);
                float hh = h * (0.75f + 0.25f * Mathf.Sin(k * 1.9f));
                Block(PrimitiveType.Cube, _map, p + Vector3.up * hh * 0.5f, new Vector3(2.6f, hh, len * 0.3f), Rock, true, new Vector3(0, yaw + k * 7f, 0));
                Block(PrimitiveType.Cube, _map, p + Vector3.up * (hh + 0.05f), new Vector3(2.7f, 0.12f, len * 0.3f), k % 2 == 0 ? Moss : CGrass, true, new Vector3(0, yaw + k * 7f, 0));
            }
        }

        void ForestNature()
        {
            ForestHills();
            var rng = new System.Random(2027);
            // 木をたくさん（通り道からは 3 以上はなす）
            int placed = 0;
            for (int tries = 0; tries < 2400 && placed < 240; tries++)
            {
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 82f, (float)(rng.NextDouble() * 2 - 1) * 56f);
                if (!FreeSpot(p2, 3.2f) || InHill(p2)) continue;
                var at = new Vector3(p2.x, 0, p2.y);
                double r = rng.NextDouble();
                if (r < 0.55) Cedar(at, 10f + (float)rng.NextDouble() * 9f);
                else if (r < 0.92) RoundTree(at, 6f + (float)rng.NextDouble() * 4f, rng);
                else SacredTree(at, 13f + (float)rng.NextDouble() * 5f);
                placed++;
            }
            // 大岩の群れと、岩の崖
            for (int k = 0, tries = 0; k < 26 && tries < 800; tries++)
            {
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 80f, (float)(rng.NextDouble() * 2 - 1) * 54f);
                if (!FreeSpot(p2, 4.5f) || InHill(p2)) continue;
                Boulders(new Vector3(p2.x, 0, p2.y), rng); k++;
            }
            for (int k = 0, tries = 0; k < 12 && tries < 1200; tries++)
            {
                var p2 = new Vector2((float)(rng.NextDouble() * 2 - 1) * 78f, (float)(rng.NextDouble() * 2 - 1) * 52f);
                float yaw = (float)rng.NextDouble() * 180f, len = 10f + (float)rng.NextDouble() * 8f;
                var dir = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                if (!FreeSpot(p2, 6f) || InHill(p2) || !FreeSpot(p2 + dir * len * 0.4f, 4.5f) || !FreeSpot(p2 - dir * len * 0.4f, 4.5f)) continue;
                CliffWall(new Vector3(p2.x, 0, p2.y), len, 5f + (float)rng.NextDouble() * 6f, yaw); k++;
            }
            // 谷のふちと谷底の木・岩
            foreach (float s in new[] { -1f, 1f })
                for (int k = 0; k < 6; k++)
                {
                    float z = -30f + k * 11f;
                    if (Mathf.Abs(Mathf.Abs(z) - 18f) < 4f || Mathf.Abs(z - s * -10f) < 4f) continue;
                    Cedar(new Vector3(s * 10.6f, 0, z), 9f + k % 3 * 2f);
                }
            // 雪山（北の山）：段の上に雪、雪をかぶった杉、岩の崖
            Box(new Vector3(0, 6.08f, 46), new Vector3(44, 0.12f, 24), Snow);
            Box(new Vector3(0, 12.08f, 50), new Vector3(20, 0.12f, 16), Snow);
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 6; k++) SnowCedar(new Vector3(s * (13 + (k % 3) * 3f), 6, 50 + (k / 3) * 4f), 8f + k % 2 * 3f);
                SnowCedar(new Vector3(s * 7.5f, 12, 55.5f), 9f);
                Block(PrimitiveType.Sphere, _map, new Vector3(s * 20.5f, 7f, 55f), new Vector3(3.5f, 4f, 3f), Rock);
                Block(PrimitiveType.Sphere, _map, new Vector3(s * 20.5f, 9.2f, 55f), new Vector3(3f, 1.2f, 2.6f), Snow, false);
            }
            // まわりの景色：雪をいただく尖った山並み（北は高く）と、深い森
            for (int i = 0; i < 34; i++)
            {
                float a = i / 34f * Mathf.PI * 2;
                bool north = Mathf.Sin(a) > 0.3f;
                var p = new Vector3(Mathf.Cos(a) * 125f, -6, Mathf.Sin(a) * 100f);
                float h = north ? 70f + (i % 4) * 14f : 34f + (i % 5) * 8f, w = 34f + (i % 3) * 8f;
                var m = new GameObject("山"); m.transform.SetParent(_map, false); m.transform.localPosition = p; m.transform.localScale = new Vector3(w, h, w);
                m.AddComponent<MeshFilter>().sharedMesh = Doll.Cone; m.AddComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.32f, 0.36f, 0.34f));
                var cap = new GameObject("雪の頂"); cap.transform.SetParent(_map, false); cap.transform.localPosition = p + Vector3.up * h * (north ? 0.55f : 0.68f); cap.transform.localScale = new Vector3(w * (north ? 0.5f : 0.36f), h * (north ? 0.32f : 0.22f), w * (north ? 0.5f : 0.36f));
                cap.AddComponent<MeshFilter>().sharedMesh = Doll.Cone; cap.AddComponent<MeshRenderer>().sharedMaterial = Mat(Snow);
            }
            for (int i = 0; i < 90; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = 1.03f + (float)rng.NextDouble() * 0.15f;
                var p = new Vector3(Mathf.Cos(a) * 86f * rr, 0, Mathf.Sin(a) * 60f * rr);
                if (Mathf.Abs(p.x) < 86f && Mathf.Abs(p.z) < 59f) p *= 1.08f;
                float h = 10f + (float)rng.NextDouble() * 8f;
                var t = new GameObject("森の木"); t.transform.SetParent(_map, false); t.transform.localPosition = p; t.transform.localScale = new Vector3(2.2f, h, 2.2f);
                t.AddComponent<MeshFilter>().sharedMesh = Doll.Cone; t.AddComponent<MeshRenderer>().sharedMaterial = Mat(i % 2 == 0 ? new Color(0.15f, 0.3f, 0.19f) : new Color(0.2f, 0.36f, 0.2f));
            }
        }


        // ───────── 森と自然の合戦場（舞台 1）─────────
        //  城下町と同じ道の配置（武者の道すじが同じ）で、家の代わりに巨木・杉の群れ・竹やぶ・苔むした岩・倒木を置く。
        //  城は木の柵の砦、谷は川の流れる峡谷、北は木々の山、南は葦とハスの湖。

        public static readonly string[] StageNamesSg = { "城下町の合戦場", "森と自然の合戦場" };
        public static readonly string[] StageLines =
        {
            "白壁の城と、瓦屋根の町並み。屋根の上も道になる。",
            "巨木と竹やぶの森。木の柵の砦と、川の流れる峡谷。",
        };
        int _theme; // 0 城下町・1 森
        Color CStone => _theme == 1 ? new Color(0.36f, 0.26f, 0.17f) : Stone;
        Color CWall => _theme == 1 ? new Color(0.55f, 0.42f, 0.28f) : Plaster;
        Color CRoof => _theme == 1 ? new Color(0.42f, 0.33f, 0.18f) : Tile;
        Color CGrass => _theme == 1 ? new Color(0.22f, 0.4f, 0.19f) : Grass;
        static readonly Color Moss = new(0.3f, 0.42f, 0.24f), Rock = new(0.44f, 0.43f, 0.4f);

        void Forest(float s)
        {
            var rng = new System.Random(57);
            // 土の小道（濃い土の色で、細め）
            var dirt = new Color(0.3f, 0.25f, 0.18f);
            Block(PrimitiveType.Cube, _map, new Vector3(s * 29, 0.01f, 0), new Vector3(34, 0.02f, 4.5f), dirt, false);
            foreach (float z in new[] { 18f, -18f }) Block(PrimitiveType.Cube, _map, new Vector3(s * 28, 0.01f, z), new Vector3(36, 0.02f, 3.2f), dirt, false);
            foreach (float x in new[] { 36f, 20f }) Block(PrimitiveType.Cube, _map, new Vector3(s * x, 0.01f, 0), new Vector3(3.2f, 0.02f, 64), dirt, false);
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
                var c = new Vector3(s * (xa + xb) * 0.5f, 0, (za + zb) * 0.5f);
                float wx = Mathf.Abs(xa - xb), wz = zb - za;
                if (bi == 2) { FireTower(c); continue; }   // 物見台（木）
                if (bi == 5 || bi == 7 || bi == 10) continue; // 神木・岩の柱・物見櫓の場所（BigFeatures）
                switch (bi % 4)
                {
                    case 0: // 巨木
                        SacredTree(c, 13f + (float)rng.NextDouble() * 4f);
                        break;
                    case 1 when bi % 3 == 0: // 岩の台地（2〜3段）
                        {
                            float y = 0; int lv = 2 + bi % 2;
                            for (int k = 0; k < lv; k++)
                            {
                                Box(c + Vector3.up * (y + 1.6f), new Vector3((wx - 1f) * (1f - k * 0.25f), 3.2f, (wz - 1f) * (1f - k * 0.25f)), k % 2 == 0 ? Rock : new Color(0.4f, 0.38f, 0.34f));
                                Box(c + Vector3.up * (y + 3.25f), new Vector3((wx - 1f) * (1f - k * 0.25f), 0.1f, (wz - 1f) * (1f - k * 0.25f)), Moss);
                                y += 3.2f;
                            }
                            Cedar(c + Vector3.up * y, 7f);
                        }
                        break;
                    case 1: // 杉の群れ
                        for (int k = 0; k < 4; k++)
                            Cedar(c + new Vector3((float)(rng.NextDouble() - 0.5) * (wx - 1.5f), 0, (float)(rng.NextDouble() - 0.5) * (wz - 1.5f)), 11f + (float)rng.NextDouble() * 6f);
                        break;
                    case 2: // 竹やぶ
                        for (int k = 0; k < 12; k++)
                        {
                            float h = 8f + (float)rng.NextDouble() * 5f;
                            var p = c + new Vector3((float)(rng.NextDouble() - 0.5) * (wx - 1f), 0, (float)(rng.NextDouble() - 0.5) * (wz - 1f));
                            Block(PrimitiveType.Cylinder, _map, p + Vector3.up * h * 0.5f, new Vector3(0.14f, h * 0.5f, 0.14f), Bamboo);
                        }
                        break;
                    default: // 苔むした岩と倒木
                        Block(PrimitiveType.Sphere, _map, c + new Vector3(0, 0.8f, -1f), new Vector3(Mathf.Min(wx, 4.5f), 3.6f, 3.6f), Rock);
                        Block(PrimitiveType.Sphere, _map, c + new Vector3(0, 2.2f, -1f), new Vector3(Mathf.Min(wx, 4.5f) * 0.8f, 1.2f, 3f), Moss, false);
                        var log = Block(PrimitiveType.Cylinder, _map, c + new Vector3(0, 0.45f, 2.5f), new Vector3(0.45f, Mathf.Min(wz * 0.35f, 3f), 0.45f), new Color(0.38f, 0.27f, 0.18f));
                        log.transform.localEulerAngles = new Vector3(90, 0, 0);
                        break;
                }
            }
            // 小道ぞいの石灯籠と、野の花
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(s * (14 + i * 5.5f), 0, i % 2 == 0 ? 3.9f : -3.9f);
                Box(p + Vector3.up * 0.6f, new Vector3(0.35f, 1.2f, 0.35f), Rock);
                Block(PrimitiveType.Sphere, _map, p + Vector3.up * 1.45f, Vector3.one * 0.45f, new Color(1f, 0.75f, 0.4f), false, null, true);
            }
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(s * (12 + (float)rng.NextDouble() * 34f), 0.15f, (float)(rng.NextDouble() * 2 - 1) * 33f);
                Block(PrimitiveType.Sphere, _map, p, new Vector3(0.25f, 0.2f, 0.25f), i % 3 == 0 ? new Color(1f, 0.85f, 0.9f) : i % 3 == 1 ? new Color(0.95f, 0.9f, 0.4f) : new Color(0.7f, 0.75f, 1f), false);
            }
        }

        /// <summary>大きな岩の柱（森の舞台の、五重塔の代わり）。上に立てる。</summary>
        void RockSpire(Vector3 at)
        {
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * 8f, new Vector3(3.2f, 8f, 3.2f), Rock);
            Block(PrimitiveType.Cylinder, _map, at + Vector3.up * 16.05f, new Vector3(3.4f, 0.1f, 3.4f), Moss);
            Block(PrimitiveType.Sphere, _map, at + new Vector3(0, 2f, 0), new Vector3(4.5f, 4f, 4.5f), Rock);
            Cedar(at + Vector3.up * 16f, 6f);
        }

        /// <summary>森の砦：城壁の上に木の杭を並べる。</summary>
        void Palisade(int team)
        {
            float s = team == 1 ? -1 : 1;
            var c = Castle0(team);
            float inner = c.x - s * 18f;
            var wood = new Color(0.4f, 0.29f, 0.19f);
            void Row(Vector3 a, Vector3 b, int n)
            {
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (float)n);
                    Block(PrimitiveType.Cylinder, _map, p + Vector3.up * 5.9f, new Vector3(0.22f, 0.9f, 0.22f), wood, false);
                }
            }
            Row(new Vector3(c.x + s * 16f, 0, -22), new Vector3(c.x + s * 16f, 0, 22), 30);
            Row(new Vector3(c.x - 17, 0, 22), new Vector3(c.x + 17, 0, 22), 22);
            Row(new Vector3(c.x - 17, 0, -22), new Vector3(c.x + 17, 0, -22), 22);
            Row(new Vector3(inner, 0, 4.5f), new Vector3(inner, 0, 22), 12);
            Row(new Vector3(inner, 0, -22), new Vector3(inner, 0, -4.5f), 12);
        }

        /// <summary>森の湖：葦とハスの葉。</summary>
        void LakeNature()
        {
            var rng = new System.Random(9);
            for (int i = 0; i < 26; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 2 - 1) * 24f, -0.55f, -36f - (float)rng.NextDouble() * 20f);
                if (Mathf.Abs(p.x) < 7.5f && p.z < -38f && p.z > -54f) continue;
                Block(PrimitiveType.Cylinder, _map, p, new Vector3(0.9f, 0.01f, 0.9f), new Color(0.25f, 0.5f, 0.28f), false);
                if (i % 3 == 0) Block(PrimitiveType.Sphere, _map, p + Vector3.up * 0.15f, new Vector3(0.25f, 0.2f, 0.25f), new Color(1f, 0.7f, 0.85f), false, null, true);
            }
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < 18; i++)
                {
                    var p = new Vector3(s * (24.5f + (float)rng.NextDouble() * 1.5f), 0, -36f - i * 1.2f);
                    Block(PrimitiveType.Cylinder, _map, p + Vector3.up * 0.9f, new Vector3(0.06f, 0.9f, 0.06f), new Color(0.55f, 0.6f, 0.3f), false);
                }
        }


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
                Box(at + Vector3.up * (y + 3.8f), new Vector3(w + 1.6f, 0.4f, w + 1.6f), CRoof);
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
            Box(at + Vector3.up * 15f, new Vector3(4.2f, 0.4f, 4.2f), CRoof);
            foreach (float dx in new[] { -1.6f, 1.6f }) Box(at + new Vector3(dx, 13.1f, 0), new Vector3(0.15f, 1.4f, 3.6f), Red);
        }

        void BigFeatures(float s)
        {
            // 城下町：神木の社（区画5）・五重塔（区画7）・物見櫓（区画10）
            SacredTree(new Vector3(s * 28, 0, 26), 18f);
            Box(new Vector3(s * 28, 0.15f, 26), new Vector3(9.5f, 0.3f, 9.5f), CStone);
            foreach (float dx in new[] { -1.3f, 1.3f }) Block(PrimitiveType.Cylinder, _map, new Vector3(s * 28 + dx, 2.4f, 21.4f), new Vector3(0.25f, 2.4f, 0.25f), Red);
            Box(new Vector3(s * 28, 4.9f, 21.4f), new Vector3(3.6f, 0.3f, 0.4f), Red);
            if (_theme == 1) RockSpire(new Vector3(s * 41.5f, 0, -10f)); else TownPagoda(new Vector3(s * 41.5f, 0, -10f));
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
                Block(PrimitiveType.Cylinder, _map, new Vector3(x, 6.05f, z), new Vector3(2f, 0.1f, 2f), CGrass);
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
