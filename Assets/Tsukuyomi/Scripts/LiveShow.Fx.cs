using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tsukuyomi
{
    /// <summary>ライブの演出：ムービングライト・レーザー・炎と火花・花火・光の輪・客席とペンライト・おまかせカメラ。</summary>
    public partial class LiveShow
    {
        // ───── 色 ─────
        public static readonly Color[] PenColors =
        {
            new Color(1f, 0.97f, 0.92f), new Color(1f, 0.38f, 0.72f), new Color(0.3f, 0.88f, 1f), new Color(0.66f, 0.4f, 1f),
            new Color(1f, 0.86f, 0.3f), new Color(0.4f, 1f, 0.55f), new Color(1f, 0.28f, 0.28f), new Color(0.3f, 0.45f, 1f),
        };
        public static readonly string[] PenNames = { "白", "ピンク", "水色", "紫", "黄", "緑", "赤", "青" };
        public static int PenColor = 1;

        public static void SetPen(int i)
        {
            PenColor = Mathf.Clamp(i, 0, PenColors.Length - 1);
            PlayerPrefs.SetInt("tsukuyomi_pen", PenColor); PlayerPrefs.Save();
        }

        /// <summary>いまの区切りの色（客席のペンライトの多くと、光の色）。</summary>
        int ThemeIndex()
        {
            var sec = Sec; var s = Song;
            if (_phase == Phase.MC || _phase == Phase.Opening) return 2;
            if (_phase == Phase.Ending) return 1;
            if (sec == null) return 0;
            if (s.style == 1)
                return sec.last ? 4 : sec.name == "chorus" ? 1 : sec.name == "pre" ? 7 : sec.name == "bridge" ? 3 : 0;
            return sec.name switch { "verse" => 2, "pre" => 3, "chorus" => 1, "drop" => (s.style == 2 ? 5 : 1), "bridge" => 7, "inter" => 2, _ => 0 };
        }

        Color Theme => PenColors[ThemeIndex()];

        // ───── 部品 ─────
        ParticleSystem _flames, _gerbs, _motes, _confetti, _burst;
        Mesh _laserMesh, _penMesh, _armMesh, _myPenMesh;
        readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
        readonly List<Color> _c = new List<Color>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int> _t = new List<int>();
        MaterialPropertyBlock _mpb;
        static readonly int IdColor = Shader.PropertyToID("_Color");
        Quaternion[] _headRot;
        Vector3[] _flamePos;
        GameObject _myPen;
        float _flash;

        // 客席の人
        class Fan { public Transform root; public float scale, phase, yaw, baseY; public int own; public bool follow; public Vector2 armUv, handUv; }
        readonly List<Fan> _fans = new List<Fan>();
        Crowd _crowd;
        LiveSec _lastSec;
        int _lastBar = -1;
        bool _fansMade;

        void FxStart()
        {
            _mpb = new MaterialPropertyBlock();
            _laserMesh = NewMesh(lasers, "live_lasers");
            _penMesh = NewMesh(pens, "live_pens");
            _armMesh = NewMesh(arms, "live_arms");
            if (heads != null)
            {
                _headRot = new Quaternion[heads.Length];
                for (int i = 0; i < heads.Length; i++) _headRot[i] = heads[i] ? heads[i].rotation : Quaternion.identity;
            }
            // 炎は舞台の前のふち、火花の噴水はうしろのふち（六角の舞台：角は左右、平らな辺が前とうしろ）
            _flamePos = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float x = -7.2f + i * 2.88f;
                _flamePos[i] = new Vector3(stageCenter.x + x, stageCenter.y + 2.25f, stageCenter.z + HexEdge(x) - 0.6f);
            }
            _gerbPosW = new Vector3[4];
            float[] gx = { -6.5f, -2.4f, 2.4f, 6.5f };
            for (int i = 0; i < 4; i++) _gerbPosW[i] = new Vector3(stageCenter.x + gx[i], stageCenter.y + 2.25f, stageCenter.z - HexEdge(gx[i]) + 0.7f);
            if (sparkMat != null && rig != null)
            {
                _flames = Particles("炎の柱", 1500, 0.5f, 0.9f, 0.9f, 1.6f, -0.3f);
                _gerbs = Particles("火花の噴水", 2500, 1.2f, 1.8f, 0.12f, 0.22f, 0.9f);
                _motes = Particles("光の粒", 600, 7f, 10f, 0.25f, 0.45f, -0.02f);
                _confetti = Particles("光の紙ふぶき", 1500, 5f, 8f, 0.18f, 0.3f, 0.12f);
                _burst = Particles("きらめき", 800, 0.8f, 1.6f, 0.15f, 0.3f, 0.3f);
                var nz = _confetti.noise; nz.enabled = true; nz.strength = 1.2f; nz.frequency = 0.35f;
                var nz2 = _motes.noise; nz2.enabled = true; nz2.strength = 0.5f; nz2.frequency = 0.2f;
            }
            _crowd = FindFirstObjectByType<Crowd>();
            if (cheer && cheer.clip == null)
            {
                var c = Resources.Load<AudioClip>("Sounds/crowd_ambience");
                if (c) cheer.clip = Synth.Seamless(c, "live_cheer");
            }
        }

        Vector3[] _gerbPosW;

        /// <summary>六角の舞台（半径 9.43）の、x の所の前のふちまでの距離。</summary>
        static float HexEdge(float x)
        {
            const float R = 9.43f, h = 8.16f;
            float ax = Mathf.Abs(x);
            return ax <= R / 2 ? h : Mathf.Max(0, h * (R - ax) / (R / 2));
        }

        static Mesh NewMesh(MeshFilter mf, string name)
        {
            if (mf == null) return null;
            var m = new Mesh { name = name };
            m.MarkDynamic();
            mf.sharedMesh = m;
            return m;
        }

        ParticleSystem Particles(string name, int max, float life0, float life1, float size0, float size1, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
            main.startSpeed = 0;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var em = ps.emission; em.rateOverTime = 0;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = sparkMat;
            r.maxParticleSize = 0.2f;
            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float life = -1, float size = -1)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams { position = pos, velocity = vel, startColor = c, applyShapeToPosition = false };
            if (life > 0) ep.startLifetime = life;
            if (size > 0) ep.startSize = size;
            ps.Emit(ep, 1);
        }

        void FxBegin()
        {
            EnsureFans(false);
            foreach (var f in _fans) if (f.root) f.root.gameObject.SetActive(true);
            if (arms) arms.gameObject.SetActive(true);
            if (pens) pens.gameObject.SetActive(true);
            foreach (var ps in new[] { _flames, _gerbs, _motes, _confetti, _burst }) if (ps) ps.Play();
            _lastSec = null; _lastBar = -1;
        }

        void FxEnd()
        {
            foreach (var f in _fans) if (f.root) f.root.gameObject.SetActive(false);
            foreach (var l in spots ?? new Light[0]) if (l) l.enabled = false;
            if (wash) wash.enabled = false;
            if (_myPen) _myPen.SetActive(false);
            // 光の輪はふだんの色にもどす
            foreach (var arr in new[] { ledRing, ledFloor }) if (arr != null) foreach (var r in arr) if (r) r.SetPropertyBlock(null);
            if (moonDisc) moonDisc.SetPropertyBlock(null);
        }

        // ───────── 客席の人（町の人と同じ作り方。はじめのライブのときに作る） ─────────

        public void EnsureFans(bool now)
        {
            if (_fansMade || _crowd == null || !_crowd.Ready) return;
            _fansMade = true;
            if (now) { MakeFans(int.MaxValue); return; }
            StartCoroutine(MakeFansSlowly());
        }

        System.Collections.IEnumerator MakeFansSlowly()
        {
            while (MakeFans(8)) yield return null;
        }

        List<Vector3> _seats;

        bool MakeFans(int count)
        {
            if (_seats == null)
            {
                _seats = new List<Vector3>();
                var rng = new System.Random(4242);
                float R01() => (float)rng.NextDouble();
                var plaza = new Vector2(stageCenter.x, stageCenter.z - 8.7f);
                for (float z = seatMin.y; z <= seatMax.y; z += 1.25f)
                    for (float x = seatMin.x; x <= seatMax.x; x += 1.2f)
                    {
                        var p = new Vector2(x + (R01() - 0.5f) * 0.7f, z + (R01() - 0.5f) * 0.6f);
                        if (Mathf.Abs(p.x) < 1.7f) continue;                                         // まんなかの通路
                        if ((p - plaza).magnitude > 40.5f) continue;                                 // 広場のふち
                        if (Mathf.Abs(Mathf.Abs(p.x) - 13f) < 1.6f && p.y < -158f) continue;          // 前のやぐら
                        if (Mathf.Abs(Mathf.Abs(p.x) - 8.5f) < 1.6f && p.y < -158.6f) continue;       // スピーカー
                        if ((new Vector2(Mathf.Abs(p.x), p.y) - new Vector2(16.4f, -152.9f)).magnitude < 2.4f) continue; // 結晶
                        float front = Mathf.InverseLerp(seatMax.y, seatMin.y, p.y);
                        float want = Mathf.Lerp(0.3f, 0.9f, front) * (Mathf.Abs(p.x) > 14f ? 0.6f : 1f);
                        if (R01() > want) continue;
                        _seats.Add(new Vector3(p.x, Island.Ground, p.y));
                        if (_seats.Count >= 120) break;
                    }
                _seatI = 0;
            }
            int made = 0;
            while (_seatI < _seats.Count && made < count)
            {
                var p = _seats[_seatI++];
                var look = spot - p; look.y = 0;
                float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg + Random.Range(-8f, 8f);
                var root = _crowd.MakeFan(p, yaw, out float scale, out var sleeve, out var skin);
                if (root == null) continue;
                var f = new Fan
                {
                    root = root, scale = scale, yaw = yaw, baseY = root.position.y, phase = Random.Range(-0.12f, 0.12f),
                    follow = Random.value < 0.72f, own = new[] { 1, 2, 3, 0, 4 }[Random.Range(0, 5)],
                    armUv = _crowd.Uv(sleeve), handUv = _crowd.Uv(skin),
                };
                root.gameObject.SetActive(Running);
                _fans.Add(f);
                made++;
            }
            _crowd.RefreshColors();
            if (arms && _crowd.Mat) arms.GetComponent<MeshRenderer>().sharedMaterial = _crowd.Mat;
            if (_seatI >= _seats.Count) Debug.Log($"[Live] fans {_fans.Count}");
            return _seatI < _seats.Count;
        }

        int _seatI;

        // ───────── 毎フレームの演出 ─────────

        void FxTick(float dt)
        {
            var s = Song; var sec = Sec;
            float beat = s != null ? s.Beat(_songT) : _pt * 1.5f;
            int energy = _phase == Phase.Opening ? 0 : _phase == Phase.MC ? 1 : _phase == Phase.Ending ? 2 : sec != null ? sec.energy : 0;
            bool ballad = s != null && s.style == 1;
            bool chorus = HypeSec(sec);
            bool last = sec != null && sec.last;
            var theme = Theme;
            float onBeat = Mathf.Pow(1f - Mathf.Repeat(beat, 1f), 3f); // 拍の頭で光る
            float t = Time.time;
            _flash = Mathf.MoveTowards(_flash, 0, dt * 2.5f);

            // 区切りの頭：炎・花火・歓声・ヤチヨの1回転（音の時計はとびとびなので、変わったときに1回だけ）
            if (sec != _lastSec)
            {
                _lastSec = sec;
                if (sec != null && _songT - sec.t0 < 0.5f)
                {
                    if (chorus) { Flames(ballad ? 0.6f : 1f); _flash = 1f; Cheer(0.5f); _spinT = 0; if (last || !ballad) Fireworks3(last ? 5 : 2); }
                    if (sec.name == "outro" && ballad) Fireworks3(4);
                }
            }
            // サビは2小節ごとに炎（ポップの曲だけ）
            if (s != null && sec != null)
            {
                int bar = Mathf.FloorToInt((_songT - s.lead) / s.BarLen);
                if (bar != _lastBar)
                {
                    _lastBar = bar;
                    if (!ballad && chorus && (bar - sec.bar0) % 2 == 1) Flames(0.55f);
                }
            }

            // ムービングライト
            if (heads != null)
                for (int i = 0; i < heads.Length; i++)
                {
                    var h = heads[i]; if (!h) continue;
                    var target = HeadTarget(i, beat, energy, ballad, chorus, t);
                    var want = Quaternion.LookRotation(target - h.position, Vector3.up);
                    float speed = energy >= 3 ? 5f : energy == 2 ? 3f : 1.4f;
                    _headRot[i] = Quaternion.Slerp(_headRot[i], want, 1 - Mathf.Exp(-speed * dt));
                    h.rotation = _headRot[i];
                    if (beams != null && i < beams.Length && beams[i])
                    {
                        Color c = (i % 2 == 0 || ballad) ? theme : (chorus ? PenColors[2] : PenColors[0]);
                        if (ballad && i % 3 == 1) c = PenColors[0];
                        float k = _phase == Phase.Opening ? 0.35f : energy >= 3 ? 0.55f + 0.45f * onBeat : energy == 2 ? 0.5f : 0.35f;
                        if (chorus && !ballad && ((int)beat + i) % 4 == 0) k = 1f; // 順番にまたたく
                        k += _flash * 0.6f;
                        k *= (1f - _aloneK) * (1f - 0.7f * _freezeK); // ふたりきり・時間が止まっている間は消える
                        _mpb.SetColor(IdColor, c * k * 0.55f);
                        beams[i].SetPropertyBlock(_mpb);
                    }
                }
            if (entranceBeam)
            {
                float a = _phase == Phase.Song && _si == 0 ? Mathf.Clamp01(1f - Mathf.Abs(_songT - EntranceLandT) / 1.6f) : _phase == Phase.Ending ? Mathf.Clamp01(1f - Mathf.Abs(_pt - (EndLen - 2f)) / 2f) : 0;
                entranceBeam.enabled = a > 0.01f;
                _mpb.SetColor(IdColor, new Color(0.8f, 0.9f, 1f) * a * 1.4f);
                entranceBeam.SetPropertyBlock(_mpb);
                if (_phase == Phase.Song && _si == 0 && EntranceSparkle(_songT) && Random.value < 0.6f)
                    Emit(_burst, performer.position + new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 3f), Random.Range(-1f, 1f)), Vector3.up * Random.Range(1f, 4f), Color.Lerp(PenColors[2], PenColors[1], Random.value));
            }

            // 光の輪（舞台のうしろの大きな輪と、床の輪）
            Ring(ledRing, beat, theme, energy, ballad, 0);
            Ring(ledFloor, beat, theme, energy, ballad, 1);
            if (moonDisc)
            {
                float m = ballad ? 0.35f + (chorus ? 0.5f : 0.2f) + (last ? 0.4f : 0) : _phase == Phase.MC ? 0.3f : 0.12f;
                _mpb.SetColor(IdColor, new Color(1f, 0.88f, 0.62f) * m);
                moonDisc.SetPropertyBlock(_mpb);
            }

            // 本物の光（ヤチヨを照らす）
            if (spots != null)
                foreach (var l in spots)
                {
                    if (!l) continue;
                    l.enabled = _phase != Phase.Opening;
                    l.transform.rotation = Quaternion.LookRotation(spot + Vector3.up * 1.2f - l.transform.position);
                    l.intensity = (ballad ? 7f : 9f) * (1 + _flash * 0.5f);
                }
            if (wash)
            {
                wash.enabled = true;
                wash.color = Color.Lerp(theme, Color.white, 0.2f);
                wash.intensity = 2f + (energy >= 3 && !ballad ? onBeat * 3f : 0.8f) + _flash * 3f;
            }

            // 火花の噴水（最後のサビと、バラードのサビ）
            if (_gerbs && _gerbPosW != null && (last || (ballad && chorus) || (_phase == Phase.Ending && _pt < 8f)))
                for (int i = 0; i < _gerbPosW.Length; i++)
                    for (int k = 0; k < 3; k++)
                        Emit(_gerbs, _gerbPosW[i], new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(5f, 8f), Random.Range(-0.6f, 0.6f)), Color.Lerp(new Color(1f, 0.85f, 0.5f), Color.white, Random.value));
            // バラードの光の粒
            if (_motes && (ballad || _phase == Phase.MC) && Random.value < (ballad && chorus ? 0.9f : 0.45f))
                Emit(_motes, new Vector3(Random.Range(seatMin.x, seatMax.x), Island.Ground + Random.Range(0.5f, 3f), Random.Range(seatMin.y - 6f, seatMax.y)), new Vector3(0, Random.Range(0.4f, 0.9f), 0),
                     Color.Lerp(new Color(1f, 0.85f, 0.55f), PenColors[2], Random.value * 0.6f));
            // おわりの紙ふぶき
            if (_confetti && _phase == Phase.Ending && _pt < 9f)
                for (int k = 0; k < 6; k++)
                    Emit(_confetti, new Vector3(Random.Range(seatMin.x, seatMax.x), 16f, Random.Range(seatMin.y - 4f, seatMax.y)), new Vector3(0, -1.5f, 0), PenColors[Random.Range(0, PenColors.Length)]);

            Lasers(beat, energy, ballad, chorus, last, t);
            FansTick(beat, energy, ballad, chorus);
            CloseCamTick(t);
        }

        Vector3 HeadTarget(int i, float beat, int energy, bool ballad, bool chorus, float t)
        {
            var aud = new Vector3(stageCenter.x, Island.Ground, stageCenter.z + 20f);
            float side = (i % 2 == 0 ? -1 : 1);
            if (_phase == Phase.Opening)
                return aud + new Vector3(Mathf.Sin(t * 0.5f + i) * 16f, 0, Mathf.Cos(t * 0.37f + i * 1.3f) * 9f);
            if (_phase == Phase.MC || _phase == Phase.Ending)
                return i < 4 ? spot + Vector3.up * 1.2f : aud + new Vector3(Mathf.Sin(t * 0.3f + i) * 14f, 0, Mathf.Cos(t * 0.25f + i) * 8f);
            if (ballad)
            {
                // ゆっくり空へ。サビは客席をなでる
                if (chorus && i % 2 == 0) return aud + new Vector3(Mathf.Sin(t * 0.25f + i * 0.8f) * 16f, 0, Mathf.Cos(t * 0.2f + i) * 7f);
                return heads[i].position + new Vector3(Mathf.Sin(t * 0.15f + i * 0.7f) * 18f, 40f, 10f + Mathf.Cos(t * 0.12f + i) * 12f);
            }
            if (chorus)
            {
                // 交差しながら拍に合わせてゆれる
                float sw = Mathf.Sin(beat * Mathf.PI * 0.5f);
                if (i % 3 == 0) return spot + Vector3.up * 1.2f + new Vector3(sw * 0.5f, 0, 0);
                return heads[i].position + new Vector3(side * sw * 22f, 30f + Mathf.Cos(beat * Mathf.PI * 0.25f) * 10f, 24f);
            }
            if (energy >= 2)
                return aud + new Vector3(Mathf.Sin(beat * Mathf.PI * 0.25f + i * 0.9f) * 17f, 0, Mathf.Cos(beat * Mathf.PI * 0.125f + i) * 9f);
            return i % 2 == 0 ? spot + Vector3.up * 1.4f : heads[i].position + new Vector3(Mathf.Sin(t * 0.3f + i) * 10f, 35f, 18f);
        }

        void Ring(Renderer[] ring, float beat, Color theme, int energy, bool ballad, int kind)
        {
            if (ring == null || ring.Length == 0) return;
            int n = ring.Length;
            float run = beat * (energy >= 3 ? 2f : 1f) * (kind == 0 ? 1 : -1);
            for (int i = 0; i < n; i++)
            {
                if (!ring[i]) continue;
                float u = i / (float)n;
                float chase = Mathf.Pow(0.5f + 0.5f * Mathf.Cos((u * 4f - run * 0.25f) * Mathf.PI * 2f), 4f);
                Color c = theme;
                if (energy >= 3 && !ballad) c = Color.Lerp(theme, PenColors[(i / 2) % 2 == 0 ? 2 : 1], 0.5f);
                float k = ballad ? 0.35f + 0.25f * Mathf.Sin(u * Mathf.PI * 2 + beat * 0.4f) : 0.25f + 0.9f * chase;
                if (_phase == Phase.Opening) k = 0.2f + 0.2f * chase;
                k += _flash * 0.8f;
                _mpb.SetColor(IdColor, c * k);
                ring[i].SetPropertyBlock(_mpb);
            }
        }

        // ───────── レーザー（1つの形にまとめて毎フレーム作りなおす） ─────────

        void Lasers(float beat, int energy, bool ballad, bool chorus, bool last, float t)
        {
            if (_laserMesh == null) return;
            _v.Clear(); _c.Clear(); _uv.Clear(); _t.Clear(); _n.Clear();
            bool sideFans = !ballad && (chorus || (Sec != null && Sec.name == "inter")) || (_phase == Phase.Ending && _pt < 10f);
            bool tunnel = !ballad && Sec != null && (Sec.name == "pre" || last);
            bool sheet = ballad && last;
            var green = new Color(0.35f, 1f, 0.45f);
            if (laserFrom != null && laserFrom.Length >= 3)
            {
                if (sideFans)
                    for (int e = 0; e < 2; e++)
                    {
                        var o = laserFrom[e];
                        float side = e == 0 ? -1 : 1;
                        float tilt = 6f + Mathf.Sin(beat * Mathf.PI * 0.5f) * 9f;
                        for (int k = 0; k < 8; k++)
                        {
                            float yaw = side * 12f - side * k * 8f + Mathf.Sin(beat * Mathf.PI * 0.25f) * 10f;
                            var dir = Quaternion.Euler(-tilt - k * 0.5f, yaw, 0) * Vector3.forward;
                            Beam(o, dir, 70f, (k % 2 == 0 ? green : Theme) * LaserDim, 0.05f);
                        }
                    }
                if (tunnel)
                {
                    var o = laserFrom[2];
                    var axis = (new Vector3(stageCenter.x, 9f, stageCenter.z + 40f) - o).normalized;
                    var rot = Quaternion.LookRotation(axis);
                    for (int k = 0; k < 14; k++)
                    {
                        float a = k * Mathf.PI * 2f / 14f + t * 0.9f;
                        var dir = rot * new Vector3(Mathf.Cos(a) * 0.32f, Mathf.Sin(a) * 0.22f, 1f).normalized;
                        Beam(o, dir, 70f, (k % 2 == 0 ? PenColors[2] : PenColors[3]) * LaserDim, 0.045f);
                    }
                }
                if (sheet)
                {
                    var o = laserFrom[2];
                    for (int k = 0; k < 18; k++)
                    {
                        float yaw = -40f + k * 80f / 17f;
                        var dir = Quaternion.Euler(-10f - Mathf.Sin(t * 0.4f) * 4f, yaw, 0) * Vector3.forward;
                        Beam(o, dir, 80f, new Color(1f, 0.8f, 0.45f), 0.04f);
                    }
                }
            }
            _laserMesh.Clear();
            if (_v.Count > 0)
            {
                _laserMesh.SetVertices(_v); _laserMesh.SetColors(_c); _laserMesh.SetUVs(0, _uv); _laserMesh.SetNormals(_n);
                _laserMesh.SetTriangles(_t, 0);
                _laserMesh.bounds = new Bounds(stageCenter, Vector3.one * 300f);
            }
        }

        /// <summary>光る棒（十字にくみあわせた2枚の板）を足す。</summary>
        void Beam(Vector3 o, Vector3 dir, float len, Color c, float w)
        {
            var e = o + dir * len;
            var a = Vector3.Cross(dir, Vector3.up); if (a.sqrMagnitude < 0.001f) a = Vector3.right;
            a = a.normalized * w;
            var b = Vector3.Cross(dir, a).normalized * w;
            Quad(o, e, a, c); Quad(o, e, b, c);
        }

        void Quad(Vector3 o, Vector3 e, Vector3 a, Color c)
        {
            int k = _v.Count;
            _v.Add(o - a); _v.Add(o + a); _v.Add(e + a); _v.Add(e - a);
            _uv.Add(new Vector2(0, 0)); _uv.Add(new Vector2(1, 0)); _uv.Add(new Vector2(1, 0.98f)); _uv.Add(new Vector2(0, 0.98f));
            for (int i = 0; i < 4; i++) { _c.Add(c); _n.Add(Vector3.up); }
            _t.Add(k); _t.Add(k + 1); _t.Add(k + 2); _t.Add(k); _t.Add(k + 2); _t.Add(k + 3);
        }

        // ───────── 客席：腕とペンライト ─────────

        /// <summary>ペンライトをふる向き（腕の向き。人の向きから見て）。</summary>
        static Vector3 WaveDir(float beat, int energy, bool ballad, bool chorus, float phase, Phase ph)
        {
            float b = beat + phase;
            if (ph == Phase.Opening || ph == Phase.MC)
                return new Vector3(Mathf.Sin(b * 0.6f) * 0.45f, 0.9f, 0.35f).normalized;
            if (ballad)
            {
                float sw = Mathf.Sin(b * Mathf.PI * 0.25f); // 2小節で1往復のゆっくりした左右
                return new Vector3(sw * 0.75f, 0.85f, 0.25f).normalized;
            }
            if (chorus || energy >= 3)
            {
                float pump = Mathf.Abs(Mathf.Sin(b * Mathf.PI)); // 拍ごとに前へふる
                return new Vector3(0.05f, 0.55f + pump * 0.45f, 0.9f - pump * 0.55f).normalized;
            }
            float s2 = Mathf.Sin(b * Mathf.PI * 0.5f);   // 2拍で左右
            return new Vector3(s2 * 0.6f, 0.9f, 0.3f).normalized;
        }

        void FansTick(float beat, int energy, bool ballad, bool chorus)
        {
            if (_penMesh == null) return;
            _v.Clear(); _c.Clear(); _uv.Clear(); _t.Clear(); _n.Clear();
            var av = new List<Vector3>(_fans.Count * 16); var an = new List<Vector3>(_fans.Count * 16); var au = new List<Vector2>(_fans.Count * 16); var at = new List<int>(_fans.Count * 24);
            var themeI = ThemeIndex();
            beat = FrozenBeat(beat); // 時間が止まっている間は、ふりも止まる
            bool hop = !ballad && energy >= 3 && !_frozen;
            _hands.Clear();
            float tt = Time.time;
            foreach (var f in _fans)
            {
                if (!f.root || !f.root.gameObject.activeSelf) continue;
                var root = f.root;
                // サビは拍に合わせて小さくはねる
                float y = hop ? Mathf.Abs(Mathf.Sin((beat + f.phase) * Mathf.PI)) * 0.07f : 0;
                // 無重力：ひとりずつ少しずつちがう高さに浮いて、ゆっくりかたむく
                float fl = FloatHeight * (0.65f + 0.35f * Mathf.Sin(f.phase * 40f + tt * 0.9f));
                var p = root.position; p.y = f.baseY + y + fl; root.position = p;
                root.rotation = Quaternion.Euler(fl * 4f * Mathf.Sin(f.phase * 30f + tt * 0.5f), f.yaw, fl * 5f * Mathf.Cos(f.phase * 25f + tt * 0.4f));
                var rot = root.rotation;
                var shoulder = root.TransformPoint(new Vector3(0.22f, 1.24f, 0));
                var dir = rot * WaveDir(beat, energy, ballad, chorus, f.phase, _phase);
                float armLen = 0.48f * f.scale;
                var hand = shoulder + dir * armLen;
                Box(av, an, au, at, shoulder, hand, 0.075f * f.scale, f.armUv);
                Box(av, an, au, at, hand - dir * 0.02f, hand + dir * 0.07f, 0.05f * f.scale, f.handUv);
                int ci = f.follow ? themeI : f.own;
                Beam(hand, dir, 0.42f, PenColors[ci], 0.05f);
                _hands.Add((hand + dir * 0.4f, PenColors[ci]));
            }
            _penMesh.Clear();
            if (_v.Count > 0)
            {
                _penMesh.SetVertices(_v); _penMesh.SetColors(_c); _penMesh.SetUVs(0, _uv); _penMesh.SetNormals(_n);
                _penMesh.SetTriangles(_t, 0);
                _penMesh.bounds = new Bounds(stageCenter, Vector3.one * 200f);
            }
            if (_armMesh != null)
            {
                _armMesh.Clear();
                if (av.Count > 0)
                {
                    _armMesh.SetVertices(av); _armMesh.SetNormals(an); _armMesh.SetUVs(0, au); _armMesh.SetTriangles(at, 0);
                    _armMesh.bounds = new Bounds(stageCenter, Vector3.one * 200f);
                }
            }
        }

        /// <summary>a から b へのびる四角い棒（ふたなし）。</summary>
        static void Box(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, float w, Vector2 cell)
        {
            var d = (b - a).normalized;
            var x = Vector3.Cross(d, Vector3.up); if (x.sqrMagnitude < 0.001f) x = Vector3.right;
            x = x.normalized * w;
            var y = Vector3.Cross(x, d).normalized * w;
            var side = new[] { x, y, -x, -y };
            for (int i = 0; i < 4; i++)
            {
                var o1 = side[i]; var o2 = side[(i + 1) % 4];
                int k = v.Count;
                v.Add(a + o1); v.Add(a + o2); v.Add(b + o2); v.Add(b + o1);
                var nn = (o1 + o2).normalized;
                for (int j = 0; j < 4; j++) { n.Add(nn); uv.Add(cell); }
                t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 3); t.Add(k + 2);
            }
        }

        // ───────── 自分のペンライト ─────────

        void PenTick(bool inVenue)
        {
            var doll = _player != null ? _player.doll : null;
            bool on = Running && inVenue && doll != null && doll.armR != null && !PlayerController.Busy && !PlayerController.Fly;
            if (doll == null || doll.armR == null) return;
            if (on && _myPen == null)
            {
                _myPen = new GameObject("自分のペンライト");
                _myPen.transform.SetParent(doll.armR, false);
                _myPen.transform.localPosition = new Vector3(0, -0.5f, 0.02f);
                _myPen.transform.localRotation = Quaternion.Euler(180f, 0, 0); // 腕の先の向き（下 = -y）へのばす
                _myPenMesh = new Mesh { name = "my_pen" };
                _v.Clear(); _c.Clear(); _uv.Clear(); _t.Clear(); _n.Clear();
                Beam(Vector3.zero, Vector3.up, 0.42f, Color.white, 0.05f);
                _myPenMesh.SetVertices(_v); _myPenMesh.SetColors(_c); _myPenMesh.SetUVs(0, _uv); _myPenMesh.SetNormals(_n); _myPenMesh.SetTriangles(_t, 0);
                _myPen.AddComponent<MeshFilter>().sharedMesh = _myPenMesh;
                _myPen.AddComponent<MeshRenderer>().sharedMaterial = penMat;
            }
            if (_myPen == null) return;
            if (_myPen.activeSelf != on) _myPen.SetActive(on);
            if (!on) { if (_penHeld) { doll.holdR = 0; _penHeld = false; } return; }
            _mpb.SetColor(IdColor, PenColors[PenColor] * 1.2f);
            _myPen.GetComponent<MeshRenderer>().SetPropertyBlock(_mpb);
            // 腕をあげて、曲に合わせてふる
            var s = Song; var sec = Sec;
            float beat = s != null ? s.Beat(_songT) : _pt * 1.5f;
            var d = WaveDir(beat, sec != null ? sec.energy : 1, s != null && s.style == 1, HypeSec(sec), 0, _phase);
            // 方向 → 腕の回転（腕は -y にのびている）
            var q = Quaternion.FromToRotation(Vector3.down, d);
            doll.holdR = 1f; _penHeld = true;
            doll.holdREuler = q.eulerAngles;
        }

        bool _penHeld;

        // ───────── 炎・花火・おわり ─────────

        void Flames(float k)
        {
            if (_flames == null) return;
            foreach (var p in _flamePos)
                for (int i = 0; i < (int)(40 * k); i++)
                {
                    var c = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.5f), Random.value);
                    Emit(_flames, p + new Vector3(Random.Range(-0.25f, 0.25f), 0, Random.Range(-0.25f, 0.25f)), new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(9f, 15f) * k, Random.Range(-0.6f, 0.6f)), c);
                }
        }

        void Fireworks3(int n)
        {
            var fw = Fireworks.I;
            if (fw == null) return;
            for (int i = 0; i < n; i++)
            {
                var pos = new Vector3(stageCenter.x + Random.Range(-45f, 45f), Random.Range(45f, 75f), stageCenter.z - Random.Range(45f, 80f));
                fw.Burst(pos, PenColors[Random.Range(0, PenColors.Length)], i * Random.Range(0.25f, 0.6f));
            }
        }

        void FxFinale() { Fireworks3(8); Flames(1f); _flash = 1f; }

        void FxThanks(int amount)
        {
            int n = Mathf.Clamp(amount / 10, 30, 220);
            var c = TipColor(amount);
            for (int i = 0; i < n; i++)
                Emit(_burst, spot + Vector3.up * 2.4f, Random.onUnitSphere * Random.Range(2f, 5f) + Vector3.up * 2f, Color.Lerp(c, Color.white, Random.value * 0.4f));
            if (amount >= 1000) Fireworks3(3);
            _flash = 0.7f;
        }

        public static Color TipColor(int amount) =>
            amount >= 3000 ? new Color(1f, 0.3f, 0.35f) : amount >= 1000 ? new Color(1f, 0.6f, 0.2f) : amount >= 500 ? new Color(1f, 0.85f, 0.3f) : amount >= 300 ? new Color(0.35f, 0.95f, 0.6f) : new Color(0.35f, 0.75f, 1f);

        // ───────── 大画面に映すカメラ ─────────

        void CloseCamTick(float t)
        {
            if (!closeCam) return;
            bool sing = _phase != Phase.Opening && _phase != Phase.Idle;
            closeCam.enabled = sing && performer && performer.gameObject.activeSelf;
            if (!closeCam.enabled) return;
            var face = performer.position + Vector3.up * 1.42f;
            float a = Mathf.Sin(t * 0.21f) * 0.6f;
            closeCam.transform.position = face + new Vector3(Mathf.Sin(a) * 2.1f, 0.15f + Mathf.Sin(t * 0.33f) * 0.1f, Mathf.Cos(a) * 2.1f);
            closeCam.transform.rotation = Quaternion.LookRotation(face - closeCam.transform.position);
        }

        // ───────── おまかせカメラ（C） ─────────

        public static bool AutoCam { get; private set; }
        float _savedFov = -1;
        int _shot = -1;

        public static void SetAutoCam(bool on)
        {
            if (I == null) { AutoCam = false; return; }
            if (on && !Running) return;
            if (AutoCam == on) return;
            AutoCam = on;
            var c = Camera.main;
            if (on) { if (c) I._savedFov = c.fieldOfView; } // 案内は下の黒い帯に出す
            else
            {
                if (c && I._savedFov > 0) c.fieldOfView = I._savedFov;
                if (I._cam) I._cam.Snap();
            }
        }

        /// <summary>確認用：おまかせカメラを決まったカットにする（-1 で自動）。</summary>
        public static int ForceShot = -1;

        static readonly int[] ShotsIntro = { 0, 2, 8 }, ShotsVerse = { 1, 3, 7, 5 }, ShotsPre = { 5, 6, 3 }, ShotsChorus = { 0, 4, 1, 2, 6, 8 }, ShotsBallad = { 0, 1, 4, 7, 6 }, ShotsTalk = { 1, 7, 4 };

        void CamLate()
        {
            if (!AutoCam) return;
            if (!Running) { SetAutoCam(false); return; }
            var c = Camera.main;
            if (c == null) return;
            var s = Song; var sec = Sec;
            int shot; float u;
            if (s != null && sec != null)
            {
                bool ballad = s.style == 1;
                int len = ballad ? (sec.energy >= 3 ? 2 : 4) : (sec.energy >= 2 ? 2 : 4);
                float bars = (_songT - sec.t0) / s.BarLen;
                int no = Mathf.FloorToInt(bars / len);
                u = (bars - no * len) / len;
                var list = ballad ? ShotsBallad : sec.name switch { "intro" => ShotsIntro, "outro" => ShotsIntro, "pre" => ShotsPre, "chorus" => ShotsChorus, "drop" => ShotsChorus, "inter" => ShotsChorus, "bridge" => ShotsTalk, _ => ShotsVerse };
                shot = list[(no + sec.bar0) % list.Length];
            }
            else
            {
                float len = 6f;
                int no = Mathf.FloorToInt(_pt / len);
                u = (_pt - no * len) / len;
                shot = _phase == Phase.Opening ? (no % 2 == 0 ? 0 : 8) : ShotsTalk[no % ShotsTalk.Length];
            }
            if (ForceShot >= 0) { shot = ForceShot; u = 0.5f; }
            _shot = shot;
            Shot(c, shot, u, Time.time);
        }

        void Shot(Camera c, int shot, float u, float t)
        {
            var P = performer && performer.gameObject.activeSelf ? performer.position : spot;
            var F = P + Vector3.up * 1.6f;
            Vector3 pos, look; float fov;
            switch (shot)
            {
                case 1: pos = P + new Vector3(0.6f - 1.2f * u, 1.7f, 2.6f); look = F; fov = 30; break;                        // 顔のアップ
                case 2: { float a = (u * 50f + 20f) * Mathf.Deg2Rad; pos = P + new Vector3(Mathf.Sin(a) * 7f, 24f, Mathf.Cos(a) * 7f + 3f); look = P + Vector3.forward * 4f; fov = 55; break; } // 真上から
                case 3: pos = new Vector3(-13f + 26f * u, P.y + 1.2f, P.z + 8.5f); look = F; fov = 38; break;               // 横に流れる
                case 4: pos = P + new Vector3(1.2f - 2.4f * u, 2.5f, -3.4f); look = new Vector3(P.x, Island.Ground + 1.6f, P.z + 22f); fov = 62; break; // ヤチヨのうしろから客席
                case 5: pos = P + new Vector3(0.9f, 0.35f, 4f - u * 0.8f); look = F + Vector3.up * 0.25f; fov = 46; break;   // 足もとから見上げる
                case 6: { float a = (t * 12f) * Mathf.Deg2Rad; pos = P + new Vector3(Mathf.Sin(a) * 11f, 5.5f, Mathf.Cos(a) * 11f); look = F; fov = 45; break; } // まわりを回る
                case 7: pos = new Vector3(-5f + 2f * u, Island.Ground + 1.7f, P.z + 19f); look = F; fov = 30; break;       // 客席の中から
                case 8: pos = new Vector3(7f - u * 2f, Island.Ground + 1.0f, P.z + 26f); look = new Vector3(P.x, Island.Ground + 2.4f, P.z + 12f); fov = 50; break; // ペンライトの海
                default: pos = new Vector3(Mathf.Sin(u * 2f - 1f) * 4f, 11f, P.z + 40f); look = new Vector3(P.x, P.y + 3f, P.z - 2f); fov = 50; break; // 引き
            }
            c.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
            c.fieldOfView = fov;
        }
    }
}
