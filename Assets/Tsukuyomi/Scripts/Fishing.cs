using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 釣り堀の釣り（自分の人形につける）。
    ///   池のふちで E → 竿を出す
    ///   クリック（か Space）を長押し → はなすと投げる（長く押すほど遠くへ。向きはカメラの向き）
    ///   浮きが「ちょん」と動くのはまだ。ずぶっと沈んだらすぐクリック（合わせ）
    ///   引き合い：押している間たぐり寄せる。魚が暴れると糸が張るので、赤くなったら離す。手もとまで寄せたら釣れる
    /// 釣った魚は図鑑と週の番付に残して、池にかえす（ふじゅ〜は増えない）。
    /// </summary>
    public class Fishing : MonoBehaviour
    {
        public enum St { Off, Ready, Charge, Fly, Wait, Bite, Fight, Caught }

        public static Fishing I { get; private set; }
        public static bool Busy => I != null && I.state != St.Off;

        public St state;
        public float power, tension, progress;
        public bool reeling, burst;
        public FishKind fish;
        public float cm;
        public CatchResult result;
        public float resultT;
        public string msg = "";
        public float msgT;

        PlayerController _pc;
        OrbitCamera _cam;
        DollAnimator _doll;
        Wardrobe _ward;
        Transform _hand, _rod, _bobber;
        readonly Transform[] _seg = new Transform[4];
        LineRenderer _line;
        Material _lineMat, _lineRed;
        GameObject _fishGo;
        Vector3 _spot, _target, _flyFrom, _near;
        float _stateT, _waitT, _nibbleT, _nibbleAnim, _biteT, _noReelT, _burstT, _burstLeft, _stamina = 1f, _L0 = 6f, _tickT, _rodBack;
        float _waterY;
        AudioSource _src;
        AudioClip _plop, _splash, _tick, _snap, _jingle, _jingleBig;
        readonly Dictionary<(Color, bool), Material> _mats = new Dictionary<(Color, bool), Material>();

        static Rect Inner { get { var p = Island.Pond; return new Rect(p.xMin + 0.7f, p.yMin + 0.7f, p.width - 1.4f, p.height - 1.4f); } }

        void Awake()
        {
            I = this;
            _pc = GetComponent<PlayerController>();
            _ward = GetComponent<Wardrobe>();
            _doll = _pc != null ? _pc.doll : GetComponentInChildren<DollAnimator>();
            _cam = _pc != null ? _pc.cam : null;
            if (_doll != null && _doll.armR != null)
                foreach (var t in _doll.armR.GetComponentsInChildren<Transform>(true)) if (t.name == "Hand") _hand = t;
            var c = Island.Pond.center;
            _waterY = Island.WaterSurface(c.x, c.y);
            if (float.IsNaN(_waterY)) _waterY = Island.TerraceHeight(c.x, c.y) - 0.55f;
        }

        void Start()
        {
            BuildGear();
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0f; _src.playOnAwake = false;
            _plop = Synth.Plop(); _splash = Synth.Splash(); _tick = Synth.Tick(); _snap = Synth.Snap();
            _jingle = Synth.Jingle(false); _jingleBig = Synth.Jingle(true);
            Show(false);
        }

        Material Mat(Color c, bool glow = false)
        {
            if (_mats.TryGetValue((c, glow), out var m)) return m;
            var lit = _ward != null ? _ward.litTemplate : null;
            var gl = _ward != null ? _ward.glowTemplate : null;
            if (lit == null) lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m = new Material(glow && gl != null ? gl : lit);
            if (glow && gl != null) m.SetColor("_Color", c);
            else { m.SetColor("_BaseColor", c); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null); }
            _mats[(c, glow)] = m;
            return m;
        }

        GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            if (col != null) Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        void BuildGear()
        {
            _rod = new GameObject("釣り竿").transform;
            var grip = Mat(new Color(0.22f, 0.14f, 0.1f));
            var bamboo = Mat(new Color(0.78f, 0.64f, 0.36f));
            var tip = Mat(new Color(0.85f, 0.18f, 0.2f));
            for (int i = 0; i < _seg.Length; i++)
            {
                float r = Mathf.Lerp(0.022f, 0.009f, i / 3f);
                _seg[i] = Prim(PrimitiveType.Cylinder, _rod, Vector3.zero, new Vector3(r * 2, 0.3f, r * 2), i == 0 ? grip : i == 3 ? tip : bamboo).transform;
            }
            _bobber = new GameObject("浮き").transform;
            Prim(PrimitiveType.Sphere, _bobber, new Vector3(0, 0.035f, 0), Vector3.one * 0.08f, Mat(new Color(0.95f, 0.2f, 0.15f)));
            Prim(PrimitiveType.Sphere, _bobber, new Vector3(0, -0.01f, 0), new Vector3(0.075f, 0.06f, 0.075f), Mat(new Color(0.96f, 0.96f, 0.94f)));
            Prim(PrimitiveType.Cylinder, _bobber, new Vector3(0, 0.1f, 0), new Vector3(0.012f, 0.04f, 0.012f), Mat(new Color(0.95f, 0.2f, 0.15f)));
            var lg = new GameObject("釣り糸");
            _line = lg.AddComponent<LineRenderer>();
            _line.positionCount = 16;
            _line.widthMultiplier = 0.012f;
            _line.numCapVertices = 0;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineMat = Mat(new Color(0.92f, 0.94f, 1f), true);
            _lineRed = Mat(new Color(1f, 0.2f, 0.25f), true);
            _line.sharedMaterial = _lineMat;
        }

        void Show(bool on)
        {
            if (_rod != null) _rod.gameObject.SetActive(on);
            if (_bobber != null) _bobber.gameObject.SetActive(on);
            if (_line != null) _line.gameObject.SetActive(on);
            if (!on && _fishGo != null) { Destroy(_fishGo); _fishGo = null; }
        }

        void Say(string s, float t = 3.5f) { msg = s; msgT = t; }

        // ───────── はじめる・やめる ─────────

        /// <summary>池のふち（か釣り台の上）にいるか。</summary>
        public static bool NearPond(Vector3 p)
        {
            float d = Island.PondDistance(p.x, p.z);
            if (d > 2.6f || d < -5f) return false;
            if (I != null && Mathf.Abs(p.y - (I._waterY + 0.55f)) > 1.3f) return false;
            return !Island.InWater(p);
        }

        public bool CanStart => state == St.Off && !Rest.Sitting && !PlayerController.Fly && (_ward == null || !_ward.Eating) && NearPond(_pc.Position);

        public void Begin()
        {
            if (state != St.Off) return;
            _spot = _pc.Position;
            state = St.Ready;
            Show(true);
            if (_doll != null) { _doll.holdR = 1f; _doll.holdREuler = new Vector3(-62, 0, 4); }
            // 池のほうを向く
            var inner = Inner;
            var q = new Vector2(Mathf.Clamp(_spot.x, inner.xMin, inner.xMax), Mathf.Clamp(_spot.z, inner.yMin, inner.yMax));
            var dir = q - new Vector2(_spot.x, _spot.z);
            if (dir.sqrMagnitude < 1f) dir = Island.Pond.center - new Vector2(_spot.x, _spot.z);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (_cam != null)
            {
                _cam.yaw = yaw;
                if (!_cam.firstPerson) { _cam.pitch = 16f; _cam.distance = Mathf.Max(_cam.distance, 4.5f); }
                _cam.Snap();
            }
            Say("竿を出しました。長押しして、はなすと投げます", 5f);
        }

        public void Stop()
        {
            resultT = 0;
            if (state == St.Off) return;
            state = St.Off;
            Show(false);
            if (_doll != null) { _doll.holdR = 0f; _doll.holdREuler = new Vector3(-120, 0, -25); }
            reeling = false; burst = false;
        }

        void ToReady(string say = null)
        {
            state = St.Ready;
            reeling = false; burst = false; tension = 0; progress = 0;
            if (_fishGo != null) { Destroy(_fishGo); _fishGo = null; }
            if (say != null) Say(say);
        }

        // ───────── 毎フレーム ─────────

        void Update()
        {
            if (state == St.Off) return;
            float dt = Time.deltaTime;
            if (msgT > 0) msgT -= dt;
            if (resultT > 0 && !Ts.ShotMode) resultT -= dt;
            if (!Ts.ShotMode && (_pc.Position - _spot).sqrMagnitude > 2.5f) { Stop(); return; }   // ワープしたら片づける
            if (Hud.Blocked || Ts.ShotMode) return;

            bool down = (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0) || Input.GetKeyDown(KeyCode.Space);
            bool held = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
            _stateT += dt;

            switch (state)
            {
                case St.Ready:
                    if (_cam != null) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, _cam.yaw, 0), 1 - Mathf.Exp(-8f * dt));
                    if (down) { state = St.Charge; power = 0; _stateT = 0; }
                    break;
                case St.Charge:
                    if (_cam != null) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, _cam.yaw, 0), 1 - Mathf.Exp(-8f * dt));
                    power = Mathf.PingPong(_stateT / 1.1f, 1f);
                    if (!held) Cast();
                    break;
                case St.Fly:
                    if (_stateT >= 0.75f)
                    {
                        state = St.Wait; _stateT = 0;
                        _waitT = Random.Range(2.8f, 9f);
                        _nibbleT = Random.Range(0.8f, 2.6f);
                        _src.PlayOneShot(_plop, 0.6f);
                    }
                    break;
                case St.Wait:
                    _waitT -= dt; _nibbleT -= dt;
                    if (_nibbleAnim > 0) _nibbleAnim -= dt;
                    if (_nibbleT <= 0 && _waitT > 1.1f) { _nibbleAnim = 0.32f; _nibbleT = Random.Range(1.1f, 3f); _src.PlayOneShot(_tick, 0.5f); }
                    if (down)
                    {
                        if (_nibbleAnim > 0) ToReady("早すぎた！ ちょんと動くのはまだ。ずぶっと沈むまで待とう");
                        else ToReady("糸を巻きもどしました");
                        break;
                    }
                    if (_waitT <= 0)
                    {
                        fish = FishBook.Pick(Ts.Now);
                        cm = FishBook.Size(fish);
                        state = St.Bite; _stateT = 0;
                        _biteT = fish.bite;
                        _src.PlayOneShot(_plop, 1f);
                    }
                    break;
                case St.Bite:
                    _biteT -= dt;
                    if (down) StartFight();
                    else if (_biteT <= 0) ToReady("にげられた…　浮きが沈んだら、すぐクリック！");
                    break;
                case St.Fight:
                    reeling = held;
                    Fight(dt);
                    break;
                case St.Caught:
                    if (_stateT > 3.6f)
                    {
                        _src.PlayOneShot(_splash, 0.8f);
                        ToReady($"{fish.name}を池にかえしました");
                    }
                    break;
            }
        }

        void Cast()
        {
            var fwd = Quaternion.Euler(0, _cam != null ? _cam.yaw : transform.eulerAngles.y, 0) * Vector3.forward;
            float d = Mathf.Lerp(3f, 12f, power);
            var p = _spot + fwd * d;
            var inner = Inner;
            p.x = Mathf.Clamp(p.x, inner.xMin, inner.xMax);
            p.z = Mathf.Clamp(p.z, inner.yMin, inner.yMax);
            p.y = _waterY;
            _target = p;
            _flyFrom = _bobber.position;
            state = St.Fly; _stateT = 0;
            _rodBack = 0;
            var to = _target - _spot; to.y = 0;
            if (to.sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(to);
        }

        void StartFight()
        {
            state = St.Fight; _stateT = 0;
            var inner = Inner;
            _near = new Vector3(Mathf.Clamp(_spot.x, inner.xMin, inner.xMax), _waterY, Mathf.Clamp(_spot.z, inner.yMin, inner.yMax));
            var flat = _target - _near; flat.y = 0;
            _L0 = Mathf.Max(3f, flat.magnitude);
            progress = 0; tension = 0.3f; _stamina = 1f; _noReelT = 0;
            burst = true; _burstLeft = 0.8f; _burstT = 0;
            _src.PlayOneShot(_splash, 0.7f);
            Say("かかった！ 押してたぐる・糸が赤くなったら離す", 4f);
            if (_fishGo != null) Destroy(_fishGo);
            _fishGo = BuildFish(fish, Mathf.Max(cm, 9f) / 100f);
        }

        void Fight(float dt)
        {
            if (burst)
            {
                _burstLeft -= dt;
                if (_burstLeft <= 0) { burst = false; _burstT = Random.Range(1.0f, 2.6f) * (1.3f - fish.power * 0.5f); }
            }
            else
            {
                _burstT -= dt;
                if (_burstT <= 0) { burst = true; _burstLeft = Random.Range(0.5f, 1.1f) * (0.6f + fish.power * 0.6f); _src.PlayOneShot(_splash, 0.35f); }
            }
            float size = Mathf.InverseLerp(fish.min, fish.max, cm);
            float pull = (0.25f + fish.power * 0.75f) * (0.7f + 0.5f * size) * _stamina;
            float now = burst ? pull : pull * 0.25f;
            if (reeling)
            {
                progress += dt * 1.7f / _L0 * (burst ? 0.35f : 1f);
                tension += dt * (0.08f + now * 1.1f);
                _noReelT = 0;
                _tickT -= dt;
                if (_tickT <= 0) { _tickT = 0.09f; _src.PlayOneShot(_tick, 0.35f); }
            }
            else
            {
                tension -= dt * 0.75f;
                progress -= dt * now * 0.9f / _L0;
                _noReelT += dt;
            }
            tension = Mathf.Clamp(tension, 0f, 1.05f);
            progress = Mathf.Min(progress, 1f);
            _stamina = Mathf.Max(0.35f, _stamina - dt * 0.05f * (burst ? 1.6f : 1f));
            if (tension >= 1f)
            {
                _src.PlayOneShot(_snap, 0.9f);
                ToReady("プツン…　糸が切れちゃった。赤くなったら離そう");
            }
            else if (progress < -0.15f || _noReelT > 7f) ToReady("にげられた…　押してたぐり寄せよう");
            else if (progress >= 1f) Land();
        }

        void Land()
        {
            state = St.Caught; _stateT = 0;
            reeling = false; burst = false;
            result = FishBook.Record(fish, cm);
            resultT = 5.5f;
            _src.PlayOneShot(fish.rank > 0 || result.isNew ? _jingleBig : _jingle, 0.8f);
            msgT = 0;
        }

        // ───────── 見た目 ─────────

        void LateUpdate()
        {
            if (state == St.Off || _rod == null) return;
            float t = Time.time;
            var up = Vector3.up;
            var fwd = transform.forward;
            var hand = _hand != null ? _hand.position : transform.position + up * 1.1f + fwd * 0.4f;

            // 竿のもとの向き（投げる前は後ろへ倒し、投げると前へ）
            float back = state == St.Charge ? power : 0f;
            if (state == St.Fly) _rodBack = Mathf.Min(1f, _rodBack + Time.deltaTime * 6f);
            var d0 = Vector3.Slerp((fwd * 0.75f + up * 0.85f).normalized, (-fwd * 0.35f + up).normalized, back);
            if (state == St.Fly) d0 = Vector3.Slerp((fwd + up * 0.3f).normalized, (fwd * 0.75f + up * 0.85f).normalized, _rodBack);

            // 浮きの場所
            Vector3 bob;
            float bend = 0f;
            var tipGuess = hand + d0 * 2.4f;
            switch (state)
            {
                case St.Fly:
                {
                    float k = Mathf.Clamp01(_stateT / 0.75f);
                    bob = Vector3.Lerp(_flyFrom, _target, k) + up * (Mathf.Sin(k * Mathf.PI) * (1.2f + Vector3.Distance(_flyFrom, _target) * 0.18f));
                    break;
                }
                case St.Wait:
                    bob = _target + up * (0.02f + Mathf.Sin(t * 2.2f) * 0.012f - (_nibbleAnim > 0 ? Mathf.Sin(_nibbleAnim / 0.32f * Mathf.PI) * 0.05f : 0f));
                    bend = 0.12f;
                    break;
                case St.Bite:
                    bob = _target + new Vector3(Mathf.Sin(t * 40f) * 0.03f, -0.12f, Mathf.Cos(t * 37f) * 0.03f);
                    bend = 0.35f;
                    break;
                case St.Fight:
                {
                    var side = Vector3.Cross(up, (_target - _near).normalized);
                    float wob = Mathf.Sin(t * (burst ? 7f : 2.5f)) * (burst ? 0.9f : 0.35f) * (1 - Mathf.Clamp01(progress));
                    bob = Vector3.Lerp(_target, _near, Mathf.Clamp01(progress)) + side * wob + up * -0.04f;
                    bend = Mathf.Clamp01(0.25f + tension * 0.75f);
                    break;
                }
                default:
                    bob = tipGuess + up * -0.7f + fwd * Mathf.Sin(t * 1.7f) * 0.04f;
                    break;
            }

            // 竿（4つの節が浮きのほうへしなる）
            var toBob = (bob - tipGuess).normalized;
            var a = hand - d0 * 0.35f;
            for (int i = 0; i < _seg.Length; i++)
            {
                var dir = Vector3.Slerp(d0, toBob, bend * (i + 1) / _seg.Length * 0.75f);
                float len = i == 0 ? 0.75f : 0.7f;
                var b = a + dir * len;
                _seg[i].position = (a + b) * 0.5f;
                _seg[i].rotation = Quaternion.FromToRotation(Vector3.up, dir);
                var s = _seg[i].localScale; s.y = len * 0.5f; _seg[i].localScale = s;
                a = b;
            }
            var tip = a;
            _bobber.position = bob;
            _bobber.rotation = Quaternion.identity;

            // 糸（張っていないときは少したるむ）
            float sag = state == St.Fight ? (1 - tension) * 0.3f : state == St.Fly ? 0.05f : state <= St.Charge ? 0f : 0.45f;
            for (int i = 0; i < _line.positionCount; i++)
            {
                float k = i / (float)(_line.positionCount - 1);
                _line.SetPosition(i, Vector3.Lerp(tip, bob + up * 0.08f, k) + Vector3.down * sag * 4f * k * (1 - k));
            }
            _line.sharedMaterial = state == St.Fight && tension > 0.78f ? _lineRed : _lineMat;

            // 魚
            if (_fishGo != null)
            {
                if (state == St.Fight)
                {
                    var away = _target - _near; away.y = 0;
                    if (away.sqrMagnitude < 0.01f) away = fwd;
                    _fishGo.transform.position = bob + up * -0.28f;
                    _fishGo.transform.rotation = Quaternion.LookRotation(away.normalized) * Quaternion.Euler(0, Mathf.Sin(t * (burst ? 18f : 7f)) * (burst ? 25f : 10f), 0);
                }
                else if (state == St.Caught)
                {
                    // 手に持ってかかげる（横から見えるように）
                    var camT = Camera.main != null ? Camera.main.transform : transform;
                    var right = camT.right; right.y = 0;
                    if (right.sqrMagnitude < 0.01f) right = transform.right;
                    _fishGo.transform.position = transform.position + up * 1.75f + fwd * 0.5f;
                    _fishGo.transform.rotation = Quaternion.LookRotation(right.normalized) * Quaternion.Euler(Mathf.Sin(t * 9f) * 6f, Mathf.Sin(t * 11f) * 12f, 0);
                }
            }
        }

        /// <summary>魚の形を基本の形で作る（体の向きは +z）。</summary>
        GameObject BuildFish(FishKind k, float len)
        {
            var root = new GameObject("釣れた魚").transform;
            float fat = k.shape == 1 ? k.fat * 1.3f : k.fat;
            float h = len * fat, w = h * 0.72f;
            var body = Mat(k.c1, k.glow);
            var fin = Mat(Color.Lerp(k.c1, k.c2, 0.45f), k.glow);
            var dark = Mat(new Color(0.05f, 0.05f, 0.07f));
            if (k.shape == 3)
            {
                Prim(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(len * 1.1f, len * 0.13f, len * 0.6f), body);
                Prim(PrimitiveType.Sphere, root, new Vector3(0, len * 0.04f, len * 0.05f), new Vector3(len * 0.5f, len * 0.08f, len * 0.35f), Mat(k.c2, true));
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0, -len * 0.6f), new Vector3(0.012f, len * 0.3f, 0.012f), fin, new Vector3(90, 0, 0));
                foreach (float sx in new[] { -1f, 1f }) Prim(PrimitiveType.Sphere, root, new Vector3(sx * len * 0.12f, len * 0.05f, len * 0.27f), Vector3.one * len * 0.05f, dark);
                return root.gameObject;
            }
            Prim(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(w, h, len * 0.8f), body);
            float tl = k.longTail ? 1.6f : 1f;
            Prim(PrimitiveType.Cube, root, new Vector3(0, 0, -len * 0.42f - len * 0.05f * tl), new Vector3(0.008f, len * 0.22f * tl, len * 0.22f * tl), fin, new Vector3(45, 0, 0));
            Prim(PrimitiveType.Cube, root, new Vector3(0, h * 0.45f, -len * 0.06f), new Vector3(0.008f, len * 0.1f, len * 0.26f), fin, new Vector3(-18, 0, 0));
            foreach (float sx in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, root, new Vector3(sx * w * 0.45f, -h * 0.25f, len * 0.12f), new Vector3(0.006f, len * 0.05f, len * 0.12f), fin, new Vector3(30, sx * 25f, 0));
            if (k.pattern == 1)
            {
                var spot = Mat(k.c2, k.glow);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Sphere, root, new Vector3(0, h * 0.3f, len * (0.22f - i * 0.2f)), new Vector3(w * 0.85f, h * 0.5f, len * 0.2f), spot);
            }
            else if (k.pattern == 2) Prim(PrimitiveType.Sphere, root, new Vector3(0, h * 0.38f, len * 0.22f), new Vector3(w * 0.6f, h * 0.35f, len * 0.16f), Mat(k.c2));
            else if (k.pattern == 3) Prim(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(w * 1.04f, h * 0.24f, len * 0.74f), Mat(k.c2, k.glow));
            float er = len * (k.bigEyes ? 0.11f : 0.055f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, root, new Vector3(sx * w * (k.bigEyes ? 0.5f : 0.4f), h * 0.12f, len * 0.29f), Vector3.one * er, k.bigEyes ? Mat(new Color(0.9f, 0.9f, 0.9f)) : dark);
                if (k.bigEyes) Prim(PrimitiveType.Sphere, root, new Vector3(sx * w * 0.56f, h * 0.12f, len * 0.3f), Vector3.one * er * 0.5f, dark);
                if (k.whiskers) Prim(PrimitiveType.Cylinder, root, new Vector3(sx * w * 0.3f, -h * 0.15f, len * 0.42f), new Vector3(0.006f, len * 0.08f, 0.006f), dark, new Vector3(70, sx * 30f, 0));
            }
            return root.gameObject;
        }

        // ───────── 確認用（--tour の fish:…） ─────────

        public void Test(string what)
        {
            Stop();
            var p = Island.Pond;
            float x = p.center.x, z = p.yMin - 1.6f;
            float ground = Island.Height(x, z);
            if (Physics.Raycast(new Vector3(x, 200, z), Vector3.down, out var hit, 400, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
            _pc.Teleport(new Vector3(x, ground + 0.1f, z), 0);
            Begin();
            _target = new Vector3(x + 1.5f, _waterY, p.center.y);
            fish = FishBook.Get("magoi"); cm = 62f;
            var a = what.Split(':');
            switch (a[0])
            {
                case "charge": state = St.Charge; power = 0.7f; break;
                case "wait": state = St.Wait; _waitT = 999; _nibbleT = 999; break;
                case "bite": state = St.Bite; _biteT = 999; break;
                case "fight":
                    StartFight();
                    progress = 0.45f; tension = 0.85f; reeling = true; burst = true;
                    break;
                case "catch":
                    fish = (a.Length > 1 ? FishBook.Get(a[1]) : null) ?? FishBook.Get("kohaku");
                    cm = Mathf.Round(Mathf.Lerp(fish.min, fish.max, 0.8f) * 10f) / 10f;
                    StartFight();
                    Land();
                    break;
            }
            msgT = what == "ready" ? 5f : 0f;
        }
    }
}
