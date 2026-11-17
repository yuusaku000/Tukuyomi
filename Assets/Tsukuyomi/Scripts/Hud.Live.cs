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
            if (_tipOpen) return Act.Wardrobe.Down;
            for (int i = 0; i < LiveShow.PenColors.Length; i++)
                if (Act.Number(i))
                {
                    LiveShow.SetPen(i);
                    Toast($"ペンライトを「{LiveShow.PenNames[i]}」にしました");
                }
            if (Input.GetKeyDown(KeyCode.T)) { TestTipAsk(); return false; }
            if (Act.Wardrobe.Down) { LiveShow.SetAutoCam(!LiveShow.AutoCam); return true; }
            return false;
        }

        // ───────── 投げ銭 ─────────

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
