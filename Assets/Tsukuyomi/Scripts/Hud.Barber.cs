using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 理髪店：髪の色（12色）と形（5種）をえらぶと、その場で試せる。「これにする」で 300 ふじゅ〜を払って決まる。
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

        void BuyHair()
        {
            if (_busy) return;
            _busy = true; _confirm = false;
            Msg("支払っています…");
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
