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

        public static void CloseTalk() { _talk = false; YachiyoNpc.Talking = false; }

        // ───────── 会話の板（2026-11-18：IMGUI から板へ。VR でも光の線とトリガーで押せる）─────────

        Board _tk;

        string _tkKey;
        const float TkW = 1100, TkH = 300;

        /// <summary>毎フレーム（BoardsTick から）。</summary>
        void TalkTick()
        {
            if (!_talk) { if (_tk != null) _tk.Alpha = 0; _tkKey = null; return; }
            if (_tk == null)
            {
                _tk = Board.Create("ヤチヨとの会話", new Vector2(TkW, TkH), 34);
                _tk.screenAnchor = new Vector2(0.5f, 0); _tk.screenOffset = new Vector2(0, 90);
                _tk.worldOffset = new Vector3(0, -0.25f, 1.15f);
                _tk.Apply();
            }
            _tk.Alpha = 1;
            string key = $"{_yLine}|{YachiyoNpc.TimeChanged}";
            if (key == _tkKey) return;
            _tkKey = key;
            var r = _tk.root;
            Board.Clear(r);
            Board.Panel(r, new Rect(0, 26, TkW, TkH - 26), new Color(0.01f, 0.03f, 0.08f, 0.9f));
            Board.Corners(r, new Rect(0, 26, TkW, TkH - 26), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.9f), 18, 3);
            Board.Panel(r, new Rect(0, 26, 6, TkH - 26), Board.Pink);
            Board.Panel(r, new Rect(34, 0, 200, 48), new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.95f));
            Board.Label(r, new Rect(34, 0, 200, 48), "ヤチヨ", 28, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Board.Wrap(r, new Rect(50, 66, TkW - 100, 100), _yLine, 30, Color.white, TextAnchor.UpperLeft);
            float bx = 40, by = TkH - 96, bw = 235, gap = 14;
            var ya = YachiyoNpc.I;
            if (!YachiyoNpc.TimeChanged)
                Board.Btn(r, new Rect(bx, by, bw, 64), "夜にして", 24, () =>
                {
                    if (YachiyoNpc.IsNight) _yLine = "いまはもう夜だよ。ほら、月がきれい…";
                    else if (ya != null) { ya.MakeNight(); CloseTalk(); }
                });
            else Board.Btn(r, new Rect(bx, by, bw, 64), "もとの時間にもどして", 21, () => { ya?.RestoreTime(); CloseTalk(); });
            bx += bw + gap;
            Board.Btn(r, new Rect(bx, by, bw, 64), "ライブをお願い", 24, () => { CloseTalk(); Live.Request(player, cam); });
            bx += bw + gap;
            Board.Btn(r, new Rect(bx, by, bw, 64), "おしゃべり", 24, () =>
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
            });
            Board.Btn(r, new Rect(TkW - 40 - bw, by, bw, 64), Vr.On ? "またね（Y）" : "またね（Esc）", 24, CloseTalk);
        }
    }
}
