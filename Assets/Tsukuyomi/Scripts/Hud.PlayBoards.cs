using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// 遊んでいる間の「見るだけの板」（釣りのゲージ・釣れた魚・ライブの曲名と歌詞とヤチヨのことば）と、投げ銭の板。
    /// もとは IMGUI で描いていたもの。見るだけの板は passive（光の線をあてない）。VR では目の前の少し下や横に浮かぶ。
    /// 毎フレーム作り直さず、文字・長さ・見える度合いだけを変える。
    /// </summary>
    public partial class Hud
    {
        /// <summary>見るだけの板を作る。</summary>
        static Board Passive(string name, Vector2 size, Vector2 anchor, Vector2 offset, Vector3 world, int order = 5)
        {
            var b = Board.Create(name, size, order);
            b.passive = true;
            b.screenAnchor = anchor; b.screenOffset = offset; b.worldOffset = world;
            b.Apply();
            b.Alpha = 0;
            return b;
        }

        /// <summary>ゲージ（地・中身・枠・上の文字）。</summary>
        class Gauge
        {
            public RectTransform root; public Image fill; public Text label, right; float _w;
            public Gauge(RectTransform p, Rect r)
            {
                root = Board.Group(p, "ゲージ", r);
                Board.Panel(root, new Rect(0, 0, r.width, r.height), new Color(0.02f, 0.05f, 0.12f, 0.7f));
                fill = Board.Panel(root, new Rect(2, 2, r.width - 4, r.height - 4), Color.white);
                _w = r.width - 4;
                Board.Outline(root, new Rect(0, 0, r.width, r.height), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.6f), 1f);
                label = Board.Label(root, new Rect(0, -28, r.width, 24), "", 18, new Color(1, 1, 1, 0.85f));
                right = Board.Label(root, new Rect(0, -28, r.width, 24), "", 18, Board.Gold, TextAnchor.MiddleRight, true);
            }
            public void Set(float v, Color c, string l, string r)
            {
                fill.rectTransform.sizeDelta = new Vector2(_w * Mathf.Clamp01(v), fill.rectTransform.sizeDelta.y);
                fill.color = c; label.text = l; right.text = r ?? "";
            }
        }

        // ───────── 釣り ─────────

        Board _fb, _cb;
        Text _fbBang, _fbMsg, _fbState, _fbHint, _fbWho;
        RectTransform _fbCharge, _fbFight;
        Gauge _gPower, _gTension, _gProgress;
        CatchResult _cbShown;

        const float FbW = 1000, FbH = 700;

        void BuildFishBoards()
        {
            if (_fb != null) return;
            // 画面の下のまん中（もとの IMGUI と同じ場所）。VR では目の前の少し下
            _fb = Passive("釣り", new Vector2(FbW, FbH), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector3(0, -0.2f, 1.25f));
            var r = _fb.root;
            _fbBang = Board.Label(r, new Rect(0, 20, FbW, 120), "！", 110, Board.Pink, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _fbMsg = Board.Label(r, new Rect(0, 380, FbW, 36), "", 24, Board.Gold, TextAnchor.MiddleCenter);
            _fbCharge = Board.Group(r, "投げる", new Rect(170, 480, 660, 150));
            Board.Panel(_fbCharge, new Rect(0, 0, 660, 150), Board.Glass);
            Board.Corners(_fbCharge, new Rect(0, 0, 660, 150), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.9f), 18, 3);
            _gPower = new Gauge(_fbCharge, new Rect(40, 70, 580, 30));
            _fbFight = Board.Group(r, "かかった", new Rect(140, 450, 720, 200));
            Board.Panel(_fbFight, new Rect(0, 0, 720, 200), Board.Glass);
            Board.Corners(_fbFight, new Rect(0, 0, 720, 200), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.9f), 18, 3);
            _gTension = new Gauge(_fbFight, new Rect(40, 58, 640, 30));
            _gProgress = new Gauge(_fbFight, new Rect(40, 130, 640, 22));
            _fbWho = Board.Label(_fbFight, new Rect(0, 160, 720, 30), "？？？ がかかっている", 18, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            _fbState = Board.Label(r, new Rect(0, 530, FbW, 44), "", 26, Color.white, TextAnchor.MiddleCenter);
            _fbHint = Board.Label(r, new Rect(0, 660, FbW, 30), "", 19, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);

            // 釣れた魚（右）。VR では右前
            _cb = Passive("釣れた魚", new Vector2(560, 400), new Vector2(1, 0.5f), new Vector2(-40, 80), new Vector3(0.48f, 0.02f, 1.35f), 6);
        }

        void FishBoardsTick()
        {
            var f = Fishing.I;
            bool can = f != null && !Blocked && !HideAll && !Kassen.Busy && !_menu && !_guide && !_exchange;
            bool fishing = can && Fishing.Busy;
            bool catchUp = can && f.result != null && f.resultT > 0;
            if (!fishing && !catchUp) { if (_fb != null) { _fb.Alpha = 0; _cb.Alpha = 0; } return; }
            BuildFishBoards();
            float t = Time.unscaledTime;
            _fb.Alpha = fishing ? 1 : 0;
            if (fishing)
            {
                string hint = "", state = "";
                bool bang = false;
                _fbCharge.gameObject.SetActive(f.state == Fishing.St.Charge);
                _fbFight.gameObject.SetActive(f.state == Fishing.St.Fight);
                _fbState.fontSize = 26; _fbState.color = Color.white; _fbState.fontStyle = FontStyle.Normal;
                switch (f.state)
                {
                    case Fishing.St.Ready:
                        hint = Vr.On ? "トリガー長押し ▸ はなすと投げる　手の向きで向き" : "クリック（Space）長押し ▸ はなすと投げる　マウスで向き　E・Esc ▸ 竿をしまう";
                        state = "池のほうを向いて投げよう";
                        break;
                    case Fishing.St.Charge:
                        _gPower.Set(f.power, Color.Lerp(new Color(0.4f, 0.9f, 1f), Board.Pink, f.power), "投げる強さ", $"{Mathf.Lerp(3f, 12f, f.power):0}m");
                        hint = "はなすと投げる";
                        break;
                    case Fishing.St.Fly:
                    case Fishing.St.Wait:
                        state = "浮きを見て…　ずぶっと沈んだらクリック！";
                        hint = "ちょんと動くのはまだ　クリックで巻きもどす　E・Esc ▸ 竿をしまう";
                        break;
                    case Fishing.St.Bite:
                        bang = Mathf.Repeat(t, 0.25f) < 0.17f;
                        state = "いまだ！ クリック！";
                        _fbState.fontSize = 34; _fbState.color = Board.Gold; _fbState.fontStyle = FontStyle.Bold;
                        break;
                    case Fishing.St.Fight:
                    {
                        bool red = f.tension > 0.78f;
                        var tc = f.tension < 0.5f ? new Color(0.45f, 1f, 0.6f) : f.tension < 0.78f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.25f, 0.3f);
                        _gTension.Set(f.tension, tc, "糸の張り", red ? (Mathf.Repeat(t, 0.3f) < 0.2f ? "あぶない！ 離して！" : "") : f.burst ? "暴れている！" : "");
                        _gProgress.Set(f.progress, new Color(0.45f, 0.9f, 1f), "手もとまで", f.reeling ? "たぐっている" : "");
                        hint = "押している間たぐる・赤くなったら離す";
                        break;
                    }
                }
                _fbBang.enabled = bang;
                _fbState.text = state;
                _fbHint.text = hint;
                if (f.msgT > 0 && !string.IsNullOrEmpty(f.msg)) { _fbMsg.text = f.msg; _fbMsg.color = new Color(Board.Gold.r, Board.Gold.g, Board.Gold.b, Mathf.Clamp01(f.msgT / 0.5f)); }
                else _fbMsg.text = "";
            }
            // 釣れた魚
            if (catchUp)
            {
                if (_cbShown != f.result) { _cbShown = f.result; BuildCatch(f.result); }
                _cb.Alpha = Mathf.Clamp01(f.resultT / 0.6f);
            }
            else _cb.Alpha = 0;
        }

        void BuildCatch(CatchResult res)
        {
            var r = _cb.root;
            Board.Clear(r);
            const float w = 560, h = 400;
            Board.Panel(r, new Rect(0, 0, w, h), new Color(Board.Glass.r, Board.Glass.g, Board.Glass.b, 0.85f));
            Board.Corners(r, new Rect(0, 0, w, h), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.9f), 18, 3);
            Board.Panel(r, new Rect(0, 0, 5, h), Board.Pink);
            var k = res.kind;
            Board.Label(r, new Rect(26, 16, 300, 22), "CATCH ▸ 釣れた！", 18, Board.Cyan, TextAnchor.MiddleLeft, true);
            Board.Label(r, new Rect(26, 16, w - 52, 22), k.Stars + "  " + k.RankName, 18, Board.Gold, TextAnchor.MiddleRight);
            Board.Raw(r, new Rect(w / 2 - 140, 46, 280, 140)).texture = FishBook.Icon(k, true);
            Board.Label(r, new Rect(0, 190, w, 54), k.name, 44, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Board.Label(r, new Rect(0, 244, w, 40), $"{res.cm:0.0} cm", 34, Board.Gold, TextAnchor.MiddleCenter, true);
            string tags = (res.isNew ? "図鑑に登録！　" : "") + (res.isBest ? "自己ベスト！　" : "") + (res.weekBest && res.weekRank > 0 ? $"今週の番付 {res.weekRank}位" : "");
            if (tags.Length > 0) Board.Label(r, new Rect(0, 290, w, 30), tags, 21, Board.Pink, TextAnchor.MiddleCenter);
            Board.Wrap(r, new Rect(24, 326, w - 48, 64), k.desc, 19, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
        }

        // ───────── ライブ ─────────

        Board _lvTitle, _lvOpen, _lvLine, _lvPen, _lvAway, _lvBarTop, _lvBarBot;
        Text _lvEn, _lvSong, _lvCredit, _lvOpenEn, _lvOpenBig, _lvCount, _lvPrep, _lvLyric, _lvTalk, _lvPenText;
        Image _lvUnder, _lvLyricBg, _lvTalkBg, _lvTalkBar, _lvTalkTag, _lvPenDot;
        Text _lvTalkTagText;

        const float LvLineW = 1500, LvLineH = 210;

        void BuildLiveBoards()
        {
            if (_lvTitle != null) return;
            // 曲名（左上）
            _lvTitle = Passive("ライブの曲名", new Vector2(960, 130), new Vector2(0, 1), new Vector2(60, -150), new Vector3(-0.35f, 0.3f, 1.6f));
            var r = _lvTitle.root;
            _lvEn = Board.Label(r, new Rect(0, 0, 900, 24), "", 18, Board.Cyan, TextAnchor.MiddleLeft, true);
            _lvSong = Board.Label(r, new Rect(0, 24, 960, 70), "", 50, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            _lvCredit = Board.Label(r, new Rect(6, 92, 800, 28), "作詞・作曲　ヤチヨ", 18, Board.Gold);
            _lvUnder = Board.Panel(r, new Rect(0, 124, 300, 2), Board.Pink);

            // 開演前（上のまん中）と、数字の数えおろし
            _lvOpen = Passive("ライブの開演", new Vector2(1200, 560), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector3(0, 0.12f, 1.6f));
            r = _lvOpen.root;
            _lvOpenEn = Board.Label(r, new Rect(0, 0, 1200, 40), "YACHIYO LIVE ▸ TSUKUYOMI", 22, Board.Cyan, TextAnchor.MiddleCenter, true);
            _lvOpenBig = Board.Label(r, new Rect(0, 40, 1200, 70), "まもなく開演", 52, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _lvPrep = Board.Label(r, new Rect(0, 118, 1200, 36), "（曲を準備しています…）", 20, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            _lvCount = Board.Label(r, new Rect(0, 230, 1200, 260), "", 170, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold);

            // 歌詞とヤチヨのことば（下のまん中）
            _lvLine = Passive("ライブの歌詞", new Vector2(LvLineW, LvLineH), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector3(0, -0.3f, 1.6f));
            r = _lvLine.root;
            _lvTalkBg = Board.Panel(r, new Rect(0, 0, 10, 58), new Color(0.02f, 0.03f, 0.1f, 0.72f));
            _lvTalkBar = Board.Panel(_lvTalkBg.rectTransform, new Rect(0, 0, 5, 58), Board.Pink);
            _lvTalkTag = Board.Panel(_lvTalkBg.rectTransform, new Rect(14, -24, 110, 28), new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.9f));
            _lvTalkTagText = Board.Label(_lvTalkTag.rectTransform, new Rect(0, 0, 110, 28), "ヤチヨ", 18, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _lvTalk = Board.Label(_lvTalkBg.rectTransform, new Rect(0, 0, 10, 58), "", 28, Color.white, TextAnchor.MiddleCenter);
            _lvLyricBg = Board.Panel(r, new Rect(0, LvLineH - 72, 10, 64), new Color(0.01f, 0.02f, 0.06f, 0.45f));
            _lvLyric = Board.Label(r, new Rect(0, LvLineH - 64, LvLineW, 50), "", 40, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _lvLyric.supportRichText = true;
            Destroy(_lvLyric.GetComponent<Shadow>());

            // ペンライトとキーの案内（右下・画面モードだけ）
            _lvPen = Passive("ライブのキー", new Vector2(620, 40), new Vector2(1, 0), new Vector2(-40, 78), new Vector3(0.45f, -0.45f, 1.6f));
            r = _lvPen.root;
            Board.Panel(r, new Rect(0, 0, 620, 40), new Color(0.02f, 0.05f, 0.12f, 0.45f));
            _lvPenDot = Board.Panel(r, new Rect(10, 12, 16, 16), Color.white);
            _lvPenText = Board.Label(r, new Rect(34, 0, 586, 40), "", 18, new Color(1, 1, 1, 0.85f));

            // 会場の外（右上）
            _lvAway = Passive("ライブの案内", new Vector2(560, 30), new Vector2(1, 1), new Vector2(-40, -106), new Vector3(0.4f, 0.32f, 1.6f));
            Board.Label(_lvAway.root, new Rect(0, 0, 560, 30), "♪ ヤチヨのライブ中（ARライブステージ）", 18, new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.85f), TextAnchor.MiddleRight);

            // おまかせカメラの上下の黒い帯（画面モードだけ）
            _lvBarTop = Passive("映画の帯（上）", new Vector2(4000, 64), new Vector2(0, 1), Vector2.zero, Vector3.forward, 1);
            Board.Panel(_lvBarTop.root, new Rect(0, 0, 4000, 64), new Color(0, 0, 0, 0.85f));
            _lvBarBot = Passive("映画の帯（下）", new Vector2(4000, 64), new Vector2(0, 0), Vector2.zero, Vector3.forward, 1);
            Board.Panel(_lvBarBot.root, new Rect(0, 0, 4000, 64), new Color(0, 0, 0, 0.85f));
            Board.Label(_lvBarBot.root, new Rect(30, 12, 700, 40), "● LIVE　おまかせカメラ（C・Esc でもどる）", 17, new Color(1, 1, 1, 0.6f));
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGBA(c);

        void LiveBoardsTick()
        {
            var ls = LiveShow.I;
            bool show = ls != null && LiveShow.Running && !Kassen.Busy && !HideAll && !_menu && !_guide && !_exchange && !_map && !_book && _shipAsk == null;
            if (!show)
            {
                if (_lvTitle != null) _lvTitle.Alpha = _lvOpen.Alpha = _lvLine.Alpha = _lvPen.Alpha = _lvAway.Alpha = _lvBarTop.Alpha = _lvBarBot.Alpha = 0;
                return;
            }
            BuildLiveBoards();
            float t = Time.unscaledTime;
            bool auto = LiveShow.AutoCam;
            bool here = LiveShow.PlayerInVenue || auto;
            bool screen = Board.Current == Board.Mode.Screen;
            _lvBarTop.Alpha = _lvBarBot.Alpha = auto && screen ? 1 : 0;
            _lvAway.Alpha = here ? 0 : 1;
            if (!here) { _lvTitle.Alpha = _lvOpen.Alpha = _lvLine.Alpha = _lvPen.Alpha = 0; return; }

            // 開演前
            if (LiveShow.Now == LiveShow.Phase.Opening)
            {
                _lvOpen.Alpha = 1;
                float cd = ls.CountDown;
                _lvCount.text = "";
                _lvPrep.text = "";
                if (cd > 0 && cd <= 3.2f)
                {
                    int n = Mathf.CeilToInt(cd - 0.2f);
                    float f = cd - Mathf.Floor(cd);
                    if (n >= 1) { _lvCount.text = n.ToString(); _lvCount.fontSize = (int)(170 + f * 40); _lvCount.color = new Color(1, 1, 1, Mathf.Clamp01(f * 2f)); }
                }
                else if (cd < 0) { _lvPrep.text = "（曲を準備しています…）"; _lvPrep.color = new Color(1, 1, 1, 0.6f + 0.3f * Mathf.Sin(t * 3f)); }
            }
            else _lvOpen.Alpha = 0;

            // 曲名（曲のはじめに大きく）
            var song = ls.Song;
            float lineA = 0;
            string line = null; float prog = 0;
            if (song != null)
            {
                float st = ls.SongTime;
                float a = Mathf.Clamp01((st - 1f) / 0.6f) * Mathf.Clamp01((9f - st) / 1f);
                if (song.style == 1) a = Mathf.Clamp01((st - 1f) / 1f) * Mathf.Clamp01((10f - st) / 1.5f);
                _lvTitle.Alpha = a;
                if (a > 0)
                {
                    _lvEn.text = $"M{ls.SongNo} ▸ " + song.en;
                    _lvSong.text = "♪ " + song.title;
                    _lvUnder.rectTransform.sizeDelta = new Vector2(300 * a, 2);
                    float y = auto && screen ? -100 : -150;
                    if (_lvTitle.screenOffset.y != y) { _lvTitle.screenOffset = new Vector2(60, y); _lvTitle.Apply(); }
                }
                line = song.Lyric(st, out prog, out lineA);
            }
            else _lvTitle.Alpha = 0;

            // 字幕（歌詞）：行のうち、歌った所までを色づける（色つきの文字で1行にする）
            bool hasLine = line != null && lineA > 0;
            if (hasLine)
            {
                int nChars = Mathf.Clamp(Mathf.RoundToInt(line.Length * prog), 0, line.Length);
                var col = song.style == 1 ? new Color(1f, 0.86f, 0.55f, lineA) : new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, lineA);
                // 色の札は文字の色をまるごと置きかえるので、うすさ（lineA）も札に入れる
                _lvLyric.text = $"<color=#{Hex(col)}>{line.Substring(0, nChars)}</color><color=#{Hex(new Color(1, 1, 1, 0.55f * lineA))}>{line.Substring(nChars)}</color>";
                _lvLyric.color = new Color(1, 1, 1, lineA);
                float w = _lvLyric.preferredWidth;
                _lvLyricBg.rectTransform.anchoredPosition = new Vector2(LvLineW / 2 - w / 2 - 40, -(LvLineH - 72));
                _lvLyricBg.rectTransform.sizeDelta = new Vector2(w + 80, 64);
                _lvLyricBg.color = new Color(0.01f, 0.02f, 0.06f, 0.45f * lineA);
            }
            _lvLyric.enabled = _lvLyricBg.enabled = hasLine;

            // ヤチヨのことば（MC）：歌詞があるときは、そのすぐ上
            var talk = ls.Talk(out float ta);
            bool hasTalk = talk != null && ta > 0;
            _lvTalkBg.gameObject.SetActive(hasTalk);
            if (hasTalk)
            {
                _lvTalk.text = talk;
                float w = Mathf.Min(1100, _lvTalk.preferredWidth + 60);
                var tr = _lvTalkBg.rectTransform;
                tr.anchoredPosition = new Vector2(LvLineW / 2 - w / 2, -(hasLine ? LvLineH - 72 - 74 : LvLineH - 66));
                tr.sizeDelta = new Vector2(w, 58);
                _lvTalk.rectTransform.sizeDelta = new Vector2(w, 58);
                _lvTalkBg.color = new Color(0.02f, 0.03f, 0.1f, 0.72f * ta);
                _lvTalkBar.color = new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, ta);
                _lvTalkTag.color = new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.9f * ta);
                _lvTalkTagText.color = _lvTalk.color = new Color(1, 1, 1, ta);
            }
            _lvLine.Alpha = hasLine || hasTalk ? 1 : 0;
            float by = auto && screen ? 64 : 120;
            if (_lvLine.screenOffset.y != by) { _lvLine.screenOffset = new Vector2(0, by); _lvLine.Apply(); }

            // ペンライトとキーの案内（画面モードだけ。VR ではキーがないので出さない）
            _lvPen.Alpha = !auto && !Blocked && screen ? 1 : 0;
            if (_lvPen.Alpha > 0)
            {
                _lvPenDot.color = LiveShow.PenColors[LiveShow.PenColor];
                _lvPenText.text = $"ペンライト「{LiveShow.PenNames[LiveShow.PenColor]}」　1〜8 色　 C おまかせカメラ　 T 投げ銭（ステージに招待）";
            }
        }

        // ───────── 投げ銭 ─────────

        Board _tb;
        string _tbKey;
        const float TbW = 860, TbH = 580;

        void TipBoardTick()
        {
            bool open = _tipOpen && !Kassen.Busy && !_menu && !_map;
            if (!open) { if (_tb != null) _tb.Alpha = 0; _tbKey = null; return; }
            if (_tb == null)
            {
                _tb = Board.Create("投げ銭", new Vector2(TbW, TbH), 25);
                _tb.worldOffset = new Vector3(0, -0.05f, 1.2f);
                _tb.Apply();
            }
            _tb.Alpha = 1;
            string key = $"{_tipAmt}|{_tipMsg}|{_tipBusy}|{_tipErr}|{BalanceText()}";
            if (key == _tbKey) return;
            _tbKey = key;
            var r = _tb.root;
            Board.Clear(r);
            if (Board.Current == Board.Mode.Screen) { if (_tb.canvas.transform.childCount < 2) Board.Dim(r, TbW, TbH, 0.4f); }
            else Board.Dim(r, TbW, TbH);
            Board.Frame(r, new Rect(0, 0, TbW, TbH));
            Board.Label(r, new Rect(0, 22, TbW, 24), "SUPER FUJU", 18, Board.Cyan, TextAnchor.MiddleCenter, true);
            Board.Label(r, new Rect(0, 46, TbW, 50), "ヤチヨに投げ銭", 36, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Board.Label(r, new Rect(0, 98, TbW, 26), "ツクヨミのふじゅ〜から払います。コメントが空で光って流れ、ステージに招待されます", 18, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            // 金額
            float bw = 150, gap = 12, bx = (TbW - (bw * TipAmounts.Length + gap * (TipAmounts.Length - 1))) / 2, by = 140;
            for (int i = 0; i < TipAmounts.Length; i++)
            {
                int ii = i;
                var col = LiveShow.TipColor(TipAmounts[i]);
                var cell = Board.Pick(r, new Rect(bx + i * (bw + gap), by, bw, 64), i == _tipAmt, () => _tipAmt = ii, !_tipBusy);
                Board.Panel(cell, new Rect(0, 0, bw, 64), new Color(col.r, col.g, col.b, i == _tipAmt ? 0.85f : 0.25f));
                if (i == _tipAmt) Board.Outline(cell, new Rect(0, 0, bw, 64), Color.white, 2.5f);
                Board.Label(cell, new Rect(0, 0, bw, 64), $"{TipAmounts[i]:N0}", 26, i == _tipAmt ? new Color(0.05f, 0.05f, 0.1f) : Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            }
            Board.Label(r, new Rect(0, by + 70, TbW, 22), "ふじゅ〜", 16, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter);
            // ひとこと
            float mw = 250, mx = (TbW - (mw * 3 + gap * 2)) / 2, my = by + 110;
            for (int i = 0; i < TipMsgs.Length; i++)
            {
                int ii = i;
                var cell = Board.Pick(r, new Rect(mx + (i % 3) * (mw + gap), my + (i / 3) * 58, mw, 48), i == _tipMsg, () => _tipMsg = ii, !_tipBusy);
                Board.Label(cell, new Rect(0, 0, mw, 48), TipMsgs[i], 21, Color.white, TextAnchor.MiddleCenter);
            }
            Board.Label(r, new Rect(0, 400, TbW, 28), _tipErr ?? BalanceText(), 19, _tipErr != null ? Board.Pink : new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            if (_tipBusy) { Board.Label(r, new Rect(0, 470, TbW, 40), "支払っています…", 22, Board.Gold, TextAnchor.MiddleCenter); return; }
            Board.Btn(r, new Rect(90, TbH - 110, 330, 70), $"{TipAmounts[_tipAmt]:N0} ふじゅ〜 送る", 26, SendTip);
            Board.Btn(r, new Rect(TbW - 420, TbH - 110, 330, 70), "やめる（Esc）", 26, () => _tipOpen = false);
        }
    }
}
