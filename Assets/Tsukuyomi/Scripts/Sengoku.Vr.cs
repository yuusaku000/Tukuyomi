using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の VR 対応。
    ///  ・頭の動きで見回し、右スティックで体を回す（Vr の「体」になる）。左スティックで歩く向きは、頭の向き。
    ///  ・ねらい（飛び道具・ワイヤー・鎖）は右手の向き。手の先から細い光の線（照準）を出す。
    ///  ・近接の武器（二刀流・ハンマー・傘・鎖鎌）は右手を振っても攻撃。左手を振ると殴る。当たるとコントローラーがふるえる。
    ///  ・手の武器の見た目は右手に持つ（二刀流の左の刀は左手）。
    ///  ・画面いっぱいの表示の板は、VR では目の前 1m に小さく浮かべる。
    /// ボタン：右トリガー 攻撃・右グリップ 飛び道具／狙う・A 移動わざ・B ジャンプ・左トリガー 殴る・左グリップ 走る・X 戦況・Y 一時停止。
    /// </summary>
    public partial class Sengoku : Vr.IBody
    {
        bool VrOn => Vr.On && Vr.RightHand != null;
        bool _testPlay, _stickHeld;

        // ───────── Vr.IBody ─────────

        public Vector3 Feet => _rig != null ? _rig.transform.position : Vector3.zero;
        public float Yaw { get => _yaw; set => _yaw = value; }
        public void Carry(Vector3 shift) { if (_cc != null && _cc.enabled) _cc.Move(shift); }
        public bool Pointer => _paused || (_match && (_phase == MPhase.Select || _phase == MPhase.Result));
        // VR では倒れたあとも観戦カメラにしない（ゲームが視界を動かすと、頭の動きがつかず酔うため）。その場で復活を待つ
        public bool CameraFree => _camOverride || (_match && _dead && !VrOn);

        void VrInit()
        {
            if (!Vr.On) return;
            Vr.Body = this;
            // 表示の板：目の前 1m に（1920 ピクセルで 1.34m。体力などは視界のはしの近くに来る）
            if (_hud != null) { _hud.worldScale = 0.0007f; _hud.worldOffset = new Vector3(0, -0.03f, 1.0f); _hud.follow = 12f; _hud.Apply(); }
            VrFitWeapons();
            VrHudLean(false);
        }

        bool _vrInfo;

        /// <summary>
        /// VR では、ずっと出ている表示（武器の名前・説明・体力・操作・目印など）は出さない（2026-11-18 ユーザーの指示）。
        /// 残すのは、まん中のお知らせ・ダメージの数字・当たった印・赤いふち・倒れたときの「復活まで」・移動わざのゲージだけ。
        /// ほかの情報は X で開く戦況の板にある（練習場は X で表示を出し入れ）。showAll で全部出す。
        /// </summary>
        void VrHudLean(bool showAll)
        {
            if (!VrOn || _hud == null) return;
            var keep = new HashSet<Transform>();
            // 移動わざのゲージ（燃料など）も出す（2026-11-18 ユーザーの指示。手もとを見なくても残りがわかるように）
            foreach (var c in new Component[] { _mCenter, _mSub, _hitX, _hurtImg, _mLives, _gaugeBg, _gaugeImg, _gaugeLabel }) if (c != null) keep.Add(c.transform);
            var dmg = new HashSet<Transform>();
            foreach (var d in _dmg) if (d.t != null) dmg.Add(d.t.transform);
            foreach (Transform c in _hud.root)
            {
                if (c.name == "まん中") { c.gameObject.SetActive(true); foreach (Transform cc in c) cc.gameObject.SetActive(showAll || dmg.Contains(cc)); continue; }
                c.gameObject.SetActive(showAll || keep.Contains(c));
            }
        }

        void VrEnd()
        {
            if (ReferenceEquals(Vr.Body, this)) Vr.Body = null;
            if (_aimLine != null) Destroy(_aimLine.gameObject);
            if (_leftHold != null) Destroy(_leftHold.gameObject);
        }

        /// <summary>頭の位置（VR）。</summary>
        Vector3 VrHead => Vr.OriginPos(null) + Vr.OriginRot(null) * Vr.HeadPos;

        /// <summary>飛び道具などを出す元（VR では右手の少しうしろ。もとの式の「＋Aim×0.7」で手の先になる）。</summary>
        Vector3 From => VrOn ? Vr.RightHand.position - Aim * 0.55f : EyePos;

        /// <summary>画面モードで、飛び道具を少し右から出すずれ（VR では手から出すので無し）。</summary>
        Vector3 Side(float k) => VrOn ? Vector3.zero : Right * k;

        /// <summary>近接攻撃の向き（VR では頭の向き。振った手の向きはばらつくので）。</summary>
        Vector3 MeleeAim => VrOn && _cam != null ? _cam.transform.forward : Aim;

        // ───────── 手の武器 ─────────

        Transform _leftHold;

        /// <summary>VR で手に持つ向き（武器ごと。x を倒すと、上を向いていた柄や刃が前を向く）。</summary>
        static readonly Vector3[] VrGrip = { new(70, 0, 0), new(60, 0, 0), new(90, 0, 0), new(0, 0, 0), new(65, 0, 0), new(60, 0, 0) };

        void VrFitWeapons()
        {
            if (_viewRoot == null || !VrOn) return;
            // 画面モードではカメラの右下に置いていた武器を、右手に持たせる（手の前に柄がくるように、向きも武器ごとに直す）
            _viewRoot.SetParent(Vr.RightHand, false);
            _viewRoot.localPosition = Vector3.zero;
            _viewRoot.localRotation = Quaternion.identity;
            for (int i = 0; i < _viewModels.Length; i++)
            {
                if (_viewModels[i] == null) continue;
                var m = _viewModels[i].transform;
                for (int c = m.childCount - 1; c >= 0; c--)
                {
                    var item = m.GetChild(c);
                    if (item.name == "分銅") { item.localPosition = new Vector3(-0.08f, -0.05f, 0.02f); continue; }
                    item.localPosition = new Vector3(0, 0, 0.03f);
                    item.localEulerAngles = VrGrip[i];
                    // 二刀流の左の刀は左手に
                    if (item.name == "左の刀" && Vr.LeftHand != null)
                    {
                        _leftHold = new GameObject("左手の刀").transform;
                        _leftHold.SetParent(Vr.LeftHand, false);
                        item.SetParent(_leftHold, false);
                    }
                }
            }
        }

        void VrWeaponsShow()
        {
            if (_leftHold != null) _leftHold.gameObject.SetActive(_viewRoot != null && _viewRoot.gameObject.activeInHierarchy && _w == 0);
        }

        // ───────── 振る攻撃 ─────────

        float _swingCdR, _swingCdL;
        const float SwingSpeed = 2.2f; // m/秒。これより速く振ったら攻撃

        /// <summary>右手を振った（近接の武器のとき、左クリックの代わり）。</summary>
        bool SwingRight(float dt)
        {
            if (_testPlay) Vr.SimSwing = true; // 確認用：前の試合の片づけが遅れて消しても、振りつづける
            _swingCdR -= dt;
            if (!VrOn || _swingCdR > 0 || Vr.RightVel.magnitude < SwingSpeed) return false;
            _swingCdR = 0.32f;
            if (Ts.ShotMode) Debug.Log($"[SENGOKU] 右手を振った {Vr.RightVel.magnitude:0.0}m/秒 武器={Weapons[_w].name}");
            return _w == 0 || _w == 1 || _w == 2 || _w == 4 || _w == 5; // 近接の武器（弓だけは振っても出ない）
        }

        /// <summary>左手を振った（殴る）。</summary>
        bool SwingLeft(float dt)
        {
            _swingCdL -= dt;
            if (!VrOn || _swingCdL > 0 || Vr.LeftVel.magnitude < SwingSpeed) return false;
            _swingCdL = 0.45f;
            return true;
        }

        // ───────── 照準の光の線 ─────────

        LineRenderer _aimLine;

        void AimLineTick(bool play)
        {
            if (!VrOn) return;
            if (_aimLine == null)
            {
                var go = new GameObject("照準の光");
                go.transform.SetParent(transform, false);
                _aimLine = go.AddComponent<LineRenderer>();
                _aimLine.useWorldSpace = true; _aimLine.positionCount = 2;
                _aimLine.startWidth = 0.008f; _aimLine.endWidth = 0.02f;
                _aimLine.material = Mat(new Color(0.4f, 1f, 1f), true);
                _aimLine.startColor = new Color(0.4f, 1f, 1f, 0.7f); _aimLine.endColor = new Color(0.4f, 1f, 1f, 0.05f);
                _aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            bool on = (play || Ts.ShotMode) && !Pointer && !_dead && !_camOverride;
            _aimLine.enabled = on;
            if ((Pointer || _dead) && _viewRoot != null) _viewRoot.gameObject.SetActive(false); // 板を押す場面・倒れている間は、手の武器を隠す
            VrWeaponsShow();
            NockTick(on);
            if (!on) return;
            var o = Vr.RightHand.position + Aim * 0.15f;
            float len = 40f;
            if (Physics.Raycast(o, Aim, out var h, len, ~0, QueryTriggerInteraction.Ignore) && h.collider != _cc) len = h.distance;
            _aimLine.SetPosition(0, o); _aimLine.SetPosition(1, o + Aim * len);
            // 弓をためている間は、照準の線を太く・金色に近づける（どれだけたまったか、手もとを見なくてもわかるように）
            float c = _w == 3 ? _charge : _w == 2 ? _charge : 0;
            var col = Color.Lerp(new Color(0.4f, 1f, 1f), new Color(1f, 0.85f, 0.35f), c);
            _aimLine.startColor = new Color(col.r, col.g, col.b, 0.7f + 0.3f * c); _aimLine.endColor = new Color(col.r, col.g, col.b, 0.05f + 0.25f * c);
            _aimLine.startWidth = 0.008f + 0.012f * c; _aimLine.endWidth = 0.02f + 0.03f * c;
        }

        // ───────── VR の弓：つがえた矢 ─────────

        Transform _nock;

        /// <summary>
        /// VR で弓をためている間、右手につがえた矢を見せる（2026-11-18：VR では弓の表示がわかりにくかったため）。
        /// ためるほど手前へ引かれ、光が強くなり、ためきると金色になる。
        /// </summary>
        void NockTick(bool on)
        {
            bool show = on && _w == 3 && _charge > 0;
            if (!show) { if (_nock != null) _nock.gameObject.SetActive(false); return; }
            if (_nock == null)
            {
                _nock = new GameObject("つがえた矢").transform;
                _nock.SetParent(transform, false);
            }
            bool full = _charge > 0.9f;
            if (!_nock.gameObject.activeSelf || _nockFull != full)
            {
                for (int i = _nock.childCount - 1; i >= 0; i--) Destroy(_nock.GetChild(i).gameObject);
                var tr = _nock.GetComponent<TrailRenderer>(); if (tr != null) Destroy(tr);
                var arrow = new GameObject("矢").transform; arrow.SetParent(_nock, false);
                BuildArrow(arrow, 1, full);
                var t2 = arrow.GetComponent<TrailRenderer>(); if (t2 != null) Destroy(t2); // 手に持っている間は尾を出さない
                _nockFull = full;
                _nock.gameObject.SetActive(true);
            }
            _nock.position = Vr.RightHand.position + Aim * (0.35f - 0.3f * _charge);
            _nock.rotation = Quaternion.LookRotation(Aim);
            _nock.localScale = Vector3.one * (0.8f + 0.3f * _charge);
        }
        bool _nockFull;

        // ───────── 表示の板の上の位置 ─────────

        /// <summary>
        /// 世界の点を、表示の板（1920×1080）のまん中からのピクセルに直す（x 右・y 上）。z が負なら、うしろ（向きだけ）。
        /// 画面モードは画面に写した位置、VR は頭から見て板の上に重なる位置。
        /// </summary>
        Vector3 HudUi(Vector3 world)
        {
            if (VrOn && _hud != null && _hud.canvas.renderMode == RenderMode.WorldSpace)
            {
                var ct = _hud.canvas.transform; var head = _cam.transform.position;
                var d = world - head; float fz = Vector3.Dot(d, ct.forward);
                if (fz <= 0.05f) return new Vector3(Vector3.Dot(d, ct.right) * 1000f, Vector3.Dot(d, ct.up) * 1000f, -1);
                var ray = new Ray(head, d);
                new Plane(ct.forward, ct.position).Raycast(ray, out var t);
                var lp = ct.InverseTransformPoint(ray.GetPoint(t));
                return new Vector3(lp.x, lp.y, fz);
            }
            var sp = _cam.WorldToScreenPoint(world);
            float s = _hud != null && _hud.canvas.scaleFactor > 0 ? _hud.canvas.scaleFactor : Screen.height / 1080f; // 板の実際の縮み具合
            var ui = new Vector2((sp.x - Screen.width / 2f) / s, (sp.y - Screen.height / 2f) / s);
            if (sp.z < 0) ui = -ui;
            return new Vector3(ui.x, ui.y, sp.z);
        }

        /// <summary>板の横の半分（ピクセル）。</summary>
        float HudHalfW => VrOn || _hud == null ? 960f : Mathf.Min(960f, ((RectTransform)_hud.canvas.transform).rect.width / 2f);

        // ───────── VR の文字 ─────────

        /// <summary>キーの説明を、VR のコントローラーの言い方に直す。</summary>
        static string VrText(string s)
        {
            if (!Vr.On || s == null) return s;
            return s.Replace("左クリック", "右トリガー（近接は右手を振っても）").Replace("右クリック", "右グリップ").Replace("E 長押し", Vr.KeyUse + " 長押し").Replace("E ", Vr.KeyUse + " ")
                    .Replace("Space", "B").Replace("F 殴る", "左トリガー・左手を振る 殴る").Replace("F でも殴れる", "左手を振っても殴れる");
        }

        static string VrKeysPractice => $"左スティック 移動　右スティック 左右で回る・上下で武器　{Vr.KeyJump} ジャンプ　Y 一時停止（長押しで島へ）";
        static string VrKeys => $"左スティック 移動　左グリップ 走る　右スティック 回る　{Vr.KeyJump} ジャンプ　左トリガー・左手を振る 殴る　X 戦況　Y 一時停止（長押しで降参）";
    }
}
