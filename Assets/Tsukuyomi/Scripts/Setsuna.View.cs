using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using F = Tsukuyomi.SetsunaFighter;

namespace Tsukuyomi
{
    /// <summary>SETSUNA の見た目：水の上の闘技場・相手の人形の動き・一人称の自分の手・カメラ・火花・音。</summary>
    public partial class Setsuna
    {
        readonly Dictionary<(Color, bool), Material> _mats = new Dictionary<(Color, bool), Material>();

        Material Mat(Color c, bool glow = false)
        {
            if (_mats.TryGetValue((c, glow), out var m)) return m;
            var w = GameSession.Instance != null ? GameSession.Instance.player.GetComponent<Wardrobe>() : null;
            var tmpl = glow && w != null && w.glowTemplate != null ? w.glowTemplate : w != null ? w.litTemplate : null;
            m = tmpl != null ? new Material(tmpl) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (glow && w != null && w.glowTemplate != null) m.SetColor("_Color", c);
            else { m.SetColor("_BaseColor", c); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null); }
            _mats[(c, glow)] = m;
            return m;
        }

        GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, bool glow = false, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Mat(c, glow);
            if (glow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        void Ball(Transform parent, Vector3 pos, float size, Color c) => Prim(PrimitiveType.Sphere, parent, pos, Vector3.one * size, c, true);

        // ───────── 闘技場 ─────────

        Transform _ring;

        void BuildArena()
        {
            _arena = new GameObject("闘技場").transform;
            _arena.SetParent(transform, false);
            var wood = new Color(0.3f, 0.19f, 0.13f); var red = new Color(0.78f, 0.16f, 0.12f); var black = new Color(0.08f, 0.07f, 0.07f);
            var teal = new Color(0.2f, 1f, 1f); var gold = new Color(1f, 0.78f, 0.4f);
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.3f, 0), new Vector3(21f, 0.3f, 21f), wood);
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.29f, 0), new Vector3(17.4f, 0.3f, 17.4f), new Color(0.42f, 0.3f, 0.22f));
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, -0.75f, 0), new Vector3(400f, 0.1f, 400f), new Color(0.03f, 0.07f, 0.13f));
            // 床の輪（ここから外へは出られない）
            for (int i = 0; i < 72; i++)
            {
                float a = i / 72f * Mathf.PI * 2;
                Prim(PrimitiveType.Cube, _arena, new Vector3(Mathf.Cos(a) * (ArenaR + 0.35f), 0.01f, Mathf.Sin(a) * (ArenaR + 0.35f)), new Vector3(0.75f, 0.02f, 0.06f), teal, true, new Vector3(0, -a * Mathf.Rad2Deg + 90, 0));
            }
            Prim(PrimitiveType.Cylinder, _arena, new Vector3(0, 0.005f, 0), new Vector3(2.2f, 0.01f, 2.2f), new Color(0.55f, 0.15f, 0.12f));
            // 四方の鳥居
            for (int k = 0; k < 4; k++)
            {
                var g = new GameObject("鳥居").transform; g.SetParent(_arena, false);
                float a = (45 + k * 90) * Mathf.Deg2Rad;
                g.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 13.5f;
                g.localRotation = Quaternion.LookRotation(-g.localPosition.normalized);
                foreach (float sx in new[] { -1f, 1f }) Prim(PrimitiveType.Cylinder, g, new Vector3(sx * 2.2f, 2.4f, 0), new Vector3(0.38f, 3.1f, 0.38f), red);
                Prim(PrimitiveType.Cube, g, new Vector3(0, 5.5f, 0), new Vector3(6.2f, 0.36f, 0.5f), black);
                Prim(PrimitiveType.Cube, g, new Vector3(0, 5.15f, 0), new Vector3(5.8f, 0.28f, 0.45f), red);
                Prim(PrimitiveType.Cube, g, new Vector3(0, 4.3f, 0), new Vector3(5f, 0.22f, 0.3f), red);
            }
            // 灯籠とのぼり
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45 + 22.5f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 10.4f;
                Prim(PrimitiveType.Cube, _arena, p + Vector3.up * 0.5f, new Vector3(0.5f, 1f, 0.5f), new Color(0.5f, 0.5f, 0.52f));
                Prim(PrimitiveType.Cube, _arena, p + Vector3.up * 1.25f, new Vector3(0.7f, 0.5f, 0.7f), new Color(0.55f, 0.55f, 0.57f));
                Ball(_arena, p + Vector3.up * 1.25f, 0.42f, gold);
                var q = new Vector3(Mathf.Cos(a + 0.2f), 0, Mathf.Sin(a + 0.2f)) * 11.6f;
                Prim(PrimitiveType.Cylinder, _arena, q + Vector3.up * 3f, new Vector3(0.08f, 3f, 0.08f), black);
                Prim(PrimitiveType.Cube, _arena, q + Vector3.up * 3.6f + Vector3.Cross(Vector3.up, q.normalized) * 0.35f, new Vector3(0.62f, 3.6f, 0.03f), k % 2 == 0 ? new Color(0.1f, 0.6f, 0.62f) : red, false, new Vector3(0, Mathf.Atan2(q.x, q.z) * Mathf.Rad2Deg + 90, 0));
            }
            // 空に浮かぶ大きな輪と灯り
            _ring = new GameObject("空の輪").transform; _ring.SetParent(_arena, false); _ring.localPosition = new Vector3(0, 10f, 0);
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2;
                Prim(PrimitiveType.Cube, _ring, new Vector3(Mathf.Cos(a) * 12f, 0, Mathf.Sin(a) * 12f), new Vector3(1.1f, 0.08f, 0.08f), i % 8 == 0 ? gold : teal, true, new Vector3(0, -a * Mathf.Rad2Deg + 90, 0));
            }
            var rng = new System.Random(77);
            for (int i = 0; i < 34; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = 16f + (float)rng.NextDouble() * 22f;
                Ball(_arena, new Vector3(Mathf.Cos(a) * r, 3f + (float)rng.NextDouble() * 12f, Mathf.Sin(a) * r), 0.35f + (float)rng.NextDouble() * 0.3f, i % 3 == 0 ? teal : gold);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                var l = new GameObject("灯り").AddComponent<Light>();
                l.transform.SetParent(_arena, false);
                l.transform.localPosition = new Vector3(sx * 6f, 6.5f, sx * 2f);
                l.type = LightType.Point; l.range = 24f; l.intensity = 2.2f; l.color = sx < 0 ? new Color(1f, 0.8f, 0.6f) : new Color(0.6f, 0.95f, 1f);
            }
        }

        // ───────── 相手の人形 ─────────

        Transform _cpuRoot, _cBody, _cHead, _cArmL, _cArmR;

        void BuildCpuDoll()
        {
            var parts = Doll.Build(C.style.name, C.style.look, c => Mat(c), _arena);
            _cpuRoot = parts.root.transform;
            _cpuRoot.localScale = Vector3.one * 0.95f;
            _cBody = parts.body; _cHead = parts.head; _cArmL = parts.armL; _cArmR = parts.armR;
            var anim = parts.root.GetComponent<DollAnimator>();
            if (anim != null) anim.enabled = false;
            // 鉢巻（相手のしるし）
            Prim(PrimitiveType.Cylinder, _cHead, new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.03f, 0.4f), C.style.look.accent, true);
        }

        static float Ext(F f)
        {
            if (f.move == null) return 0;
            var m = f.move;
            if (f.t < m.startup) return Mathf.SmoothStep(0, 1, f.t / Mathf.Max(0.01f, m.startup));
            if (f.t < m.startup + m.active) return 1;
            return 1 - Mathf.Clamp01((f.t - m.startup - m.active) / Mathf.Max(0.01f, m.recovery + f.stun));
        }

        void PoseCpu(float dt)
        {
            var f = C;
            _cpuRoot.position = f.pos + Vector3.up * f.y;
            _cpuRoot.rotation = Quaternion.LookRotation(f.face);
            float tm = Time.time;
            Vector3 bodyPos = new Vector3(0, Mathf.Sin(tm * 6f) * 0.02f, 0), bodyRot = Vector3.zero;
            Vector3 l = new Vector3(-80, 25, -10), r = new Vector3(-65, -25, 10), head = Vector3.zero;
            float k = Ext(f);
            if (f.crouch && f.st == F.St.Idle || (f.move != null && f.move.h == Height.Low)) bodyPos.y -= 0.42f;
            switch (f.st)
            {
                case F.St.Idle:
                    if (f.guard) { l = new Vector3(-115, 35, -25); r = new Vector3(-115, -35, 25); bodyRot.x = -4; }
                    break;
                case F.St.Blockstun:
                    l = new Vector3(-115, 35, -25); r = new Vector3(-115, -35, 25); bodyRot.x = -10;
                    break;
                case F.St.Hitstun:
                    bodyRot.x = -20 * Mathf.Clamp01(f.stun * 4); head.x = -15; l = new Vector3(-30, 0, -30); r = new Vector3(-30, 0, 30);
                    break;
                case F.St.Juggle:
                    bodyRot.x = -45 - f.juggle * 10; l = new Vector3(-150, 0, -40); r = new Vector3(-150, 0, 40);
                    break;
                case F.St.Down:
                case F.St.KO:
                    bodyRot.x = -88; bodyPos = new Vector3(0, 0.25f, -0.5f); l = new Vector3(-170, 0, -20); r = new Vector3(-170, 0, 20);
                    break;
                case F.St.Air:
                    l = new Vector3(-120, 0, -20); r = new Vector3(-120, 0, 20); bodyRot.x = 10;
                    break;
                case F.St.Dash:
                    bodyRot.x = 18;
                    break;
                case F.St.Backstep:
                    bodyRot.x = -12;
                    break;
                case F.St.Attack:
                    var m = f.move;
                    switch (m.id)
                    {
                        case "jab": l = Vector3.Lerp(l, new Vector3(-92, 0, 0), k); bodyRot.y = -10 * k; break;
                        case "jab2": r = Vector3.Lerp(r, new Vector3(-92, 0, 0), k); bodyRot.y = 10 * k; break;
                        case "heavy": r = Vector3.Lerp(r, new Vector3(-95, 10, 0), k); bodyRot = new Vector3(12 * k, 25 * k, 0); break;
                        case "clight": r = Vector3.Lerp(r, new Vector3(-45, 0, 0), k); bodyRot.x = 20 * k; break;
                        case "cheavy": l = new Vector3(-40, 0, -40); r = Vector3.Lerp(r, new Vector3(-20, 60, 30), k); bodyRot = new Vector3(30 * k, -40 * k, 0); break;
                        case "jlight": case "jheavy": r = Vector3.Lerp(r, new Vector3(-60, 0, 0), k); bodyRot.x = 25 * k; break;
                        case "upper": r = Vector3.Lerp(r, new Vector3(-175, 0, 0), k); bodyPos.y += 0.3f * k; bodyRot.x = -10 * k; break;
                        case "rush": l = Vector3.Lerp(l, new Vector3(-90, 10, 0), k); r = Vector3.Lerp(r, new Vector3(-90, -10, 0), k); bodyRot.x = 28 * k; break;
                        case "fox": l = Vector3.Lerp(l, new Vector3(-90, 15, 0), k); r = Vector3.Lerp(r, new Vector3(-90, -15, 0), k); break;
                        case "throw": l = Vector3.Lerp(l, new Vector3(-85, 25, 0), k); r = Vector3.Lerp(r, new Vector3(-85, -25, 0), k); bodyRot.x = 10 * k; break;
                        case "super":
                            bool alt = Mathf.Repeat(f.t * 12f, 2f) < 1f;
                            l = alt ? new Vector3(-92, 0, 0) : new Vector3(-60, 20, -10); r = alt ? new Vector3(-60, -20, 10) : new Vector3(-92, 0, 0);
                            bodyRot.x = 15; break;
                    }
                    break;
            }
            float s = dt <= 0 ? 1 : 1 - Mathf.Exp(-22f * dt);
            _cBody.localPosition = Vector3.Lerp(_cBody.localPosition, bodyPos, s);
            _cBody.localRotation = Quaternion.Slerp(_cBody.localRotation, Quaternion.Euler(bodyRot), s);
            _cArmL.localRotation = Quaternion.Slerp(_cArmL.localRotation, Quaternion.Euler(l), s * 1.5f);
            _cArmR.localRotation = Quaternion.Slerp(_cArmR.localRotation, Quaternion.Euler(r), s * 1.5f);
            _cHead.localRotation = Quaternion.Slerp(_cHead.localRotation, Quaternion.Euler(head), s);
            if (_ring != null) _ring.localRotation = Quaternion.Euler(0, Time.time * 6f, 0);
        }

        // ───────── 一人称の自分の手 ─────────

        Transform _armsRoot, _fistL, _fistR, _foreL, _foreR, _leg;

        void BuildArms()
        {
            if (_cam == null) return;
            var d = GameSession.Instance != null ? GameSession.Instance.Data : null;
            var it = Catalog.Get(d != null ? d.clothes : Catalog.StartClothes) ?? Catalog.Get(Catalog.StartClothes);
            var skin = it.style == 4 ? it.cuff : DollLook.Player.skin;
            _armsRoot = new GameObject("自分の手").transform;
            _armsRoot.SetParent(_cam.transform, false);
            _foreL = Prim(PrimitiveType.Capsule, _armsRoot, Vector3.zero, Vector3.one * 0.1f, it.sleeve).transform;
            _foreR = Prim(PrimitiveType.Capsule, _armsRoot, Vector3.zero, Vector3.one * 0.1f, it.sleeve).transform;
            _fistL = Prim(PrimitiveType.Sphere, _armsRoot, Vector3.zero, new Vector3(0.09f, 0.08f, 0.1f), skin).transform;
            _fistR = Prim(PrimitiveType.Sphere, _armsRoot, Vector3.zero, new Vector3(0.09f, 0.08f, 0.1f), skin).transform;
            foreach (var t in new[] { _foreL, _foreR }) Prim(PrimitiveType.Cylinder, t, new Vector3(0, -0.85f, 0), new Vector3(1.15f, 0.12f, 1.15f), it.cuff, it.glowCuff);
            _leg = Prim(PrimitiveType.Capsule, _armsRoot, Vector3.zero, new Vector3(0.14f, 0.45f, 0.14f), it.robe).transform;
            foreach (var r in _armsRoot.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void PoseArms(float dt)
        {
            if (_armsRoot == null) return;
            var f = P;
            Vector3 l = new Vector3(-0.22f, -0.24f, 0.56f), r = new Vector3(0.25f, -0.27f, 0.52f);
            float k = Ext(f), tm = Time.time;
            bool leg = false; Vector3 legFrom = Vector3.zero, legTo = Vector3.zero;
            l.y += Mathf.Sin(tm * 5f) * 0.008f; r.y += Mathf.Sin(tm * 5f + 1f) * 0.008f;
            switch (f.st)
            {
                case F.St.Idle:
                    if (f.guard || f.crouch && f.inMove.y < -0.2f) { l = new Vector3(-0.11f, -0.14f, 0.47f); r = new Vector3(0.11f, -0.12f, 0.48f); }
                    break;
                case F.St.Blockstun:
                    l = new Vector3(-0.1f, -0.13f, 0.44f); r = new Vector3(0.1f, -0.11f, 0.45f); break;
                case F.St.Hitstun:
                case F.St.Juggle:
                    l = new Vector3(-0.3f, -0.4f, 0.45f); r = new Vector3(0.3f, -0.42f, 0.45f); break;
                case F.St.Down:
                case F.St.KO:
                    l = new Vector3(-0.35f, -0.6f, 0.2f); r = new Vector3(0.35f, -0.6f, 0.2f); break;
                case F.St.Attack:
                    switch (f.move.id)
                    {
                        case "jab": l = Vector3.Lerp(l, new Vector3(-0.05f, -0.1f, 0.8f), k); break;
                        case "jab2": r = Vector3.Lerp(r, new Vector3(0.05f, -0.1f, 0.8f), k); break;
                        case "heavy": r = Vector3.Lerp(r, new Vector3(0.03f, -0.07f, 0.84f), k); l = Vector3.Lerp(l, new Vector3(-0.25f, -0.32f, 0.3f), k); break;
                        case "clight": r = Vector3.Lerp(r, new Vector3(0.06f, -0.3f, 0.8f), k); break;
                        case "cheavy": leg = true; legFrom = new Vector3(0.25f, -1.05f, 0.1f); legTo = Vector3.Lerp(new Vector3(0.3f, -0.95f, 0.3f), new Vector3(-0.05f, -0.75f, 1.05f), k); break;
                        case "jlight": l = Vector3.Lerp(l, new Vector3(-0.04f, -0.25f, 0.85f), k); break;
                        case "jheavy": r = Vector3.Lerp(r, new Vector3(0.03f, -0.3f, 0.9f), k); l = Vector3.Lerp(l, new Vector3(-0.08f, -0.3f, 0.75f), k); break;
                        case "upper": r = Vector3.Lerp(new Vector3(0.12f, -0.45f, 0.5f), new Vector3(0.04f, 0.32f, 0.72f), k); break;
                        case "rush": l = Vector3.Lerp(l, new Vector3(-0.08f, -0.12f, 0.82f), k); r = Vector3.Lerp(r, new Vector3(0.08f, -0.12f, 0.82f), k); break;
                        case "fox": l = Vector3.Lerp(l, new Vector3(-0.05f, -0.1f, 0.7f), k); r = Vector3.Lerp(r, new Vector3(0.05f, -0.1f, 0.7f), k); break;
                        case "throw": l = Vector3.Lerp(l, new Vector3(-0.15f, -0.1f, 0.78f), k); r = Vector3.Lerp(r, new Vector3(0.15f, -0.1f, 0.78f), k); break;
                        case "super":
                            bool alt = Mathf.Repeat(f.t * 14f, 2f) < 1f;
                            l = alt ? new Vector3(-0.05f, -0.08f, 0.8f) : new Vector3(-0.22f, -0.25f, 0.4f);
                            r = alt ? new Vector3(0.22f, -0.25f, 0.4f) : new Vector3(0.05f, -0.08f, 0.8f);
                            break;
                    }
                    break;
            }
            float s = 1 - Mathf.Exp(-30f * dt);
            if (dt <= 0) s = 1;
            _fistL.localPosition = Vector3.Lerp(_fistL.localPosition, l, s);
            _fistR.localPosition = Vector3.Lerp(_fistR.localPosition, r, s);
            Fore(_foreL, ElbowFor(_fistL.localPosition, -1), _fistL.localPosition, 0.085f);
            Fore(_foreR, ElbowFor(_fistR.localPosition, 1), _fistR.localPosition, 0.085f);
            _leg.gameObject.SetActive(leg);
            if (leg) Fore(_leg, legFrom, legTo, 0.15f);
        }

        /// <summary>ひじの位置：こぶしの少し後ろ・下・外側（腕が画面いっぱいにならないように、短めの前腕だけ見せる）。</summary>
        static Vector3 ElbowFor(Vector3 fist, float side) => fist + new Vector3(side * 0.07f, -0.2f, -0.24f);

        static void Fore(Transform t, Vector3 from, Vector3 to, float w = 0.1f)
        {
            var d = to - from;
            t.localPosition = (from + to) * 0.5f;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            t.localScale = new Vector3(w, d.magnitude * 0.5f, w);
        }

        // ───────── カメラ ─────────

        void PoseCamera(float dt)
        {
            if (_cam == null) return;
            var f = P; var o = C;
            float eye = f.crouch && f.st == F.St.Idle || (f.move != null && f.move.h == Height.Low) ? 0.95f : 1.52f;
            if (f.st == F.St.Down || f.st == F.St.KO) eye = 0.45f;
            var pos = f.pos + Vector3.up * (eye + f.y) - f.face * 0.05f;
            var look = o.pos + Vector3.up * ((o.crouch ? 0.85f : 1.2f) + o.y);
            if (o.st == F.St.Down || o.st == F.St.KO) look = o.pos + Vector3.up * 0.4f;
            var rot = Quaternion.LookRotation(look - pos);
            if (f.st == F.St.Hitstun || f.st == F.St.Juggle) rot *= Quaternion.Euler(-6, 0, Mathf.Sin(Time.time * 30f) * 3f);
            if (f.st == F.St.Down || f.st == F.St.KO) rot *= Quaternion.Euler(-20, 0, 12);
            _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime * 0.4f);
            pos += Random.insideUnitSphere * _shake;
            float s = dt <= 0 ? 1 : 1 - Mathf.Exp(-18f * dt);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, pos, dt <= 0 ? 1 : 1 - Mathf.Exp(-30f * dt));
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, rot, s);
            float fov = f.move == SetsunaMoves.Super && f.st == F.St.Attack ? 62f : 72f;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, 1 - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }

        void Visuals(float dt)
        {
            PoseCpu(dt);
            PoseArms(dt);
            PoseCamera(dt);
            Fx(Time.unscaledDeltaTime);
        }

        // ───────── 火花 ─────────

        class Spark { public Transform tr; public Vector3 v; public float life, max; }
        readonly List<Spark> _sparks = new List<Spark>();

        void Sparks(Vector3 at, Color c, int n, float power)
        {
            for (int i = 0; i < n; i++)
            {
                var g = Prim(PrimitiveType.Cube, _arena, Vector3.zero, Vector3.one * Random.Range(0.04f, 0.09f) * power, i % 3 == 0 ? Color.white : c, true);
                g.transform.position = at;
                var v = Random.onUnitSphere * Random.Range(2f, 6f) * power; v.y = Mathf.Abs(v.y) * 0.7f;
                _sparks.Add(new Spark { tr = g.transform, v = v, life = Random.Range(0.18f, 0.35f), max = 0.35f });
            }
        }

        void SuperFlash(F f)
        {
            Center("刹那・月華乱舞", f.isPlayer ? Board.Teal : Board.Vermilion, 1.0f, f.isPlayer ? "" : $"{f.style.name}の超必殺");
            Sparks(f.pos + Vector3.up * 1.3f + f.face * 0.4f, f.isPlayer ? new Color(0.4f, 1f, 1f) : new Color(1f, 0.5f, 0.3f), 30, 1.6f);
            _hitstop = 0.25f; _shake = 0.06f;
            Play(_sJust, 0.7f);
        }

        void Fx(float udt)
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.life -= udt;
                if (s.life <= 0 || s.tr == null) { if (s.tr) Destroy(s.tr.gameObject); _sparks.RemoveAt(i); continue; }
                s.tr.position += s.v * udt; s.v *= 1 - 4f * udt;
                s.tr.localScale *= 1 - 3f * udt;
            }
        }

        // ───────── 音 ─────────

        static AudioClip _sHit, _sHitHeavy, _sGuard, _sJust, _sWhoosh, _sGong, _sThud;

        static void EnsureSounds()
        {
            if (_sHit != null) return;
            _sHit = Synth.Hit(false); _sHitHeavy = Synth.Hit(true); _sGuard = Synth.Guard(false); _sJust = Synth.Guard(true);
            _sWhoosh = Synth.Whoosh(); _sGong = Synth.Gong(); _sThud = Synth.Boom();
        }

        void Play(AudioClip c, float vol)
        {
            if (c != null && _audio != null) _audio.PlayOneShot(c, vol);
        }

        void Whoosh() => Play(_sWhoosh, 0.35f);
        void Thud() => Play(_sHitHeavy, 0.5f);
    }
}
