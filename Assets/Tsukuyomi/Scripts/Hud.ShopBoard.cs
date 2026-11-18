using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// お店・着がえ・理髪店の板（Board）。もとは IMGUI（OnGUI）で描いていたもの。
    ///  画面モード：右に品物の板、左下に人形を回すボタン（左に自分の姿が見えるように）。
    ///  VR モード ：品物の板は少し右前、左前に「鏡の小窓」（自分を正面から写す）と回すボタン。
    /// 中身は、えらんでいる品物・確認中・残高などが変わったときだけ作り直す。
    /// </summary>
    public partial class Hud
    {
        Board _sb, _sbDoll;
        string _sbKey;
        RectTransform _mirrorBox;
        RawImage _mirrorImg;
        Camera _mirrorCam;
        RenderTexture _mirrorRt;

        const float SbW = 720, SbH = 880;

        /// <summary>毎フレーム：板の表示（お店・地図・図鑑など、IMGUI から移した板をまとめて）。</summary>
        void BoardsTick()
        {
            ShopBoardTick();
            IntroTick();
            TalkTick();
            VrHintsTick();
            MapBoardTick();
            BookBoardTick();
            ShipBoardTick();
            FishBoardsTick();
            LiveBoardsTick();
            TipBoardTick();
        }

        void BuildShopBoard()
        {
            if (_sb != null) return;
            _sb = Board.Create("お店", new Vector2(SbW, SbH), 20);
            _sb.screenAnchor = new Vector2(1, 0.5f); _sb.screenOffset = new Vector2(-40, 0);
            _sb.worldOffset = new Vector3(0.3f, -0.1f, 1.15f);
            _sb.Apply();

            const float dw = 420, dh = 620;
            _sbDoll = Board.Create("人形", new Vector2(dw, dh), 19);
            _sbDoll.screenAnchor = new Vector2(0.3f, 0); _sbDoll.screenOffset = new Vector2(0, 110);
            _sbDoll.worldOffset = new Vector3(-0.42f, -0.1f, 1.1f);
            _sbDoll.Apply();
            var d = _sbDoll.root;
            // 鏡の小窓（VR のときだけ見せる）
            _mirrorBox = Board.Group(d, "鏡", new Rect(0, 0, dw, 500));
            Board.Frame(_mirrorBox, new Rect(0, 0, dw, 500));
            Board.Label(_mirrorBox, new Rect(0, 16, dw, 26), "MIRROR", 18, Board.Cyan, TextAnchor.MiddleCenter, true);
            _mirrorImg = Board.Raw(_mirrorBox, new Rect(24, 50, dw - 48, 426));
            // 回すボタン（押している間まわる。VR のトリガーでは1回で30度）
            Board.Label(d, new Rect(0, 512, dw, 26), "A・D（←・→）でも回せます", 17, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);
            var l = Board.Btn(d, new Rect(30, 548, 170, 58), "◀ 回す", 22, null);
            l.gameObject.AddComponent<HoldTurn>().dir = -1;
            var r = Board.Btn(d, new Rect(dw - 200, 548, 170, 58), "回す ▶", 22, null);
            r.gameObject.AddComponent<HoldTurn>().dir = 1;
        }

        void ShopBoardTick()
        {
            if (_introDress)
            {
                // 初めてのログインの着がえ：人形を回すボタンと、VR の鏡だけ出す（品物の板は Hud.Intro）
                BuildShopBoard();
                _sb.Alpha = 0; _sbDoll.Alpha = 1; _sbKey = null;
                _mirrorBox.gameObject.SetActive(Vr.On);
                Mirror(Vr.On);
                return;
            }
            bool open = ShopOpen && !_map && !_menu && _i == this;
            if (!open)
            {
                if (_sb != null) { _sb.Alpha = 0; _sbDoll.Alpha = 0; }
                _sbKey = null;
                Mirror(false);
                return;
            }
            BuildShopBoard();
            _sb.Alpha = 1;
            bool doll = _wardrobe || (_shop != null && _shop.kind != ItemKind.Food);
            _sbDoll.Alpha = doll ? 1 : 0;
            _mirrorBox.gameObject.SetActive(Vr.On);
            Mirror(doll && Vr.On);
            string key = ShopKey();
            if (key == _sbKey) return;
            _sbKey = key;
            Board.Clear(_sb.root);
            if (Board.Current != Board.Mode.Screen) Board.Dim(_sb.root, SbW, SbH);
            if (_wardrobe) BuildWardrobe(_sb.root);
            else if (IsBarber) BuildBarber(_sb.root);
            else BuildShopList(_sb.root);
        }

        /// <summary>作り直すかどうかを決める、いまの状態の文字。</summary>
        string ShopKey()
        {
            var d = Data;
            string own = d == null ? "" : $"{d.owned.Count}/{d.clothes}/{string.Join(",", d.wearing)}/{d.eaten}/{d.hairSet}{d.hairStyle}{d.hairColor}";
            string msg = _shopMsgT > 0 ? _shopMsg : "";
            return $"{(_wardrobe ? "w" + _wardTab : _shop != null ? _shop.GetInstanceID().ToString() : "")}|{_sel}|{_confirm}|{_busy}|{BalanceText()}|{msg}|{own}|{_hairSel}|{_styleSel}";
        }

        /// <summary>お店の上の見出し（英語・店の名前・残高・ひとこと）。</summary>
        void ShopHead(RectTransform r, string en, string title, string note)
        {
            Board.Frame(r, new Rect(0, 0, SbW, SbH));
            Board.Label(r, new Rect(36, 26, 420, 24), en, 18, Board.Cyan, TextAnchor.MiddleLeft, true);
            Board.Label(r, new Rect(34, 50, 420, 52), title, 38, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            if (note != null)
            {
                Board.Label(r, new Rect(SbW - 420, 30, 384, 26), BalanceText(), _balanceErr != null ? 16 : 21, _balanceErr != null ? Board.Pink : Board.Gold, TextAnchor.MiddleRight);
                Board.Label(r, new Rect(SbW - 420, 60, 384, 22), note, 15, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);
            }
            Board.Panel(r, new Rect(36, 108, 220, 2), Board.Vermilion);
        }

        void ShopMsg(RectTransform r, float y)
        {
            if (_shopMsgT > 0 && !string.IsNullOrEmpty(_shopMsg))
                Board.Label(r, new Rect(36, y, SbW - 72, 30), "▸ " + _shopMsg, 20, Board.Gold);
        }

        /// <summary>はい／やめる の確認（品物の板の真ん中に重ねる）。</summary>
        void ShopConfirm(RectTransform r, string line1, string line2, string line3, Color c3, string yes, System.Action onYes)
        {
            var cr = new Rect(50, 300, SbW - 100, 260);
            Board.Dialog(r, cr);
            Board.Label(r, new Rect(cr.x, cr.y + 26, cr.width, 40), line1, 28, Color.white, TextAnchor.MiddleCenter);
            if (line2 != null) Board.Label(r, new Rect(cr.x, cr.y + 68, cr.width, 40), line2, 28, Color.white, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(line3)) Board.Label(r, new Rect(cr.x, cr.y + 112, cr.width, 30), line3, 20, c3, TextAnchor.MiddleCenter);
            Board.Btn(r, new Rect(cr.x + 36, cr.y + 168, 260, 62), yes, 24, onYes);
            Board.Btn(r, new Rect(cr.xMax - 296, cr.y + 168, 260, 62), "やめる", 24, () => _confirm = false);
        }

        void BuildShopList(RectTransform r)
        {
            ShopHead(r, _shop.english, _shop.title, "支払いはツクヨミのふじゅ〜（両替は Esc のメニュー）");
            var list = Items(_shop.group);
            float y = 124;
            float step = Mathf.Min(62f, 590f / Mathf.Max(1, list.Count)); // 品物が多いお店は行を低くして全部入れる
            bool can = !_confirm && !_busy;
            for (int k = 0; k < list.Count; k++)
            {
                var it = list[k];
                int kk = k;
                var row = Board.Pick(r, new Rect(20, y, SbW - 40, step - 4), k == _sel, () => Select(kk), can);
                float rh = step - 4, rw = SbW - 40;
                string tag = it.kind == ItemKind.Food ? "" : Wears(it) ? (it.kind == ItemKind.Clothes ? "着ている" : "つけている") : Owns(it) ? "持っている" : "";
                string slot = it.kind == ItemKind.Accessory ? $"〔{SlotName(it.slot)}〕" : "";
                Board.Label(row, new Rect(18, 0, rw - 220, rh), it.name, rh < 44 ? 20 : 24, Color.white);
                string t = tag.Length > 0 ? tag : slot;
                if (t.Length > 0) Board.Label(row, new Rect(rw - 310, 0, 130, rh), t, 17, tag.Length > 0 ? new Color(0.5f, 1f, 0.7f) : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleRight);
                Board.Label(row, new Rect(rw - 172, 0, 156, rh), Owns(it) && it.kind != ItemKind.Food ? "―" : $"{Catalog.Yen(it.price)} ふじゅ〜", 20, Board.Gold, TextAnchor.MiddleRight);
                y += step;
            }

            var sel = _sel >= 0 && _sel < list.Count ? list[_sel] : null;
            float by = SbH - 96;
            if (sel != null) Board.Wrap(r, new Rect(36, by - 82, SbW - 72, 34), sel.desc, 20, new Color(1, 1, 1, 0.85f));
            ShopMsg(r, by - 44);

            if (_confirm && sel != null)
            {
                int left = Fuju.Last - sel.price;
                ShopConfirm(r, $"「{sel.name}」を", $"{Catalog.Yen(sel.price)} ふじゅ〜で{(sel.kind == ItemKind.Food ? "食べますか？" : "買いますか？")}",
                    Fuju.Last >= 0 ? (left >= 0 ? $"買ったあとの残り：{Catalog.Yen(left)} ふじゅ〜" : "ふじゅ〜が足りないかもしれません") : "", left >= 0 ? Board.Gold : Board.Pink,
                    "はい、買う", () => DoBuy(sel));
                return;
            }

            var b1 = new Rect(30, by, 400, 66);
            if (sel != null && !_busy)
            {
                if (sel.kind == ItemKind.Food) Board.Btn(r, b1, $"食べる　{Catalog.Yen(sel.price)} ふじゅ〜", 24, () => _confirm = true);
                else if (!Owns(sel)) Board.Btn(r, b1, $"買う　{Catalog.Yen(sel.price)} ふじゅ〜", 24, () => _confirm = true);
                else if (sel.kind == ItemKind.Clothes) { if (!Wears(sel)) Board.Btn(r, b1, "これを着る", 24, () => Equip(sel, true)); }
                else if (Wears(sel)) Board.Btn(r, b1, "はずす", 24, () => { Equip(sel, false); Select(_sel); });
                else Board.Btn(r, b1, "つける", 24, () => Equip(sel, true));
            }
            else if (sel == null) Board.Label(r, b1, "品物をえらぶと試着できます", 20, new Color(1, 1, 1, 0.7f));
            Board.Btn(r, new Rect(SbW - 256, by, 226, 66), "とじる（Esc）", 24, CloseShop);
        }

        void BuildWardrobe(RectTransform r)
        {
            ShopHead(r, "WARDROBE", "着がえ", null);
            var d = Data;
            if (d == null) return;
            // 服と飾りをタブで切りかえ、2列で並べる
            Board.Pick(r, new Rect(30, 124, 200, 50), _wardTab == 0, () => { _wardTab = 0; });
            Board.Label(r, new Rect(30, 124, 200, 50), "服", 22, Color.white, TextAnchor.MiddleCenter);
            Board.Pick(r, new Rect(242, 124, 200, 50), _wardTab == 1, () => { _wardTab = 1; });
            Board.Label(r, new Rect(242, 124, 200, 50), "飾り", 22, Color.white, TextAnchor.MiddleCenter);
            if (_wardTab == 1) Board.Label(r, new Rect(458, 124, 240, 50), "同じ場所は1つだけ", 17, new Color(1, 1, 1, 0.6f));
            float colW = (SbW - 60) / 2;
            int k = 0;
            foreach (var it in Catalog.Of(_wardTab == 0 ? ItemKind.Clothes : ItemKind.Accessory))
            {
                if (!Owns(it)) continue;
                bool on = Wears(it);
                var item = it;
                var row = Board.Pick(r, new Rect(20 + (k % 2) * (colW + 20), 190 + (k / 2) * 50, colW, 46), on, () =>
                {
                    if (item.kind == ItemKind.Clothes) { if (!Wears(item)) Equip(item, true); }
                    else Equip(item, !Wears(item));
                });
                k++;
                Board.Label(row, new Rect(14, 0, colW - 70, 46), it.name, 20, Color.white);
                if (it.kind == ItemKind.Accessory) Board.Label(row, new Rect(colW - 64, 0, 52, 46), SlotName(it.slot), 15, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleRight);
            }
            if (k == 0)
                Board.Wrap(r, new Rect(36, 200, SbW - 72, 120), _wardTab == 0 ? "服は浴衣・振袖・ふだん着のお店で買えます" : "まだ持っていません。\n小間物屋（島のあちこちにあります）で買えます", 20, new Color(1, 1, 1, 0.7f));
            Board.Wrap(r, new Rect(36, SbH - 150, SbW - 72, 56), $"食べたもの：{d.eaten} こ　　服は浴衣・振袖・ふだん着のお店で買えます", 18, new Color(1, 1, 1, 0.7f));
            Board.Btn(r, new Rect(SbW - 276, SbH - 96, 246, 66), "とじる（C / Esc）", 24, CloseShop);
        }

        void BuildBarber(RectTransform r)
        {
            ShopHead(r, _shop.english, _shop.title, $"1回 {BarberPrice} ふじゅ〜（試すのは無料）");
            var w = Ward;
            bool can = !_confirm && !_busy;
            Board.Label(r, new Rect(36, 124, 300, 30), "髪の色", 24, Color.white);
            for (int i = 0; i < HairColors.Length; i++)
            {
                var (name, c) = HairColors[i];
                var cc = c;
                var cell = Board.Pick(r, new Rect(36 + (i % 6) * 108, 162 + (i / 6) * 104, 98, 94), Near(c, _hairSel), () => { _hairSel = cc; if (Ward != null) Ward.SetHair(_hairSel, _styleSel); }, can);
                Board.Panel(cell, new Rect(19, 8, 60, 52), c);
                Board.Label(cell, new Rect(0, 62, 98, 28), name, 18, Color.white, TextAnchor.MiddleCenter);
            }
            Board.Label(r, new Rect(36, 384, 300, 30), "髪の形", 24, Color.white);
            for (int i = 0; i < Wardrobe.HairStyles.Length; i++)
            {
                int ii = i;
                var cell = Board.Pick(r, new Rect(36 + (i % 3) * 216, 422 + (i / 3) * 70, 204, 60), i == _styleSel, () => { _styleSel = ii; if (Ward != null) Ward.SetHair(_hairSel, _styleSel); }, can);
                Board.Label(cell, new Rect(0, 0, 204, 60), Wardrobe.HairStyles[i], 22, Color.white, TextAnchor.MiddleCenter);
            }
            float by = SbH - 96;
            ShopMsg(r, by - 52);
            var d = Data;
            bool same = d != null && d.hairSet && d.hairStyle == _styleSel && Near(d.hairColor, _hairSel);
            if (_confirm)
            {
                ShopConfirm(r, "この髪にしますか？", null, $"{BarberPrice} ふじゅ〜", Board.Gold, "はい", BuyHair);
                return;
            }
            if (!_busy && !same) Board.Btn(r, new Rect(30, by, 400, 66), $"これにする　{BarberPrice} ふじゅ〜", 24, () => _confirm = true);
            Board.Btn(r, new Rect(SbW - 256, by, 226, 66), "とじる（Esc）", 24, CloseShop);
        }

        // ───────── 鏡の小窓（VR）─────────

        /// <summary>VR では自分の姿が見えないので、正面から写すカメラを置いて小窓に出す。</summary>
        void Mirror(bool on)
        {
            if (!on)
            {
                if (_mirrorCam != null) _mirrorCam.enabled = false;
                return;
            }
            if (_mirrorCam == null)
            {
                _mirrorRt = new RenderTexture(372, 426, 24) { name = "鏡" };
                var go = new GameObject("鏡のカメラ");
                DontDestroyOnLoad(go);
                _mirrorCam = go.AddComponent<Camera>();
                _mirrorCam.targetTexture = _mirrorRt;
                _mirrorCam.fieldOfView = 32f;
                _mirrorCam.nearClipPlane = 2.4f; _mirrorCam.farClipPlane = 120f; // 自分より手前（お店の壁など）は写さない
                var ad = _mirrorCam.GetUniversalAdditionalCameraData();
                ad.renderPostProcessing = false; ad.renderShadows = false;
                _mirrorImg.texture = _mirrorRt;
                RenderPipelineManager.beginCameraRendering += MirrorBegin;
                RenderPipelineManager.endCameraRendering += MirrorEnd;
            }
            _mirrorCam.enabled = true;
            if (player == null) return;
            var t = player.transform;
            var chest = t.position + Vector3.up * 0.95f;
            _mirrorCam.transform.position = chest + t.forward * 3.4f + Vector3.up * 0.15f;
            _mirrorCam.transform.LookAt(chest);
        }

        // VR では頭のカメラから体を隠している（OrbitCamera.SetBodyHidden）。鏡のカメラが描く間だけ出す
        readonly List<Renderer> _mirrorBody = new List<Renderer>();
        readonly List<bool> _mirrorWas = new List<bool>();

        void MirrorBegin(ScriptableRenderContext ctx, Camera c)
        {
            if (c != _mirrorCam || player == null) return;
            player.GetComponentsInChildren(_mirrorBody);
            _mirrorWas.Clear();
            foreach (var r in _mirrorBody) { _mirrorWas.Add(r.enabled); r.enabled = true; }
        }

        void MirrorEnd(ScriptableRenderContext ctx, Camera c)
        {
            if (c != _mirrorCam) return;
            for (int i = 0; i < _mirrorBody.Count && i < _mirrorWas.Count; i++) if (_mirrorBody[i] != null) _mirrorBody[i].enabled = _mirrorWas[i];
        }

        // ───────── 人形を回すボタン ─────────

        static void SetTurn(int dir) { if (_i != null) _i._turnBtn = dir; }
        static void TurnStep(int dir) { if (_i != null && _i.player != null) _i.player.transform.Rotate(0, dir * 30f, 0); }

        /// <summary>押している間まわす（マウス）。押す・離すのない光の線のクリックでは、1回で30度まわす。</summary>
        class HoldTurn : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
        {
            public int dir;
            bool _down;
            public void OnPointerDown(PointerEventData e) { _down = true; SetTurn(dir); }
            public void OnPointerUp(PointerEventData e) => SetTurn(0);
            public void OnPointerExit(PointerEventData e) => SetTurn(0);
            public void OnPointerClick(PointerEventData e) { if (_down) { _down = false; return; } TurnStep(dir); }
        }
    }
}
