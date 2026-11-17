using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 武者（CPU）の移動わざ。武器ごとに、決めた場面で使う（強いほどよく使い、待ち時間も短い）。
    ///  二刀流：引っかけて一気に引き寄せられる（高いところへ・谷を渡る・逃げる）
    ///  ハンマー：ジェットで斜め上へ（谷を飛びこえる・山の上へ・高いところの敵へ飛びこむ）
    ///  傘：吹き抜けの乗物（駕籠）に乗って浮いてすべる（湖を渡って浮き島へ）
    ///  弓：自分が矢になって一直線に飛ぶ（敵へ突っこむ・囲まれて逃げる）
    ///  金棒：大きく振って、敵へ一気に跳びこむ（弓の強い版）・遠回りの近道
    ///  団扇：あおいで反対方向へ飛ぶ（近づかれたら逃げる・遠回りの近道）
    /// 使ったあとは、近くの道すじから進みなおす。
    /// </summary>
    public partial class Sengoku
    {
        const int SkNone = 0, SkHook = 1, SkJet = 2, SkHover = 3, SkDash = 4, SkRecoil = 5;
        static readonly float[] SkillChance = { 0.2f, 0.55f, 0.9f }, SkillCd = { 9f, 5.5f, 3.5f };

        /// <summary>移動わざを使っている最中なら動かして true。</summary>
        bool UnitSkill(Unit u, float dt)
        {
            if (u.skill == SkNone) return false;
            u.skillT += dt;
            var p = u.Pos;
            bool done = false;
            switch (u.skill)
            {
                case SkHook:
                    {
                        var to = u.skillTarget - (p + Vector3.up);
                        u.vel = to.normalized * 24f;
                        SkillRope(u, true, u.skillTarget, new Color(0.4f, 1f, 1f));
                        if (to.magnitude < 1.6f || u.skillT > 2.6f) { done = true; u.vel = Vector3.up * 5f + new Vector3(to.x, 0, to.z).normalized * 2f; }
                    }
                    break;
                case SkJet:
                    {
                        var flat = u.skillTarget - p; flat.y = 0;
                        var dir = flat.sqrMagnitude > 0.01f ? flat.normalized : u.go.transform.forward;
                        float climb = u.skillTarget.y + 3f - p.y;
                        u.vel.x = dir.x * 22f; u.vel.z = dir.z * 22f;
                        u.vel.y = Mathf.Lerp(u.vel.y, Mathf.Clamp(climb * 1.5f, -4f, 11f), 1 - Mathf.Exp(-4f * dt));
                        SkillFlame(u, true);
                        if ((flat.magnitude < 3f && p.y > u.skillTarget.y - 0.8f) || u.skillT > 3f) { done = true; u.vel = new Vector3(dir.x * 6f, 2f, dir.z * 6f); }
                        if (flat.magnitude < 3f) { u.vel.x *= 0.2f; u.vel.z *= 0.2f; u.vel.y = Mathf.Max(u.vel.y, 9f); } // 真上へは、高さに届くまで上がる
                    }
                    break;
                case SkHover:
                    {
                        var flat = u.skillTarget - p; flat.y = 0;
                        var dir = flat.sqrMagnitude > 0.01f ? flat.normalized : u.go.transform.forward;
                        u.vel.x = dir.x * 12f; u.vel.z = dir.z * 12f;
                        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var g, 4.5f, ~0, QueryTriggerInteraction.Ignore) && g.collider != u.cc)
                            u.vel.y = Mathf.Lerp(u.vel.y, (g.point.y + 1.3f - p.y) * 5f, 1 - Mathf.Exp(-8f * dt));
                        else u.vel.y = Mathf.Max(u.vel.y - 6f * dt, -1.6f);
                        SkillCanopy(u, true);
                        u.target.frontGuard = 0.7f;
                        if (flat.magnitude < 2f || u.skillT > 9f) done = true;
                    }
                    break;
                case SkDash:
                    {
                        bool kanabo = u.weapon == 4; // 金棒は弓より速く・長く・痛い
                        u.vel = u.skillDir * (kanabo ? 36f : 30f);
                        foreach (var col in Physics.OverlapSphere(p + Vector3.up + u.skillDir * 0.8f, 1.3f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            var t = col.GetComponentInParent<SengokuTarget>();
                            if (t == null || t.Down || t.team == u.team || !u.skillHits.Add(t)) continue;
                            var pt = col.ClosestPoint(p + Vector3.up);
                            t.Hit((kanabo ? 55f : 45f) * AiDmg[_level], pt, u.skillDir, true, true);
                            if (t == _me) _vel += u.skillDir * 8f;
                            Sparks(pt, new Color(1f, 0.9f, 0.5f), 10);
                        }
                        if (u.skillT > (kanabo ? 0.65f : 0.5f)) { done = true; u.vel *= kanabo ? 0.5f : 0.3f; }
                    }
                    break;
                case SkRecoil:
                    if (u.skillT > 0.7f || (u.cc.isGrounded && u.skillT > 0.2f)) done = true;
                    else u.vel.y -= Gravity * dt;
                    break;
            }
            var flags = u.cc.Move(u.vel * dt);
            if (u.skill == SkHook && (flags & CollisionFlags.Sides) != 0 && u.skillT > 0.4f && u.vel.magnitude < 3f) done = true;
            var fwd = new Vector3(u.vel.x, 0, u.vel.z);
            if (fwd.sqrMagnitude > 0.1f && u.skill != SkRecoil) u.go.transform.rotation = Quaternion.Slerp(u.go.transform.rotation, Quaternion.LookRotation(fwd.normalized), 1 - Mathf.Exp(-10f * dt));
            u.doll.speed = 0; u.doll.holdR = 1; u.doll.holdREuler = new Vector3(-150, 0, 0);
            if (done) EndSkill(u);
            return true;
        }

        void EndSkill(Unit u)
        {
            if (Ts.Flag("--sengoku-log")) Debug.Log($"[SengokuSkill] t={300 - _clock:0} {u.name} end at={u.Pos:F0} after={u.skillT:F1}s");
            u.skill = SkNone; u.skillT = 0;
            u.skillCd = SkillCd[_level] * Random.Range(0.8f, 1.3f);
            u.target.frontGuard = 0;
            SkillRope(u, false, Vector3.zero, Color.white); SkillFlame(u, false); SkillCanopy(u, false);
            // 着いた場所の近くの道すじから、進みなおす
            u.repathT = 0; u.thinkT = 0; u.stuck = 0; u.path.Clear(); u.pathI = 0;
        }

        void StartSkill(Unit u, int kind, Vector3 target, Vector3 dir)
        {
            u.skill = kind; u.skillT = 0; u.skillTarget = target; u.skillDir = dir; u.skillHits.Clear();
            if (Ts.Flag("--sengoku-log")) Debug.Log($"[SengokuSkill] t={300 - _clock:0} {u.name} {Weapons[u.weapon].name} kind={kind} from={u.Pos:F0} to={target:F0}");
            if (kind == SkJet || kind == SkHook) u.vel.y = Mathf.Max(u.vel.y, 4f);
            if (Vector3.Distance(u.Pos, EyePos) < 30f) Play(_sWhoosh, 0.5f);
        }

        // ───────── 使う場面を決める ─────────

        void TrySkill(Unit u, SengokuTarget enemy, float ed, float dt)
        {
            if (u.skill != SkNone) return;
            u.skillCd -= dt; u.skillCheckT -= dt;
            if (u.skillCd > 0 || u.skillCheckT > 0 || !u.cc.isGrounded) return;
            u.skillCheckT = 0.5f;
            if (Random.value > SkillChance[_level]) return;
            var p = u.Pos; var eye = u.Eye;

            // ── 戦いの中で ──
            if (enemy != null)
            {
                var to = enemy.transform.position - p; var flat = new Vector3(to.x, 0, to.z);
                if (u.weapon == 4 && u.stance == 0 && ed > 7f && ed < 22f && u.target.hp > 40f && Visible(eye, enemy))
                {
                    // 金棒：大きく振って、敵へ跳びこむ
                    StartSkill(u, SkDash, Vector3.zero, (enemy.transform.position + Vector3.up * 1.6f - eye).normalized);
                    return;
                }
                if (u.weapon == 3)
                {
                    // 弓：8〜18m の敵へ矢になって突っこむ（体力があって攻めるとき）。囲まれて下がるときは逆向きに
                    if (u.stance == 0 && ed > 8f && ed < 18f && u.target.hp > 50f && Visible(eye, enemy)) { StartSkill(u, SkDash, Vector3.zero, (enemy.transform.position + Vector3.up - eye).normalized); return; }
                    if (u.stance == 3 && ed < 10f) { var away = (-flat.normalized + Vector3.up * 0.15f).normalized; if (!Physics.Raycast(eye, away, 10f, ~0, QueryTriggerInteraction.Ignore)) { StartSkill(u, SkDash, Vector3.zero, away); return; } }
                }
                if (u.weapon == 5 && (ed < 4.5f && u.stance == 3 || ed < 3f && u.target.hp < 50f))
                {
                    // 団扇：近づかれたら、あおいで後ろへ飛ぶ（前の相手は風で押す）
                    var aim = (enemy.transform.position + Vector3.up - eye).normalized;
                    Melee(6 * AiDmg[_level], 4.5f, false, 8f, eye, aim, u.team);
                    u.vel = -flat.normalized * 16f + Vector3.up * 8f;
                    StartSkill(u, SkRecoil, Vector3.zero, -flat.normalized);
                    return;
                }
                if (u.weapon == 1 && to.y > 3f && ed < 28f && ed > 8f)
                {
                    // ハンマー：高いところの敵へ飛びこむ
                    StartSkill(u, SkJet, enemy.transform.position, flat.normalized);
                    return;
                }
                if (u.weapon == 0 && u.stance == 3)
                {
                    // 二刀流：逃げるとき、敵と反対の高いところへ引っかける
                    for (int k = 0; k < 6; k++)
                    {
                        var dir = Quaternion.Euler(-35f - Random.value * 15f, Random.Range(-60f, 60f), 0) * Quaternion.LookRotation(-flat.normalized) * Vector3.forward;
                        if (Physics.Raycast(eye, dir, out var h, 32f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y > 0.5f && h.point.y > p.y + 3f && h.collider.GetComponentInParent<SengokuTarget>() == null)
                        { StartSkill(u, SkHook, h.point + Vector3.up * 0.8f, dir); return; }
                    }
                }
                if (ed < 16f) return; // 近くに敵がいるときは、移動の近道には使わない
            }

            // ── 移動の近道 ──
            if (u.path == null || u.path.Count == 0 || u.pathI >= u.path.Count) return;
            var next = u.path[u.pathI];
            var goal = u.path[u.path.Count - 1];
            // 高いところへ（山の段・櫓の台）
            if (next.y > p.y + 3f && Vector3.Distance(p, next) < 34f)
            {
                if (u.weapon == 0)
                {
                    var tgt = next + Vector3.up * 0.8f;
                    if (!Physics.Linecast(eye, tgt, out var h, ~0, QueryTriggerInteraction.Ignore) || Vector3.Distance(h.point, tgt) < 2.5f)
                    { StartSkill(u, SkHook, tgt, (tgt - eye).normalized); return; }
                    // 崖のふちに隠れているときは、ふちの上をねらう
                    var edgeDir = new Vector3(tgt.x - eye.x, 0, tgt.z - eye.z).normalized;
                    if (Physics.Raycast(h.point - edgeDir * 0.3f + edgeDir * 1.5f + Vector3.up * 12f, Vector3.down, out var top, 14f, ~0, QueryTriggerInteraction.Ignore) && top.normal.y > 0.6f && top.point.y > p.y + 2f)
                    {
                        var lip = top.point + Vector3.up * 0.8f;
                        if (!Physics.Linecast(eye, lip, out var h3, ~0, QueryTriggerInteraction.Ignore) || Vector3.Distance(h3.point, lip) < 2.5f)
                        { StartSkill(u, SkHook, lip, (lip - eye).normalized); return; }
                    }
                }
                if (u.weapon == 1) { StartSkill(u, SkJet, next, (next - p).normalized); return; }
            }
            // 遠回りしている（谷を渡る・湖を渡る）：まっすぐの距離よりずっと長い道なら、近道する
            float pathLen = 0; var prev = p;
            for (int i = u.pathI; i < u.path.Count; i++) { pathLen += Vector3.Distance(prev, u.path[i]); prev = u.path[i]; }
            float straight = new Vector2(goal.x - p.x, goal.z - p.z).magnitude;
            if (pathLen < straight * 1.45f || straight < 20f) return;
            var gdir = new Vector3(goal.x - p.x, 0, goal.z - p.z).normalized;
            if (u.weapon == 2 && InLake(p + gdir * 8f + Vector3.down * 0.5f)) { StartSkill(u, SkHover, goal, gdir); return; } // 傘：湖を浮いて渡る
            // 道すじのうち、届く距離でいちばん先の点へ（見通しがきき、高さが近い点）
            float hop = u.weapon == 1 ? 48f : 40f;
            for (int i = u.path.Count - 1; i > u.pathI; i--)
            {
                var n = u.path[i];
                if (Vector3.Distance(p, n) > hop || Mathf.Abs(n.y - p.y) > 6f || InLake(n + Vector3.down * 0.3f)) continue;
                var tgt = n + Vector3.up * 0.8f;
                if (Physics.Linecast(eye, tgt, out var h2, ~0, QueryTriggerInteraction.Ignore) && Vector3.Distance(h2.point, tgt) > 2.5f && u.weapon != 1) continue;
                if (u.weapon == 1) StartSkill(u, SkJet, n, gdir);
                else if (u.weapon == 0) StartSkill(u, SkHook, tgt, (tgt - eye).normalized);
                else if (u.weapon == 4) StartSkill(u, SkDash, Vector3.zero, (tgt + Vector3.up * 2f - eye).normalized); // 金棒：大きく跳んで近道
                else if (u.weapon == 5) { u.vel = gdir * 15f + Vector3.up * 8f; StartSkill(u, SkRecoil, Vector3.zero, gdir); } // 団扇：うしろをあおいで前へ飛ぶ
                else return;
                u.pathI = i;
                return;
            }
        }

        // ───────── 見た目（ロープ・炎・開いた傘）─────────

        void SkillRope(Unit u, bool on, Vector3 to, Color c)
        {
            if (!on) { if (u.rope) u.rope.gameObject.SetActive(false); return; }
            if (u.rope == null) u.rope = Block(PrimitiveType.Cube, transform, Vector3.zero, Vector3.one, c, false, null, true).transform;
            var from = u.Pos + Vector3.up * 1.3f + u.go.transform.right * 0.3f;
            var d = to - from;
            u.rope.gameObject.SetActive(true);
            u.rope.position = (from + to) * 0.5f; u.rope.rotation = Quaternion.LookRotation(d.normalized); u.rope.localScale = new Vector3(0.06f, 0.06f, d.magnitude);
        }

        void SkillFlame(Unit u, bool on)
        {
            if (u.flame == null)
            {
                if (!on) return;
                u.flame = Block(PrimitiveType.Sphere, u.go.transform, new Vector3(0, 0.9f, -0.6f), new Vector3(0.4f, 0.4f, 0.9f), new Color(1f, 0.6f, 0.2f), false, null, true).transform;
            }
            u.flame.gameObject.SetActive(on);
            if (on) u.flame.localScale = new Vector3(0.4f, 0.4f, 0.8f + Random.value * 0.6f);
        }

        void SkillCanopy(Unit u, bool on)
        {
            if (u.canopy == null)
            {
                if (!on) return;
                u.canopy = new GameObject("乗物").transform; u.canopy.SetParent(u.go.transform, false); u.canopy.localPosition = new Vector3(0, 0.05f, 0);
                BuildNorimono(u.canopy);
            }
            u.canopy.gameObject.SetActive(on);
        }
    }
}
