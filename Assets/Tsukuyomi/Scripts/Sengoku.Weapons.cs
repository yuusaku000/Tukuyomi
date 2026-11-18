using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の6つの武器。左クリック＝攻撃、右クリック＝飛び道具／投げる・置く、E＝移動わざ。
    ///  1 二刀流（彩葉）：連撃・先端から光弾・E でワイヤー（引っかけて振り子で飛ぶ）
    ///  2 ハンマー（かぐや）：ためて叩きつけ・ミサイル・E 長押しでジェット（燃料あり。回復は約4秒）
    ///  3 傘（ヤチヨ）：突き・ブーメランのように投げる・E で乗物に乗って浮かんですべる（前を守る）
    ///  4 弓（乃衣）：長押しでためて射る・右で狙う（ズーム）・E で自分が矢になって飛ぶ
    ///  5 金棒（帝）：金色の金棒。重い一撃・右で金の弾を撃つ・E で大きく振って、その方向へ大きく跳ぶ（弓の移動の強い版）
    ///  6 団扇（雷）：大きな団扇。少し遠くまで届く・右で地雷を置く・E であおいで反対方向へ飛ぶ（前の相手は風で押す）
    ///    地雷：100 ダメージで壊れる・2m まで近づかないと見えない・踏むと 50 ダメージ＋15m 打ち上げ＋3秒動けない（Sengoku.Mines）
    /// （2026-11-18 ユーザーの指示で、大筒と鎖鎌を金棒と団扇に置きかえた）
    /// </summary>
    public partial class Sengoku
    {
        public class Weapon { public string name, en, hint; }

        public static readonly Weapon[] Weapons =
        {
            new() { name = "二刀流", en = "TWIN BLADES", hint = "左クリック 連撃（3回目が強い）\n右クリック 先端から光弾\nF 殴る　E ワイヤー（引っかけるとぐっと引き寄せる・Space 長押しでさらに引き寄せ・離すと振り子・もう一度 E で離す。勢いは残るので空中で次へ）" },
            new() { name = "ハンマー", en = "HAMMER", hint = "左クリック 叩きつけ（重い一撃・吹き飛ばす）\n右クリック ミサイル（爆発）\nE 長押し ジェットで飛ぶ（燃料は地面で回復）" },
            new() { name = "傘", en = "UMBRELLA", hint = "左クリック 突き\n右クリック ブーメランのように投げる（長押しでためると遠く・強く。もどってくる）\nE 乗物（吹き抜けの駕籠）に乗って浮かぶ（すべって速い・水の上も渡れる・前の攻撃を防ぐ・Space でおりる）" },
            new() { name = "弓", en = "BOW", hint = "左クリック 長押しでためて、離して射る\n右クリック 長押しで狙う（ズーム）\nE 自分が矢になって飛ぶ（当たると痛い）" },
            new() { name = "金棒", en = "KANABO", hint = "左クリック 振りおろす（重い一撃・吹き飛ばす）\n右クリック 金の弾を撃つ\nE 大きく振って、ねらった方へ大きく跳ぶ（当たると痛い）" },
            new() { name = "団扇", en = "THUNDER FAN", hint = "左クリック あおぎ打ち（少し遠くまで届く・押し返す）\n右クリック 地雷を置く（3つまで。2m まで近づかないと見えない）\nE あおいで、反対方向へ飛ぶ　Space 空中でもう一度跳べる（2段ジャンプ）" },
        };

        int _w;
        float _cd, _cd2, _abilityCd, _swing, _swingLen = 0.3f, _charge, _fuel = 1f;
        int _combo;
        bool _wire, _ride, _pull, _zoom, _hammerWind;
        float _zipT; // ワイヤーが刺さった直後の、引き寄せの残り時間
        Vector3 _anchor, _pullTo; float _ropeLen, _dashT, _pullT, _windT;
        Vector3 _dashDir;
        readonly HashSet<SengokuTarget> _dashHit = new HashSet<SengokuTarget>();
        Shot _boomer;

        float SpeedMul => _w == 4 ? 0.92f : 1f;
        float _dashSpeed = 34f, _dashDmg = 70f, _dashKeep = 0.3f;
        float FovAdd => _zoom ? (_w == 3 ? -40f : -30f) : (_dashT > 0 ? 12f : 0f) + (_w == 1 && Act.Special.Held && _fuel > 0 ? 6f : 0f);

        float Gauge01 => _w switch
        {
            0 => _wire ? 1 : WireTarget(out _) ? 1 : 0.15f,
            1 => _fuel,
            2 => _charge > 0 ? _charge : 1,
            3 => _charge > 0 ? _charge : 1 - Mathf.Clamp01(_abilityCd / 3f),
            4 => 1 - Mathf.Clamp01(_abilityCd / 3.5f),
            _ => 1 - Mathf.Clamp01(_abilityCd / 2.2f),
        };

        string GaugeText => _w switch
        {
            0 => _wire ? (Vr.On ? $"ワイヤー中（{Vr.KeyJump} 長押しで引き寄せ・{Vr.KeyUse} で離す）" : "ワイヤー中（Space 長押しで引き寄せ・E で離す）") : WireTarget(out _) ? "E ワイヤー（届く）" : "ワイヤーが届かない",
            1 => $"燃料 {_fuel * 100:0}%",
            2 => _charge > 0 ? $"傘をためる {_charge * 100:0}%（離して投げる）" : _ride ? "傘で浮かんでいる（Space でおりる）" : "E で傘を開く",
            3 => _charge > 0 ? $"ため {_charge * 100:0}%" : _abilityCd > 0 ? "矢になる：準備中" : "E 矢になって飛ぶ",
            4 => _abilityCd > 0 ? "大振り：準備中" : "E 大きく振って跳ぶ",
            _ => (_abilityCd > 0 ? "あおぐ：準備中" : "E あおいで飛ぶ") + $"　地雷 {MinesOf(1)}/{MaxMines}",
        };

        void Select(int i)
        {
            EndAbilities();
            _w = Mathf.Clamp(i, 0, Weapons.Length - 1);
            for (int k = 0; k < _viewModels.Length; k++) if (_viewModels[k] != null) _viewModels[k].SetActive(k == _w);
            _charge = 0; _zoom = false; _swing = 0; _combo = 0;
            if (_wTag != null) _wTag.text = Board.Vertical(Weapons[_w].name);
            Play(_sClick, 0.5f);
        }

        void EndAbilities()
        {
            _wire = false; _ride = false; _pull = false; _dashT = 0; _hammerWind = false;
            if (_rope != null) _rope.gameObject.SetActive(false);
        }

        // ───────── 入力 ─────────

        void WeaponInput(float dt)
        {
            _cd -= dt; _cd2 -= dt; _abilityCd -= dt;
            // F：どの武器でも殴れる（大将落としは近接攻撃でしか落とせない）
            bool swingR = SwingRight(dt), swingL = SwingLeft(dt); // VR：手を振った
            if ((Act.Throw.Down || swingL) && _cd <= 0 && !_hammerWind) { Melee(25, 2.4f, false); Swing(0.25f); _cd = 0.45f; }
            bool L = Act.Light.Down || swingR, LH = Act.Light.Held, LU = Act.Light.Up, R = Act.Heavy.Down, RH = Act.Heavy.Held, RU = Act.Heavy.Up, E = Act.Special.Down && _stunT <= 0;
            switch (_w)
            {
                case 0: // 二刀流
                    if (L && _cd <= 0) { _combo = (_combo + 1) % 3; bool big = _combo == 0; Melee(big ? 45 : 28, 2.5f, big); Swing(big ? 0.38f : 0.24f); _cd = big ? 0.42f : 0.24f; }
                    if (R && _cd2 <= 0) { Fire(From + Aim * 0.7f + Side(0.15f), Aim * 55f, 0, 14, 0, false, ShotKind.Spark); _cd2 = 0.22f; Swing(0.15f); }
                    // ワイヤー：E で引っかける・もう一度 E で離して跳ぶ（押しっぱなしにしなくてよい。Shift と同時に E を押しつづけられないキーボードでも切れないように）
                    // 離す：勢いはそのまま、少し上へ（続けて次の所へ引っかけられる）
                    if (E && _wire) { _wire = false; var fl = new Vector3(_vel.x, 0, _vel.z); _vel += Vector3.up * 5f + fl.normalized * Mathf.Min(4f, fl.magnitude * 0.15f); Play(_sJump, 0.5f); }
                    else if (E && WireTarget(out var hit))
                    {
                        _wire = true; _anchor = hit.point; _ropeLen = Vector3.Distance(_rig.transform.position + Vector3.up, _anchor);
                        _zipT = 0.4f; // 刺さった直後は、ぐっと引き寄せる
                        _grounded = false; if (_vel.y < 2f) _vel.y = 2f;
                        Play(_sWhoosh, 0.6f);
                    }
                    else if (E) { ShowText(EyePos + Aim * 4f, "ワイヤーが届かない", Color.white); Play(_sClick, 0.4f); }
                    break;
                case 1: // ハンマー
                    if (L && _cd <= 0 && !_hammerWind) { _hammerWind = true; _windT = 0.32f; Swing(0.7f); }
                    if (_hammerWind) { _windT -= dt; if (_windT <= 0) { _hammerWind = false; Melee(90, 3f, true, 9f); Kick(-4f); _cd = 0.55f; Boom(EyePos + MeleeAim * 2f + Vector3.down * 0.6f, 0.8f, new Color(1f, 0.7f, 0.4f)); } }
                    if (R && _cd2 <= 0) { Fire(From + Aim * 0.9f + Side(0.3f), Aim * 26f, 0, 0, 60, true, ShotKind.Missile); _cd2 = 1.2f; Kick(2f); }
                    break;
                case 2: // 傘
                    if (L && _cd <= 0) { Melee(35, 2.7f, false); Swing(0.3f); _cd = 0.38f; }
                    // 右を長押しでためる（最大1秒）。離すと投げる。ためるほど遠くまで飛び、強い（ユーザーの指示）
                    if (RH && _boomer == null) _charge = Mathf.Min(1f, _charge + dt);
                    if (RU && _boomer == null)
                    {
                        float c = _charge; _charge = 0;
                        _boomer = Fire(From + Aim * 0.8f, Aim * (28f + 18f * c), 0, 30 + 40 * c, 0, c > 0.9f, ShotKind.Boomerang);
                        _boomer.boomerang = true; _boomer.outT = 0.6f + 0.7f * c;
                        Swing(0.3f + 0.2f * c); Kick(0.5f + 1.5f * c);
                    }
                    if (E) { _ride = !_ride; if (_ride) { _vel.y = Mathf.Max(_vel.y, 3f); } Play(_sWhoosh, 0.5f); }
                    break;
                case 3: // 弓
                    if (LH && _cd <= 0) _charge = Mathf.Min(1f, _charge + dt);
                    if (LU && _charge > 0) { Fire(From + Aim * 0.7f, Aim * (40f + 55f * _charge), 9f, 25 + 75 * _charge, 0, _charge > 0.9f, ShotKind.Arrow); _charge = 0; _cd = 0.3f; Kick(1.2f); }
                    _zoom = RH;
                    if (E && _abilityCd <= 0) { _dashT = 0.55f; _dashDir = Aim; _dashSpeed = 34f; _dashDmg = 70f; _dashKeep = 0.3f; _dashHit.Clear(); _abilityCd = 3f; Play(_sWhoosh, 0.8f); }
                    break;
                case 4: // 金棒（帝）
                    if (L && _cd <= 0) { Melee(70, 3f, true, 6f); Swing(0.45f); Kick(-3f); _cd = 0.8f; }
                    if (R && _cd2 <= 0) { Fire(From + Aim * 0.9f + Side(0.25f), Aim * 70f, 0, 16, 0, false, ShotKind.Bullet); _cd2 = 0.32f; Kick(0.8f); }
                    if (E && _abilityCd <= 0)
                    {
                        // 大きく振って、ねらった方へ跳ぶ（弓の「矢になる」より速く・長く・痛い。少し上向きに）
                        _dashT = 0.7f; _dashDir = (Aim + Vector3.up * 0.22f).normalized; _dashSpeed = 40f; _dashDmg = 85f; _dashKeep = 0.5f;
                        _dashHit.Clear(); _abilityCd = 3.5f; Swing(0.6f); Play(_sWhoosh, 0.9f);
                    }
                    break;
                case 5: // 団扇（雷）
                    if (L && _cd <= 0) { Melee(30, 4.2f, false, 6f); Swing(0.32f); _cd = 0.45f; Sparks(EyePos + MeleeAim * 2.5f, new Color(1f, 0.95f, 0.4f), 6); }
                    if (R && _cd2 <= 0) { if (PlaceMine(FeetAhead(1.2f), 1)) Swing(0.25f); _cd2 = 0.8f; }
                    if (E && _abilityCd <= 0)
                    {
                        // あおいで、反対方向へ飛ぶ（前の相手は風で押す）
                        Melee(8, 4.5f, false, 9f); Swing(0.4f);
                        _vel = -Aim * 18f + Vector3.up * 8f; _grounded = false; _abilityCd = 2.2f; Kick(-6f); Play(_sWhoosh, 0.9f);
                        Sparks(EyePos + Aim * 2f, new Color(1f, 0.95f, 0.5f), 10);
                    }
                    break;
            }
        }

        /// <summary>ワイヤーが届く長さ（2026-11-18 ユーザーの指示で 55m から伸ばした）。</summary>
        const float WireRange = 75f;

        bool WireTarget(out RaycastHit hit)
        {
            hit = default;
            if (_rig == null) return false;
            float floor = _rig.transform.position.y - 2f;
            bool Ok(RaycastHit h) => h.collider != _cc && h.point.y > floor && h.collider.GetComponentInParent<SengokuTarget>() == null;
            if (Physics.Raycast(From, Aim, out hit, WireRange, ~0, QueryTriggerInteraction.Ignore) && Ok(hit)) return true;
            // ねらいの手助け（2026-11-18）：太めの線で、それでもだめなら少し上・左右にずらした向きでさがす
            if (Physics.SphereCast(From, 0.8f, Aim, out hit, WireRange, ~0, QueryTriggerInteraction.Ignore) && Ok(hit)) return true;
            var up = Vector3.up; var side = Vector3.Cross(up, Aim).normalized;
            foreach (var (a, b) in new[] { (5f, 0f), (0f, 5f), (0f, -5f), (9f, 0f), (5f, 6f), (5f, -6f) })
            {
                var dir = Quaternion.AngleAxis(-a, side) * Quaternion.AngleAxis(b, up) * Aim;
                if (Physics.Raycast(From, dir, out hit, WireRange, ~0, QueryTriggerInteraction.Ignore) && Ok(hit)) return true;
            }
            return false;
        }

        // ───────── 移動わざ（ふつうの歩きの代わりに動かす）─────────

        bool AbilityMove(bool play, float dt)
        {
            Vector2 mv = play ? Act.Move.ReadValue<Vector2>() : Vector2.zero;
            var wish = Forward * mv.y + Right * mv.x;
            var p = _rig.transform.position;

            if (_w == 0 && _wire)
            {
                // ワイヤー（2026-11-18 作りなおし。立体的に飛びまわる道具の動きを参考に）：
                //  ・刺さった直後（0.4秒）と、Space（VR は B）を押している間は、ガスでぐっと引き寄せる（巻きとり）
                //  ・離している間は振り子。ロープは長さを保ち、左スティック・WASD で振る向きを変えられる
                //  ・アンカーのすぐ近くまで来たら、勢いで乗りこえるように自動で離す
                //  ・E でいつでも離せる。勢いは残るので、空中ですぐ次へ引っかけられる
                var c = p + Vector3.up;
                var to = _anchor - c; float d = to.magnitude; var dir = d > 0.01f ? to / d : Vector3.up;
                bool reel = _zipT > 0 || (play && Act.Jump.Held);
                _zipT -= dt;
                _vel.y -= Gravity * (reel ? 0.35f : 0.85f) * dt;
                _vel += wish * (reel ? 10f : 14f) * dt;
                if (reel)
                {
                    _vel += dir * 42f * dt;                                   // ガスで引き寄せる
                    float along = Vector3.Dot(_vel, dir);
                    if (along < 8f) _vel += dir * (8f - along);               // 引き寄せる向きの速さは、少なくとも毎秒 8m
                    _ropeLen = Mathf.Max(2f, Mathf.Min(_ropeLen, d));

                }
                else _ropeLen = Mathf.Max(2.5f, _ropeLen - 2f * dt);
                var next = c + _vel * dt;
                var off = next - _anchor;
                if (off.magnitude > _ropeLen)
                {
                    // ロープより外へは出ない：外向きの速さだけを消す（横向きの勢いは残して、振り子になる）
                    var n = off.normalized; float outV = Vector3.Dot(_vel, n);
                    if (outV > 0) _vel -= n * outV;
                }
                _vel = Vector3.ClampMagnitude(_vel, 42f);
                if (d < 2.2f)
                {
                    // アンカーに着いた：勢いで乗りこえるように離す
                    _wire = false; _vel += Vector3.up * 6f + new Vector3(dir.x, 0, dir.z) * 3f; Play(_sJump, 0.5f);
                    return false;
                }
                ShowLine(_anchor, reel ? new Color(0.7f, 1f, 1f) : new Color(0.4f, 1f, 1f), 0f);
                return true;
            }
            if (_w == 1 && play && Act.Special.Held && _fuel > 0)
            {
                var thrust = (Aim * 0.55f + Vector3.up * 0.85f).normalized * 32f;
                _vel += (thrust + wish * 10f) * dt;
                _vel.y -= Gravity * dt;
                _vel = Vector3.ClampMagnitude(_vel, 24f);
                _fuel = Mathf.Max(0, _fuel - dt / 3.2f);
                _grounded = false;
                _flameT = 0.1f;
                return true;
            }
            if (_grounded && _fuel < 1) _fuel = Mathf.Min(1, _fuel + dt * 0.25f); // 燃料の回復：約4秒（前は約2.2秒。ユーザーの指示で遅く）

            if (_w == 2 && _ride)
            {
                var flat = new Vector3(_vel.x, 0, _vel.z);
                flat = Vector3.Lerp(flat, wish * 12f, 1 - Mathf.Exp(-3f * dt));
                _vel.x = flat.x; _vel.z = flat.z;
                if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var g, 4.5f, ~0, QueryTriggerInteraction.Ignore) && g.collider != _cc)
                    _vel.y = Mathf.Lerp(_vel.y, (g.point.y + 1.3f - p.y) * 5f, 1 - Mathf.Exp(-8f * dt));
                else _vel.y = Mathf.Max(_vel.y - 6f * dt, -1.6f); // 高いところからは、ゆっくりおりる
                if (play && Act.Jump.Down) { _ride = false; _vel.y = 7f; Play(_sJump, 0.4f); }
                return true;
            }
            if (_dashT > 0)
            {
                _dashT -= dt;
                _vel = _dashDir * _dashSpeed;
                foreach (var col in Physics.OverlapSphere(p + Vector3.up + _dashDir * 0.8f, 1.3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var t = col.GetComponentInParent<SengokuTarget>();
                    if (t != null && !t.Down && t.team != 1 && _dashHit.Add(t)) { t.Hit(_dashDmg, col.ClosestPoint(p + Vector3.up), _dashDir, true, true); Play(_sHeavy, 0.8f); Sparks(col.ClosestPoint(p + Vector3.up), new Color(1f, 0.9f, 0.5f), 12); }
                }
                if (_dashT <= 0) _vel *= _dashKeep;
                return true;
            }
            if (_pull)
            {
                _pullT -= dt;
                var d = _pullTo - p;
                _vel = d.normalized * 24f;
                ShowLine(_pullTo, new Color(0.7f, 0.7f, 0.75f), 0f);
                if (d.magnitude < 1.4f || _pullT <= 0) { _pull = false; _vel = Vector3.up * 5f + Forward * 2f; if (_rope) _rope.gameObject.SetActive(false); }
                return true;
            }
            if (_rope != null && _ropeShow <= 0 && !_wire && !_pull) _rope.gameObject.SetActive(false);
            return false;
        }

        // ───────── 近接攻撃 ─────────

        /// <summary>近接攻撃。from・dir を渡すと武者の攻撃（team は攻めた側。同じ組には当たらない）。</summary>
        void Melee(float dmg, float range, bool heavy, float shove = 0, Vector3? from = null, Vector3? dir = null, int team = 1)
        {
            var o = from ?? EyePos; var a = dir ?? MeleeAim;
            var center = o + a * range * 0.55f;
            var done = new HashSet<SengokuTarget>();
            bool any = false;
            foreach (var col in Physics.OverlapSphere(center, range * 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = col.GetComponentInParent<SengokuTarget>();
                if (t == null || t.Down || (team != 0 && t.team == team) || !done.Add(t)) continue;
                var pt = col.ClosestPoint(o);
                t.Hit(dmg, pt, a, heavy, true);
                if (team == 1 && t.team == 2) HitMark(t.Down);
                if (shove > 0) { t.Shove(a * shove); if (t == _me) _vel += a * shove * 0.8f + Vector3.up * 3f; }
                Sparks(pt, heavy ? new Color(1f, 0.6f, 0.3f) : new Color(0.5f, 1f, 1f), heavy ? 14 : 8);
                any = true;
            }
            if (any && team == 1 && from == null) Vr.Buzz(true, heavy ? 0.9f : 0.5f, heavy ? 0.15f : 0.07f); // VR：当たったらふるえる
            if (team == 1 && from == null || Vector3.Distance(o, EyePos) < 14f) Play(any ? (heavy ? _sHeavy : _sHit) : _sWhoosh, (any ? 0.8f : 0.4f) * (from == null ? 1f : 0.5f));
        }

        void ChainThrow()
        {
            if (Physics.SphereCast(From, 0.4f, Aim, out var h, 18f, ~0, QueryTriggerInteraction.Ignore) && h.collider != _cc)
            {
                ShowLine(h.point, new Color(0.7f, 0.7f, 0.75f), 0.3f);
                var t = h.collider.GetComponentInParent<SengokuTarget>();
                if (t != null && !t.Down && t.team != 1) { t.Hit(20, h.point, -Aim); t.Shove(-Aim * 8f); Play(_sHit, 0.7f); Sparks(h.point, Color.white, 8); }
                else Play(_sClick, 0.5f);
            }
            else ShowLine(From + Aim * 18f, new Color(0.7f, 0.7f, 0.75f), 0.2f);
            Swing(0.25f);
        }

        // ───────── 飛び道具 ─────────

        enum ShotKind { Spark, Missile, Boomerang, Arrow, Ball, Bullet }

        class Shot
        {
            public Vector3 p, v; public float grav, dmg, aoe, life, t, outT = 0.6f; public bool heavy, boomerang, back;
            public ShotKind kind; public Transform tr;
            public int team = 1; public Collider self;
            public readonly HashSet<SengokuTarget> hit = new HashSet<SengokuTarget>();
        }
        readonly List<Shot> _shots = new List<Shot>();

        Shot Fire(Vector3 from, Vector3 vel, float grav, float dmg, float aoe, bool heavy, ShotKind kind, int team = 1, Collider self = null)
        {
            var s = new Shot { p = from, v = vel, grav = grav, dmg = dmg, aoe = aoe, heavy = heavy, kind = kind, life = kind == ShotKind.Boomerang ? 6f : 5f, team = team, self = self != null ? self : _cc };
            var root = new GameObject("飛び道具").transform; root.SetParent(transform, false);
            switch (kind)
            {
                case ShotKind.Spark: Block(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.18f, new Color(0.4f, 1f, 1f), false, null, true); break;
                case ShotKind.Missile:
                    Block(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.22f, 0.4f, 0.22f), new Color(0.85f, 0.2f, 0.15f), false, new Vector3(90, 0, 0));
                    Block(PrimitiveType.Sphere, root, new Vector3(0, 0, -0.45f), Vector3.one * 0.25f, new Color(1f, 0.7f, 0.3f), false, null, true); break;
                case ShotKind.Boomerang: BuildUmbrella(root, true, 2.2f); break; // 投げた傘は大きく（ユーザーの指示。1.2 → 2.2）
                case ShotKind.Arrow: BuildArrow(root, team, heavy); break;
                case ShotKind.Ball: Block(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.45f, new Color(0.15f, 0.14f, 0.14f), false); break;
                case ShotKind.Bullet: Block(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(0.12f, 0.12f, 0.3f), new Color(1f, 0.82f, 0.3f), false, null, true); break;
            }
            s.tr = root; root.position = from; root.rotation = Quaternion.LookRotation(vel);
            _shots.Add(s);
            if (kind != ShotKind.Ball && (team == 1 || Vector3.Distance(from, EyePos) < 18f)) Play(_sWhoosh, team == 1 ? 0.45f : 0.25f);
            return s;
        }

        /// <summary>
        /// 矢（2026-11-18 ユーザーの指示で派手に）：光る矢じり・光る羽根・光の尾（味方は水色、敵は赤）。
        /// ためきった矢（heavy）は金色で大きく、尾も長い。
        /// </summary>
        void BuildArrow(Transform root, int team, bool heavy)
        {
            var glow = heavy ? new Color(1f, 0.85f, 0.35f) : team == 1 ? new Color(0.45f, 0.95f, 1f) : new Color(1f, 0.4f, 0.35f);
            float k = heavy ? 1.5f : 1f;
            Block(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.035f, 0.035f, 0.95f) * k, new Color(0.65f, 0.48f, 0.3f), false);
            Block(PrimitiveType.Cube, root, new Vector3(0, 0, 0.5f * k), new Vector3(0.09f, 0.09f, 0.16f) * k, glow, false, new Vector3(0, 0, 45), true);
            foreach (float a in new[] { 0f, 120f, 240f })
                Block(PrimitiveType.Cube, root, Quaternion.Euler(0, 0, a) * new Vector3(0, 0.05f * k, -0.4f * k), new Vector3(0.01f, 0.09f, 0.16f) * k, glow, false, new Vector3(0, 0, a), true);
            var tr = root.gameObject.AddComponent<TrailRenderer>();
            tr.time = heavy ? 0.45f : 0.28f;
            tr.startWidth = heavy ? 0.16f : 0.09f; tr.endWidth = 0f;
            tr.material = Mat(glow, true);
            tr.startColor = new Color(glow.r, glow.g, glow.b, 0.95f); tr.endColor = new Color(glow.r, glow.g, glow.b, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.minVertexDistance = 0.2f;
        }

        void Shots(float dt)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                s.t += dt; s.life -= dt;
                if (s.boomerang)
                {
                    if (!s.back && s.t > s.outT) { s.back = true; s.hit.Clear(); }
                    if (s.back) { var to = EyePos - s.p; s.v = to.normalized * 32f; if (to.magnitude < 1.2f) { Kill(i); _boomer = null; continue; } }
                    s.tr.Rotate(0, 900f * dt, 0, Space.World);
                }
                s.v += Vector3.down * s.grav * dt;
                var step = s.v * dt;
                bool gone = s.life <= 0;
                if (!gone && Physics.SphereCast(s.p, ShotRadius(s.kind), step.normalized, out var h, step.magnitude, ~0, QueryTriggerInteraction.Ignore) && h.collider != s.self
                    && !(h.collider.GetComponentInParent<SengokuTarget>() is SengokuTarget ft && s.team != 0 && ft.team == s.team))
                {
                    var t = h.collider.GetComponentInParent<SengokuTarget>();
                    if (s.boomerang)
                    {
                        if (t != null && !t.Down && s.hit.Add(t)) { t.Hit(s.dmg, h.point, s.v, false); Sparks(h.point, new Color(0.6f, 0.8f, 1f), 8); Play(_sHit, 0.7f); }
                        else if (t == null) { s.back = true; }
                    }
                    else
                    {
                        if (t != null && !t.Down && s.dmg > 0) { t.Hit(s.dmg, h.point, s.v, s.heavy); Play(s.heavy ? _sHeavy : _sHit, 0.8f); if (s.team == 1 && t.team == 2) HitMark(t.Down); }
                        if (s.aoe > 0) Explode(h.point, s.aoe, s.kind == ShotKind.Ball ? 4.5f : 3.5f, s.heavy, s.team);
                        else Sparks(h.point, s.kind == ShotKind.Arrow ? (s.heavy ? new Color(1f, 0.85f, 0.35f) : new Color(0.8f, 0.95f, 1f)) : new Color(0.4f, 1f, 1f), s.kind == ShotKind.Arrow ? (s.heavy ? 16 : 10) : 7);
                        gone = true;
                    }
                }
                s.p += step;
                if (s.tr) { s.tr.position = s.p; if (!s.boomerang && s.v.sqrMagnitude > 0.01f) s.tr.rotation = Quaternion.LookRotation(s.v); }
                if (gone) { Kill(i); if (s == _boomer) _boomer = null; }
            }
        }

        /// <summary>飛び道具の当たりの太さ（投げた傘は見た目に合わせて大きく）。</summary>
        static float ShotRadius(ShotKind k) => k == ShotKind.Boomerang ? 0.75f : k == ShotKind.Ball ? 0.25f : k == ShotKind.Arrow ? 0.16f : 0.12f;

        void Kill(int i)
        {
            if (_shots[i].tr) Destroy(_shots[i].tr.gameObject);
            _shots.RemoveAt(i);
        }

        void Explode(Vector3 at, float dmg, float radius, bool heavy, int team = 1)
        {
            var done = new HashSet<SengokuTarget>();
            foreach (var col in Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = col.GetComponentInParent<SengokuTarget>();
                if (t == null || t.Down || (team != 0 && t.team == team) || !done.Add(t)) continue;
                var pt = col.ClosestPoint(at);
                float k = 1f - Mathf.Clamp01(Vector3.Distance(pt, at) / radius) * 0.6f;
                t.Hit(dmg * k, pt, pt - at, heavy);
                if (team == 1 && t.team == 2) HitMark(t.Down);
                t.Shove((pt - at).normalized * 6f);
            }
            Boom(at, radius / 3f, new Color(1f, 0.6f, 0.25f));
            if (Vector3.Distance(at, EyePos) < 30f) Play(_sBoom, 0.7f);
            if (Vector3.Distance(at, EyePos) < radius + 2f) Kick(3f);
        }

        // ───────── 光と火花 ─────────

        class Fx { public Transform tr; public Vector3 v; public float life, max, grow; }
        readonly List<Fx> _fx = new List<Fx>();

        void Sparks(Vector3 at, Color c, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var g = Block(PrimitiveType.Cube, transform, at, Vector3.one * Random.Range(0.05f, 0.11f), i % 3 == 0 ? Color.white : c, false, null, true);
                var v = Random.onUnitSphere * Random.Range(2f, 7f); v.y = Mathf.Abs(v.y);
                _fx.Add(new Fx { tr = g.transform, v = v, life = 0.3f, max = 0.3f });
            }
        }

        void Boom(Vector3 at, float size, Color c)
        {
            var g = Block(PrimitiveType.Sphere, transform, at, Vector3.one * size, c, false, null, true);
            _fx.Add(new Fx { tr = g.transform, v = Vector3.zero, life = 0.35f, max = 0.35f, grow = size * 9f });
            Sparks(at, c, 14);
        }

        void FxTick(float dt)
        {
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                f.life -= dt;
                if (f.life <= 0 || f.tr == null) { if (f.tr) Destroy(f.tr.gameObject); _fx.RemoveAt(i); continue; }
                f.tr.position += f.v * dt; f.v *= 1 - 3f * dt;
                if (f.grow > 0) f.tr.localScale += Vector3.one * f.grow * dt;
                else f.tr.localScale *= 1 - 3f * dt;
            }
        }

        // ───────── ロープ・鎖の線 ─────────

        Transform _rope;
        float _ropeShow;

        void ShowLine(Vector3 to, Color c, float hold)
        {
            if (_rope == null)
            {
                _rope = Block(PrimitiveType.Cube, transform, Vector3.zero, Vector3.one, c, false, null, true).transform;
            }
            _rope.GetComponent<Renderer>().sharedMaterial = Mat(c, true);
            var from = EyePos + Right * 0.3f - Vector3.up * 0.3f + Forward * 0.3f;
            var d = to - from;
            _rope.gameObject.SetActive(true);
            _rope.position = (from + to) * 0.5f;
            _rope.rotation = Quaternion.LookRotation(d.normalized);
            _rope.localScale = new Vector3(0.05f, 0.05f, d.magnitude);
            if (hold > 0) _ropeShow = hold;
        }

        // ───────── 手に持つ武器の見た目（カメラの子）─────────

        Transform _viewRoot;
        readonly GameObject[] _viewModels = new GameObject[6];
        float _flameT;
        Transform _flame, _openUmbrella;

        void Swing(float len) { _swing = len; _swingLen = len; }

        void BuildWeapons()
        {
            if (_cam == null) return;
            _viewRoot = new GameObject("手の武器").transform;
            _viewRoot.SetParent(_cam.transform, false);
            for (int i = 0; i < 6; i++)
            {
                var m = new GameObject(Weapons[i].name).transform;
                m.SetParent(_viewRoot, false);
                _viewModels[i] = m.gameObject;
            }
            var white = new Color(0.92f, 0.95f, 1f); var teal = new Color(0.3f, 1f, 1f); var dark = new Color(0.12f, 0.1f, 0.1f); var gold = new Color(1f, 0.8f, 0.35f);
            // 二刀流
            foreach (float sx in new[] { -1f, 1f })
            {
                var b = new GameObject(sx < 0 ? "左の刀" : "右の刀").transform; b.SetParent(_viewModels[0].transform, false);
                b.localPosition = new Vector3(sx * 0.32f, -0.32f, 0.55f); b.localEulerAngles = new Vector3(-55, sx * -12, sx * -8);
                Block(PrimitiveType.Cylinder, b, Vector3.zero, new Vector3(0.05f, 0.12f, 0.05f), dark, false);
                Block(PrimitiveType.Cube, b, new Vector3(0, 0.14f, 0), new Vector3(0.12f, 0.02f, 0.06f), gold, false);
                Block(PrimitiveType.Cube, b, new Vector3(0, 0.6f, 0), new Vector3(0.035f, 0.9f, 0.012f), white, false);
                Block(PrimitiveType.Cube, b, new Vector3(0.02f, 0.6f, 0), new Vector3(0.008f, 0.9f, 0.014f), teal, false, null, true);
            }
            // ハンマー
            {
                var h = new GameObject("ハンマー").transform; h.SetParent(_viewModels[1].transform, false);
                h.localPosition = new Vector3(0.42f, -0.5f, 0.85f); h.localEulerAngles = new Vector3(-30, -10, 0); h.localScale = Vector3.one * 0.7f;
                Block(PrimitiveType.Cylinder, h, new Vector3(0, 0.1f, 0), new Vector3(0.06f, 0.45f, 0.06f), new Color(0.45f, 0.3f, 0.2f), false);
                Block(PrimitiveType.Cylinder, h, new Vector3(0, 0.65f, 0), new Vector3(0.38f, 0.3f, 0.38f), new Color(0.85f, 0.2f, 0.25f), false, new Vector3(0, 0, 90));
                foreach (float sx in new[] { -1f, 1f }) Block(PrimitiveType.Cylinder, h, new Vector3(sx * 0.24f, 0.65f, 0), new Vector3(0.4f, 0.03f, 0.4f), gold, false, new Vector3(0, 0, 90));
                _flame = Block(PrimitiveType.Sphere, h, new Vector3(0, 0.65f, -0.35f), new Vector3(0.25f, 0.25f, 0.6f), new Color(1f, 0.6f, 0.2f), false, null, true).transform;
            }
            // 傘
            {
                var u = new GameObject("傘").transform; u.SetParent(_viewModels[2].transform, false);
                u.localPosition = new Vector3(0.32f, -0.35f, 0.6f); u.localEulerAngles = new Vector3(-70, 0, 0);
                BuildUmbrella(u, false, 1.35f); // 傘を大きく（ユーザーの指示）
                _openUmbrella = new GameObject("乗物").transform; _openUmbrella.SetParent(_rig.transform, false);
                _openUmbrella.localPosition = new Vector3(0, 0.05f, 0.15f);
                BuildNorimono(_openUmbrella);
                _openUmbrella.gameObject.SetActive(false);
            }
            // 弓
            {
                var b = new GameObject("弓").transform; b.SetParent(_viewModels[3].transform, false);
                b.localPosition = new Vector3(-0.32f, -0.2f, 0.85f); b.localEulerAngles = new Vector3(0, 0, 12); b.localScale = Vector3.one * 1.05f; // 弓を大きく（ユーザーの指示）
                for (int i = -4; i <= 4; i++)
                {
                    float a = i / 4f * 0.9f;
                    Block(PrimitiveType.Cube, b, new Vector3(0, Mathf.Sin(a) * 0.45f, -Mathf.Cos(a) * 0.12f + 0.12f), new Vector3(0.03f, 0.12f, 0.03f), i == 0 ? gold : new Color(0.95f, 0.95f, 0.98f), false, new Vector3(-a * Mathf.Rad2Deg, 0, 0));
                }
                Block(PrimitiveType.Cube, b, new Vector3(0, 0, -0.03f), new Vector3(0.005f, 0.82f, 0.005f), teal, false, null, true).name = "弦";
                Block(PrimitiveType.Cube, b, new Vector3(0.02f, 0, 0.25f), new Vector3(0.02f, 0.02f, 0.65f), new Color(0.6f, 0.45f, 0.3f), false).name = "矢";
            }
            // 金棒（帝）：金色の太い棒にとげ
            {
                var k = new GameObject("金棒").transform; k.SetParent(_viewModels[4].transform, false);
                k.localPosition = new Vector3(0.4f, -0.38f, 0.8f); k.localEulerAngles = new Vector3(-35, -8, 0); k.localScale = Vector3.one * 0.45f;
                var goldD = new Color(1f, 0.78f, 0.25f); var goldL = new Color(1f, 0.92f, 0.55f);
                Block(PrimitiveType.Cylinder, k, new Vector3(0, 0f, 0), new Vector3(0.07f, 0.2f, 0.07f), new Color(0.35f, 0.15f, 0.12f), false);
                Block(PrimitiveType.Cylinder, k, new Vector3(0, 0.62f, 0), new Vector3(0.16f, 0.45f, 0.16f), goldD, false);
                Block(PrimitiveType.Sphere, k, new Vector3(0, 1.06f, 0), new Vector3(0.17f, 0.1f, 0.17f), goldD, false);
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 6; j++)
                    {
                        float a = j / 6f * Mathf.PI * 2 + i * 0.5f;
                        Block(PrimitiveType.Cube, k, new Vector3(Mathf.Cos(a) * 0.085f, 0.36f + i * 0.17f, Mathf.Sin(a) * 0.085f), Vector3.one * 0.045f, goldL, false, new Vector3(45, -a * Mathf.Rad2Deg, 45), true);
                    }
            }
            // 団扇（雷）：大きな団扇に、雷のいなずま
            {
                var f = new GameObject("団扇").transform; f.SetParent(_viewModels[5].transform, false);
                f.localPosition = new Vector3(0.42f, -0.36f, 0.8f); f.localEulerAngles = new Vector3(-40, -15, 0); f.localScale = Vector3.one * 0.55f;
                Block(PrimitiveType.Cylinder, f, new Vector3(0, 0.05f, 0), new Vector3(0.045f, 0.22f, 0.045f), new Color(0.3f, 0.2f, 0.15f), false);
                Block(PrimitiveType.Cylinder, f, new Vector3(0, 0.62f, 0), new Vector3(0.8f, 0.012f, 0.62f), new Color(0.32f, 0.22f, 0.55f), false, new Vector3(90, 0, 0));
                Block(PrimitiveType.Cylinder, f, new Vector3(0, 0.62f, 0.008f), new Vector3(0.84f, 0.006f, 0.66f), new Color(1f, 0.88f, 0.35f), false, new Vector3(90, 0, 0));
                // いなずま（じぐざぐ）
                Vector2[] zig = { new(-0.05f, 0.95f), new(0.06f, 0.75f), new(-0.04f, 0.6f), new(0.07f, 0.4f) };
                for (int i = 0; i < zig.Length - 1; i++)
                {
                    var za = zig[i]; var zb = zig[i + 1]; var mid = (za + zb) * 0.5f; var d = zb - za;
                    Block(PrimitiveType.Cube, f, new Vector3(mid.x, mid.y, -0.02f), new Vector3(0.035f, d.magnitude, 0.01f), new Color(1f, 0.95f, 0.4f), false, new Vector3(0, 0, Mathf.Atan2(d.x, d.y) * -Mathf.Rad2Deg), true);
                }
            }
            foreach (var r in _viewRoot.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// 乗物：大名行列で使う駕籠を、壁のない吹き抜けにしたもの。黒漆の床・赤い座布団・金の柱・反りのある屋根・前後の担ぎ棒・房飾り。
        /// 床の高さがその足もと（parent の原点）。底がうっすら光って浮かぶ。
        /// </summary>
        void BuildNorimono(Transform parent)
        {
            var lacquer = new Color(0.07f, 0.06f, 0.07f); var gold = new Color(1f, 0.78f, 0.32f); var red = new Color(0.72f, 0.12f, 0.12f);
            Block(PrimitiveType.Cube, parent, new Vector3(0, 0.05f, 0), new Vector3(1.5f, 0.16f, 1.7f), lacquer, false);
            Block(PrimitiveType.Cube, parent, new Vector3(0, 0.15f, 0), new Vector3(1.56f, 0.04f, 1.76f), gold, false);           // 縁の金
            Block(PrimitiveType.Cube, parent, new Vector3(0, 0.2f, -0.1f), new Vector3(0.9f, 0.08f, 0.9f), red, false);            // 座布団
            foreach (float sx in new[] { -0.68f, 0.68f }) foreach (float sz in new[] { -0.78f, 0.78f })
                Block(PrimitiveType.Cylinder, parent, new Vector3(sx, 1.2f, sz), new Vector3(0.06f, 1.05f, 0.06f), gold, false);
            // 反りのある屋根（重ねた板）と棟
            Block(PrimitiveType.Cube, parent, new Vector3(0, 2.3f, 0), new Vector3(1.8f, 0.1f, 2.0f), lacquer, false);
            Block(PrimitiveType.Cube, parent, new Vector3(0, 2.4f, 0), new Vector3(1.4f, 0.1f, 1.6f), lacquer, false);
            Block(PrimitiveType.Cube, parent, new Vector3(0, 2.5f, 0), new Vector3(0.9f, 0.1f, 1.2f), lacquer, false);
            Block(PrimitiveType.Cube, parent, new Vector3(0, 2.36f, 0), new Vector3(1.84f, 0.03f, 2.04f), gold, false);
            Block(PrimitiveType.Cube, parent, new Vector3(0, 2.6f, 0), new Vector3(0.12f, 0.1f, 1.3f), gold, false);
            foreach (float sx in new[] { -0.92f, 0.92f }) foreach (float sz in new[] { -1.02f, 1.02f })
                Block(PrimitiveType.Cube, parent, new Vector3(sx, 2.38f, sz), new Vector3(0.14f, 0.08f, 0.14f), gold, false, new Vector3(0, 45, 0)); // 反った角
            // 担ぎ棒（前後に長く）と房飾り
            Block(PrimitiveType.Cube, parent, new Vector3(0, 2.15f, 0), new Vector3(0.12f, 0.12f, 4.2f), lacquer, false);
            foreach (float sz in new[] { -2.05f, 2.05f })
            {
                Block(PrimitiveType.Cube, parent, new Vector3(0, 2.15f, sz), new Vector3(0.16f, 0.16f, 0.12f), gold, false);
                Block(PrimitiveType.Cylinder, parent, new Vector3(0, 1.85f, sz), new Vector3(0.05f, 0.25f, 0.05f), new Color(1f, 0.55f, 0.75f), false, null, true);
            }
            foreach (float sx in new[] { -0.92f, 0.92f }) foreach (float sz in new[] { -1.02f, 1.02f })
                Block(PrimitiveType.Sphere, parent, new Vector3(sx, 2.1f, sz), new Vector3(0.1f, 0.22f, 0.1f), new Color(1f, 0.55f, 0.75f), false, null, true);
            // 底の光（浮いているしるし）
            Block(PrimitiveType.Cylinder, parent, new Vector3(0, -0.1f, 0), new Vector3(1.6f, 0.02f, 1.8f), new Color(0.4f, 0.95f, 1f), false, null, true);
            foreach (var r in parent.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>ヤチヨの傘（クラゲのような和傘：うす青の笠と、ひらひらの光る足）。open で開いた形。</summary>
        void BuildUmbrella(Transform parent, bool open, float size)
        {
            var canopy = new GameObject("笠"); canopy.transform.SetParent(parent, false);
            canopy.transform.localPosition = new Vector3(0, open ? 0.25f * size : 0.55f * size, 0);
            canopy.transform.localScale = open ? new Vector3(1.4f, 0.4f, 1.4f) * size * 0.6f : new Vector3(0.22f, 0.8f, 0.22f) * size * 0.6f;
            canopy.AddComponent<MeshFilter>().sharedMesh = Doll.Cone;
            canopy.AddComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.65f, 0.85f, 1f));
            if (open) canopy.transform.localEulerAngles = Vector3.zero;
            Block(PrimitiveType.Cylinder, parent, new Vector3(0, 0.25f * size, 0), new Vector3(0.025f, 0.45f, 0.025f) * size, new Color(0.35f, 0.22f, 0.12f), false);
            Block(PrimitiveType.Sphere, parent, new Vector3(0, open ? 0.5f * size : 1.05f * size, 0), Vector3.one * 0.06f * size, new Color(1f, 0.6f, 0.8f), false, null, true);
            int n = open ? 8 : 3;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2;
                var p = open ? new Vector3(Mathf.Cos(a) * 0.5f, 0.1f, Mathf.Sin(a) * 0.5f) * size : new Vector3(Mathf.Cos(a) * 0.06f, 0.2f, Mathf.Sin(a) * 0.06f) * size;
                Block(PrimitiveType.Cube, parent, p, new Vector3(0.02f, 0.35f, 0.01f) * size, new Color(0.5f, 0.95f, 1f), false, null, true);
            }
        }

        void WeaponTick(float dt)
        {
            FxTick(dt);
            if (_ropeShow > 0) _ropeShow -= dt;
            if (_viewRoot == null) return;
            _swing = Mathf.Max(0, _swing - dt);
            float k = _swingLen > 0 ? Mathf.Sin(Mathf.Clamp01(1 - _swing / _swingLen) * Mathf.PI) : 0;
            var m = _viewModels[_w].transform;
            float bob = _grounded ? Mathf.Sin(Time.time * 9f) * 0.008f * new Vector2(_vel.x, _vel.z).magnitude / 6f : 0;
            Vector3 pos = new Vector3(0, bob, 0), rot = Vector3.zero;
            switch (_w)
            {
                case 0: rot = new Vector3(55 * k, (_combo % 2 == 0 ? -1 : 1) * 40 * k, 0); pos += new Vector3(0, 0, 0.15f * k); break;
                case 1: rot = _hammerWind ? new Vector3(-35, 0, 0) : new Vector3(70 * k, 0, 0); break;
                case 2: pos += new Vector3(0, 0, 0.3f * k); break;
                case 3:
                    pos += new Vector3(_zoom ? -0.12f : 0, _zoom ? -0.08f : 0, -0.06f * _charge);
                    var arrow = m.Find("弓/矢"); if (arrow) arrow.localPosition = new Vector3(0.02f, 0, 0.25f - 0.2f * _charge);
                    break;
                case 4: // 金棒：振りかぶって、前へ大きく振りおろす（ユーザーの指示）
                    {
                        float t01 = _swingLen > 0 ? Mathf.Clamp01(1 - _swing / _swingLen) : 1;
                        // はじめの3割で振りかぶり（うしろへ倒す）、のこりで一気に振りおろす
                        float a = t01 < 0.3f ? -45f * (t01 / 0.3f) : Mathf.Lerp(-45f, 95f, Mathf.SmoothStep(0, 1, (t01 - 0.3f) / 0.7f));
                        if (_swing <= 0) a = 0;
                        rot = new Vector3(a, -18f * k, -10f * k);
                        pos += new Vector3(-0.05f * k, -0.04f * k, 0.18f * k);
                    }
                    break;
                case 5: rot = new Vector3(60 * k, -30 * k, 0); break;
            }
            m.localPosition = Vector3.Lerp(m.localPosition, pos, 1 - Mathf.Exp(-25f * dt));
            m.localRotation = Quaternion.Slerp(m.localRotation, Quaternion.Euler(rot), 1 - Mathf.Exp(-25f * dt));
            if (_flame != null) { _flameT -= dt; _flame.gameObject.SetActive(_flameT > 0); if (_flameT > 0) _flame.localScale = new Vector3(0.25f, 0.25f, 0.5f + Random.value * 0.4f); }
            if (_openUmbrella != null)
            {
                _openUmbrella.gameObject.SetActive(_ride);
                if (_ride) _openUmbrella.rotation = Quaternion.Slerp(_openUmbrella.rotation, Quaternion.Euler(0, _yaw, Mathf.Sin(Time.time * 2f) * 2f), 1 - Mathf.Exp(-6f * dt));
                var closed = _viewModels[2].transform.Find("傘");
                if (closed) closed.gameObject.SetActive(_boomer == null);
            }
        }
    }
}
