using System;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 海の大鳥居の高舞台にいるヤチヨ。前で E → 話しかける（画面は Hud.Yachiyo）。
    ///   「夜にして」：日が沈み、夜がやってきます → 空が10秒ほどかけて夜（21時ごろ）になる。ログアウトか「もとの時間にもどして」で現実の時刻へ
    ///   「ライブをお願い」：段階4のライブにつなぐ（Live.Request）。それまではステージへ案内する
    /// </summary>
    public class YachiyoNpc : MonoBehaviour
    {
        public Vector3 stand;      // 話しかける所
        public float baseYaw;
        public Transform ring;     // 足もとの光の輪

        public static YachiyoNpc I { get; private set; }
        public static bool Talking;

        float _shiftT = -1f, _shiftLen = 10f;
        TimeSpan _from, _to;

        void Awake() => I = this;

        public static YachiyoNpc Near(Vector3 p, float r)
        {
            if (I == null || !I.isActiveAndEnabled) return null; // ライブの間は舞台にいる
            var d = I.stand - p; d.y *= 0.5f;
            return d.magnitude < r ? I : null;
        }

        /// <summary>いまは夜か（19時〜4時）。</summary>
        public static bool IsNight { get { int h = Ts.Now.Hour; return h >= 19 || h < 4; } }

        /// <summary>ヤチヨに時刻を動かしてもらっているか。</summary>
        public static bool TimeChanged { get; private set; }

        public void MakeNight()
        {
            var now = DateTime.Now;
            var target = now.Date.AddHours(21);
            if (now.Hour < 4) target = now.Date.AddHours(-3);   // 夜中なら、その日の21時へはもどさない
            Shift(target - now, 10f);
            TimeChanged = true;
            Hud.Banner("日が沈み、夜がやってきます", "YACHIYO ▸ NIGHTFALL", 6f);
        }

        /// <summary>初めてのログインの夜を、起動しなおしたあとも続ける（いきなり夜の時刻へ。動かさない）。</summary>
        public static void KeepNight(TimeSpan offset)
        {
            Ts.Offset = offset;
            TimeChanged = true;
        }

        public void RestoreTime()
        {
            Hud.ClearKeptNight();
            Shift(TimeSpan.Zero, 8f);
            TimeChanged = false;
            Hud.Banner("時間が、もとにもどります", "YACHIYO ▸ REAL TIME", 5f);
        }

        void Shift(TimeSpan to, float seconds)
        {
            _from = Ts.Offset; _to = to; _shiftLen = seconds; _shiftT = 0;
            Ts.Shifting = true;
        }

        void Update()
        {
            float t = Time.time;
            if (ring != null) ring.localRotation = Quaternion.Euler(0, t * 20f, 0);
            // 話しているときは自分のほうを向く
            var pc = Hud.PlayerPos;
            float want = baseYaw;
            if (Talking && pc.HasValue)
            {
                var v = pc.Value - transform.position; v.y = 0;
                if (v.sqrMagnitude > 0.01f) want = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            }
            transform.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(transform.eulerAngles.y, want, 180f * Time.deltaTime), 0);

            if (_shiftT >= 0)
            {
                _shiftT += Time.deltaTime;
                float k = Mathf.Clamp01(_shiftT / _shiftLen);
                k = k * k * (3 - 2 * k);
                Ts.Offset = TimeSpan.FromTicks(_from.Ticks + (long)((_to.Ticks - _from.Ticks) * (double)k));
                if (_shiftT >= _shiftLen) { _shiftT = -1; Ts.Offset = _to; Ts.Shifting = false; }
            }
        }
    }

    /// <summary>ライブのお願い。Requested は LiveShow が入れる（ステージの客席へワープして開演）。</summary>
    public static class Live
    {
        public static Action Requested;

        public static void Request(PlayerController player, OrbitCamera cam)
        {
            if (Requested != null) { Requested(); return; }
            var z = Zone.Find("stage");
            if (z != null && player != null) z.WarpTo(player, cam);
            Hud.Toast("ライブはいま準備中だよ。ステージで待っててね！（ヤチヨ）");
        }
    }
}
