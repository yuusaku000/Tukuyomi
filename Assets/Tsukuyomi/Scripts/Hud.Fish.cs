using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 段階3の画面：釣り（投げる強さ・糸の張り・釣れた魚）、座る、魚の図鑑と週の大物番付（B）。
    /// E を押したときの順番：お店 → 座る → 番付の立て札 → 釣り。釣り・座っている間の E はやめる・立つ。
    /// </summary>
    public partial class Hud
    {
        static bool _book;
        static ShipBooth _shipAsk;
        static bool _shipBusy;
        static string _shipMsg;
        public static bool ShipAskOpen => _shipAsk != null;
        static int _bookTab, _bookSel = -1;

        public static void OpenBook(int tab)
        {
            if (_i == null) return;
            _book = true; _bookTab = tab; _bookSel = -1;
            _i._menu = false; _i._map = false;
        }

        public static void CloseBook() => _book = false;

        /// <summary>E と B（お店より先に見る）。E を使ったら true。</summary>
        bool ActivityKeys()
        {
            if (_menu || _map || ShopOpen || _shipAsk != null || player == null) return false;
            if (Input.GetKeyDown(KeyCode.B)) { if (_book) _book = false; else OpenBook(0); return false; }
            if (_book || !Input.GetKeyDown(KeyCode.E) || PlayerController.Fly) return false;
            if (Fishing.Busy) { Fishing.I.Stop(); Toast("竿をしまいました"); return true; }
            if (Rest.Sitting) { Rest.I.Stand(); return true; }
            if (ShopStand.Near(player.Position, 3.2f) != null) return false;
            var booth = ShipBooth.Near(player.Position, 2.4f);
            if (booth != null && SkyShip.I != null) { _shipAsk = booth; _shipMsg = SkyShip.I.CanBook(booth.atLookout); _shipBusy = false; RefreshBalance(); return true; }
            if (Rest.I != null && Rest.I.Near(out var s, out int k)) { Rest.I.Sit(s, k); return true; }
            if (RankBoard.Near(player.Position, 2.6f) != null) { OpenBook(1); return true; }
            if (Fishing.I != null && Fishing.I.CanStart) { Fishing.I.Begin(); return true; }
            return false;
        }

        void Prompt(string s)
        {
            var r = new Rect(W / 2 - 300, H - 330, 600, 60);
            Panel(r);
            Text(r, s, 28, Gold, TextAnchor.MiddleCenter);
        }

        Rect Bar(Rect r, float v, Color c, string label, string right)
        {
            Fill(r, new Color(0.02f, 0.05f, 0.12f, 0.7f));
            Fill(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(v), r.height - 4), c);
            Frame(r, new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f), 1f);
            Text(new Rect(r.x, r.y - 28, r.width, 24), label, 18, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft);
            if (right != null) Text(new Rect(r.x, r.y - 28, r.width, 24), right, 18, Gold, TextAnchor.MiddleRight, true);
            return r;
        }

        void DrawActivity()
        {
            if (player == null) return;
            float t = Time.unscaledTime;
            var f = Fishing.I;
            if (Fishing.Busy)
            {
                DrawFishing(f, t);
                return;
            }
            if (f != null && f.resultT > 0 && f.result != null) DrawCatch(f.result, f.resultT);
            if (Rest.Sitting)
            {
                float a = Rest.Idle > 6f ? 0.35f : 0.9f;
                Text(new Rect(0, H - 120, W, 30), "E・WASD ▸ 立つ", 20, new Color(1, 1, 1, a), TextAnchor.MiddleCenter);
                return;
            }
            // 番付の立て札の AR 看板
            var c = Camera.main;
            if (c != null)
                foreach (var b in RankBoard.All)
                {
                    var v = b.sign - c.transform.position;
                    float dist = v.magnitude;
                    if (dist > 40f || Vector3.Dot(v, c.transform.forward) < 0) continue;
                    var sp = c.WorldToScreenPoint(b.sign);
                    float x = sp.x / S, y = (Screen.height - sp.y) / S;
                    float al = Mathf.Clamp01((40f - dist) / 10f);
                    var r = new Rect(x - 130, y - 34, 260, 68);
                    Panel(r, al);
                    Fill(new Rect(r.x, r.y, 4, r.height), new Color(Pink.r, Pink.g, Pink.b, al));
                    Text(new Rect(r.x, r.y + 5, r.width, 18), "WEEKLY BIG FISH", 13, new Color(Cyan.r, Cyan.g, Cyan.b, al), TextAnchor.UpperCenter, true);
                    Text(new Rect(r.x, r.y + 22, r.width, 40), "大物番付", 24, new Color(1, 1, 1, al), TextAnchor.MiddleCenter, false, FontStyle.Bold);
                }
            if (PlayerController.Fly || ShopStand.Near(player.Position, 3.2f) != null) return;
            // 遊覧船の切符売り場の看板と、乗っている間の案内
            if (c != null)
                foreach (var b in ShipBooth.All)
                {
                    var v = b.sign - c.transform.position;
                    float dist = v.magnitude;
                    if (dist > 45f || Vector3.Dot(v, c.transform.forward) < 0) continue;
                    var sp = c.WorldToScreenPoint(b.sign);
                    float x = sp.x / S, y = (Screen.height - sp.y) / S;
                    float al = Mathf.Clamp01((45f - dist) / 10f);
                    var r = new Rect(x - 150, y - 34, 300, 68);
                    Panel(r, al);
                    Fill(new Rect(r.x, r.y, 4, r.height), new Color(Gold.r, Gold.g, Gold.b, al));
                    Text(new Rect(r.x, r.y + 5, r.width, 18), "SKY CRUISE ▸ " + SkyShip.Price + " FUJU", 13, new Color(Cyan.r, Cyan.g, Cyan.b, al), TextAnchor.UpperCenter, true);
                    Text(new Rect(r.x, r.y + 22, r.width, 40), "遊覧船「月の舟」", 24, new Color(1, 1, 1, al), TextAnchor.MiddleCenter, false, FontStyle.Bold);
                }
            if (SkyShip.I != null && SkyShip.I.RiderAboard && !string.IsNullOrEmpty(SkyShip.I.Status))
                Text(new Rect(W - 640, 110, 600, 26), "遊覧船 ▸ " + SkyShip.I.Status, 18, new Color(Gold.r, Gold.g, Gold.b, 0.9f), TextAnchor.UpperRight);
            if (ShipBooth.Near(player.Position, 2.4f) != null) Prompt($"E ▸ 遊覧船に乗る（{SkyShip.Price} ふじゅ〜）");
            else if (Rest.I != null && Rest.I.Near(out _, out _)) Prompt("E ▸ すわる");
            else if (RankBoard.Near(player.Position, 2.6f) != null) Prompt("E ▸ 大物番付を見る");
            else if (f != null && f.CanStart) Prompt("E ▸ 釣りをする");
        }

        void DrawFishing(Fishing f, float t)
        {
            var panel = new Rect(W / 2 - 330, H - 300, 660, 150);
            string hint = "";
            switch (f.state)
            {
                case Fishing.St.Ready:
                    hint = "クリック（Space）長押し ▸ はなすと投げる　マウスで向き　E・Esc ▸ 竿をしまう";
                    Text(new Rect(0, H - 250, W, 40), "池のほうを向いて投げよう", 26, Color.white, TextAnchor.MiddleCenter);
                    break;
                case Fishing.St.Charge:
                    Panel(panel);
                    Bar(new Rect(panel.x + 40, panel.y + 70, panel.width - 80, 30), f.power, Color.Lerp(new Color(0.4f, 0.9f, 1f), Pink, f.power), "投げる強さ", $"{Mathf.Lerp(3f, 12f, f.power):0}m");
                    hint = "はなすと投げる";
                    break;
                case Fishing.St.Fly:
                case Fishing.St.Wait:
                    Text(new Rect(0, H - 250, W, 40), "浮きを見て…　ずぶっと沈んだらクリック！", 26, Color.white, TextAnchor.MiddleCenter);
                    hint = "ちょんと動くのはまだ　クリックで巻きもどす　E・Esc ▸ 竿をしまう";
                    break;
                case Fishing.St.Bite:
                    if (Mathf.Repeat(t, 0.25f) < 0.17f)
                        Text(new Rect(0, H / 2 - 220, W, 120), "！", 110, Pink, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                    Text(new Rect(0, H - 250, W, 44), "いまだ！ クリック！", 34, Gold, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                    break;
                case Fishing.St.Fight:
                {
                    panel = new Rect(W / 2 - 360, H - 330, 720, 200);
                    Panel(panel);
                    bool red = f.tension > 0.78f;
                    var tc = f.tension < 0.5f ? new Color(0.45f, 1f, 0.6f) : f.tension < 0.78f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.25f, 0.3f);
                    Bar(new Rect(panel.x + 40, panel.y + 58, panel.width - 80, 30), f.tension, tc, "糸の張り", red ? (Mathf.Repeat(t, 0.3f) < 0.2f ? "あぶない！ 離して！" : "") : f.burst ? "暴れている！" : "");
                    Bar(new Rect(panel.x + 40, panel.y + 130, panel.width - 80, 22), f.progress, new Color(0.45f, 0.9f, 1f), "手もとまで", f.reeling ? "たぐっている" : "");
                    Text(new Rect(panel.x, panel.y + 160, panel.width, 30), "？？？ がかかっている", 18, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
                    hint = "押している間たぐる・赤くなったら離す";
                    break;
                }
                case Fishing.St.Caught:
                    break;
            }
            if (f.result != null && f.resultT > 0) DrawCatch(f.result, f.resultT);
            if (f.msgT > 0 && !string.IsNullOrEmpty(f.msg))
            {
                float a = Mathf.Clamp01(f.msgT / 0.5f);
                Text(new Rect(0, H - 400, W, 36), f.msg, 24, new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.MiddleCenter);
            }
            if (hint.Length > 0) Text(new Rect(0, H - 120, W, 30), hint, 19, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);
        }

        void DrawCatch(CatchResult res, float left)
        {
            float a = Mathf.Clamp01(left / 0.6f);
            var r = new Rect(W - 600, 260, 560, 400);
            Panel(r, a);
            Fill(new Rect(r.x, r.y, 5, r.height), new Color(Pink.r, Pink.g, Pink.b, a));
            var k = res.kind;
            Text(new Rect(r.x + 26, r.y + 16, 300, 22), "CATCH ▸ 釣れた！", 18, new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.UpperLeft, true);
            Text(new Rect(r.x + 26, r.y + 16, r.width - 52, 22), k.Stars + "  " + k.RankName, 18, new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.UpperRight);
            var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
            GUI.DrawTexture(new Rect(r.x + r.width / 2 - 140, r.y + 46, 280, 140), FishBook.Icon(k, true), ScaleMode.ScaleToFit);
            GUI.color = old;
            Text(new Rect(r.x, r.y + 190, r.width, 54), k.name, 44, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Text(new Rect(r.x, r.y + 244, r.width, 40), $"{res.cm:0.0} cm", 34, new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.MiddleCenter, true);
            string tags = (res.isNew ? "図鑑に登録！　" : "") + (res.isBest ? "自己ベスト！　" : "") + (res.weekBest && res.weekRank > 0 ? $"今週の番付 {res.weekRank}位" : "");
            if (tags.Length > 0) Text(new Rect(r.x, r.y + 290, r.width, 30), tags, 21, new Color(Pink.r, Pink.g, Pink.b, a), TextAnchor.MiddleCenter);
            Text(new Rect(r.x + 24, r.y + 326, r.width - 48, 60), k.desc, 19, new Color(1, 1, 1, 0.85f * a), TextAnchor.MiddleCenter);
        }

        // ───────── 遊覧船の切符 ─────────

        void DrawShipAsk()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.03f, 0.08f, 0.45f));
            var r = new Rect(W / 2 - 380, H / 2 - 200, 760, 400);
            Fill(r, new Color(0.01f, 0.03f, 0.08f, 0.92f));
            Panel(r);
            Text(new Rect(r.x, r.y + 22, r.width, 22), "SKY CRUISE", 16, Cyan, TextAnchor.MiddleCenter, true);
            Text(new Rect(r.x, r.y + 50, r.width, 50), "遊覧船「月の舟」", 38, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Text(new Rect(r.x + 30, r.y + 110, r.width - 60, 60), _shipAsk.atLookout
                ? $"月見台まで迎えに来て、空を{SkyShip.Laps}周（約4分）して月見台にもどります。"
                : $"桟橋からすぐ出航して、空を{SkyShip.Laps}周（約4分）して桟橋にもどります。", 21, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            Text(new Rect(r.x, r.y + 172, r.width, 40), $"{SkyShip.Price} ふじゅ〜で乗りますか？", 30, Gold, TextAnchor.MiddleCenter);
            Text(new Rect(r.x, r.y + 214, r.width, 28), _shipMsg ?? BalanceText(), 19, _shipMsg != null ? Pink : new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            if (_shipBusy) { Text(new Rect(r.x, r.y + 290, r.width, 40), "ミニヤチヨにふじゅ〜を送っています…", 22, Gold, TextAnchor.MiddleCenter); return; }
            bool can = SkyShip.I != null && SkyShip.I.CanBook(_shipAsk.atLookout) == null;
            if (can && Button(new Rect(r.x + 70, r.y + 280, 290, 70), "はい、乗る")) BuyShip();
            if (Button(new Rect(r.xMax - 360, r.y + 280, 290, 70), "やめる（Esc）")) _shipAsk = null;
        }

        void BuyShip()
        {
            var booth = _shipAsk;
            _shipBusy = true;
            var ticket = new Item { id = "yuransen", name = "遊覧船「月の舟」", price = SkyShip.Price, kind = ItemKind.Food };
            StartCoroutine(Fuju.Buy(ticket, res =>
            {
                _shipBusy = false;
                if (!res.ok) { _shipMsg = string.IsNullOrEmpty(res.error) ? "払えませんでした" : res.error; return; }
                _shipAsk = null;
                Sfx.Coin();
                if (SkyShip.I == null) return;
                if (booth.atLookout) SkyShip.I.CallToLookout(); else SkyShip.I.BookAtSea();
                Toast($"遊覧船の切符を買いました（{SkyShip.Price} ふじゅ〜）　残り {Catalog.Yen(res.fuju)} ふじゅ〜");
            }));
        }

        // ───────── 図鑑と番付 ─────────

        void DrawBook()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.03f, 0.08f, 0.55f));
            var r = new Rect(W / 2 - 660, 70, 1320, 940);
            Panel(r);
            Text(new Rect(r.x + 30, r.y + 18, 400, 20), _bookTab == 0 ? "FISH BOOK" : "WEEKLY BIG FISH", 16, Cyan, TextAnchor.UpperLeft, true);
            Text(new Rect(r.x + 28, r.y + 38, 600, 56), _bookTab == 0 ? "魚の図鑑" : "今週の大物番付", 40, Color.white, TextAnchor.UpperLeft, false, FontStyle.Bold);
            Fill(new Rect(r.x + 28, r.y + 98, 220, 2), Pink);
            if (Button(new Rect(r.xMax - 470, r.y + 30, 210, 52), _bookTab == 0 ? "▸ 図鑑" : "図鑑", 22)) { _bookTab = 0; _bookSel = -1; }
            if (Button(new Rect(r.xMax - 250, r.y + 30, 210, 52), _bookTab == 1 ? "▸ 番付" : "番付", 22)) _bookTab = 1;
            var d = GameSession.Instance != null ? GameSession.Instance.Data : null;
            if (_bookTab == 0) DrawFishGrid(r, d);
            else DrawRanking(r, d);
            if (Button(new Rect(r.xMax - 260, r.yMax - 80, 230, 58), "とじる（B / Esc）", 22)) _book = false;
        }

        void DrawFishGrid(Rect r, SaveData d)
        {
            Text(new Rect(r.x + 280, r.y + 52, 500, 34), $"{FishBook.Known} / {FishBook.All.Length} 種　　釣った数 {FishBook.Total} 匹", 22, Gold, TextAnchor.MiddleLeft);
            const int cols = 5;
            float cw = (r.width - 60 - (cols - 1) * 14) / cols, ch = 150;
            for (int n = 0; n < FishBook.All.Length; n++)
            {
                var k = FishBook.All[n];
                var rec = FishBook.Rec(k.id);
                bool known = rec != null;
                var cr = new Rect(r.x + 30 + (n % cols) * (cw + 14), r.y + 118 + (n / cols) * (ch + 12), cw, ch);
                bool sel = n == _bookSel, hover = cr.Contains(Event.current.mousePosition);
                Fill(cr, sel ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.2f) : hover ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.1f) : new Color(0.02f, 0.05f, 0.12f, 0.55f));
                Frame(cr, new Color(Cyan.r, Cyan.g, Cyan.b, sel ? 0.9f : 0.25f), sel ? 2f : 1f);
                if (k.rank > 0) Fill(new Rect(cr.x, cr.y, 4, cr.height), k.rank == 2 ? Gold : Pink);
                Text(new Rect(cr.x + 8, cr.y + 4, 60, 20), $"No.{n + 1:00}", 13, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f), TextAnchor.UpperLeft, true);
                Text(new Rect(cr.x, cr.y + 4, cr.width - 8, 20), k.Stars, 14, Gold, TextAnchor.UpperRight);
                GUI.DrawTexture(new Rect(cr.x + cw / 2 - 70, cr.y + 22, 140, 70), FishBook.Icon(k, known), ScaleMode.ScaleToFit);
                Text(new Rect(cr.x, cr.y + 92, cr.width, 30), known ? k.name : "？？？", 22, known ? Color.white : new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
                Text(new Rect(cr.x, cr.y + 120, cr.width, 24), known ? $"最大 {rec.best:0.0}cm・{rec.count}匹" : "まだ釣っていない", 16, known ? Gold : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
                if (GUI.Button(cr, GUIContent.none, GUIStyle.none)) _bookSel = n;
            }
            // 説明
            var dr = new Rect(r.x + 30, r.yMax - 168, r.width - 330, 120);
            if (_bookSel >= 0)
            {
                var k = FishBook.All[_bookSel];
                var rec = FishBook.Rec(k.id);
                if (rec != null)
                {
                    Text(new Rect(dr.x, dr.y, dr.width, 36), $"{k.name}　{k.Stars} {k.RankName}　（{k.min:0}〜{k.max:0}cm）", 26, Color.white, TextAnchor.UpperLeft, false, FontStyle.Bold);
                    Text(new Rect(dr.x, dr.y + 40, dr.width, 30), k.desc, 21, new Color(1, 1, 1, 0.85f));
                    Text(new Rect(dr.x, dr.y + 74, dr.width, 30), $"はじめて釣った日 {rec.first}　　{k.TimeHint}", 18, new Color(Cyan.r, Cyan.g, Cyan.b, 0.85f));
                }
                else
                {
                    Text(new Rect(dr.x, dr.y, dr.width, 36), $"？？？　{k.Stars} {k.RankName}", 26, new Color(1, 1, 1, 0.7f), TextAnchor.UpperLeft, false, FontStyle.Bold);
                    Text(new Rect(dr.x, dr.y + 40, dr.width, 30), "ヒント：" + k.TimeHint, 21, Gold);
                }
            }
            else
            {
                float rest = d != null ? d.restMinutes : 0f;
                Text(new Rect(dr.x, dr.y + 10, dr.width, 30), "魚をえらぶと説明が出ます。まだの魚は、えらぶと釣れる時間のヒントが出ます", 20, new Color(1, 1, 1, 0.75f));
                Text(new Rect(dr.x, dr.y + 46, dr.width, 30), $"釣った魚は図鑑と番付に残して、池にかえします　　座って休んだ時間 {rest:0} 分", 18, new Color(1, 1, 1, 0.6f));
            }
        }

        void DrawRanking(Rect r, SaveData d)
        {
            var mon = FishBook.WeekStart(Ts.Now);
            Text(new Rect(r.x + 380, r.y + 52, 500, 34), $"{mon:M/d}（月）〜 {mon.AddDays(6):M/d}（日）", 22, Gold, TextAnchor.MiddleLeft);
            var list = FishBook.ThisWeek();
            int me = list.FindIndex(e => e.player);
            float y = r.y + 122;
            Text(new Rect(r.x + 60, y, 100, 28), "順位", 17, new Color(1, 1, 1, 0.6f));
            Text(new Rect(r.x + 180, y, 400, 28), "釣り人", 17, new Color(1, 1, 1, 0.6f));
            Text(new Rect(r.x + 640, y, 300, 28), "魚", 17, new Color(1, 1, 1, 0.6f));
            Text(new Rect(r.x + 900, y, 300, 28), "大きさ", 17, new Color(1, 1, 1, 0.6f), TextAnchor.UpperRight);
            y += 34;
            int shown = Mathf.Min(list.Count, 10);
            for (int i = 0; i < shown + (me >= shown ? 1 : 0); i++)
            {
                int idx = i < shown ? i : me;
                var e = list[idx];
                var row = new Rect(r.x + 40, y, 1200, 50);
                if (e.player) { Fill(row, new Color(Pink.r, Pink.g, Pink.b, 0.22f)); Fill(new Rect(row.x, row.y, 5, row.height), Pink); }
                else Fill(row, new Color(0.02f, 0.05f, 0.12f, i % 2 == 0 ? 0.55f : 0.35f));
                Color rc = idx == 0 ? Gold : idx < 3 ? new Color(0.85f, 0.9f, 1f) : Color.white;
                Text(new Rect(row.x + 20, row.y, 100, row.height), $"{idx + 1}位", 26, rc, TextAnchor.MiddleLeft, false, FontStyle.Bold);
                Text(new Rect(row.x + 140, row.y, 460, row.height), e.who, 24, Color.white, TextAnchor.MiddleLeft);
                Text(new Rect(row.x + 600, row.y, 260, row.height), e.fish, 22, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft);
                Text(new Rect(row.x + 860, row.y, 300, row.height), $"{e.cm:0.0} cm", 26, Gold, TextAnchor.MiddleRight, true);
                y += i == shown - 1 && me >= shown ? 70 : 56;
            }
            string mine = me >= 0 ? $"あなたは今週 {me + 1}位（{list[me].cm:0.0}cm・{list[me].fish}）" : "あなたはまだ今週の記録がありません。釣り堀で1匹釣ると番付にのります";
            Text(new Rect(r.x + 40, r.yMax - 150, r.width - 340, 30), mine, 22, Pink);
            string last = d != null && d.lastWeekRank > 0 ? $"先週は {d.lastWeekRank}位（{d.lastWeekCm:0.0}cm）でした　　" : "";
            Text(new Rect(r.x + 40, r.yMax - 112, r.width - 340, 30), last + "番付は毎週月曜に新しくなります", 18, new Color(1, 1, 1, 0.7f));
            Text(new Rect(r.x + 40, r.yMax - 80, r.width - 340, 30), "※ 仮想の釣り人たちの記録です。釣りでふじゅ〜は増えません", 17, new Color(1, 1, 1, 0.55f));
        }
    }
}
