using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 空の遊覧船「月の舟」。ふだんは海の桟橋に浮かんで止まっている。
    ///   桟橋の切符売り場で 500 ふじゅ〜 → すぐ出航して空を3周し、桟橋にもどる
    ///   頂上の月見台の切符売り場で払う → 月見台まで迎えに来て、乗ったら3周して月見台にもどり、そのあと桟橋へ帰る
    ///   お客がいなくても、毎時0分と30分に1周だけ空を回る（甲板に人がいるときは見送る）
    /// 船の向きは local +z、甲板の上が local y = 0、乗り口は左舷（local -x）のまんなか。
    /// 甲板にいる人は、船といっしょに動かす。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SkyShip : MonoBehaviour
    {
        public const int Price = 500, Laps = 3;
        public const float LapSeconds = 80f;

        public Vector3 seaDock; public float seaYaw;
        public Vector3 lookDock; public float lookYaw;
        public GameObject lookGate;   // 月見台の乗り口の見えない柵（船が来ているときだけ消す）
        public GameObject shipGate;   // 船の乗り口の見えない柵（止まっているときだけ消す）
        public ParticleSystem trail;
        public Vector3 cruiseCenter = new Vector3(0, 75f, 140f);
        public float cruiseRadius = 125f;

        public static SkyShip I { get; private set; }

        public enum Where { Sea, Flying, Look }
        public Where where = Where.Sea;
        public bool Busy => _legs.Count > 0 || where != Where.Sea;
        public string Status { get; private set; } = "";

        class Leg
        {
            public float dur, t;
            public Func<float, Vector3> pos;
            public Func<float, float> yaw; // null なら進む向き
            public Action begin, end;
            public bool docked;
            public string say;
        }
        readonly Queue<Leg> _legs = new Queue<Leg>();
        Leg _cur;
        bool _paidRider, _riderOn;
        float _bank; // _riderOn：空を飛んでいる間に甲板にいた
        int _lastAutoSlot = -1;
        PlayerController _pc;
        Matrix4x4 _prev;
        bool _hasPrev;

        void Awake() => I = this;

        void Start()
        {
            transform.SetPositionAndRotation(seaDock, Quaternion.Euler(0, seaYaw, 0));
            SetDocked(true, false);
            _pc = FindFirstObjectByType<PlayerController>();
            var n = Ts.Now; _lastAutoSlot = n.Hour * 2 + n.Minute / 30;
            if (Ts.Arg("--ship") == "fly") { AutoLap(1); }
            if (Ts.Arg("--ship") == "look") { CallToLookout(); }
            if (Ts.Arg("--ship") == "ride" && Ts.ShotMode) { BookAtSea(); } // 確認用：払わずに乗って3周
        }

        void SetDocked(bool docked, bool atLook)
        {
            if (shipGate != null) shipGate.SetActive(!docked);
            if (lookGate != null) lookGate.SetActive(!(docked && atLook));
            if (trail != null) { var em = trail.emission; em.enabled = !docked; }
        }

        // ───────── 予約 ─────────

        /// <summary>甲板の上か（船の中の座標で見る）。</summary>
        public bool OnDeck(Vector3 p)
        {
            var l = transform.InverseTransformPoint(p);
            return Mathf.Abs(l.x) < 3.2f && Mathf.Abs(l.z) < 8.5f && l.y > -0.8f && l.y < 3.5f;
        }

        public bool RiderAboard => _pc != null && OnDeck(_pc.Position);

        /// <summary>払えるか（だめならその理由）。</summary>
        public string CanBook(bool atLook)
        {
            if (Busy) return "遊覧船はいま空を回っています。もどるまで待ってね";
            return null;
        }

        /// <summary>桟橋で払った：乗せてすぐ出航。</summary>
        public void BookAtSea()
        {
            _paidRider = true;
            if (_pc != null && !RiderAboard) _pc.Teleport(transform.TransformPoint(new Vector3(0, 0.15f, 2.5f)), transform.eulerAngles.y);
            Hud.Toast("ご乗船ありがとうございます。まもなく出航します");
            _legs.Enqueue(Wait(4f, false, "まもなく出航"));
            Ride(seaDock, seaYaw, Laps, true);
        }

        /// <summary>月見台で払った：迎えに行き、乗ったら3周して月見台へもどり、そのあと桟橋へ帰る。</summary>
        public void CallToLookout()
        {
            _paidRider = true;
            Hud.Toast("遊覧船がむかえに来ます。月見台の乗り口で待っていてね");
            Fly(seaDock, seaYaw, lookDock, lookYaw, 30f, "月見台へむかっています", () => { where = Where.Flying; SetDocked(false, false); });
            var wait = Wait(40f, true, "月見台で乗るのを待っています");
            wait.begin = () => { where = Where.Look; SetDocked(true, true); Hud.Toast("遊覧船が月見台に着きました。乗り口から乗ってね"); };
            _legs.Enqueue(wait);
            Ride(lookDock, lookYaw, Laps, false);
            var back = Wait(20f, true, "月見台に着きました");
            back.begin = () => { where = Where.Look; SetDocked(true, true); Hud.Toast("おつかれさまでした。月見台に着きました"); };
            _legs.Enqueue(back);
            Fly(lookDock, lookYaw, seaDock, seaYaw, 30f, "桟橋へ帰っています", () => { where = Where.Flying; SetDocked(false, false); });
            _legs.Enqueue(Home());
        }

        /// <summary>だれも払っていないときの1周。</summary>
        void AutoLap(int laps)
        {
            _paidRider = false;
            Ride(seaDock, seaYaw, laps, true);
        }

        // ───────── 道すじ ─────────

        static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }

        static float Ease(float t) => t * t * (3 - 2 * t);

        Vector3 Circle(float ang) => cruiseCenter + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * cruiseRadius;
        float AngleOf(Vector3 p) => Mathf.Atan2(p.z - cruiseCenter.z, p.x - cruiseCenter.x);

        Leg Wait(float sec, bool docked, string say) => new Leg { dur = sec, docked = docked, say = say, pos = null };

        Leg Home()
        {
            var l = Wait(0.1f, true, "");
            l.begin = () => { where = Where.Sea; SetDocked(true, false); };
            return l;
        }

        /// <summary>a から b へ、いったん上がってから降りる飛び方。</summary>
        void Fly(Vector3 a, float ya, Vector3 b, float yb, float sec, string say, Action begin)
        {
            var fa = Quaternion.Euler(0, ya, 0) * Vector3.forward;
            var fb = Quaternion.Euler(0, yb, 0) * Vector3.forward;
            float lift = Mathf.Max(25f, Mathf.Abs(b.y - a.y) + 20f);
            var c1 = a + fa * 25f + Vector3.up * lift;
            var c2 = b - fb * 25f + Vector3.up * lift;
            _legs.Enqueue(new Leg { dur = sec, say = say, begin = begin, pos = t => Bez(a, c1, c2, b, Ease(t)) });
        }

        /// <summary>dock から空へ上がり、laps 周して dock へもどる。</summary>
        void Ride(Vector3 dock, float dockYaw, int laps, bool homeAfter)
        {
            float a0 = AngleOf(dock);
            var entry = Circle(a0);
            var tangent = new Vector3(-Mathf.Sin(a0), 0, Mathf.Cos(a0));
            var fd = Quaternion.Euler(0, dockYaw, 0) * Vector3.forward;
            _legs.Enqueue(new Leg
            {
                dur = 22f, say = "空へのぼっています",
                begin = () =>
                {
                    where = Where.Flying; SetDocked(false, false);
                    if (_pc != null && (_pc.Position - transform.position).sqrMagnitude < 70f * 70f) Sfx.Gong(); // 出航の合図
                },
                pos = t => Bez(dock, dock + fd * 30f + Vector3.up * 20f, entry - tangent * 45f, entry, Ease(t)),
            });
            // 1周目：島の上を高く大きく／2周目：町の名所をめぐる低空／3周目：島のまわりの海の上をすれすれに
            string[] names = { "島の上を高く回っています", "町の名所をめぐっています（低空）", "海の上をすれすれに飛んでいます" };
            for (int k = 0; k < laps; k++)
            {
                var route = Route(k % 3, a0);
                int lap = k;
                _legs.Enqueue(new Leg
                {
                    dur = route.len / CruiseSpeed(k % 3), say = (laps > 1 ? $"{lap + 1}周目：" : "") + names[k % 3],
                    pos = route.At,
                });
            }
            _legs.Enqueue(new Leg
            {
                dur = 22f, say = "おりています",
                pos = t => Bez(entry, entry + tangent * 45f, dock - fd * 30f + Vector3.up * 20f, dock, Ease(t)), // 前向きのまま着く
                end = () => { if (_paidRider && RiderAboard && homeAfter) Hud.Toast("おつかれさまでした。桟橋に着きました"); },
            });
            if (homeAfter) _legs.Enqueue(Home());
        }

        // ───────── 3つの航路 ─────────

        static float CruiseSpeed(int kind) => kind == 0 ? 11f : kind == 1 ? 9f : 12f;

        /// <summary>密に区切った道すじ（長さで引ける）。高さは、下の建物や地形より clearance 以上あける。</summary>
        public class Path
        {
            public Vector3[] p; public float[] s; public float len;
            public Vector3 At(float u)
            {
                float d = Mathf.Clamp01(u) * len;
                int lo = 0, hi = s.Length - 1;
                while (hi - lo > 1) { int m = (lo + hi) / 2; if (s[m] <= d) lo = m; else hi = m; }
                float seg = s[hi] - s[lo];
                return Vector3.Lerp(p[lo], p[hi], seg > 1e-4f ? (d - s[lo]) / seg : 0);
            }
        }

        readonly Dictionary<(int, int), Path> _routes = new Dictionary<(int, int), Path>();
        readonly RaycastHit[] _hits = new RaycastHit[8];

        static readonly Vector2[] Sights =
        {
            new Vector2(0, 22), new Vector2(74, 44), new Vector2(40, 100), new Vector2(0, 75), new Vector2(-40, 112), new Vector2(-74, 127),
            new Vector2(-28, 160), new Vector2(-11, 157), new Vector2(0, 204), new Vector2(36, 198), new Vector2(23, 158), new Vector2(62, 140),
        };

        /// <summary>そこの真下でいちばん高い物の高さ（船の幅ぶん、5か所で見る）。</summary>
        float Highest(Vector3 p)
        {
            float best = Island.Height(p.x, p.z);
            foreach (var o in new[] { Vector3.zero, new Vector3(6, 0, 0), new Vector3(-6, 0, 0), new Vector3(0, 0, 9), new Vector3(0, 0, -9) })
            {
                int n = Physics.RaycastNonAlloc(new Vector3(p.x + o.x, 400f, p.z + o.z), Vector3.down, _hits, 500f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (_hits[i].collider.transform.IsChildOf(transform) || _hits[i].collider is CharacterController) continue;
                    if (_hits[i].point.y > best) best = _hits[i].point.y;
                }
            }
            return Mathf.Max(best, 0f);
        }

        Path Route(int kind, float a0)
        {
            int key = Mathf.RoundToInt(a0 * 100f);
            if (_routes.TryGetValue((kind, key), out var cached)) return cached;
            var c = new Vector2(cruiseCenter.x, cruiseCenter.z);
            var J = Circle(a0);
            var wps = new List<Vector3> { J };
            float clearance = 14f;
            if (kind == 0)
            {
                for (int i = 1; i < 32; i++) wps.Add(Circle(a0 + i * Mathf.PI * 2 / 32));
            }
            else if (kind == 1)
            {
                // 町の上をひとまわり：同じ向きに回りながら、内と外へゆるく波うつ輪（行ったり来たりしない）
                for (int i = 1; i < 48; i++)
                {
                    float a = a0 + i * Mathf.PI * 2 / 48;
                    float rr = 82f + 24f * Mathf.Sin(3f * (a - a0));
                    var q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    wps.Add(new Vector3(q.x, Island.TerraceHeight(q.x, q.y) + 20f, q.y));
                }
                clearance = 12f;
            }
            else
            {
                var ic = Island.Center;
                for (int i = 1; i < 36; i++)
                {
                    float a = a0 + i * Mathf.PI * 2 / 36;
                    wps.Add(new Vector3(ic.x + Mathf.Cos(a) * 178f, 12f, ic.y + Mathf.Sin(a) * 178f));
                }
                clearance = 10f;
            }
            wps.Add(J);
            // カトマル・ロムで3mごとに区切る
            var pts = new List<Vector3>();
            for (int i = 0; i < wps.Count - 1; i++)
            {
                var p0 = wps[Mathf.Max(0, i - 1)]; var p1 = wps[i]; var p2 = wps[i + 1]; var p3 = wps[Mathf.Min(wps.Count - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 3f));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    pts.Add(0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
                }
            }
            pts.Add(J);
            // 角をまるめる（急に曲がらないよう、水平の位置を何回かならす。はしの点はそのまま）
            for (int pass = 0; pass < 40; pass++)
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    var a = pts[i - 1]; var b = pts[i + 1]; var m = pts[i];
                    pts[i] = new Vector3(m.x * 0.5f + (a.x + b.x) * 0.25f, m.y, m.z * 0.5f + (a.z + b.z) * 0.25f);
                }
            // 下の物より clearance 以上あけて、上り下りはなめらかに（前もって上がる）
            var h = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) h[i] = Mathf.Max(pts[i].y, Highest(pts[i]) + clearance);
            h[0] = J.y; h[h.Length - 1] = J.y;
            for (int i = 1; i < h.Length; i++) h[i] = Mathf.Max(h[i], h[i - 1] - 1.2f);
            for (int i = h.Length - 2; i >= 0; i--) h[i] = Mathf.Max(h[i], h[i + 1] - 1.2f);
            for (int pass = 0; pass < 3; pass++)
                for (int i = 1; i < h.Length - 1; i++) h[i] = Mathf.Max(h[i], (h[i - 1] + h[i] + h[i + 1]) / 3f);
            if (kind == 1)
            {
                // 低空の周は、上り下りをへらす：前後 150m でいちばん高い所に合わせてから、ゆっくりならす
                var src = (float[])h.Clone();
                for (int i = 1; i < h.Length - 1; i++)
                {
                    float m = src[i];
                    for (int k = Mathf.Max(0, i - 50); k <= Mathf.Min(h.Length - 1, i + 50); k++) m = Mathf.Max(m, src[k]);
                    h[i] = m;
                }
                for (int pass = 0; pass < 4; pass++)
                {
                    var t2 = (float[])h.Clone();
                    for (int i = 1; i < h.Length - 1; i++)
                    {
                        float sum = 0; int n = 0;
                        for (int k = Mathf.Max(0, i - 25); k <= Mathf.Min(h.Length - 1, i + 25); k++) { sum += t2[k]; n++; }
                        h[i] = Mathf.Max(src[i], sum / n);
                    }
                }
            }
            var path = new Path { p = new Vector3[pts.Count], s = new float[pts.Count] };
            for (int i = 0; i < pts.Count; i++)
            {
                path.p[i] = new Vector3(pts[i].x, h[i], pts[i].z);
                if (i > 0) path.s[i] = path.s[i - 1] + Vector3.Distance(path.p[i - 1], path.p[i]);
            }
            path.len = path.s[path.s.Length - 1];
            _routes[(kind, key)] = path;
            float hmin = float.MaxValue, hmax = 0; foreach (var q in path.p) { hmin = Mathf.Min(hmin, q.y); hmax = Mathf.Max(hmax, q.y); }
            Debug.Log($"[Ship] route {kind}: {path.len:0}m, {path.len / CruiseSpeed(kind):0}s, height {hmin:0}〜{hmax:0}");
            return path;
        }

        // ───────── 毎フレーム ─────────

        void Update()
        {
            float dt = Time.deltaTime;
            // 自動の1周（毎時0分・30分）
            var now = Ts.Now;
            int slot = now.Hour * 2 + now.Minute / 30;
            if (Ts.Shifting) _lastAutoSlot = slot; // 夜にしてもらっている間に自動で飛び立たない
            if (slot != _lastAutoSlot && !Busy)
            {
                if (RiderAboard) { if (!Ts.ShotMode) Hud.Toast("遊覧船に乗るときは、桟橋の切符売り場でどうぞ（500 ふじゅ〜）"); _lastAutoSlot = slot; }
                else { _lastAutoSlot = slot; AutoLap(1); }
            }

            if (_cur == null && _legs.Count > 0) { _cur = _legs.Dequeue(); _cur.t = 0; _cur.begin?.Invoke(); }
            if (_cur != null)
            {
                _cur.t += dt;
                float k = Mathf.Clamp01(_cur.t / _cur.dur);
                Status = _cur.say;
                // 月見台で待つ：お客が乗ったら少しして出発
                if (_cur.docked && where == Where.Look && _paidRider && RiderAboard && _cur.t < _cur.dur - 4f && _cur.say.StartsWith("月見台で乗る")) _cur.t = _cur.dur - 4f;
                if (_cur.pos != null)
                {
                    var p = _cur.pos(k);
                    // 少し先（約3秒先）を見て向きを決め、1秒に 9° までしか曲がらない（急に向きを変えない）
                    float look = Mathf.Min(1f, k + Mathf.Clamp(3f / Mathf.Max(1f, _cur.dur), 0.002f, 0.08f));
                    var ahead = _cur.pos(look) - p;
                    ahead.y = 0;
                    float cur = transform.eulerAngles.y, yaw = cur;
                    if (ahead.sqrMagnitude > 0.04f) yaw = Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg;
                    float ny = Mathf.MoveTowardsAngle(cur, yaw, 9f * dt);
                    // 曲がるときは少しだけ内がわへかたむく
                    _bank = Mathf.Lerp(_bank, Mathf.Clamp(-Mathf.DeltaAngle(cur, ny) / Mathf.Max(dt, 1e-4f) * 0.35f, -4f, 4f), 1 - Mathf.Exp(-1.5f * dt));
                    transform.SetPositionAndRotation(p, Quaternion.Euler(0, ny, _bank));
                }
                if (_cur.t >= _cur.dur)
                {
                    _cur.end?.Invoke();
                    _cur = null;
                    if (_legs.Count == 0) { Status = ""; _paidRider = false; }
                }
            }
            else if (where == Where.Sea)
            {
                // 海の上でゆっくり揺れる
                transform.SetPositionAndRotation(seaDock + Vector3.up * Mathf.Sin(Time.time * 0.6f) * 0.05f, Quaternion.Euler(0, seaYaw, Mathf.Sin(Time.time * 0.45f) * 0.6f));
            }
            // 着いたら止まるときの向きへ
            if (_cur != null && _cur.pos == null)
            {
                float want = where == Where.Look ? lookYaw : seaYaw;
                transform.rotation = Quaternion.Euler(0, Mathf.MoveTowardsAngle(transform.eulerAngles.y, want, 9f * dt), 0);
            }

            // 甲板の人をいっしょに動かす
            if (_pc != null && _hasPrev)
            {
                var local = _prev.inverse.MultiplyPoint3x4(_pc.Position);
                if (Mathf.Abs(local.x) < 3.2f && Mathf.Abs(local.z) < 8.5f && local.y > -0.8f && local.y < 3.5f)
                {
                    var np = transform.localToWorldMatrix.MultiplyPoint3x4(local);
                    float dy = Mathf.DeltaAngle(_prev.rotation.eulerAngles.y, transform.eulerAngles.y);
                    _pc.Carry(np - _pc.Position, dy);
                    _riderOn = where == Where.Flying;
                }
                else if (_riderOn && where == Where.Flying && _pc.Position.y < transform.position.y - 6f && (_pc.Position - transform.position).sqrMagnitude < 60f * 60f)
                {
                    // 万一落ちたら甲板にもどす
                    _pc.Teleport(transform.TransformPoint(new Vector3(0, 0.2f, 2.5f)), transform.eulerAngles.y);
                }
                else if (where != Where.Flying) _riderOn = false;
            }
            _prev = transform.localToWorldMatrix; _hasPrev = true;
        }
    }
}
