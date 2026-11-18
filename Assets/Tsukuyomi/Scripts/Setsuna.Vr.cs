using UnityEngine;
using F = Tsukuyomi.SetsunaFighter;

namespace Tsukuyomi
{
    /// <summary>
    /// SETSUNA の VR 対応（ユーザーと決めたこと：画面モードと同じく、視界ごと相手のほうへ向き、揺れも残す）。
    /// 2026-11-18：酔い防止の設定を足した（設定 → VR の「SETSUNA：体だけ相手へ向く」「SETSUNA：揺れを止める」。どちらも最初はオフ）。
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

        /// <summary>
        /// 体の向き：ふつうはいつも相手のほう（回しても変わらない）。
        /// 設定「体だけ相手へ向く」（Vr.FightBodyOnly）のときは、視界は勝手に回らない。右スティックで回せて、
        /// 相手が正面から 70 度より外へ出たら、一気に相手のほうへ向きなおす（視界のふちを暗くして）。
        /// </summary>
        public float Yaw
        {
            get
            {
                float face = FaceYaw;
                if (!Vr.FightBodyOnly) return face;
                if (float.IsNaN(_vrYaw)) _vrYaw = face;
                if (Mathf.Abs(Mathf.DeltaAngle(_vrYaw, face)) > 70f) { _vrYaw = face; Vr.Flash(0.3f); }
                return _vrYaw;
            }
            set { if (Vr.FightBodyOnly) _vrYaw = value; }
        }
        float _vrYaw = float.NaN;

        float FaceYaw
        {
            get
            {
                if (P == null || C == null) return 0;
                var d = C.pos - P.pos; d.y = 0;
                return d.sqrMagnitude > 1e-4f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0;
            }
        }

        /// <summary>ラウンドの始めに、視界を相手のほうへ向けなおす。</summary>
        void VrRound() { _vrYaw = float.NaN; }

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
            _vrShake = Vr.FightNoShake ? Vector3.zero : Random.insideUnitSphere * _shake;
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
        static string VrKeys => $"左スティック 前後・回りこむ（うしろに倒したまま＝ガード）　左グリップ しゃがみ　{Vr.KeyJump} ジャンプ\n手を振る 攻撃（右を速く＝強）　右トリガー 弱・右グリップ 強　{Vr.KeyUse} 必殺　左トリガー 投げ　X（左スティック押しこみでも） 超必殺";
    }
}
