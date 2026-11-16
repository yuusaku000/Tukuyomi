using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 月見商店街のお店と、着がえの画面。
    ///   店先で E → お店（品物をえらぶと試着・買うときは確認してからミニヤチヨのふじゅ〜で払う）
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
            _shop = null; _wardrobe = false; _confirm = false;
            var d = Data; var w = _i.Ward;
            if (d != null && w != null) w.Show(d.clothes, d.wearing);
            _i.RestoreCam();
        }

        /// <summary>確認用：品物を本当に買う（テスト用のミニヤチヨに向けて使う）。</summary>
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
            Msg("ミニヤチヨにふじゅ〜を送っています…");
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

        void ShopKeys()
        {
            if (_menu || _map) return;
            if (!ShopOpen && player != null)
            {
                var near = ShopStand.Near(player.Position, 3.2f);
                if (near != null && Input.GetKeyDown(KeyCode.E) && !PlayerController.Fly) OpenShop(near, false);
                else if (Input.GetKeyDown(KeyCode.C)) OpenWardrobe();
            }
            else if (_wardrobe && Input.GetKeyDown(KeyCode.C)) CloseShop();
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
                if (dist > 45f || Vector3.Dot(v, c.transform.forward) < 0) continue;
                var sp = c.WorldToScreenPoint(s.sign);
                float x = sp.x / S, y = (Screen.height - sp.y) / S;
                float a = Mathf.Clamp01((45f - dist) / 10f);
                var r = new Rect(x - 130, y - 34, 260, 68);
                Panel(r, a);
                Fill(new Rect(r.x, r.y, 4, r.height), new Color(Pink.r, Pink.g, Pink.b, a));
                Text(new Rect(r.x, r.y + 5, r.width, 18), s.english, 13, new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.UpperCenter, true);
                Text(new Rect(r.x, r.y + 22, r.width, 40), s.title, 24, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            }
            var near = ShopStand.Near(player.Position, 3.2f);
            if (near != null)
            {
                var r = new Rect(W / 2 - 300, H - 330, 600, 60);
                Panel(r);
                Text(r, $"E ▸ {near.title}に入る", 28, Gold, TextAnchor.MiddleCenter);
            }
        }

        void Row(Rect r, bool selected, string name, string right, string tag, Color tagColor)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, selected ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.2f) : hover ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.1f) : new Color(0.02f, 0.05f, 0.12f, 0.55f));
            Frame(r, new Color(Cyan.r, Cyan.g, Cyan.b, selected ? 0.9f : 0.25f), selected ? 2f : 1f);
            if (selected) Fill(new Rect(r.x, r.y, 5, r.height), Pink);
            Text(new Rect(r.x + 18, r.y, r.width - 200, r.height), name, 24, Color.white, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(tag)) Text(new Rect(r.xMax - 300, r.y, 120, r.height), tag, 17, tagColor, TextAnchor.MiddleRight);
            Text(new Rect(r.xMax - 170, r.y, 155, r.height), right, 20, Gold, TextAnchor.MiddleRight, true);
        }

        string BalanceText() =>
            _balanceErr != null ? _balanceErr : _balanceKnown && Fuju.Last >= 0 ? $"残高 {Catalog.Yen(Fuju.Last)} ふじゅ〜" : "残高をたしかめています…";

        void DrawShop()
        {
            if (_wardrobe) { DrawWardrobe(); return; }
            var r = new Rect(W - 740, 100, 700, 880);
            Panel(r);
            Text(new Rect(r.x + 26, r.y + 18, 400, 20), _shop.english, 16, Cyan, TextAnchor.UpperLeft, true);
            Text(new Rect(r.x + 24, r.y + 38, 500, 56), _shop.title, 40, Color.white, TextAnchor.UpperLeft, false, FontStyle.Bold);
            Text(new Rect(r.x + 300, r.y + 20, 376, 26), BalanceText(), _balanceErr != null ? 16 : 21, _balanceErr != null ? Pink : Gold, TextAnchor.UpperRight);
            Text(new Rect(r.x + 300, r.y + 50, 376, 22), "支払いはミニヤチヨのふじゅ〜", 15, new Color(1, 1, 1, 0.6f), TextAnchor.UpperRight);
            Fill(new Rect(r.x + 24, r.y + 98, 220, 2), Pink);

            var list = Items(_shop.group);
            float y = r.y + 112;

            for (int k = 0; k < list.Count; k++)
            {
                var it = list[k];
                var row = new Rect(r.x + 20, y, r.width - 40, 58);
                string tag = it.kind == ItemKind.Food ? "" : Wears(it) ? (it.kind == ItemKind.Clothes ? "着ている" : "つけている") : Owns(it) ? "持っている" : "";
                string slot = it.kind == ItemKind.Accessory ? $"〔{SlotName(it.slot)}〕" : "";
                Row(row, k == _sel, it.name + (slot.Length > 0 ? "  " : ""), Owns(it) && it.kind != ItemKind.Food ? "―" : $"{Catalog.Yen(it.price)} ふじゅ〜", tag.Length > 0 ? tag : slot, tag.Length > 0 ? new Color(0.5f, 1f, 0.7f) : new Color(1, 1, 1, 0.55f));
                if (!_confirm && !_busy && GUI.Button(row, GUIContent.none, GUIStyle.none)) Select(k);
                y += 62;
            }

            var sel = _sel >= 0 && _sel < list.Count ? list[_sel] : null;
            float by = r.yMax - 92;
            if (sel != null) Text(new Rect(r.x + 26, by - 76, r.width - 52, 30), sel.desc, 20, new Color(1, 1, 1, 0.85f));
            if (_shopMsgT > 0 && !string.IsNullOrEmpty(_shopMsg)) Text(new Rect(r.x + 26, by - 42, r.width - 52, 30), "▸ " + _shopMsg, 20, Gold);

            if (_confirm && sel != null)
            {
                int left = Fuju.Last - sel.price;
                var cr = new Rect(r.x + 40, r.y + 300, r.width - 80, 250);
                Fill(cr, new Color(0.01f, 0.03f, 0.08f, 0.96f));
                Panel(cr);
                Text(new Rect(cr.x, cr.y + 26, cr.width, 40), $"「{sel.name}」を", 30, Color.white, TextAnchor.MiddleCenter);
                Text(new Rect(cr.x, cr.y + 66, cr.width, 40), $"{Catalog.Yen(sel.price)} ふじゅ〜で{(sel.kind == ItemKind.Food ? "食べますか？" : "買いますか？")}", 30, Color.white, TextAnchor.MiddleCenter);
                Text(new Rect(cr.x, cr.y + 108, cr.width, 30), Fuju.Last >= 0 ? (left >= 0 ? $"買ったあとの残り：{Catalog.Yen(left)} ふじゅ〜" : "ふじゅ〜が足りないかもしれません") : "", 20, left >= 0 ? Gold : Pink, TextAnchor.MiddleCenter);
                if (Button(new Rect(cr.x + 40, cr.y + 160, 260, 62), "はい、買う")) DoBuy(sel);
                if (Button(new Rect(cr.xMax - 300, cr.y + 160, 260, 62), "やめる")) _confirm = false;
                return;
            }

            if (sel != null && !_busy)
            {
                var b1 = new Rect(r.x + 24, by, 400, 66);
                if (sel.kind == ItemKind.Food) { if (Button(b1, $"食べる　{Catalog.Yen(sel.price)} ふじゅ〜")) _confirm = true; }
                else if (!Owns(sel)) { if (Button(b1, $"買う　{Catalog.Yen(sel.price)} ふじゅ〜")) _confirm = true; }
                else if (sel.kind == ItemKind.Clothes) { if (!Wears(sel) && Button(b1, "これを着る")) Equip(sel, true); }
                else if (Wears(sel)) { if (Button(b1, "はずす")) { Equip(sel, false); Select(_sel); } }
                else if (Button(b1, "つける")) Equip(sel, true);
            }
            else if (sel == null) Text(new Rect(r.x + 26, by, 420, 66), "品物をえらぶと試着できます", 20, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
            if (Button(new Rect(r.xMax - 250, by, 226, 66), "とじる（Esc）")) CloseShop();
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

        void DrawWardrobe()
        {
            var r = new Rect(W - 760, 100, 720, 880);
            Panel(r);
            Text(new Rect(r.x + 26, r.y + 18, 400, 20), "WARDROBE", 16, Cyan, TextAnchor.UpperLeft, true);
            Text(new Rect(r.x + 24, r.y + 38, 500, 56), "着がえ", 40, Color.white, TextAnchor.UpperLeft, false, FontStyle.Bold);
            Fill(new Rect(r.x + 24, r.y + 98, 220, 2), Pink);
            var d = Data;
            if (d == null) return;

            // 服と飾りをタブで切りかえ、2列で並べる
            if (Button(new Rect(r.x + 24, r.y + 112, 200, 50), _wardTab == 0 ? "▸ 服" : "服", 22)) _wardTab = 0;
            if (Button(new Rect(r.x + 236, r.y + 112, 200, 50), _wardTab == 1 ? "▸ 飾り" : "飾り", 22)) _wardTab = 1;
            if (_wardTab == 1) Text(new Rect(r.x + 450, r.y + 112, 250, 50), "同じ場所は1つだけ", 17, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
            float colW = (r.width - 60) / 2;
            int k = 0;
            foreach (var it in Catalog.Of(_wardTab == 0 ? ItemKind.Clothes : ItemKind.Accessory))
            {
                if (!Owns(it)) continue;
                var row = new Rect(r.x + 20 + (k % 2) * (colW + 20), r.y + 180 + (k / 2) * 50, colW, 46);
                k++;
                bool on = Wears(it);
                Fill(row, on ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f) : new Color(0.02f, 0.05f, 0.12f, 0.55f));
                Frame(row, new Color(Cyan.r, Cyan.g, Cyan.b, on ? 0.9f : 0.25f), on ? 2f : 1f);
                if (on) Fill(new Rect(row.x, row.y, 5, row.height), Pink);
                Text(new Rect(row.x + 14, row.y, row.width - 70, row.height), it.name, 20, Color.white, TextAnchor.MiddleLeft);
                if (it.kind == ItemKind.Accessory) Text(new Rect(row.xMax - 60, row.y, 50, row.height), SlotName(it.slot), 15, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleRight);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    if (it.kind == ItemKind.Clothes) { if (!on) Equip(it, true); }
                    else Equip(it, !on);
                }
            }
            if (k == 0)
                Text(new Rect(r.x + 26, r.y + 190, r.width - 52, 120), _wardTab == 0 ? "服は浴衣・振袖・ふだん着のお店で買えます" : "まだ持っていません。\n小間物屋（島のあちこちにあります）で買えます", 20, new Color(1, 1, 1, 0.7f));
            Text(new Rect(r.x + 26, r.yMax - 150, r.width - 52, 60), $"食べたもの：{d.eaten} こ　　服は浴衣・振袖・ふだん着のお店で買えます", 18, new Color(1, 1, 1, 0.7f));
            if (Button(new Rect(r.xMax - 270, r.yMax - 90, 246, 66), "とじる（C / Esc）")) CloseShop();
        }
    }
}
