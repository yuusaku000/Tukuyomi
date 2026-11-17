using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>お知らせ（Hud.Toast）。板（Board）で作った最初の表示。画面では下のまん中、VR では目の前の少し下に出る。</summary>
    public partial class Hud
    {
        Board _notice;
        Text _noticeText, _noticeTag;
        Image _noticeBar;

        void NoticeTick()
        {
            bool panelOpen = _exchange || _map || _menu || ShopOpen || _book || _shipAsk != null || _talk || _tipOpen;
            float a = (_toastT > 0 && !string.IsNullOrEmpty(_toast) && (!HideAll || panelOpen)) ? Mathf.Clamp01(_toastT / 0.5f) : 0f;
            if (_notice == null)
            {
                if (a <= 0) return;
                _notice = Board.Create("お知らせ", new Vector2(860, 64), 10);
                _notice.screenAnchor = new Vector2(0.5f, 0f);
                _notice.screenOffset = new Vector2(0, 230 - 64);
                _notice.worldOffset = new Vector3(0, -0.38f, 1.3f);
                _notice.Apply();
                var r = _notice.root;
                Board.Panel(r, new Rect(0, 0, 860, 64));
                Board.Corners(r, new Rect(0, 0, 860, 64), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.9f));
                _noticeBar = Board.Panel(r, new Rect(0, 0, 5, 64), Board.Pink);
                _noticeTag = Board.Label(r, new Rect(20, 6, 200, 20), "▸ NOTICE", 14, Board.Cyan, TextAnchor.UpperLeft, true);
                _noticeText = Board.Label(r, new Rect(0, 8, 860, 64), "", 26, Color.white, TextAnchor.MiddleCenter);
            }
            _notice.Alpha = a;
            if (a > 0) _noticeText.text = _toast;
        }
    }
}
