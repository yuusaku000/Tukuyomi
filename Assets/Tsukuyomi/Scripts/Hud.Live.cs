using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>ライブの画面：開演の数字・曲名・字幕（歌詞）・ヤチヨのことば・コメント欄・投げ銭・キーの案内。</summary>
    public partial class Hud
    {
        static bool _tipOpen, _tipBusy;
        static int _tipAmt = 1, _tipMsg;
        static string _tipErr;
        static readonly int[] TipAmounts = { 100, 300, 500, 1000, 3000 };
        static readonly string[] TipMsgs = { "最高〜！", "ヤチヨ大好き！", "新曲すき！", "勉強がんばるね", "また来るね！", "888888" };

        public static bool TipOpen => _tipOpen;

        /// <summary>確認用（--tour の live:秒:ask）。</summary>
        public static void TestTipAsk() { _tipOpen = true; _tipBusy = false; _tipErr = null; _i?.RefreshBalance(); }

        /// <summary>ライブの間のキー。C を使ったら true（着がえは開かない）。</summary>
        bool LiveKeys()
        {
            if (!LiveShow.Running || _menu || _map || ShopOpen || _book || _talk || _shipAsk != null) return false;
            bool here = LiveShow.PlayerInVenue || LiveShow.AutoCam;
            if (!here) return false;
            if (_tipOpen) return Input.GetKeyDown(KeyCode.C);
            for (int i = 0; i < LiveShow.PenColors.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    LiveShow.SetPen(i);
                    Toast($"ペンライトを「{LiveShow.PenNames[i]}」にしました");
                }
            if (Input.GetKeyDown(KeyCode.T)) { TestTipAsk(); return false; }
            if (Input.GetKeyDown(KeyCode.C)) { LiveShow.SetAutoCam(!LiveShow.AutoCam); return true; }
            return false;
        }

        void DrawLive()
        {
            var ls = LiveShow.I;
            if (ls == null) return;
            float t = Time.unscaledTime;
            bool auto = LiveShow.AutoCam;
            bool here = LiveShow.PlayerInVenue || auto;

            // おまかせカメラ：映画のような上下の黒い帯
            if (auto)
            {
                Fill(new Rect(0, 0, W, 64), new Color(0, 0, 0, 0.85f));
                Fill(new Rect(0, H - 64, W, 64), new Color(0, 0, 0, 0.85f));
                Text(new Rect(30, H - 52, 600, 40), "● LIVE　おまかせカメラ（C・Esc でもどる）", 17, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
            }
            if (!here)
            {
                // 会場の外：小さな案内だけ
                Text(new Rect(W - 560, 108, 520, 26), "♪ ヤチヨのライブ中（ARライブステージ）", 18, new Color(Pink.r, Pink.g, Pink.b, 0.85f), TextAnchor.MiddleRight);
                return;
            }

            var phase = LiveShow.Now;
            // 開演前
            if (phase == LiveShow.Phase.Opening)
            {
                float pt = ls.PhaseTime;
                Text(new Rect(0, 150, W, 40), "YACHIYO LIVE ▸ TSUKUYOMI", 22, Cyan, TextAnchor.MiddleCenter, true);
                Text(new Rect(0, 190, W, 70), "まもなく開演", 52, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                float cd = ls.CountDown;
                if (cd > 0 && cd <= 3.2f)
                {
                    int n = Mathf.CeilToInt(cd - 0.2f);
                    float f = cd - Mathf.Floor(cd);
                    if (n >= 1) Text(new Rect(0, H / 2 - 160, W, 260), n.ToString(), (int)(170 + f * 40), new Color(1, 1, 1, Mathf.Clamp01(f * 2f)), TextAnchor.MiddleCenter, true, FontStyle.Bold);
                }
                else if (cd < 0) Text(new Rect(0, 268, W, 36), "（曲を準備しています…）", 20, new Color(1, 1, 1, 0.6f + 0.3f * Mathf.Sin(t * 3f)), TextAnchor.MiddleCenter);
            }

            // 曲名（曲のはじめに大きく）
            var song = ls.Song;
            if (song != null)
            {
                float st = ls.SongTime;
                float a = Mathf.Clamp01((st - 1f) / 0.6f) * Mathf.Clamp01((9f - st) / 1f);
                if (song.style == 1) a = Mathf.Clamp01((st - 1f) / 1f) * Mathf.Clamp01((10f - st) / 1.5f);
                if (a > 0)
                {
                    float x = 60, y = auto ? 100 : 150;
                    Text(new Rect(x, y, 800, 24), (LiveShow.Now == LiveShow.Phase.Song1 ? "M1 ▸ " : "M2 ▸ ") + song.en, 18, new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.UpperLeft, true);
                    Text(new Rect(x, y + 24, 900, 70), "♪ " + song.title, 50, new Color(1, 1, 1, a), TextAnchor.UpperLeft, false, FontStyle.Bold);
                    Text(new Rect(x + 6, y + 92, 800, 28), "作詞・作曲　ヤチヨ", 18, new Color(Gold.r, Gold.g, Gold.b, a * 0.9f), TextAnchor.UpperLeft);
                    Fill(new Rect(x, y + 124, 300 * a, 2), new Color(Pink.r, Pink.g, Pink.b, a));
                }
                // 字幕（歌詞）：行のうち、歌った所までを色づける
                var line = song.Lyric(st, out float prog, out float la);
                if (line != null && la > 0)
                {
                    float y = H - (auto ? 170 : 300);
                    int size = 40;
                    var full = new GUIContent(line);
                    _label.font = Ts.Font; _label.fontSize = size; _label.fontStyle = FontStyle.Bold;
                    float w = _label.CalcSize(full).x;
                    float x0 = W / 2 - w / 2;
                    Fill(new Rect(x0 - 40, y - 8, w + 80, 64), new Color(0.01f, 0.02f, 0.06f, 0.45f * la));
                    // 下の行と色づけた行を、同じ左はしから描く（ずれないように）
                    Text(new Rect(x0, y, w + 20, 50), line, size, new Color(1, 1, 1, 0.55f * la), TextAnchor.MiddleLeft, false, FontStyle.Bold, false);
                    int nChars = Mathf.Clamp(Mathf.RoundToInt(line.Length * prog), 0, line.Length);
                    if (nChars > 0)
                    {
                        var col = song.style == 1 ? new Color(1f, 0.86f, 0.55f, la) : new Color(Pink.r, Pink.g, Pink.b, la);
                        // 左そろえで、歌った所までを重ねて描く
                        float part = _label.CalcSize(new GUIContent(line.Substring(0, nChars))).x;
                        GUI.BeginGroup(new Rect(x0, y, part + 1, 50));
                        Text(new Rect(0, 0, w + 20, 50), line, size, col, TextAnchor.MiddleLeft, false, FontStyle.Bold, false);
                        GUI.EndGroup();
                    }
                }
            }

            // ヤチヨのことば（MC・お礼）
            var talk = ls.Talk(out float ta);
            if (talk != null && ta > 0)
            {
                float y = H - (auto ? 250 : 390);
                if (song != null && song.Lyric(ls.SongTime, out _, out float la2) != null && la2 > 0) y -= 70;
                _label.font = Ts.Font; _label.fontSize = 28;
                float w = Mathf.Min(1100, _label.CalcSize(new GUIContent(talk)).x + 60);
                var r = new Rect(W / 2 - w / 2, y, w, 58);
                Fill(r, new Color(0.02f, 0.03f, 0.1f, 0.72f * ta));
                Fill(new Rect(r.x, r.y, 5, r.height), new Color(Pink.r, Pink.g, Pink.b, ta));
                var tag = new Rect(r.x + 14, r.y - 24, 110, 28);
                Fill(tag, new Color(Pink.r, Pink.g, Pink.b, 0.9f * ta));
                Text(tag, "ヤチヨ", 18, new Color(1, 1, 1, ta), TextAnchor.MiddleCenter, false, FontStyle.Bold);
                Text(r, talk, 28, new Color(1, 1, 1, ta), TextAnchor.MiddleCenter);
            }

            // コメント欄（右）
            DrawComments(auto);

            // キーの案内とペンライトの色
            if (!auto && !Blocked)
            {
                var pc = LiveShow.PenColors[LiveShow.PenColor];
                var kr = new Rect(W - 640, H - 112, 600, 30);
                Fill(new Rect(kr.x - 8, kr.y - 4, kr.width + 8, kr.height + 8), new Color(0.02f, 0.05f, 0.12f, 0.45f));
                Fill(new Rect(kr.x, kr.y + 7, 16, 16), pc);
                Text(new Rect(kr.x + 24, kr.y, 600, 30), $"ペンライト「{LiveShow.PenNames[LiveShow.PenColor]}」　1〜8 色　 C おまかせカメラ　 T 投げ銭", 18, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft);
            }
        }

        void DrawComments(bool auto)
        {
            var list = LiveShow.Comments;
            float w = 400, x = W - w - 36, bottom = H - (auto ? 90 : 140), y = bottom;
            float top = auto ? 90 : 150;
            if (!auto)
            {
                Fill(new Rect(x - 10, top - 34, w + 20, bottom - top + 44), new Color(0.01f, 0.03f, 0.08f, 0.32f));
                Text(new Rect(x, top - 30, w, 24), "LIVE CHAT ▸ コメント", 15, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f), TextAnchor.MiddleLeft, true);
            }
            float now = Time.unscaledTime;
            for (int i = list.Count - 1; i >= 0 && y > top; i--)
            {
                var c = list[i];
                float age = now - c.t;
                float a = Mathf.Clamp01(age / 0.25f) * (auto ? Mathf.Clamp01((14f - age) / 2f) : 1f);
                if (a <= 0) continue;
                if (c.amount > 0)
                {
                    // 投げ銭：色つきの箱で光る
                    float h = 66;
                    y -= h + 6;
                    if (y < top) break;
                    var col = LiveShow.TipColor(c.amount);
                    float glow = c.mine ? 0.5f + 0.5f * Mathf.Sin(now * 5f) : 0;
                    var r = new Rect(x, y, w, h);
                    Fill(r, new Color(col.r * 0.55f, col.g * 0.55f, col.b * 0.55f, 0.92f * a));
                    Fill(new Rect(x, y, w, 28), new Color(col.r, col.g, col.b, 0.95f * a));
                    if (c.mine) Frame(new Rect(x - 3, y - 3, w + 6, h + 6), new Color(1, 1, 1, (0.5f + 0.5f * glow) * a), 2.5f);
                    Text(new Rect(x + 10, y + 2, w - 20, 24), c.name, 16, new Color(0.05f, 0.05f, 0.1f, a), TextAnchor.MiddleLeft, false, FontStyle.Bold, false);
                    Text(new Rect(x + 10, y + 2, w - 20, 24), $"{c.amount:N0} ふじゅ〜", 17, new Color(0.05f, 0.05f, 0.1f, a), TextAnchor.MiddleRight, true, FontStyle.Bold, false);
                    Text(new Rect(x + 10, y + 32, w - 20, 30), c.text, 20, new Color(1, 1, 1, a), TextAnchor.MiddleLeft);
                }
                else
                {
                    y -= 30;
                    Text(new Rect(x, y, 150, 28), c.name, 15, new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f * a), TextAnchor.MiddleLeft);
                    Text(new Rect(x + 150, y, w - 150, 28), c.text, 18, new Color(1, 1, 1, 0.9f * a), TextAnchor.MiddleLeft);
                }
            }
        }

        // ───────── 投げ銭 ─────────

        void DrawTip()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.03f, 0.08f, 0.4f));
            var r = new Rect(W / 2 - 430, H / 2 - 290, 860, 580);
            Fill(r, new Color(0.01f, 0.03f, 0.08f, 0.93f));
            Panel(r);
            Text(new Rect(r.x, r.y + 20, r.width, 22), "SUPER FUJU", 16, Cyan, TextAnchor.MiddleCenter, true);
            Text(new Rect(r.x, r.y + 46, r.width, 50), "ヤチヨに投げ銭", 38, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Text(new Rect(r.x, r.y + 98, r.width, 26), "ミニヤチヨの本物のふじゅ〜から払います。コメントが光って、ヤチヨがお礼を言います", 18, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            // 金額
            float bw = 150, gap = 12, bx = r.x + (r.width - (bw * TipAmounts.Length + gap * (TipAmounts.Length - 1))) / 2, by = r.y + 140;
            for (int i = 0; i < TipAmounts.Length; i++)
            {
                var br = new Rect(bx + i * (bw + gap), by, bw, 64);
                var col = LiveShow.TipColor(TipAmounts[i]);
                Fill(br, new Color(col.r, col.g, col.b, i == _tipAmt ? 0.85f : 0.25f));
                if (i == _tipAmt) Frame(br, Color.white, 2.5f);
                Text(br, $"{TipAmounts[i]:N0}", 26, i == _tipAmt ? new Color(0.05f, 0.05f, 0.1f) : Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold, false);
                if (!_tipBusy && GUI.Button(br, GUIContent.none, GUIStyle.none)) { _tipAmt = i; Sfx.Click(); }
            }
            Text(new Rect(r.x, by + 70, r.width, 22), "ふじゅ〜", 16, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter);
            // ひとこと
            float mw = 250, mx = r.x + (r.width - (mw * 3 + gap * 2)) / 2, my = by + 110;
            for (int i = 0; i < TipMsgs.Length; i++)
            {
                var mr = new Rect(mx + (i % 3) * (mw + gap), my + (i / 3) * 58, mw, 48);
                Fill(mr, new Color(Cyan.r, Cyan.g, Cyan.b, i == _tipMsg ? 0.35f : 0.08f));
                Frame(mr, new Color(Cyan.r, Cyan.g, Cyan.b, i == _tipMsg ? 0.95f : 0.35f), i == _tipMsg ? 2f : 1f);
                Text(mr, TipMsgs[i], 21, Color.white, TextAnchor.MiddleCenter);
                if (!_tipBusy && GUI.Button(mr, GUIContent.none, GUIStyle.none)) { _tipMsg = i; Sfx.Click(); }
            }
            Text(new Rect(r.x, r.y + 400, r.width, 28), _tipErr ?? BalanceText(), 19, _tipErr != null ? Pink : new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            if (_tipBusy) { Text(new Rect(r.x, r.y + 470, r.width, 40), "ミニヤチヨにふじゅ〜を送っています…", 22, Gold, TextAnchor.MiddleCenter); return; }
            if (Button(new Rect(r.x + 90, r.yMax - 110, 330, 70), $"{TipAmounts[_tipAmt]:N0} ふじゅ〜 送る")) SendTip();
            if (Button(new Rect(r.xMax - 420, r.yMax - 110, 330, 70), "やめる（Esc）")) _tipOpen = false;
        }

        void SendTip()
        {
            int amt = TipAmounts[_tipAmt];
            string msg = TipMsgs[_tipMsg];
            _tipBusy = true; _tipErr = null;
            var item = new Item { id = "live_tip", name = "ヤチヨのライブに投げ銭", price = amt, kind = ItemKind.Food };
            StartCoroutine(Fuju.Buy(item, res =>
            {
                _tipBusy = false;
                if (!res.ok) { _tipErr = string.IsNullOrEmpty(res.error) ? "送れませんでした" : res.error; return; }
                _tipOpen = false;
                Sfx.Coin();
                LiveShow.I?.Tipped(amt, msg, true);
                Toast($"{amt:N0} ふじゅ〜を投げ銭しました　残り {Catalog.Yen(res.fuju)} ふじゅ〜");
            }));
        }
    }
}
