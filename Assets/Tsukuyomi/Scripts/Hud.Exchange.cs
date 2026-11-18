using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// ふじゅ〜の両替（ミニヤチヨ → ツクヨミの片道）。板（Board）で作る：画面では真ん中、VR では目の前に出る。
    /// Esc のメニューの「ふじゅ〜の両替」から開く。金額をえらんで「両替する」。もどせないことを画面に書いておく。
    /// </summary>
    public partial class Hud
    {
        static bool _exchange;
        Board _ex;
        Text _exBal, _exMsg, _exPick;
        readonly UnityEngine.UI.Button[] _exAmountBtn = new UnityEngine.UI.Button[4];
        static readonly int[] ExAmounts = { 100, 500, 1000, 5000 };
        int _exAmount;
        bool _exBusy;

        public static void OpenExchange(int doAmount = 0)
        {
            if (_i == null) return;
            _i._menu = false;
            _exchange = true;
            _i.BuildExchange();
            _i._exAmount = 0;
            _i._exMsg.text = "金額をえらんでください";
            _i.RefreshMini();
            if (doAmount > 0) { _i._exAmount = doAmount; _i.DoExchange(); }
        }

        public static void CloseExchange() { if (!(_i != null && _i._exBusy)) _exchange = false; }

        void BuildExchange()
        {
            if (_ex != null) return;
            const float w = 720, h = 470;
            _ex = Board.Create("両替", new Vector2(w, h), 20);
            _ex.screenAnchor = new Vector2(0.5f, 0.5f);
            _ex.worldOffset = new Vector3(0, -0.05f, 1.2f);
            _ex.Apply();
            var r = _ex.root;
            var c = Board.Cyan;
            Board.Dim(r, w, h);
            Board.Frame(r, new Rect(0, 0, w, h));
            Board.Display(r, new Rect(0, 14, w, 34), "FUJU EXCHANGE", 24, Board.Teal);
            Board.Label(r, new Rect(0, 48, w, 50), "ふじゅ〜の両替", 38, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Board.Panel(r, new Rect(w / 2 - 110, 102, 220, 2), Board.Vermilion);
            Board.Label(r, new Rect(0, 116, w, 26), "ミニヤチヨ → ツクヨミ（1:1・片道です。ツクヨミからはもどせません）", 18, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);
            _exBal = Board.Label(r, new Rect(0, 156, w, 34), "", 24, Board.Gold, TextAnchor.MiddleCenter);
            float bx = 60, bw = 140, gap = (w - 120 - bw * 4) / 3f;
            for (int i = 0; i < ExAmounts.Length; i++)
            {
                int amt = ExAmounts[i];
                _exAmountBtn[i] = Board.ButtonNode(r, new Rect(bx + i * (bw + gap), 210, bw, 58), $"{amt:N0}", 26, () => { _exAmount = amt; _exMsg.text = ""; });
            }
            _exPick = Board.Label(r, new Rect(0, 282, w, 30), "", 22, Color.white, TextAnchor.MiddleCenter);
            Board.ButtonNode(r, new Rect(w / 2 - 250, 330, 240, 60), "両替する", 26, DoExchange);
            Board.ButtonNode(r, new Rect(w / 2 + 10, 330, 240, 60), "閉じる（Esc）", 24, CloseExchange);
            _exMsg = Board.Label(r, new Rect(0, 408, w, 30), "", 20, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
        }

        void RefreshMini()
        {
            StartCoroutine(Fuju.MiniWallet(res =>
            {
                if (res.mini < 0 && !_exBusy && _exMsg != null) _exMsg.text = res.error;
            }));
        }

        void DoExchange()
        {
            if (_exBusy) return;
            if (_exAmount <= 0) { _exMsg.text = "先に金額をえらんでください"; return; }
            _exBusy = true;
            _exMsg.text = "ミニヤチヨから受け取っています…";
            int amt = _exAmount;
            StartCoroutine(Fuju.Exchange(amt, res =>
            {
                _exBusy = false;
                if (res.ok)
                {
                    _exMsg.text = $"{amt:N0} ふじゅ〜を両替しました！";
                    _exAmount = 0;
                    Sfx.Coin();
                }
                else _exMsg.text = res.error;
            }));
        }

        /// <summary>毎フレーム（Update から）：板の表示と文字を今の状態に合わせる。</summary>
        void ExchangeTick()
        {
            if (_ex == null) return;
            _ex.Alpha = _exchange ? 1f : 0f;
            if (!_exchange) return;
            string mini = Fuju.MiniLast >= 0 ? $"{Fuju.MiniLast:N0}" : "―";
            _exBal.text = $"ミニヤチヨ {mini} ふじゅ〜　▸　ツクヨミ {Mathf.Max(0, Fuju.Last):N0} ふじゅ〜";
            _exPick.text = _exAmount > 0 ? $"{_exAmount:N0} ふじゅ〜を両替します" : "";
            for (int i = 0; i < _exAmountBtn.Length; i++)
            {
                var img = _exAmountBtn[i].GetComponent<Image>();
                img.color = ExAmounts[i] == _exAmount ? new Color(Board.Pink.r * 0.55f, Board.Pink.g * 0.35f, Board.Pink.b * 0.5f, 0.95f)
                                                       : new Color(Board.Cyan.r * 0.25f, Board.Cyan.g * 0.25f, Board.Cyan.b * 0.3f, 0.85f);
            }
        }
    }
}
