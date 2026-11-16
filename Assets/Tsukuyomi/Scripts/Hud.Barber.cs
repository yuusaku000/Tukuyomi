using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 理髪店：髪の色（12色）と形（5種）をえらぶと、その場で試せる。「これにする」で 300 ふじゅ〜（ミニヤチヨ）を払って決まる。
    /// 払わずに閉じたら、もとの髪にもどす。
    /// </summary>
    public partial class Hud
    {
        public const int BarberPrice = 300;

        static readonly (string name, Color c)[] HairColors =
        {
            ("黒", new Color(0.1f, 0.08f, 0.09f)), ("こげ茶", new Color(0.24f, 0.15f, 0.1f)), ("栗色", new Color(0.48f, 0.3f, 0.17f)), ("金", new Color(0.95f, 0.8f, 0.42f)),
            ("銀", new Color(0.8f, 0.82f, 0.88f)), ("ヤチヨ色", new Color(0.86f, 0.89f, 0.98f)), ("桜", new Color(1f, 0.72f, 0.82f)), ("紅", new Color(0.75f, 0.12f, 0.2f)),
            ("藤", new Color(0.65f, 0.55f, 0.9f)), ("空色", new Color(0.55f, 0.8f, 1f)), ("若草", new Color(0.55f, 0.8f, 0.45f)), ("紺", new Color(0.15f, 0.18f, 0.42f)),
        };

        static Color _hairSel;
        static int _styleSel;
        static bool IsBarber => _shop != null && _shop.group == "barber";

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

        void BarberOpen()
        {
            var w = Ward;
            if (w == null) return;
            _hairSel = w.HairColor; _styleSel = w.HairStyle; _confirm = false;
        }

        /// <summary>払わずに閉じたとき：保存してある髪にもどす。</summary>
        void BarberRevert()
        {
            var d = Data; var w = Ward;
            if (w == null) return;
            if (d != null && d.hairSet) w.SetHair(d.hairColor, d.hairStyle);
            else w.SetHair(new Color(0.16f, 0.12f, 0.16f), 1);
        }

        void DrawBarber()
        {
            var r = new Rect(W - 740, 100, 700, 880);
            Panel(r);
            Text(new Rect(r.x + 26, r.y + 18, 400, 20), _shop.english, 16, Cyan, TextAnchor.UpperLeft, true);
            Text(new Rect(r.x + 24, r.y + 38, 500, 56), _shop.title, 40, Color.white, TextAnchor.UpperLeft, false, FontStyle.Bold);
            Text(new Rect(r.x + 300, r.y + 20, 376, 26), BalanceText(), _balanceErr != null ? 16 : 21, _balanceErr != null ? Pink : Gold, TextAnchor.UpperRight);
            Text(new Rect(r.x + 300, r.y + 50, 376, 22), $"1回 {BarberPrice} ふじゅ〜（試すのは無料）", 15, new Color(1, 1, 1, 0.65f), TextAnchor.UpperRight);
            Fill(new Rect(r.x + 24, r.y + 98, 220, 2), Pink);
            var w = Ward;
            bool changed = false;
            Text(new Rect(r.x + 26, r.y + 116, 300, 30), "髪の色", 24, Color.white);
            for (int i = 0; i < HairColors.Length; i++)
            {
                var (name, c) = HairColors[i];
                var cr = new Rect(r.x + 26 + (i % 6) * 108, r.y + 156 + (i / 6) * 104, 98, 94);
                bool sel = Near(c, _hairSel);
                Fill(cr, new Color(0.02f, 0.05f, 0.12f, 0.6f));
                Fill(new Rect(cr.x + 19, cr.y + 8, 60, 52), c);
                Frame(cr, sel ? Pink : new Color(Cyan.r, Cyan.g, Cyan.b, 0.3f), sel ? 3f : 1f);
                Text(new Rect(cr.x, cr.y + 62, cr.width, 28), name, 18, Color.white, TextAnchor.MiddleCenter);
                if (!_confirm && !_busy && GUI.Button(cr, GUIContent.none, GUIStyle.none)) { _hairSel = c; changed = true; Sfx.Click(); }
            }
            Text(new Rect(r.x + 26, r.y + 376, 300, 30), "髪の形", 24, Color.white);
            for (int i = 0; i < Wardrobe.HairStyles.Length; i++)
            {
                var br = new Rect(r.x + 26 + (i % 3) * 216, r.y + 416 + (i / 3) * 70, 204, 60);
                bool sel = i == _styleSel;
                if (Button(br, (sel ? "▸ " : "") + Wardrobe.HairStyles[i], 22) && !_confirm && !_busy) { _styleSel = i; changed = true; }
                if (sel) Frame(br, Pink, 3f);
            }
            if (changed && w != null) w.SetHair(_hairSel, _styleSel);

            float by = r.yMax - 92;
            if (_shopMsgT > 0 && !string.IsNullOrEmpty(_shopMsg)) Text(new Rect(r.x + 26, by - 50, r.width - 52, 30), "▸ " + _shopMsg, 20, Gold);
            var d = Data;
            bool same = d != null && d.hairSet && d.hairStyle == _styleSel && Near(d.hairColor, _hairSel);
            if (_confirm)
            {
                var cr = new Rect(r.x + 40, r.y + 300, r.width - 80, 250);
                Fill(cr, new Color(0.01f, 0.03f, 0.08f, 0.96f));
                Panel(cr);
                Text(new Rect(cr.x, cr.y + 30, cr.width, 40), "この髪にしますか？", 30, Color.white, TextAnchor.MiddleCenter);
                Text(new Rect(cr.x, cr.y + 76, cr.width, 36), $"{BarberPrice} ふじゅ〜", 28, Gold, TextAnchor.MiddleCenter, true);
                if (Button(new Rect(cr.x + 40, cr.y + 160, 260, 62), "はい")) BuyHair();
                if (Button(new Rect(cr.xMax - 300, cr.y + 160, 260, 62), "やめる")) _confirm = false;
                return;
            }
            if (!_busy && !same && Button(new Rect(r.x + 24, by, 400, 66), $"これにする　{BarberPrice} ふじゅ〜")) _confirm = true;
            if (Button(new Rect(r.xMax - 250, by, 226, 66), "とじる（Esc）")) { CloseShop(); return; }
            TurnButtons(r);
        }

        void BuyHair()
        {
            if (_busy) return;
            _busy = true; _confirm = false;
            Msg("ミニヤチヨにふじゅ〜を送っています…");
            var ticket = new Item { id = "barber", name = "理髪店", price = BarberPrice, kind = ItemKind.Accessory };
            Color c = _hairSel; int st = _styleSel;
            StartCoroutine(Fuju.Buy(ticket, res =>
            {
                _busy = false;
                if (!res.ok) { Msg(string.IsNullOrEmpty(res.error) ? "払えませんでした" : res.error); return; }
                Sfx.Coin();
                var d = Data;
                if (d != null) { d.hairSet = true; d.hairColor = c; d.hairStyle = st; GameSession.Instance.SaveItems(); }
                Msg($"すてきです！ 残り {Catalog.Yen(res.fuju)} ふじゅ〜");
            }));
        }
    }
}
