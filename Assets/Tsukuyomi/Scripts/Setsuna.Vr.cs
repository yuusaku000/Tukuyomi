using UnityEngine;
using F = Tsukuyomi.SetsunaFighter;

namespace Tsukuyomi
{
    /// <summary>
    /// SETSUNA の VR 対応（ユーザーと決めたこと：画面モードと同じく、視界ごと相手のほうへ向き、揺れも残す）。
    ///  ・体の向きはいつも相手のほう（右スティックでは回らない）。しゃがむ・倒れると目の高さも下がる。当たると視界が揺れる。
    ///  ・自分の手は VR の手（画面モードの、カメラの前で動く腕は出さない）。
    ///  ・手を振ると攻撃：右手を速く振ると強、ふつうに振ると弱。左手を振ると弱。当たるとコントローラーがふるえる。
    ///  ・ボタン：右トリガー 弱・右グリップ 強・A 必殺・B ジャンプ・左トリガー 投げ・左グリップ しゃがみ・左スティック押しこみ 超必殺・Y 一時停止。
    ///  ・表示の板は目の前 1m に。
    /// </summary>
    public partial class Setsuna : Vr.IBody
    {
        bool VrOn => Vr.On && Vr.RightHand != null;
        Vector3 _vrShake;
        bool _testPlay;

        // ───────── Vr.IBody ─────────

        public Vector3 Feet
        {
            get
            {
                var f = P;
                if (f == null) return Vector3.zero;
                // 画面モードの目の高さ（ふつう 1.52・しゃがみ 0.95・倒れ 0.45）との差だけ、足もとを上下させる
                float eye = f.crouch && f.st == F.St.Idle || (f.move != null && f.move.h == Height.Low) ? 0.95f : 1.52f;
                if (f.st == F.St.Down || f.st == F.St.KO) eye = 0.45f;
                return f.pos + Vector3.up * (f.y + eye - 1.52f) - f.face * 0.4f + _vrShake;
            }
        }

        /// <summary>体の向き：いつも相手のほう（回しても変わらない）。</summary>
        public float Yaw
        {
            get
            {
                if (P == null || C == null) return 0;
                var d = C.pos - P.pos; d.y = 0;
                return d.sqrMagnitude > 1e-4f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0;
            }
            set { }
        }

        public void Carry(Vector3 shift) { }
        public bool Pointer => _paused || _phase == Phase.Result;
        public bool CameraFree => false;

        void VrInit()
        {
            if (!Vr.On) return;
            Vr.Body = this;
            if (_armsRoot != null) _armsRoot.gameObject.SetActive(false);
            if (_hud != null) { _hud.worldScale = 0.0007f; _hud.worldOffset = new Vector3(0, -0.03f, 1.0f); _hud.follow = 12f; _hud.Apply(); }
        }

        void VrEnd()
        {
            if (ReferenceEquals(Vr.Body, this)) Vr.Body = null;
        }

        /// <summary>毎フレーム（Visuals から）：揺れ。</summary>
        void VrTick()
        {
            if (!VrOn) return;
            if (_armsRoot != null && _armsRoot.gameObject.activeSelf) _armsRoot.gameObject.SetActive(false);
            _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime * 0.4f);
            _vrShake = Random.insideUnitSphere * _shake;
        }

        // ───────── 振る攻撃 ─────────

        float _swingCdR, _swingCdL;
        const float SwingLight = 2.2f, SwingHeavy = 3.6f; // m/秒

        /// <summary>手を振ったら、押したボタンの代わりの名前を返す（なければ null）。</summary>
        string VrSwing()
        {
            if (!VrOn) return null;
            if (_testPlay) Vr.SimSwing = true; // 確認用：前の試合の片づけが遅れて消しても、振りつづける
            float dt = Time.deltaTime;
            _swingCdR -= dt; _swingCdL -= dt;
            float r = Vr.RightVel.magnitude, l = Vr.LeftVel.magnitude;
            string got = null;
            if (_swingCdR <= 0 && r > SwingLight) { got = r > SwingHeavy ? "heavy" : "light"; _swingCdR = 0.35f; }
            else if (_swingCdL <= 0 && l > SwingLight) { got = "light"; _swingCdL = 0.35f; }
            if (got != null && Ts.ShotMode) Debug.Log($"[SETSUNA] 手を振った 右{r:0.0} 左{l:0.0}m/秒 → {got}");
            return got;
        }

        /// <summary>VR のときの、操作の説明。</summary>
        const string VrKeys = "左スティック 前後・回りこむ（うしろに倒したまま＝ガード）　左グリップ しゃがみ　B ジャンプ\n手を振る 攻撃（右を速く＝強）　右トリガー 弱・右グリップ 強　A 必殺　左トリガー 投げ　左スティック押しこみ 超必殺";
    }
}
