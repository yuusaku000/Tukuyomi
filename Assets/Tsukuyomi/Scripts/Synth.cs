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

        /// <summary>取ってきた録音（ステレオでも）を、つなぎ目なしでくり返せる1チャンネルの音にする。</summary>
        public static AudioClip Seamless(AudioClip src, string name)
        {
            if (src == null) return null;
            try
            {
                if (src.loadState != AudioDataLoadState.Loaded) src.LoadAudioData();
                int ch = src.channels, n = src.samples;
                var raw = new float[n * ch];
                if (!src.GetData(raw, 0)) return src;
                var mono = new float[n];
                for (int i = 0; i < n; i++) { float s = 0; for (int c = 0; c < ch; c++) s += raw[i * ch + c]; mono[i] = s / ch; }
                int nf = Mathf.Min(n / 4, src.frequency); // 1秒ぶん重ねる
                int len = n - nf;
                var data = new float[len];
                System.Array.Copy(mono, data, len);
                for (int i = 0; i < nf; i++)
                {
                    float k = i / (float)nf;
                    data[i] = mono[i] * Mathf.Sqrt(k) + mono[len + i] * Mathf.Sqrt(1 - k);
                }
                var clip = AudioClip.Create(name, len, 1, src.frequency, false);
                clip.SetData(data, 0);
                return clip;
            }
            catch { return src; }
        }

        /// <summary>
        /// くり返し用の音：少し長めに作って、終わりの部分を始まりに重ねる（つなぎ目で「プツッ」と鳴らないように）。
        /// </summary>
        static AudioClip MakeLoop(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate), nf = Mathf.CeilToInt(Mathf.Min(1.5f, seconds * 0.25f) * Rate);
            var raw = new float[n + nf];
            for (int i = 0; i < raw.Length; i++) raw[i] = f(i / (float)Rate);
            var data = new float[n];
            System.Array.Copy(raw, data, n);
            for (int i = 0; i < nf; i++)
            {
                float k = i / (float)nf;
                data[i] = raw[i] * Mathf.Sqrt(k) + raw[n + i] * Mathf.Sqrt(1 - k);
            }
            // 大きすぎる所をおさえる（音割れの雑音を防ぐ）
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.9f) for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;
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
            return MakeLoop("water", seconds, t =>
            {
                lp += (r.Next() - lp) * bright;
                lp2 += (lp - lp2) * 0.25f;
                float swell = 0.7f + 0.3f * Mathf.Sin(t * 2 * Mathf.PI / seconds * 3f);
                return lp2 * swell * 0.9f;
            });
        }

        /// <summary>人のざわめき（ループ）。</summary>
        public static AudioClip Crowd(float seconds)
        {
            var r = new Rng(777);
            float bp = 0, bp2 = 0;
            float bp3 = 0;
            return MakeLoop("crowd", seconds, t =>
            {
                bp += (r.Next() - bp) * 0.08f;
                bp2 += (bp - bp2) * 0.04f;
                bp3 += ((bp - bp2) - bp3) * 0.35f; // 高い「サー」という音をけずる
                float syll = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 3.1f + 1f));
                return bp3 * syll * 1.8f;
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
                // 笛（すんだ高い音）は「ぴぇー・きゅー」と生き物の声のように聞こえたので鳴らさない。太鼓と鉦だけ
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

        /// <summary>ボタンの「ポン」。</summary>
        public static AudioClip Click() => Make("click", 0.08f, t => Mathf.Sin(2 * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 60f) * 0.35f);

        /// <summary>買ったときの「チリン」。</summary>
        public static AudioClip Coin() => Make("coin", 0.6f, t =>
            (Mathf.Sin(2 * Mathf.PI * 1976f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 2637f * Mathf.Max(0, t - 0.07f)) * (t > 0.07f ? 1 : 0)) * Mathf.Exp(-t * 7f) * 0.22f);

        /// <summary>石畳の足音（やわらかい「コッ」）。</summary>
        public static AudioClip Step(int seed)
        {
            var r = new Rng((uint)(91 + seed));
            float lp = 0;
            return Make("step", 0.09f, t =>
            {
                lp += (r.Next() - lp) * 0.35f;
                return (lp * 0.8f + Mathf.Sin(2 * Mathf.PI * (180f + seed * 25f) * t) * 0.4f) * Mathf.Exp(-t * 55f) * 0.5f;
            });
        }

        /// <summary>遊覧船の出航の合図（やわらかい鐘）。</summary>
        public static AudioClip Gong() => Make("gong", 3f, t =>
            (Mathf.Sin(2 * Mathf.PI * 330f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 495f * t) * Mathf.Exp(-t * 1.5f) + 0.3f * Mathf.Sin(2 * Mathf.PI * 660f * t)) * Mathf.Exp(-t * 1.4f) * 0.3f * Mathf.Clamp01(t * 300f));

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
    
        // ───────── KASSEN（格闘）の音 ─────────

        /// <summary>打撃が当たった音。heavy で低く重く。</summary>
        public static AudioClip Hit(bool heavy)
        {
            var r = new Rng(heavy ? 777u : 555u);
            float lp = 0;
            return Make(heavy ? "hit_heavy" : "hit_light", heavy ? 0.35f : 0.18f, t =>
            {
                lp += (r.Next() - lp) * (heavy ? 0.25f : 0.5f);
                float body = Mathf.Sin(2 * Mathf.PI * (heavy ? 70f : 140f) * t * (1 + Mathf.Exp(-t * 30f))) * Mathf.Exp(-t * (heavy ? 14f : 28f));
                return (lp * Mathf.Exp(-t * (heavy ? 22f : 40f)) * 1.4f + body * 0.9f) * 0.75f;
            });
        }

        /// <summary>ガードした音（かたい金属っぽい音）。just で高く澄んだ音（刹那ガード）。</summary>
        public static AudioClip Guard(bool just) => Make(just ? "guard_just" : "guard", just ? 0.9f : 0.2f, t =>
            just ? (Mathf.Sin(2 * Mathf.PI * 1760f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 2637f * t) * 0.3f) * Mathf.Exp(-t * 4f) * 0.6f
                 : (Mathf.Sin(2 * Mathf.PI * 620f * t) + Mathf.Sin(2 * Mathf.PI * 931f * t) * 0.6f) * Mathf.Exp(-t * 30f) * 0.45f);

        /// <summary>空を切る音。</summary>
        public static AudioClip Whoosh()
        {
            var r = new Rng(31337u);
            float lp = 0, k = 0;
            return Make("whoosh", 0.22f, t =>
            {
                k = Mathf.Lerp(0.05f, 0.4f, t / 0.22f);
                lp += (r.Next() - lp) * k;
                return lp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.22f)) * 0.5f;
            });
        }
    }
}
