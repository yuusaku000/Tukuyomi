using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// 遊び方（チュートリアル）の板。ページをめくって読む。
    /// 初めて島に入ったときに1回だけ自動で開き（save.json の guideSeen）、あとは Esc のメニューの「遊び方」から開ける。
    /// ←→・A/D・クリック・ゲームパッドの十字で前後、Esc で閉じる。板（Board）なので VR でも目の前に浮かぶ。
    /// </summary>
    public partial class Hud
    {
        static bool _guide;
        Board _gd;
        int _gdPage;
        RectTransform _gdBody;
        Text _gdNum, _gdNextLabel;
        Image[] _gdDots;

        struct GuidePage { public string tag, en, title, lead; public (string key, string text)[] rows; }

        static readonly GuidePage[] GuidePages =
        {
            new GuidePage { tag = "ようこそ", en = "WELCOME", title = "ようこそ、ツクヨミへ", lead = "ここは月の上の島。好きなように歩いて、遊んで、くつろぐ場所です。",
                rows = new[] { ("歩く", "島をめぐる・景色を見る・写真をとる"), ("買う", "お店で食べ物や服を買う・着がえる"), ("遊ぶ", "釣り・ヤチヨのライブ・遊覧船"), ("戦う", "KASSEN（SETSUNA と SENGOKU）"), ("貯める", "ふじゅ〜（ツクヨミのお金）") } },
            new GuidePage { tag = "歩く", en = "MOVE", title = "歩く・見回す", lead = "まず画面をクリックすると、操作できるようになります。",
                rows = new[] { ("WASD", "歩く（矢印キーでも）"), ("マウス", "見回す"), ("SHIFT", "走る"), ("SPACE", "ジャンプ"), ("V", "一人称と三人称の切りかえ"), ("ホイール", "カメラを近づける・離す") } },
            new GuidePage { tag = "調べる", en = "ACTION", title = "調べる・話す・座る", lead = "何かに近づくと「E」の案内が出ます。",
                rows = new[] { ("E", "お店に入る・人と話す・座る・釣りをする"), ("C", "着がえ（買った服や髪型をえらぶ）"), ("F12", "写真をとる（メニューの「写真のフォルダ」で見られる）"), ("ESC", "閉じる・やめる・メニューを開く") } },
            new GuidePage { tag = "便利な板", en = "TOOLS", title = "島の便利な板", lead = "迷ったら地図を開けば、行きたい場所へワープできます。",
                rows = new[] { ("M", "地図とワープ"), ("B", "魚の図鑑と大物番付"), ("H", "表示をぜんぶ消す（景色だけにする）"), ("ESC", "メニュー（音量・画質・遊び方・両替）") } },
            new GuidePage { tag = "ふじゅ〜", en = "FUJU", title = "ふじゅ〜", lead = "ツクヨミのお金です。初めてのログインで 5,000 ふじゅ〜もらえます。",
                rows = new[] { ("使う", "お店の食べ物・服・髪型など"), ("稼ぐ", "KASSEN で勝つ（SENGOKU は 250〜900）"), ("両替", "ミニヤチヨ → ツクヨミの片道（メニューから。もどせません）") } },
            new GuidePage { tag = "合戦", en = "KASSEN", title = "KASSEN（合戦）", lead = "竹の小道の入口から入ります。2つの遊び方があります。",
                rows = new[] { ("SETSUNA", "1対1 の一人称の格闘。連続技と必殺技で戦う"), ("SENGOKU", "3対3 の陣取り合戦。武器をえらんで櫓と大将を狙う"), ("練習", "どちらも、試合の前に練習できる") } },
            new GuidePage { tag = "戦国", en = "SENGOKU", title = "SENGOKU の遊び方", lead = "2本先取。1人2機。舞台は城下町か森で、試合ごとに変わります。",
                rows = new[] { ("櫓", "輪の中にいると占拠。両方を60秒持てばコールド勝ち"), ("大将落とし", "相手の本陣のだるま。近接攻撃（F でも殴れる）だけ効く"), ("本陣", "自分の陣地に戻ると回復"), ("TAB", "戦況のホログラムの板を開く"), ("E", "武器の移動わざ（引っかけ・ジェット・駕籠など）") } },
        };

        public static void OpenGuide(int page = 0)
        {
            if (_i == null) return;
            _i._menu = false; _i._map = false;
            _guide = true;
            _i.BuildGuide();
            _i.ShowGuidePage(Mathf.Clamp(page, 0, GuidePages.Length - 1));
        }

        public static void CloseGuide()
        {
            _guide = false;
            var d = GameSession.Instance != null ? GameSession.Instance.Data : null;
            if (d != null && !d.guideSeen) { d.guideSeen = true; GameSession.Instance.SaveItems(); }
        }

        const float GdW = 1100, GdH = 640;

        void BuildGuide()
        {
            if (_gd != null) return;
            _gd = Board.Create("遊び方", new Vector2(GdW, GdH), 30);
            _gd.screenAnchor = new Vector2(0.5f, 0.5f);
            _gd.worldOffset = new Vector3(0, -0.05f, 1.25f);
            _gd.Apply();
            var r = _gd.root;
            Board.Dim(r, GdW, GdH);
            Board.Frame(r, new Rect(0, 0, GdW, GdH));
            Board.Display(r, new Rect(60, 30, 400, 30), "HOW TO PLAY", 22, Board.Teal, TextAnchor.MiddleLeft);
            _gdNum = Board.Display(r, new Rect(GdW - 260, 30, 200, 30), "", 22, Board.Gold, TextAnchor.MiddleRight);
            _gdBody = new GameObject("ページ", typeof(RectTransform)).GetComponent<RectTransform>();
            _gdBody.SetParent(r, false);
            _gdBody.anchorMin = _gdBody.anchorMax = _gdBody.pivot = new Vector2(0, 1);
            _gdBody.sizeDelta = new Vector2(GdW, GdH);
            _gdBody.anchoredPosition = Vector2.zero;
            // 下：前へ・ページの点・次へ／閉じる
            Board.ButtonNode(r, new Rect(60, GdH - 96, 200, 56), "◀ 前へ", 24, () => ShowGuidePage(_gdPage - 1));
            _gdNextLabel = Board.ButtonNode(r, new Rect(GdW - 260, GdH - 96, 200, 56), "次へ ▶", 24, () => { if (_gdPage >= GuidePages.Length - 1) CloseGuide(); else ShowGuidePage(_gdPage + 1); }).GetComponentInChildren<Text>();
            _gdDots = new Image[GuidePages.Length];
            float dx = GdW / 2 - (GuidePages.Length - 1) * 14f;
            for (int i = 0; i < GuidePages.Length; i++)
            {
                _gdDots[i] = Board.Panel(r, new Rect(dx + i * 28 - 6, GdH - 74, 12, 12));
                _gdDots[i].sprite = Board.DiscSprite;
            }
            Board.Label(r, new Rect(0, GdH - 50, GdW, 24), "←→ でめくる　　Esc で閉じる（メニューの「遊び方」でいつでも見られます）", 16, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);
        }

        void ShowGuidePage(int p)
        {
            _gdPage = Mathf.Clamp(p, 0, GuidePages.Length - 1);
            for (int i = _gdBody.childCount - 1; i >= 0; i--) Destroy(_gdBody.GetChild(i).gameObject);
            var g = GuidePages[_gdPage];
            var b = _gdBody;
            // 左：縦書きの名札
            Board.NameTag(b, new Rect(64, 86, 74, 380), g.tag, _gdPage % 2 == 0 ? Board.Teal : Board.Vermilion, false, 30);
            // 右：見出しと説明
            Board.Display(b, new Rect(190, 76, 800, 40), g.en, 34, Color.white, TextAnchor.MiddleLeft);
            Board.Label(b, new Rect(190, 120, 860, 46), g.title, 36, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            Board.Panel(b, new Rect(190, 170, 160, 2), Board.Vermilion);
            Board.Label(b, new Rect(190, 184, 860, 30), g.lead, 20, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleLeft);
            float y = 234;
            foreach (var (key, text) in g.rows)
            {
                float kw = Mathf.Max(90, key.Length * (key[0] < 128 ? 15 : 24) + 28);
                Board.Panel(b, new Rect(190, y, kw, 40), new Color(Board.Cyan.r * 0.18f, Board.Cyan.g * 0.18f, Board.Cyan.b * 0.22f, 0.9f));
                Board.Corners(b, new Rect(190, y, kw, 40), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.8f), 8, 1.5f);
                Board.Label(b, new Rect(190, y, kw, 40), key, 20, Board.Cyan, TextAnchor.MiddleCenter, key[0] < 128);
                Board.Label(b, new Rect(190 + kw + 22, y, 860 - kw - 22, 40), text, 22, Color.white, TextAnchor.MiddleLeft);
                y += 50;
            }
            _gdNum.text = $"{_gdPage + 1} / {GuidePages.Length}";
            _gdNextLabel.text = _gdPage >= GuidePages.Length - 1 ? "はじめる ▶" : "次へ ▶";
            for (int i = 0; i < _gdDots.Length; i++) _gdDots[i].color = i == _gdPage ? Board.Pink : new Color(1, 1, 1, 0.3f);
        }

        /// <summary>毎フレーム：表示と、めくるキー。</summary>
        void GuideTick()
        {
            if (_gd == null) return;
            _gd.Alpha = _guide ? 1f : 0f;
            if (!_guide) return;
            var k = Keyboard.current; var gp = Gamepad.current;
            bool next = (k != null && (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) || (gp != null && gp.dpad.right.wasPressedThisFrame);
            bool prev = (k != null && (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame)) || (gp != null && gp.dpad.left.wasPressedThisFrame);
            if (next) { if (_gdPage >= GuidePages.Length - 1) CloseGuide(); else ShowGuidePage(_gdPage + 1); }
            else if (prev) ShowGuidePage(_gdPage - 1);
        }
    }
}
