using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using F = Tsukuyomi.SetsunaFighter;

namespace Tsukuyomi
{
    /// <summary>
    /// SETSUNA：1対1 の一人称の格闘ゲーム（KASSEN のモード）。
    /// 相手を自動でロックオンし、W で近づく・S で下がる（押したままでガード）・A/D で回りこむ。
    /// 左クリック 弱・右クリック 強・E 必殺（S→W＋E 裂空突、W→S＋E 狐火、E だけ 月輪昇）・F 投げ・Ctrl しゃがみ・Space ジャンプ・R 超必殺。
    /// WW で踏みこみ、SS で後ろへ跳ぶ。攻撃が当たる瞬間にガードすると「刹那ガード」（時間がゆっくりになり、反撃のチャンス）。
    /// 3ラウンドのうち2本先取。1ラウンド 99 秒。表示は板（Board）で作る。
    /// </summary>
    public partial class Setsuna : MonoBehaviour
    {
        public static Setsuna I;
        enum Phase { Intro, Fight, RoundEnd, Result }

        const float ArenaR = 8.4f, RoundTime = 99f;
        static readonly float[] AiReact = { 0.45f, 0.28f, 0.16f }, AiBlock = { 0.25f, 0.55f, 0.8f }, AiJust = { 0f, 0.06f, 0.22f },
            AiAggr = { 0.35f, 0.55f, 0.72f }, AiAnti = { 0.1f, 0.35f, 0.7f }, AiThrow = { 0.05f, 0.15f, 0.25f }, AiSpecial = { 0.1f, 0.25f, 0.4f }, AiPunish = { 0.1f, 0.4f, 0.8f };
        static readonly int[] AiCombo = { 1, 2, 4 };

        F P, C;
        int _opp, _level, _round;
        Phase _phase;
        float _phaseT, _clock, _hitstop, _slowT, _shake;
        bool _paused, _frozen;
        Camera _cam;
        Transform _arena;
        AudioSource _audio;
        bool _clearSet;
        (CameraClearFlags, Color, bool) _clearKeep;

        // ───────── はじめる・おわる ─────────

        public static void Begin(Scene scene, int opp, int level, int stage = 0)
        {
            var go = new GameObject("SETSUNA");
            if (scene.IsValid()) SceneManager.MoveGameObjectToScene(go, scene);
            var s = go.AddComponent<Setsuna>();
            s._stage = stage < 0 ? Random.Range(0, StageNames.Length) : Mathf.Clamp(stage, 0, StageNames.Length - 1);
            s.Init(opp, level);
        }

        void Init(int opp, int level)
        {
            I = this;
            _opp = Mathf.Clamp(opp, 0, SetsunaStyles.Cpu.Length - 1);
            _level = Mathf.Clamp(level, 0, 2);
            _cam = Camera.main;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 0;
            EnsureSounds();
            P = new F { style = SetsunaStyles.Player, isPlayer = true };
            C = new F { style = SetsunaStyles.Cpu[_opp] };
            BuildArena();
            if (_stage == 2 && _cam != null)
            {
                // 月面：空を宇宙の黒に
                _clearKeep = (_cam.clearFlags, _cam.backgroundColor, RenderSettings.fog);
                _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.005f, 0.006f, 0.015f); RenderSettings.fog = false;
                _clearSet = true;
            }
            BuildCpuDoll();
            BuildArms();
            BuildHud();
            if (_cam != null) _cam.fieldOfView = 72f;
            VrInit();
            _round = 0;
            NextRound();
        }

        void OnDestroy()
        {
            if (I == this) { I = null; Vr.SimSwing = false; }
            VrEnd();
            foreach (var b in new[] { _hud, _pauseB, _resultB }) if (b != null) Destroy(b.gameObject);
            if (_armsRoot != null) Destroy(_armsRoot.gameObject);
            if (_clearSet && _cam != null) { _cam.clearFlags = _clearKeep.Item1; _cam.backgroundColor = _clearKeep.Item2; RenderSettings.fog = _clearKeep.Item3; }
            Time.timeScale = 1f;
        }

        void NextRound()
        {
            _round++;
            P.ResetRound(new Vector3(0, 0, -2.6f), Vector3.forward);
            C.ResetRound(new Vector3(0, 0, 2.6f), Vector3.back);
            P.gauge = Mathf.Min(P.gauge, 100); C.gauge = Mathf.Min(C.gauge, 100);
            _projs.ForEach(p => { if (p.tr) Destroy(p.tr.gameObject); });
            _projs.Clear();
            _clock = RoundTime;
            _comboP = _comboC = 0;
            SetPhase(Phase.Intro);
            VrRound();
            Center(_round == 3 ? "FINAL ROUND" : $"ROUND {_round}", Board.Teal, 1.6f, "");
        }

        void SetPhase(Phase p) { _phase = p; _phaseT = 0; }

        void Leave()
        {
            Kassen.EndMatch();
            Destroy(gameObject);
        }

        // ───────── 毎フレーム ─────────

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            if (_slowT > 0) { _slowT -= udt; if (_slowT <= 0 && !_paused) Time.timeScale = 1f; }
            Lock(_phase == Phase.Fight && !_paused && !Ts.ShotMode);

            if (QuitHoldTick()) return;
            if (_paused) { if (Act.Back.Down) Resume(); return; }
            if (_phase == Phase.Result) { HudTick(); return; }
            if (!Ts.ShotMode && Act.Back.Down && _phase != Phase.Result) { Pause(); return; }

            float dt = Time.deltaTime;
            _phaseT += dt;
            if (_frozen) { Visuals(0); HudTick(); return; }

            switch (_phase)
            {
                case Phase.Intro:
                    if (_phaseT > 1.6f && _phaseT - dt <= 1.6f) Center("FIGHT!", Board.Gold, 0.8f, "");
                    if (_phaseT > 2.2f) SetPhase(Phase.Fight);
                    break;
                case Phase.Fight:
                    _clock -= dt;
                    if (_clock <= 0) { _clock = 0; EndRound(true); }
                    break;
                case Phase.RoundEnd:
                    if (_phaseT > 3.2f) AfterRound();
                    break;
            }

            P.ClearInput(); C.ClearInput();
            if (_phase == Phase.Fight)
            {
                if (!Ts.ShotMode || _testPlay) ReadPlayer();
                Brain();
            }

            if (_hitstop > 0) _hitstop -= udt;
            else
            {
                Step(P, C, dt); Step(C, P, dt);
                Resolve(P, C); Resolve(C, P);
                Projectiles(dt);
                Separate();
            }
            Visuals(dt);
            HudTick();
        }

        void Lock(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // ───────── 自分の入力 ─────────

        float _tFwd = -9, _tBack = -9, _prevY;
        string _buf; float _bufT;

        void ReadPlayer()
        {
            float now = Time.time;
            P.inMove = Act.Move.ReadValue<Vector2>();
            P.inCrouch = Act.Crouch.Held;
            float y = P.inMove.y;
            if (y > 0.5f && _prevY <= 0.5f) { if (now - _tFwd < 0.25f) P.inDashF = true; _tFwd = now; }
            if (y < -0.5f && _prevY >= -0.5f) { if (now - _tBack < 0.25f) P.inDashB = true; _tBack = now; P.lastGuardPress = now; }
            _prevY = y;
            if (Act.Crouch.Down) P.lastGuardPress = now;

            string press = Act.Super.Down ? "super" : Act.Throw.Down ? "throw" : Act.Special.Down ? SpecialFromCommand(now) :
                           Act.Heavy.Down ? "heavy" : Act.Light.Down ? "light" : Act.Jump.Down ? "jump" : VrSwing(); // VR：手を振っても攻撃
            if (press != null) { _buf = press; _bufT = now; }
            if (_buf != null && now - _bufT > 0.16f) _buf = null;
            if (_buf != null) Press(P, _buf);
        }

        string SpecialFromCommand(float now)
        {
            if (_tBack < _tFwd && now - _tBack < 0.5f && now - _tFwd < 0.35f) return "rush";
            if (_tFwd < _tBack && now - _tFwd < 0.5f && now - _tBack < 0.35f) return "fox";
            return "upper";
        }

        static void Press(F f, string a)
        {
            switch (a)
            {
                case "super": f.inSuper = true; break;
                case "throw": f.inThrow = true; break;
                case "rush": f.inSpecial = true; f.inSpecialKind = 1; break;
                case "fox": f.inSpecial = true; f.inSpecialKind = 2; break;
                case "upper": f.inSpecial = true; f.inSpecialKind = 0; break;
                case "heavy": f.inHeavy = true; break;
                case "light": f.inLight = true; break;
                case "jump": f.inJump = true; break;
            }
        }

        void Consumed(F f) { if (f == P) _buf = null; }

        // ───────── 動き ─────────

        static Vector3 Right(F f) => Vector3.Cross(Vector3.up, f.face);

        void Step(F f, F o, float dt)
        {
            f.t += dt;
            if (f.invuln > 0) f.invuln -= dt;
            var d = o.pos - f.pos; d.y = 0;
            if (d.sqrMagnitude > 0.0004f && f.st != F.St.KO && f.st != F.St.Down) f.face = d.normalized;
            float sp = f.style.speed;
            if (f.st != F.St.Idle) f.guard = false;

            switch (f.st)
            {
                case F.St.Idle:
                    f.crouch = f.inCrouch;
                    f.guard = f.inMove.y < -0.5f || (f.crouch && f.inMove.y < -0.2f) || (f.jgReady && !f.isPlayer);
                    if (TryActions(f)) break;
                    if (f.inJump) { Jump(f); Consumed(f); break; }
                    if (f.inDashF && !f.crouch) { f.st = F.St.Dash; f.t = 0; break; }
                    if (f.inDashB && !f.crouch) { f.st = F.St.Backstep; f.t = 0; f.invuln = 0.12f; break; }
                    if (!f.crouch)
                    {
                        var v = f.face * f.inMove.y * (f.inMove.y > 0 ? 2.7f : 2.1f) + Right(f) * f.inMove.x * 2.3f;
                        f.pos += v * sp * dt;
                    }
                    break;
                case F.St.Dash:
                    f.pos += f.face * 7.2f * sp * dt;
                    if (f.t > 0.26f) { f.st = F.St.Idle; f.t = 0; }
                    else TryActions(f);
                    break;
                case F.St.Backstep:
                    f.pos -= f.face * 6.4f * sp * dt;
                    if (f.t > 0.3f) { f.st = F.St.Idle; f.t = 0; }
                    break;
                case F.St.Attack:
                    var m = f.move;
                    if (m.travel > 0 && f.Phase < 2 && !f.moveHit && f.t > m.startup * 0.5f) f.pos += f.face * m.travel * sp * dt;
                    if (m.air || f.Airborne) Fall(f, dt);
                    if (m.projectile && f.superHits == 0 && f.t >= m.startup) { Shoot(f); f.superHits = 1; }
                    if (m == SetsunaMoves.Super && f.Phase == 1) { _shake = Mathf.Max(_shake, 0.04f); }
                    float total = m.startup + m.active + m.recovery + f.stun;
                    if (f.CanCancel()) TryActions(f, true);
                    if (f.st == F.St.Attack && f.t >= total) { f.st = f.Airborne ? F.St.Air : F.St.Idle; f.t = 0; f.move = null; f.stun = 0; }
                    break;
                case F.St.Hitstun:
                case F.St.Blockstun:
                    f.stun -= dt;
                    if (f.stun <= 0) { f.st = F.St.Idle; f.t = 0; }
                    break;
                case F.St.Air:
                    Fall(f, dt);
                    f.pos += f.airVel * dt;
                    if (!f.airAttackUsed && (f.inLight || f.inHeavy))
                    {
                        f.airAttackUsed = true;
                        f.StartMove(f.inHeavy ? SetsunaMoves.JHeavy : SetsunaMoves.JLight);
                        Consumed(f);
                        Whoosh();
                    }
                    break;
                case F.St.Juggle:
                    f.pos += f.airVel * dt;
                    if (Fall(f, dt)) { f.st = F.St.Down; f.stun = 0.75f; f.t = 0; f.juggle = 0; Thud(); }
                    break;
                case F.St.Down:
                    if (f.y > 0) Fall(f, dt);
                    f.stun -= dt;
                    if (f.stun <= 0 && f.hp > 0) { f.st = F.St.Idle; f.t = 0; f.invuln = 0.4f; }
                    break;
                case F.St.KO:
                    if (f.y > 0) Fall(f, dt);
                    break;
            }
            // 闘技場の外へは出ない
            var flat = f.pos; flat.y = 0;
            if (flat.magnitude > ArenaR) f.pos = flat.normalized * ArenaR;
        }

        /// <summary>落ちる。着地したら true。</summary>
        bool Fall(F f, float dt)
        {
            if (f.y <= 0 && f.vy <= 0) { f.y = 0; f.vy = 0; return false; }
            f.vy -= _grav * dt;
            f.y += f.vy * dt;
            if (f.y > 0) return false;
            f.y = 0; f.vy = 0; f.airVel = Vector3.zero;
            if (f.st == F.St.Air) { f.st = F.St.Idle; f.t = 0; }
            if (f.st == F.St.Attack && f.move != null && f.move.air) { f.st = F.St.Idle; f.t = 0; f.move = null; }
            return true;
        }

        void Jump(F f)
        {
            f.st = F.St.Air; f.t = 0; f.vy = 7.2f * Mathf.Sqrt(_grav / 22f) * (_grav < 20 ? 1.25f : 1f); f.y = 0.01f; f.airAttackUsed = false; f.crouch = false;
            f.airVel = (f.face * f.inMove.y + Right(f) * f.inMove.x) * 2.8f * f.style.speed;
        }

        /// <summary>技を出す（出せたら true）。cancel は技の途中からつなげるとき。</summary>
        bool TryActions(F f, bool cancel = false)
        {
            SetsunaMove m = null;
            if (f.inSuper && f.gauge >= 100) m = SetsunaMoves.Super;
            else if (f.inThrow && !cancel) m = SetsunaMoves.Grab;
            else if (f.inSpecial)
            {
                int k = f.inSpecialKind;
                if (k == 2 && !f.style.fox) k = 1;
                m = k == 1 ? SetsunaMoves.Rush : k == 2 ? SetsunaMoves.Fox : SetsunaMoves.Upper;
            }
            else if (f.inHeavy) m = f.crouch || (cancel && f.move != null && f.move.h == Height.Low) ? SetsunaMoves.CHeavy : SetsunaMoves.Heavy;
            else if (f.inLight)
            {
                if (f.crouch || (cancel && f.move == SetsunaMoves.CLight)) m = SetsunaMoves.CLight;
                else m = cancel && f.move == SetsunaMoves.Jab ? SetsunaMoves.Jab2 : SetsunaMoves.Jab;
            }
            if (m == null) return false;
            if (m == SetsunaMoves.Fox && _projs.Exists(p => p.owner == f)) return false;
            if (cancel && !f.CanCancelInto(m.id)) return false;
            f.StartMove(m);
            Consumed(f);
            if (m == SetsunaMoves.Super) SuperFlash(f);
            else Whoosh();
            return true;
        }
    }
}
