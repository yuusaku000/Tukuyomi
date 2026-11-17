using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の見張り（足軽）。両軍とも、自分の側の要所（町の辻・橋のたもと・山道・湖の岸）に2人ずつ立っている。
    /// 近づいた相手の組（武者・自分・相手の見張り）を攻撃する。持ち場から7mより遠くへは追いかけず、すぐもどる。
    /// 倒すと、そのラウンドのあいだは二度と出てこない（次のラウンドは全員そろって始まる）。
    /// </summary>
    public partial class Sengoku
    {
        class Guard
        {
            public int team; public bool archer; public Vector3 post, faceOut;
            public GameObject go; public CharacterController cc; public SengokuTarget target; public DollAnimator doll;
            public Vector3 vel; public float cd, thinkT, attackPose; public SengokuTarget enemy;
            public bool Gone => target.Down;
            public Vector3 Pos => go.transform.position;
        }

        readonly List<Guard> _guards = new List<Guard>();
        const float GuardLeash = 7f, GuardSight = 15f;

        void BuildGuards()
        {
            // 持ち場（元の数の位置。西は x を負に）。弓の見張りは少しうしろから射る
            var posts = new[] { (new Vector3(36, 0, 9), false), (new Vector3(20, 0, -9), false), (new Vector3(20, 0, 24), true), (new Vector3(20, 0, -24), true), (new Vector3(31, 0, 39), false), (new Vector3(31, 0, -47), false) };
            for (int team = 1; team <= 2; team++)
            {
                float s = team == 1 ? -1 : 1;
                foreach (var (pp, bow) in posts)
                {
                    var c = Sx(new Vector3(pp.x * s, pp.y, pp.z));
                    for (int i = 0; i < 2; i++)
                    {
                        var at = c + new Vector3(0, 0, (i - 0.5f) * 2.4f);
                        _guards.Add(MakeGuard(team, at, bow && i == 1, new Vector3(-s, 0, 0)));
                    }
                }
            }
        }

        Guard MakeGuard(int team, Vector3 at, bool archer, Vector3 faceOut)
        {
            var g = new Guard { team = team, archer = archer, post = at, faceOut = faceOut };
            g.go = new GameObject(team == 1 ? "西の見張り" : "東の見張り");
            g.go.transform.SetParent(transform, false);
            g.cc = g.go.AddComponent<CharacterController>();
            g.cc.height = 1.5f; g.cc.radius = 0.34f; g.cc.center = new Vector3(0, 0.78f, 0); g.cc.stepOffset = 0.45f;
            var main = team == 1 ? new Color(0.1f, 0.38f, 0.42f) : new Color(0.5f, 0.14f, 0.12f);
            var look = new DollLook
            {
                skin = new Color(0.95f, 0.82f, 0.7f), hair = new Color(0.12f, 0.1f, 0.1f), robe = main, robe2 = new Color(0.25f, 0.22f, 0.2f),
                sleeve = Color.Lerp(main, Color.black, 0.2f), cuff = new Color(0.25f, 0.22f, 0.2f), sash = new Color(0.8f, 0.7f, 0.5f),
                eye = new Color(0.15f, 0.1f, 0.1f), accent = new Color(0.8f, 0.7f, 0.5f),
            };
            var parts = Doll.Build("見張り", look, c => Mat(c), g.go.transform);
            parts.root.transform.localScale = Vector3.one * 0.8f;
            g.doll = parts.root.GetComponent<DollAnimator>();
            // 陣笠と、槍か弓
            var hat = new GameObject("陣笠"); hat.transform.SetParent(parts.head, false); hat.transform.localPosition = new Vector3(0, 0.12f, 0); hat.transform.localScale = new Vector3(0.75f, 0.18f, 0.75f);
            hat.AddComponent<MeshFilter>().sharedMesh = Doll.Cone; hat.AddComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.12f, 0.1f, 0.1f));
            var h = new GameObject("持ち物").transform; h.SetParent(parts.armR, false); h.localPosition = new Vector3(0, -0.5f, 0.05f);
            if (archer) for (int i = -3; i <= 3; i++) Block(PrimitiveType.Cube, h, new Vector3(0, i * 0.11f, 0.12f - Mathf.Abs(i) * 0.03f), new Vector3(0.03f, 0.12f, 0.03f), new Color(0.5f, 0.35f, 0.2f), false);
            else
            {
                Block(PrimitiveType.Cylinder, h, new Vector3(0, 0, 0.4f), new Vector3(0.04f, 1.1f, 0.04f), new Color(0.4f, 0.28f, 0.18f), false, new Vector3(90, 0, 0));
                Block(PrimitiveType.Cube, h, new Vector3(0, 0, 1.55f), new Vector3(0.05f, 0.03f, 0.28f), new Color(0.9f, 0.92f, 0.95f), false);
            }
            foreach (var r in g.go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.target = g.go.AddComponent<SengokuTarget>();
            g.target.team = team; g.target.maxHp = g.target.hp = 50; g.target.respawn = 0; g.target.visual = false; g.target.label = "見張り";
            g.target.onDown = t => { Boom(g.Pos + Vector3.up, 0.4f, team == 1 ? new Color(0.3f, 1f, 1f) : new Color(1f, 0.45f, 0.3f)); g.go.SetActive(false); };
            PlaceGuard(g);
            return g;
        }

        void PlaceGuard(Guard g)
        {
            g.cc.enabled = false; g.go.transform.position = g.post + Vector3.up * 0.2f; g.cc.enabled = true;
            g.go.transform.rotation = Quaternion.LookRotation(g.faceOut);
            g.vel = Vector3.zero; g.enemy = null; g.cd = 0;
        }

        /// <summary>ラウンドの始め：見張りを全員、持ち場にもどす（倒されていた見張りも、新しいラウンドではもどる）。</summary>
        void ResetGuards()
        {
            foreach (var g in _guards) { g.target.Revive(); g.go.SetActive(true); PlaceGuard(g); }
        }

        IEnumerable<SengokuTarget> GuardTargetsOf(int team)
        {
            foreach (var g in _guards) if (g.team == team && !g.Gone && g.go.activeSelf) yield return g.target;
        }

        void GuardsTick(float dt)
        {
            bool fighting = _phase == MPhase.Fight;
            foreach (var g in _guards)
            {
                if (g.Gone || !g.go.activeSelf) continue;
                g.cd -= dt; g.thinkT -= dt; g.attackPose -= dt;
                var p = g.Pos;
                if (fighting && g.thinkT <= 0)
                {
                    g.thinkT = 0.4f;
                    SengokuTarget best = null; float bd = GuardSight;
                    foreach (var e in Enemies(g.team))
                    {
                        float d = Vector3.Distance(g.post, e.transform.position);
                        if (d < bd && Visible(p + Vector3.up * 1.3f, e)) { bd = d; best = e; }
                    }
                    g.enemy = best;
                }
                Vector3 want = Vector3.zero, face = g.faceOut;
                if (fighting && g.enemy != null && !g.enemy.Down && g.enemy.gameObject.activeInHierarchy)
                {
                    var ep = g.enemy.transform.position; var to = ep - p; to.y = 0;
                    face = to.normalized;
                    float d = to.magnitude;
                    if (g.archer)
                    {
                        // 弓：持ち場から離れずに射る
                        if (Vector3.Distance(p, g.post) > 1.5f) want = (g.post - p).normalized;
                        if (g.cd <= 0 && d < GuardSight + 4f)
                        {
                            var eye = p + Vector3.up * 1.3f; var aim = ep + Vector3.up * 1.1f;
                            float tt = Vector3.Distance(aim, eye) / 40f;
                            var dir = (aim + VelOf(g.enemy) * tt * 0.5f - eye + Vector3.up * 0.5f * 9f * tt * tt).normalized;
                            var e2 = Random.insideUnitCircle * (5f - _level * 1.5f);
                            dir = Quaternion.AngleAxis(e2.x, Vector3.up) * Quaternion.AngleAxis(e2.y, Vector3.Cross(dir, Vector3.up)) * dir;
                            Fire(eye + face * 0.5f, dir * 40f, 9f, 9f * AiDmg[_level], 0, false, ShotKind.Arrow, g.team, g.cc);
                            g.cd = 2.2f; g.attackPose = 0.4f;
                        }
                    }
                    else
                    {
                        // 槍：持ち場から7mまでは追いかけて突く
                        var chaseTo = ep;
                        var off = chaseTo - g.post; off.y = 0;
                        if (off.magnitude > GuardLeash) chaseTo = g.post + off.normalized * GuardLeash;
                        var mv = chaseTo - p; mv.y = 0;
                        if (d > 2.0f && mv.magnitude > 0.5f) want = mv.normalized;
                        if (d < 2.4f && g.cd <= 0)
                        {
                            Melee(10f * AiDmg[_level], 2.5f, false, 0, p + Vector3.up * 1.2f, (ep + Vector3.up - (p + Vector3.up * 1.2f)).normalized, g.team);
                            g.cd = 1.3f; g.attackPose = 0.3f;
                        }
                    }
                }
                else
                {
                    // 持ち場へもどって、外を向いて立つ
                    var back = g.post - p; back.y = 0;
                    if (back.magnitude > 0.6f) { want = back.normalized; face = want; }
                }
                var flat = new Vector3(g.vel.x, 0, g.vel.z);
                flat = Vector3.Lerp(flat, want * 4.8f, 1 - Mathf.Exp(-10f * dt));
                g.vel.x = flat.x; g.vel.z = flat.z;
                g.vel.y -= Gravity * dt;
                g.cc.Move(g.vel * dt);
                if (g.cc.isGrounded && g.vel.y < 0) g.vel.y = -2f;
                if (face.sqrMagnitude > 0.01f) g.go.transform.rotation = Quaternion.Slerp(g.go.transform.rotation, Quaternion.LookRotation(face), 1 - Mathf.Exp(-8f * dt));
                g.doll.speed = flat.magnitude;
                g.doll.holdR = g.attackPose > 0 ? 1 : 0; g.doll.holdREuler = new Vector3(-95, 0, 0);
                g.target.facing = g.go.transform.forward;
                if (p.y < -19f) PlaceGuard(g);
            }
        }
    }
}
