using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tsukuyomi
{
    /// <summary>
    /// 現実ではできない演出（ARライブならでは）。曲の区切りごとにランダムに選ぶ。
    ///   登場：gate（空に浮かぶ大鳥居から降りてくる）・stairs（空から光の階段を歩いて降りる）・geyser（海の柱といっしょに飛び出す）
    ///   サビ：float（重力が消えて客席ごと浮く）・tunnel（巨大な鳥居が次々と頭の上を飛んでくる）・dragon（光の竜が会場を巻く）
    ///         chibi（ちびヤチヨの群れが飛び回る）・flyby（ヤチヨが光の円盤で客席の上を飛ぶ）・island（島じゅうの明かりが拍で明滅し、光の柱が立つ）
    ///         giant（海から巨大なヤチヨが立ち上がる）・rain（星の雨と空の色）
    ///   バラード：sea（会場が海の中に沈む）・moon（月が近づき、空へ昇っていく）・giant
    ///   おわり：客席みんなのペンライトの光が空へ昇って、ハートの星座になる
    ///   いつも：歌詞は空に光る文字で浮かび、コメントは舞台のうしろの空を右から左へ流れる
    ///   投げ銭をすると、ステージに招待される（ヤチヨのとなりから客席を見られる）
    /// </summary>
    public partial class LiveShow
    {
        public static readonly string[] HypeFx = { "float", "tunnel", "dragon", "chibi", "flyby", "island", "giant", "rain", "flood", "rise", "shatter", "clones", "sakura", "ribbons", "bloom" };
        static readonly string[] AllFx = { "float", "tunnel", "dragon", "chibi", "flyby", "island", "giant", "rain", "flood", "rise", "shatter", "clones", "sakura", "ribbons", "bloom", "sea", "moon", "alone", "spiral", "ascend", "morph", "split", "mirror" };
        public static readonly string[] Openings = { "gate", "stairs", "geyser" };
        /// <summary>確認用：演出・登場を決める（tour の fx= / open=）。</summary>
        public static string ForceFx, ForceOpen;

        /// <summary>重力が消えて浮いている高さ（m）。PlayerController と客席が使う。</summary>
        public static float FloatHeight { get; private set; }
        public Vector3 PerformerOffset { get; private set; }
        public float PerformerYaw { get; private set; }

        readonly Dictionary<LiveSec, string> _plan = new Dictionary<LiveSec, string>();
        readonly Dictionary<string, float> _k = new Dictionary<string, float>();
        readonly List<string> _fxBag = new List<string>();
        string _open = "gate";
        float _wowMax;
        Transform _wow;              // 演出の部品の親
        RealTimeSky _sky;
        Vector3 _moonScale = Vector3.one, _haloScale = Vector3.one;
        Mesh _cone;
        static readonly int IdNightW = Shader.PropertyToID("_TsNight");

        float LaserDim => 1f - 0.65f * _wowMax;

        // ───────── 準備 ─────────

        void WowStart()
        {
            _wow = new GameObject("ありえない演出").transform;
            _wow.SetParent(transform, false);
            _sky = FindFirstObjectByType<RealTimeSky>();
            if (_sky != null && _sky.moonBody) _moonScale = _sky.moonBody.localScale;
            if (_sky != null && _sky.moonHalo) _haloScale = _sky.moonHalo.localScale;
            _cone = ConeMesh();
            OnComment += SkyComment;
        }

        void OnDestroy() => OnComment -= SkyComment;

        void WowBegin()
        {
            _open = ForceOpen ?? Openings[Random.Range(0, Openings.Length)];
            _fxBag.Clear(); _k.Clear(); _plan.Clear(); _grandUsed.Clear();
            _starQ.Clear(); _constDone = false;
            _wowMax = 0; FloatHeight = 0; PerformerOffset = Vector3.zero; PerformerYaw = 0;
            if (_wow) _wow.gameObject.SetActive(true);
            Debug.Log("[Live] opening: " + _open);
        }

        void WowEnd()
        {
            FloatHeight = 0; PerformerOffset = Vector3.zero; PerformerYaw = 0;
            _k.Clear();
            GrandEnd();
            if (_wow) _wow.gameObject.SetActive(false);
            RestoreSky();
            if (_onStageT > 0) { _onStageT = 0; }
            if (_lyric) _lyric.gameObject.SetActive(false);
        }

        /// <summary>曲のはじめ：区切りごとの演出を決める（同じライブでなるべく重ならないように）。</summary>
        void WowSongStart()
        {
            _plan.Clear();
            var s = Song;
            if (s == null) return;
            if (s.Hype)
            {
                foreach (var sec in s.secs)
                {
                    if (sec.name == "bridge") { _plan[sec] = ForceFx ?? NextGrandOr(Random.value < 0.7f ? "alone" : "rise"); continue; } // 大仕掛け／急にふたりきり／舞台がせり上がる
                    if (sec.name != "chorus" && sec.name != "drop" && sec.name != "inter") continue;
                    if (sec.name == "inter" && Random.value < 0.5f) continue;
                    _plan[sec] = ForceFx ?? NextFx();
                }
            }
            else
            {
                // バラード：前半は海の中（か星の雨）、大サビの前に巨大なヤチヨ、最後は月へ
                float r0 = Random.value;
                string first = ForceFx ?? (r0 < 0.3f ? "flood" : r0 < 0.6f ? "sea" : r0 < 0.8f ? "bloom" : "rain");
                bool late = false;
                foreach (var sec in s.secs)
                {
                    if (sec.name == "bridge") late = true;
                    if (!late) { if (sec.name != "intro") _plan[sec] = first; }
                    else if (sec.name == "bridge") _plan[sec] = ForceFx ?? (Random.value < 0.45f ? "ascend" : Random.value < 0.55f ? "alone" : "giant"); // 会場ごと天へ（そのあと月へ）
                    else _plan[sec] = ForceFx ?? "moon";
                }
            }
            var sb = new System.Text.StringBuilder($"[Live] plan '{s.title}':");
            foreach (var sec in s.secs) if (_plan.TryGetValue(sec, out var f)) sb.Append($" {sec.name}={f}");
            Debug.Log(sb.ToString());
        }

        /// <summary>確認用：演出をすぐ選びなおす。</summary>
        public void Replan() => WowSongStart();

        string NextGrandOr(string other) => NextGrand() ?? other;

        string NextFx()
        {
            var g = NextGrand(); // 大仕掛けを優先（毎回のライブで見られるように）
            if (g != null) return g;
            if (_fxBag.Count == 0) { _fxBag.AddRange(HypeFx); }
            int i = Random.Range(0, _fxBag.Count);
            var f = _fxBag[i]; _fxBag.RemoveAt(i);
            return f;
        }

        /// <summary>確認用に時間をとばしたとき：演出をすぐその強さにする。</summary>
        void WowSeek()
        {
            var sec = Sec;
            _k.Clear();
            if (sec != null && _plan.TryGetValue(sec, out var f)) _k[f] = 1f;
        }

        float K(string fx) => _k.TryGetValue(fx, out var v) ? v : 0f;

        // ───────── 毎フレーム ─────────

        void WowTick(float dt)
        {
            var s = Song; var sec = Sec;
            float beat = s != null ? s.Beat(_songT) : _pt * 1.5f;
            float onBeat = Mathf.Pow(1f - Mathf.Repeat(beat, 1f), 3f);
            float t = Time.time;
            string want = null;
            if (sec != null) _plan.TryGetValue(sec, out want);
            foreach (var f in AllFx) Fade(f, want, dt);
            _wowMax = 0; foreach (var v in _k.Values) _wowMax = Mathf.Max(_wowMax, v);

            // 浮く高さ（無重力・月へ昇る）
            float fl = K("float") * (3.6f + Mathf.Sin(t * 0.7f) * 0.8f);
            fl = Mathf.Max(fl, K("moon") * 2.2f);
            FloatHeight = fl;
            PerformerOffset = Vector3.up * fl * 1.25f;
            PerformerYaw = 0;

            Opening(dt, t);
            Float(K("float"), t);
            Tunnel(K("tunnel"), beat, onBeat, dt);
            Dragon(K("dragon"), t, onBeat);
            Chibi(K("chibi"), t, beat);
            Flyby(K("flyby"), t);
            IslandDance(K("island"), t, onBeat, beat);
            Giant(K("giant"), t, beat);
            Rain(K("rain"), dt);
            Flood(K("flood"), t);
            Sea(Mathf.Max(K("sea"), K("flood")), t, dt);
            Rise(K("rise"), t, beat);
            Alone(K("alone"), t);
            Shatter(K("shatter"), t, dt);
            Clones(K("clones"), t);
            Sakura(K("sakura"), t);
            Ribbons(K("ribbons"), t, beat);
            Bloom(K("bloom"), t, beat, onBeat);
            Moon(Mathf.Max(K("moon"), K("ascend") * 0.85f), dt); // 会場ごと天へ：月が目の前に
            GrandTick(dt, t, beat, onBeat);
            Constellation();
            SkyLyrics();
            SkyComments(dt);
            StageGuest(dt);
        }

        void Fade(string f, string want, float dt)
        {
            float k = K(f), w = f == want ? 1f : 0f;
            bool sudden = f == "flood" || f == "shatter"; // 「急に」起こる演出
            float up = sudden ? 0.45f : f == "alone" ? 2.6f : f == "ascend" ? 6f : f == "spiral" || f == "morph" ? 3f : 1.4f; // ヤチヨが会いに来るのは、ゆっくり。会場が昇るのも、ゆっくり
            float down = f == "ascend" ? 5f : f == "spiral" ? 2.6f : 1.8f;
            k = Mathf.MoveTowards(k, w, dt / (w > k ? up : down));
            if (k <= 0) _k.Remove(f); else _k[f] = k;
        }

        // ───────── 部品をつくる道具 ─────────

        GameObject Part(PrimitiveType type, Transform parent, Material m, Vector3 pos, Vector3 scale)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>(); if (col) Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return g;
        }

        Material GlowM(Color c, float night = 3f)
        {
            var m = new Material(glowMat);
            m.SetColor("_Color", c); m.SetFloat("_Day", 1.6f); m.SetFloat("_Night", night); m.SetFloat("_Pulse", 0);
            return m;
        }

        Material HoloM(Color c)
        {
            var m = new Material(holoMat);
            m.SetColor("_Color", c);
            m.SetFloat("_Day", 1.1f); m.SetFloat("_Night", 3f); m.SetFloat("_Core", 0.6f);
            return m;
        }

        Transform Group(string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(_wow, false);
            return g;
        }

        /// <summary>鳥居（2本の柱・笠木・貫）。幅 w、高さ h。足もとが原点。</summary>
        Transform Torii(Transform parent, float w, float h, Material m)
        {
            var g = new GameObject("鳥居").transform;
            g.SetParent(parent, false);
            float th = w * 0.045f;
            Part(PrimitiveType.Cube, g, m, new Vector3(-w * 0.38f, h * 0.45f, 0), new Vector3(th, h * 0.9f, th));
            Part(PrimitiveType.Cube, g, m, new Vector3(w * 0.38f, h * 0.45f, 0), new Vector3(th, h * 0.9f, th));
            Part(PrimitiveType.Cube, g, m, new Vector3(0, h * 0.94f, 0), new Vector3(w, th * 1.3f, th * 1.6f));
            Part(PrimitiveType.Cube, g, m, new Vector3(0, h * 0.76f, 0), new Vector3(w * 0.86f, th * 0.8f, th));
            return g;
        }

        ParticleSystem _streak, _bubbles, _stars;

        ParticleSystem Streaks()
        {
            if (_streak) return _streak;
            _streak = Particles("流れる光", 3000, 1.2f, 2f, 0.25f, 0.5f, 0);
            _streak.transform.SetParent(_wow, false);
            var r = _streak.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 2f; r.maxParticleSize = 1f;
            _streak.Play();
            return _streak;
        }

        static Mesh ConeMesh()
        {
            const int n = 16;
            var v = new List<Vector3>(); var nn = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                v.Add(d * 0.12f); v.Add(d + Vector3.forward); nn.Add(d); nn.Add(d);
                uv.Add(new Vector2(0.5f, 0)); uv.Add(new Vector2(0.5f, 1));
            }
            for (int i = 0; i < n; i++) { int k = i * 2; t.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 }); }
            var m = new Mesh { name = "wow_cone" };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.bounds = new Bounds(new Vector3(0, 0, 0.5f), new Vector3(2, 2, 1));
            return m;
        }

        Renderer Cone(Transform parent, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = _cone;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = beamMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        // ───────── 登場 ─────────

        Transform _gate, _stairs;
        Renderer _gateLight;
        Transform[] _steps;
        const int StepN = 22;

        float EntranceLandT => _open == "stairs" ? 7.2f : _open == "geyser" ? 4.6f : 6.2f;
        bool EntranceActive(float songT) => songT < EntranceLandT + 0.05f;
        bool EntranceSparkle(float songT) => songT > 0.5f && songT < EntranceLandT;
        Vector3 GateTop => new Vector3(stageCenter.x, Island.Ground + 22f, stageCenter.z - 12f);

        Vector3 EntrancePos(float songT, out bool visible)
        {
            visible = songT > 0.55f;
            float u = Ease((songT - 0.6f) / (EntranceLandT - 0.6f));
            switch (_open)
            {
                case "stairs":
                {
                    var top = StepPos(0); var bottom = spot;
                    if (u >= 1) return spot;
                    float f = u * (StepN - 1);
                    int i = Mathf.Clamp((int)f, 0, StepN - 2);
                    return Vector3.Lerp(StepPos(i), StepPos(i + 1), f - i) + Vector3.up * 0.12f;
                }
                case "geyser":
                {
                    // 舞台の下から飛び出して、ふわっと降りる
                    float up = Mathf.Clamp01((songT - 0.4f) / 0.9f);
                    float h = songT < 1.3f ? Mathf.Lerp(-3f, 12f, Mathf.Sqrt(up)) : Mathf.Lerp(12f, 0f, Ease((songT - 1.3f) / (EntranceLandT - 1.3f)));
                    visible = songT > 0.4f;
                    return spot + Vector3.up * h;
                }
                default:
                {
                    // 大鳥居の門から、らせんをえがいて降りてくる
                    var p = Vector3.Lerp(GateTop + Vector3.up * 2f, spot, u);
                    float a = u * Mathf.PI * 3f, r = (1 - u) * 6f;
                    return p + new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r * 0.5f);
                }
            }
        }

        Vector3 StepPos(int i)
        {
            float u = i / (float)(StepN - 1);
            var top = new Vector3(stageCenter.x - 14f, Island.Ground + 40f, stageCenter.z - 30f);
            var p = Vector3.Lerp(top, spot, u);
            p.x += Mathf.Sin(u * Mathf.PI) * 10f; // ゆるくカーブ
            return p;
        }

        void Opening(float dt, float t)
        {
            bool first = (_phase == Phase.Opening) || (_phase == Phase.Song && _si == 0 && _songT < 16f);
            float pre = _phase == Phase.Opening ? Mathf.Clamp01((_pt - 1f) / 4f) : 1f;          // 開演前にあらわれる
            float post = _phase == Phase.Song ? 1f - Mathf.Clamp01((_songT - 11f) / 4f) : 1f;     // 登場のあと消える
            float k = first ? pre * post : 0;
            // 大鳥居
            if (_open == "gate" && k > 0 && _gate == null)
            {
                _gate = Group("空の大鳥居");
                var m = GlowM(new Color(1f, 0.32f, 0.22f), 3.5f);
                Torii(_gate, 30f, 30f, m);
                var gl = Part(PrimitiveType.Quad, _gate, beamMat, new Vector3(0, 13.5f, 0), new Vector3(22f, 26f, 1f));
                _gateLight = gl.GetComponent<Renderer>();
            }
            if (_gate)
            {
                _gate.gameObject.SetActive(k > 0.001f);
                _gate.position = GateTop + Vector3.down * 17f + Vector3.up * Mathf.Sin(t * 0.5f) * 0.6f;
                _gate.localScale = Vector3.one * Mathf.Max(0.001f, Ease(k));
                if (_gateLight)
                {
                    float open = _phase == Phase.Song ? Mathf.Clamp01(1f - Mathf.Abs(_songT - 1.2f) / 2.5f) : 0.15f * pre;
                    _mpb.SetColor(IdColor, new Color(0.7f, 0.85f, 1f) * (0.2f + open * 1.5f) * k);
                    _gateLight.SetPropertyBlock(_mpb);
                }
                if (k > 0.05f && Random.value < 0.5f)
                    Emit(_burst, _gate.position + new Vector3(Random.Range(-15f, 15f), Random.Range(0f, 30f), 0), Random.insideUnitSphere * 2f, Color.Lerp(new Color(1f, 0.4f, 0.3f), new Color(1f, 0.85f, 0.5f), Random.value));
            }
            // 光の階段
            if (_open == "stairs" && k > 0 && _stairs == null)
            {
                _stairs = Group("光の階段");
                var m = GlowM(new Color(0.55f, 0.85f, 1f), 3f);
                _steps = new Transform[StepN];
                for (int i = 0; i < StepN; i++)
                {
                    var st = Part(PrimitiveType.Cube, _stairs, m, StepPos(i), new Vector3(2.6f, 0.12f, 1.2f)).transform;
                    var d = StepPos(Mathf.Min(i + 1, StepN - 1)) - StepPos(Mathf.Max(i - 1, 0)); d.y = 0;
                    if (d.sqrMagnitude > 0.01f) st.rotation = Quaternion.LookRotation(d);
                    _steps[i] = st;
                }
            }
            if (_stairs)
            {
                _stairs.gameObject.SetActive(k > 0.001f);
                for (int i = 0; i < StepN; i++)
                {
                    float appear = _phase == Phase.Opening ? Mathf.Clamp01((_pt - 1.5f) * 4f - i * 0.18f) : 1f;
                    _steps[i].localScale = new Vector3(2.6f, 0.12f, 1.2f) * appear * k;
                }
            }
            // 海の柱
            if (_open == "geyser" && _phase == Phase.Song && _si == 0 && _songT > 0.3f && _songT < 2.6f)
            {
                for (int i = 0; i < 25; i++)
                    Emit(_flames, spot + new Vector3(Random.Range(-1.4f, 1.4f), 0, Random.Range(-1.4f, 1.4f)), new Vector3(Random.Range(-2f, 2f), Random.Range(22f, 34f), Random.Range(-2f, 2f)),
                         Color.Lerp(new Color(0.5f, 0.85f, 1f), Color.white, Random.value), Random.Range(1.4f, 2.2f), Random.Range(0.5f, 1.1f));
                if (_songT < 0.4f + dt * 2f)
                    for (int i = 0; i < 120; i++)
                    {
                        float a = i * Mathf.PI * 2f / 120f;
                        Emit(_burst, stageCenter + Vector3.up * 2.4f, new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * 16f, new Color(0.6f, 0.9f, 1f), 1.6f, 0.5f);
                    }
            }
        }

        // ───────── 無重力 ─────────

        void Float(float k, float t)
        {
            if (k <= 0 || _burst == null) return;
            // 光の粒が下から上へ「降る」
            for (int i = 0; i < 4; i++)
                if (Random.value < k)
                    Emit(_motes, new Vector3(Random.Range(seatMin.x - 6, seatMax.x + 6), Island.Ground + Random.Range(0f, 2f), Random.Range(seatMin.y - 14, seatMax.y + 2)),
                         new Vector3(0, Random.Range(3f, 7f), 0), Color.Lerp(PenColors[2], PenColors[1], Random.value), 4f, 0.35f);
        }

        // ───────── 千本鳥居トンネル ─────────

        readonly List<Transform> _tunnel = new List<Transform>();
        readonly List<Renderer[]> _tunnelR = new List<Renderer[]>();
        float _tunnelNext;

        void Tunnel(float k, float beat, float onBeat, float dt)
        {
            if (k <= 0 && _tunnel.Count == 0) return;
            if (_tunnel.Count == 0)
            {
                var g = Group("千本鳥居トンネル");
                var m = GlowM(new Color(1f, 0.3f, 0.2f), 3.2f);
                for (int i = 0; i < 16; i++)
                {
                    var tr = Torii(g, 50f, 34f, m);
                    tr.gameObject.SetActive(false);
                    _tunnel.Add(tr); _tunnelR.Add(tr.GetComponentsInChildren<Renderer>());
                }
            }
            float speed = 85f;
            float z0 = stageCenter.z - 300f, z1 = stageCenter.z + 80f;
            // 拍ごとに1つ
            _tunnelNext -= dt;
            if (k > 0.3f && _tunnelNext <= 0)
            {
                _tunnelNext = Song != null ? Song.BeatLen : 0.45f;
                foreach (var tr in _tunnel)
                    if (!tr.gameObject.activeSelf) { tr.gameObject.SetActive(true); tr.position = new Vector3(stageCenter.x, Island.Ground - 3f, z0); break; }
            }
            int n = 0;
            for (int i = 0; i < _tunnel.Count; i++)
            {
                var tr = _tunnel[i];
                if (!tr.gameObject.activeSelf) continue;
                n++;
                tr.position += Vector3.forward * speed * dt;
                if (tr.position.z > z1) { tr.gameObject.SetActive(false); continue; }
                float near = 1f - Mathf.Clamp01(Mathf.Abs(tr.position.z - (stageCenter.z + 20f)) / 200f);
                var c = (i % 3 == 2 ? new Color(1f, 0.8f, 0.35f) : new Color(1f, 0.3f, 0.2f)) * (0.4f + near * 0.8f + onBeat * 0.5f) * Mathf.Max(k, 0.2f);
                _mpb.SetColor(IdColor, c);
                foreach (var r in _tunnelR[i]) r.SetPropertyBlock(_mpb);
            }
            // 速さを感じる光の筋
            if (k > 0.1f)
            {
                var ps = Streaks();
                for (int i = 0; i < 6; i++)
                    if (Random.value < k)
                    {
                        float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(14f, 40f);
                        Emit(ps, new Vector3(stageCenter.x + Mathf.Cos(a) * r, Island.Ground + 14f + Mathf.Sin(a) * r * 0.6f, stageCenter.z - 160f), new Vector3(0, 0, 140f), Color.Lerp(new Color(1f, 0.5f, 0.35f), Color.white, Random.value), 1.4f, 0.4f);
                    }
            }
        }

        // ───────── 光の竜 ─────────

        Transform _dragon;
        Transform[] _seg;
        const int SegN = 46;

        void Dragon(float k, float t, float onBeat)
        {
            if (k <= 0) { if (_dragon) _dragon.gameObject.SetActive(false); return; }
            if (_dragon == null)
            {
                _dragon = Group("光の竜");
                var m1 = HoloM(new Color(0.35f, 0.9f, 1f)); var m2 = HoloM(new Color(1f, 0.55f, 0.85f));
                var gold = GlowM(new Color(1f, 0.85f, 0.4f), 3.5f);
                _seg = new Transform[SegN];
                for (int i = 0; i < SegN; i++)
                {
                    var s = Part(PrimitiveType.Sphere, _dragon, i % 4 == 0 ? m2 : m1, Vector3.zero, Vector3.one).transform;
                    _seg[i] = s;
                    if (i == 0)
                    {
                        Part(PrimitiveType.Cube, s, gold, new Vector3(-0.35f, 0.45f, -0.2f), new Vector3(0.12f, 0.7f, 0.12f)).transform.localRotation = Quaternion.Euler(-40, 0, 20);
                        Part(PrimitiveType.Cube, s, gold, new Vector3(0.35f, 0.45f, -0.2f), new Vector3(0.12f, 0.7f, 0.12f)).transform.localRotation = Quaternion.Euler(-40, 0, -20);
                        Part(PrimitiveType.Sphere, s, gold, new Vector3(-0.22f, 0.12f, 0.38f), Vector3.one * 0.16f);
                        Part(PrimitiveType.Sphere, s, gold, new Vector3(0.22f, 0.12f, 0.38f), Vector3.one * 0.16f);
                    }
                    else if (i % 6 == 3)
                        Part(PrimitiveType.Cube, s, gold, new Vector3(0, 0.55f, 0), new Vector3(0.08f, 0.5f, 0.5f)); // 背びれ
                }
            }
            _dragon.gameObject.SetActive(true);
            var c = new Vector3(stageCenter.x, Island.Ground, stageCenter.z + 6f);
            float rise = (1f - Ease(k)) * -45f; // 海から昇ってくる
            Vector3 P(float a) => c + new Vector3(Mathf.Cos(a) * (30f + Mathf.Sin(a * 2f) * 6f), 17f + Mathf.Sin(a * 1.7f + t * 0.3f) * 8f + rise, Mathf.Sin(a) * (24f + Mathf.Sin(a * 2f) * 4f));
            float head = t * 0.62f;
            for (int i = 0; i < SegN; i++)
            {
                float a = head - i * 0.085f;
                var p = P(a);
                var q = P(a + 0.02f);
                _seg[i].position = p;
                _seg[i].rotation = Quaternion.LookRotation(q - p);
                float s = i == 0 ? 5.5f : Mathf.Lerp(4.6f, 0.8f, i / (float)SegN) * (1f + 0.08f * Mathf.Sin(t * 6f - i * 0.5f));
                _seg[i].localScale = new Vector3(s, s, s * (i == 0 ? 1.4f : 1.15f));
            }
            if (Random.value < 0.8f) Emit(_burst, _seg[SegN - 1].position, Random.insideUnitSphere * 2f, new Color(0.5f, 0.9f, 1f), 1.5f, 0.6f);
            if (onBeat > 0.9f) Emit(_burst, _seg[0].position + _seg[0].forward * 3f, _seg[0].forward * 12f + Random.insideUnitSphere * 4f, new Color(1f, 0.85f, 0.45f), 0.8f, 0.8f);
        }

        // ───────── ちびヤチヨの群れ ─────────

        readonly List<(Transform root, Transform armL, Transform armR)> _chibi = new List<(Transform, Transform, Transform)>();

        void Chibi(float k, float t, float beat)
        {
            if (k <= 0) { foreach (var c in _chibi) if (c.root) c.root.gameObject.SetActive(false); return; }
            if (_chibi.Count == 0 && _crowd != null && _crowd.Ready)
            {
                for (int i = 0; i < 12; i++)
                {
                    var root = _crowd.MakeFigure(DollLook.Yachiyo, 0.9f, null, out var al, out var ar);
                    root.SetParent(_wow, true);
                    _chibi.Add((root, al, ar));
                }
            }
            var center = new Vector3(stageCenter.x, Island.Ground + 5.5f, seatMin.y + 1f);
            for (int i = 0; i < _chibi.Count; i++)
            {
                var (root, al, ar) = _chibi[i];
                if (!root) continue;
                root.gameObject.SetActive(true);
                Vector3 P(float tt) => center + new Vector3(Mathf.Sin(tt * 0.55f + i * 0.52f) * 17f, Mathf.Sin(tt * 1.3f + i) * 2.2f + (i % 3) * 1.4f, Mathf.Cos(tt * 0.42f + i * 0.71f) * 11f);
                var p = P(t); var v = P(t + 0.1f) - p;
                var from = spot + Vector3.up * 1.5f;
                root.position = Vector3.Lerp(from, p, Ease(k * 1.2f - i * 0.02f));
                v.y = 0;
                if (v.sqrMagnitude > 0.0001f) root.rotation = Quaternion.LookRotation(v) * Quaternion.Euler(10f, 0, Mathf.Sin(t * 3f + i) * 15f);
                root.localScale = Vector3.one * 0.9f * Mathf.Clamp01(k * 1.5f);
                float w = Mathf.Sin((beat + i * 0.25f) * Mathf.PI);
                if (al) al.localRotation = Quaternion.Euler(0, 0, -140f + w * 25f);
                if (ar) ar.localRotation = Quaternion.Euler(0, 0, 140f - w * 25f);
                if (Random.value < 0.25f) Emit(_burst, root.position, Vector3.down * 0.5f, PenColors[(i % 2 == 0) ? 1 : 2], 1f, 0.25f);
            }
        }

        // ───────── ヤチヨが客席の上を飛ぶ ─────────

        Transform _disc;

        void Flyby(float k, float t)
        {
            if (k <= 0) { if (_disc) _disc.gameObject.SetActive(false); return; }
            if (_disc == null)
            {
                _disc = Group("光の円盤");
                var m = GlowM(new Color(0.5f, 0.9f, 1f), 3f);
                Part(PrimitiveType.Cylinder, _disc, m, Vector3.zero, new Vector3(2.4f, 0.04f, 2.4f));
                var ring = GlowM(new Color(1f, 0.5f, 0.8f), 3.5f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    Part(PrimitiveType.Cube, _disc, ring, new Vector3(Mathf.Cos(a) * 1.4f, -0.05f, Mathf.Sin(a) * 1.4f), new Vector3(0.3f, 0.06f, 0.1f)).transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0);
                }
                var cone = Cone(_disc, "下への光");
                cone.transform.localRotation = Quaternion.LookRotation(Vector3.down);
                cone.transform.localScale = new Vector3(2.2f, 2.2f, 9f);
            }
            float w = t * 0.42f;
            var mid = new Vector3(stageCenter.x, Island.Ground + 8.5f, (seatMin.y + seatMax.y) / 2f - 2f);
            var loop = mid + new Vector3(Mathf.Sin(w) * 14f, Mathf.Sin(w * 2f) * 1.5f, Mathf.Cos(w) * 8f);
            var vel = new Vector3(Mathf.Cos(w), 0, -Mathf.Sin(w) * 0.6f);
            float e = Ease(k);
            PerformerOffset += (loop - spot) * e;
            // 客席のほうを向きつつ、進む向きへ少し傾く
            PerformerYaw = Mathf.LerpAngle(0, Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg * 0.4f, e);
            _disc.gameObject.SetActive(true);
            _disc.position = spot + PerformerOffset - Vector3.up * 0.05f;
            _disc.rotation = Quaternion.Euler(0, t * 60f, 0);
            _disc.localScale = Vector3.one * Mathf.Max(0.01f, e);
            if (Random.value < 0.9f * k) Emit(_burst, _disc.position + Random.insideUnitSphere * 1.2f, Vector3.down * 1.5f, Color.Lerp(PenColors[2], PenColors[1], Random.value), 1.8f, 0.3f);
        }

        // ───────── 島が踊る ─────────

        readonly List<Renderer> _search = new List<Renderer>();
        readonly List<Vector3> _searchBase = new List<Vector3>();

        void IslandDance(float k, float t, float onBeat, float beat)
        {
            if (k > 0)
            {
                // 島じゅうの提灯・ネオン・灯籠（光るものぜんぶ）を、拍に合わせて明滅させる
                float night = RealTimeSky.Night;
                float pulse = Mathf.Lerp(night, 0.2f + 0.8f * onBeat, k * 0.9f);
                Shader.SetGlobalFloat(IdNightW, pulse);
            }
            if (k <= 0) { foreach (var r in _search) if (r) r.enabled = false; return; }
            if (_search.Count == 0)
            {
                var g = Group("島の光の柱");
                foreach (var z in Zone.All)
                {
                    if (z.id == "stage" || _search.Count >= 22) continue;
                    var r = Cone(g, "光の柱");
                    r.transform.position = z.warpPos + Vector3.up * 1f;
                    r.transform.localScale = new Vector3(9f, 9f, 170f);
                    _search.Add(r); _searchBase.Add(z.warpPos);
                }
            }
            for (int i = 0; i < _search.Count; i++)
            {
                var r = _search[i];
                r.enabled = true;
                float a = t * 0.8f + i * 1.3f;
                var dir = new Vector3(Mathf.Sin(a) * 0.45f, 1f, Mathf.Cos(a * 0.7f) * 0.45f);
                r.transform.rotation = Quaternion.LookRotation(dir);
                var c = PenColors[(i + (int)beat) % PenColors.Length];
                _mpb.SetColor(IdColor, c * (0.7f + 1.1f * onBeat) * k);
                r.SetPropertyBlock(_mpb);
            }
        }

        // ───────── 巨大なヤチヨ ─────────

        Transform _giant, _gArmL, _gArmR;

        void Giant(float k, float t, float beat)
        {
            if (k <= 0) { if (_giant) _giant.gameObject.SetActive(false); return; }
            if (_giant == null && _crowd != null && _crowd.Ready)
            {
                _giant = _crowd.MakeFigure(DollLook.Yachiyo, 17f, HoloM(new Color(0.55f, 0.85f, 1f)), out _gArmL, out _gArmR);
                _giant.SetParent(_wow, true);
                foreach (var r in _giant.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_giant == null) return;
            _giant.gameObject.SetActive(true);
            float e = Ease(k);
            _giant.position = new Vector3(stageCenter.x, Island.Ground - 36f * (1f - e) - 7f + Mathf.Sin(t * 0.8f) * 0.6f, stageCenter.z - 34f); // 舞台のすぐうしろの海から、腰まで出す
            _giant.rotation = Quaternion.Euler(0, Mathf.Sin(beat * Mathf.PI / 4f) * 12f, Mathf.Sin(beat * Mathf.PI / 2f) * 3f);
            bool ballad = Song != null && Song.style == 1;
            float w = Mathf.Sin(beat * Mathf.PI * (ballad ? 0.25f : 0.5f));
            if (_gArmL) _gArmL.localRotation = Quaternion.Euler(-30f + w * 20f, 0, -120f + w * 30f);
            if (_gArmR) _gArmR.localRotation = Quaternion.Euler(-30f - w * 20f, 0, 120f + w * 30f);
            // 海から立ち上がるときのしぶき
            if (k < 0.95f && k > 0.05f)
                for (int i = 0; i < 10; i++)
                    Emit(_flames, _giant.position + new Vector3(Random.Range(-12f, 12f), 36f * (1f - e) + 7f, Random.Range(-6f, 6f)) , new Vector3(Random.Range(-3f, 3f), Random.Range(10f, 18f), Random.Range(-3f, 3f)), new Color(0.6f, 0.9f, 1f), 1.6f, 1.2f);
        }

        // ───────── 星の雨 ─────────

        void Rain(float k, float dt)
        {
            if (k <= 0) return;
            var ps = Streaks();
            int n = Mathf.CeilToInt(14 * k);
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(stageCenter.x + Random.Range(-170f, 120f), Island.Ground + Random.Range(110f, 150f), stageCenter.z + Random.Range(-260f, 60f));
                Emit(ps, p, new Vector3(55f, -95f, 22f), Color.Lerp(new Color(1f, 0.85f, 0.5f), Random.value < 0.5f ? PenColors[2] : PenColors[1], Random.value), 2.2f, Random.Range(0.4f, 0.9f));
            }
            _skyTint = Color.Lerp(_skyTint, new Color(0.32f, 0.1f, 0.42f), k * 0.7f);
            _skyK = Mathf.Max(_skyK, k);
        }

        // ───────── 海の中 ─────────

        Transform _sea;
        readonly List<(Transform t, float r, float h, float sp, float ph)> _fish = new List<(Transform, float, float, float, float)>();
        readonly List<Renderer> _rays = new List<Renderer>();
        Color _skyTint; float _skyK, _fogStart = -1, _fogEnd = -1;

        void Sea(float k, float t, float dt)
        {
            if (k <= 0) { if (_sea) _sea.gameObject.SetActive(false); return; }
            if (_sea == null)
            {
                _sea = Group("海の中");
                var mf = new[] { HoloM(new Color(0.4f, 0.9f, 1f)), HoloM(new Color(1f, 0.55f, 0.85f)), HoloM(new Color(1f, 0.85f, 0.45f)) };
                for (int i = 0; i < 34; i++)
                {
                    var f = new GameObject("光る魚").transform;
                    f.SetParent(_sea, false);
                    var m = mf[i % 3];
                    float s = Random.Range(0.6f, 1.6f) * (i % 9 == 0 ? 4f : 1f); // ときどき大きな魚
                    Part(PrimitiveType.Sphere, f, m, Vector3.zero, new Vector3(0.45f, 0.6f, 1.6f) * s);
                    Part(PrimitiveType.Cube, f, m, new Vector3(0, 0, -0.95f * s), new Vector3(0.05f, 0.55f, 0.45f) * s);
                    _fish.Add((f, Random.Range(6f, 32f), Random.Range(2f, 16f), Random.Range(0.15f, 0.4f) * (Random.value < 0.5f ? -1 : 1), Random.Range(0f, 6.3f)));
                }
                for (int i = 0; i < 9; i++)
                {
                    var r = Cone(_sea, "海の光");
                    r.transform.position = new Vector3(stageCenter.x + Random.Range(-30f, 30f), Island.Ground + 60f, stageCenter.z + Random.Range(-20f, 30f));
                    r.transform.localScale = new Vector3(5f, 5f, 70f);
                    _rays.Add(r);
                }
                _bubbles = Particles("泡", 1200, 4f, 7f, 0.15f, 0.35f, -0.15f);
                _bubbles.transform.SetParent(_sea, false);
            }
            _sea.gameObject.SetActive(true);
            var c = new Vector3(stageCenter.x, Island.Ground, stageCenter.z + 8f);
            foreach (var (f, r, h, sp, ph) in _fish)
            {
                float a = t * sp + ph;
                var p = c + new Vector3(Mathf.Cos(a) * r, h + Mathf.Sin(a * 3f) * 0.8f, Mathf.Sin(a) * r * 0.8f);
                var q = c + new Vector3(Mathf.Cos(a + 0.05f * Mathf.Sign(sp)) * r, h, Mathf.Sin(a + 0.05f * Mathf.Sign(sp)) * r * 0.8f);
                f.position = p; f.rotation = Quaternion.LookRotation(q - p) * Quaternion.Euler(0, Mathf.Sin(t * 6f + ph) * 12f, 0);
                f.localScale = Vector3.one * Ease(k);
            }
            for (int i = 0; i < _rays.Count; i++)
            {
                var r = _rays[i];
                r.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(t * 0.2f + i) * 0.15f, -1f, Mathf.Cos(t * 0.17f + i) * 0.15f));
                _mpb.SetColor(IdColor, new Color(0.45f, 0.85f, 0.95f) * 0.35f * k * (0.7f + 0.3f * Mathf.Sin(t * 0.8f + i * 2f)));
                r.SetPropertyBlock(_mpb);
            }
            for (int i = 0; i < 3; i++)
                if (Random.value < k)
                    Emit(_bubbles, new Vector3(Random.Range(seatMin.x - 10, seatMax.x + 10), Island.Ground + 0.2f, Random.Range(seatMin.y - 18, seatMax.y + 4)), new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(1.5f, 3f), 0), new Color(0.6f, 0.95f, 1f), 6f, Random.Range(0.12f, 0.35f));
            _skyTint = Color.Lerp(_skyTint, new Color(0.02f, 0.28f, 0.38f), k);
            _skyK = Mathf.Max(_skyK, k);
            _fogClose = Mathf.Max(_fogClose, k);
        }

        float _fogClose;

        // ───────── 月へ昇る ─────────

        void Moon(float k, float dt)
        {
            if (_sky != null && _sky.moonBody)
            {
                var cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                float e = Ease(k);
                if (k > 0)
                {
                    // 月の向き（RealTimeSky と同じ計算）。近づけて、大きくする
                    float el = Mathf.Lerp(_sky.moonElevation, 24f, e) * Mathf.Deg2Rad, az = _sky.moonAzimuth * Mathf.Deg2Rad; // 舞台のうしろへ降りてくる
                    var dir = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
                    float d = _sky.moonDistance;
                    _sky.moonBody.position = cam + dir * d * (1f - 0.3f * e);
                    _sky.moonBody.localScale = _moonScale * (1f + 0.9f * e);
                    if (_sky.moonHalo) { _sky.moonHalo.position = cam + dir * (d + 40f) * (1f - 0.3f * e); _sky.moonHalo.localScale = _haloScale * (1f + 0.9f * e); }
                }
                else
                {
                    _sky.moonBody.localScale = _moonScale;
                    if (_sky.moonHalo) _sky.moonHalo.localScale = _haloScale;
                }
            }
            if (k <= 0) return;
            // まわりの光が下へ流れていく（昇っているように見える）
            var ps = Streaks();
            for (int i = 0; i < 8; i++)
                if (Random.value < k)
                {
                    float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(25f, 120f);
                    Emit(ps, new Vector3(stageCenter.x + Mathf.Cos(a) * r, Island.Ground + 70f, stageCenter.z + Mathf.Sin(a) * r), new Vector3(0, -70f, 0), Color.Lerp(new Color(1f, 0.9f, 0.7f), PenColors[2], Random.value * 0.5f), 1.6f, 0.4f);
                }
            _skyTint = Color.Lerp(_skyTint, new Color(0.05f, 0.03f, 0.12f), k * 0.6f);
            _skyK = Mathf.Max(_skyK, k * 0.6f);
        }

        /// <summary>空の色と霧を上書きする（RealTimeSky は0.5秒ごとにもとにもどすので、毎フレーム上書き）。</summary>
        void LateSky()
        {
            if (_skyK > 0.001f)
            {
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, _skyTint, _skyK);
                RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, _skyTint * 2.2f, _skyK * 0.7f);
                RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, _skyTint * 1.8f, _skyK * 0.7f);
            }
            if (_fogClose > 0.001f)
            {
                if (_fogStart < 0) { _fogStart = RenderSettings.fogStartDistance; _fogEnd = RenderSettings.fogEndDistance; }
                RenderSettings.fogStartDistance = Mathf.Lerp(_fogStart, 2f, _fogClose);
                RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd, 85f, _fogClose);
            }
            else if (_fogStart >= 0) { RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd; _fogStart = _fogEnd = -1; }
            _skyK = 0; _fogClose = 0; _skyTint = RenderSettings.fogColor;
        }

        void RestoreSky()
        {
            if (_fogStart >= 0) { RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd; _fogStart = _fogEnd = -1; }
            if (_sky != null && _sky.moonBody) _sky.moonBody.localScale = _moonScale;
            if (_sky != null && _sky.moonHalo) _sky.moonHalo.localScale = _haloScale;
            Shader.SetGlobalFloat(IdNightW, RealTimeSky.Night);
        }

        // ───────── おわり：ペンライトの星座 ─────────

        readonly List<(float t, Vector3 p, Color c)> _starQ = new List<(float, Vector3, Color)>();
        readonly List<(Vector3 hand, Color c)> _hands = new List<(Vector3, Color)>();
        bool _constDone;

        void Constellation()
        {
            if (_phase == Phase.Ending && _pt > 15f && !_constDone)
            {
                _constDone = true;
                if (_stars == null) { _stars = Particles("ペンライトの星", 1200, 10f, 12f, 0.9f, 1.3f, 0); _stars.transform.SetParent(_wow, false); _stars.Play(); }
                var center = new Vector3(stageCenter.x, Island.Ground + 29f, stageCenter.z - 30f);
                int n = _hands.Count;
                for (int i = 0; i < n; i++)
                {
                    float th = i / (float)n * Mathf.PI * 2f;
                    float x = 16f * Mathf.Pow(Mathf.Sin(th), 3), y = 13f * Mathf.Cos(th) - 5f * Mathf.Cos(2 * th) - 2f * Mathf.Cos(3 * th) - Mathf.Cos(4 * th);
                    var target = center + new Vector3(x, y, 0) * 1.1f;
                    var (hand, c) = _hands[i];
                    float T = Random.Range(3.2f, 4.6f);
                    Emit(_stars, hand, (target - hand) / T, c, T + 0.05f, 0.6f); // 重力なしで、まっすぐ空の点へ
                    _starQ.Add((Time.time + T, target, c));
                }
            }
            for (int i = _starQ.Count - 1; i >= 0; i--)
                if (Time.time >= _starQ[i].t)
                {
                    Emit(_stars, _starQ[i].p, Vector3.zero, Color.Lerp(_starQ[i].c, Color.white, 0.3f), Random.Range(7f, 9f), 1.6f);
                    _starQ.RemoveAt(i);
                }
        }

        // ───────── 急にライブステージが水没する ─────────

        Transform _water;

        void Flood(float k, float t)
        {
            if (k <= 0) { if (_water) _water.gameObject.SetActive(false); return; }
            if (_water == null && waterMat != null)
            {
                _water = Group("水没");
                var up = Part(PrimitiveType.Quad, _water, waterMat, Vector3.zero, new Vector3(150f, 150f, 1f));
                up.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var dn = Part(PrimitiveType.Quad, _water, waterMat, Vector3.zero, new Vector3(150f, 150f, 1f));
                dn.transform.localRotation = Quaternion.Euler(-90, 0, 0); // 水の中から見上げても水面が見える
            }
            if (_water == null) return;
            _water.gameObject.SetActive(true);
            float u = 1f - Mathf.Pow(1f - k, 3f); // 一気に上がって、ゆっくり止まる
            float h = Island.Ground - 0.8f + 9f * u + Mathf.Sin(t * 0.9f) * 0.15f;
            _water.position = new Vector3(stageCenter.x, h, stageCenter.z + 8f);
            // 水が上がっていく間は、水面がはげしくしぶく
            if (k < 0.98f)
                for (int i = 0; i < 30; i++)
                    Emit(_flames, new Vector3(stageCenter.x + Random.Range(-40f, 40f), h, stageCenter.z + 8f + Random.Range(-38f, 38f)), new Vector3(Random.Range(-2f, 2f), Random.Range(4f, 12f), Random.Range(-2f, 2f)), new Color(0.75f, 0.95f, 1f), 1.2f, Random.Range(0.4f, 0.9f));
            FloatHeight = Mathf.Max(FloatHeight, 0.8f * u); // 水の中ではふわっと浮く
        }

        // ───────── 舞台がせり上がる ─────────

        Transform _tower, _towerCol;
        readonly List<Transform> _pistons = new List<Transform>(), _towerRings = new List<Transform>();

        void Rise(float k, float t, float beat)
        {
            if (k <= 0) { if (_tower) _tower.gameObject.SetActive(false); return; }
            if (_tower == null)
            {
                _tower = Group("せり上がる舞台");
                var crystal = HoloM(new Color(0.6f, 0.85f, 1f));
                var pink = HoloM(new Color(1f, 0.55f, 0.85f));
                var gold = GlowM(new Color(1f, 0.82f, 0.45f), 3.2f);
                _towerCol = Part(PrimitiveType.Cylinder, _tower, crystal, Vector3.zero, Vector3.one).transform;
                Part(PrimitiveType.Cylinder, _towerCol, gold, new Vector3(0, 1f, 0), new Vector3(1.15f, 0.02f, 1.15f)); // 上のふち
                for (int i = 0; i < 6; i++)
                    _pistons.Add(Part(PrimitiveType.Cylinder, _tower, i % 2 == 0 ? pink : crystal, Vector3.zero, Vector3.one).transform);
                for (int i = 0; i < 3; i++)
                    _towerRings.Add(Part(PrimitiveType.Cylinder, _tower, gold, Vector3.zero, new Vector3(1f, 0.015f, 1f)).transform);
            }
            _tower.gameObject.SetActive(true);
            float e = Ease(k);
            float h = 16f * e;
            // まんなかの柱がヤチヨを持ち上げる
            _towerCol.position = spot + Vector3.up * (h / 2f - 0.02f);
            _towerCol.localScale = new Vector3(3.4f, Mathf.Max(0.01f, h / 2f), 3.4f);
            PerformerOffset += Vector3.up * h;
            // まわりの6本は、拍に合わせて上下する
            for (int i = 0; i < _pistons.Count; i++)
            {
                float a = i * Mathf.PI / 3f + 0.5f;
                float ph = (6f + 4f * Mathf.Sin(i * 1.7f)) * e * (0.75f + 0.25f * Mathf.Sin(beat * Mathf.PI * 0.5f + i));
                var b = new Vector3(stageCenter.x + Mathf.Cos(a) * 6.2f, spot.y, stageCenter.z + Mathf.Sin(a) * 6.2f);
                _pistons[i].position = b + Vector3.up * ph / 2f;
                _pistons[i].localScale = new Vector3(1.8f, Mathf.Max(0.01f, ph / 2f), 1.8f);
            }
            for (int i = 0; i < _towerRings.Count; i++)
            {
                float y = Mathf.Repeat(t * 3f + i * h / 3f, Mathf.Max(0.1f, h));
                _towerRings[i].position = spot + Vector3.up * y;
                _towerRings[i].localScale = new Vector3(5.5f + Mathf.Sin(t * 2f + i) * 0.6f, 0.015f, 5.5f + Mathf.Sin(t * 2f + i) * 0.6f);
            }
            if (Random.value < 0.8f * k) Emit(_burst, spot + Vector3.up * h + Random.insideUnitSphere * 2f, Vector3.up * Random.Range(2f, 6f), Color.Lerp(new Color(1f, 0.85f, 0.5f), PenColors[2], Random.value), 1.4f, 0.4f);
        }

        // ───────── ヤチヨが会いに来る（花びらの道を飛んで、目の前で歌う） ─────────

        float _aloneK;
        Renderer _aloneLight;

        void Alone(float k, float t)
        {
            _aloneK = k;
            if (k <= 0) { if (_aloneLight) _aloneLight.enabled = false; return; }
            // 空に弧をえがいて飛んできて、目の前に降りる（客席はそのまま、ペンライトがやさしいピンクにそろう）
            var me = _player != null && PlayerInVenue ? _player.Position : new Vector3(stageCenter.x, Island.Ground, (seatMin.y + seatMax.y) / 2f);
            var dir = spot - me; dir.y = 0;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            var stand = me + dir * 2.6f + FloatHeight * Vector3.up; stand.y = Mathf.Max(stand.y, Island.Ground + 0.6f); // 少し浮いている
            float e = Ease(k);
            var mid = (spot + stand) / 2f + Vector3.up * 9f;
            var p = (1 - e) * (1 - e) * spot + 2 * (1 - e) * e * mid + e * e * stand;   // 弧をえがく
            PerformerOffset += p - spot;
            PerformerYaw = Mathf.LerpAngle(0, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg, e);
            // 飛んでくる道に、花びらと光
            if (k < 0.98f && _petals != null)
                for (int i = 0; i < 3; i++) Emit(_petals, p + Random.insideUnitSphere * 0.8f, Random.insideUnitSphere * 1.5f, new Color(1f, 0.6f, 0.8f) * 1.3f, 2.5f, 0.22f);
            // ふたりのまわりを、花びらがゆっくり回る
            Sakura(0, t);
            if (_petals != null && Random.value < k)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    var c0 = (me + stand) / 2f;
                    Emit(_petals, c0 + new Vector3(Mathf.Cos(a) * 5f, Random.Range(0.2f, 3.5f), Mathf.Sin(a) * 5f), new Vector3(-Mathf.Sin(a), 0.2f, Mathf.Cos(a)) * 2.5f, new Color(1f, 0.7f, 0.85f) * 1.2f, 3.5f, 0.2f);
                }
            // まわりは少しだけ暗く、やわらかい色に
            _skyTint = Color.Lerp(_skyTint, new Color(0.16f, 0.06f, 0.2f), k * 0.7f);
            _skyK = Mathf.Max(_skyK, k * 0.5f);
            if (_aloneLight == null) { _aloneLight = Cone(_wow, "ふたりの光"); _aloneLight.transform.rotation = Quaternion.LookRotation(Vector3.down); }
            _aloneLight.enabled = true;
            _aloneLight.transform.position = (me + stand) / 2f + Vector3.up * 14f;
            _aloneLight.transform.localScale = new Vector3(3.6f, 3.6f, 15f);
            _mpb.SetColor(IdColor, new Color(1f, 0.75f, 0.9f) * 0.35f * k);
            _aloneLight.SetPropertyBlock(_mpb);
            if (Random.value < 0.5f * k) Emit(_motes, stand + new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 3f), Random.Range(-3f, 3f)), Vector3.up * 0.4f, new Color(1f, 0.8f, 0.9f), 4f, 0.3f);
        }

        // ───────── 空がガラスのように割れて、別の空があらわれる ─────────

        readonly List<(Transform t, Vector3 v, Vector3 spin)> _shards = new List<(Transform, Vector3, Vector3)>();
        float _shatterWas;

        void Shatter(float k, float t, float dt)
        {
            if (k <= 0 && _shards.Count == 0) return;
            if (_shards.Count == 0)
            {
                var g = Group("空のかけら");
                var m1 = GlowM(new Color(0.75f, 0.92f, 1f), 2.6f); var m2 = GlowM(new Color(1f, 0.6f, 0.9f), 2.6f);
                for (int i = 0; i < 70; i++)
                {
                    var s = Part(PrimitiveType.Cube, g, i % 3 == 0 ? m2 : m1, Vector3.zero, new Vector3(Random.Range(1.5f, 5f), Random.Range(2f, 7f), 0.06f)).transform;
                    s.gameObject.SetActive(false);
                    _shards.Add((s, Vector3.zero, Vector3.zero));
                }
            }
            // 割れた瞬間：空いっぱいに光って、かけらがいっせいに落ちはじめる
            if (k > 0.2f && _shatterWas <= 0.2f) { _flash = 1f; Cheer(0.6f); for (int i = 0; i < _shards.Count; i++) Respawn(i); }
            _shatterWas = k;
            for (int i = 0; i < _shards.Count; i++)
            {
                var (s, v, sp) = _shards[i];
                if (!s.gameObject.activeSelf) continue;
                v += Vector3.down * 9f * dt;
                s.position += v * dt;
                s.Rotate(sp * dt, Space.Self);
                _shards[i] = (s, v, sp);
                if (s.position.y < Island.Ground - 2f) { if (k > 0.3f) Respawn(i); else s.gameObject.SetActive(false); }
            }
            if (k > 0)
            {
                _skyTint = Color.Lerp(_skyTint, new Color(0.38f, 0.05f, 0.32f), k);
                _skyK = Mathf.Max(_skyK, k * 0.8f);
            }
        }

        void Respawn(int i)
        {
            var (s, _, _) = _shards[i];
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0f, 70f);
            s.position = new Vector3(stageCenter.x + Mathf.Cos(a) * r, Island.Ground + Random.Range(45f, 95f), stageCenter.z + 6f + Mathf.Sin(a) * r);
            s.rotation = Random.rotation;
            s.gameObject.SetActive(true);
            _shards[i] = (s, new Vector3(Random.Range(-3f, 3f), Random.Range(-4f, 2f), Random.Range(-3f, 3f)), Random.insideUnitSphere * 220f);
        }

        // ───────── ヤチヨの分身（6人が並んで、同じふりで踊る） ─────────

        readonly List<(Transform root, Transform armL, Transform armR)> _clones = new List<(Transform, Transform, Transform)>();
        bool _clonesWere;

        void Clones(float k, float t)
        {
            if (k <= 0) { foreach (var c in _clones) if (c.root) c.root.gameObject.SetActive(false); _clonesWere = false; return; }
            if (_clones.Count == 0 && _crowd != null && _crowd.Ready)
            {
                var mats = new[] { HoloM(new Color(1f, 0.55f, 0.85f)), HoloM(new Color(0.45f, 0.9f, 1f)) };
                for (int i = 0; i < 6; i++)
                {
                    var root = _crowd.MakeFigure(DollLook.Yachiyo, 1.08f, mats[i % 2], out var al, out var ar);
                    root.SetParent(_wow, true);
                    _clones.Add((root, al, ar));
                }
            }
            float[] xs = { -7.5f, -5f, -2.5f, 2.5f, 5f, 7.5f };
            for (int i = 0; i < _clones.Count; i++)
            {
                var (root, al, ar) = _clones[i];
                if (!root) continue;
                root.gameObject.SetActive(true);
                // ヤチヨの体から、左右へすべり出る
                var home = spot + new Vector3(xs[i], 0, -Mathf.Abs(xs[i]) * 0.35f) + PerformerOffset;
                var p = Vector3.Lerp(performer ? performer.position : spot, home, Ease(k * 1.3f - i * 0.05f));
                root.position = p;
                root.rotation = performer ? performer.rotation : Quaternion.identity;
                if (al && yArmL) al.localRotation = yArmL.localRotation;
                if (ar && yArmR) ar.localRotation = yArmR.localRotation;
                if (!_clonesWere) for (int j = 0; j < 30; j++) Emit(_burst, spot + Vector3.up * Random.Range(0f, 2f), Random.insideUnitSphere * 5f, PenColors[i % 2 == 0 ? 1 : 2], 1f, 0.35f);
            }
            _clonesWere = true;
        }

        // ───────── 桜吹雪の嵐 ─────────

        ParticleSystem _petals;

        void Sakura(float k, float t)
        {
            if (_petals == null)
            {
                _petals = Particles("桜の嵐", 6000, 3f, 4.5f, 0.7f, 1.3f, 0.05f);
                _petals.transform.SetParent(_wow, false);
                var nz = _petals.noise; nz.enabled = true; nz.strength = 2f; nz.frequency = 0.25f;
                _petals.Play();
            }
            if (k <= 0) return;
            var c = new Vector3(stageCenter.x, Island.Ground, stageCenter.z + 6f);
            int n = Mathf.CeilToInt(80 * k);
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(18f, 48f);
                var p = c + new Vector3(Mathf.Cos(a) * r, Random.Range(0.5f, 28f), Mathf.Sin(a) * r);
                var tang = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * Random.Range(14f, 22f) + Vector3.up * Random.Range(1f, 4f) - new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3f;
                Emit(_petals, p, tang, Color.Lerp(new Color(1f, 0.45f, 0.7f), new Color(1f, 0.85f, 0.92f), Random.value * 0.6f) * 1.4f);
            }
        }

        // ───────── 時間が止まる（ヤチヨだけが動ける） ─────────

        float _freezeK;
        bool _frozen;
        float _frozenBeat;

        /// <summary>時間が止まっている間の、客席のふりの拍。</summary>
        public float FrozenBeat(float beat) => _frozen ? _frozenBeat : beat;

        void Freeze(float k)
        {
            _freezeK = k;
            bool now = k > 0.5f;
            if (now != _frozen)
            {
                _frozen = now;
                var s = Song;
                _frozenBeat = s != null ? s.Beat(_songT) : 0;
                foreach (var ps in new[] { _flames, _gerbs, _motes, _confetti, _burst, _streak, _petals, _bubbles })
                    if (ps) { if (now) ps.Pause(); else ps.Play(); }
                if (!now) { _flash = 1f; Cheer(0.8f); } // 動きだす瞬間
            }
            if (k > 0)
            {
                _skyTint = Color.Lerp(_skyTint, new Color(0.18f, 0.2f, 0.26f), k);
                _skyK = Mathf.Max(_skyK, k * 0.85f);
            }
        }

        // ───────── 光のリボン（客席をぐるりと巻いて、空までのぼる） ─────────

        Mesh _ribbonMesh;
        Renderer _ribbonR;

        void Ribbons(float k, float t, float beat)
        {
            if (k <= 0) { if (_ribbonR) _ribbonR.enabled = false; return; }
            if (_ribbonMesh == null)
            {
                var g = new GameObject("光のリボン");
                g.transform.SetParent(_wow, false);
                _ribbonMesh = new Mesh { name = "ribbons" }; _ribbonMesh.MarkDynamic();
                g.AddComponent<MeshFilter>().sharedMesh = _ribbonMesh;
                _ribbonR = g.AddComponent<MeshRenderer>();
                _ribbonR.sharedMaterial = penMat; _ribbonR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _ribbonR.enabled = true;
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var tr = new List<int>();
            var center = new Vector3(stageCenter.x, Island.Ground, (seatMin.y + seatMax.y) / 2f - 4f);
            const int R = 7, Seg = 90;
            for (int r = 0; r < R; r++)
            {
                var c1 = PenColors[(r * 2 + 1) % PenColors.Length]; var c2 = PenColors[(r * 2 + 2) % PenColors.Length];
                for (int i = 0; i <= Seg; i++)
                {
                    float s = i / (float)Seg;
                    float grow = Mathf.Clamp01(k * 1.6f - s * 0.6f); // 足もとからのびていく
                    float a = s * Mathf.PI * 4.5f + t * 0.45f + r * Mathf.PI * 2f / R;
                    float rad = 7f + s * 26f + Mathf.Sin(s * 9f + t + r) * 1.5f;
                    var p = center + new Vector3(Mathf.Cos(a) * rad, 0.8f + s * 38f * grow + Mathf.Sin(a * 2f + t) * 0.6f, Mathf.Sin(a) * rad * 0.8f);
                    var tan = new Vector3(-Mathf.Sin(a), 0.4f, Mathf.Cos(a) * 0.8f).normalized;
                    var side = Vector3.Cross(tan, Vector3.up).normalized * Mathf.Lerp(0.15f, 1.3f, Mathf.Sin(s * Mathf.PI)) * grow;
                    var col = Color.Lerp(c1, c2, s) * (0.7f + 0.3f * Mathf.Sin(s * 20f - beat * Mathf.PI)) * k * 0.55f;
                    v.Add(p - side); v.Add(p + side);
                    c.Add(col); c.Add(col);
                    uv.Add(new Vector2(0, 0.5f)); uv.Add(new Vector2(1, 0.5f));
                    n.Add(Vector3.up); n.Add(Vector3.up);
                    if (i > 0) { int b = v.Count - 4; tr.Add(b); tr.Add(b + 1); tr.Add(b + 3); tr.Add(b); tr.Add(b + 3); tr.Add(b + 2); }
                }
            }
            _ribbonMesh.Clear();
            _ribbonMesh.SetVertices(v); _ribbonMesh.SetColors(c); _ribbonMesh.SetUVs(0, uv); _ribbonMesh.SetNormals(n); _ribbonMesh.SetTriangles(tr, 0);
            _ribbonMesh.bounds = new Bounds(center, Vector3.one * 200f);
        }

        // ───────── 光の蓮（会場ごと、大きな花の中に入る） ─────────

        Transform _lotus;
        readonly List<(Transform pivot, Renderer r, float yaw, bool inner)> _petalsL = new List<(Transform, Renderer, float, bool)>();

        void Bloom(float k, float t, float beat, float onBeat)
        {
            if (k <= 0) { if (_lotus) _lotus.gameObject.SetActive(false); return; }
            if (_lotus == null)
            {
                _lotus = Group("光の蓮");
                var outer = HoloM(new Color(1f, 0.55f, 0.82f)); var inner = HoloM(new Color(1f, 0.82f, 0.92f));
                for (int ring = 0; ring < 2; ring++)
                {
                    int cnt = ring == 0 ? 12 : 10;
                    for (int i = 0; i < cnt; i++)
                    {
                        float yaw = (i + ring * 0.5f) * 360f / cnt;
                        var pv = new GameObject("花びらのつけね").transform;
                        pv.SetParent(_lotus, false);
                        float len = ring == 0 ? 30f : 22f;
                        var pt = Part(PrimitiveType.Sphere, pv, ring == 0 ? outer : inner, new Vector3(0, 0, len / 2f), new Vector3(len * 0.42f, 0.5f, len));
                        _petalsL.Add((pv, pt.GetComponent<Renderer>(), yaw, ring == 1));
                    }
                }
            }
            _lotus.gameObject.SetActive(true);
            var center = new Vector3(stageCenter.x, Island.Ground - 0.5f, (seatMin.y + seatMax.y) / 2f - 6f);
            _lotus.position = center;
            float e = Ease(k);
            foreach (var (pv, r, yaw, inner) in _petalsL)
            {
                float r0 = inner ? 20f : 30f;
                // 閉じたつぼみ（上を向く）→ 開いて、客席をかこむように少し外へひらく
                float open = Mathf.Lerp(-80f, inner ? -35f : -22f, e) + Mathf.Sin(beat * Mathf.PI * 0.25f + yaw) * 2f;
                pv.localPosition = Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, r0 * e);
                pv.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(open, 0, 0);
                pv.localScale = Vector3.one * Mathf.Max(0.01f, e);
                _mpb.SetColor(IdColor, (inner ? new Color(1f, 0.82f, 0.92f) : new Color(1f, 0.55f, 0.82f)) * (0.8f + 0.4f * onBeat));
                r.SetPropertyBlock(_mpb);
            }
            // 花のまんなかから、光の粒がのぼる
            for (int i = 0; i < 3; i++)
                if (Random.value < k) Emit(_motes, center + new Vector3(Random.Range(-14f, 14f), 0.6f, Random.Range(-10f, 10f)), Vector3.up * Random.Range(1.5f, 4f), new Color(1f, 0.85f, 0.6f), 6f, 0.4f);
        }

        // ───────── 空に浮かぶ歌詞 ─────────

        TextMesh _lyric;
        string _lastLine;

        void SkyLyrics()
        {
            var s = Song;
            string line = null; float prog = 0, a = 0;
            if (s != null) line = s.Lyric(_songT, out prog, out a);
            if (_lyric == null)
            {
                var g = new GameObject("空の歌詞");
                g.transform.SetParent(_wow, false);
                g.transform.SetPositionAndRotation(new Vector3(stageCenter.x, Island.Ground + 22f, stageCenter.z - 16f), Quaternion.Euler(0, 180f, 0));
                _lyric = g.AddComponent<TextMesh>();
                _lyric.font = Ts.Font; _lyric.fontSize = 64; _lyric.characterSize = 0.34f; _lyric.anchor = TextAnchor.MiddleCenter; _lyric.richText = true;
                g.GetComponent<MeshRenderer>().sharedMaterial = Ts.Font.material;
            }
            if (line != _lastLine && _lastLine != null && _burst != null)
            {
                // 歌いおわった行は、星になってくずれる
                for (int i = 0; i < 90; i++)
                    Emit(_burst, _lyric.transform.position + new Vector3(Random.Range(-14f, 14f), Random.Range(-1.2f, 1.2f), 0), new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 3f), Random.Range(-1f, 1f)), s != null && s.style == 1 ? new Color(1f, 0.85f, 0.55f) : PenColors[Random.Range(0, 3)], Random.Range(1.2f, 2.4f), 0.35f);
            }
            _lastLine = line;
            if (line == null || a <= 0) { _lyric.gameObject.SetActive(false); return; }
            _lyric.gameObject.SetActive(true);
            int n = Mathf.Clamp(Mathf.RoundToInt(line.Length * prog), 0, line.Length);
            string on = s.style == 1 ? "ffe2a0" : "ff8fd0";
            int al = Mathf.RoundToInt(a * 255), al2 = Mathf.RoundToInt(a * 110);
            _lyric.text = $"<color=#{on}{al:x2}>{line.Substring(0, n)}</color><color=#ffffff{al2:x2}>{line.Substring(n)}</color>";
            // 浮いている高さに合わせる
            _lyric.transform.position = new Vector3(stageCenter.x, Island.Ground + 22f + FloatHeight, stageCenter.z - 16f);
        }

        // ───────── 空に流れるコメント ─────────

        readonly List<(TextMesh tm, float speed)> _flow = new List<(TextMesh, float)>();
        readonly Stack<TextMesh> _flowPool = new Stack<TextMesh>();

        void SkyComment(Comment c)
        {
            if (!Running || _wow == null) return;
            if (_flow.Count > 26 && c.amount <= 0) return;
            var tm = _flowPool.Count > 0 ? _flowPool.Pop() : null;
            if (tm == null)
            {
                var g = new GameObject("空のコメント");
                g.transform.SetParent(_wow, false);
                g.transform.rotation = Quaternion.Euler(0, 180f, 0);
                tm = g.AddComponent<TextMesh>();
                tm.font = Ts.Font; tm.fontSize = 64; tm.anchor = TextAnchor.MiddleLeft;
                g.GetComponent<MeshRenderer>().sharedMaterial = Ts.Font.material;
            }
            tm.gameObject.SetActive(true);
            bool tip = c.amount > 0;
            tm.text = tip ? $"【{c.amount:N0}ふじゅ〜】{c.name}：{c.text}" : c.text;
            tm.characterSize = tip ? (c.mine ? 0.42f : 0.3f) : 0.2f;
            var col = tip ? TipColor(c.amount) : new Color(1f, 1f, 1f, 0.75f);
            tm.color = col;
            // 見ている人の右から左へ（ワールドでは -x → +x）
            tm.transform.position = new Vector3(stageCenter.x - 80f, Island.Ground + Random.Range(tip ? 26f : 18f, tip ? 40f : 56f), stageCenter.z - 48f);
            _flow.Add((tm, Random.Range(10f, 14f) * (tip ? 0.8f : 1f)));
        }

        void SkyComments(float dt)
        {
            for (int i = _flow.Count - 1; i >= 0; i--)
            {
                var (tm, sp) = _flow[i];
                tm.transform.position += Vector3.right * sp * dt;
                if (tm.transform.position.x > stageCenter.x + 95f) { tm.gameObject.SetActive(false); _flowPool.Push(tm); _flow.RemoveAt(i); }
            }
        }

        // ───────── 投げ銭でステージへ招待 ─────────

        float _onStageT;

        void BeamUp()
        {
            if (_player == null || !InVenue(_player.Position) || Ts.ShotMode && _onStageT > 0) return;
            _onStageT = 20f;
            var at = spot + new Vector3(2.6f, 0.15f, 0.8f);
            for (int i = 0; i < 80; i++) Emit(_burst, _player.Position + Vector3.up * Random.Range(0f, 2f), Vector3.up * Random.Range(4f, 10f) + Random.insideUnitSphere, PenColors[2], 1.2f, 0.4f);
            _player.Teleport(at, 0f);
            if (_cam != null) { _cam.yaw = 180f; _cam.pitch = 8f; _cam.distance = 4f; _cam.Snap(); }
            for (int i = 0; i < 80; i++) Emit(_burst, at + Vector3.up * Random.Range(0f, 2.5f), Random.insideUnitSphere * 3f, PenColors[1], 1.2f, 0.4f);
            Hud.Toast("ステージに招待されました！ ヤチヨのとなりから客席を見てみよう（20秒）");
        }

        void StageGuest(float dt)
        {
            if (_onStageT <= 0) return;
            _onStageT -= dt;
            bool songOver = _phase != Phase.Song;
            if (_onStageT <= 0 || (songOver && _onStageT < 18f))
            {
                _onStageT = 0;
                ToSeats();
                Hud.Toast("客席にもどりました");
            }
        }

        void LateUpdate()
        {
            if (Running) LateSky();
            CamLate();
        }
    }
}
