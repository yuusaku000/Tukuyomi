using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>ライブの曲の1区切り（イントロ・Aメロ・サビ…）。</summary>
    public sealed class LiveSec
    {
        public string name;        // intro / verse / pre / chorus / inter / bridge / outro
        public string[] mel;       // 1小節に8分音符8つ（"F#5" 音・"-" のばす・"." 休み）
        public string[] chords;    // 1小節に1つ（"F#m"・"G/B"・"Asus4"…）
        public string[] lyrics;    // 字幕（lyrBars 小節ごとに1行）
        public int lyrBars = 4;
        public int energy;         // 0 しずか 〜 3 いちばん盛り上がる
        public int inst;           // メロディの楽器：0 リード・1 ベル（オルゴール）・2 なし
        public int shift;          // 転調（半音）
        public bool last;          // 最後のサビ
        // 組み立てたあとに入る
        public int bar0;
        public float t0, t1;
        public int Bars => chords.Length;
    }

    /// <summary>ライブの曲。音はその場で合成する（歌声はなく、メロディは楽器。歌詞は字幕）。</summary>
    public sealed class LiveSong
    {
        public string title, en;
        public float bpm;
        public int style;           // 0 明るいポップ・1 バラード・2 EDM・3 ロック
        public string mc;           // 曲の前のヤチヨのひとこと
        public bool Hype => style != 1;
        public float reverb = 0.2f;
        public readonly List<LiveSec> secs = new List<LiveSec>();
        public float lead = 0.6f;   // 曲が始まるまでの間（秒）
        public float length;

        public float BarLen => 240f / bpm;
        public float BeatLen => 60f / bpm;

        public void Layout()
        {
            float t = lead; int bar = 0;
            foreach (var s in secs) { s.bar0 = bar; s.t0 = t; t += s.Bars * BarLen; s.t1 = t; bar += s.Bars; }
            length = t + (style == 1 ? 5f : 3.5f); // 最後の響き
        }

        public LiveSec At(float t)
        {
            foreach (var s in secs) if (t < s.t1) return t >= s.t0 ? s : null;
            return null;
        }

        /// <summary>拍（0 からの通し番号。小数つき）。曲の前は負。</summary>
        public float Beat(float t) => (t - lead) / BeatLen;

        /// <summary>いまの字幕の行と、その行のどこまで進んだか（0〜1）。</summary>
        public string Lyric(float t, out float progress, out float alpha)
        {
            progress = 0; alpha = 0;
            var s = At(t);
            if (s == null || s.lyrics == null) return null;
            float lineLen = s.lyrBars * BarLen;
            int i = Mathf.FloorToInt((t - s.t0) / lineLen);
            if (i < 0 || i >= s.lyrics.Length) return null;
            float lt = t - s.t0 - i * lineLen;
            progress = Mathf.Clamp01(lt / (lineLen * 0.82f));
            alpha = Mathf.Clamp01(lt / 0.25f) * Mathf.Clamp01((lineLen - lt) / 0.35f);
            return s.lyrics[i];
        }
    }

    /// <summary>
    /// ヤチヨのライブの2曲（オリジナル）と、その音の合成。
    ///   1曲目「月灯りパレード」：明るいポップ（136 BPM・ニ長調）
    ///   2曲目「八千夜のてがみ」：バラード（76 BPM・ハ長調 → 最後のサビでニ長調へ）
    /// 合成は重いので、別のスレッドで float の配列を作り、できたら AudioClip にする。
    /// </summary>
    public static class LiveMusic
    {
        public const int Rate = 32000;

        static LiveSec S(string name, int energy, int inst, string chords, string[] mel, string[] lyr = null, int lyrBars = 4, int shift = 0, bool last = false)
            => new LiveSec { name = name, energy = energy, inst = inst, chords = chords.Split(' '), mel = mel, lyrics = lyr, lyrBars = lyrBars, shift = shift, last = last };

        // ───────── 1曲目「月灯りパレード」 ─────────

        static readonly string[] P_Verse =
        {
            "F#5 - E5 - D5 - E5 F#5", "E5 - - - . . C#5 D5", "D5 - B4 - D5 - F#5 -", "E5 - - - - - . .",
            "F#5 - E5 - D5 - E5 F#5", "A5 - - - . . F#5 E5", "D5 - E5 - F#5 - E5 D5", "E5 - - - - - . .",
            "F#5 - E5 - D5 - E5 F#5", "E5 - - - . . C#5 D5", "D5 - B4 - D5 - F#5 -", "G5 - F#5 - E5 - - -",
            "E5 - F#5 - G5 - A5 -", "A5 - - - F#5 - E5 -", "D5 - E5 - F#5 - G5 -", "A5 - - - - - . .",
        };
        const string C_Verse = "D A Bm G D A Bm G D A Bm G Em F#m G A";

        static readonly string[] P_Pre =
        {
            "B4 - D5 - G5 - F#5 -", "E5 - - - C#5 - E5 -", "A5 - - - F#5 - E5 F#5", "D5 - - - - - . .",
            "G5 - F#5 - E5 - G5 -", "A5 - - - C#6 - A5 -", "B5 - A5 - G5 - F#5 -", "E5 - - - . . A5 B5",
        };
        const string C_Pre = "G A F#m Bm Em F#m G A";

        static readonly string[] P_Chorus =
        {
            "D6 - - - B5 - A5 -", "A5 - B5 - C#6 - D6 -", "C#6 - - - A5 - F#5 -", "F#5 - A5 - B5 - - -",
            "D6 - - - B5 - A5 -", "A5 - B5 - A5 - G5 -", "F#5 - - - - - E5 F#5", "D5 - - - - - . .",
            "D6 - - - B5 - A5 -", "A5 - B5 - C#6 - D6 -", "E6 - - - C#6 - A5 -", "B5 - - - A5 - F#5 -",
            "G5 - - - A5 - B5 -", "A5 - - - E5 - G5 -", "F#5 - - - E5 - D5 -", "D5 - - - - - . .",
        };
        const string C_Chorus = "G A F#m Bm G A D D G A F#m Bm Em A D D";

        static readonly string[] P_Intro =
        {
            "D6 - - - B5 - A5 -", "A5 - B5 - C#6 - D6 -", "C#6 - - - A5 - F#5 -", "F#5 - A5 - B5 - - -",
            "D6 - - - B5 - A5 -", "A5 - B5 - A5 - G5 -", "F#5 - - - - - E5 F#5", "D5 - - - - - . .",
        };
        static readonly string[] P_Inter =
        {
            "D6 C#6 B5 A5 B5 A5 F#5 A5", "B5 - A5 - F#5 - E5 -", "F#5 A5 C#6 A5 F#5 A5 C#6 E6", "D6 - - - - - . .",
        };
        static readonly string[] P_Bridge =
        {
            "F#5 - - - D5 - - -", "B4 - D5 - G5 - - -", "F#5 - - - A5 - - -", "E5 - - - - - . .",
            "F#5 - - - D5 - F#5 -", "B5 - - - G5 - B5 -", "A5 - - - G5 - F#5 -", "E5 - - - A5 - C#6 -",
        };
        static readonly string[] P_Outro =
        {
            "D6 - - - B5 - A5 -", "A5 - B5 - C#6 - D6 -", "D6 - - - - - - -", "- - - - - - . .",
            "D6 - - - B5 - A5 -", "A5 - B5 - A5 - G5 -", "F#5 - - - E5 - - -", "D5 - - - - - - -",
        };

        static readonly string[] L1_Verse1 =
        {
            "改札みたいな 鳥居をくぐって",
            "きょうのわたしに ログインしよう",
            "つまさきで鳴らす 石だたみのリズム",
            "ひとりじゃないって 月が言う",
        };
        static readonly string[] L1_Pre1 = { "ねえ 聞こえる？ 胸の奥のドラム", "数えて 3、2、1 ──" };
        static readonly string[] L1_Chorus =
        {
            "月灯りパレード 手をのばして",
            "光の色で きみを見つけるよ",
            "八千の夜を こえて届け",
            "いまここで 鳴らそう ツクヨミ！",
        };
        static readonly string[] L1_Verse2 =
        {
            "宿題のページ 半分とじて",
            "スマコンごしに 会いに来たんだね",
            "屋台の湯気も 釣り堀の波も",
            "ぜんぶきょうの ステージになる",
        };
        static readonly string[] L1_Pre2 = { "ねえ 見えてる？ ゆれるペンライトの海", "声にして 3、2、1 ──" };
        static readonly string[] L1_Bridge = { "もしも明日 画面がくもっても", "この光は きっと消えないから" };
        static readonly string[] L1_Last =
        {
            "月灯りパレード 手をのばして",
            "光の色で きみを見つけるよ",
            "八千の夜を こえて届け",
            "いつまでも 鳴らそう ツクヨミ！",
        };

        public static LiveSong Parade()
        {
            var s = new LiveSong { title = "月灯りパレード", en = "TSUKIAKARI PARADE", bpm = 136, style = 0, reverb = 0.16f, mc = "みんなで光の行進、いくよ〜！" };
            s.secs.Add(S("intro", 2, 1, "G A F#m Bm G A D D", P_Intro));
            s.secs.Add(S("verse", 1, 0, C_Verse, P_Verse, L1_Verse1));
            s.secs.Add(S("pre", 2, 0, C_Pre, P_Pre, L1_Pre1));
            s.secs.Add(S("chorus", 3, 0, C_Chorus, P_Chorus, L1_Chorus));
            s.secs.Add(S("inter", 2, 0, "G A F#m Bm", P_Inter));
            s.secs.Add(S("verse", 1, 0, C_Verse, P_Verse, L1_Verse2));
            s.secs.Add(S("pre", 2, 0, C_Pre, P_Pre, L1_Pre2));
            s.secs.Add(S("chorus", 3, 0, C_Chorus, P_Chorus, L1_Chorus));
            s.secs.Add(S("bridge", 1, 0, "Bm G D A Bm G Em A", P_Bridge, L1_Bridge));
            s.secs.Add(S("chorus", 3, 0, C_Chorus, P_Chorus, L1_Last, last: true));
            s.secs.Add(S("outro", 2, 1, "G A D D G A D D", P_Outro));
            s.Layout();
            return s;
        }

        // ───────── 2曲目「八千夜のてがみ」 ─────────

        static readonly string[] B_A =
        {
            "G4 - C5 - D5 - E5 -", "D5 - - - - - . .", "C5 - D5 - E5 - G5 -", "G5 - - - E5 - . .",
            "F5 - - - E5 - D5 -", "E5 - - - C5 - . .", "D5 - E5 - F5 - E5 -", "D5 - - - - - . .",
        };
        const string BC_A = "C G/B Am Em/G F C/E Dm G";
        static readonly string[] B_B =
        {
            "A4 - C5 - F5 - - -", "B4 - D5 - G5 - - -", "G5 - F5 - E5 - D5 -", "E5 - - - C5 - . .",
            "D5 - E5 - F5 - A5 -", "G5 - - - E5 - G5 -", "A5 - - - G5 - F5 -", "G5 - - - - - . .",
        };
        const string BC_B = "F G Em Am Dm Em F Gsus4";
        static readonly string[] B_K =
        {
            "C6 - - - A5 - G5 -", "G5 - A5 - B5 - D6 -", "C6 - - - B5 - G5 -", "A5 - - - - - E5 G5",
            "A5 - - - G5 - F5 -", "E5 - F5 - G5 - D6 -", "C6 - - - - - - -", "- - - - . . . .",
        };
        const string BC_K = "F G Em Am F G C C";
        static readonly string[] B_Intro = { "E6 - D6 - C6 - G5 -", "D6 - - - - - . .", "C6 - B5 - A5 - E5 -", "G5 - - - - - . ." };
        static readonly string[] B_Inter = { "A5 C6 F6 C6 A5 C6 F6 C6", "B5 D6 G6 D6 B5 D6 G6 D6" };
        static readonly string[] B_Bridge = { "E5 - - - A5 - - -", "C6 - - - A5 - F5 -", "G5 - - - D6 - B5 -", "D6 - - - - - . ." };
        static readonly string[] B_Outro = { "C6 - - - A5 - G5 -", "G5 - A5 - B5 - D6 -", "C6 - - - - - - -", "- - - - - - . ." };

        static readonly string[] L2_A1 =
        {
            "ながい ながい 夜のむこうで",
            "だれかを ずっと 待っていた",
            "月の舟に 手紙をのせて",
            "とどくかな って 笑ってた",
        };
        static readonly string[] L2_B1 =
        {
            "数えきれない 星の数だけ",
            "さよならも ありがとうも あった",
            "それでも ほら きょうの空は",
            "こんなに 明るいよ",
        };
        static readonly string[] L2_K =
        {
            "八千夜の てがみを ひらいて",
            "いちばん 最初の ことばは",
            "「また会えたね」って それだけで",
            "ぜんぶ むくわれる 気がした",
        };
        static readonly string[] L2_A2 =
        {
            "きみが 勉強に つかれた夜も",
            "眠れずに 見上げた 朝も",
            "この島の あかりは ずっと",
            "きみの 帰り道を 照らす",
        };
        static readonly string[] L2_B2 =
        {
            "遠くても 画面ごしでも",
            "となりに いるって 信じてる",
            "だから ねえ もう一度だけ",
            "その手を ふってみて",
        };
        static readonly string[] L2_Bridge = { "時がわたしを わすれても", "わたしは きみを わすれない" };
        static readonly string[] L2_Last =
        {
            "八千夜の てがみの さいごに",
            "書きたい ことばは ひとつだけ",
            "「また会おうね」って 月の下で",
            "なんどでも ここで 待ってる",
        };

        public static LiveSong Letter()
        {
            var s = new LiveSong { title = "八千夜のてがみ", en = "A LETTER OF 8000 NIGHTS", bpm = 76, style = 1, reverb = 0.3f, mc = "ゆっくり聞いてね。ずっと前から、みんなに聞いてほしかった歌です" };
            s.secs.Add(S("intro", 0, 1, "C G/B Am G", B_Intro));
            s.secs.Add(S("verse", 1, 0, BC_A, B_A, L2_A1, 2));
            s.secs.Add(S("pre", 1, 0, BC_B, B_B, L2_B1, 2));
            s.secs.Add(S("chorus", 2, 0, BC_K, B_K, L2_K, 2));
            s.secs.Add(S("inter", 1, 1, "F G", B_Inter));
            s.secs.Add(S("verse", 1, 0, BC_A, B_A, L2_A2, 2));
            s.secs.Add(S("pre", 2, 0, BC_B, B_B, L2_B2, 2));
            s.secs.Add(S("chorus", 3, 0, BC_K, B_K, L2_K, 2));
            s.secs.Add(S("bridge", 1, 0, "Am F G G", B_Bridge, L2_Bridge, 2));
            s.secs.Add(S("chorus", 3, 0, BC_K, B_K, L2_Last, 2, shift: 2, last: true));
            s.secs.Add(S("outro", 1, 1, "F G C C", B_Outro, shift: 2));
            s.Layout();
            return s;
        }


        // ───────── 3曲目「ヤオヨロ☆ハイパージャンプ」（ロック・172 BPM・イ長調） ─────────

        static readonly string[] J_Intro =
        {
            "A5 - E5 - A5 B5 C#6 -", "B5 - G#5 - E5 - - -", "F#5 - C#6 - B5 A5 B5 -", "A5 - F#5 - D5 - - -",
            "A5 - E5 - A5 B5 C#6 -", "B5 - G#5 - E5 - E6 -", "D6 - C#6 - B5 - A5 -", "B5 - - - . . . .",
        };
        static readonly string[] J_Verse =
        {
            "C#5 C#5 C#5 - B4 - A4 -", "B4 - B4 C#5 E5 - - -", "F#5 F#5 E5 - C#5 - A4 -", "F#4 - A4 - B4 - - -",
            "C#5 C#5 C#5 - B4 - A4 -", "B4 - B4 C#5 E5 - F#5 -", "F#5 - E5 - D5 - C#5 -", "B4 - - - . . . .",
        };
        static readonly string[] J_Pre =
        {
            "D5 - D5 - E5 - F#5 -", "E5 - - - B4 - - -", "C#5 - E5 - G#5 - A5 -", "A5 - - - F#5 - - -",
            "D5 - D5 - E5 - F#5 -", "G#5 - - - E5 - - -", "F#5 - F#5 - E5 - D5 -", "E5 - - - - - . .",
        };
        static readonly string[] J_Chorus =
        {
            "A5 - - - F#5 - A5 -", "B5 - - - G#5 - E5 -", "E5 - C#5 - E5 - A5 -", "A5 - - - - - . .",
            "A5 - - - F#5 - A5 -", "B5 - - - C#6 - B5 -", "A5 - - - E5 - C#5 -", "E5 - - - - - . .",
            "A5 - - - F#5 - A5 -", "B5 - - - G#5 - E5 -", "C#6 - B5 - A5 - C#6 -", "C#6 - - - B5 - A5 -",
            "F#5 - - - A5 - B5 -", "B5 - - - G#5 - E5 -", "A5 - - - - - - -", "- - - - . . . .",
        };
        static readonly string[] J_Inter = { "A5 E5 A5 B5 C#6 B5 A5 E5", "B5 G#5 E5 G#5 B5 - - -", "F#5 A5 D6 C#6 B5 A5 F#5 -", "E5 G#5 B5 E6 - - . ." };
        static readonly string[] J_Bridge =
        {
            "C#5 - - - E5 - - -", "F#5 - - - A5 - - -", "E5 - - - C#5 - - -", "B4 - - - - - . .",
            "C#5 - - - E5 - F#5 -", "A5 - - - F#5 - A5 -", "B5 - - - D6 - B5 -", "E6 - - - - - - -",
        };
        static readonly string[] J_Outro = { "A5 - - - F#5 - A5 -", "B5 - - - G#5 - E5 -", "A5 - - - - - - -", "A5 - - - - - . ." };
        const string JC_V = "A E F#m D A E D E", JC_P = "D E C#m F#m D E Bm E", JC_C = "D E C#m F#m D E A A D E C#m F#m Bm E A A";
        static readonly string[] L3_V1 = { "ノートを閉じたら 合図はひとつ", "スマコン起動 ヤオヨロ！" };
        static readonly string[] L3_P1 = { "重力なんて 置いてきちゃった", "さあ ふみきって ──" };
        static readonly string[] L3_C = { "ヤオヨロ☆ハイパージャンプ！ 空まで届け", "八百万の 光が呼んでる", "ヤオヨロ☆ハイパージャンプ！ 月を越えて", "いまだけは だれより 高く！" };
        static readonly string[] L3_V2 = { "千本鳥居を 駆けあがったら", "屋根の上まで 一直線" };
        static readonly string[] L3_P2 = { "心のスイッチ ぜんぶオンにして", "せーので とびこめ ──" };
        static readonly string[] L3_B = { "落ちるのが こわくなったら", "わたしの手を つかんでいて" };
        static readonly string[] L3_Last = { "ヤオヨロ☆ハイパージャンプ！ 空まで届け", "八百万の 光が呼んでる", "ヤオヨロ☆ハイパージャンプ！ 月を越えて", "どこまでも いっしょに 高く！" };

        public static LiveSong Jump()
        {
            var s = new LiveSong { title = "ヤオヨロ☆ハイパージャンプ", en = "YAOYORO HYPER JUMP", bpm = 172, style = 3, reverb = 0.12f, mc = "重力なんて、ここにはないよ！ いっしょにとぼう！" };
            s.secs.Add(S("intro", 2, 0, "A E F#m D A E D E", J_Intro));
            s.secs.Add(S("verse", 1, 0, JC_V, J_Verse, L3_V1));
            s.secs.Add(S("pre", 2, 0, JC_P, J_Pre, L3_P1));
            s.secs.Add(S("chorus", 3, 0, JC_C, J_Chorus, L3_C));
            s.secs.Add(S("inter", 2, 0, "A E D E", J_Inter));
            s.secs.Add(S("verse", 1, 0, JC_V, J_Verse, L3_V2));
            s.secs.Add(S("pre", 2, 0, JC_P, J_Pre, L3_P2));
            s.secs.Add(S("chorus", 3, 0, JC_C, J_Chorus, L3_C));
            s.secs.Add(S("bridge", 1, 0, "F#m D A E F#m D Bm E", J_Bridge, L3_B));
            s.secs.Add(S("chorus", 3, 0, JC_C, J_Chorus, L3_Last, last: true));
            s.secs.Add(S("outro", 2, 0, "D E A A", J_Outro));
            s.Layout();
            return s;
        }

        // ───────── 4曲目「ネオン金魚の夜」（EDM・128 BPM・イ短調） ─────────

        static readonly string[] K_Hook = { "A5 C6 E6 C6 A5 C6 E6 G6", "F5 A5 C6 A5 F5 A5 C6 E6", "E5 G5 C6 G5 E5 G5 C6 E6", "D5 G5 B5 G5 D5 G5 B5 D6" };
        static readonly string[] K_Verse =
        {
            "A4 - C5 - E5 - D5 -", "C5 - - - A4 - - -", "G4 - C5 - E5 - G5 -", "G5 - - - E5 - D5 -",
            "A4 - C5 - E5 - D5 -", "C5 - - - A4 - C5 -", "E5 - - - D5 - C5 -", "B4 - - - . . . .",
        };
        static readonly string[] K_Pre =
        {
            "A5 - - - G5 - F5 -", "G5 - - - D5 - - -", "E5 - G5 - B5 - A5 -", "A5 - - - - - . .",
            "A5 - - - G5 - F5 -", "G5 - - - B5 - D6 -", "B5 - - - G#5 - E5 -", "G#5 - - - B5 - - -",
        };
        static readonly string[] K_Chorus =
        {
            "C6 - - - A5 - C6 -", "D6 - - - B5 - G5 -", "B5 - - - G5 - E5 -", "A5 - - - - - E5 G5",
            "C6 - - - A5 - C6 -", "D6 - - - E6 - D6 -", "C6 - B5 - A5 - - -", "A5 - - - - - . .",
        };
        static readonly string[] K_Drop =
        {
            "A5 A5 . A5 C6 . A5 G5", "F5 F5 . F5 A5 . C6 A5", "G5 G5 . G5 C6 . E6 C6", "B5 . G5 . D6 . B5 .",
            "A5 A5 . A5 C6 . E6 D6", "C6 C6 . A5 F5 . A5 C6", "E6 . D6 . C6 . G5 .", "B5 - - - D6 - - -",
        };
        static readonly string[] K_Bridge =
        {
            "F5 - - - A5 - - -", "G5 - - - B5 - - -", "A5 - - - C6 - - -", "B5 - - - D6 - - -",
            "F5 - A5 - C6 - - -", "G5 - B5 - D6 - - -", "C6 - - - B5 - A5 -", "G#5 - - - B5 - E6 -",
        };
        static readonly string[] K_Outro = { "A5 C6 E6 C6 A5 C6 E6 G6", "F5 A5 C6 A5 F5 A5 C6 E6", "G5 B5 D6 B5 G5 B5 D6 -", "A5 - - - - - - -" };
        const string KC_8 = "Am F C G Am F C G", KC_P = "F G Em Am F G Esus4 E", KC_C = "F G Em Am F G Am Am";
        static readonly string[] L4_V1 = { "ネオンの水面に 金魚がおよぐ", "だれも知らない 夜のすきま" };
        static readonly string[] L4_P1 = { "しっぽをゆらして ついておいで", "光る 迷路の むこうまで ──" };
        static readonly string[] L4_C = { "ネオン金魚の夜 ゆらゆら 踊ろう", "ぜんぶ 忘れて 光になれ" };
        static readonly string[] L4_V2 = { "提灯の海を すりぬけたら", "月が水槽に みえてくる" };
        static readonly string[] L4_P2 = { "ひれをひろげて 風をつかんで", "はじけろ 3、2、1 ──" };
        static readonly string[] L4_B = { "朝がきたら 消えてしまう魔法", "だから いまを 焼きつけて" };
        static readonly string[] L4_Last = { "ネオン金魚の夜 ゆらゆら 踊ろう", "この夜を ずっと おぼえていて" };

        static string[] Twice(string[] a) { var r = new string[a.Length * 2]; a.CopyTo(r, 0); a.CopyTo(r, a.Length); return r; }

        public static LiveSong Kingyo()
        {
            var s = new LiveSong { title = "ネオン金魚の夜", en = "NEON GOLDFISH NIGHT", bpm = 128, style = 2, reverb = 0.2f, mc = "ネオンの海へ、もぐっちゃお！" };
            s.secs.Add(S("intro", 2, 1, KC_8, Twice(K_Hook)));
            s.secs.Add(S("verse", 1, 0, KC_8, K_Verse, L4_V1));
            s.secs.Add(S("pre", 2, 0, KC_P, K_Pre, L4_P1));
            s.secs.Add(S("chorus", 3, 0, KC_C, K_Chorus, L4_C));
            s.secs.Add(S("drop", 3, 0, KC_8, K_Drop));
            s.secs.Add(S("verse", 1, 0, KC_8, K_Verse, L4_V2));
            s.secs.Add(S("pre", 2, 0, KC_P, K_Pre, L4_P2));
            s.secs.Add(S("chorus", 3, 0, KC_C, K_Chorus, L4_C));
            s.secs.Add(S("drop", 3, 0, KC_8, K_Drop));
            s.secs.Add(S("bridge", 1, 0, "Dm Em F G Dm Em F E", K_Bridge, L4_B));
            s.secs.Add(S("chorus", 3, 0, KC_C, K_Chorus, L4_Last, last: true));
            s.secs.Add(S("drop", 3, 0, KC_8, K_Drop, last: true));
            s.secs.Add(S("outro", 2, 1, "Am F G Am", K_Outro));
            s.Layout();
            return s;
        }

        // ───────── 5曲目「千本鳥居ハイウェイ」（速いEDM・160 BPM・ホ短調） ─────────

        static readonly string[] H_Intro =
        {
            "E5 G5 B5 E6 D6 B5 G5 B5", "C6 - B5 - G5 - E5 -", "D5 F#5 A5 D6 C6 A5 F#5 A5", "B5 - - - F#5 - - -",
            "E5 G5 B5 E6 D6 B5 G5 B5", "C6 - B5 - G5 - E5 -", "F#5 - G5 - A5 - D6 -", "C6 - - - B5 - . .",
        };
        static readonly string[] H_Verse =
        {
            "B4 - B4 - E5 - B4 -", "C5 - E5 - G5 - - -", "F#5 - E5 - D5 - E5 -", "F#5 - - - D5 - - -",
            "B4 - B4 - E5 - B4 -", "C5 - E5 - G5 - A5 -", "A5 - G5 - F#5 - D5 -", "E5 - - - . . . .",
        };
        static readonly string[] H_Pre =
        {
            "E5 - G5 - - - E5 -", "F#5 - A5 - - - F#5 -", "D5 - F#5 - B5 - A5 -", "G5 - - - - - . .",
            "E5 - G5 - - - C6 -", "A5 - F#5 - - - A5 -", "B5 - - - D#5 - F#5 -", "B5 - - - - - . .",
        };
        static readonly string[] H_Chorus =
        {
            "E6 - - - D6 - C6 -", "D6 - - - A5 - F#5 -", "F#5 - - - G5 - A5 -", "B5 - - - - - G5 B5",
            "E6 - - - D6 - C6 -", "D6 - - - E6 - F#6 -", "G6 - - - F#6 - D6 -", "D6 - - - - - . .",
            "E6 - - - D6 - C6 -", "D6 - - - A5 - F#5 -", "F#5 - - - G5 - A5 -", "B5 - - - E6 - G6 -",
            "E6 - - - D6 - C6 -", "B5 - - - D#6 - F#6 -", "E6 - - - - - - -", "- - - - . . . .",
        };
        static readonly string[] H_Drop =
        {
            "E6 E6 B5 E6 . E6 D6 B5", "C6 C6 G5 C6 . C6 B5 G5", "D6 D6 A5 D6 . D6 F#6 D6", "B5 . F#5 . B5 . D#6 .",
            "E6 E6 B5 E6 . E6 G6 E6", "C6 C6 G5 C6 . E6 C6 G5", "D6 . A5 . F#6 . D6 .", "D#6 - - - F#6 - - -",
        };
        static readonly string[] H_Bridge =
        {
            "C5 - - - E5 - - -", "B4 - - - E5 - - -", "E5 - - - G5 - - -", "F#5 - - - A5 - - -",
            "C5 - - - E5 - A5 -", "B5 - - - G5 - E5 -", "G5 - - - E5 - G5 -", "F#5 - - - D#5 - B4 -",
        };
        static readonly string[] H_Outro = { "E6 - - - D6 - C6 -", "D6 - - - F#6 - - -", "E6 - - - - - - -", "E6 - - - - - . ." };
        const string HC_V = "Em C D Bm Em C D D", HC_P = "C D Bm Em C D B B", HC_C = "C D Bm Em C D G G C D Bm Em Am B Em Em";
        static readonly string[] L5_V1 = { "赤いゲートが 光のレーン", "アクセル踏んで 千本先へ" };
        static readonly string[] L5_P1 = { "ふりかえらない ふりむかない", "くぐりぬけて ──" };
        static readonly string[] L5_C = { "千本鳥居ハイウェイ 夜をつらぬけ", "ネオンの風が 背中を押すよ", "千本鳥居ハイウェイ 願いを積んで", "終点は きみと 見上げる月！" };
        static readonly string[] L5_V2 = { "ひとつくぐれば 昨日が消えて", "ふたつくぐれば 明日が見える" };
        static readonly string[] L5_P2 = { "エンジン音は 胸のビート", "つかまってて ──" };
        static readonly string[] L5_B = { "迷ったら 赤い光を", "数えながら 帰っておいで" };
        static readonly string[] L5_Last = { "千本鳥居ハイウェイ 夜をつらぬけ", "ネオンの風が 背中を押すよ", "千本鳥居ハイウェイ 願いを積んで", "終点で きみを 待ってる月！" };

        public static LiveSong Highway()
        {
            var s = new LiveSong { title = "千本鳥居ハイウェイ", en = "SENBON TORII HIGHWAY", bpm = 160, style = 2, reverb = 0.15f, mc = "シートベルトしめてね！ 全速力でいくよ！" };
            s.secs.Add(S("intro", 2, 1, "Em C D Bm Em C D D", H_Intro));
            s.secs.Add(S("verse", 1, 0, HC_V, H_Verse, L5_V1));
            s.secs.Add(S("pre", 2, 0, HC_P, H_Pre, L5_P1));
            s.secs.Add(S("chorus", 3, 0, HC_C, H_Chorus, L5_C));
            s.secs.Add(S("inter", 2, 0, "Em C D B", new[] { H_Drop[0], H_Drop[1], H_Drop[2], H_Drop[3] }));
            s.secs.Add(S("verse", 1, 0, HC_V, H_Verse, L5_V2));
            s.secs.Add(S("pre", 2, 0, HC_P, H_Pre, L5_P2));
            s.secs.Add(S("chorus", 3, 0, HC_C, H_Chorus, L5_C));
            s.secs.Add(S("bridge", 1, 0, "Am Em C D Am Em C B", H_Bridge, L5_B));
            s.secs.Add(S("chorus", 3, 0, HC_C, H_Chorus, L5_Last, last: true));
            s.secs.Add(S("drop", 3, 0, "Em C D Bm Em C D B", H_Drop, last: true));
            s.secs.Add(S("outro", 2, 0, "C D Em Em", H_Outro));
            s.Layout();
            return s;
        }

        /// <summary>ライブで歌う曲ぜんぶ（曲順は LiveShow がランダムに決める）。</summary>
        public static List<LiveSong> All() => new List<LiveSong> { Parade(), Letter(), Jump(), Kingyo(), Highway() };

        // ───────── 音の名前 ─────────

        static int Pc(char c) => c switch { 'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11, _ => 0 };

        /// <summary>"F#5" → MIDI の番号。</summary>
        public static int Midi(string s)
        {
            int pc = Pc(s[0]), i = 1;
            if (i < s.Length && s[i] == '#') { pc++; i++; }
            else if (i < s.Length && s[i] == 'b') { pc--; i++; }
            int oct = int.Parse(s.Substring(i));
            return 12 * (oct + 1) + pc;
        }

        /// <summary>和音：根音（0〜11）・構成音（根音からの半音）・ベースの音（0〜11）。</summary>
        public static (int root, int[] tones, int bass) Chord(string s)
        {
            string main = s, bassName = null;
            int slash = s.IndexOf('/');
            if (slash > 0) { main = s.Substring(0, slash); bassName = s.Substring(slash + 1); }
            int root = Pc(main[0]), i = 1;
            if (i < main.Length && main[i] == '#') { root++; i++; }
            else if (i < main.Length && main[i] == 'b') { root--; i++; }
            string q = main.Substring(i);
            int[] tones = q switch
            {
                "m" => new[] { 0, 3, 7 },
                "7" => new[] { 0, 4, 7, 10 },
                "m7" => new[] { 0, 3, 7, 10 },
                "maj7" => new[] { 0, 4, 7, 11 },
                "sus4" => new[] { 0, 5, 7 },
                _ => new[] { 0, 4, 7 },
            };
            int bass = root;
            if (bassName != null)
            {
                bass = Pc(bassName[0]);
                if (bassName.Length > 1 && bassName[1] == '#') bass++;
                else if (bassName.Length > 1 && bassName[1] == 'b') bass--;
            }
            return ((root + 12) % 12, tones, (bass + 12) % 12);
        }

        static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        // ───────── 合成 ─────────

        sealed class Rng
        {
            uint _s;
            public Rng(uint seed) => _s = seed == 0 ? 1 : seed;
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f; }
        }

        static readonly float[] SinT = MakeSin();
        static float[] MakeSin() { var t = new float[4096]; for (int i = 0; i < 4096; i++) t[i] = (float)Math.Sin(i * Math.PI * 2 / 4096); return t; }
        static float Sin(double cycles) => SinT[(int)((cycles - Math.Floor(cycles)) * 4096) & 4095];

        static float Blep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1; }
            if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
            return 0;
        }

        static float Saw(ref double ph, double inc)
        {
            ph += inc; if (ph >= 1) ph -= 1;
            float t = (float)ph;
            return 2 * t - 1 - Blep(t, (float)inc);
        }

        static readonly float[] PianoAmp = { 0, 1f, 0.42f, 0.25f, 0.18f, 0.13f, 0.11f };

        enum Inst { Lead, Bell, Pad, Bass, Arp, Piano, Strings, Flute, BalladBass, Saw5, Guitar, Sub }
        static readonly float[] Det5 = { 1f, 1.0071f, 0.9929f, 1.0153f, 0.9847f };

        /// <summary>1つの音を buf に足す。</summary>
        static void Note(float[] buf, Inst inst, float t0, float dur, float midi, float vel, Rng rng)
        {
            float f = Hz(midi);
            double inc = f / Rate;
            float rel = inst switch { Inst.Pad => 0.5f, Inst.Strings => 0.9f, Inst.Bell => 1.6f, Inst.Piano => 1.4f, Inst.Flute => 0.25f, Inst.Lead => 0.14f, Inst.Arp => 0.05f, Inst.Saw5 => 0.12f, _ => 0.08f };
            var ph5 = new double[5] { 0.1, 0.37, 0.63, 0.81, 0.22 };
            int i0 = Mathf.Max(0, (int)(t0 * Rate));
            int n = (int)((dur + rel) * Rate);
            if (i0 + n > buf.Length) n = buf.Length - i0;
            double p1 = 0, p2 = 0.31, p3 = 0.67;
            float lp = 0, lp2 = 0;
            float detune = inst == Inst.Pad || inst == Inst.Strings ? 1.006f : 1.004f;
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)Rate;
                // 音量の形（立ち上がり・のばし・はなし）
                float env;
                switch (inst)
                {
                    case Inst.Bell: case Inst.Piano:
                        env = Mathf.Min(1, t * 400f) * Mathf.Exp(-t * (inst == Inst.Bell ? 2.4f : 1.6f));
                        if (t > dur) env *= Mathf.Exp(-(t - dur) * 5f);
                        break;
                    case Inst.Arp:
                        env = Mathf.Min(1, t * 600f) * Mathf.Exp(-t * 16f);
                        break;
                    case Inst.Bass: case Inst.BalladBass:
                        env = Mathf.Min(1, t * 300f) * (inst == Inst.Bass ? (0.65f + 0.35f * Mathf.Exp(-t * 8f)) : Mathf.Exp(-t * 0.5f));
                        if (t > dur) env *= Mathf.Max(0, 1 - (t - dur) / rel);
                        break;
                    default:
                        float att = inst switch { Inst.Pad => 0.25f, Inst.Strings => 0.55f, Inst.Flute => 0.07f, Inst.Saw5 => 0.006f, Inst.Guitar => 0.004f, _ => 0.012f };
                        env = Mathf.Min(1, t / att);
                        if (inst == Inst.Lead) env *= 0.78f + 0.22f * Mathf.Exp(-t * 7f);
                        if (t > dur) env *= Mathf.Max(0, 1 - (t - dur) / rel);
                        break;
                }
                if (env <= 0.00001f && t > dur) break;
                // ビブラート（のばしている音だけ）
                float vib = 1f;
                if (inst == Inst.Lead || inst == Inst.Flute || inst == Inst.Strings)
                {
                    float depth = inst == Inst.Strings ? 0.0025f : Mathf.Clamp01((t - 0.22f) * 2.5f) * (inst == Inst.Flute ? 0.006f : 0.0045f);
                    vib = 1f + depth * Sin(t * 5.4f);
                }
                double di = inc * vib;
                float v;
                switch (inst)
                {
                    case Inst.Lead:
                    {
                        float a = Saw(ref p1, di), b = Saw(ref p2, di * detune);
                        p3 += di * 0.5; if (p3 >= 1) p3 -= 1;
                        float sq = p3 < 0.5 ? 0.5f : -0.5f;
                        float raw = (a + b) * 0.5f + sq * 0.35f;
                        float cut = Mathf.Clamp(0.12f + 0.25f * Mathf.Exp(-t * 6f), 0.05f, 0.6f);
                        lp += (raw - lp) * cut; lp2 += (lp - lp2) * 0.5f;
                        v = lp2;
                        break;
                    }
                    case Inst.Bell:
                    {
                        p1 += di; p2 += di * 3.5;
                        float mod = Sin(p2) * 1.2f * Mathf.Exp(-t * 5f);
                        v = Sin(p1 + mod * 0.16) + 0.25f * Sin(p1 * 2) * Mathf.Exp(-t * 3f);
                        break;
                    }
                    case Inst.Pad: case Inst.Strings:
                    {
                        float a = Saw(ref p1, di), b = Saw(ref p2, di * detune), c = Saw(ref p3, di / detune);
                        float raw = (a + b + c) / 3f;
                        float cut = inst == Inst.Pad ? 0.06f : 0.09f;
                        lp += (raw - lp) * cut; lp2 += (lp - lp2) * cut;
                        v = lp2 * 1.6f;
                        break;
                    }
                    case Inst.Bass:
                    {
                        p1 += di; float s = Sin(p1);
                        float a = Saw(ref p2, di);
                        lp += (a - lp) * (0.05f + 0.15f * Mathf.Exp(-t * 12f));
                        v = s * 0.8f + lp * 0.6f;
                        break;
                    }
                    case Inst.BalladBass:
                    {
                        p1 += di; v = Sin(p1) + 0.15f * Sin(p1 * 2);
                        break;
                    }
                    case Inst.Arp:
                    {
                        p1 += di; if (p1 >= 1) p1 -= 1;
                        float sq = p1 < 0.5 ? 1f : -1f;
                        lp += (sq - lp) * 0.25f;
                        v = lp;
                        break;
                    }
                    case Inst.Piano:
                    {
                        p1 += di;
                        v = 0;
                        // 倍音ごとに消えるはやさを変える（高い倍音ほどはやく消える）
                        for (int h = 1; h <= 6; h++)
                            v += Sin(p1 * h * (1 + 0.0004 * h * h)) * Mathf.Exp(-t * (0.6f + h * 0.9f)) * PianoAmp[h];
                        v += rng.Next() * Mathf.Exp(-t * 90f) * 0.25f; // ハンマーの音
                        v *= 0.7f;
                        break;
                    }
                    case Inst.Flute:
                    {
                        p1 += di;
                        float breath = rng.Next();
                        lp += (breath - lp) * 0.2f;
                        v = Sin(p1) + 0.22f * Sin(p1 * 2) + 0.08f * Sin(p1 * 3) + lp * (0.25f * Mathf.Exp(-t * 6f) + 0.05f);
                        break;
                    }
                    case Inst.Saw5:
                    {
                        // スーパーソウ：少しずつずらした5本のノコギリ波
                        float sum = 0;
                        for (int q = 0; q < 5; q++) sum += Saw(ref ph5[q], di * Det5[q]);
                        lp += (sum * 0.3f - lp) * 0.4f;
                        v = lp;
                        break;
                    }
                    case Inst.Guitar:
                    {
                        // ひずんだギター：2本のノコギリ波を強くひずませる
                        float a = Saw(ref p1, di), b = Saw(ref p2, di * 1.003);
                        lp += ((a + b) * 0.5f - lp) * 0.3f;
                        v = (float)Math.Tanh(lp * 4.5f) * 0.55f;
                        break;
                    }
                    case Inst.Sub:
                    {
                        p1 += di; v = Sin(p1);
                        break;
                    }
                    default: v = 0; break;
                }
                buf[i0 + k] += v * env * vel;
            }
        }

        // 打楽器
        static void Kick(float[] buf, float t0, float vel)
        {
            int i0 = (int)(t0 * Rate), n = (int)(0.35f * Rate);
            double ph = 0;
            for (int k = 0; k < n && i0 + k < buf.Length; k++)
            {
                if (i0 + k < 0) continue;
                float t = k / (float)Rate;
                float f = 45f + 110f * Mathf.Exp(-t * 28f);
                ph += f / Rate;
                buf[i0 + k] += (Sin(ph) * Mathf.Exp(-t * 9f) + (k < 40 ? 0.4f * (1 - k / 40f) : 0)) * vel;
            }
        }

        static void Noise(float[] buf, float t0, float len, float decay, float hp, float bp, float vel, Rng r, float tone = 0, float toneDecay = 20f)
        {
            int i0 = (int)(t0 * Rate), n = (int)(len * Rate);
            float lp = 0, lp2 = 0; double ph = 0;
            for (int k = 0; k < n && i0 + k < buf.Length; k++)
            {
                if (i0 + k < 0) continue;
                float t = k / (float)Rate;
                float x = r.Next();
                lp += (x - lp) * hp;           // 高い音だけ残す（x - lp）
                float h = x - lp;
                lp2 += (h - lp2) * bp;         // 帯をしぼる
                float v = lp2 * Mathf.Exp(-t * decay);
                if (tone > 0) { ph += tone / Rate; v += Sin(ph) * Mathf.Exp(-t * toneDecay) * 0.6f; }
                buf[i0 + k] += v * vel * Mathf.Min(1, t * 2000f);
            }
        }

        static void Snare(float[] b, float t, float v, Rng r) => Noise(b, t, 0.25f, 16f, 0.08f, 0.6f, v, r, 190f, 25f);
        static void Clap(float[] b, float t, float v, Rng r) { for (int i = 0; i < 3; i++) Noise(b, t + i * 0.011f, 0.2f, i == 2 ? 14f : 60f, 0.15f, 0.45f, v * 0.9f, r); }
        static void HatC(float[] b, float t, float v, Rng r) => Noise(b, t, 0.06f, 70f, 0.55f, 0.95f, v, r);
        static void HatO(float[] b, float t, float v, Rng r) => Noise(b, t, 0.3f, 9f, 0.55f, 0.95f, v, r);
        static void Crash(float[] b, float t, float v, Rng r) => Noise(b, t, 2.2f, 1.8f, 0.4f, 0.9f, v, r);
        static void Shaker(float[] b, float t, float v, Rng r) => Noise(b, t, 0.09f, 40f, 0.6f, 0.7f, v, r);
        static void Rim(float[] b, float t, float v, Rng r) => Noise(b, t, 0.06f, 60f, 0.3f, 0.8f, v, r, 820f, 50f);

        /// <summary>サビの前の「シューッ」とのぼる音。</summary>
        static void Riser(float[] buf, float t0, float len, float vel, Rng r)
        {
            int i0 = (int)(t0 * Rate), n = (int)(len * Rate);
            float lp = 0;
            for (int k = 0; k < n && i0 + k < buf.Length; k++)
            {
                if (i0 + k < 0) continue;
                float u = k / (float)n;
                float x = r.Next();
                lp += (x - lp) * (0.02f + 0.5f * u * u);
                buf[i0 + k] += lp * u * u * vel;
            }
        }

        static void Tom(float[] buf, float t0, float f0, float vel)
        {
            int i0 = (int)(t0 * Rate), n = (int)(0.4f * Rate); double ph = 0;
            for (int k = 0; k < n && i0 + k < buf.Length; k++)
            {
                if (i0 + k < 0) continue;
                float t = k / (float)Rate;
                ph += (f0 * (1 + 0.5f * Mathf.Exp(-t * 20f))) / Rate;
                buf[i0 + k] += Sin(ph) * Mathf.Exp(-t * 7f) * vel;
            }
        }

        /// <summary>曲の音をぜんぶ作る（別のスレッドから呼んでよい。Unity の物は使わない）。</summary>
        public static float[] Render(LiveSong song)
        {
            int total = (int)(song.length * Rate);
            var buf = new float[total];
            var drm = new float[total];   // 打楽器（EDM では、ほかの音をキックに合わせて小さくする「ポンピング」のために分ける）
            var r = new Rng((uint)(song.bpm * 13 + song.style * 101));
            float bar = song.BarLen, beat = song.BeatLen, e8 = beat / 2, e16 = beat / 4;
            bool pop = song.style == 0, edm = song.style == 2, rock = song.style == 3;

            for (int si = 0; si < song.secs.Count; si++)
            {
                var sec = song.secs[si];
                var next = si + 1 < song.secs.Count ? song.secs[si + 1] : null;
                int sh = sec.shift;

                // メロディ（小節をまたいでのばせる）
                if (sec.inst != 2)
                {
                    var toks = new List<string>();
                    foreach (var m in sec.mel) toks.AddRange(m.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    for (int i = 0; i < toks.Count; i++)
                    {
                        var tk = toks[i];
                        if (tk == "-" || tk == ".") continue;
                        int len = 1;
                        while (i + len < toks.Count && toks[i + len] == "-") len++;
                        float t = sec.t0 + i * e8;
                        int midi = Midi(tk) + sh;
                        float d = len * e8 * 0.94f;
                        if (sec.inst == 1) Note(buf, Inst.Bell, t, d, midi, pop || edm ? 0.2f : 0.17f, r);
                        else if (edm)
                        {
                            if (sec.energy >= 3) { Note(buf, Inst.Saw5, t, d, midi, 0.16f, r); Note(buf, Inst.Bell, t, d, midi + 12, 0.06f, r); }
                            else Note(buf, Inst.Lead, t, d, midi, 0.22f, r);
                        }
                        else if (rock)
                        {
                            Note(buf, Inst.Lead, t, d, midi, 0.22f, r);
                            if (sec.energy >= 3) Note(buf, Inst.Bell, t, d, midi + 12, 0.045f, r);
                        }
                        else if (pop)
                        {
                            Note(buf, Inst.Lead, t, d, midi, 0.24f, r);
                            if (sec.energy >= 3) Note(buf, Inst.Bell, t, d, midi + 12, 0.05f, r); // サビはきらきらを重ねる
                        }
                        else
                        {
                            Note(buf, Inst.Flute, t, d, midi, 0.24f, r);
                            if (sec.last) Note(buf, Inst.Bell, t, d, midi + 12, 0.035f, r);
                        }
                    }
                }

                for (int b = 0; b < sec.Bars; b++)
                {
                    float tb = sec.t0 + b * bar;
                    var (root, tones, bassPc) = Chord(sec.chords[b]);
                    root = (root + sh + 12) % 12; bassPc = (bassPc + sh + 12) % 12;
                    // 和音の音を G3〜G4 あたりにならべる
                    var voice = new int[tones.Length];
                    for (int i = 0; i < tones.Length; i++) { int m = 48 + root + tones[i]; while (m < 55) m += 12; while (m >= 67) m -= 12; voice[i] = m; }
                    int bassM = 36 + bassPc; if (bassM < 38) bassM += 12;
                    bool lastBar = b == sec.Bars - 1;
                    bool fill = lastBar && next != null && next.energy > sec.energy;
                    bool firstBar = b == 0;

                    if (edm) EdmBar(buf, drm, sec, next, b, tb, voice, bassM, bar, beat, e8, e16, r);
                    else if (rock) RockBar(buf, sec, next, b, tb, root, bassM, bar, beat, e8, e16, r);
                    else if (pop)
                    {
                        // 和音のパッド
                        foreach (var m in voice) Note(buf, Inst.Pad, tb, bar * 0.98f, m, sec.energy >= 2 ? 0.075f : 0.06f, r);
                        // ベース
                        if (sec.name == "intro" && b < 4) { }
                        else if (sec.energy >= 3)
                            for (int k = 0; k < 8; k++) Note(buf, Inst.Bass, tb + k * e8, e8 * 0.8f, bassM + (k % 2 == 1 ? 12 : 0), 0.26f, r);
                        else if (sec.energy == 2)
                            for (int k = 0; k < 8; k++) Note(buf, Inst.Bass, tb + k * e8, e8 * 0.7f, bassM, 0.24f, r);
                        else
                            foreach (int k in new[] { 0, 3, 4, 6 }) Note(buf, Inst.Bass, tb + k * e8, e8 * 0.9f, bassM, 0.24f, r);
                        // 16分のアルペジオ（盛り上がる所だけ）
                        if (sec.energy >= 2)
                            for (int k = 0; k < 16; k++)
                            {
                                int m = voice[k % voice.Length] + 12 * ((k / voice.Length) % 2);
                                Note(buf, Inst.Arp, tb + k * e16, e16 * 0.6f, m, 0.045f, r);
                            }
                        // ドラム
                        if (sec.name == "intro" && b < 4)
                        {
                            for (int k = 0; k < 8; k++) HatC(buf, tb + k * e8, 0.05f + (b * 0.02f), r);
                            if (b == 3) Riser(buf, tb, bar, 0.25f, r);
                        }
                        else if (sec.name == "bridge" && b >= sec.Bars - 1)
                        {
                            // 最後のサビの前は、ドラムを止めて「ため」る
                            Kick(buf, tb, 0.6f);
                            Riser(buf, tb, bar, 0.35f, r);
                            for (int k = 0; k < 4; k++) Snare(buf, tb + bar * 0.5f + k * e16 * 2, 0.08f + k * 0.04f, r);
                        }
                        else
                        {
                            if (firstBar && sec.energy >= 2) Crash(buf, tb, sec.energy >= 3 ? 0.16f : 0.1f, r);
                            if (sec.energy >= 2 && !(fill && sec.name == "pre"))
                                for (int k = 0; k < 4; k++) Kick(buf, tb + k * beat, 0.62f);
                            else if (sec.energy <= 1)
                            { Kick(buf, tb, 0.58f); Kick(buf, tb + 2.5f * beat, 0.45f); }
                            if (fill && sec.name == "pre")
                            {
                                Kick(buf, tb, 0.6f); Kick(buf, tb + beat, 0.6f);
                                for (int k = 8; k < 16; k++) Snare(buf, tb + k * e16, 0.06f + (k - 8) * 0.025f, r);
                                Riser(buf, tb, bar, 0.3f, r);
                            }
                            else
                            {
                                Snare(buf, tb + beat, sec.energy >= 2 ? 0.26f : 0.18f, r);
                                Snare(buf, tb + 3 * beat, sec.energy >= 2 ? 0.26f : 0.18f, r);
                                if (sec.energy >= 3) { Clap(buf, tb + beat, 0.12f, r); Clap(buf, tb + 3 * beat, 0.12f, r); }
                                if (fill && sec.name != "pre") for (int k = 12; k < 16; k++) Snare(buf, tb + k * e16, 0.1f + (k - 12) * 0.04f, r);
                            }
                            for (int k = 0; k < 8; k++)
                            {
                                if (sec.energy >= 3 && k % 2 == 1) HatO(buf, tb + k * e8, 0.05f, r);
                                else HatC(buf, tb + k * e8, k % 2 == 0 ? 0.07f : 0.045f, r);
                            }
                        }
                        if (sec.name == "outro" && lastBar) { Crash(buf, tb + bar, 0.2f, r); Kick(buf, tb + bar, 0.7f); }
                    }
                    else
                    {
                        // バラード：ピアノの分散和音
                        int[] order = { 0, 1, 2, 3, 2, 1, 2, 3 };
                        Note(buf, Inst.Piano, tb, bar, bassM, 0.16f, r);
                        for (int k = 0; k < 8; k++)
                        {
                            int idx = order[k];
                            int m = idx == 0 ? bassM + 12 : voice[(idx - 1) % voice.Length] + (idx == 3 ? 12 : 0);
                            Note(buf, Inst.Piano, tb + k * e8, e8 * 1.6f, m, k == 0 ? 0.1f : 0.075f, r);
                        }
                        if (sec.energy >= 2 || sec.name == "bridge")
                            foreach (var m in voice) Note(buf, Inst.Strings, tb, bar * 0.99f, m + (sec.energy >= 3 ? 12 : 0), sec.energy >= 3 ? 0.06f : 0.05f, r);
                        if (sec.energy >= 1 && sec.name != "intro") Note(buf, Inst.BalladBass, tb, bar * 0.95f, bassM - 12 < 28 ? bassM : bassM - 12, 0.22f, r);
                        // 打楽器はひかえめ（サビだけ）
                        if (sec.energy >= 2)
                        {
                            Kick(buf, tb, 0.42f); Kick(buf, tb + 1.5f * beat, 0.3f);
                            Rim(buf, tb + 2 * beat, sec.energy >= 3 ? 0.16f : 0.1f, r);
                            for (int k = 0; k < 8; k++) Shaker(buf, tb + k * e8, k % 2 == 0 ? 0.04f : 0.025f, r);
                            if (firstBar) Crash(buf, tb, 0.08f, r);
                        }
                        else if (sec.energy == 1 && sec.name == "pre")
                            for (int k = 0; k < 8; k++) Shaker(buf, tb + k * e8, 0.02f, r);
                        if (fill && sec.energy >= 1)
                        {
                            Tom(buf, tb + 2 * beat, 140f, 0.25f); Tom(buf, tb + 2.5f * beat, 115f, 0.25f);
                            Tom(buf, tb + 3 * beat, 95f, 0.28f); Tom(buf, tb + 3.5f * beat, 80f, 0.3f);
                        }
                        if (sec.name == "bridge" && lastBar) Riser(buf, tb, bar, 0.12f, r);
                        if (sec.name == "outro" && lastBar) Note(buf, Inst.Bell, tb + bar, 2.5f, 48 + root + 24, 0.12f, r);
                    }
                }
            }

            Reverb(buf, song.reverb);
            if (edm)
            {
                // ポンピング：拍の頭（キック）で、ほかの音を一瞬小さくする
                for (int i = 0; i < total; i++)
                {
                    float t = i / (float)Rate - song.lead;
                    float g = 1f;
                    if (t > 0)
                    {
                        float tb = t - Mathf.Floor(t / beat) * beat;
                        var sec = song.At(t + song.lead);
                        if (sec != null && sec.energy >= 2) g = 1f - 0.6f * Mathf.Exp(-tb * 9f);
                    }
                    buf[i] = buf[i] * g + drm[i];
                }
            }
            else for (int i = 0; i < total; i++) buf[i] += drm[i];
            Master(buf);
            return buf;
        }

        static void EdmBar(float[] buf, float[] drm, LiveSec sec, LiveSec next, int b, float tb, int[] voice, int bassM, float bar, float beat, float e8, float e16, Rng r)
        {
            bool lastBar = b == sec.Bars - 1, firstBar = b == 0;
            bool fill = lastBar && next != null && next.energy > sec.energy;
            bool hype = sec.energy >= 3;
            string n = sec.name;
            // 和音
            if (n == "drop" || (hype && n == "chorus"))
            {
                for (int k = 0; k < 8; k++)
                    if (k % 2 == 1 || n == "chorus")
                        foreach (var m in voice) Note(buf, Inst.Saw5, tb + k * e8, e8 * (n == "chorus" ? 0.9f : 0.5f), m, n == "drop" ? 0.06f : 0.035f, r);
            }
            else if (n == "pre")
            {
                for (int k = 0; k < 8; k++) foreach (var m in voice) Note(buf, Inst.Saw5, tb + k * e8, e8 * 0.4f, m, 0.03f + b * 0.003f, r);
            }
            else foreach (var m in voice) Note(buf, Inst.Pad, tb, bar * 0.98f, m, 0.06f, r);
            if (sec.energy >= 2)
                for (int k = 0; k < 16; k++) Note(buf, Inst.Arp, tb + k * e16, e16 * 0.5f, voice[k % voice.Length] + 12 * ((k / voice.Length) % 2) + 12, 0.035f, r);
            // ベース
            if (hype || n == "pre")
                for (int k = 0; k < 8; k++)
                {
                    if (k % 2 == 1) Note(buf, Inst.Bass, tb + k * e8, e8 * 0.7f, bassM + 12, 0.24f, r);
                    else Note(buf, Inst.Sub, tb + k * e8, e8 * 0.9f, bassM, 0.3f, r);
                }
            else if (n != "intro" || b >= 4) { Note(buf, Inst.Sub, tb, bar * 0.48f, bassM, 0.3f, r); Note(buf, Inst.Sub, tb + 2 * beat, bar * 0.45f, bassM, 0.28f, r); }
            // 打楽器
            if (n == "bridge")
            {
                Clap(drm, tb + beat, 0.08f, r); Clap(drm, tb + 3 * beat, 0.08f, r);
                if (lastBar) { Riser(drm, tb, bar, 0.4f, r); for (int k = 8; k < 16; k++) Snare(drm, tb + k * e16, 0.05f + (k - 8) * 0.03f, r); }
                return;
            }
            if (n == "intro" && b < 4) { for (int k = 0; k < 8; k++) HatC(drm, tb + k * e8, 0.04f + b * 0.015f, r); if (b == 3) Riser(drm, tb, bar, 0.3f, r); return; }
            if (firstBar && sec.energy >= 2) Crash(drm, tb, 0.15f, r);
            if (fill && n == "pre")
            {
                for (int k = 0; k < 16; k++) Snare(drm, tb + k * e16, 0.04f + k * 0.014f, r);
                Riser(drm, tb, bar, 0.4f, r);
                return;
            }
            if (sec.energy >= 2) for (int k = 0; k < 4; k++) Kick(drm, tb + k * beat, 0.7f);
            else { Kick(drm, tb, 0.6f); Kick(drm, tb + 2 * beat, 0.55f); }
            Clap(drm, tb + beat, sec.energy >= 2 ? 0.16f : 0.1f, r); Clap(drm, tb + 3 * beat, sec.energy >= 2 ? 0.16f : 0.1f, r);
            for (int k = 0; k < 8; k++)
            {
                if (k % 2 == 1) { if (sec.energy >= 2) HatO(drm, tb + k * e8, 0.06f, r); else HatC(drm, tb + k * e8, 0.05f, r); }
                else HatC(drm, tb + k * e8, 0.035f, r);
            }
            if (hype) for (int k = 1; k < 16; k += 2) HatC(drm, tb + k * e16, 0.025f, r);
            if (fill) for (int k = 12; k < 16; k++) Snare(drm, tb + k * e16, 0.1f + (k - 12) * 0.04f, r);
            if (n == "outro" && lastBar) { Crash(drm, tb + bar, 0.2f, r); Kick(drm, tb + bar, 0.8f); }
        }

        static void RockBar(float[] buf, LiveSec sec, LiveSec next, int b, float tb, int root, int bassM, float bar, float beat, float e8, float e16, Rng r)
        {
            bool lastBar = b == sec.Bars - 1, firstBar = b == 0;
            bool fill = lastBar && next != null && next.energy >= sec.energy && next.name != sec.name;
            string n = sec.name;
            int g0 = bassM + 12; // ギターの根音
            int[] power = { g0, g0 + 7, g0 + 12 };
            // ギター
            if (n == "verse")
                for (int k = 0; k < 8; k++) foreach (var m in power) Note(buf, Inst.Guitar, tb + k * e8, e8 * 0.4f, m, 0.05f, r);   // ブリッジミュートの刻み
            else if (n == "bridge")
                foreach (var m in power) Note(buf, Inst.Pad, tb, bar * 0.98f, m + 12, 0.06f, r);
            else if (n == "pre")
                for (int k = 0; k < 4; k++) foreach (var m in power) Note(buf, Inst.Guitar, tb + k * beat, beat * 0.9f, m, 0.055f, r);
            else
            {
                foreach (var m in power) Note(buf, Inst.Guitar, tb, beat * 1.4f, m, 0.06f, r);
                for (int k = 3; k < 8; k++) foreach (var m in power) Note(buf, Inst.Guitar, tb + k * e8, e8 * 0.8f, m, 0.05f, r);
            }
            // ベース
            if (n != "bridge") for (int k = 0; k < 8; k++) Note(buf, Inst.Bass, tb + k * e8, e8 * 0.8f, bassM, 0.24f, r);
            else Note(buf, Inst.Bass, tb, bar * 0.9f, bassM, 0.22f, r);
            // ドラム
            if (n == "bridge")
            {
                Kick(buf, tb, 0.5f); HatC(buf, tb + beat, 0.04f, r); HatC(buf, tb + 3 * beat, 0.04f, r);
                if (lastBar) { for (int k = 0; k < 8; k++) Tom(buf, tb + bar * 0.5f + k * e16, 180f - k * 14f, 0.28f); Riser(buf, tb, bar, 0.3f, r); }
                return;
            }
            if (firstBar || sec.energy >= 3 && b % 2 == 0) Crash(buf, tb, 0.13f, r);
            int[] kicks = sec.energy >= 3 ? new[] { 0, 2, 3, 4, 6 } : sec.energy == 2 ? new[] { 0, 2, 4, 6 } : new[] { 0, 3, 4 };
            foreach (int k in kicks) Kick(buf, tb + k * e8, 0.6f);
            if (fill)
            {
                Snare(buf, tb + beat, 0.25f, r);
                for (int k = 8; k < 16; k++) Tom(buf, tb + k * e16, 200f - k * 8f, 0.26f);
                return;
            }
            Snare(buf, tb + beat, 0.28f, r); Snare(buf, tb + 3 * beat, 0.28f, r);
            for (int k = 0; k < 8; k++) { if (sec.energy >= 3) HatO(buf, tb + k * e8, 0.04f, r); else HatC(buf, tb + k * e8, 0.06f, r); }
            if (n == "outro" && lastBar) { Crash(buf, tb + bar, 0.22f, r); Kick(buf, tb + bar, 0.8f); }
        }

        /// <summary>ホールのような響き（くし形のフィルタ4つ＋全域通過2つ）。</summary>
        static void Reverb(float[] buf, float mix)
        {
            float sc = Rate / 44100f;
            int[] cl = { (int)(1116 * sc), (int)(1188 * sc), (int)(1277 * sc), (int)(1356 * sc) };
            int[] al = { (int)(556 * sc), (int)(441 * sc) };
            var cb = new float[4][]; var cs = new float[4]; var ci = new int[4];
            for (int i = 0; i < 4; i++) cb[i] = new float[cl[i]];
            var ab = new float[2][]; var ai = new int[2];
            for (int i = 0; i < 2; i++) ab[i] = new float[al[i]];
            const float fb = 0.82f, damp = 0.3f;
            for (int n = 0; n < buf.Length; n++)
            {
                float x = buf[n] * 0.3f, o = 0;
                for (int i = 0; i < 4; i++)
                {
                    float y = cb[i][ci[i]];
                    cs[i] = y * (1 - damp) + cs[i] * damp;
                    cb[i][ci[i]] = x + cs[i] * fb;
                    if (++ci[i] >= cl[i]) ci[i] = 0;
                    o += y;
                }
                for (int i = 0; i < 2; i++)
                {
                    float bo = ab[i][ai[i]];
                    ab[i][ai[i]] = o + bo * 0.5f;
                    if (++ai[i] >= al[i]) ai[i] = 0;
                    o = bo - o;
                }
                buf[n] += o * mix;
            }
        }

        /// <summary>音量をそろえ、大きすぎる所をやわらかくおさえる。はじめと終わりは静かに。</summary>
        static void Master(float[] buf)
        {
            double sum = 0; int cnt = 0;
            for (int i = 0; i < buf.Length; i += 7) { sum += buf[i] * buf[i]; cnt++; }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, cnt));
            float g = rms > 1e-5f ? 0.2f / rms : 1f;
            int fade = Rate / 50;
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i] * g;
                x = x * (27 + x * x) / (27 + 9 * x * x);  // tanh に近いやわらかい頭打ち
                if (x > 0.98f) x = 0.98f; else if (x < -0.98f) x = -0.98f;
                if (i < fade) x *= i / (float)fade;
                int e = buf.Length - 1 - i;
                if (e < Rate * 2) x *= e / (float)(Rate * 2);
                buf[i] = x;
            }
        }

        /// <summary>確認用：音の大きさ（RMS）。</summary>
        public static float Rms(float[] buf, float t0, float t1)
        {
            int a = Mathf.Clamp((int)(t0 * Rate), 0, buf.Length), b = Mathf.Clamp((int)(t1 * Rate), 0, buf.Length);
            double s = 0; for (int i = a; i < b; i++) s += buf[i] * buf[i];
            return b > a ? (float)Math.Sqrt(s / (b - a)) : 0;
        }
    }
}
