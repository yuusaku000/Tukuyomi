using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 月見商店街のお店と、着がえの画面。
    ///   店先で E → お店（品物をえらぶと試着・買うときは確認してからツクヨミのふじゅ〜で払う）
    ///   C → 着がえ（持っている服と飾り）
    /// </summary>
    public partial class Hud
    {
        static ShopStand _shop;
        static bool _wardrobe;
        static int _wardTab;
        static int _sel;
        static bool _confirm, _busy;
        static string _shopMsg = "";
        static float _shopMsgT;
        static bool _balanceKnown;
        static string _balanceErr;
        float _savedYaw, _savedPitch, _savedDist;
        bool _savedFp, _camSaved;

        public static bool ShopOpen => _shop != null || _wardrobe;

        static SaveData Data => GameSession.Instance != null ? GameSession.Instance.Data : null;
        Wardrobe Ward => player != null ? player.GetComponent<Wardrobe>() : null;

        // ───────── 開く・閉じる ─────────

        public static void OpenShopFor(string id, string preview, bool warp)
        {
            if (_i == null) return;
            var s = ShopStand.Find(id);
            if (s == null) { Debug.LogWarning("shop not found: " + id); return; }
            _i.OpenShop(s, warp);
            if (preview != null)
            {
                var list = Items(s.group);
                int k = list.FindIndex(x => x.id == preview);
                if (k >= 0) _i.Select(k);
            }
        }

        public static void OpenWardrobe()
        {
            if (_i == null) return;
            CloseShop();
            _wardrobe = true;
            _sel = -1;
            _i._map = false; _i._menu = false;
            _i.FrontCam(_i.player.Yaw + 180f);
        }

        public static void CloseShop()
        {
            if (_i == null || !ShopOpen) return;
            bool barber = IsBarber;
            _shop = null; _wardrobe = false; _confirm = false;
            var d = Data; var w = _i.Ward;
            if (d != null && w != null) w.Show(d.clothes, d.wearing);
            if (barber) _i.BarberRevert();
            _i.RestoreCam();
        }

        /// <summary>確認用：品物を本当に買う（ツクヨミのふじゅ〜で）。</summary>
        public static void TestBuy(string id)
        {
            var it = Catalog.Get(id);
            if (_i == null || it == null) return;
            OpenShopFor(it.shop, id, true);
            _i.DoBuy(it);
        }

        void OpenShop(ShopStand s, bool warp)
        {
            CloseShop();
            _shop = s;
            _sel = -1; _confirm = false; _busy = false;
            _map = false; _menu = false;
            _shopMsg = s.hello; _shopMsgT = 6f;
            // 通りのほうを向いて立つ（カメラは通りから店を背にした自分を写す）
            if (warp || Vector3.Distance(player.Position, s.stand) < 4f)
                player.Teleport(s.stand, s.standYaw + 180f);
            FrontCam(s.standYaw);
            RefreshBalance();
            if (IsBarber) BarberOpen();
        }

        void FrontCam(float yaw)
        {
            if (cam == null) return;
            if (!_camSaved)
            {
                _savedYaw = cam.yaw; _savedPitch = cam.pitch; _savedDist = cam.distance; _savedFp = cam.firstPerson;
                _camSaved = true;
            }
            cam.firstPerson = false;
            cam.yaw = yaw; cam.pitch = 4f; cam.distance = 3.3f;
            cam.Snap();
        }

        void RestoreCam()
        {
            if (cam == null || !_camSaved) return;
            cam.yaw = _savedYaw; cam.pitch = _savedPitch; cam.distance = _savedDist; cam.firstPerson = _savedFp;
            _camSaved = false;
            cam.Snap();
        }

        void RefreshBalance()
        {
            _balanceKnown = Fuju.Last >= 0; _balanceErr = null;
            StartCoroutine(Fuju.Wallet(r =>
            {
                if (r.fuju >= 0) { _balanceKnown = true; _balanceErr = null; }
                else _balanceErr = r.error;
            }));
        }

        static List<Item> Items(string group) => new List<Item>(Catalog.InShop(group));

        static bool Owns(Item it) => it.id == Catalog.StartClothes || (Data != null && Data.owned.Contains(it.id));
        static bool Wears(Item it) => Data != null && (it.kind == ItemKind.Clothes ? Data.clothes == it.id : Data.wearing.Contains(it.id));

        /// <summary>飾りをつける：同じ場所の飾りは外してから。</summary>
        static List<string> WithAcc(List<string> wearing, Item it)
        {
            var l = new List<string>();
            foreach (var w in wearing) { var x = Catalog.Get(w); if (x != null && x.slot != it.slot) l.Add(w); }
            l.Add(it.id);
            return l;
        }

        void Select(int k)
        {
            _sel = k;
            _confirm = false;
            var d = Data; var w = Ward;
            if (_shop == null || d == null || w == null) return;
            var list = Items(_shop.group);
            if (k < 0 || k >= list.Count) return;
            var it = list[k];
            if (it.kind == ItemKind.Clothes) w.Show(it.id, d.wearing);
            else if (it.kind == ItemKind.Accessory) w.Show(d.clothes, WithAcc(d.wearing, it));
        }

        void Equip(Item it, bool on)
        {
            var d = Data; var w = Ward;
            if (d == null || w == null) return;
            if (it.kind == ItemKind.Clothes) d.clothes = it.id;
            else if (it.kind == ItemKind.Accessory)
            {
                if (on) { var l = WithAcc(d.wearing, it); d.wearing.Clear(); d.wearing.AddRange(l); }
                else d.wearing.Remove(it.id);
            }
            w.Wear(d.clothes, d.wearing);
            GameSession.Instance.SaveItems();
        }

        void DoBuy(Item it)
        {
            if (_busy) return;
            _busy = true; _confirm = false;
            Msg("支払っています…");
            StartCoroutine(Fuju.Buy(it, r =>
            {
                _busy = false;
                if (!r.ok)
                {
                    Msg(string.IsNullOrEmpty(r.error) ? "買えませんでした" : r.error);
                    if (r.fuju < 0) _balanceErr = r.error; else _balanceKnown = true;
                    return;
                }
                _balanceKnown = true; _balanceErr = null;
                Sfx.Coin();
                var d = Data;
                if (it.kind == ItemKind.Food)
                {
                    if (d != null) { d.eaten++; GameSession.Instance.SaveItems(); }
                    var st = _shop;
                    CloseShop();
                    // 食べているところを正面から見せて、食べ終わったらカメラをもどす
                    if (st != null) { FrontCam(st.standYaw); _eatCam = 3.4f; }
                    Ward?.Eat(it);
                    Toast($"{it.name}（{Catalog.Yen(it.price)} ふじゅ〜）　残り {Catalog.Yen(r.fuju)} ふじゅ〜");
                    return;
                }
                if (d != null && !d.owned.Contains(it.id)) d.owned.Add(it.id);
                Equip(it, true);
                Msg($"「{it.name}」を買いました！　残り {Catalog.Yen(r.fuju)} ふじゅ〜");
            }));
        }

        static void Msg(string s) { _shopMsg = s; _shopMsgT = 8f; }

        float _eatCam;

        /// <summary>食べ終わったらカメラをもどす（Update から毎フレーム）。</summary>
        void EatCamTick()
        {
            if (_eatCam <= 0) return;
            _eatCam -= Time.unscaledDeltaTime;
            if (_eatCam <= 0 && !ShopOpen) RestoreCam();
        }


        // ───────── キー ─────────

        void ShopKeys(bool usedE, bool usedC = false)
        {
            TurnTick();
            if (_menu || _map || _book) return;
            if (!ShopOpen && player != null && !PlayerController.Busy)
            {
                var near = ShopStand.Near(player.Position, 3.2f);
                if (near != null && Act.Interact.Down && !usedE && !PlayerController.Fly) OpenShop(near, false);
                else if (Act.Wardrobe.Down && !usedC) OpenWardrobe();
            }
            else if (_wardrobe && Act.Wardrobe.Down) CloseShop();
            _shopMsgT -= Time.unscaledDeltaTime;
        }

        // ───────── 描く ─────────

        /// <summary>お店のうえの AR 看板と、店先での「E で入る」。</summary>
        void DrawShopSigns()
        {
            var c = Camera.main;
            if (c == null || player == null) return;
            foreach (var s in ShopStand.All)
            {
                var v = s.sign - c.transform.position;
                float dist = v.magnitude;
                if (dist > 24f || Vector3.Dot(v, c.transform.forward) < 0) continue; // 近くの店だけ名前を出す
                var sp = c.WorldToScreenPoint(s.sign);
                float x = sp.x / S, y = (Screen.height - sp.y) / S;
                float a = Mathf.Clamp01((24f - dist) / 6f);
                var r = new Rect(x - 130, y - 34, 260, 68);
                Panel(r, a);
                Fill(new Rect(r.x, r.y, 4, r.height), new Color(Pink.r, Pink.g, Pink.b, a));
                Text(new Rect(r.x, r.y + 5, r.width, 18), s.english, 13, new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.UpperCenter, true);
                Text(new Rect(r.x, r.y + 22, r.width, 40), s.title, 24, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            }
            var near = PlayerController.Busy ? null : ShopStand.Near(player.Position, 3.2f);
            if (near != null)
            {
                var r = new Rect(W / 2 - 300, H - 330, 600, 60);
                Panel(r);
                Text(r, $"E ▸ {near.title}に入る", 28, Gold, TextAnchor.MiddleCenter);
            }
        }

        string BalanceText() =>
            _balanceErr != null ? _balanceErr : _balanceKnown && Fuju.Last >= 0 ? $"残高 {Catalog.Yen(Fuju.Last)} ふじゅ〜" : "残高をたしかめています…";

        int _turnBtn;

        /// <summary>お店・着がえの間、人形をその場で回す（Update から）。</summary>
        void TurnTick()
        {
            if ((!ShopOpen && !_introDress) || player == null) { _turnBtn = 0; return; }
            if (_shop != null && _shop.kind == ItemKind.Food) return;
            float dir = _turnBtn;
            float tx = Act.TurnX;
            if (Mathf.Abs(tx) > 0.3f) dir += Mathf.Sign(tx);
            if (Mathf.Abs(dir) > 0.01f) player.transform.Rotate(0, Mathf.Sign(dir) * 120f * Time.unscaledDeltaTime, 0);
        }

        /// <summary>近くの人の吹き出し（頭の上に、白い吹き出しと黒い文字）。</summary>
        void DrawBubbles()
        {
            var c = Camera.main;
            if (c == null || Crowd.Bubbles.Count == 0) return;
            foreach (var b in Crowd.Bubbles)
            {
                if (b.who == null) continue;
                var wp = b.who.position + Vector3.up * b.height;
                var v = wp - c.transform.position;
                if (Vector3.Dot(v, c.transform.forward) < 0.5f) continue;
                var sp = c.WorldToScreenPoint(wp);
                float x = sp.x / S, y = (Screen.height - sp.y) / S;
                float a = Mathf.Clamp01(b.t / 0.4f) * Mathf.Clamp01((20f - v.magnitude) / 4f);
                if (a <= 0) continue;
                float w = b.text.Length * 21f + 36f;
                var r = new Rect(x - w / 2, y - 62, w, 46);
                Fill(r, new Color(1f, 1f, 1f, 0.92f * a));
                Frame(r, new Color(Pink.r, Pink.g, Pink.b, 0.8f * a), 2f);
                // しっぽ
                for (int i = 0; i < 4; i++) Fill(new Rect(x - 7 + i * 2, r.yMax + i * 3, 14 - i * 4, 3), new Color(1f, 1f, 1f, 0.92f * a));
                Text(r, b.text, 21, new Color(0.12f, 0.1f, 0.2f, a), TextAnchor.MiddleCenter, false, FontStyle.Normal, false);
            }
        }

        static string SlotName(Slot s) => s switch

        {
            Slot.Hair => "髪", Slot.Head => "頭", Slot.Face => "顔", Slot.Hand => "手", Slot.Shoulder => "肩", Slot.Back => "背中", _ => "",
        };
    }
}
