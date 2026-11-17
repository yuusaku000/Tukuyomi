using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の6つの武器。左クリック＝攻撃、右クリック＝飛び道具／投げる、E＝移動わざ。
    ///  1 二刀流（イロハ）：連撃・先端から光弾・E でワイヤー（引っかけて振り子で飛ぶ）
    ///  2 ハンマー（かぐや）：ためて叩きつけ・ミサイル・E 長押しでジェット（燃料あり）
    ///  3 傘（ヤチヨ）：突き・ブーメランのように投げる・E で傘を開いて浮かんですべる（前を守る）
    ///  4 弓（のい）：長押しでためて射る・右で狙う（ズーム）・E で自分が矢になって飛ぶ
    ///  5 大筒：重い大砲（建物に2倍）・右で狙う・E で撃った反動で跳ぶ
    ///  6 鎖鎌：鎌で斬る・右で鎖を投げて引き寄せる・E で鎖を引っかけて登る
    /// </summary>
    public partial class Sengoku
    {
        public class Weapon { public string name, en, hint; }

        public static readonly Weapon[] Weapons =
        {
            new() { name = "二刀流", en = "TWIN BLADES", hint = "左クリック 連撃（3回目が強い）\n右クリック 先端から光弾\nE 長押し ワイヤー（建物に引っかけて飛ぶ・Space で離して跳ぶ）" },
            new() { name = "ハンマー", en = "HAMMER", hint = "左クリック 叩きつけ（重い一撃・吹き飛ばす）\n右クリック ミサイル（爆発）\nE 長押し ジェットで飛ぶ（燃料は地面で回復）" },
            new() { name = "傘", en = "UMBRELLA", hint = "左クリック 突き\n右クリック ブーメランのように投げる（もどってくる）\nE 傘を開いて浮かぶ（すべって速い・前の攻撃を防ぐ・Space でおりる）" },
            new() { name = "弓", en = "BOW", hint = "左クリック 長押しでためて、離して射る\n右クリック 長押しで狙う（ズーム）\nE 自分が矢になって飛ぶ（当たると痛い）" },
            new() { name = "大筒", en = "CANNON", hint = "左クリック 大砲（爆発・建物に2倍）\n右クリック 長押しで狙う\nE 撃った反動で後ろへ大きく跳ぶ" },
            new() { name = "鎖鎌", en = "KUSARIGAMA", hint = "左クリック 鎌で斬る\n右クリック 鎖を投げて相手を引き寄せる\nE 鎖を引っかけて、そこまで登る" },
        };

        int _w;
        float _cd, _cd2, _abilityCd, _swing, _swingLen = 0.3f, _charge, _fuel = 1f;
        int _combo;
        bool _wire, _ride, _pull, _zoom, _hammerWind;
        Vector3 _anchor, _pullTo; float _ropeLen, _dashT, _pullT, _windT;
        Vector3 _dashDir;
        readonly HashSet<SengokuTarget> _dashHit = new HashSet<SengokuTarget>();
        Shot _boomer;

        float SpeedMul => _w == 4 ? 0.85f : 1f;
        float FovAdd => _zoom ? (_w == 3 ? -40f : -30f) : (_dashT > 0 ? 12f : 0f) + (_w == 1 && Act.Special.Held && _fuel > 0 ? 6f : 0f);

        float Gauge01 => _w switch
        {
            0 => _wire ? 1 : WireTarget(out _) ? 1 : 0.15f,
            1 => _fuel,
            2 => _ride ? 1 : 1,
            3 => _charge > 0 ? _charge : 1 - Mathf.Clamp01(_abilityCd / 3f),
            4 => 1 - Mathf.Clamp01(_cd / 1.6f),
            _ => 1 - Mathf.Clamp01(_abilityCd / 1.5f),
        };

        string GaugeText => _w switch
        {
            0 => _wire ? "ワイヤー中" : WireTarget(out _) ? "E ワイヤー（届く）" : "ワイヤーが届かない",
            1 => $"燃料 {_fuel * 100:0}%",
            2 => _ride ? "傘で浮かんでいる（Space でおりる）" : "E で傘を開く",
            3 => _charge > 0 ? $"ため {_charge * 100:0}%" : _abilityCd > 0 ? "矢になる：準備中" : "E 矢になって飛ぶ",
            4 => _cd > 0 ? "装填中…" : "発射できる",
            _ => _abilityCd > 0 ? "鎖：準備中" : "E 鎖で登る",
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
            bool L = Act.Light.Down, LH = Act.Light.Held, LU = Act.Light.Up, R = Act.Heavy.Down, RH = Act.Heavy.Held, E = Act.Special.Down;
            switch (_w)
            {
                case 0: // 二刀流
                    if (L && _cd <= 0) { _combo = (_combo + 1) % 3; bool big = _combo == 0; Melee(big ? 45 : 28, 2.5f, big); Swing(big ? 0.38f : 0.24f); _cd = big ? 0.42f : 0.24f; }
                    if (R && _cd2 <= 0) { Fire(EyePos + Aim * 0.7f + Right * 0.15f, Aim * 55f, 0, 14, 0, false, ShotKind.Spark); _cd2 = 0.22f; Swing(0.15f); }
                    if (E && WireTarget(out var hit)) { _wire = true; _anchor = hit.point; _ropeLen = Vector3.Distance(_rig.transform.position + Vector3.up, _anchor); Play(_sWhoosh, 0.6f); }
                    if (_wire && (!Act.Special.Held)) _wire = false;
                    break;
                case 1: // ハンマー
                    if (L && _cd <= 0 && !_hammerWind) { _hammerWind = true; _windT = 0.32f; Swing(0.7f); }
                    if (_hammerWind) { _windT -= dt; if (_windT <= 0) { _hammerWind = false; Melee(90, 3f, true, 9f); Kick(-4f); _cd = 0.55f; Boom(EyePos + Aim * 2f + Vector3.down * 0.6f, 0.8f, new Color(1f, 0.7f, 0.4f)); } }
                    if (R && _cd2 <= 0) { Fire(EyePos + Aim * 0.9f + Right * 0.3f, Aim * 26f, 0, 0, 60, true, ShotKind.Missile); _cd2 = 1.2f; Kick(2f); }
                    break;
                case 2: // 傘
                    if (L && _cd <= 0) { Melee(35, 2.7f, false); Swing(0.3f); _cd = 0.38f; }
                    if (R && _boomer == null) { _boomer = Fire(EyePos + Aim * 0.8f, Aim * 28f, 0, 30, 0, false, ShotKind.Boomerang); _boomer.boomerang = true; }
                    if (E) { _ride = !_ride; if (_ride) { _vel.y = Mathf.Max(_vel.y, 3f); } Play(_sWhoosh, 0.5f); }
                    break;
                case 3: // 弓
                    if (LH && _cd <= 0) _charge = Mathf.Min(1f, _charge + dt);
                    if (LU && _charge > 0) { Fire(EyePos + Aim * 0.7f, Aim * (40f + 55f * _charge), 9f, 25 + 75 * _charge, 0, _charge > 0.9f, ShotKind.Arrow); _charge = 0; _cd = 0.3f; Kick(1.2f); }
                    _zoom = RH;
                    if (E && _abilityCd <= 0) { _dashT = 0.55f; _dashDir = Aim; _dashHit.Clear(); _abilityCd = 3f; Play(_sWhoosh, 0.8f); }
                    break;
                case 4: // 大筒
                    _zoom = RH;
                    if (L && _cd <= 0) { Fire(EyePos + Aim * 1.2f + Right * 0.25f, Aim * 38f, 12f, 0, 110, true, ShotKind.Ball); _cd = 1.6f; Kick(7f); _vel -= Aim * 2.5f; Play(_sBoom, 0.5f); }
                    if (E && _abilityCd <= 0) { Fire(EyePos + Aim * 1.2f, Aim * 38f, 12f, 0, 80, true, ShotKind.Ball); _vel = -Aim * 17f + Vector3.up * 9f; _grounded = false; _abilityCd = 4f; Kick(10f); Play(_sBoom, 0.6f); }
                    break;
                case 5: // 鎖鎌
                    if (L && _cd <= 0) { Melee(32, 2.6f, false); Swing(0.3f); _cd = 0.34f; }
                    if (R && _cd2 <= 0) { ChainThrow(); _cd2 = 0.9f; }
                    if (E && _abilityCd <= 0 && Physics.Raycast(EyePos, Aim, out var h2, 32f, ~0, QueryTriggerInteraction.Ignore) && h2.collider != _cc)
                    {
                        _pull = true; _pullT = 1.6f; _pullTo = h2.point + h2.normal * 0.6f + Vector3.up * 0.4f; _abilityCd = 1.5f;
                        ShowLine(h2.point, new Color(0.7f, 0.7f, 0.75f), 0.25f);
                        Play(_sWhoosh, 0.6f);
                    }
                    break;
            }
        }

        bool WireTarget(out RaycastHit hit)
        {
            hit = default;
            if (_rig == null) return false;
            return Physics.Raycast(EyePos, Aim, out hit, 55f, ~0, QueryTriggerInteraction.Ignore) && hit.collider != _cc && hit.point.y > _rig.transform.position.y - 2f;
        }

        // ───────── 移動わざ（ふつうの歩きの代わりに動かす）─────────

        bool AbilityMove(bool play, float dt)
        {
            Vector2 mv = play ? Act.Move.ReadValue<Vector2>() : Vector2.zero;
            var wish = Forward * mv.y + Right * mv.x;
            var p = _rig.transform.position;

            if (_w == 0 && _wire)
            {
                // 振り子：重力＋入力で動き、ロープの長さを超えたら外へ出ないようにする。ロープは少しずつ巻きとる
                _vel.y -= Gravity * dt;
                _vel += wish * 9f * dt + (_anchor - p).normalized * 6f * dt;
                _ropeLen = Mathf.Max(2.5f, _ropeLen - 7f * dt);
                var c = p + Vector3.up;
                var next = c + _vel * dt;
                var off = next - _anchor;
                if (off.magnitude > _ropeLen) { var fix = _anchor + off.normalized * _ropeLen; _vel = (fix - c) / dt; }
                _vel = Vector3.ClampMagnitude(_vel, 32f);
                if (play && Act.Jump.Down) { _wire = false; _vel += Vector3.up * 6f + Forward * 3f; Play(_sJump, 0.5f); }
                ShowLine(_anchor, new Color(0.4f, 1f, 1f), 0f);
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
            if (_grounded && _fuel < 1) _fuel = Mathf.Min(1, _fuel + dt * 0.45f);

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
                _vel = _dashDir * 34f;
                foreach (var col in Physics.OverlapSphere(p + Vector3.up + _dashDir * 0.8f, 1.3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var t = col.GetComponentInParent<SengokuTarget>();
                    if (t != null && !t.Down && _dashHit.Add(t)) { t.Hit(70, col.ClosestPoint(p + Vector3.up), _dashDir, true); Play(_sHeavy, 0.8f); Sparks(col.ClosestPoint(p + Vector3.up), new Color(1f, 0.9f, 0.5f), 12); }
                }
                if (_dashT <= 0) _vel *= 0.3f;
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

        void Melee(float dmg, float range, bool heavy, float shove = 0)
        {
            var center = EyePos + Aim * range * 0.55f;
            var done = new HashSet<SengokuTarget>();
            bool any = false;
            foreach (var col in Physics.OverlapSphere(center, range * 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = col.GetComponentInParent<SengokuTarget>();
                if (t == null || t.Down || !done.Add(t)) continue;
                var pt = col.ClosestPoint(EyePos);
                t.Hit(dmg, pt, Aim, heavy);
                if (shove > 0) t.Shove(Aim * shove);
                Sparks(pt, heavy ? new Color(1f, 0.6f, 0.3f) : new Color(0.5f, 1f, 1f), heavy ? 14 : 8);
                any = true;
            }
            Play(any ? (heavy ? _sHeavy : _sHit) : _sWhoosh, any ? 0.8f : 0.4f);
        }

        void ChainThrow()
        {
            if (Physics.SphereCast(EyePos, 0.4f, Aim, out var h, 18f, ~0, QueryTriggerInteraction.Ignore) && h.collider != _cc)
            {
                ShowLine(h.point, new Color(0.7f, 0.7f, 0.75f), 0.3f);
                var t = h.collider.GetComponentInParent<SengokuTarget>();
                if (t != null && !t.Down) { t.Hit(20, h.point, -Aim); t.Shove(-Aim * 8f); Play(_sHit, 0.7f); Sparks(h.point, Color.white, 8); }
                else Play(_sClick, 0.5f);
            }
            else ShowLine(EyePos + Aim * 18f, new Color(0.7f, 0.7f, 0.75f), 0.2f);
            Swing(0.25f);
        }

        // ───────── 飛び道具 ─────────

        enum ShotKind { Spark, Missile, Boomerang, Arrow, Ball }

        class Shot
        {
            public Vector3 p, v; public float grav, dmg, aoe, life, t; public bool heavy, boomerang, back;
            public ShotKind kind; public Transform tr;
            public readonly HashSet<SengokuTarget> hit = new HashSet<SengokuTarget>();
        }
        readonly List<Shot> _shots = new List<Shot>();

        Shot Fire(Vector3 from, Vector3 vel, float grav, float dmg, float aoe, bool heavy, ShotKind kind)
        {
            var s = new Shot { p = from, v = vel, grav = grav, dmg = dmg, aoe = aoe, heavy = heavy, kind = kind, life = kind == ShotKind.Boomerang ? 4f : 5f };
            var root = new GameObject("飛び道具").transform; root.SetParent(transform, false);
            switch (kind)
            {
                case ShotKind.Spark: Block(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.18f, new Color(0.4f, 1f, 1f), false, null, true); break;
                case ShotKind.Missile:
                    Block(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.22f, 0.4f, 0.22f), new Color(0.85f, 0.2f, 0.15f), false, new Vector3(90, 0, 0));
                    Block(PrimitiveType.Sphere, root, new Vector3(0, 0, -0.45f), Vector3.one * 0.25f, new Color(1f, 0.7f, 0.3f), false, null, true); break;
                case ShotKind.Boomerang: BuildUmbrella(root, true, 0.9f); break;
                case ShotKind.Arrow:
                    Block(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.03f, 0.03f, 0.9f), new Color(0.6f, 0.45f, 0.3f), false);
                    Block(PrimitiveType.Cube, root, new Vector3(0, 0, 0.47f), new Vector3(0.06f, 0.06f, 0.1f), new Color(0.75f, 0.9f, 1f), false, null, true); break;
                case ShotKind.Ball: Block(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.45f, new Color(0.15f, 0.14f, 0.14f), false); break;
            }
            s.tr = root; root.position = from; root.rotation = Quaternion.LookRotation(vel);
            _shots.Add(s);
            if (kind != ShotKind.Ball) Play(_sWhoosh, 0.45f);
            return s;
        }

        void Shots(float dt)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                s.t += dt; s.life -= dt;
                if (s.boomerang)
                {
                    if (!s.back && s.t > 0.6f) { s.back = true; s.hit.Clear(); }
                    if (s.back) { var to = EyePos - s.p; s.v = to.normalized * 32f; if (to.magnitude < 1.2f) { Kill(i); _boomer = null; continue; } }
                    s.tr.Rotate(0, 900f * dt, 0, Space.World);
                }
                s.v += Vector3.down * s.grav * dt;
                var step = s.v * dt;
                bool gone = s.life <= 0;
                if (!gone && Physics.SphereCast(s.p, s.kind == ShotKind.Ball ? 0.25f : 0.12f, step.normalized, out var h, step.magnitude, ~0, QueryTriggerInteraction.Ignore) && h.collider != _cc)
                {
                    var t = h.collider.GetComponentInParent<SengokuTarget>();
                    if (s.boomerang)
                    {
                        if (t != null && !t.Down && s.hit.Add(t)) { t.Hit(s.dmg, h.point, s.v, false); Sparks(h.point, new Color(0.6f, 0.8f, 1f), 8); Play(_sHit, 0.7f); }
                        else if (t == null) { s.back = true; }
                    }
                    else
                    {
                        if (t != null && !t.Down && s.dmg > 0) { t.Hit(s.dmg, h.point, s.v, s.heavy); Play(s.heavy ? _sHeavy : _sHit, 0.8f); }
                        if (s.aoe > 0) Explode(h.point, s.aoe, s.kind == ShotKind.Ball ? 4.5f : 3.5f, s.heavy);
                        else Sparks(h.point, s.kind == ShotKind.Arrow ? new Color(0.8f, 0.95f, 1f) : new Color(0.4f, 1f, 1f), 7);
                        gone = true;
                    }
                }
                s.p += step;
                if (s.tr) { s.tr.position = s.p; if (!s.boomerang && s.v.sqrMagnitude > 0.01f) s.tr.rotation = Quaternion.LookRotation(s.v); }
                if (gone) { Kill(i); if (s == _boomer) _boomer = null; }
            }
        }

        void Kill(int i)
        {
            if (_shots[i].tr) Destroy(_shots[i].tr.gameObject);
            _shots.RemoveAt(i);
        }

        void Explode(Vector3 at, float dmg, float radius, bool heavy)
        {
            var done = new HashSet<SengokuTarget>();
            foreach (var col in Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = col.GetComponentInParent<SengokuTarget>();
                if (t == null || t.Down || !done.Add(t)) continue;
                var pt = col.ClosestPoint(at);
                float k = 1f - Mathf.Clamp01(Vector3.Distance(pt, at) / radius) * 0.6f;
                t.Hit(dmg * k, pt, pt - at, heavy);
                t.Shove((pt - at).normalized * 6f);
            }
            Boom(at, radius / 3f, new Color(1f, 0.6f, 0.25f));
            Play(_sBoom, 0.7f);
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
                BuildUmbrella(u, false, 1f);
                _openUmbrella = new GameObject("開いた傘").transform; _openUmbrella.SetParent(_viewModels[2].transform, false);
                _openUmbrella.localPosition = new Vector3(0, 0.55f, 0.15f);
                BuildUmbrella(_openUmbrella, true, 2.2f);
                _openUmbrella.gameObject.SetActive(false);
            }
            // 弓
            {
                var b = new GameObject("弓").transform; b.SetParent(_viewModels[3].transform, false);
                b.localPosition = new Vector3(-0.3f, -0.2f, 0.8f); b.localEulerAngles = new Vector3(0, 0, 12); b.localScale = Vector3.one * 0.75f;
                for (int i = -4; i <= 4; i++)
                {
                    float a = i / 4f * 0.9f;
                    Block(PrimitiveType.Cube, b, new Vector3(0, Mathf.Sin(a) * 0.45f, -Mathf.Cos(a) * 0.12f + 0.12f), new Vector3(0.03f, 0.12f, 0.03f), i == 0 ? gold : new Color(0.95f, 0.95f, 0.98f), false, new Vector3(-a * Mathf.Rad2Deg, 0, 0));
                }
                Block(PrimitiveType.Cube, b, new Vector3(0, 0, -0.03f), new Vector3(0.005f, 0.82f, 0.005f), teal, false, null, true).name = "弦";
                Block(PrimitiveType.Cube, b, new Vector3(0.02f, 0, 0.25f), new Vector3(0.02f, 0.02f, 0.65f), new Color(0.6f, 0.45f, 0.3f), false).name = "矢";
            }
            // 大筒
            {
                var c = new GameObject("大筒").transform; c.SetParent(_viewModels[4].transform, false);
                c.localPosition = new Vector3(0.32f, -0.3f, 0.45f); c.localEulerAngles = new Vector3(90, 0, 0);
                Block(PrimitiveType.Cylinder, c, Vector3.zero, new Vector3(0.24f, 0.6f, 0.24f), new Color(0.2f, 0.18f, 0.17f), false);
                foreach (float y in new[] { -0.4f, 0.1f, 0.58f }) Block(PrimitiveType.Cylinder, c, new Vector3(0, y, 0), new Vector3(0.28f, 0.04f, 0.28f), gold, false);
            }
            // 鎖鎌
            {
                var k = new GameObject("鎖鎌").transform; k.SetParent(_viewModels[5].transform, false);
                k.localPosition = new Vector3(0.3f, -0.32f, 0.55f); k.localEulerAngles = new Vector3(-50, 0, 0);
                Block(PrimitiveType.Cylinder, k, Vector3.zero, new Vector3(0.05f, 0.18f, 0.05f), new Color(0.4f, 0.28f, 0.18f), false);
                for (int i = 0; i < 4; i++) Block(PrimitiveType.Cube, k, new Vector3(0.05f + i * 0.05f, 0.2f + Mathf.Sin(i * 0.5f) * 0.04f, 0), new Vector3(0.07f, 0.03f, 0.01f), white, false, new Vector3(0, 0, -i * 18));
                var w = new GameObject("分銅").transform; w.SetParent(_viewModels[5].transform, false); w.localPosition = new Vector3(-0.3f, -0.42f, 0.5f);
                for (int i = 0; i < 6; i++) Block(PrimitiveType.Sphere, w, new Vector3(0.04f * i, 0.03f * i, 0.02f * i), Vector3.one * 0.035f, new Color(0.6f, 0.6f, 0.62f), false);
                Block(PrimitiveType.Sphere, w, Vector3.zero, Vector3.one * 0.09f, new Color(0.35f, 0.35f, 0.37f), false);
            }
            foreach (var r in _viewRoot.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
                case 4: pos += new Vector3(0, 0, -0.12f * Mathf.Clamp01(_cd / 1.6f) * 3f * Mathf.Clamp01(_cd - 1.2f)); break;
                case 5: rot = new Vector3(60 * k, -30 * k, 0); break;
            }
            m.localPosition = Vector3.Lerp(m.localPosition, pos, 1 - Mathf.Exp(-25f * dt));
            m.localRotation = Quaternion.Slerp(m.localRotation, Quaternion.Euler(rot), 1 - Mathf.Exp(-25f * dt));
            if (_flame != null) { _flameT -= dt; _flame.gameObject.SetActive(_flameT > 0); if (_flameT > 0) _flame.localScale = new Vector3(0.25f, 0.25f, 0.5f + Random.value * 0.4f); }
            if (_openUmbrella != null)
            {
                _openUmbrella.gameObject.SetActive(_ride);
                var closed = _viewModels[2].transform.Find("傘");
                if (closed) closed.gameObject.SetActive(!_ride && _boomer == null);
            }
        }
    }
}
