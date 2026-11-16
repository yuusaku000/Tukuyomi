using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>ヤチヨとの会話の画面（大鳥居の高舞台で E）。</summary>
    public partial class Hud
    {
        static bool _talk;
        static string _yLine = "";
        static readonly string[] Hello =
        {
            "ヤオヨロ〜！ ツクヨミへようこそ。今日はなにして遊ぶ？",
            "あ、来てくれたんだ！ うれしいな〜",
            "月がきれいだね。なにか、お願いある？",
            "ふふ、ツクヨミのことなら、なんでも聞いてね",
        };

        /// <summary>自分の場所（ヤチヨがこちらを向くため）。</summary>
        public static Vector3? PlayerPos => _i != null && _i.player != null ? _i.player.Position : (Vector3?)null;

        public static bool TalkOpen => _talk;

        static void OpenTalk()
        {
            _talk = true; YachiyoNpc.Talking = true;
            _yLine = Hello[Random.Range(0, Hello.Length)];
        }

        /// <summary>確認用（--tour の talk）。</summary>
        public static void TestTalk() => OpenTalk();

        static void CloseTalk() { _talk = false; YachiyoNpc.Talking = false; }

        void DrawTalk()
        {
            var r = new Rect(W / 2 - 520, H - 420, 1040, 300);
            Fill(r, new Color(0.01f, 0.03f, 0.08f, 0.82f));
            Panel(r);
            var tag = new Rect(r.x + 30, r.y - 26, 200, 46);
            Fill(tag, new Color(Pink.r, Pink.g, Pink.b, 0.9f));
            Text(tag, "ヤチヨ", 26, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Text(new Rect(r.x + 40, r.y + 34, r.width - 80, 80), _yLine, 28, Color.white, TextAnchor.UpperLeft);
            float bx = r.x + 40, by = r.yMax - 100, bw = 230, gap = 16;
            var ya = YachiyoNpc.I;
            if (!YachiyoNpc.TimeChanged)
            {
                if (Button(new Rect(bx, by, bw, 64), "夜にして"))
                {
                    if (YachiyoNpc.IsNight) _yLine = "いまはもう夜だよ。ほら、月がきれい…";
                    else if (ya != null) { ya.MakeNight(); CloseTalk(); }
                }
            }
            else if (Button(new Rect(bx, by, bw, 64), "もとの時間にもどして", 22)) { ya?.RestoreTime(); CloseTalk(); }
            bx += bw + gap;
            if (Button(new Rect(bx, by, bw, 64), "ライブをお願い")) { CloseTalk(); Live.Request(player, cam); }
            bx += bw + gap;
            if (Button(new Rect(bx, by, bw, 64), "おしゃべり"))
            {
                string[] chat =
                {
                    "釣り堀の番付、見た？ 今週はだれが一位かな〜",
                    "遊覧船「月の舟」、のったことある？ 空から見るツクヨミ、きれいだよ",
                    "月見の湯、ぽかぽかでおすすめ！",
                    "夜になると、空にクジラが泳ぐこともあるんだって",
                    "わたしの新しい曲、ライブで歌うから待っててね",
                };
                _yLine = chat[Random.Range(0, chat.Length)];
            }
            if (Button(new Rect(r.xMax - 40 - bw, by, bw, 64), "またね（Esc）")) CloseTalk();
        }
    }
}
