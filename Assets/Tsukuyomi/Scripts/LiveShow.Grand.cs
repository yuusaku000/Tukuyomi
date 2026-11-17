using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tsukuyomi
{
    /// <summary>
    /// ライブの大仕掛け（2026-11-18 ユーザーの指示「唸るような、すげえええってなる演出」）と、ライブ中の島の軽量化。
    ///   spiral：まわりの鳥居20基が拍で光り、ほどけて客席を囲む螺旋の階段になって空へ伸びる。ヤチヨが駆け上がり、てっぺんで花火
    ///   ascend：会場ごと天へ。海と島が足もとへ沈み、雲を突き抜けて雲海の上・月の目の前へ（曲の終わりにゆっくりもどる）
    ///   morph ：セットの変身。舞台のまわりに光の蓮が咲いて開き、うしろに巨大な扇が広がり、左右に桜の大木が海から伸びる
    ///   split ：海が割れる。会場の左右に光る水の壁が立ちあがり、壁一面に巨大なヤチヨが映る
    ///   mirror：鏡の回廊。足もとが黒い鏡になり、光る鳥居が四方へどこまでも並ぶ
    ///   軽量化：会場にいる間は、開演のときに撮った島の写真を遠くの背景に映し、本物の島の見た目は描かない（当たりはそのまま）
    /// </summary>
    public partial class LiveShow
    {
        static readonly string[] GrandFx = { "spiral", "ascend", "morph", "split", "mirror" };
        readonly HashSet<string> _grandUsed = new HashSet<string>();

        /// <summary>大仕掛けを、まだ出ていないものから優先して選ぶ（毎回のライブで見られるように）。</summary>
        string NextGrand()
        {
            var left = new List<string>();
            foreach (var g in GrandFx) if (!_grandUsed.Contains(g)) left.Add(g);
            if (left.Count == 0 || Random.value > 0.65f) return null;
            var f = left[Random.Range(0, left.Count)];
            _grandUsed.Add(f);
            return f;
        }

        void GrandTick(float dt, float t, float beat, float onBeat)
        {
            BackdropTick();
            Spiral(K("spiral"), t, beat, onBeat, dt);
            Ascend(K("ascend"), t, dt);
            Morph(K("morph"), t, beat, onBeat);
            Split(K("split"), t);
            Mirror(K("mirror"), t, beat, onBeat);
        }

        void GrandEnd()
        {
            foreach (var f in GrandFx) _k.Remove(f);
            Spiral(0, 0, 0, 0, 0); Ascend(0, 0, 0); Morph(0, 0, 0, 0); Split(0, 0); Mirror(0, 0, 0, 0);
            BackdropOff();
        }

        // ───────── 軽量化：島を写真の背景に ─────────

        readonly List<Renderer> _islandR = new List<Renderer>();
        Transform _bdQuad; Material _bdMat; RenderTexture _bdRt; Texture2D _bdTex;
        Vector3 _bdBase; bool _bdOn;
        const float BdDist = 300f, BdVFov = 46f, BdAspect = 3.4f;

        /// <summary>客席の上から北（島）を向いて、島だけを写す。</summary>
        Vector3 BdEye => new Vector3(stageCenter.x, Island.Ground + 6f, (seatMin.y + seatMax.y) / 2f);

        void BackdropTick()
        {
            bool want = Running && (PlayerInVenue || AutoCam) && !Ts.Flag("--no-backdrop"); // --no-backdrop：くらべる確認用
            if (want && !_bdOn) BackdropOn();
            else if (!want && _bdOn) BackdropOff();
            if (_bdOn && _bdMat != null)
            {
                // 島の明かりが拍で光る演出（island）は、写真の明るさで
                float pulse = 1f + K("island") * (0.6f * Mathf.Pow(1f - Mathf.Repeat(Song != null ? Song.Beat(_songT) : 0, 1f), 3f) - 0.15f);
                _bdMat.color = new Color(pulse, pulse, pulse, 1f);
            }
        }

        void BackdropOn()
        {
            var eye = BdEye;
            var vc = venueCenterXZ();
            // 島の見た目：会場から 80m より遠く、空や海のような大きすぎる物ではないもの（町の人は Crowd が自分で消すので触らない）
            _islandR.Clear();
            var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in all)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.magnitude > 1500f) continue;                          // 空・海
                if (r.transform.IsChildOf(transform) || r.GetComponentInParent<Crowd>() != null) continue;
                if (r.GetComponentInParent<Camera>() != null || r is ParticleSystemRenderer) continue;
                var c = b.center; c.y = 0;
                if (Vector3.Distance(c, vc) < 80f) continue;                     // 会場・光の橋
                _islandR.Add(r);
            }
            // 写す（島のほかは一時的に消して、すき間は透明に）
            if (_bdRt == null) { _bdRt = new RenderTexture(4096, 1204, 24, RenderTextureFormat.ARGB32) { name = "島の写真の撮影" }; }
            var hide = new List<Renderer>();
            var isl = new HashSet<Renderer>(_islandR);
            foreach (var r in all) if (r != null && r.enabled && r.gameObject.activeInHierarchy && !isl.Contains(r)) { r.enabled = false; hide.Add(r); }
            var go = new GameObject("島の写真のカメラ");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(Vector3.forward));
            cam.fieldOfView = BdVFov; cam.aspect = BdAspect; cam.nearClipPlane = 20f; cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.allowHDR = false; cam.targetTexture = _bdRt;
            var ad = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            ad.renderPostProcessing = false; ad.renderShadows = true;
            cam.Render();
            // 撮った写真を、ふつうの絵（Texture2D）に写す（レンダーテクスチャをそのまま貼ると映らなかった）
            var prev = RenderTexture.active; RenderTexture.active = _bdRt;
            if (_bdTex == null) _bdTex = new Texture2D(_bdRt.width, _bdRt.height, TextureFormat.RGBA32, false) { name = "島の写真", wrapMode = TextureWrapMode.Clamp };
            _bdTex.ReadPixels(new Rect(0, 0, _bdRt.width, _bdRt.height), 0, 0); _bdTex.Apply(false, false);
            RenderTexture.active = prev;
            if (Ts.ShotMode && Ts.Arg("--tour-dir") != null)
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(Ts.Arg("--tour-dir"), "backdrop.png"), _bdTex.EncodeToPNG()); // 確認用
            Destroy(go);
            foreach (var r in hide) if (r != null) r.enabled = true;
            // 本物の島の見た目を消す
            foreach (var r in _islandR) if (r != null) r.enabled = false;
            // 写真を、写したときと同じ向き・大きさで、遠くに立てる
            if (_bdQuad == null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "島の写真（背景）";
                _bdQuad = q.transform; _bdQuad.SetParent(transform, false);
                _bdMat = new Material(Shader.Find("UI/Default")) { renderQueue = 2990 };
                _bdMat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                WhiteQuad(q);
                var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = _bdMat; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; qr.receiveShadows = false;
            }
            float h = 2f * BdDist * Mathf.Tan(BdVFov * 0.5f * Mathf.Deg2Rad);
            _bdBase = eye + Vector3.forward * BdDist;
            _bdQuad.SetPositionAndRotation(_bdBase, Quaternion.LookRotation(Vector3.forward));
            _bdQuad.localScale = new Vector3(h * BdAspect, h, 1f);
            _bdMat.mainTexture = _bdTex;
            _bdQuad.gameObject.SetActive(true);
            _bdOn = true;
            Debug.Log($"[Live] 島を写真の背景にしました（描かない物 {_islandR.Count}）");
        }

        void BackdropOff()
        {
            if (!_bdOn) return;
            foreach (var r in _islandR) if (r != null) r.enabled = true;
            _islandR.Clear();
            if (_bdQuad) _bdQuad.gameObject.SetActive(false);
            _bdOn = false;
        }

        /// <summary>四角い板に白い頂点の色を入れる（UI/Default は頂点の色を掛けるので、ないと透明になって見えない）。</summary>
        static void WhiteQuad(GameObject q)
        {
            var mf = q.GetComponent<MeshFilter>();
            var m = Instantiate(mf.sharedMesh);
            var cols = new Color32[m.vertexCount];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            m.colors32 = cols;
            mf.sharedMesh = m;
        }

        Vector3 venueCenterXZ() => new Vector3(stageCenter.x, 0, (stageCenter.z + (seatMin.y + seatMax.y) / 2f) / 2f);

        // ───────── 鳥居の螺旋 ─────────

        readonly List<(Transform t, Vector3 lp, Quaternion lr)> _spT = new List<(Transform, Vector3, Quaternion)>();
        readonly List<Floaty> _spFloaty = new List<Floaty>();
        bool _spOn, _spBoom; float _spClimb;
        LineRenderer _spLine;

        void Spiral(float k, float t, float beat, float onBeat, float dt)
        {
            if (k <= 0)
            {
                if (_spOn)
                {
                    foreach (var (tr, lp, lr) in _spT) if (tr) { tr.localPosition = lp; tr.localRotation = lr; }
                    foreach (var f in _spFloaty) if (f) f.enabled = true;
                    if (_spLine) _spLine.enabled = false;
                    _spOn = false;
                }
                return;
            }
            if (!_spOn)
            {
                // まわりの鳥居の輪（会場の部品）を見つけて、今のかたちを覚える
                _spT.Clear(); _spFloaty.Clear();
                foreach (var f in FindObjectsByType<Floaty>(FindObjectsSortMode.None))
                {
                    if (!f.name.StartsWith("鳥居の輪")) continue;
                    _spFloaty.Add(f); f.enabled = false;
                    foreach (Transform c in f.transform) if (c.name == "鳥居") _spT.Add((c, c.localPosition, c.localRotation));
                }
                _spOn = true; _spBoom = false; _spClimb = 0;
            }
            float e = Ease(k);
            int n = _spT.Count;
            if (n == 0) return;
            var c0 = new Vector3(stageCenter.x, Island.Ground, (stageCenter.z + (seatMin.y + seatMax.y) / 2f) / 2f);
            Vector3 Helix(float u) // u：0 下 → 1 てっぺん
            {
                float a = u * Mathf.PI * 2f * 1.7f + t * 0.3f;
                float r = Mathf.Lerp(26f, 15f, u);
                return c0 + new Vector3(Mathf.Cos(a) * r, 2f + u * 60f, Mathf.Sin(a) * r);
            }
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var (tr, lp, lr) = _spT[i];
                if (!tr) continue;
                float u = i / (float)(n - 1);
                var p = Helix(u);
                var tan = (Helix(u + 0.01f) - p).normalized;
                var parent = tr.parent;
                var from = parent.TransformPoint(lp); var fromR = parent.rotation * lr;
                tr.position = Vector3.Lerp(from, p, e);
                tr.rotation = Quaternion.Slerp(fromR, Quaternion.LookRotation(tan, Vector3.up), e);
                tr.localScale = Vector3.one * (1f + 0.5f * e); // 少し大きく
                pts[i] = p + Vector3.up * 2.5f;
                // 拍で、下から順に金の光がかけ上がる
                float wave = Mathf.Repeat(beat * 0.5f - u * 2f, 1f);
                if (wave < 0.06f && Random.value < k) Emit(_burst, tr.position + Vector3.up * 4f, Vector3.up * 2f + Random.insideUnitSphere, Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(1f, 0.85f, 0.4f), u), 1.2f, 0.7f);
            }
            // 鳥居をつなぐ光の帯
            if (_spLine == null)
            {
                var g = Group("鳥居の螺旋の光");
                _spLine = g.gameObject.AddComponent<LineRenderer>();
                _spLine.material = GlowM(new Color(1f, 0.8f, 0.45f), 3f);
                _spLine.widthMultiplier = 0.35f; _spLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _spLine.enabled = e > 0.5f;
            _spLine.positionCount = n; _spLine.SetPositions(pts);
            // ヤチヨが螺旋を駆け上がる。てっぺんで花火
            if (k > 0.95f) _spClimb = Mathf.Min(1f, _spClimb + dt * 0.055f);
            if (_spClimb > 0)
            {
                var top = Helix(_spClimb) + Vector3.up * 0.4f;
                PerformerOffset += (top - spot) * Mathf.Clamp01(_spClimb * 8f);
                var dir = Helix(_spClimb + 0.01f) - top; dir.y = 0;
                if (dir.sqrMagnitude > 0.001f) PerformerYaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
                if (Random.value < 0.7f) Emit(_motes, top + Random.insideUnitSphere, Vector3.up, new Color(1f, 0.85f, 0.5f), 1.5f, 0.3f);
            }
            if (_spClimb >= 1f && !_spBoom) { _spBoom = true; Fireworks3(10); _flash = 1f; }
            _skyTint = Color.Lerp(_skyTint, new Color(0.3f, 0.08f, 0.1f), k * 0.5f);
            _skyK = Mathf.Max(_skyK, k * 0.5f);
        }

        // ───────── 会場ごと天へ ─────────

        Transform _seaT; float _seaY0 = float.NaN;
        Transform _cloudG; readonly List<(Transform t, Vector3 off)> _clouds = new List<(Transform, Vector3)>();
        Renderer _liftBeam;

        void Ascend(float k, float t, float dt)
        {
            if (_seaT == null) { var s = GameObject.Find("海"); if (s) { _seaT = s.transform; _seaY0 = _seaT.position.y; } }
            float e = Ease(k);
            float lift = 300f * e;
            if (_seaT != null && !float.IsNaN(_seaY0)) { var p = _seaT.position; p.y = _seaY0 - lift; _seaT.position = p; }
            if (_bdQuad != null && _bdOn) _bdQuad.position = _bdBase - Vector3.up * lift * 0.95f;
            if (k <= 0) { if (_cloudG) _cloudG.gameObject.SetActive(false); if (_liftBeam) _liftBeam.enabled = false; return; }
            if (_cloudG == null)
            {
                _cloudG = Group("雲海");
                var m = GlowM(new Color(0.55f, 0.6f, 0.85f), 1.1f); // 夜の雲（月あかりで、うっすら光る）
                for (int i = 0; i < 70; i++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(30f, 320f);
                    var off = new Vector3(Mathf.Cos(a) * r, Random.Range(-6f, 6f), Mathf.Sin(a) * r);
                    var c = Part(PrimitiveType.Sphere, _cloudG, m, Vector3.zero, new Vector3(Random.Range(30f, 70f), Random.Range(5f, 10f), Random.Range(30f, 70f))).transform;
                    _clouds.Add((c, off));
                }
            }
            _cloudG.gameObject.SetActive(true);
            // 雲は、頭の上 → 足もと（雲を突き抜けて、雲海の上に出る）
            float cy = Mathf.Lerp(Island.Ground + 55f, Island.Ground - 22f, Mathf.Clamp01(e * 1.4f));
            var vc = venueCenterXZ();
            foreach (var (c, off) in _clouds)
            {
                // 会場の真上と真下には雲を置かない（舞台と客席が見えるように）
                var o = off; var flat = new Vector3(o.x, 0, o.z);
                if (flat.magnitude < 34f) o = flat.normalized * 34f + Vector3.up * o.y;
                c.position = vc + o + Vector3.up * (cy + Mathf.Sin(t * 0.2f + o.x) * 1.5f);
            }
            // 会場を持ち上げる光の柱（下へ）
            if (_liftBeam == null) { _liftBeam = Cone(_wow, "会場を持ち上げる光"); _liftBeam.transform.rotation = Quaternion.LookRotation(Vector3.down); }
            _liftBeam.enabled = true;
            _liftBeam.transform.position = vc + Vector3.up * (Island.Ground - 1.5f);
            _liftBeam.transform.localScale = new Vector3(45f, 45f, Mathf.Max(1f, lift));
            _mpb.SetColor(IdColor, new Color(0.5f, 0.85f, 1f) * 0.45f * k);
            _liftBeam.SetPropertyBlock(_mpb);
            // 下へ流れる光（昇っている感じ）と、星の多い深い空
            var ps = Streaks();
            if (k < 0.97f)
                for (int i = 0; i < 10; i++)
                {
                    float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(20f, 90f);
                    Emit(ps, vc + new Vector3(Mathf.Cos(a) * r, Island.Ground + 60f, Mathf.Sin(a) * r), new Vector3(0, -80f, 0), new Color(0.8f, 0.9f, 1f), 1.4f, 0.35f);
                }
            _skyTint = Color.Lerp(_skyTint, new Color(0.04f, 0.04f, 0.16f), k * 0.75f);
            _skyK = Mathf.Max(_skyK, k * 0.75f);
        }

        // ───────── セットの変身 ─────────

        Transform _mpG;
        readonly List<Transform> _mpLotus = new List<Transform>(), _fanRibs = new List<Transform>(), _trees = new List<Transform>();
        readonly List<Transform> _treeCanopy = new List<Transform>();

        void Morph(float k, float t, float beat, float onBeat)
        {
            if (k <= 0) { if (_mpG) _mpG.gameObject.SetActive(false); return; }
            if (_mpG == null)
            {
                _mpG = Group("セットの変身");
                var pink = HoloM(new Color(0.55f, 0.22f, 0.45f)); var white = HoloM(new Color(0.35f, 0.45f, 0.6f)); // 明るすぎて画面が白くなったので、色をおさえる
                // 光の蓮（内 8・外 12 の花びら）
                for (int ring = 0; ring < 2; ring++)
                {
                    int cnt = ring == 0 ? 8 : 12;
                    for (int i = 0; i < cnt; i++)
                    {
                        var pv = new GameObject("花びら").transform; pv.SetParent(_mpG, false);
                        Part(PrimitiveType.Sphere, pv, ring == 0 ? white : pink, new Vector3(0, ring == 0 ? 2.6f : 3.6f, 0), new Vector3(ring == 0 ? 2.6f : 3.6f, ring == 0 ? 5.2f : 7.2f, 0.35f));
                        _mpLotus.Add(pv);
                    }
                }
                // 巨大な扇（舞台のうしろ）
                var gold = GlowM(new Color(1f, 0.8f, 0.4f), 3f); var red = GlowM(new Color(1f, 0.3f, 0.25f), 2.4f);
                for (int i = 0; i < 25; i++)
                {
                    var rib = new GameObject("扇の骨").transform; rib.SetParent(_mpG, false);
                    Part(PrimitiveType.Cube, rib, i % 2 == 0 ? gold : red, new Vector3(0, 21f, 0), new Vector3(i % 2 == 0 ? 0.6f : 3.2f, 42f, 0.3f));
                    _fanRibs.Add(rib);
                }
                // 桜の大木（左右の海から）
                var bark = GlowM(new Color(0.35f, 0.18f, 0.2f), 1.2f); var blossom = GlowM(new Color(1f, 0.45f, 0.7f), 1.3f); // 桜色（白く光りすぎないように）
                foreach (float side in new[] { -1f, 1f })
                {
                    var tree = new GameObject("桜の大木").transform; tree.SetParent(_mpG, false);
                    Part(PrimitiveType.Cylinder, tree, bark, new Vector3(0, 15f, 0), new Vector3(3.2f, 15f, 3.2f));
                    for (int j = 0; j < 9; j++)
                    {
                        var cp = Part(PrimitiveType.Sphere, tree, blossom, new Vector3(Random.Range(-12f, 12f), 30f + Random.Range(-4f, 8f), Random.Range(-10f, 10f)), Vector3.one * Random.Range(10f, 17f)).transform;
                        _treeCanopy.Add(cp);
                    }
                    tree.position = new Vector3(stageCenter.x + side * 48f, Island.Ground, stageCenter.z - 6f);
                    _trees.Add(tree);
                }
            }
            _mpG.gameObject.SetActive(true);
            float e = Ease(k);
            float eLotus = Ease(Mathf.Clamp01(k * 2.2f)), eFan = Ease(Mathf.Clamp01(k * 2f - 0.5f)), eTree = Ease(Mathf.Clamp01(k * 1.8f - 0.6f));
            // 蓮：海の下から上がって、外へ開く（拍でふわっと）
            int idx = 0;
            for (int ring = 0; ring < 2; ring++)
            {
                int cnt = ring == 0 ? 8 : 12;
                float rr = ring == 0 ? 11f : 15f;
                for (int i = 0; i < cnt; i++, idx++)
                {
                    float a = i * Mathf.PI * 2f / cnt + (ring == 0 ? 0.2f : 0f);
                    var pv = _mpLotus[idx];
                    pv.position = new Vector3(stageCenter.x + Mathf.Cos(a) * rr, Island.Ground - 14f * (1f - eLotus), stageCenter.z + Mathf.Sin(a) * rr);
                    float open = Mathf.Lerp(0f, ring == 0 ? 35f : 55f, eLotus) + onBeat * 6f * k;
                    pv.rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0) * Quaternion.Euler(0, 0, 0) * Quaternion.AngleAxis(open, Vector3.right);
                }
            }
            // 扇：閉じた形から、舞台のうしろに 160 度ひらく
            var pivot = new Vector3(stageCenter.x, Island.Ground + 1f, stageCenter.z - 26f);
            for (int i = 0; i < _fanRibs.Count; i++)
            {
                float u = i / (float)(_fanRibs.Count - 1) - 0.5f;
                float ang = u * 160f * eFan;
                _fanRibs[i].position = pivot;
                _fanRibs[i].rotation = Quaternion.Euler(0, 0, -ang);
                _fanRibs[i].localScale = Vector3.one * Mathf.Max(0.01f, eFan);
            }
            // 桜の大木：海から伸びて、花がひらく
            foreach (var tr in _trees) tr.localScale = new Vector3(1f, Mathf.Max(0.01f, eTree), 1f);
            foreach (var cp in _treeCanopy) cp.gameObject.SetActive(eTree > 0.6f);
            if (eTree > 0.6f && _petals != null)
                foreach (var tr in _trees) if (Random.value < k) Emit(_petals, tr.position + new Vector3(Random.Range(-14f, 14f), Random.Range(24f, 38f), Random.Range(-12f, 12f)), new Vector3(Random.Range(-2f, 2f) + (tr.position.x < stageCenter.x ? 3f : -3f), -1.2f, Random.Range(0f, 3f)), new Color(1f, 0.65f, 0.82f) * 1.3f, 6f, 0.35f);
            if (Random.value < 0.4f * k) Emit(_burst, pivot + Quaternion.Euler(0, 0, Random.Range(-80f, 80f) * eFan) * Vector3.up * Random.Range(10f, 42f), Random.insideUnitSphere * 2f, new Color(1f, 0.85f, 0.5f), 1.2f, 0.7f);
            _skyTint = Color.Lerp(_skyTint, new Color(0.25f, 0.08f, 0.2f), e * 0.4f);
            _skyK = Mathf.Max(_skyK, e * 0.4f);
        }

        // ───────── 海が割れる ─────────

        Transform _spG; readonly List<Transform> _walls = new List<Transform>(), _wallScreens = new List<Transform>();

        void Split(float k, float t)
        {
            if (k <= 0) { if (_spG) _spG.gameObject.SetActive(false); return; }
            if (_spG == null)
            {
                _spG = Group("割れた海");
                var water = HoloM(new Color(0.35f, 0.75f, 1f));
                // 大画面のヤチヨの映像（LiveRT）
                Texture live = null;
                foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.targetTexture != null && c.targetTexture.name.Contains("LiveRT")) live = c.targetTexture;
                var scr = new Material(Shader.Find("UI/Default")) { mainTexture = live, renderQueue = 3100 };
                scr.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                foreach (float side in new[] { -1f, 1f })
                {
                    var w = Part(PrimitiveType.Cube, _spG, water, Vector3.zero, new Vector3(6f, 70f, 420f)).transform;
                    _walls.Add(w);
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                    q.name = "水の壁のヤチヨ"; q.transform.SetParent(_spG, false); WhiteQuad(q);
                    var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = scr; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _wallScreens.Add(q.transform);
                }
            }
            _spG.gameObject.SetActive(true);
            float e = Ease(k);
            var vc = venueCenterXZ();
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float x = vc.x + side * Mathf.Lerp(12f, 52f, e);   // 海がまんなかから左右へ割れていく
                float h = 70f * e;
                _walls[i].position = new Vector3(x, Island.Ground - 35f + h / 2f + Mathf.Sin(t * 1.3f + i) * 0.6f, vc.z);
                _walls[i].localScale = new Vector3(6f, Mathf.Max(0.1f, h), 420f);
                var q = _wallScreens[i];
                q.position = new Vector3(x - side * 3.2f, Island.Ground + h * 0.45f, vc.z - 8f);
                q.rotation = Quaternion.LookRotation(new Vector3(side, 0, 0)); // 内がわ（会場）を向く
                q.localScale = new Vector3(96f, 54f, 1f) * Mathf.Max(0.01f, Ease(Mathf.Clamp01(k * 1.5f - 0.5f)));
                // 壁のてっぺんのしぶき
                if (Random.value < k) Emit(_flames, new Vector3(x, Island.Ground - 35f + h, vc.z + Random.Range(-180f, 180f)), new Vector3(-side * Random.Range(1f, 4f), Random.Range(2f, 6f), 0), new Color(0.6f, 0.9f, 1f), 1.6f, 1.2f);
            }
            _skyTint = Color.Lerp(_skyTint, new Color(0.02f, 0.18f, 0.3f), k * 0.6f);
            _skyK = Mathf.Max(_skyK, k * 0.6f);
        }

        // ───────── 鏡の回廊 ─────────

        Transform _mrG; Renderer _mrFloor; readonly List<Renderer> _mrTorii = new List<Renderer>();
        readonly List<(Transform t, float dist, int dir)> _mrRows = new List<(Transform, float, int)>();

        void Mirror(float k, float t, float beat, float onBeat)
        {
            if (k <= 0) { if (_mrG) _mrG.gameObject.SetActive(false); return; }
            if (_mrG == null)
            {
                _mrG = Group("鏡の回廊");
                var floorM = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                floorM.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.04f)); floorM.SetFloat("_Smoothness", 0.97f); floorM.SetFloat("_Metallic", 0.6f);
                _mrFloor = Part(PrimitiveType.Cube, _mrG, floorM, Vector3.zero, new Vector3(900f, 0.2f, 900f)).GetComponent<Renderer>();
                var m = GlowM(new Color(1f, 0.35f, 0.3f), 3f);
                // 四方へ 16 基ずつ（だんだん遠く）
                for (int dir = 0; dir < 4; dir++)
                    for (int i = 0; i < 16; i++)
                    {
                        var tr = Torii(_mrG, 11f, 13f, m);
                        _mrRows.Add((tr, 34f + i * 18f, dir));
                        foreach (var r in tr.GetComponentsInChildren<Renderer>()) _mrTorii.Add(r);
                    }
            }
            _mrG.gameObject.SetActive(true);
            float e = Ease(k);
            var vc = venueCenterXZ();
            // 鏡の床は、海の上にせり上がってくる（会場の床より少し下）
            _mrFloor.transform.position = vc + Vector3.up * (Island.Ground - 0.35f - 30f * (1f - e));
            for (int i = 0; i < _mrRows.Count; i++)
            {
                var (tr, dist, dir) = _mrRows[i];
                var fwd = Quaternion.Euler(0, dir * 90f, 0) * Vector3.forward;
                // 奥から手前へ、順に立ちあがる。拍で少し前へ流れる（どこまでも続いて見える）
                float flow = Mathf.Repeat(beat * 0.25f, 1f) * 18f;
                float d = dist - flow;
                float rise = Ease(Mathf.Clamp01(k * 2f - (dist - 34f) / 300f));
                tr.position = vc + fwd * d + Vector3.up * (Island.Ground - 0.3f - 14f * (1f - rise));
                tr.rotation = Quaternion.LookRotation(fwd);
            }
            foreach (var r in _mrTorii)
            {
                _mpb.SetColor(IdColor, Color.Lerp(new Color(1f, 0.35f, 0.3f), new Color(1f, 0.85f, 0.45f), onBeat) * (0.6f + 0.8f * onBeat));
                r.SetPropertyBlock(_mpb);
            }
            // まわりは暗く、遠くは霧に消える（どこまでも続くように見える）
            _skyTint = Color.Lerp(_skyTint, new Color(0.01f, 0.01f, 0.03f), k * 0.9f);
            _skyK = Mathf.Max(_skyK, k * 0.9f);
            _fogClose = Mathf.Max(_fogClose, k * 0.6f);
        }
    }
}
