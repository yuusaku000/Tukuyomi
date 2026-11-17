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
            if (_menu || _map || ShopOpen || _shipAsk != null || _talk || player == null) return false;
            if (Act.Book.Down) { if (_book) _book = false; else OpenBook(0); return false; }
            if (_book || !Act.Interact.Down || PlayerController.Fly) return false;
            if (Fishing.Busy) { Fishing.I.Stop(); Toast("竿をしまいました"); return true; }
            if (Rest.Sitting) { Rest.I.Stand(); return true; }
            if (YachiyoNpc.Near(player.Position, 2.8f) != null) { OpenTalk(); return true; }
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

        void DrawActivity()
        {
            if (player == null) return;
            float t = Time.unscaledTime;
            var f = Fishing.I;
            if (Fishing.Busy) return; // 釣りのゲージと釣れた魚は板（Hud.PlayBoards）
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
            if (YachiyoNpc.Near(player.Position, 2.8f) != null) Prompt("E ▸ ヤチヨに話しかける");
            else if (ShipBooth.Near(player.Position, 2.4f) != null) Prompt($"E ▸ 遊覧船に乗る（{SkyShip.Price} ふじゅ〜）");
            else if (Rest.I != null && Rest.I.Near(out _, out _)) Prompt("E ▸ すわる");
            else if (RankBoard.Near(player.Position, 2.6f) != null) Prompt("E ▸ 大物番付を見る");
            else if (f != null && f.CanStart) Prompt("E ▸ 釣りをする");
        }

        // ───────── 遊覧船の切符 ─────────

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
    }
}
