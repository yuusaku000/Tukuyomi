using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 島の形（世界の設計図）。地形を作るときと、遊んでいるとき（水に落ちたか・地図）の両方で使う。
    ///
    /// 丸い島（中心 (0,150)・半径150）の上に、段が3つ重なる。
    ///   下町 L0 = 1.2m  水路と屋形船
    ///   中段 L1 = 9.2m  商店街・大画面広場・釣り堀・千年桜の丘
    ///   上段 L2 = 21.2m 五重塔・竹林・展望台・神楽殿・鐘楼
    ///   頂上 L3 = 37.2m 月の御殿
    /// 川は頂上の近くから湧いて、上段・中段のふちで滝になり、下町で水路に分かれて海へ出る。
    /// </summary>
    public static class Island
    {
        public const float Ground = 1.2f;
        public static readonly Vector2 Center = new Vector2(0, 150);
        public const float Radius = 150f;

        public struct Level
        {
            public Vector2 c;
            public float r, rise, f1, a1, p1, f2, a2, p2;
            public Level(float cx, float cz, float r, float rise, float f1, float a1, float p1, float f2, float a2, float p2)
            {
                c = new Vector2(cx, cz); this.r = r; this.rise = rise;
                this.f1 = f1; this.a1 = a1; this.p1 = p1; this.f2 = f2; this.a2 = a2; this.p2 = p2;
            }
            public float RadiusAt(float ang) => r * (1 + a1 * Mathf.Sin(f1 * ang + p1) + a2 * Mathf.Sin(f2 * ang + p2));
        }

        public static readonly Level[] Levels =
        {
            new Level(0, 165, 108, 8f, 3, 0.06f, 0.5f, 8, 0.035f, 2f),
            new Level(5, 182, 60, 12f, 4, 0.08f, 1f, 9, 0.04f, 0.3f),
            new Level(0, 204, 26, 16f, 5, 0.06f, 0f, 3, 0.04f, 1f),
        };

        public static float LevelTop(int i)
        {
            float h = Ground;
            for (int k = 0; k < i; k++) h += Levels[k].rise;
            return h;
        }

        /// <summary>千年桜の丘（中段の西）。</summary>
        public static readonly Vector2 HillCenter = new Vector2(-74, 150);
        public const float HillRadius = 26f, HillHeight = 7f;

        /// <summary>釣り堀（中段の南西）。</summary>
        public static readonly Rect Pond = new Rect(-70, 86, 28, 16);
        public const float PondDepth = 1.6f;

        public class Channel
        {
            public string name;
            public Vector2[] pts;
            public float half, depth;
        }

        public static readonly Channel[] Channels =
        {
            new Channel
            {
                name = "天の川", half = 3.2f, depth = 1.9f,
                pts = new[]
                {
                    new Vector2(-12, 212), new Vector2(-26, 206), new Vector2(-40, 197), new Vector2(-52, 188), new Vector2(-64, 180),
                    new Vector2(-80, 176), new Vector2(-94, 170), new Vector2(-104, 160), new Vector2(-114, 150),
                    new Vector2(-124, 136), new Vector2(-131, 116), new Vector2(-133, 94), new Vector2(-127, 72),
                    new Vector2(-112, 50), new Vector2(-90, 34), new Vector2(-62, 27),
                },
            },
            new Channel
            {
                name = "東の水路", half = 4f, depth = 1.9f,
                pts = new[]
                {
                    new Vector2(-62, 27), new Vector2(-30, 28), new Vector2(0, 29), new Vector2(30, 28), new Vector2(60, 26),
                    new Vector2(90, 34), new Vector2(114, 52), new Vector2(130, 74), new Vector2(150, 92), new Vector2(172, 100),
                },
            },
            new Channel
            {
                name = "南の水路", half = 4f, depth = 1.9f,
                pts = new[] { new Vector2(-62, 27), new Vector2(-60, 12), new Vector2(-58, -4), new Vector2(-56, -24) },
            },
        };

        // ───────── 高さ ─────────

        /// <summary>段・丘だけの高さ（川や島のふちを考えない）。</summary>
        public static float TerraceHeight(float x, float z)
        {
            float h = Ground;
            foreach (var L in Levels)
            {
                var d = new Vector2(x - L.c.x, z - L.c.y);
                float dist = d.magnitude;
                float r = L.RadiusAt(Mathf.Atan2(d.y, d.x));
                h += L.rise * (1 - Ts.Smooth(r - 0.7f, r + 0.7f, dist));
            }
            float hd = Vector2.Distance(new Vector2(x, z), HillCenter);
            h += HillHeight * Ts.Smooth(HillRadius, 3f, hd);
            return h;
        }

        public static float EdgeRadius(float ang) => Radius * (1 + 0.035f * Mathf.Sin(5 * ang + 0.7f) + 0.025f * Mathf.Sin(12 * ang + 2f));

        /// <summary>島のふちまでの余り（正なら島の中）。</summary>
        public static float Inside(float x, float z)
        {
            var d = new Vector2(x - Center.x, z - Center.y);
            return EdgeRadius(Mathf.Atan2(d.y, d.x)) - d.magnitude;
        }

        /// <summary>川・水路・池を彫る前の地面（島のふちは海へ下がる）。</summary>
        public static float SurfaceNoWater(float x, float z)
        {
            float h = TerraceHeight(x, z);
            float inside = Inside(x, z);
            return Mathf.Lerp(-3.5f, h, Ts.Smooth(-7f, 0f, inside));
        }

        /// <summary>本当の地面の高さ。</summary>
        public static float Height(float x, float z)
        {
            float h = SurfaceNoWater(x, z);
            foreach (var ch in Channels)
            {
                float d = DistToChannel(ch, x, z, out _);
                if (d < ch.half + 1.2f)
                {
                    float bed = h - ch.depth;
                    h = Mathf.Lerp(bed, h, Ts.Smooth(ch.half - 0.6f, ch.half + 0.5f, d));
                }
            }
            if (PondDistance(x, z) < 0)
                h = SurfaceNoWater(x, z) - PondDepth;
            return Mathf.Max(h, -3.5f);
        }

        public static float PondDistance(float x, float z)
        {
            float dx = Mathf.Max(Pond.xMin - x, x - Pond.xMax);
            float dz = Mathf.Max(Pond.yMin - z, z - Pond.yMax);
            return Mathf.Max(dx, dz);
        }

        /// <summary>そこが水の上なら、水面の高さ（なければ float.NaN）。</summary>
        public static float WaterSurface(float x, float z)
        {
            foreach (var ch in Channels)
                if (DistToChannel(ch, x, z, out _) < ch.half - 0.2f)
                    return Mathf.Max(0.05f, SurfaceNoWater(x, z) - 0.75f);
            if (PondDistance(x, z) < -0.1f) return SurfaceNoWater(x, z) - 0.55f;
            if (Inside(x, z) < -2f) return 0f;
            return float.NaN;
        }

        /// <summary>人が水に落ちている（足が水面より下）。</summary>
        public static bool InWater(Vector3 p)
        {
            float w = WaterSurface(p.x, p.z);
            return !float.IsNaN(w) && p.y < w + 0.05f;
        }

        public static float DistToChannel(Channel ch, float x, float z, out float along)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            along = 0;
            float acc = 0;
            for (int i = 0; i < ch.pts.Length - 1; i++)
            {
                var a = ch.pts[i]; var b = ch.pts[i + 1];
                var ab = b - a;
                float len = ab.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (len * len));
                float d = Vector2.Distance(p, a + ab * t);
                if (d < best) { best = d; along = acc + t * len; }
                acc += len;
            }
            return best;
        }

        public static float NearestWater(float x, float z)
        {
            float best = float.MaxValue;
            foreach (var ch in Channels) best = Mathf.Min(best, DistToChannel(ch, x, z, out _) - ch.half);
            best = Mathf.Min(best, PondDistance(x, z));
            return best;
        }

        /// <summary>なめらかに分けた折れ線（屋形船の道や水面の帯に使う）。</summary>
        public static List<Vector2> Resample(Vector2[] pts, float step)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int k = 0; k < n; k++) list.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            list.Add(pts[pts.Length - 1]);
            // 角を少し丸める
            for (int pass = 0; pass < 3; pass++)
                for (int i = 1; i < list.Count - 1; i++)
                    list[i] = (list[i - 1] + list[i] * 2 + list[i + 1]) / 4;
            return list;
        }
    }
}
