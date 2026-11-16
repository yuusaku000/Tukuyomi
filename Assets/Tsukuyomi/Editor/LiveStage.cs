using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 段階4：海の AR ライブステージの舞台装置と、ライブ（LiveShow）。
    ///   動かないもの（やぐら・スピーカー・大画面の枠）は _env に置いて、ほかとまとめて軽くする。
    ///   動くもの（ムービングライト・レーザー・光の輪・舞台の上のヤチヨ・客席のペンライト）は _deco に置く。
    /// 結晶の舞台（Landmarks.SeaStage）：まんなか (0, -169.3)、六角の半径 9.43、上の面の高さ Ground + 2.2。
    /// 乱数は使わない（町の並びが変わらないように）。
    /// </summary>
    public static class LiveStage
    {
        const int PerformerLayer = 10; // 大画面のカメラはヤチヨだけを映す

        public static void Build(Transform env, Transform deco)
        {
            float G = Island.Ground;
            var C = new Vector3(0, G, -169.3f);          // 舞台のまんなか（床の高さ）
            float top = G + 2.2f;                          // 舞台の上の面
            var spot = new Vector3(0, top, -165.5f);       // ヤチヨの立つ所

            var metal = Lit("live_truss", new Color(0.5f, 0.52f, 0.58f), 0.65f, 0.8f);
            var dark = Lit("live_speaker", new Color(0.06f, 0.06f, 0.08f), 0.35f);
            var grill = Lit("live_grill", new Color(0.12f, 0.12f, 0.15f), 0.2f);
            var beamMat = Shaded("live_beam", "Tsukuyomi/Beam");
            beamMat.SetFloat("_Boost", 1.4f); beamMat.SetFloat("_Fade", 1.3f); beamMat.SetFloat("_Edge", 0); beamMat.SetFloat("_Soft", 1.6f);
            var laserMat = Shaded("live_laser", "Tsukuyomi/Beam");
            laserMat.SetFloat("_Boost", 3f); laserMat.SetFloat("_Fade", 0.7f); laserMat.SetFloat("_Edge", 2f); laserMat.SetFloat("_Soft", 0);
            var penMat = Shaded("live_pen", "Tsukuyomi/Beam");
            penMat.SetFloat("_Boost", 2.6f); penMat.SetFloat("_Fade", 0); penMat.SetFloat("_Edge", 1.4f); penMat.SetFloat("_Soft", 0);
            foreach (var m in new[] { beamMat, laserMat, penMat }) { m.SetColor("_Color", Color.white); EditorUtility.SetDirty(m); }
            var led = Glow("glow_live_led", new Color(0.85f, 0.9f, 1f), 0.6f, 1.6f);
            var moonMat = Glow("glow_live_moon", new Color(1f, 0.88f, 0.62f), 0.5f, 1.2f);
            var lens = Glow("glow_live_lens", new Color(0.95f, 0.95f, 1f), 0.8f, 2.5f);

            // ───── やぐら（四隅の塔と、上の梁） ─────
            var st = Group(env, "ライブの舞台装置", Vector3.zero);
            float tx = 13f, zb = -180f, zf = -160.5f, ty = 14f;
            foreach (float x in new[] { -tx, tx })
                foreach (float z in new[] { zb, zf })
                    Tower(st, new Vector3(x, G, z), ty - G, metal);
            TrussX(st, new Vector3(0, ty, zb), tx * 2 + 0.6f, metal);
            TrussX(st, new Vector3(0, ty, zf), tx * 2 + 0.6f, metal);
            TrussZ(st, new Vector3(-tx, ty, (zb + zf) / 2), zf - zb, metal);
            TrussZ(st, new Vector3(tx, ty, (zb + zf) / 2), zf - zb, metal);

            // ───── スピーカー（舞台の前の左右に積む＋前の梁から吊る） ─────
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 8.6f, z = -160.4f;
                for (int k = 0; k < 3; k++)
                {
                    float y = G + 0.55f + k * 1.12f;
                    Box(st, "スピーカー", new Vector3(x, y, z), new Vector3(1.6f, 1.08f, 1.1f), dark, k == 0);
                    Cyl(st, "スピーカーの口", new Vector3(x - 0.35f, y, z + 0.56f), 0.32f, 0.04f, grill, false, new Vector3(90, 0, 0));
                    Cyl(st, "スピーカーの口", new Vector3(x + 0.45f, y + 0.2f, z + 0.56f), 0.16f, 0.04f, grill, false, new Vector3(90, 0, 0));
                }
                for (int k = 0; k < 6; k++)
                {
                    float y = ty - 1f - k * 0.62f;
                    Box(st, "吊りスピーカー", new Vector3(side * 10.8f, y, zf + k * 0.05f), new Vector3(1.3f, 0.55f, 0.9f), dark, false, new Vector3(k * 3f, 0, 0));
                }
            }

            // ───── 大画面（左右。ヤチヨのアップを映す） ─────
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(Gen + "/LiveRT.renderTexture");
            if (rt == null)
            {
                rt = new RenderTexture(640, 360, 24) { name = "LiveRT", antiAliasing = 2 };
                AssetDatabase.CreateAsset(rt, Gen + "/LiveRT.renderTexture");
            }
            var screenMat = Shaded("live_screen", "Tsukuyomi/Screen");
            screenMat.SetTexture("_MainTex", rt);
            screenMat.SetFloat("_Bright", 1.3f);
            EditorUtility.SetDirty(screenMat);
            foreach (float side in new[] { -1f, 1f })
            {
                var pos = new Vector3(side * 17.5f, G + 7.6f, -171f);
                var f = new Vector3(0, 0, -147f) - pos; f.y = 0; f.Normalize();
                var rot = Quaternion.LookRotation(-f);
                var g = new GameObject("ライブの大画面").transform;
                g.SetParent(st, false);
                g.SetPositionAndRotation(pos, rot);
                Box(g, "枠", new Vector3(0, 0, 0.18f), new Vector3(9.5f, 5.6f, 0.3f), Black);
                var q = Prim(PrimitiveType.Quad, g, "画面", Vector3.zero, new Vector3(9f, 5.06f, 1f), screenMat);
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Box(g, "光の枠", new Vector3(0, 2.86f, 0.0f), new Vector3(9.6f, 0.08f, 0.08f), GlowPink);
                Box(g, "光の枠", new Vector3(0, -2.86f, 0.0f), new Vector3(9.6f, 0.08f, 0.08f), GlowCyan);
                Box(g, "柱", new Vector3(0, -(G + 7.6f - G) / 2 - 1.4f, 0.25f), new Vector3(0.35f, G + 7.6f - G - 2.8f, 0.35f), metal);
            }

            // ───── 動くもの ─────
            var root = Group(deco, "ヤチヨのライブ", Vector3.zero);
            var show = root.gameObject.AddComponent<LiveShow>();
            var rig = Group(root, "ライブの光", Vector3.zero);

            // ムービングライト（うしろの梁に6・前の梁に4・舞台のうしろの床に2）
            var heads = new System.Collections.Generic.List<Transform>();
            var beams = new System.Collections.Generic.List<Renderer>();
            var cone = SaveMesh("live_cone", BeamCone());
            void Head(Vector3 p, bool hang)
            {
                var b = Group(rig, "ムービングライト", p);
                Box(b, "台", new Vector3(0, hang ? 0.5f : -0.15f, 0), new Vector3(0.5f, 0.18f, 0.5f), dark);
                var h = new GameObject("頭").transform;
                h.SetParent(b, false);
                h.localRotation = Quaternion.LookRotation(hang ? Vector3.down : Vector3.up);
                Box(h, "体", new Vector3(0, 0, -0.1f), new Vector3(0.42f, 0.42f, 0.5f), dark);
                Cyl(h, "レンズ", new Vector3(0, 0, 0.16f), 0.17f, 0.03f, lens, false, new Vector3(90, 0, 0));
                var bm = MeshObj(h, "光の筋", cone, new Vector3(0, 0, 0.17f), beamMat);
                bm.transform.localScale = new Vector3(2.4f, 2.4f, 46f);
                var r = bm.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                heads.Add(h); beams.Add(r);
            }
            foreach (float x in new[] { -10.5f, -6.3f, -2.1f, 2.1f, 6.3f, 10.5f }) Head(new Vector3(x, ty - 0.8f, zb), true);
            foreach (float x in new[] { -9f, -3f, 3f, 9f }) Head(new Vector3(x, ty - 0.8f, zf), true);
            foreach (float x in new[] { -4f, 4f }) Head(new Vector3(x, top + 0.3f, -175.6f), false);

            // 登場の光の柱
            var eb = MeshObj(rig, "登場の光", cone, spot + Vector3.up * 0.05f, beamMat);
            eb.transform.localRotation = Quaternion.LookRotation(Vector3.up);
            eb.transform.localScale = new Vector3(1.4f, 1.4f, 40f);
            var ebr = eb.GetComponent<Renderer>(); ebr.shadowCastingMode = ShadowCastingMode.Off;

            // レーザー・客席のペンライト・客席の腕（形は LiveShow が毎フレーム作る）
            MeshFilter Dyn(string n, Material m)
            {
                var go = new GameObject(n);
                go.transform.SetParent(rig, false);
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
                return mf;
            }
            var lasers = Dyn("レーザー", laserMat);
            lasers.GetComponent<MeshRenderer>().receiveShadows = false;
            var pens = Dyn("客席のペンライト", penMat);
            var arms = Dyn("客席の腕", ColorMat(new Color(0.8f, 0.8f, 0.85f)));
            arms.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            Box(rig, "レーザーの箱", new Vector3(-6f, top + 0.12f, -163.8f), new Vector3(0.45f, 0.24f, 0.45f), dark);
            Box(rig, "レーザーの箱", new Vector3(6f, top + 0.12f, -163.8f), new Vector3(0.45f, 0.24f, 0.45f), dark);

            // 光の輪（舞台のうしろに立つ大きな輪と月、舞台の床の輪）。ふだんもうすく光っている
            var rings = Group(root, "光の輪", Vector3.zero);
            var ledRing = new System.Collections.Generic.List<Renderer>();
            var ringC = new Vector3(0, G + 9.6f, -177.4f);
            void RingV(float r, int n, float w)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 2f / n;
                    var seg = Box(rings, "輪", ringC + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0), new Vector3(2f * Mathf.PI * r / n * 0.82f, w, 0.15f), led, false, new Vector3(0, 0, a * Mathf.Rad2Deg + 90f));
                    var rr = seg.GetComponent<Renderer>(); rr.shadowCastingMode = ShadowCastingMode.Off;
                    ledRing.Add(rr);
                }
            }
            RingV(6.2f, 36, 0.28f);
            RingV(4.7f, 28, 0.16f);
            var moon = Cyl(rings, "月", ringC + new Vector3(0, 0, -0.15f), 4.1f, 0.06f, moonMat, false, new Vector3(90, 0, 0));
            moon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var ledFloor = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < 32; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 32;
                var seg = Box(rings, "床の輪", new Vector3(C.x + Mathf.Cos(a) * 6.8f, top + 0.03f, C.z + Mathf.Sin(a) * 6.8f), new Vector3(2f * Mathf.PI * 6.8f / 32 * 0.8f, 0.04f, 0.14f), led, false, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
                var rr = seg.GetComponent<Renderer>(); rr.shadowCastingMode = ShadowCastingMode.Off;
                ledFloor.Add(rr);
            }

            // 本物の光（ヤチヨを照らすスポット2つと、客席をそめる光）
            Light SpotL(Vector3 p, Color c)
            {
                var go = new GameObject("スポットライト");
                go.transform.SetParent(rig, false);
                go.transform.position = p;
                go.transform.rotation = Quaternion.LookRotation(spot + Vector3.up * 1.2f - p);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot; l.color = c; l.range = 34f; l.spotAngle = 16f; l.innerSpotAngle = 8f; l.intensity = 9f;
                l.shadows = LightShadows.None;
                return l;
            }
            var spots = new[] { SpotL(new Vector3(-3.5f, ty - 0.6f, zf), new Color(1f, 0.95f, 0.98f)), SpotL(new Vector3(3.5f, ty - 0.6f, zf), new Color(1f, 0.8f, 0.92f)) };
            var washGo = new GameObject("客席の光");
            washGo.transform.SetParent(rig, false);
            washGo.transform.position = new Vector3(0, G + 8f, -150f);
            var wash = washGo.AddComponent<Light>();
            wash.type = LightType.Point; wash.range = 32f; wash.intensity = 3f; wash.shadows = LightShadows.None;

            // 舞台の上のヤチヨ（右手にマイク）
            var perf = new GameObject("舞台のヤチヨ").transform;
            perf.SetParent(root, false);
            perf.position = spot;
            var doll = Doll.Build("ヤチヨの人形（ライブ）", DollLook.Yachiyo, ColorMat, perf);
            doll.root.transform.localScale = Vector3.one * 1.08f;
            Cyl(doll.armR, "マイク", new Vector3(0, -0.56f, 0.03f), 0.022f, 0.16f, Black);
            Ball(doll.armR, "マイクの頭", new Vector3(0, -0.66f, 0.03f), Vector3.one * 0.075f, Lit("live_mic", new Color(0.75f, 0.77f, 0.8f), 0.7f, 0.9f));
            foreach (var t in perf.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PerformerLayer;
            doll.root.GetComponent<DollAnimator>().enabled = false;

            // 大画面に映すカメラ（ヤチヨだけ）
            var camGo = new GameObject("大画面のカメラ");
            camGo.transform.SetParent(root, false);
            camGo.transform.position = spot + new Vector3(0, 1.5f, 2.2f);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt; cam.fieldOfView = 28; cam.nearClipPlane = 0.1f; cam.farClipPlane = 25f; cam.depth = -9;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.03f, 0.12f);
            cam.cullingMask = 1 << PerformerLayer;
            var cd = camGo.GetComponent<UniversalAdditionalCameraData>() ?? camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = false; cd.renderShadows = false;
            cam.enabled = false;

            // 音（曲と歓声）
            AudioSource Src(string n, Vector3 p, float minD, float maxD, bool loop, int prio)
            {
                var go = new GameObject(n);
                go.transform.SetParent(root, false);
                go.transform.position = p;
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = loop; s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = minD; s.maxDistance = maxD; s.dopplerLevel = 0; s.priority = prio; s.spatialBlend = 1f; s.volume = 0;
                return s;
            }

            show.performer = perf;
            show.yBody = doll.body; show.yHead = doll.head; show.yArmL = doll.armL; show.yArmR = doll.armR;
            show.spot = spot;
            show.stageCenter = C;
            show.rig = rig;
            show.heads = heads.ToArray();
            show.beams = beams.ToArray();
            show.entranceBeam = ebr;
            show.lasers = lasers; show.pens = pens; show.arms = arms;
            show.laserFrom = new[] { new Vector3(-6f, top + 0.3f, -163.8f), new Vector3(6f, top + 0.3f, -163.8f), new Vector3(0, ty - 0.6f, zb + 0.4f) };
            show.ledRing = ledRing.ToArray();
            show.ledFloor = ledFloor.ToArray();
            show.moonDisc = moon.GetComponent<Renderer>();
            show.closeCam = cam;
            show.spots = spots;
            show.wash = wash;
            show.music = Src("ライブの曲", new Vector3(0, G + 9f, -170f), 35f, 450f, false, 0);
            show.cheer = Src("客席の歓声", new Vector3(0, G + 2f, -150f), 22f, 140f, true, 40);
            show.sparkMat = Shaded("spark", "Tsukuyomi/Spark");
            show.penMat = penMat;
            show.beamMat = beamMat;
            show.glowMat = led;
            var water = Shaded("water_live", "Tsukuyomi/Water");
            water.SetColor("_Deep", new Color(0.02f, 0.18f, 0.3f)); water.SetColor("_Shallow", new Color(0.1f, 0.5f, 0.6f));
            water.SetFloat("_Scale", 0.2f); water.SetFloat("_Speed", 0.9f); water.SetFloat("_Alpha", 0.62f); water.SetFloat("_Grid", 0f);
            EditorUtility.SetDirty(water);
            show.waterMat = water;
            show.holoMat = Landmarks.Holo("holo_live", new Color(0.5f, 0.85f, 1f), 0.5f, 1.8f, 0.15f);
            rig.gameObject.SetActive(false);
            Debug.Log($"[Tsukuyomi] live stage: heads {heads.Count}, ring {ledRing.Count}+{ledFloor.Count}");
        }

        /// <summary>トラス（細い4本の柱と横木）の塔。</summary>
        static void Tower(Transform p, Vector3 b, float h, Material m)
        {
            const float w = 0.6f;
            foreach (float x in new[] { -w / 2, w / 2 })
                foreach (float z in new[] { -w / 2, w / 2 })
                    Box(p, "やぐら", b + new Vector3(x, h / 2, z), new Vector3(0.07f, h, 0.07f), m, x < 0 && z < 0);
            for (float y = 0.6f; y < h; y += 1.2f)
            {
                Box(p, "やぐら", b + new Vector3(0, y, -w / 2), new Vector3(w, 0.05f, 0.05f), m);
                Box(p, "やぐら", b + new Vector3(0, y, w / 2), new Vector3(w, 0.05f, 0.05f), m);
                Box(p, "やぐら", b + new Vector3(-w / 2, y, 0), new Vector3(0.05f, 0.05f, w), m);
                Box(p, "やぐら", b + new Vector3(w / 2, y, 0), new Vector3(0.05f, 0.05f, w), m);
            }
            Box(p, "やぐらの台", b + new Vector3(0, 0.08f, 0), new Vector3(1.1f, 0.16f, 1.1f), m, true);
        }

        static void TrussX(Transform p, Vector3 c, float len, Material m)
        {
            const float w = 0.6f;
            foreach (float y in new[] { -w / 2, w / 2 })
                foreach (float z in new[] { -w / 2, w / 2 })
                    Box(p, "梁", c + new Vector3(0, y, z), new Vector3(len, 0.07f, 0.07f), m);
            for (float x = -len / 2 + 0.6f; x < len / 2; x += 1.2f)
            {
                Box(p, "梁", c + new Vector3(x, 0, -w / 2), new Vector3(0.05f, w, 0.05f), m);
                Box(p, "梁", c + new Vector3(x, 0, w / 2), new Vector3(0.05f, w, 0.05f), m);
            }
        }

        static void TrussZ(Transform p, Vector3 c, float len, Material m)
        {
            const float w = 0.6f;
            foreach (float y in new[] { -w / 2, w / 2 })
                foreach (float x in new[] { -w / 2, w / 2 })
                    Box(p, "梁", c + new Vector3(x, y, 0), new Vector3(0.07f, 0.07f, len), m);
            for (float z = -len / 2 + 0.6f; z < len / 2; z += 1.2f)
            {
                Box(p, "梁", c + new Vector3(-w / 2, 0, z), new Vector3(0.05f, w, 0.05f), m);
                Box(p, "梁", c + new Vector3(w / 2, 0, z), new Vector3(0.05f, w, 0.05f), m);
            }
        }

        /// <summary>光の筋の円すい（+z へ広がる。根もとの半径 0.12、先の半径 1、長さ 1。uv.y が根もと→先）。</summary>
        static Mesh BeamCone()
        {
            const int n = 20;
            var v = new System.Collections.Generic.List<Vector3>(); var nn = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>(); var t = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                v.Add(d * 0.12f); v.Add(d + Vector3.forward);
                nn.Add(d); nn.Add(d);
                uv.Add(new Vector2(0.5f, 0)); uv.Add(new Vector2(0.5f, 1));
            }
            for (int i = 0; i < n; i++)
            {
                int k = i * 2;
                t.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 });
            }
            var m = new Mesh { name = "live_cone" };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.bounds = new Bounds(new Vector3(0, 0, 0.5f), new Vector3(2, 2, 1));
            return m;
        }
    }
}
