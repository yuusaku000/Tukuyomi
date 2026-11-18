using UnityEngine;
using UnityEngine.InputSystem;

namespace Tsukuyomi
{
    /// <summary>
    /// KASSEN の板のボタンを、コントローラー・キーボードのボタンでも押せるようにするための共通の読み方（2026-11-18 ユーザーの指示）。
    /// 板に出るボタンには、ここで決めたボタンの名前を（ ）で書く。
    ///   決定（出陣・つづける・もう一度）：VR は A（ボタン配置の入れかえでは B）・キーボード Enter / E・ゲームパッド 下 / 左
    ///   もどる（閉じる・島へもどる）：VR は Y・キーボード Esc・ゲームパッド 右 / Start
    ///   べつの操作（舞台の切りかえ）：VR は X・キーボード Tab
    ///   えらぶ：左スティック・矢印キー・WASD　　強さ：右スティックの左右・1〜3
    /// </summary>
    public static class Pad
    {
        public static bool Ok => Act.Confirm.Down || Act.Interact.Down;
        public static bool Back => Act.Back.Down;
        public static bool Alt => Act.Info.Down;

        public static string OkKey => Vr.On ? Vr.KeyUse : "Enter";
        public static string BackKey => Vr.On ? "Y" : "Esc";
        public static string AltKey => Vr.On ? "X" : "Tab";
        public static string PickKeys => Vr.On ? "左スティック" : "矢印キー";
        public static string RightKeys => Vr.On ? "右スティック ←→" : "1・2・3";

        /// <summary>右スティックの左右（VR のコントローラー・ゲームパッド）。</summary>
        public static float RightX => Vr.On ? Vr.RightStick.x : Gamepad.current != null ? Gamepad.current.rightStick.x.ReadValue() : 0f;

        /// <summary>スティックを倒した瞬間だけ -1 / +1 を返す（倒しっぱなしで何度も動かないように）。</summary>
        public sealed class Edge
        {
            float _prev;
            public int Tick(float v)
            {
                int r = 0;
                if (Mathf.Abs(v) > 0.6f && Mathf.Abs(_prev) <= 0.6f) r = v > 0 ? 1 : -1;
                _prev = v;
                return r;
            }
        }

        /// <summary>ボタンの文字（例：「出陣」→「出陣（A）」）。</summary>
        public static string Label(string text, string key) => $"{text}（{key}）";

        /// <summary>ボタンの文字を書きかえる（ボタン配置を入れかえたときなど、毎回いまの名前に）。</summary>
        public static void SetLabel(UnityEngine.UI.Button b, string text)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
            if (t != null && t.text != text) t.text = text;
        }
    }
}
