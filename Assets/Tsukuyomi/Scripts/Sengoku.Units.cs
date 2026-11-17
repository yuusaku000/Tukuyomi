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
            public SengokuTarget enemy; public float thinkT, strafeT, strafeDir = 1; public bool healing, regen;
            public float stunT; // 地雷を踏んで動けない残り時間
            public int task, stuck, avoidSide = 1; public Vector3 taskGoal; public bool onSlope;
            // 構え・隠れ場所・狙い
            // 移動わざ
            public int skill; public float skillT, skillCd = 2f, skillCheckT; public Vector3 skillTarget, skillDir; public Transform rope, flame, canopy;
            public readonly HashSet<SengokuTarget> skillHits = new HashSet<SengokuTarget>();
            public int stance, flankSide = 1; public float coverT, peekT, aimErr = 5f, reactT, fear; public bool los, hasCover, peeking; public Vector3 coverPt, peekPt;
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
        readonly int[] _pierNode = new int[2], _waterNode = new int[2];
        readonly List<List<int>> _edges = new List<List<int>>();

        int Node(Vector3 p) { _nodes.Add(Sx(p)); _edges.Add(new List<int>()); return _nodes.Count - 1; }
        void Link(int a, int b) { _edges[a].Add(b); _edges[b].Add(a); }

        void BuildGraph()
        {
            if (_theme == 1) { BuildForestGraph(); return; }
            int Side(float s, out int sp, out int dar, out int gate, out int c36, out int c20, out int n36, out int n20, out int brn, out int s36, out int s20, out int brs,
                     out int n36b, out int mt0, out int mt1, out int mt2, out int mt3, out int lk0, out int lk1, out int lk2, out int vw1)
            {
                sp = Node(new Vector3(s * 60, CampH, 8)); dar = Node(new Vector3(s * 53, CampH, 0)); gate = Node(new Vector3(s * 44, CampH * 0.68f, 0));
                c36 = Node(new Vector3(s * 36, 0, 0)); c20 = Node(new Vector3(s * 20, 0, 0));
                n36 = Node(new Vector3(s * 36, 0, 18)); n20 = Node(new Vector3(s * 20, 0, 18)); brn = Node(new Vector3(s * 11.5f, 0, 18));
                s36 = Node(new Vector3(s * 36, 0, -18)); s20 = Node(new Vector3(s * 20, 0, -18)); brs = Node(new Vector3(s * 11.5f, 0, -18));
                n36b = Node(new Vector3(s * 36, 0, 33)); mt0 = Node(new Vector3(s * 37, 0, 44)); mt1 = Node(new Vector3(s * 19, 6, 44));
                mt2 = Node(new Vector3(s * 7, 6, 34.4f)); mt3 = Node(new Vector3(s * 7, 12, 43.5f));
                int mt1b = Node(new Vector3(s * 12.5f, 6, 34.8f)); // 坂の横から入らないよう、坂の手前へ回る
                lk0 = Node(new Vector3(s * 36, 0, -33)); lk1 = Node(new Vector3(s * 29, 0, -42)); lk2 = Node(new Vector3(s * 15, 0.6f, -42));
                vw1 = Node(new Vector3(s * 10.6f, 0, s < 0 ? 11 : -11));
                Link(sp, dar); Link(dar, gate); Link(gate, c36); Link(c36, c20); Link(c36, n36); Link(c36, s36); Link(c20, n20); Link(c20, s20);
                Link(n36, n20); Link(s36, s20); Link(n20, brn); Link(s20, brs); Link(n36, n36b); Link(n36b, mt0); Link(mt0, mt1); Link(mt1, mt1b); Link(mt1b, mt2); Link(mt2, mt3);
                Link(s36, lk0); Link(lk0, lk1); Link(lk1, lk2); Link(vw1, c20);
                // 桟橋の先（ここから浮き島へ）と、湖に落ちたときに上がる道
                int pier = Node(new Vector3(s * 7.2f, 1f, -42)); Link(lk2, pier);
                int wl = Node(new Vector3(s * 12.5f, -1.6f, -48.5f)), lx0 = Node(new Vector3(s * 22.5f, -1.6f, -54)), lx1 = Node(new Vector3(s * 30f, 0, -54));
                Link(wl, lx0); Link(lx0, lx1); Link(lx1, lk1);
                _pierNode[s < 0 ? 0 : 1] = pier; _waterNode[s < 0 ? 0 : 1] = wl;
                return 0;
            }
            Side(-1, out _, out _, out _, out _, out _, out _, out _, out var wbrn, out _, out _, out var wbrs, out _, out _, out _, out var wmt2, out var wmt3, out _, out _, out var wlk2, out var wvw1);
            Side(1, out _, out _, out _, out _, out _, out _, out _, out var ebrn, out _, out _, out var ebrs, out _, out _, out _, out var emt2, out var emt3, out _, out _, out var elk2, out var evw1);
            int bcn = Node(new Vector3(0, -0.8f, 18)), bcs = Node(new Vector3(0, -0.8f, -18));
            Link(wbrn, bcn); Link(bcn, ebrn); Link(wbrs, bcs); Link(bcs, ebrs);
            int mtop = Node(N0 + new Vector3(0, 0, -2)); Link(wmt3, mtop); Link(emt3, mtop); Link(wmt2, emt2);
            int ltop = Node(S0);
            for (int i = 0; i < 2; i++) { Link(_pierNode[i], ltop); Link(_waterNode[i], ltop); }
            // 谷の底（落ちたときに登る坂）
            int vc = Node(new Vector3(0, -10, 0)), vw0 = Node(new Vector3(-7.4f, -10, -24)), ve0 = Node(new Vector3(7.4f, -10, 24));
            int vwTop = Node(new Vector3(-7.4f, 0, 10)), veTop = Node(new Vector3(7.4f, 0, -10));
            Link(vc, vw0); Link(vc, ve0); Link(vw0, vwTop); Link(ve0, veTop); Link(vwTop, wvw1); Link(veTop, evw1);
        }

        /// <summary>いちばん近い道すじの点。見通しのきく点（あいだに壁や崖がない点）を選ぶ。</summary>
        int Nearest(Vector3 p)
        {
            var order = new List<(float d, int i)>();
            for (int i = 0; i < _nodes.Count; i++)
            {
                var d = _nodes[i] - p;
                order.Add((new Vector2(d.x, d.z).sqrMagnitude + d.y * d.y * 16f, i));
            }
            order.Sort((a, b) => a.d.CompareTo(b.d));
            for (int k = 0; k < Mathf.Min(8, order.Count); k++)
            {
                var n = _nodes[order[k].i];
                var a = p + Vector3.up * 1f; var b = n + Vector3.up * 1f;
                if (Mathf.Abs(n.y - p.y) < 3f && !Physics.Raycast(a, (b - a).normalized, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore)) return order[k].i;
            }
            return order[0].i;
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
            if (path.Count > 1 && Vector3.Distance(from, path[1]) < Vector3.Distance(_nodes[a], path[1]) && Mathf.Abs(from.y - path[1].y) < 1.5f && Walkable(from, path[1])) path.RemoveAt(0);
            path.Add(to);
            return path;
        }

        /// <summary>まっすぐ行っても谷に落ちないか（谷をまたがない・高さがほぼ同じ）。</summary>
        static bool DirectOk(Vector3 a, Vector3 b)
        {
            if (Mathf.Abs(a.y - b.y) > 2.5f) return false;
            if (Mathf.Sign(a.x) != Mathf.Sign(b.x) && Mathf.Abs(a.x) > 8 * KX && Mathf.Abs(b.x) > 8 * KX && a.y > -3 * KY && b.y > -3 * KY) return false;
            return Vector3.Distance(a, b) < 28f;
        }

        // ───────── 武者をつくる ─────────

        void SpawnUnits()
        {
            var weaponsW = new[] { 0, 3 }; var weaponsE = new[] { 1, 4, 5 }; // 東軍：ハンマー・金棒・団扇（新しい武器と地雷を相手が使ってくる）
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
            u.target.onHit = (t, d, pt) => { u.aimErr += 2.5f; u.thinkT = Mathf.Min(u.thinkT, 0.1f); }; // 撃たれるとひるんで狙いがぶれ、すぐに考えなおす
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
                case 2: BuildUmbrella(h, false, 1.1f); h.localEulerAngles = new Vector3(90, 0, 0); break; // 傘を大きく
                case 3: for (int i = -3; i <= 3; i++) Block(PrimitiveType.Cube, h, new Vector3(0, i * 0.17f, 0.2f - Mathf.Abs(i) * 0.042f), new Vector3(0.04f, 0.18f, 0.04f), white, false); break; // 弓を大きく
                case 4: // 金棒
                    Block(PrimitiveType.Cylinder, h, new Vector3(0, 0, 0.45f), new Vector3(0.12f, 0.45f, 0.12f), new Color(1f, 0.78f, 0.25f), false, new Vector3(90, 0, 0));
                    for (int i = 0; i < 3; i++) Block(PrimitiveType.Cube, h, new Vector3(0, 0.07f, 0.35f + i * 0.17f), Vector3.one * 0.05f, new Color(1f, 0.92f, 0.55f), false, new Vector3(45, 0, 45), true);
                    break;
                case 5: // 団扇
                    Block(PrimitiveType.Cylinder, h, new Vector3(0, 0, 0.15f), new Vector3(0.04f, 0.15f, 0.04f), new Color(0.3f, 0.2f, 0.15f), false, new Vector3(90, 0, 0));
                    Block(PrimitiveType.Cylinder, h, new Vector3(0, 0, 0.6f), new Vector3(0.7f, 0.01f, 0.55f), new Color(0.32f, 0.22f, 0.55f), false, new Vector3(0, 0, 90));
                    Block(PrimitiveType.Cube, h, new Vector3(0.012f, 0, 0.6f), new Vector3(0.01f, 0.05f, 0.4f), new Color(1f, 0.95f, 0.4f), false, null, true);
                    break;
            }
        }

        void Respawn(Unit u)
        {
            int idx = u.role + (u.team == 1 ? 1 : 0);
            var p = SpawnOf(u.team, idx);
            u.cc.enabled = false; u.go.transform.position = p; u.cc.enabled = true;
            u.go.transform.rotation = Quaternion.LookRotation(u.team == 1 ? Vector3.right : Vector3.left);
            u.vel = Vector3.zero; u.path.Clear(); u.goal = new Vector3(999, 0, 0); u.enemy = null;
            if (u.skill != SkNone) EndSkill(u);
            u.skillCd = 2f;
            u.target.Revive();
            u.go.SetActive(true);
            if (u.tag) u.tag.Alpha = 1;
        }

        // ───────── 毎フレーム ─────────

        void UnitsTick(float dt)
        {
            PlanTeams(dt);
            GuardsTick(dt);
            if (Ts.Flag("--sengoku-log")) { _logT -= dt; if (_logT <= 0) { _logT = 3f; LogUnits(); } }
            foreach (var u in _units)
            {
                if (u.Dead)
                {
                    if (u.go.activeSelf && u.respawnT <= 0)
                    {
                        u.respawnT = 5f; u.lives--; DownFx(u);
                        if (u.lives <= 0 && _phase == MPhase.Fight)
                        {
                            Feed($"{u.name}（{(u.team == 1 ? "西" : "東")}）が退場（残機なし）");
                            if (u.team == 1) Center($"味方の{u.name}が退場", Board.Vermilion, 1.6f, "残機を使いきった。このラウンドはもう出てこない");
                        }
                    }
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

        float _logT;
        /// <summary>確認用（--sengoku-log）：武者の様子を記録に書く。</summary>
        void LogUnits()
        {
            var sb = new System.Text.StringBuilder($"[SengokuLog] t={300 - _clock:0} phase={_phase} me={_rig.transform.position:F0} hp={_me.hp:0} N={_towers[0].owner}/{_towers[0].prog:F2} S={_towers[1].owner}/{_towers[1].prog:F2}");
            foreach (var u in _units)
                sb.Append($"\n  {u.name} t{u.team} act={u.go.activeSelf} dead={u.Dead} lives={u.lives} hp={u.target.hp:0} pos={u.Pos:F1} task={u.task} goal={u.goal:F0} path={u.pathI}/{u.path.Count} next={(u.path.Count > 0 ? u.path[Mathf.Min(u.pathI, u.path.Count - 1)].ToString("F0") : "-")} stuck={u.stuck} heal={u.healing} st={u.stance} cov={u.hasCover} sk={u.skill} err={u.aimErr:F1} enemy={(u.enemy != null ? u.enemy.label : "-")}");
            Debug.Log(sb.ToString());
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
            foreach (var g in GuardTargetsOf(team == 1 ? 2 : 1)) yield return g;
        }

        bool Visible(Vector3 from, SengokuTarget t)
        {
            var to = t.transform.position + Vector3.up * 1.2f;
            if (Physics.Raycast(from, (to - from).normalized, out var h, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                return h.collider.GetComponentInParent<SengokuTarget>() == t;
            return true;
        }

        // ───────── 組ごとの作戦（1秒ごと）─────────
        //  自分の大将落としが出ていたら、いちばん近い1人が守る。
        //  持っていない櫓へ、近い順に1人ずつ。のこりは、相手の大将落としが出ていれば攻め、出ていなければ近い櫓を助ける。

        float _planT;

        void PlanTeams(float dt)
        {
            _planT -= dt;
            if (_planT > 0) return;
            _planT = 1f;
            for (int team = 1; team <= 2; team++) Plan(team);
        }

        void Plan(int team)
        {
            var free = new List<Unit>();
            foreach (var u in _units) if (u.team == team && !u.Dead && u.go.activeSelf && !u.healing) free.Add(u);
            Unit Take(Vector3 at)
            {
                Unit best = null; float bd = float.MaxValue;
                foreach (var u in free) { float d = Vector3.Distance(u.Pos, at); if (d < bd) { bd = d; best = u; } }
                if (best != null) free.Remove(best);
                return best;
            }
            float side = team == 1 ? -1 : 1;
            var ownDar = _daruma[team]; var enemyDar = _daruma[team == 1 ? 2 : 1];
            if (ownDar.active)
            {
                var d = Take(DarumaSpot(team));
                if (d != null) { d.task = 3; d.taskGoal = DarumaSpot(team) + new Vector3(-side * 5f, 0, 2f); }
            }
            for (int i = 0; i < 2; i++)
            {
                var t = _towers[i];
                if (t.owner == team && Mathf.Abs(t.prog) >= 1f) continue;
                var u = Take(t.c);
                if (u != null) { u.task = 1 + i; u.taskGoal = t.c + new Vector3(side * 1.5f, 0, (u.role - 1) * 1.6f); }
            }
            foreach (var u in free)
            {
                if (enemyDar.active) { u.task = 4; u.taskGoal = DarumaSpot(team == 1 ? 2 : 1) + new Vector3(side * 3.5f, 0, (u.role - 1) * 1.8f); }
                else
                {
                    // 相手が持っている櫓、なければ近い櫓へ
                    int ti = _towers[0].owner != team ? 0 : _towers[1].owner != team ? 1 : (Vector3.Distance(u.Pos, _towers[0].c) < Vector3.Distance(u.Pos, _towers[1].c) ? 0 : 1);
                    u.task = 1 + ti; u.taskGoal = _towers[ti].c + new Vector3(side * 2.5f, 0, (u.role - 1) * 2f);
                }
            }
        }

        // ───────── 考える（狙う相手・構え・隠れ場所）─────────
        //  構え（stance）：0 攻める・1 構える（不利なので距離を取り、隠れ場所から顔を出して撃つ）・2 回りこむ（近接で、相手がこちらを向いているとき）・3 下がる（体力が少なく不利）
        static readonly float[] AimStart = { 9f, 5.5f, 3f }, AimMin = { 4f, 1.6f, 0.25f }, AimSettle = { 2.5f, 4.5f, 9f }, React = { 0.65f, 0.38f, 0.16f }, Lead = { 0.4f, 0.75f, 1f };

        void Think(Unit u, float dt)
        {
            u.thinkT -= dt;
            if (u.thinkT > 0) return;
            u.thinkT = AiThink[_level] * Random.Range(0.8f, 1.2f);
            // 体力が減ったら下がって身を隠し、回復（しばらく攻撃を受けなければ回復する）を待つ。80 までもどったら、また前へ
            if (u.target.hp < 35f) u.healing = true;
            if (u.healing && u.target.hp > 80f) u.healing = false;
            // 狙う相手：見えている敵のうち、近い・弱っている・味方も狙っている・目的地の近くにいる相手
            SengokuTarget best = null; float bs = float.MaxValue;
            foreach (var e in Enemies(u.team))
            {
                float d = Vector3.Distance(u.Pos, e.transform.position);
                if (d > 32f || !Visible(u.Eye, e)) continue;
                float score = d - (e.hp < e.maxHp * 0.4f ? 7f : 0f) - (Vector3.Distance(e.transform.position, u.taskGoal) < 10f ? 4f : 0f) + (e.label == "見張り" ? 6f : 0f);
                foreach (var o in _units) if (o != u && o.team == u.team && o.enemy == e) score -= 4f; // 味方と同じ相手を狙う
                if (score < bs) { bs = score; best = e; }
            }
            if (u.healing && u.target.hp < 20f) best = null;
            if (best != u.enemy && best != null) { u.reactT = React[_level] * Random.Range(0.8f, 1.3f); u.aimErr = AimStart[_level]; }
            u.enemy = best;
            u.los = best != null;

            // 構えを決める：自分と近くの味方の体力 と 近くの敵の体力 をくらべる
            if (best != null)
            {
                float mine = u.target.hp / 100f, foe = 0;
                foreach (var o in _units) if (o != u && o.team == u.team && !o.Dead && o.go.activeSelf && Vector3.Distance(o.Pos, u.Pos) < 16f) mine += o.target.hp / 100f * 0.8f;
                if (u.team == 1 && !_dead && Vector3.Distance(_rig.transform.position, u.Pos) < 16f) mine += _me.hp / 100f * 0.8f;
                foreach (var e in Enemies(u.team)) { float d = Vector3.Distance(e.transform.position, u.Pos); if (d < 20f) foe += e.hp / 100f * (d < 10f ? 1.2f : 1f); }
                bool ranged = u.weapon == 3;
                float ed = Vector3.Distance(best.transform.position, u.Pos);
                if (u.target.hp < 45f && foe > 0.6f) u.stance = 3;
                else if (foe > mine + 0.25f - _level * 0.1f) u.stance = 1;
                else if (!ranged && ed > 5f && FacingMe(best, u)) { if (u.stance != 2) u.flankSide = Random.value < 0.5f ? -1 : 1; u.stance = 2; }
                else u.stance = ranged ? 1 : 0;
                // 隠れ場所（構える・下がるとき、2.5秒ごとに選びなおす）
                if ((u.stance == 1 || u.stance == 3) && (u.coverT <= 0 || !u.hasCover)) { u.hasCover = FindCover(u, best, ranged ? 15f : 9f, out u.coverPt, out u.peekPt); u.coverT = 2.5f; }
            }
            else { u.stance = 0; u.hasCover = false; }

            var goal = u.healing ? SpawnOf(u.team, u.role + 1) + new Vector3(u.team == 1 ? 3f : -3f, 0, 0) : u.taskGoal;
            if (Vector3.Distance(goal, u.goal) > 2f || u.repathT <= 0)
            {
                u.goal = goal; u.path = FindPath(u.Pos, goal); u.pathI = 0; u.repathT = 6f;
            }
        }

        /// <summary>相手がこちらを向いているか（自分なら狙いの向き、武者なら体の向き）。</summary>
        bool FacingMe(SengokuTarget e, Unit u)
        {
            var to = (u.Pos - e.transform.position); to.y = 0;
            var f = e == _me ? new Vector3(Aim.x, 0, Aim.z) : e.transform.forward;
            return Vector3.Angle(f, to) < 35f;
        }

        /// <summary>
        /// 隠れ場所をさがす：まわりの点のうち、敵の目から体（1.2m）が見えない・地面があって歩いて行ける・敵との距離がちょうどよい・近い点。
        /// あわせて、そこから横に1.6m出ると敵が見える「顔を出す点」も決める。
        /// </summary>
        bool FindCover(Unit u, SengokuTarget enemy, float prefer, out Vector3 cover, out Vector3 peek)
        {
            cover = peek = u.Pos;
            var ep = enemy.transform.position + Vector3.up * 1.5f;
            float best = float.MinValue; bool found = false;
            for (int ring = 0; ring < 2; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float r = ring == 0 ? 5f : 10f, a = k / 12f * Mathf.PI * 2;
                    var c = u.Pos + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    if (!Physics.Raycast(c + Vector3.up * 3f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    c = g.point;
                    if (g.normal.y < 0.8f || Mathf.Abs(c.y - u.Pos.y) > 1.2f || !Walkable(u.Pos, c) || InLake(c)) continue;
                    bool hidden = Physics.Linecast(ep, c + Vector3.up * 1.2f, out var h, ~0, QueryTriggerInteraction.Ignore) && h.collider.GetComponentInParent<SengokuTarget>() == null;
                    if (!hidden) continue;
                    float de = Vector3.Distance(c, enemy.transform.position);
                    float score = -Mathf.Abs(de - prefer) * 0.5f - Vector3.Distance(u.Pos, c) * 0.6f;
                    if (de < 4f) score -= 20f;
                    // 顔を出す点：左右どちらかに1.6m出て、敵が見える
                    var side = Vector3.Cross(Vector3.up, (enemy.transform.position - c).normalized);
                    Vector3 pk = c; bool canPeek = false;
                    foreach (float sgn in new[] { 1f, -1f })
                    {
                        var q = c + side * sgn * 1.6f;
                        if (!Physics.Linecast(ep, q + Vector3.up * 1.2f, out var h2, ~0, QueryTriggerInteraction.Ignore) || h2.collider.GetComponentInParent<SengokuTarget>() != null) { pk = q; canPeek = true; break; }
                    }
                    if (canPeek) score += 3f;
                    if (score > best) { best = score; cover = c; peek = canPeek ? pk : c; found = true; }
                }
            return found;
        }

        Vector3 VelOf(SengokuTarget t)
        {
            if (t == _me) return _vel;
            foreach (var o in _units) if (o.target == t) return new Vector3(o.vel.x, 0, o.vel.z);
            return Vector3.zero;
        }

        void Drive(Unit u, float dt)
        {
            if (u.stunT > 0)
            {
                // 地雷を踏んで打ち上げられ、動けない（落ちてくるだけ）
                u.stunT -= dt;
                u.vel.x = Mathf.MoveTowards(u.vel.x, 0, 4f * dt); u.vel.z = Mathf.MoveTowards(u.vel.z, 0, 4f * dt);
                u.vel.y -= Gravity * dt;
                u.cc.Move(u.vel * dt);
                if (u.cc.isGrounded && u.vel.y < 0) u.vel.y = -2f;
                u.doll.speed = 0; u.doll.holdR = 0;
                if (u.Pos.y < -19f) Respawn(u);
                return;
            }
            u.cd -= dt; u.cd2 -= dt; u.repathT -= dt; u.attackPose -= dt; u.strafeT -= dt; u.reactT -= dt; u.coverT -= dt; u.peekT -= dt;
            if (u.strafeT <= 0) { u.strafeDir = Random.value < 0.5f ? -1 : 1; u.strafeT = Random.Range(0.8f, 2f); }
            u.aimErr = Mathf.MoveTowards(u.aimErr, AimMin[_level], AimSettle[_level] * dt);
            if (_phase == MPhase.Fight)
            {
                if (UnitSkill(u, dt)) return;
                var se = u.enemy != null && !u.enemy.Down && u.enemy.gameObject.activeInHierarchy ? u.enemy : null;
                TrySkill(u, se, se != null ? Vector3.Distance(u.Pos, se.transform.position) : 999f, dt);
                if (u.skill != SkNone) return;
            }
            var p = u.Pos;
            Vector3 want = Vector3.zero; Vector3 face = u.go.transform.forward;
            bool ranged = u.weapon == 3, hybrid = u.weapon == 0 || u.weapon == 2 || u.weapon == 4 || u.weapon == 5;
            float run = 1f;
            bool fighting = _phase == MPhase.Fight;
            var dar = _daruma[u.team == 1 ? 2 : 1];
            bool atDar = u.task == 4 && dar.active && Vector3.Distance(p, dar.target.transform.position) < 14f;
            var enemy = u.enemy != null && !u.enemy.Down && u.enemy.gameObject.activeInHierarchy ? u.enemy : null;
            float ed = enemy != null ? Vector3.Distance(p, enemy.transform.position) : 999f;
            bool engage = enemy != null && (ed < 16f || u.task == 3 || u.stance == 3 || Vector3.Distance(enemy.transform.position, u.taskGoal) < 12f) && !atDar;

            if (!fighting) { want = Vector3.zero; }
            else if (engage)
            {
                var to = enemy.transform.position - p; to.y = 0;
                var dir = to.sqrMagnitude > 0.01f ? to.normalized : u.go.transform.forward;
                face = dir;
                var strafe = Vector3.Cross(Vector3.up, dir) * u.strafeDir;
                switch (u.stance)
                {
                    case 3: // 下がる：隠れ場所か、味方・本陣の方へ。撃てる武器は撃ちながら
                        if (u.hasCover && Vector3.Distance(p, u.coverPt) > 1f) want = (u.coverPt - p).normalized;
                        else if (!u.hasCover) want = Follow(u, p, SpawnOf(u.team, u.role + 1));
                        run = 1.15f;
                        break;
                    case 1: // 構える：隠れ場所に入り、ときどき横に顔を出して撃つ。隠れ場所がなければ距離を取って左右に動く
                        if (u.hasCover)
                        {
                            bool atCover = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(u.coverPt.x, 0, u.coverPt.z)) < 1.2f;
                            if (!atCover && !u.peeking) want = (u.coverPt - p).normalized;
                            else
                            {
                                if (u.peekT <= 0) { u.peeking = !u.peeking; u.peekT = u.peeking ? Random.Range(1f, 1.8f) : Random.Range(0.6f, 1.2f) / (1 + _level * 0.4f); }
                                var target = u.peeking ? u.peekPt : u.coverPt;
                                var off = target - p; off.y = 0;
                                if (off.magnitude > 0.3f) want = off.normalized * 0.8f;
                            }
                        }
                        else
                        {
                            float pref = ranged ? 16f : 9f;
                            if (ed < pref - 3f) want = (-dir + strafe * 0.5f).normalized;
                            else if (ed > pref + 6f) want = Walkable(p, enemy.transform.position) ? dir : Follow(u, p, enemy.transform.position);
                            else want = strafe * 0.8f;
                        }
                        break;
                    case 2: // 回りこむ：相手の横へ回ってから詰める
                        {
                            var side = Vector3.Cross(Vector3.up, -dir) * u.flankSide;
                            var fp = enemy.transform.position + side * 7f;
                            bool flanked = !FacingMe(enemy, u) || ed < 3.5f;
                            if (flanked) { u.stance = 0; want = dir; }
                            else want = Walkable(p, fp) ? (fp - p).normalized : Follow(u, p, fp);
                            run = 1.2f;
                        }
                        break;
                    default: // 攻める
                        if (ranged)
                        {
                            if (ed > 24f) want = DirectOk(p, enemy.transform.position) && Walkable(p, enemy.transform.position) ? dir : Follow(u, p, enemy.transform.position);
                            else if (ed < 10f) want = (-dir + strafe * 0.6f).normalized;
                            else want = strafe * 0.8f;
                        }
                        else
                        {
                            float near = hybrid ? 2.2f : 2.0f;
                            if (ed > near + 0.4f)
                            {
                                want = DirectOk(p, enemy.transform.position) && Walkable(p, enemy.transform.position) ? (dir + strafe * (ed > 6 ? 0.35f : 0f)).normalized : Follow(u, p, enemy.transform.position);
                                if (ed < 7f) run = 1.35f; // 間合いに飛びこむ
                            }
                            else want = strafe * 0.7f;
                        }
                        break;
                }
                // 自分（プレイヤー）に狙われていたら、横にかわす（強いほどよく動く）
                if (enemy == _me && _level > 0 && ed < 35f && Vector3.Angle(Aim, (u.Eye - EyePos).normalized) < 7f)
                {
                    want = (want + strafe * (0.8f + _level * 0.4f)).normalized;
                    if (_level == 2 && u.cc.isGrounded && Random.value < dt * 0.8f) u.vel.y = 7f;
                }
                Attack(u, enemy, ed);
            }
            else
            {
                if (atDar)
                {
                    var to = dar.target.transform.position - p; to.y = 0; face = to.normalized;
                    if (to.magnitude > 2.4f) want = face;
                    else want = Vector3.Cross(Vector3.up, face) * u.strafeDir * 0.4f;
                    Attack(u, dar.target, to.magnitude, true);
                }
                else if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(u.goal.x, 0, u.goal.z)) > 1.5f)
                {
                    want = Follow(u, p, u.goal);
                    run = Vector3.Distance(p, u.goal) > 20f ? 1.2f : 1f;
                }
                if (enemy != null && (ranged || hybrid)) { face = (enemy.transform.position - p).normalized; Attack(u, enemy, ed); }
            }
            // 団扇：敵がいないあいだ、守っている櫓（持っている櫓）のまわりに地雷をしかける
            if (fighting && !engage && u.weapon == 5 && u.cd2 <= 0 && (u.task == 1 || u.task == 2))
            {
                var tw = _towers[u.task - 1];
                if (tw.owner == u.team && Vector3.Distance(p, tw.c) < 7f && MinesOf(u.team, u) < MaxMines)
                {
                    var ring = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(3f, 6f);
                    var at = tw.c + ring + Vector3.up * 2f;
                    if (Physics.Raycast(at, Vector3.down, out var gh, 6f, ~0, QueryTriggerInteraction.Ignore)) { PlaceMine(gh.point, u.team, u); u.attackPose = 0.2f; }
                    u.cd2 = Random.Range(4f, 7f);
                }
            }
            if (fighting && !(u.onSlope && !engage)) want = Avoid(u, p, want);
            if (want.sqrMagnitude > 0.01f && !engage && !atDar && enemy == null) face = new Vector3(want.x, 0, want.z).normalized;
            face.y = 0;

            float speed = (u.weapon == 4 ? 5.8f : 6.2f) * run * (InLake(p) ? 0.55f : 1f);
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

            // 引っかかったら、まず跳ぶ。それでもだめなら、よける向きを変えて道を引きなおす
            u.stuckT += dt;
            if (u.stuckT > 1f)
            {
                if (want.sqrMagnitude > 0.2f && Vector3.Distance(u.lastPos, p) < 0.6f)
                {
                    u.stuck++;
                    if (u.cc.isGrounded) u.vel.y = 7.5f;
                    if (u.stuck >= 2) { u.avoidSide = -u.avoidSide; u.repathT = 0; u.thinkT = 0; u.hasCover = false; u.vel += Random.insideUnitSphere * 3f; u.path.Clear(); }
                    if (u.stuck >= 4) u.skillCd = 0; // 移動わざで抜け出せるなら使う
                    // 最後の手段：10秒以上動けず、自分から見えていなければ、近くの道すじの点へもどす
                    if (u.stuck >= 10 && (_cam == null || Vector3.Dot(_cam.transform.forward, (p - _cam.transform.position).normalized) < 0.3f || Vector3.Distance(p, _cam.transform.position) > 40f))
                    {
                        var n = _nodes[Nearest(p)];
                        u.cc.enabled = false; u.go.transform.position = n + Vector3.up * 0.3f; u.cc.enabled = true;
                        u.vel = Vector3.zero; u.stuck = 0; u.path.Clear(); u.repathT = 0;
                    }
                }
                else u.stuck = 0;
                u.lastPos = p; u.stuckT = 0;
            }
            if (p.y < -19f) Respawn(u);
            // 回復：しばらくダメージを受けなければ（自分と同じ）
            u.regen = Time.time - u.target.lastHit > RegenDelay && u.target.hp < u.target.maxHp;
            if (u.regen) u.target.hp = Mathf.Min(u.target.maxHp, u.target.hp + RegenRate * dt);
        }

        /// <summary>前に壁や建物があれば横へよける。味方とは間をあける。</summary>
        Vector3 Avoid(Unit u, Vector3 p, Vector3 want)
        {
            if (want.sqrMagnitude < 0.01f) return want;
            var dir = new Vector3(want.x, 0, want.z).normalized;
            if (Physics.SphereCast(p + Vector3.up * 0.9f, 0.35f, dir, out var h, 1.6f, ~0, QueryTriggerInteraction.Ignore)
                && h.collider != u.cc && h.normal.y < 0.6f && h.collider.GetComponentInParent<SengokuTarget>() == null)
            {
                var side = Quaternion.Euler(0, 60f * u.avoidSide, 0) * dir;
                if (Physics.Raycast(p + Vector3.up * 0.9f, side, 1.6f, ~0, QueryTriggerInteraction.Ignore)) { u.avoidSide = -u.avoidSide; side = Quaternion.Euler(0, 60f * u.avoidSide, 0) * dir; }
                dir = side;
            }
            foreach (var o in _units)
            {
                if (o == u || o.team != u.team || o.Dead) continue;
                var off = p - o.Pos; off.y = 0;
                if (off.sqrMagnitude < 2f && off.sqrMagnitude > 0.001f) dir += off.normalized * 0.5f;
            }
            // 見えている（2m 以内の）相手の地雷は、よけて歩く
            foreach (var m in _mines)
            {
                if (m.team == u.team || m.tr == null) continue;
                var off = p - m.tr.position; off.y = 0;
                float d = off.magnitude;
                if (d < MineSee && d > 0.01f) dir += off.normalized * (MineSee - d) * 1.6f;
            }
            return dir.normalized * Mathf.Min(1f, want.magnitude);
        }

        /// <summary>a から b へまっすぐ歩いて、途中で落ちないか（足もとに、ほぼ同じ高さの地面があるか）。</summary>
        static bool Walkable(Vector3 a, Vector3 b)
        {
            for (int i = 1; i <= 4; i++)
            {
                var q = Vector3.Lerp(a, b, i / 5f);
                if (!Physics.Raycast(q + Vector3.up * 1.5f, Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(h.point.y - q.y) > 1.3f) return false;
            }
            return true;
        }

        static bool InLake(Vector3 p) => ThemeNow == 0 && p.z < -34 * KX && Mathf.Abs(p.x) < 26 * KX && p.y < -0.4f * KY && !(Mathf.Abs(p.x) < 6 * KX && p.z > -52 * KX && p.z < -40 * KX);

        Vector3 Follow(Unit u, Vector3 p, Vector3 goal)
        {
            if (u.path == null || u.path.Count == 0 || u.repathT <= 0) { u.path = FindPath(p, goal); u.pathI = 0; u.repathT = 6f; }
            while (u.pathI < u.path.Count - 1)
            {
                var n = u.path[u.pathI]; var d = n - p;
                if (new Vector2(d.x, d.z).magnitude < 1.8f && Mathf.Abs(d.y) < 3f) u.pathI++;
                else break;
            }
            // 次の次の点まで、まっすぐ行けるなら近道する
            if (u.pathI + 1 < u.path.Count)
            {
                var nn = u.path[u.pathI + 1];
                var a = p + Vector3.up * 0.9f; var b = nn + Vector3.up * 0.9f;
                if (DirectOk(p, nn) && Walkable(p, nn) && !Physics.Raycast(a, (b - a).normalized, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore)) u.pathI++;
            }
            // 道の中心線に沿って歩く（前の点と次の点を結ぶ線の、少し先をめざす）。坂・橋・桟橋から落ちないように
            if (u.pathI > 0 && u.pathI < u.path.Count)
            {
                var a0 = u.path[u.pathI - 1]; var b0 = u.path[u.pathI];
                var seg = b0 - a0; seg.y = 0;
                float len = seg.magnitude;
                if (len > 0.5f)
                {
                    var dir = seg / len;
                    var rel = p - a0; rel.y = 0;
                    float along = Mathf.Clamp(Vector3.Dot(rel, dir) + 2.2f, 0, len);
                    var aim = a0 + dir * along; aim.y = p.y;
                    var tt = aim - p; tt.y = 0;
                    u.onSlope = Mathf.Abs(b0.y - a0.y) > 0.8f;
                    if (tt.sqrMagnitude > 0.04f) return tt.normalized;
                }
            }
            u.onSlope = false;
            var t = u.path[Mathf.Min(u.pathI, u.path.Count - 1)] - p; t.y = 0;
            return t.sqrMagnitude > 0.04f ? t.normalized : Vector3.zero;
        }

        /// <summary>武者の攻撃（武器ごと）。</summary>
        void Attack(Unit u, SengokuTarget t, float dist, bool structureTarget = false)
        {
            if (_phase != MPhase.Fight) return;
            if (!structureTarget && u.reactT > 0) return; // 見つけてすぐは撃たない（反応の時間）
            if (structureTarget)
            {
                // 大将落としは近接攻撃でしか落ちないので、どの武器も近づいて殴る
                if (dist < 3.2f && u.cd <= 0)
                {
                    var c = t.transform.position + Vector3.up * 1.5f;
                    Melee((u.weapon == 1 ? 50 : 28) * AiDmg[_level], 3.2f, u.weapon == 1, 0, u.Eye, (c - u.Eye).normalized, u.team);
                    u.cd = u.weapon == 1 ? 1.2f : 0.7f; u.attackPose = 0.3f;
                }
                return;
            }
            var aimAt = t.transform.position + Vector3.up * (t.structure ? 1.2f : 1.1f);
            var eye = u.Eye;
            Vector3 AimDir(float speed, float grav)
            {
                // 相手の動きを読み（強いほど正確）、弓は落ちる分だけ上を狙う。狙いのぶれは、同じ相手を狙い続けるほど小さくなる
                float tt = Vector3.Distance(aimAt, eye) / Mathf.Max(1, speed);
                var lead = aimAt + VelOf(t) * tt * Lead[_level];
                tt = Vector3.Distance(lead, eye) / Mathf.Max(1, speed);
                var dir = (lead - eye + Vector3.up * 0.5f * grav * tt * tt).normalized;
                var e = Random.insideUnitCircle * u.aimErr;
                return Quaternion.AngleAxis(e.x, Vector3.up) * Quaternion.AngleAxis(e.y, Vector3.Cross(dir, Vector3.up)) * dir;
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
                case 4: // 金棒：近くは重い一撃、離れていれば金の弾
                    if (dist < 3f && u.cd <= 0) { Melee(50 * k, 3f, true, 6f, eye, (aimAt - eye).normalized, u.team); u.cd = 1.3f; u.attackPose = 0.4f; }
                    else if (dist > 4 && dist < 26 && u.cd2 <= 0) { Fire(eye + u.go.transform.forward * 0.8f, AimDir(70, 0) * 70f, 0, 8 * k, 0, false, ShotKind.Bullet, u.team, u.cc); u.cd2 = 0.75f; u.attackPose = 0.2f; }
                    break;
                case 5: // 団扇：少し遠くまで届くあおぎ打ち。近くに敵がいれば、ときどき足もとに地雷をしかける
                    if (dist < 4f && u.cd <= 0) { Melee(24 * k, 4.2f, false, 5f, eye, (aimAt - eye).normalized, u.team); u.cd = 0.65f; u.attackPose = 0.25f; }
                    else if (dist < 14f && u.cd2 <= 0) { PlaceMine(u.Pos + u.go.transform.forward * 1.2f, u.team, u); u.cd2 = Random.Range(5f, 8f); u.attackPose = 0.2f; }
                    break;
            }
        }
    }
}
