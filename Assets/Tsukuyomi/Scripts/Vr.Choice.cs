using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>起動時に「画面で遊ぶ／VR で遊ぶ」をえらぶ板。</summary>
    public partial class Vr
    {
        static Board _choice;
        static Text _choiceStatus;
        static float _choiceAt;

        /// <summary>えらぶ板が開いている（その間は歩かない）。</summary>
        public static bool Choosing => _choice != null;

        const float ChW = 960, ChH = 470;

        static void ShowChoice()
        {
            _choice = Board.Create("画面か VR か", new Vector2(ChW, ChH), 60);
            _choice.worldOffset = new Vector3(0, -0.05f, 1.3f);
            _choice.Apply();
            _choiceAt = Time.unscaledTime;
            var r = _choice.root;
            Board.Dim(r, ChW, ChH, 0.6f);
            Board.Frame(r, new Rect(0, 0, ChW, ChH));
            Board.Display(r, new Rect(60, 34, 500, 30), "TSUKUYOMI", 22, Board.Teal, TextAnchor.MiddleLeft);
            Board.Label(r, new Rect(0, 80, ChW, 50), "どちらで遊びますか？", 36, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Board.Panel(r, new Rect(ChW / 2 - 80, 136, 160, 2), Board.Vermilion);
            var a = Board.ButtonNode(r, new Rect(110, 170, 340, 120), "画面で遊ぶ", 32, () => { Sfx.Click(); CloseChoice(); });
            Board.Label((RectTransform)a.transform, new Rect(0, 82, 340, 30), "キーボード・マウス・ゲームパッド（Enter）", 15, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter);
            var b = Board.ButtonNode(r, new Rect(ChW - 450, 170, 340, 120), "VR で遊ぶ", 32, () => { Sfx.Click(); Host.StartCoroutine(StartXr()); });
            Board.Label((RectTransform)b.transform, new Rect(0, 82, 340, 30), "VR 機器（OpenXR）", 15, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter);
            _choiceStatus = Board.Label(r, new Rect(40, 312, ChW - 80, 40), "", 19, Board.Gold, TextAnchor.MiddleCenter);
            Board.Label(r, new Rect(40, 360, ChW - 80, 60),
                "VR は、先に VR 機器をつないでおいてください（Quest は Link でつないでから）。\nVR の設定は、あとから Esc のメニューの「設定 → VR」で変えられます。", 16, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter);
        }

        /// <summary>確認用（tour の choice / choice:vr）：えらぶ板を出す。vr なら「VR で遊ぶ」を押す。</summary>
        public static void TestChoice(bool pressVr)
        {
            if (On) return;
            if (_choice == null) ShowChoice();
            if (pressVr) Host.StartCoroutine(StartXr());
        }

        static void SetChoiceStatus(string s) { if (_choiceStatus != null) _choiceStatus.text = s; }

        static void CloseChoice()
        {
            if (_choice == null) return;
            Board.Unregister(_choice);
            Destroy(_choice.gameObject);
            _choice = null; _choiceStatus = null;
        }

        /// <summary>毎フレーム（Hud から）：Enter で「画面で遊ぶ」。</summary>
        public static void ChoiceTick()
        {
            if (_choice == null) return;
            if (Time.unscaledTime - _choiceAt > 0.3f && Act.Confirm.Down) { Sfx.Click(); CloseChoice(); }
        }
    }
}
