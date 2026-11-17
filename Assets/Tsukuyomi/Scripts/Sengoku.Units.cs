using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の武者（コンピューター）。西軍＝自分と味方2人、東軍＝敵3人。
    /// 道すじ（通りや橋や坂の点のつながり）をたどって、櫓を取りに行く・大将落としを叩く・近くの敵と戦う。
    /// 移動わざは使わない（歩く・走る・跳ぶだけ）。武器ごとに、近づいて斬るか、離れて撃つ。
    /// </summary>
    public partial class Sengoku
    {
        public class Unit
        {
            public string name; public int team, weapon, role, lives = 2;
            public GameObject go; public CharacterController cc; public SengokuTarget target; public DollAnimator doll;
            public Vector3 vel; public float cd, cd2, respawnT, stuckT, repathT, attackPose;
            public Vector3 lastPos, goal = new(999, 0, 0);
            public List<Vector3> path = new List<Vector3>(); public int pathI;
            public SengokuTarget enemy; public float thinkT; public bool healing;
            public Board tag; public RectTransform hpFill;
            public bool Dead => target.Down;
            public Vector3 Pos => go.transform.position;
            public Vector3 Eye => go.transform.position + Vector3.up * 1.5f;
        }

        readonly List<Unit> _units = new List<Unit>();
        static readonly string[] WestNames = { "茜", "楓" }, EastNames = { "疾風", "岩鉄", "朧" };
        static readonly float[] AiErr = { 7f, 3.8f, 1.8f }, AiDmg = { 0.6f, 0.9f, 1.15f }, AiThink = { 0.7f, 0.45f, 0.28f };

        // ───────── 道すじ ─────────

        readonly List<Vector3> _nodes = new List<Vector3>();
        readonly List<List<int>> _edges = new List<List<int>>();

        int Node(Vector3 p) { _nodes.Add(p * K); _edges.Add(new List<int>()); return _nodes.Count - 1; }
        void Link(int a, int b) { _edges[a].Add(b); _edges[b].Add(a); }

        void BuildGraph()
        {
            int Side(float s, out int sp, out int dar, out int gate, out int c36, out int c20, out int n36, out int n20, out int brn, out int s36, out int s20, out int brs,
                     out int n36b, out int mt0, out int mt1, out int mt2, out int mt3, out int lk0, out int lk1, out int lk2, out int vw1)
            {
                sp = Node(new Vector3(s * 60, 0, 8)); dar = Node(new Vector3(s * 53, 0, 0)); gate = Node(new Vector3(s * 44, 0, 0));
                c36 = Node(new Vector3(s * 36, 0, 0)); c20 = Node(new Vector3(s * 20, 0, 0));
                n36 = Node(new Vector3(s * 36, 0, 18)); n20 = Node(new Vector3(s * 20, 0, 18)); brn = Node(new Vector3(s * 11.5f, 0, 18));
                s36 = Node(new Vector3(s * 36, 0, -18)); s20 = Node(new Vector3(s * 20, 0, -18)); brs = Node(new Vector3(s * 11.5f, 0, -18));
                n36b = Node(new Vector3(s * 36, 0, 33)); mt0 = Node(new Vector3(s * 37, 0, 44)); mt1 = Node(new Vector3(s * 19, 6, 44));
                mt2 = Node(new Vector3(s * 7, 6, 34.5f)); mt3 = Node(new Vector3(s * 7, 12, 43.5f));
                lk0 = Node(new Vector3(s * 36, 0, -33)); lk1 = Node(new Vector3(s * 29, 0, -42)); lk2 = Node(new Vector3(s * 15, 0.6f, -42));
                vw1 = Node(new Vector3(s * 10.6f, 0, s < 0 ? 11 : -11));
                Link(sp, dar); Link(dar, gate); Link(gate, c36); Link(c36, c20); Link(c36, n36); Link(c36, s36); Link(c20, n20); Link(c20, s20);
                Link(n36, n20); Link(s36, s20); Link(n20, brn); Link(s20, brs); Link(n36, n36b); Link(n36b, mt0); Link(mt0, mt1); Link(mt1, mt2); Link(mt2, mt3);
                Link(s36, lk0); Link(lk0, lk1); Link(lk1, lk2); Link(vw1, c20);
                return 0;
            }
            Side(-1, out _, out _, out _, out _, out _, out _, out _, out var wbrn, out _, out _, out var wbrs, out _, out _, out _, out var wmt2, out var wmt3, out _, out _, out var wlk2, out var wvw1);
            Side(1, out _, out _, out _, out _, out _, out _, out _, out var ebrn, out _, out _, out var ebrs, out _, out _, out _, out var emt2, out var emt3, out _, out _, out var elk2, out var evw1);
            int bcn = Node(new Vector3(0, -0.8f, 18)), bcs = Node(new Vector3(0, -0.8f, -18));
            Link(wbrn, bcn); Link(bcn, ebrn); Link(wbrs, bcs); Link(bcs, ebrs);
            int mtop = Node(N0 + new Vector3(0, 0, -2)); Link(wmt3, mtop); Link(emt3, mtop); Link(wmt2, emt2);
            int ltop = Node(S0); Link(wlk2, ltop); Link(elk2, ltop);
            // 谷の底（落ちたときに登る坂）
            int vc = Node(new Vector3(0, -10, 0)), vw0 = Node(new Vector3(-7.4f, -10, -24)), ve0 = Node(new Vector3(7.4f, -10, 24));
            int vwTop = Node(new Vector3(-7.4f, 0, 10)), veTop = Node(new Vector3(7.4f, 0, -10));
            Link(vc, vw0); Link(vc, ve0); Link(vw0, vwTop); Link(ve0, veTop); Link(vwTop, wvw1); Link(veTop, evw1);
        }

        int Nearest(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var d = _nodes[i] - p;
                float dist = new Vector2(d.x, d.z).sqrMagnitude + d.y * d.y * 16f;
                if (dist < bd) { bd = dist; best = i; }
            }
            return best;
        }

        List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            int a = Nearest(from), b = Nearest(to);
            var dist = new float[_nodes.Count]; var prev = new int[_nodes.Count]; var done = new bool[_nodes.Count];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[a] = 0;
            for (int it = 0; it < _nodes.Count; it++)
            {
                int u = -1; float ud = float.MaxValue;
                for (int i = 0; i < dist.Length; i++) if (!done[i] && dist[i] < ud) { ud = dist[i]; u = i; }
                if (u < 0 || u == b) break;
                done[u] = true;
                foreach (var v in _edges[u])
                {
                    float nd = dist[u] + Vector3.Distance(_nodes[u], _nodes[v]);
                    if (nd < dist[v]) { dist[v] = nd; prev[v] = u; }
                }
            }
            var path = new List<Vector3>();
            for (int v = b; v >= 0; v = prev[v]) { path.Insert(0, _nodes[v]); if (v == a) break; }
            // いちばん近い点がうしろにあるときは飛ばす
            if (path.Count > 1 && Vector3.Distance(from, path[1]) < Vector3.Distance(_nodes[a], path[1])) path.RemoveAt(0);
            path.Add(to);
            return path;
        }

        /// <summary>まっすぐ行っても谷に落ちないか（谷をまたがない・高さがほぼ同じ）。</summary>
        static bool DirectOk(Vector3 a, Vector3 b)
        {
            if (Mathf.Abs(a.y - b.y) > 2.5f) return false;
            if (Mathf.Sign(a.x) != Mathf.Sign(b.x) && Mathf.Abs(a.x) > 8 * K && Mathf.Abs(b.x) > 8 * K && a.y > -3 * K && b.y > -3 * K) return false;
            return Vector3.Distance(a, b) < 18f;
        }

        // ───────── 武者をつくる ─────────

        void SpawnUnits()
        {
            var weaponsW = new[] { 0, 3 }; var weaponsE = new[] { 1, 2, 5 };
            for (int i = 0; i < 2; i++) _units.Add(MakeUnit(WestNames[i], 1, weaponsW[i], i));
            for (int i = 0; i < 3; i++) _units.Add(MakeUnit(EastNames[i], 2, weaponsE[i], i));
        }

        Unit MakeUnit(string name, int team, int weapon, int role)
        {
            var u = new Unit { name = name, team = team, weapon = weapon, role = role };
            u.go = new GameObject("武者：" + name);
            u.go.transform.SetParent(transform, false);
            u.cc = u.go.AddComponent<CharacterController>();
            u.cc.height = 1.7f; u.cc.radius = 0.38f; u.cc.center = new Vector3(0, 0.88f, 0); u.cc.stepOffset = 0.45f; u.cc.slopeLimit = 50f;
            var teal = new Color(0.12f, 0.55f, 0.58f); var red = new Color(0.72f, 0.18f, 0.14f);
            var main = team == 1 ? teal : red;
            var look = new DollLook
            {
                skin = new Color(1f, 0.87f, 0.78f), hair = team == 1 ? new Color(0.2f, 0.15f, 0.15f) : new Color(0.15f, 0.12f, 0.12f),
                robe = main, robe2 = new Color(0.15f, 0.14f, 0.16f), sleeve = Color.Lerp(main, Color.white, 0.25f), cuff = new Color(1f, 0.8f, 0.35f),
                sash = new Color(1f, 0.8f, 0.35f), eye = new Color(0.2f, 0.15f, 0.12f), accent = new Color(1f, 0.85f, 0.4f), ponytail = role % 2 == 0, longHair = role == 1,
            };
            var parts = Doll.Build(name, look, c => Mat(c), u.go.transform);
            parts.root.transform.localScale = Vector3.one * 0.9f;
            u.doll = parts.root.GetComponent<DollAnimator>();
            // 兜の角（組のしるし）と、持っている武器
            Block(PrimitiveType.Cube, parts.head, new Vector3(0, 0.24f, 0.12f), new Vector3(0.36f, 0.05f, 0.05f), new Color(1f, 0.8f, 0.35f), false, new Vector3(0, 0, 0), true);
            HandWeapon(parts.armR, weapon);
            u.target = u.go.AddComponent<SengokuTarget>();
            u.target.team = team; u.target.maxHp = u.target.hp = 100; u.target.respawn = 0; u.target.visual = false; u.target.label = name;
            u.target.onDown = t => OnDown(u.name, team);
            foreach (var r in u.go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // 頭の上の名札と体力
            u.tag = Board.Create("名札：" + name, new Vector2(260, 64), 3);
            u.tag.pinned = true; u.tag.pinScale = 0.006f; u.tag.Apply();
            Board.Panel(u.tag.root, new Rect(0, 0, 260, 64), new Color(0.02f, 0.05f, 0.1f, 0.55f));
            Board.Label(u.tag.root, new Rect(0, 2, 260, 34), $"{name}　{Weapons[weapon].name}", 22, team == 1 ? Board.Teal : new Color(1f, 0.55f, 0.45f), TextAnchor.MiddleCenter);
            Board.Panel(u.tag.root, new Rect(20, 42, 220, 12), new Color(0, 0, 0, 0.6f));
            u.hpFill = (RectTransform)Board.Panel(u.tag.root, new Rect(20, 42, 220, 12), team == 1 ? Board.Teal : Board.Vermilion).transform;
            Respawn(u);
            return u;
        }

        void HandWeapon(Transform arm, int w)
        {
            var h = new GameObject("手の武器").transform; h.SetParent(arm, false); h.localPosition = new Vector3(0, -0.5f, 0.05f);
            var white = new Color(0.92f, 0.95f, 1f);
            switch (w)
            {
                case 0: Block(PrimitiveType.Cube, h, new Vector3(0, 0, 0.4f), new Vector3(0.04f, 0.02f, 0.8f), white, false); break;
                case 1: Block(PrimitiveType.Cylinder, h, new Vector3(0, 0, 0.3f), new Vector3(0.05f, 0.35f, 0.05f), new Color(0.45f, 0.3f, 0.2f), false, new Vector3(90, 0, 0));
                        Block(PrimitiveType.Cylinder, h, new Vector3(0, 0, 0.7f), new Vector3(0.3f, 0.22f, 0.3f), new Color(0.85f, 0.2f, 0.25f), false); break;
                case 2: BuildUmbrella(h, false, 0.8f); h.localEulerAngles = new Vector3(90, 0, 0); break;
                case 3: for (int i = -3; i <= 3; i++) Block(PrimitiveType.Cube, h, new Vector3(0, i * 0.12f, 0.15f - Mathf.Abs(i) * 0.03f), new Vector3(0.03f, 0.13f, 0.03f), white, false); break;
                case 4: Block(PrimitiveType.Cylinder, h, new Vector3(0, 0, 0.3f), new Vector3(0.2f, 0.5f, 0.2f), new Color(0.2f, 0.18f, 0.17f), false, new Vector3(90, 0, 0)); break;
                case 5: Block(PrimitiveType.Cube, h, new Vector3(0.08f, 0, 0.2f), new Vector3(0.18f, 0.03f, 0.03f), white, false, new Vector3(0, 30, 0)); break;
            }
        }

        void Respawn(Unit u)
        {
            int idx = u.role + (u.team == 1 ? 1 : 0);
            var p = SpawnOf(u.team, idx);
            u.cc.enabled = false; u.go.transform.position = p; u.cc.enabled = true;
            u.go.transform.rotation = Quaternion.LookRotation(u.team == 1 ? Vector3.right : Vector3.left);
            u.vel = Vector3.zero; u.path.Clear(); u.goal = new Vector3(999, 0, 0); u.enemy = null;
            u.target.Revive();
            u.go.SetActive(true);
            if (u.tag) u.tag.Alpha = 1;
        }

        // ───────── 毎フレーム ─────────

        void UnitsTick(float dt)
        {
            foreach (var u in _units)
            {
                if (u.Dead)
                {
                    if (u.go.activeSelf && u.respawnT <= 0) { u.respawnT = 5f; u.lives--; DownFx(u); }
                    u.respawnT -= dt;
                    if (u.respawnT <= 0 && u.lives > 0 && _phase == MPhase.Fight) Respawn(u);
                    continue;
                }
                Think(u, dt);
                Drive(u, dt);
                u.hpFill.sizeDelta = new Vector2(220 * u.target.hp / u.target.maxHp, 12);
                u.tag.transform.position = u.Pos + Vector3.up * 2.35f;
                u.tag.Alpha = _cam != null && Vector3.Distance(_cam.transform.position, u.Pos) < 45f ? 1 : 0;
                u.target.facing = u.go.transform.forward;
            }
        }

        void DownFx(Unit u)
        {
            Boom(u.Pos + Vector3.up, 0.6f, u.team == 1 ? new Color(0.3f, 1f, 1f) : new Color(1f, 0.45f, 0.3f));
            u.go.SetActive(false);
            if (u.tag) u.tag.Alpha = 0;
        }

        IEnumerable<SengokuTarget> Enemies(int team)
        {
            foreach (var o in _units) if (o.team != team && !o.Dead && o.go.activeSelf) yield return o.target;
            if (team == 2 && _me != null && !_me.Down && !_dead) yield return _me;
        }

        bool Visible(Vector3 from, SengokuTarget t)
        {
            var to = t.transform.position + Vector3.up * 1.2f;
            if (Physics.Raycast(from, (to - from).normalized, out var h, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                return h.collider.GetComponentInParent<SengokuTarget>() == t;
            return true;
        }

        void Think(Unit u, float dt)
        {
            u.thinkT -= dt;
            if (u.thinkT > 0) return;
            u.thinkT = AiThink[_level] * Random.Range(0.8f, 1.2f);
            // 近くの敵
            SengokuTarget best = null; float bd = 26f;
            foreach (var e in Enemies(u.team))
            {
                float d = Vector3.Distance(u.Pos, e.transform.position);
                if (d < bd && Visible(u.Eye, e)) { bd = d; best = e; }
            }
            u.enemy = best;
            // 目的地：櫓、または敵の大将落とし
            var enemyDar = _daruma[u.team == 1 ? 2 : 1];
            Vector3 goal;
            bool attackDar = enemyDar.active && (u.role != 1 || Owned(u.team) == 2);
            // 体力が減ったら本陣へもどって回復する（満タン近くまで）
            if (u.target.hp < 35f) u.healing = true;
            if (u.healing && u.target.hp > 90f) u.healing = false;
            if (u.healing) { u.enemy = u.target.hp < 20f ? null : u.enemy; goal = SpawnOf(u.team, u.role + 1) + new Vector3(u.team == 1 ? 3f : -3f, 0, 0); }
            else if (attackDar) goal = DarumaSpot(u.team == 1 ? 2 : 1) + new Vector3(u.team == 1 ? -3.5f : 3.5f, 0, (u.role - 1) * 1.5f);
            else
            {
                int ti = u.role == 0 ? 0 : u.role == 1 ? 1 : (_towers[0].owner == u.team ? 1 : 0);
                var c = ti == 0 ? NorthCapture : SouthCapture;
                goal = c + new Vector3((u.team == 1 ? -1 : 1) * 2f, 0, (u.role - 1) * 1.5f);
            }
            if (Vector3.Distance(goal, u.goal) > 2f || u.repathT <= 0)
            {
                u.goal = goal; u.path = FindPath(u.Pos, goal); u.pathI = 0; u.repathT = 6f;
            }
        }

        void Drive(Unit u, float dt)
        {
            u.cd -= dt; u.cd2 -= dt; u.repathT -= dt; u.attackPose -= dt;
            var p = u.Pos;
            Vector3 want = Vector3.zero; Vector3 face = u.go.transform.forward;
            bool ranged = u.weapon == 3 || u.weapon == 4;
            if (u.enemy != null && !u.enemy.Down && u.enemy.gameObject.activeInHierarchy)
            {
                var ep = u.enemy.transform.position; var to = ep - p; to.y = 0;
                float d = to.magnitude;
                face = to.normalized;
                float near = ranged ? 9f : 1.9f, far = ranged ? 18f : 2.4f;
                if (d > far)
                {
                    if (DirectOk(p, ep)) want = to.normalized;
                    else want = Follow(u, p, ep);
                }
                else if (d < near && ranged) want = -to.normalized * 0.7f;
                else want = Vector3.Cross(Vector3.up, to.normalized) * Mathf.Sin(Time.time * 0.8f + u.role) * 0.6f; // 横に回りこむ
                Attack(u, u.enemy, d);
            }
            else
            {
                var dar = _daruma[u.team == 1 ? 2 : 1];
                if (dar.active && Vector3.Distance(p, dar.target.transform.position) < 8f)
                {
                    var to = dar.target.transform.position - p; to.y = 0; face = to.normalized;
                    if (to.magnitude > (ranged ? 7f : 2.2f)) want = to.normalized;
                    Attack(u, dar.target, to.magnitude);
                }
                else if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(u.goal.x, 0, u.goal.z)) > 1.2f) want = Follow(u, p, u.goal);
            }
            if (want.sqrMagnitude > 0.01f && u.enemy == null) face = new Vector3(want.x, 0, want.z).normalized;

            float speed = (u.weapon == 4 ? 5.2f : 6.2f) * (InLake(p) ? 0.55f : 1f);
            var flat = new Vector3(u.vel.x, 0, u.vel.z);
            flat = Vector3.Lerp(flat, want * speed, 1 - Mathf.Exp(-10f * dt));
            u.vel.x = flat.x; u.vel.z = flat.z;
            u.vel.y -= Gravity * dt;
            u.cc.Move(u.vel * dt);
            if (u.cc.isGrounded && u.vel.y < 0) u.vel.y = -2f;
            if (face.sqrMagnitude > 0.01f) u.go.transform.rotation = Quaternion.Slerp(u.go.transform.rotation, Quaternion.LookRotation(face), 1 - Mathf.Exp(-10f * dt));
            u.doll.speed = flat.magnitude;
            u.doll.holdR = u.attackPose > 0 ? 1 : 0;
            u.doll.holdREuler = new Vector3(-95, 0, 0);

            // 引っかかったら跳んで、道を引きなおす
            u.stuckT += dt;
            if (u.stuckT > 1.2f)
            {
                if (want.sqrMagnitude > 0.2f && Vector3.Distance(u.lastPos, p) < 0.5f && u.cc.isGrounded) { u.vel.y = 7.5f; u.vel += Random.insideUnitSphere * 3f; u.repathT = 0; u.thinkT = 0; }
                u.lastPos = p; u.stuckT = 0;
            }
            if (p.y < -19f) Respawn(u);
            if (InBase(u.team, p)) u.target.hp = Mathf.Min(u.target.maxHp, u.target.hp + 25f * dt);
        }

        static bool InLake(Vector3 p) => p.z < -34 * K && Mathf.Abs(p.x) < 26 * K && p.y < -0.4f * K && !(Mathf.Abs(p.x) < 6 * K && p.z > -52 * K && p.z < -40 * K);

        Vector3 Follow(Unit u, Vector3 p, Vector3 goal)
        {
            if (u.path == null || u.path.Count == 0 || u.repathT <= 0) { u.path = FindPath(p, goal); u.pathI = 0; u.repathT = 6f; }
            while (u.pathI < u.path.Count - 1)
            {
                var n = u.path[u.pathI]; var d = n - p;
                if (new Vector2(d.x, d.z).magnitude < 1.6f && Mathf.Abs(d.y) < 3f) u.pathI++;
                else break;
            }
            var t = u.path[Mathf.Min(u.pathI, u.path.Count - 1)] - p; t.y = 0;
            return t.sqrMagnitude > 0.04f ? t.normalized : Vector3.zero;
        }

        /// <summary>武者の攻撃（武器ごと）。</summary>
        void Attack(Unit u, SengokuTarget t, float dist)
        {
            if (_phase != MPhase.Fight) return;
            var aimAt = t.transform.position + Vector3.up * (t.structure ? 1.2f : 1.1f);
            var eye = u.Eye;
            Vector3 AimDir(float speed, float grav)
            {
                var d = aimAt - eye; float tt = d.magnitude / Mathf.Max(1, speed);
                var dir = (d + Vector3.up * 0.5f * grav * tt * tt).normalized;
                return Quaternion.Euler(Random.Range(-1f, 1f) * AiErr[_level], Random.Range(-1f, 1f) * AiErr[_level], 0) * dir;
            }
            float k = AiDmg[_level];
            switch (u.weapon)
            {
                case 0:
                    if (dist < 2.6f && u.cd <= 0) { Melee(24 * k, 2.6f, false, 0, eye, (aimAt - eye).normalized, u.team); u.cd = 0.55f; u.attackPose = 0.25f; }
                    else if (dist > 4 && dist < 18 && u.cd2 <= 0) { Fire(eye + u.go.transform.forward * 0.6f, AimDir(55, 0) * 55f, 0, 9 * k, 0, false, ShotKind.Spark, u.team, u.cc); u.cd2 = 0.7f; u.attackPose = 0.2f; }
                    break;
                case 1:
                    if (dist < 3f && u.cd <= 0) { Melee(55 * k, 3f, true, 7f, eye, (aimAt - eye).normalized, u.team); u.cd = 1.4f; u.attackPose = 0.4f; }
                    else if (dist > 6 && dist < 24 && u.cd2 <= 0) { Fire(eye + u.go.transform.forward * 0.8f, AimDir(26, 0) * 26f, 0, 0, 40 * k, true, ShotKind.Missile, u.team, u.cc); u.cd2 = 3f; u.attackPose = 0.3f; }
                    break;
                case 2:
                    if (dist < 2.8f && u.cd <= 0) { Melee(28 * k, 2.8f, false, 0, eye, (aimAt - eye).normalized, u.team); u.cd = 0.7f; u.attackPose = 0.25f; }
                    else if (dist > 5 && dist < 18 && u.cd2 <= 0) { Fire(eye + u.go.transform.forward * 0.8f, AimDir(28, 0) * 28f, 0, 20 * k, 0, false, ShotKind.Spark, u.team, u.cc); u.cd2 = 2f; u.attackPose = 0.3f; }
                    break;
                case 3:
                    if (dist < 42 && u.cd <= 0) { Fire(eye + u.go.transform.forward * 0.6f, AimDir(70, 9) * 70f, 9, 28 * k, 0, false, ShotKind.Arrow, u.team, u.cc); u.cd = 1.5f; u.attackPose = 0.4f; }
                    break;
                case 4:
                    if (dist < 42 && u.cd <= 0) { Fire(eye + u.go.transform.forward * 1f, AimDir(38, 12) * 38f, 12, 0, 70 * k, true, ShotKind.Ball, u.team, u.cc); u.cd = 2.8f; u.attackPose = 0.4f; }
                    break;
                case 5:
                    if (dist < 2.7f && u.cd <= 0) { Melee(26 * k, 2.7f, false, 0, eye, (aimAt - eye).normalized, u.team); u.cd = 0.6f; u.attackPose = 0.25f; }
                    else if (dist > 4 && dist < 13 && u.cd2 <= 0 && t == _me)
                    {
                        // 鎖で自分を引き寄せる
                        _vel += (u.Pos - EyePos).normalized * 12f + Vector3.up * 3f; _me.Hit(10 * k, EyePos, EyePos - u.Pos);
                        u.cd2 = 3f; u.attackPose = 0.3f; Play(_sHit, 0.6f);
                    }
                    break;
            }
        }
    }
}
