using System.Collections.Generic;
using UnityEngine;
using F = Tsukuyomi.SetsunaFighter;

namespace Tsukuyomi
{
    /// <summary>SETSUNA：当たり・ガード・刹那ガード・コンボ・狐火・ラウンドの勝ち負け・コンピューターの頭。</summary>
    public partial class Setsuna
    {
        int _comboP, _comboC, _comboDmgP, _comboDmgC;
        float _comboShowP, _comboShowC;

        class Proj { public F owner; public Vector3 p, v; public Transform tr; public float life; }
        readonly List<Proj> _projs = new List<Proj>();

        // ───────── 当たり ─────────

        void Resolve(F a, F d, bool proj = false)
        {
            if (!proj && (a.st != F.St.Attack || a.move == null || a.move.projectile || a.Phase != 1)) return;
            var m = a.move;
            if (m.hits > 1)
            {
                // 多段：出ている間を hits 回に分けて当てる
                int due = Mathf.Min(m.hits, 1 + (int)((a.t - m.startup) / (m.active / m.hits)));
                if (a.superHits >= due) return;
            }
            else if (a.moveHit) return;

            var off = d.pos - a.pos; off.y = 0;
            float dist = off.magnitude;
            if (!proj && dist > m.reach + 0.3f) return;
            if (!proj && Mathf.Abs(a.y - d.y) > 1.6f) return;
            if (d.invuln > 0 || d.st == F.St.KO || d.st == F.St.Down) return;

            if (m.h == Height.Throw && (d.Airborne || d.st == F.St.Hitstun || d.st == F.St.Blockstun || d.st == F.St.Attack && d.move == SetsunaMoves.Super)) return;
            if (m.h == Height.High && d.crouch && !d.Airborne && d.st != F.St.Hitstun) return; // しゃがんでかわした
            if (m.h == Height.Low && d.Airborne) return;

            bool canBlock = (d.st == F.St.Idle || d.st == F.St.Blockstun) && !d.Airborne && m.h != Height.Throw && d.guard;
            bool blocked = canBlock && (m.h == Height.Mid || (m.h == Height.Low ? d.crouch : !d.crouch));
            var contact = d.pos + Vector3.up * (d.crouch ? 0.8f : 1.25f) + d.y * Vector3.up - a.face * 0.35f;

            if (m.hits > 1) a.superHits++; else a.moveHit = true;

            if (blocked)
            {
                bool just = Time.time - d.lastGuardPress < 0.14f || (!d.isPlayer && d.jgReady);
                d.jgReady = false;
                d.st = F.St.Blockstun; d.t = 0;
                d.stun = just ? m.blockstun * 0.4f : m.blockstun;
                Push(a, d, m.push * (just ? 0.4f : 1f));
                if (just)
                {
                    a.stun += 0.35f; // 攻めた側のすきが大きくなる
                    d.gauge = Mathf.Min(100, d.gauge + 15);
                    Time.timeScale = 0.3f; _slowT = 0.5f;
                    Center("刹那!", Board.Gold, 0.9f, d.isPlayer ? "刹那ガード ― 反撃のチャンス" : "見切られた");
                    Play(_sJust, 0.9f);
                    Sparks(contact, new Color(1f, 0.9f, 0.5f), 18, 1.4f);
                }
                else
                {
                    int chip = m.id == "rush" || m.id == "fox" || m.id == "super" ? Mathf.RoundToInt(m.dmg * 0.1f) : 0;
                    d.hp = Mathf.Max(1, d.hp - chip);
                    Play(_sGuard, 0.6f);
                    Sparks(contact, new Color(0.6f, 0.9f, 1f), 6, 0.6f);
                    d.gauge = Mathf.Min(100, d.gauge + 3);
                }
                _hitstop = 0.05f;
                return;
            }

            // 当たった
            bool inCombo = d.st == F.St.Hitstun || d.st == F.St.Juggle;
            int combo = (inCombo ? (a.isPlayer ? _comboP : _comboC) + 1 : 1);
            int comboDmg = combo == 1 ? 0 : (a.isPlayer ? _comboDmgP : _comboDmgC);
            float scale = combo <= 2 ? 1f : Mathf.Max(0.3f, 1f - 0.1f * (combo - 2));
            float per = m.hits > 1 ? m.dmg / (float)m.hits : m.dmg;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(per * a.style.power * scale));
            d.hp = Mathf.Max(0, d.hp - dmg);
            comboDmg += dmg;
            if (a.isPlayer) { _comboP = combo; _comboDmgP = comboDmg; _comboShowP = 1.4f; }
            else { _comboC = combo; _comboDmgC = comboDmg; _comboShowC = 1.4f; }
            a.gauge = Mathf.Min(100, a.gauge + m.gauge);
            d.gauge = Mathf.Min(100, d.gauge + m.gauge * 0.5f);

            bool lastHit = m.hits <= 1 || a.superHits >= m.hits;
            d.t = 0; d.crouch = false; d.guard = false; d.move = null;
            if (d.Airborne || (m.launch && lastHit))
            {
                d.st = F.St.Juggle; d.juggle++;
                d.vy = m.launch && lastHit ? 8f : 4.2f; d.y = Mathf.Max(d.y, 0.02f);
                d.airVel = a.face * (m.launch ? 0.8f : 1.2f);
                if (d.juggle > 7) d.invuln = 3f; // 浮かせつづけるのはここまで
            }
            else if (m.knockdown && lastHit)
            {
                d.st = F.St.Down; d.stun = 0.85f;
                if (m.h == Height.Throw) { d.vy = 4f; d.y = 0.02f; Center("投げ", Board.Vermilion, 0.6f, ""); }
            }
            else { d.st = F.St.Hitstun; d.stun = m.hitstun; }
            Push(a, d, m.push);

            bool big = m.heavy || m.hits > 1 || m.h == Height.Throw;
            _hitstop = m.hits > 1 && !lastHit ? 0.04f : big ? 0.11f : 0.06f;
            _shake = Mathf.Max(_shake, big ? 0.09f : 0.04f);
            Play(big ? _sHitHeavy : _sHit, big ? 0.95f : 0.75f);
            Sparks(contact, a.isPlayer ? new Color(0.4f, 1f, 1f) : new Color(1f, 0.45f, 0.3f), big ? 16 : 9, big ? 1.2f : 0.8f);

            if (d.hp <= 0) KO(a, d);
        }

        void Push(F a, F d, float amount)
        {
            var np = d.pos + a.face * amount;
            var flat = np; flat.y = 0;
            if (flat.magnitude > ArenaR) a.pos -= a.face * amount; // 端なら攻めた側が下がる
            else d.pos = np;
        }

        void Separate()
        {
            var off = C.pos - P.pos; off.y = 0;
            float dist = off.magnitude;
            if (dist < 0.78f && Mathf.Abs(P.y - C.y) < 1.4f)
            {
                var n = dist > 0.001f ? off / dist : Vector3.forward;
                float push = (0.78f - dist) * 0.5f;
                P.pos -= n * push; C.pos += n * push;
            }
        }

        // ───────── 狐火 ─────────

        void Shoot(F f)
        {
            var pr = new Proj { owner = f, p = f.pos + f.face * 0.7f + Vector3.up * 1.15f, v = f.face * 9f, life = 2.6f };
            pr.tr = new GameObject("狐火").transform;
            pr.tr.SetParent(_arena, false);
            var col = f.isPlayer ? new Color(0.35f, 1f, 1f) : new Color(0.75f, 0.5f, 1f);
            Ball(pr.tr, Vector3.zero, 0.42f, col);
            Ball(pr.tr, new Vector3(0, 0, -0.3f), 0.3f, col * 0.8f);
            Ball(pr.tr, new Vector3(0, 0, -0.55f), 0.18f, col * 0.6f);
            pr.tr.position = pr.p; pr.tr.rotation = Quaternion.LookRotation(f.face);
            _projs.Add(pr);
            Play(_sWhoosh, 0.7f);
        }

        void Projectiles(float dt)
        {
            for (int i = _projs.Count - 1; i >= 0; i--)
            {
                var pr = _projs[i];
                pr.p += pr.v * dt; pr.life -= dt;
                if (pr.tr) { pr.tr.position = pr.p; pr.tr.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 30f) * 0.08f); }
                var d = pr.owner == P ? C : P;
                var off = d.pos - pr.p; off.y = 0;
                bool gone = pr.life <= 0 || new Vector2(pr.p.x, pr.p.z).magnitude > ArenaR + 3;
                // 狐火どうしは打ち消しあう
                foreach (var q in _projs) if (q != pr && q.owner != pr.owner && (q.p - pr.p).sqrMagnitude < 0.5f) { gone = true; q.life = 0; Sparks(pr.p, Color.white, 12, 1f); }
                if (!gone && off.magnitude < 0.6f && d.y < 0.9f && d.invuln <= 0 && d.st != F.St.KO && d.st != F.St.Down)
                {
                    gone = true;
                    var a = pr.owner;
                    // 当たりの計算は技と同じ（届く距離の判定だけ飛ばす）
                    var keep = (a.st, a.move, a.t, a.moveHit, a.pos);
                    a.st = F.St.Attack; a.move = SetsunaMoves.Fox; a.t = SetsunaMoves.Fox.startup + 0.01f; a.moveHit = false;
                    a.pos = d.pos - a.face * 0.5f;
                    Resolve(a, d, true);
                    (a.st, a.move, a.t, a.moveHit, a.pos) = keep;
                }
                if (gone) { if (pr.tr) Destroy(pr.tr.gameObject); _projs.RemoveAt(i); }
            }
        }

        // ───────── ラウンド ─────────

        void KO(F a, F d)
        {
            d.st = F.St.KO; d.t = 0; d.vy = Mathf.Max(d.vy, 3f); d.y = Mathf.Max(d.y, 0.02f);
            Time.timeScale = 0.35f; _slowT = 1.1f;
            EndRound(false);
        }

        void EndRound(bool timeUp)
        {
            if (_phase != Phase.Fight) return;
            F winner = null;
            if (P.hp > C.hp) winner = P; else if (C.hp > P.hp) winner = C;
            if (winner != null) winner.wins++;
            string sub = winner == null ? "引き分け" : winner == P ? "あなたの勝ち" : $"{C.style.name}の勝ち";
            Center(timeUp ? "TIME" : "K.O.", timeUp ? Board.Gold : Board.Vermilion, 2.6f, sub);
            Play(_sGong, 0.8f);
            SetPhase(Phase.RoundEnd);
        }

        void AfterRound()
        {
            if (P.wins >= 2 || C.wins >= 2 || _round >= 3) { ShowResult(P.wins > C.wins); return; }
            NextRound();
        }

        // ───────── コンピューター ─────────

        float _aiNext, _aiGuardUntil, _aiMoveUntil, _aiAirDecided;
        Vector2 _aiMove;
        bool _aiGuardLow;
        object _aiSeenMove; float _aiSeenT = -1;
        readonly Queue<string> _aiPlan = new Queue<string>();

        void Brain()
        {
            var f = C; var o = P;
            float now = Time.time;
            var off = o.pos - f.pos; off.y = 0;
            float dist = off.magnitude;
            int L = _level;

            // 相手の攻撃を見てガードを決める（1つの技につき1回）
            bool threat = o.st == F.St.Attack && o.move != null && o.Phase < 2 && dist < o.move.reach + 1.0f && !o.move.projectile;
            foreach (var pr in _projs) if (pr.owner == o && Vector3.Dot(pr.v, f.pos - pr.p) > 0 && (f.pos - pr.p).magnitude < 4f) threat = true;
            if (threat && (_aiSeenMove != (object)o.move || o.t < _aiSeenT))
            {
                _aiSeenMove = o.move; _aiSeenT = o.t;
                if (Random.value < AiBlock[L])
                {
                    _aiGuardUntil = now + 0.45f;
                    _aiGuardLow = o.move != null && o.move.h == Height.Low;
                    f.jgReady = Random.value < AiJust[L];
                }
            }
            if (now < _aiGuardUntil && f.st != F.St.Attack)
            {
                f.inMove = new Vector2(0, -1); f.inCrouch = _aiGuardLow;
                return;
            }
            f.jgReady = false;

            // コンボのつづき
            if (f.st == F.St.Attack && f.CanCancel() && _aiPlan.Count > 0) { Press(f, _aiPlan.Dequeue()); return; }
            if (f.st == F.St.Attack) return;
            if (!f.CanAct && f.st != F.St.Air) { _aiPlan.Clear(); return; }

            // 跳んできたら月輪昇
            if (o.st == F.St.Air && o.vy < 2f && dist < 2.4f && now > _aiAirDecided)
            {
                _aiAirDecided = now + 0.8f;
                if (Random.value < AiAnti[L]) { Press(f, "upper"); return; }
            }
            // すきを突く
            if (o.st == F.St.Attack && o.Phase == 2 && dist < 1.6f && Random.value < AiPunish[L] * Time.deltaTime * 8f) { StartCombo(f, L); return; }

            if (now >= _aiNext)
            {
                _aiNext = now + AiReact[L] * Random.Range(0.7f, 1.4f) / Mathf.Max(0.6f, f.style.speed);
                float r = Random.value;
                if (dist > 2.3f)
                {
                    if (f.style.fox && dist > 3.5f && r < AiSpecial[L] * 0.9f) Press(f, "fox");
                    else if (r < AiAggr[L] * 0.35f) f.inDashF = true;
                    else if (r < 0.08f + L * 0.04f && dist < 4f) { f.inMove = new Vector2(0, 1); f.inJump = true; _aiPlan.Clear(); _aiPlan.Enqueue("heavy"); }
                    else Hold(new Vector2(Random.Range(-0.4f, 0.4f), 1), 0.5f);
                }
                else if (dist <= 1.55f)
                {
                    if (r < AiThrow[L]) Press(f, "throw");
                    else if (r < AiThrow[L] + AiAggr[L]) StartCombo(f, L);
                    else if (r < 0.85f) Hold(new Vector2(Random.value < 0.5f ? -1 : 1, -0.3f), 0.35f);
                    else Hold(new Vector2(0, -1), 0.4f);
                }
                else
                {
                    if (r < AiSpecial[L] * 0.5f) Press(f, "rush");
                    else Hold(new Vector2(Random.Range(-0.6f, 0.6f), 1), 0.3f);
                }
            }
            if (f.st == F.St.Air && _aiPlan.Count > 0 && f.vy < 0.5f) { Press(f, _aiPlan.Dequeue()); return; }
            if (now < _aiMoveUntil && f.inMove == Vector2.zero) f.inMove = _aiMove;
        }

        void Hold(Vector2 mv, float sec) { _aiMove = mv; _aiMoveUntil = Time.time + sec; }

        void StartCombo(F f, int L)
        {
            _aiPlan.Clear();
            bool low = Random.value < 0.35f;
            var seq = low ? new[] { "light", "heavy" } : new[] { "light", "light", "heavy", Random.value < 0.5f ? "rush" : "upper" };
            if (low) { f.inCrouch = true; Hold(new Vector2(0, 0), 0.5f); }
            int n = Mathf.Min(seq.Length, AiCombo[L] + 1);
            for (int i = 1; i < n; i++) _aiPlan.Enqueue(seq[i]);
            Press(f, seq[0]);
        }
    }
}
