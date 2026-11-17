using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 魚の図鑑と週の大物番付（B）、遊覧船の切符の確認の板（Board）。もとは IMGUI で描いていたもの。
    /// 中身は、タブ・えらんでいる魚・残高などが変わったときだけ作り直す。
    /// </summary>
    public partial class Hud
    {
        Board _bk, _sa;
        string _bkKey, _saKey;

        const float BkW = 1320, BkH = 940;

        // ───────── 図鑑と番付 ─────────

        void BookBoardTick()
        {
            bool open = _book && !_map && !_menu && !Kassen.Busy;
            if (!open) { if (_bk != null) _bk.Alpha = 0; _bkKey = null; return; }
            if (_bk == null)
            {
                _bk = Board.Create("図鑑", new Vector2(BkW, BkH), 25);
                _bk.worldOffset = new Vector3(0, -0.05f, 1.35f);
                _bk.Apply();
            }
            _bk.Alpha = 1;
            string key = $"{_bookTab}|{_bookSel}|{FishBook.Total}|{FishBook.Known}|{Ts.Now:yyyyMMdd}";
            if (key == _bkKey) return;
            _bkKey = key;
            var r = _bk.root;
            Board.Clear(r);
            if (Board.Current == Board.Mode.Screen)
            {
                // 画面の暗がりはキャンバスに付くので、1回だけ作る
                if (_bk.canvas.transform.childCount < 2) Board.Dim(r, BkW, BkH, 0.55f);
            }
            else Board.Dim(r, BkW, BkH);
            Board.Frame(r, new Rect(0, 0, BkW, BkH));
            Board.Label(r, new Rect(40, 26, 400, 24), _bookTab == 0 ? "FISH BOOK" : "WEEKLY BIG FISH", 18, Board.Cyan, TextAnchor.MiddleLeft, true);
            Board.Label(r, new Rect(38, 50, 600, 52), _bookTab == 0 ? "魚の図鑑" : "今週の大物番付", 38, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            Board.Panel(r, new Rect(40, 108, 220, 2), Board.Vermilion);
            var t0 = Board.Pick(r, new Rect(BkW - 480, 34, 210, 52), _bookTab == 0, () => { _bookTab = 0; _bookSel = -1; });
            Board.Label(t0, new Rect(0, 0, 210, 52), "図鑑", 22, Color.white, TextAnchor.MiddleCenter);
            var t1 = Board.Pick(r, new Rect(BkW - 258, 34, 210, 52), _bookTab == 1, () => _bookTab = 1);
            Board.Label(t1, new Rect(0, 0, 210, 52), "番付", 22, Color.white, TextAnchor.MiddleCenter);
            var d = GameSession.Instance != null ? GameSession.Instance.Data : null;
            if (_bookTab == 0) BuildFishGrid(r, d);
            else BuildRanking(r, d);
            Board.Btn(r, new Rect(BkW - 270, BkH - 88, 230, 58), "とじる（B / Esc）", 22, () => _book = false);
        }

        void BuildFishGrid(RectTransform r, SaveData d)
        {
            Board.Label(r, new Rect(290, 58, 500, 34), $"{FishBook.Known} / {FishBook.All.Length} 種　　釣った数 {FishBook.Total} 匹", 22, Board.Gold);
            const int cols = 5;
            float cw = (BkW - 80 - (cols - 1) * 14) / cols, ch = 150;
            for (int n = 0; n < FishBook.All.Length; n++)
            {
                var k = FishBook.All[n];
                var rec = FishBook.Rec(k.id);
                bool known = rec != null;
                int nn = n;
                var cell = Board.Pick(r, new Rect(40 + (n % cols) * (cw + 14), 124 + (n / cols) * (ch + 12), cw, ch), n == _bookSel, () => _bookSel = nn);
                if (k.rank > 0) Board.Panel(cell, new Rect(cw - 4, 0, 4, ch), k.rank == 2 ? Board.Gold : Board.Pink);
                Board.Label(cell, new Rect(12, 4, 80, 20), $"No.{n + 1:00}", 13, new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.7f), TextAnchor.MiddleLeft, true);
                Board.Label(cell, new Rect(0, 4, cw - 12, 20), k.Stars, 14, Board.Gold, TextAnchor.MiddleRight);
                Board.Raw(cell, new Rect(cw / 2 - 70, 22, 140, 70)).texture = FishBook.Icon(k, known);
                Board.Label(cell, new Rect(0, 92, cw, 30), known ? k.name : "？？？", 22, known ? Color.white : new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
                Board.Label(cell, new Rect(0, 120, cw, 24), known ? $"最大 {rec.best:0.0}cm・{rec.count}匹" : "まだ釣っていない", 16, known ? Board.Gold : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
            }
            // 説明
            var dr = new Rect(40, BkH - 172, BkW - 340, 130);
            if (_bookSel >= 0)
            {
                var k = FishBook.All[_bookSel];
                var rec = FishBook.Rec(k.id);
                if (rec != null)
                {
                    Board.Label(r, new Rect(dr.x, dr.y, dr.width, 36), $"{k.name}　{k.Stars} {k.RankName}　（{k.min:0}〜{k.max:0}cm）", 26, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
                    Board.Wrap(r, new Rect(dr.x, dr.y + 40, dr.width, 56), k.desc, 21, new Color(1, 1, 1, 0.85f));
                    Board.Label(r, new Rect(dr.x, dr.y + 98, dr.width, 30), $"はじめて釣った日 {rec.first}　　{k.TimeHint}", 18, new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.85f));
                }
                else
                {
                    Board.Label(r, new Rect(dr.x, dr.y, dr.width, 36), $"？？？　{k.Stars} {k.RankName}", 26, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft, false, FontStyle.Bold);
                    Board.Label(r, new Rect(dr.x, dr.y + 44, dr.width, 30), "ヒント：" + k.TimeHint, 21, Board.Gold);
                }
            }
            else
            {
                float rest = d != null ? d.restMinutes : 0f;
                Board.Wrap(r, new Rect(dr.x, dr.y + 10, dr.width, 60), "魚をえらぶと説明が出ます。まだの魚は、えらぶと釣れる時間のヒントが出ます", 20, new Color(1, 1, 1, 0.75f));
                Board.Label(r, new Rect(dr.x, dr.y + 74, dr.width, 30), $"釣った魚は図鑑と番付に残して、池にかえします　　座って休んだ時間 {rest:0} 分", 18, new Color(1, 1, 1, 0.6f));
            }
        }

        void BuildRanking(RectTransform r, SaveData d)
        {
            var mon = FishBook.WeekStart(Ts.Now);
            Board.Label(r, new Rect(390, 58, 500, 34), $"{mon:M/d}（月）〜 {mon.AddDays(6):M/d}（日）", 22, Board.Gold);
            var list = FishBook.ThisWeek();
            int me = list.FindIndex(e => e.player);
            float y = 128;
            var head = new Color(1, 1, 1, 0.6f);
            Board.Label(r, new Rect(70, y, 100, 28), "順位", 17, head);
            Board.Label(r, new Rect(190, y, 400, 28), "釣り人", 17, head);
            Board.Label(r, new Rect(650, y, 300, 28), "魚", 17, head);
            Board.Label(r, new Rect(910, y, 300, 28), "大きさ", 17, head, TextAnchor.MiddleRight);
            y += 34;
            int shown = Mathf.Min(list.Count, 10);
            for (int i = 0; i < shown + (me >= shown ? 1 : 0); i++)
            {
                int idx = i < shown ? i : me;
                var e = list[idx];
                var row = new Rect(50, y, 1220, 50);
                if (e.player) { Board.Panel(r, row, new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.22f)); Board.Panel(r, new Rect(row.x, row.y, 5, row.height), Board.Pink); }
                else Board.Panel(r, row, new Color(0.02f, 0.05f, 0.12f, i % 2 == 0 ? 0.55f : 0.35f));
                Color rc = idx == 0 ? Board.Gold : idx < 3 ? new Color(0.85f, 0.9f, 1f) : Color.white;
                Board.Label(r, new Rect(row.x + 20, row.y, 100, row.height), $"{idx + 1}位", 26, rc, TextAnchor.MiddleLeft, false, FontStyle.Bold);
                Board.Label(r, new Rect(row.x + 140, row.y, 460, row.height), e.who, 24, Color.white);
                Board.Label(r, new Rect(row.x + 600, row.y, 260, row.height), e.fish, 22, new Color(1, 1, 1, 0.85f));
                Board.Label(r, new Rect(row.x + 860, row.y, 300, row.height), $"{e.cm:0.0} cm", 26, Board.Gold, TextAnchor.MiddleRight, true);
                y += i == shown - 1 && me >= shown ? 70 : 56;
            }
            string mine = me >= 0 ? $"あなたは今週 {me + 1}位（{list[me].cm:0.0}cm・{list[me].fish}）" : "あなたはまだ今週の記録がありません。釣り堀で1匹釣ると番付にのります";
            Board.Label(r, new Rect(50, BkH - 156, BkW - 360, 30), mine, 22, Board.Pink);
            string last = d != null && d.lastWeekRank > 0 ? $"先週は {d.lastWeekRank}位（{d.lastWeekCm:0.0}cm）でした　　" : "";
            Board.Label(r, new Rect(50, BkH - 118, BkW - 360, 30), last + "番付は毎週月曜に新しくなります", 18, new Color(1, 1, 1, 0.7f));
            Board.Label(r, new Rect(50, BkH - 86, BkW - 360, 30), "※ 仮想の釣り人たちの記録です。釣りでふじゅ〜は増えません", 17, new Color(1, 1, 1, 0.55f));
        }

        // ───────── 遊覧船の切符 ─────────

        const float SaW = 760, SaH = 400;

        void ShipBoardTick()
        {
            bool open = _shipAsk != null && !_map && !_menu && !Kassen.Busy;
            if (!open) { if (_sa != null) _sa.Alpha = 0; _saKey = null; return; }
            if (_sa == null)
            {
                _sa = Board.Create("遊覧船", new Vector2(SaW, SaH), 25);
                _sa.worldOffset = new Vector3(0, -0.05f, 1.2f);
                _sa.Apply();
            }
            _sa.Alpha = 1;
            bool can = SkyShip.I != null && SkyShip.I.CanBook(_shipAsk.atLookout) == null;
            string key = $"{_shipAsk.GetInstanceID()}|{_shipBusy}|{_shipMsg}|{BalanceText()}|{can}";
            if (key == _saKey) return;
            _saKey = key;
            var r = _sa.root;
            Board.Clear(r);
            if (Board.Current == Board.Mode.Screen) { if (_sa.canvas.transform.childCount < 2) Board.Dim(r, SaW, SaH, 0.45f); }
            else Board.Dim(r, SaW, SaH);
            Board.Frame(r, new Rect(0, 0, SaW, SaH));
            Board.Label(r, new Rect(0, 24, SaW, 24), "SKY CRUISE", 18, Board.Cyan, TextAnchor.MiddleCenter, true);
            Board.Label(r, new Rect(0, 50, SaW, 50), "遊覧船「月の舟」", 36, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Board.Wrap(r, new Rect(40, 108, SaW - 80, 60), _shipAsk.atLookout
                ? $"月見台まで迎えに来て、空を{SkyShip.Laps}周（約4分）して月見台にもどります。"
                : $"桟橋からすぐ出航して、空を{SkyShip.Laps}周（約4分）して桟橋にもどります。", 21, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            Board.Label(r, new Rect(0, 172, SaW, 40), $"{SkyShip.Price} ふじゅ〜で乗りますか？", 30, Board.Gold, TextAnchor.MiddleCenter);
            Board.Label(r, new Rect(0, 214, SaW, 28), _shipMsg ?? BalanceText(), 19, _shipMsg != null ? Board.Pink : new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            if (_shipBusy) { Board.Label(r, new Rect(0, 290, SaW, 40), "支払っています…", 22, Board.Gold, TextAnchor.MiddleCenter); return; }
            if (can) Board.Btn(r, new Rect(70, 280, 290, 70), "はい、乗る", 26, BuyShip);
            Board.Btn(r, new Rect(SaW - 360, 280, 290, 70), "やめる（Esc）", 26, () => _shipAsk = null);
        }
    }
}
