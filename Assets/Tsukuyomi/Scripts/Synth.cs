using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>音を、ファイルを使わずにその場で作る（鐘・風鈴・太鼓と笛・水・ざわめき・花火）。</summary>
    public static class Synth
    {
        public const int Rate = 44100;

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = f(i / (float)Rate);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        sealed class Rng
        {
            uint _s;
            public Rng(uint seed) => _s = seed;
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        /// <summary>お寺の鐘。うなりのある倍音が長く残る。</summary>
        public static AudioClip Bell()
        {
            float[] ratio = { 0.5f, 1f, 1.19f, 1.56f, 2.0f, 2.74f, 3.76f };
            float[] amp = { 0.5f, 1f, 0.45f, 0.35f, 0.3f, 0.2f, 0.12f };
            float[] decay = { 0.12f, 0.22f, 0.3f, 0.4f, 0.5f, 0.7f, 1f };
            const float f0 = 128f;
            return Make("bell", 9f, t =>
            {
                float v = 0;
                for (int k = 0; k < ratio.Length; k++)
                    v += amp[k] * Mathf.Sin(2 * Mathf.PI * f0 * ratio[k] * t + Mathf.Sin(t * 4.1f * (k + 1)) * 0.2f) * Mathf.Exp(-t * decay[k]);
                float strike = Mathf.Exp(-t * 30f) * 0.6f * Mathf.Sin(2 * Mathf.PI * 380f * t);
                return (v * 0.28f + strike) * Mathf.Clamp01(t * 200f);
            });
        }

        /// <summary>風鈴。</summary>
        public static AudioClip Chime(float freq)
        {
            return Make("chime", 2.2f, t =>
            {
                float v = Mathf.Sin(2 * Mathf.PI * freq * t)
                          + 0.5f * Mathf.Sin(2 * Mathf.PI * freq * 2.76f * t) * Mathf.Exp(-t * 3f)
                          + 0.3f * Mathf.Sin(2 * Mathf.PI * freq * 5.4f * t) * Mathf.Exp(-t * 6f);
                return v * 0.25f * Mathf.Exp(-t * 1.8f) * Mathf.Clamp01(t * 800f);
            });
        }

        /// <summary>水の音（ループ）。bright が大きいほど高い音がまじる。</summary>
        public static AudioClip Water(float seconds, float bright)
        {
            var r = new Rng(12345);
            float lp = 0, lp2 = 0;
            return Make("water", seconds, t =>
            {
                lp += (r.Next() - lp) * bright;
                lp2 += (lp - lp2) * 0.3f;
                float swell = 0.7f + 0.3f * Mathf.Sin(t * 2 * Mathf.PI / seconds * 3f);
                float edge = Mathf.Min(1f, Mathf.Min(t, seconds - t) * 4f);
                return lp2 * swell * 0.9f * Mathf.Lerp(0.6f, 1f, edge);
            });
        }

        /// <summary>人のざわめき（ループ）。</summary>
        public static AudioClip Crowd(float seconds)
        {
            var r = new Rng(777);
            float bp = 0, bp2 = 0;
            return Make("crowd", seconds, t =>
            {
                bp += (r.Next() - bp) * 0.12f;
                bp2 += (bp - bp2) * 0.04f;
                float syll = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 3.1f + 1f));
                float edge = Mathf.Min(1f, Mathf.Min(t, seconds - t) * 4f);
                return (bp - bp2) * syll * 1.6f * Mathf.Lerp(0.7f, 1f, edge);
            });
        }

        /// <summary>遠くの祭囃子：太鼓と鉦と、五音の笛（くり返し）。</summary>
        public static AudioClip Festival()
        {
            float[] scale = { 587.3f, 659.3f, 784f, 880f, 987.8f, 1174.7f };
            int[] melody = { 2, 3, 4, 3, 2, 1, 2, -1, 3, 4, 5, 4, 3, 2, 3, -1, 2, 3, 4, 3, 2, 1, 0, -1, 1, 2, 3, 2, 1, 0, 1, -1 };
            const float beat = 0.5f;
            var r = new Rng(99);
            return Make("festival", melody.Length * beat, t =>
            {
                float v = 0;
                float bt = t % (beat * 2);
                v += Mathf.Sin(2 * Mathf.PI * (70f + 60f * Mathf.Exp(-bt * 20f)) * bt) * Mathf.Exp(-bt * 9f) * 0.9f;
                float bt2 = (t + beat * 0.5f) % (beat * 4);
                v += Mathf.Sin(2 * Mathf.PI * 90f * bt2) * Mathf.Exp(-bt2 * 14f) * 0.5f;
                float ct = t % (beat / 2);
                v += r.Next() * Mathf.Exp(-ct * 60f) * 0.12f;
                int k = Mathf.Min(melody.Length - 1, (int)(t / beat));
                int note = melody[k];
                if (note >= 0)
                {
                    float nt = t - k * beat;
                    float f = scale[note] * (1 + 0.006f * Mathf.Sin(2 * Mathf.PI * 5.5f * t));
                    v += (Mathf.Sin(2 * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * f * 2 * t)) * 0.28f
                         * Mathf.Clamp01(nt * 25f) * Mathf.Clamp01((beat - nt) * 12f);
                }
                return v * 0.5f;
            });
        }

        /// <summary>浮きが水に落ちる・沈む「ぽちゃん」。</summary>
        public static AudioClip Plop()
        {
            var r = new Rng(31);
            float lp = 0;
            return Make("plop", 0.5f, t =>
            {
                lp += (r.Next() - lp) * 0.25f;
                float bub = Mathf.Sin(2 * Mathf.PI * (380f + 900f * t) * t) * Mathf.Exp(-t * 18f);
                return (bub * 0.5f + lp * Mathf.Exp(-t * 14f) * 0.8f) * Mathf.Clamp01(t * 400f) * 0.7f;
            });
        }

        /// <summary>魚がはねる・池にかえす「ばしゃっ」。</summary>
        public static AudioClip Splash()
        {
            var r = new Rng(57);
            float lp = 0;
            return Make("splash", 0.9f, t =>
            {
                lp += (r.Next() - lp) * 0.45f;
                return lp * Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 200f) * (0.7f + 0.3f * Mathf.Sin(t * 70f));
            });
        }

        /// <summary>リールを巻く「カチッ」。</summary>
        public static AudioClip Tick()
        {
            var r = new Rng(3);
            return Make("tick", 0.03f, t => (r.Next() * 0.5f + Mathf.Sin(2 * Mathf.PI * 2600f * t)) * Mathf.Exp(-t * 180f) * 0.35f);
        }

        /// <summary>糸が切れる「プツン」。</summary>
        public static AudioClip Snap() =>
            Make("snap", 0.25f, t => Mathf.Sin(2 * Mathf.PI * (900f - 2400f * t) * t) * Mathf.Exp(-t * 22f) * 0.5f);

        /// <summary>鹿威しの「カコーン」（竹が石を打つ音）。</summary>
        public static AudioClip Kokon()
        {
            return Make("kokon", 1.6f, t =>
            {
                float knock = Mathf.Sin(2 * Mathf.PI * 820f * t) * Mathf.Exp(-t * 26f) * 0.6f;
                float hollow = (Mathf.Sin(2 * Mathf.PI * 410f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 1230f * t)) * Mathf.Exp(-t * 4.2f) * 0.35f;
                return (knock + hollow) * Mathf.Clamp01(t * 900f);
            });
        }

        /// <summary>釣れたときの、ちいさな3つの音。</summary>
        public static AudioClip Jingle(bool big)
        {
            float[] notes = big ? new[] { 784f, 988f, 1175f, 1568f } : new[] { 880f, 1175f, 1319f };
            const float step = 0.11f;
            return Make("jingle", notes.Length * step + 0.9f, t =>
            {
                float v = 0;
                for (int k = 0; k < notes.Length; k++)
                {
                    float nt = t - k * step;
                    if (nt < 0) continue;
                    v += (Mathf.Sin(2 * Mathf.PI * notes[k] * nt) + 0.3f * Mathf.Sin(2 * Mathf.PI * notes[k] * 3 * nt) * Mathf.Exp(-nt * 8)) * Mathf.Exp(-nt * 4.5f);
                }
                return v * 0.16f * Mathf.Clamp01(t * 600f);
            });
        }

        /// <summary>花火（ドーン、パチパチ）。</summary>
        public static AudioClip Boom()
        {
            var r = new Rng(4242);
            float lp = 0;
            return Make("boom", 2.5f, t =>
            {
                lp += (r.Next() - lp) * 0.05f;
                float thump = Mathf.Sin(2 * Mathf.PI * (45f + 40f * Mathf.Exp(-t * 8f)) * t) * Mathf.Exp(-t * 3f);
                float crackle = r.Next() * Mathf.Exp(-t * 2f) * (Mathf.Sin(t * 90f) > 0.6f ? 0.25f : 0.02f) * Mathf.Clamp01(t * 3f - 0.5f);
                return (thump * 0.8f + lp * 2.5f * Mathf.Exp(-t * 1.5f) + crackle) * 0.7f;
            });
        }
    }
}
