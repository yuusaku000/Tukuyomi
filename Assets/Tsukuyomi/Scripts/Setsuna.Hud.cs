using UnityEngine;
using UnityEngine.UI;
using F = Tsukuyomi.SetsunaFighter;

namespace Tsukuyomi
{
    /// <summary>
    /// SETSUNA の表示（板）。上に体力（自分は青緑・相手は朱色）と丸い時計、横に縦書きの名札、
    /// 右にコンボ数（光る斜体の大きな数字）、下に超必殺のゲージ、まん中に「ROUND 1」「K.O.」「刹那!」。
    /// </summary>
    public partial class Setsuna
    {
        Board _hud, _pauseB, _resultB;
        RectTransform _hpP, _hpC, _hpPw, _hpCw, _gauge;
        Image _gaugeImg;
        Text _timer, _comboNumP, _comboHitP, _comboDmgTP, _comboNumC, _comboHitC, _center, _centerSub, _gaugeLabel, _resultTitle, _resultMsg;
        readonly Image[] _winP = new Image[2], _winC = new Image[2];
        float _centerT;
        const float BarW = 700;

        static bool Ascii(string s) { foreach (var ch in s) if (ch > 127) return false; return true; }

        /// <summary>光る大きな文字の中身を変える（日本語は游ゴシック、英字は Bahnschrift）。</summary>
        static void SetBig(Text t, string s)
        {
            t.font = Ascii(s) ? Board.DisplayFont : Ts.Font;
            t.text = s;
        }

        void BuildHud()
        {
            _hud = Board.Create("SETSUNA の表示", new Vector2(1920, 1080), 40);
            _hud.worldOffset = new Vector3(0, 0, 1.9f);
            _hud.Apply();
            var r = _hud.root;
            Board.Fans(r, new Rect(16, 16, 1888, 1048), new Color(1, 1, 1, 0.7f), 90);

            // 体力
            _hpP = Bar(r, new Rect(960 - 80 - BarW, 44, BarW, 26), Board.Teal, false, out _hpPw);
            _hpC = Bar(r, new Rect(960 + 80, 44, BarW, 26), Board.Vermilion, true, out _hpCw);
            for (int i = 0; i < 2; i++)
            {
                _winP[i] = Dot(r, new Vector2(960 - 100 - i * 30, 92));
                _winC[i] = Dot(r, new Vector2(960 + 100 + i * 30, 92));
            }
            // 時計
            Board.Panel(r, new Rect(960 - 62, 16, 124, 124), new Color(0, 0, 0, 0));
            var disc = Board.Panel(r, new Rect(960 - 60, 18, 120, 120), new Color(0.03f, 0.05f, 0.12f, 0.85f)); disc.sprite = Board.DiscSprite;
            Board.Ring(r, new Vector2(960, 78), 60, new Color(1, 1, 1, 0.9f), false);
            Board.Ring(r, new Vector2(960, 78), 50, new Color(Board.Teal.r, Board.Teal.g, Board.Teal.b, 0.8f));
            _timer = Board.Display(r, new Rect(900, 40, 120, 76), "99", 58, Color.white);
            // 名札
            Board.NameTag(r, new Rect(40, 120, 56, 200), "あなた", Board.Teal, false, 32);
            Board.NameTag(r, new Rect(1824, 120, 56, 160), C.style.name, Board.Vermilion, true, 32);
            Board.Label(r, new Rect(108, 120, 300, 26), "PLAYER", 16, new Color(1, 1, 1, 0.75f), TextAnchor.UpperLeft, true);
            Board.Label(r, new Rect(1512, 120, 300, 26), Kassen.Levels[_level], 16, new Color(1, 1, 1, 0.75f), TextAnchor.UpperRight);

            // コンボ
            _comboNumP = Board.Display(r, new Rect(1180, 300, 300, 170), "", 150, Board.Teal, TextAnchor.LowerRight);
            _comboHitP = Board.Display(r, new Rect(1488, 380, 200, 80), "HIT", 56, Color.white, TextAnchor.LowerLeft);
            _comboDmgTP = Board.Label(r, new Rect(1200, 470, 480, 34), "", 26, Color.white, TextAnchor.MiddleRight);
            _comboNumC = Board.Display(r, new Rect(260, 320, 260, 140), "", 120, Board.Vermilion, TextAnchor.LowerRight);
            _comboHitC = Board.Display(r, new Rect(526, 386, 180, 70), "HIT", 46, Color.white, TextAnchor.LowerLeft);

            // まん中の文字
            _center = Board.Display(r, new Rect(0, 360, 1920, 220), "", 170, Board.Teal);
            _centerSub = Board.Label(r, new Rect(0, 578, 1920, 50), "", 34, Color.white, TextAnchor.MiddleCenter);

            // 超必殺ゲージ
            Board.Panel(r, new Rect(660, 996, 600, 18), new Color(0.02f, 0.08f, 0.12f, 0.85f));
            _gaugeImg = Board.Panel(r, new Rect(662, 998, 596, 14), Board.Gold);
            _gauge = (RectTransform)_gaugeImg.transform;
            Board.Panel(r, new Rect(660, 996, 600, 1), new Color(1, 1, 1, 0.8f));
            _gaugeLabel = Board.Label(r, new Rect(660, 1018, 600, 30), "超必殺", 20, Color.white, TextAnchor.MiddleCenter);
            // 操作の説明
            Board.Label(r, new Rect(60, 980, 560, 70), Vr.On ? VrKeys :
                "W/S 前後（S 押したまま＝ガード）　A/D 回りこむ　Space ジャンプ　Ctrl しゃがみ\n左クリック 弱　右クリック 強　E 必殺（S→W＋E 突進・W→S＋E 狐火）　F 投げ　R 超必殺",
                15, new Color(1, 1, 1, 0.75f), TextAnchor.LowerLeft);
            Board.Label(r, new Rect(1400, 1010, 460, 30), Vr.On ? "Y 一時停止" : "Esc 一時停止", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleRight);
            _hud.FitToScreen(); // 画面の縦横の比に合わせて、表示を画面のふちに寄せる
        }

        RectTransform Bar(RectTransform r, Rect rc, Color c, bool rightSide, out RectTransform white)
        {
            Board.Panel(r, new Rect(rc.x - 3, rc.y - 3, rc.width + 6, rc.height + 6), new Color(0.02f, 0.05f, 0.1f, 0.85f));
            var w = Board.Panel(r, rc, new Color(1, 1, 1, 0.85f));
            var f = Board.Panel(r, rc, c);
            Board.Panel(r, new Rect(rc.x - 3, rc.y - 3, rc.width + 6, 1.2f), new Color(1, 1, 1, 0.8f));
            Board.Panel(r, new Rect(rc.x - 3, rc.yMax + 2, rc.width + 6, 1.2f), new Color(1, 1, 1, 0.8f));
            Board.Fan(r, new Rect(rc.x - 14, rc.y - 12, rc.width + 28, rc.height + 24), rightSide ? 1 : 0, new Color(1, 1, 1, 0.6f), 26, 5);
            white = (RectTransform)w.transform; var fill = (RectTransform)f.transform;
            if (!rightSide)
            {
                // 自分の体力は外側（左）に寄せて、まん中側から減る
                foreach (var t in new[] { white, fill }) { t.pivot = new Vector2(0, 1); }
            }
            else
            {
                foreach (var t in new[] { white, fill }) { t.pivot = new Vector2(1, 1); t.anchoredPosition = new Vector2(rc.xMax, -rc.y); }
            }
            return fill;
        }

        Image Dot(RectTransform r, Vector2 c)
        {
            var i = Board.Panel(r, new Rect(c.x - 9, c.y - 9, 18, 18), new Color(1, 1, 1, 0.25f));
            i.sprite = Board.DiscSprite;
            Board.Ring(r, c, 12, new Color(1, 1, 1, 0.8f), false);
            return i;
        }

        void Center(string text, Color c, float sec, string sub)
        {
            if (_center == null) return;
            SetBig(_center, text);
            _center.color = c;
            foreach (var o in _center.GetComponents<Outline>()) o.effectColor = new Color(c.r, c.g, c.b, o.effectColor.a);
            _centerSub.text = sub;
            _centerT = sec;
        }

        void HudTick()
        {
            if (_hud == null) return;
            float udt = Time.unscaledDeltaTime;
            P.shownHp = Mathf.MoveTowards(P.shownHp, P.hp, udt * 400f * (P.st == F.St.Hitstun || P.st == F.St.Juggle ? 0.2f : 1f));
            C.shownHp = Mathf.MoveTowards(C.shownHp, C.hp, udt * 400f * (C.st == F.St.Hitstun || C.st == F.St.Juggle ? 0.2f : 1f));
            _hpP.sizeDelta = new Vector2(BarW * P.hp / P.style.hp, _hpP.sizeDelta.y);
            _hpPw.sizeDelta = new Vector2(BarW * P.shownHp / P.style.hp, _hpPw.sizeDelta.y);
            _hpC.sizeDelta = new Vector2(BarW * C.hp / C.style.hp, _hpC.sizeDelta.y);
            _hpCw.sizeDelta = new Vector2(BarW * C.shownHp / C.style.hp, _hpCw.sizeDelta.y);
            for (int i = 0; i < 2; i++)
            {
                _winP[i].color = P.wins > i ? Board.Teal : new Color(1, 1, 1, 0.2f);
                _winC[i].color = C.wins > i ? Board.Vermilion : new Color(1, 1, 1, 0.2f);
            }
            _timer.text = Mathf.CeilToInt(_clock).ToString();
            _gauge.sizeDelta = new Vector2(596 * P.gauge / 100f, _gauge.sizeDelta.y);
            bool full = P.gauge >= 100;
            _gaugeImg.color = full ? Color.Lerp(Board.Gold, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f)) : Board.Gold;
            _gaugeLabel.text = full ? "超必殺　R で使える！" : $"超必殺　{Mathf.FloorToInt(P.gauge)}%";

            _comboShowP -= udt; _comboShowC -= udt;
            bool showP = _comboP >= 2 && _comboShowP > 0, showC = _comboC >= 2 && _comboShowC > 0;
            _comboNumP.text = showP ? _comboP.ToString() : ""; _comboHitP.text = showP ? "HIT" : "";
            _comboDmgTP.text = showP ? $"{_comboDmgP:N0} ダメージ" : "";
            if (showP) _comboNumP.fontSize = 130 + Mathf.Min(60, _comboP * 4);
            _comboNumC.text = showC ? _comboC.ToString() : ""; _comboHitC.text = showC ? "HIT" : "";

            _centerT -= udt;
            float a = Mathf.Clamp01(_centerT / 0.3f);
            _center.canvasRenderer.SetAlpha(a); _centerSub.canvasRenderer.SetAlpha(a);
        }

        // ───────── 一時停止と結果 ─────────

        void Pause()
        {
            _paused = true;
            Time.timeScale = 0f;
            Lock(false);
            if (_pauseB == null)
            {
                _pauseB = Board.Create("一時停止", new Vector2(720, 420), 50);
                _pauseB.worldOffset = new Vector3(0, 0, 1.2f);
                _pauseB.Apply();
                var r = _pauseB.root;
                Board.Frame(r, new Rect(0, 0, 720, 420));
                Board.Display(r, new Rect(0, 40, 720, 90), "PAUSE", 76, Board.Teal);
                Board.Label(r, new Rect(0, 130, 720, 34), "一時停止中", 24, Color.white, TextAnchor.MiddleCenter);
                Board.ButtonNode(r, new Rect(210, 200, 300, 64), $"つづける（{Pad.OkKey}・{Pad.BackKey}）", 26, Resume);
                Board.ButtonNode(r, new Rect(160, 285, 400, 56), "降参して島へもどる\n<size=15>" + (Vr.On ? "Y" : "Esc") + " を長押しでも</size>", 22, Surrender);
            }
            _pauseB.Alpha = 1;
        }

        /// <summary>降参して島へもどる。一時停止の板のボタンか、Y・Esc の長押しで。</summary>
        void Surrender() { if (!Ts.ShotMode) Kassen.Reward(false, _level); Leave(); }

        float _quitHold;

        /// <summary>Y（VR）・Esc を 1.5 秒長押ししたら、降参して島へもどる（2026-11-18 ユーザーの指示）。もどったら true。</summary>
        bool QuitHoldTick()
        {
            if (Ts.ShotMode || _phase == Phase.Result) return false;
            if (!Act.Back.Held) { _quitHold = 0; return false; }
            _quitHold += Time.unscaledDeltaTime;
            if (_quitHold > 0.4f && _quitHold < 1.5f) Center("降参する…", Board.Vermilion, 0.2f, $"{(Vr.On ? "Y" : "Esc")} をそのまま押しつづける（{1.5f - _quitHold:0.0} 秒）");
            if (_quitHold < 1.5f) return false;
            _quitHold = -999f;
            if (_paused) Resume();
            Surrender();
            return true;
        }

        void Resume()
        {
            _paused = false;
            Time.timeScale = _slowT > 0 ? 0.3f : 1f;
            if (_pauseB != null) _pauseB.Alpha = 0;
        }

        void ShowResult(bool win)
        {
            SetPhase(Phase.Result);
            Time.timeScale = 1f;
            string got = Ts.ShotMode ? (win ? $"＋{Kassen.Rewards[_level]:N0} ふじゅ〜" : "") : Kassen.Reward(win, _level);
            ResultBoard(win, got);
        }

        void ResultBoard(bool win, string got)
        {
            Lock(false);
            if (_resultB == null)
            {
                const float w = 960, h = 560;
                _resultB = Board.Create("結果", new Vector2(w, h), 50);
                _resultB.worldOffset = new Vector3(0, 0, 1.3f);
                _resultB.Apply();
                var r = _resultB.root;
                Board.Frame(r, new Rect(0, 0, w, h));
                _resultTitle = Board.Display(r, new Rect(0, 40, w, 150), "", 130, Board.Teal);
                _resultMsg = Board.Label(r, new Rect(60, 210, w - 120, 150), "", 28, Color.white, TextAnchor.MiddleCenter);
                Board.NameTag(r, new Rect(w - 110, 60, 50, 200), C.style.name, Board.Vermilion, true, 28);
                _rAgain = Board.ButtonNode(r, new Rect(w / 2 - 330, h - 130, 300, 70), "もう一度", 28, Rematch);
                _rLeave = Board.ButtonNode(r, new Rect(w / 2 + 30, h - 130, 300, 70), "島へもどる", 28, LeaveResult);
            }
            SetBig(_resultTitle, win ? "勝利" : "敗北");
            _resultTitle.color = win ? Board.Teal : Board.Vermilion;
            _resultMsg.text = $"{C.style.name}（{Kassen.Levels[_level]}）に {P.wins} − {C.wins}\n{got}";
            _resultB.Alpha = 1;
            _lastWin = win; _lastGot = got; _resultShownAt = Time.unscaledTime;
            Pad.SetLabel(_rAgain, Pad.Label("もう一度", Pad.OkKey));
            Pad.SetLabel(_rLeave, Pad.Label("島へもどる", Pad.BackKey));
        }

        UnityEngine.UI.Button _rAgain, _rLeave;
        bool _lastWin; string _lastGot = ""; float _resultShownAt;

        void LeaveResult()
        {
            Hud.Toast(_lastWin ? $"SETSUNA に勝ちました　{_lastGot.Split('\n')[0]}" : "SETSUNA：また挑戦してね");
            Leave();
        }

        /// <summary>結果の板：A（Enter）もう一度・Y（Esc）島へもどる。出てすぐ（1秒）は、必殺のボタンを押しつづけていても進まないように待つ。</summary>
        bool ResultKeys()
        {
            if (_phase != Phase.Result || _resultB == null || _resultB.Alpha <= 0 || Ts.ShotMode) return false;
            if (Time.unscaledTime - _resultShownAt < 1f) return false;
            if (Pad.Ok) { Rematch(); return true; }
            if (Pad.Back) { LeaveResult(); return true; }
            return false;
        }

        void Rematch()
        {
            if (_resultB != null) _resultB.Alpha = 0;
            P.wins = C.wins = 0; P.gauge = C.gauge = 0; _round = 0;
            NextRound();
        }

        // ───────── 確認用（--tour の setsuna:相手:強さ:場面）─────────

        public static void TestPose(string pose)
        {
            var s = I;
            if (s == null) return;
            switch (pose)
            {
                case "fight":
                    s.SetPhase(Phase.Fight); s._clock = 87; s._centerT = 0;
                    break;
                case "vrswing": // 確認用（--vr-sim）：右手を振りつづけて、振る攻撃が出るか見る
                    s.SetPhase(Phase.Fight); s._clock = 87; s._centerT = 0; s._testPlay = true; Vr.SimSwing = true;
                    break;
                case "combo":
                    s.SetPhase(Phase.Fight); s._clock = 74; s._centerT = 0;
                    s.C.pos = new Vector3(0, 0, -1.0f); s.C.hp = 560; s.C.st = F.St.Hitstun; s.C.stun = 0.3f;
                    s.P.StartMove(SetsunaMoves.Heavy); s.P.t = SetsunaMoves.Heavy.startup + 0.02f; s.P.moveHit = true; s.P.gauge = 100;
                    s._comboP = 12; s._comboDmgP = 1240; s._comboShowP = 99;
                    s.P.wins = 1;
                    s._frozen = true;
                    break;
                case "just":
                    s.SetPhase(Phase.Fight); s._clock = 52;
                    s.C.pos = new Vector3(0, 0, -1.2f); s.C.StartMove(SetsunaMoves.Heavy); s.C.t = SetsunaMoves.Heavy.startup + 0.03f;
                    s.P.st = F.St.Blockstun; s.P.stun = 1; s.P.guard = true;
                    s.Center("刹那!", Board.Gold, 99, "刹那ガード ― 反撃のチャンス");
                    s._frozen = true;
                    break;
                case "ko":
                    s.SetPhase(Phase.RoundEnd); s._clock = 31;
                    s.C.pos = new Vector3(0, 0, 0.2f); s.C.hp = 0; s.C.st = F.St.KO; s.P.wins = 1;
                    s.Center("K.O.", Board.Vermilion, 99, "あなたの勝ち");
                    s._frozen = true;
                    break;
                case "result":
                    s.P.wins = 2; s.C.wins = 1;
                    s.SetPhase(Phase.Result);
                    s.ResultBoard(true, $"＋{Kassen.Rewards[s._level]:N0} ふじゅ〜" + (s._level == 2 ? "\n「狐のきぐるみ」を手に入れました！（C の着がえで着られます）" : ""));
                    s._frozen = true;
                    break;
            }
        }
    }
}
