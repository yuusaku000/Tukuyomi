using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// ツクヨミの世界をまるごと組み立てて Assets/Scenes/Tsukuyomi.unity に保存する（乱数の種が固定なので毎回同じ形）。
    ///
    /// 島の形は Tsukuyomi.Island（丸い島・3つの段・川と水路）。上から見て +z が北。
    ///   海：AR ライブステージ (0,-130) ─ 大鳥居 (0,-82) ─ 高舞台（スポーン）─ 海の回廊 ─ 大門 (0,5)
    ///   下町：水路と屋形船・風鈴の回廊　中段：月見商店街・大画面広場・釣り堀・千年桜の丘・藤棚・番傘の通り
    ///   上段：千本鳥居の石段・五重塔・竹林・懸造りの展望台・神楽殿・鐘楼　頂上：月の御殿
    /// </summary>
    public static class WorldBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tsukuyomi.unity";
        const float G = Tsukuyomi.Island.Ground;

        static Transform _env, _deco;
        static readonly List<Transform> CrowdPts = new List<Transform>(), WaterPts = new List<Transform>(), ChimePts = new List<Transform>();
        static Transform _festival, _bell;
        static Vector3 _lookoutWarp, _stageWarp;

        static float L(int i) => Tsukuyomi.Island.LevelTop(i);
        static float Hgt(float x, float z) => Tsukuyomi.Island.Height(x, z);

        static Transform Marker(string name, Vector3 pos, List<Transform> list)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_deco, false);
            t.position = pos;
            list?.Add(t);
            return t;
        }

        [MenuItem("Tsukuyomi/ワールドを組み立てる")]
        public static void BuildAll()
        {
            Rng = new System.Random(8000);
            Begin();
            SaveMesh("doll_robe", Doll.Cone);
            CrowdPts.Clear(); WaterPts.Clear(); ChimePts.Clear();
            Town.Reset(); Town.Streets.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _env = new GameObject("ワールド").transform;
            _deco = new GameObject("動くもの").transform;

            var (sun, moon) = Lights();
            Sea();
            TerrainBuilder.Build(_env);
            Entrance();
            _stageWarp = Landmarks.SeaStage(_env, _deco, new Vector2(0, -130), -74.2f);
            ReserveLandmarks();
            Streets();
            StairsAll();
            Town.BuildStreets(_env, Parts.Paving);
            SaveStreetMap();
            CheckLandmarks();
            LandmarksAll(out var studio);
            Houses();
            Clutter.Build(_env, _deco);
            Waterfalls();
            Landmarks.SkyAndNight(_deco);
            SkyLanterns();
            Boundaries();
            var player = Player(out var cam, out var spawn);
            Sky(sun, moon, cam);
            Post();
            Zones();
            Audio();

            var sys = new GameObject("ツクヨミ");
            var session = sys.AddComponent<GameSession>();
            session.player = player; session.cam = cam; session.spawn = spawn;
            var hud = sys.AddComponent<Hud>();
            hud.player = player; hud.cam = cam;

            Combiner.Run(_env);
            foreach (var t in _env.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var settings = new LightingSettings { name = "TsukuyomiLighting", bakedGI = false, realtimeGI = false };
            string lsPath = Gen + "/Lighting.lighting";
            AssetDatabase.DeleteAsset(lsPath);
            AssetDatabase.CreateAsset(settings, lsPath);
            Lightmapping.lightingSettings = settings;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Tsukuyomi] world built: " + _env.GetComponentsInChildren<Renderer>().Length + " renderers, " + _deco.GetComponentsInChildren<Renderer>().Length + " moving");
        }

        // ───────────────────────── 光と空 ─────────────────────────

        static (Light sun, Light moon) Lights()
        {
            Light Make(string n, float intensity, Color c)
            {
                var go = new GameObject(n);
                var l = go.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = intensity;
                l.color = c;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.85f;
                return l;
            }
            var sun = Make("太陽", 1.4f, Color.white);
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            var moon = Make("月の光", 0.5f, new Color(0.62f, 0.72f, 1f));
            moon.shadowStrength = 0.6f;
            return (sun, moon);
        }

        static void Sky(Light sun, Light moon, OrbitCamera cam)
        {
            var skyMat = Shaded("sky_procedural", "Skybox/Procedural");
            skyMat.SetFloat("_SunDisk", 2);
            skyMat.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            RenderSettings.skybox = skyMat;
            RenderSettings.sun = sun;

            var root = new GameObject("空");
            var sky = root.AddComponent<RealTimeSky>();
            sky.sun = sun; sky.moon = moon; sky.skybox = skyMat; sky.follow = cam.GetComponent<Camera>();

            var stars = Prim(PrimitiveType.Sphere, root.transform, "星", Vector3.zero, Vector3.one * 3600f, Shaded("stars", "Tsukuyomi/Stars"));
            stars.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            sky.stars = stars.transform;

            var moonMat = Shaded("moon", "Tsukuyomi/Moon");
            var moonBody = Prim(PrimitiveType.Sphere, root.transform, "ツクヨミの月", new Vector3(0, 500, -1400), Vector3.one * 300f, moonMat);
            moonBody.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            sky.moonBody = moonBody.transform;
            var haloMat = Shaded("moon_halo", "Tsukuyomi/Halo");
            haloMat.SetColor("_Color", new Color(0.55f, 0.6f, 0.9f));
            haloMat.SetFloat("_Power", 2.6f);
            haloMat.SetFloat("_DayK", 0.08f);
            var halo = Prim(PrimitiveType.Quad, root.transform, "月の光の輪", Vector3.zero, Vector3.one * 1300f, haloMat);
            halo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            sky.moonHalo = halo.transform;
        }


        static void Post()
        {
            string path = Gen + "/PostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var bloom = Add<Bloom>();
            bloom.intensity.Override(1.1f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.68f);
            var tone = Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var color = Add<ColorAdjustments>();
            color.saturation.Override(12f);
            color.contrast.Override(6f);
            var vig = Add<Vignette>();
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("画づくり");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }


        static UniversalAdditionalCameraData AddData(GameObject go)
        {
            var d = go.GetComponent<UniversalAdditionalCameraData>();
            if (d == null) d = go.AddComponent<UniversalAdditionalCameraData>();
            return d;
        }

        // ───────────────────────── 自分 ─────────────────────────

        static PlayerController Player(out OrbitCamera cam, out Transform spawn)
        {
            spawn = new GameObject("スポーン（大鳥居の高舞台）").transform;
            spawn.position = new Vector3(0, G + 0.1f, -69f);
            spawn.rotation = Quaternion.Euler(0, 0, 0);

            var go = new GameObject("自分");
            go.transform.position = spawn.position;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.32f; cc.center = new Vector3(0, 0.82f, 0);
            cc.stepOffset = 0.4f; cc.slopeLimit = 50f; cc.skinWidth = 0.05f;
            var pc = go.AddComponent<PlayerController>();
            var doll = Doll.Build("アバター", DollLook.Player, ColorMat, go.transform);
            doll.root.transform.localScale = Vector3.one * 0.9f;
            pc.doll = doll.root.GetComponent<DollAnimator>();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = camGo.AddComponent<Camera>();
            c.nearClipPlane = 0.1f; c.farClipPlane = 4200f; c.fieldOfView = 58f;
            camGo.AddComponent<AudioListener>();
            var data = AddData(camGo);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam = camGo.AddComponent<OrbitCamera>();
            cam.target = go.transform;
            pc.cam = cam;
            return pc;
        }


        // ───────────────────────── 海と入口 ─────────────────────────

        static void Sea()
        {
            var sea = Shaded("water_sea", "Tsukuyomi/Water");
            var q = Prim(PrimitiveType.Quad, _env, "海", new Vector3(0, 0, 80), new Vector3(5000, 5000, 1), sea, false, new Vector3(90, 0, 0));
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Quad, _env, "海の底", new Vector3(0, -3.6f, 80), new Vector3(5000, 5000, 1), Lit("seabed", new Color(0.12f, 0.2f, 0.22f)), false, new Vector3(90, 0, 0));
            var bed = Box(_env, "海の底（足場）", new Vector3(0, -1.4f, -150), new Vector3(1000, 1f, 300), Sand, true);
            bed.GetComponent<Renderer>().enabled = false;
        }

        static void Entrance()
        {
            var g = Group(_env, "大鳥居と海の回廊", Vector3.zero);
            Torii(g, new Vector3(0, 0, -82), 1f);

            Box(g, "高舞台", new Vector3(0, G - 0.5f, -68), new Vector3(16, 1f, 12), Wood, true);
            Box(g, "高舞台のふち", new Vector3(0, G - 0.55f, -68), new Vector3(16.4f, 0.9f, 12.4f), Vermilion);
            for (int i = 0; i < 6; i++)
                foreach (float z in new[] { -73.8f, -62.2f })
                    Cyl(g, "舞台の脚", new Vector3(-7.5f + i * 3f, -0.9f, z), 0.25f, 3.6f, Vermilion);
            foreach (float x in new[] { -8f, 8f })
            {
                Box(g, "高欄", new Vector3(x, G + 0.9f, -68), new Vector3(0.14f, 0.12f, 12f), Vermilion);
                Box(g, "見えない柵", new Vector3(x, G + 0.7f, -68), new Vector3(0.3f, 1.4f, 12f), Vermilion, true).GetComponent<Renderer>().enabled = false;
                for (int k = 0; k < 5; k++) Cyl(g, "高欄の柱", new Vector3(x, G + 0.5f, -73.5f + k * 2.75f), 0.07f, 1f, Vermilion);
            }
            // 南側（大鳥居側）は、まんなかだけ光の橋へあける
            foreach (float x in new[] { -5.25f, 5.25f })
            {
                Box(g, "高欄", new Vector3(x, G + 0.9f, -74f), new Vector3(5.5f, 0.12f, 0.14f), Vermilion);
                Box(g, "見えない柵", new Vector3(x, G + 0.7f, -74.1f), new Vector3(5.5f, 1.4f, 0.3f), Vermilion, true).GetComponent<Renderer>().enabled = false;
            }
            Toro(g, new Vector3(-6.5f, G, -64.5f), true);
            Toro(g, new Vector3(6.5f, G, -64.5f), true);
            Box(g, "光の線", new Vector3(0, G + 0.02f, -68), new Vector3(0.12f, 0.02f, 11f), GlowCyan);
            Box(g, "光の線", new Vector3(0, G + 0.02f, -68), new Vector3(15f, 0.02f, 0.12f), GlowCyan);

            const float w = 7f;
            Box(g, "回廊の床", new Vector3(0, G - 0.5f, -31), new Vector3(w, 1f, 62f), Wood, true);
            Box(g, "回廊の桁", new Vector3(0, G - 1.1f, -31), new Vector3(w + 0.3f, 0.4f, 62f), Vermilion);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (w / 2 - 0.1f);
                Box(g, "高欄", new Vector3(x, G + 0.9f, -31), new Vector3(0.14f, 0.12f, 62f), Vermilion);
                Box(g, "高欄", new Vector3(x, G + 0.45f, -31), new Vector3(0.1f, 0.1f, 62f), Vermilion);
                Box(g, "見えない柵", new Vector3(x, G + 0.7f, -31), new Vector3(0.3f, 1.4f, 62f), Vermilion, true).GetComponent<Renderer>().enabled = false;
                Box(g, "光の線", new Vector3(side * (w / 2 - 0.5f), G + 0.02f, -31), new Vector3(0.1f, 0.02f, 61f), GlowCyan);
                for (int i = 0; i <= 15; i++)
                {
                    float z = -62 + i * 4f;
                    Cyl(g, "高欄の柱", new Vector3(x, G + 0.5f, z), 0.08f, 1f, Vermilion);
                    Cyl(g, "回廊の脚", new Vector3(x, -1f, z), 0.22f, 3.6f, Vermilion);
                    if (i % 2 == 1)
                    {
                        Cyl(g, "提灯の柱", new Vector3(x, G + 1.6f, z), 0.07f, 3.2f, WoodDark);
                        Chochin(g, new Vector3(x - side * 0.35f, G + 2.7f, z), i % 4 == 1 ? GlowRed : GlowWhite);
                        if (i % 4 == 1) Lamp(g, new Vector3(x - side * 0.6f, G + 2.4f, z), new Color(1f, 0.55f, 0.4f), 7f, 1.6f);
                    }
                }
            }

            // 大門
            var gate = Group(g, "大門", new Vector3(0, G, 6));
            foreach (float x in new[] { -6.5f, -2.6f, 2.6f, 6.5f })
                foreach (float z in new[] { -2f, 2f })
                    Cyl(gate, "柱", new Vector3(x, 3.2f, z), 0.32f, 6.4f, Vermilion, Mathf.Abs(x) > 5);
            Box(gate, "貫", new Vector3(0, 5.6f, -2), new Vector3(14.5f, 0.5f, 0.5f), Vermilion);
            Box(gate, "貫", new Vector3(0, 5.6f, 2), new Vector3(14.5f, 0.5f, 0.5f), Vermilion);
            MeshObj(gate, "屋根", Roof(17, 8, 2.2f, 0.9f), new Vector3(0, 6.4f, 0), RoofCopper);
            Box(gate, "二階", new Vector3(0, 8.3f, 0), new Vector3(10f, 2.6f, 4f), WhiteWall);
            Box(gate, "二階格子", new Vector3(0, 8.4f, -2.03f), new Vector3(9f, 1.6f, 0.06f), Lattice);
            MeshObj(gate, "屋根2", Roof(15, 7.5f, 3.2f, 1f), new Vector3(0, 9.6f, 0), RoofCopper);
            Box(gate, "棟", new Vector3(0, 12.9f, 0), new Vector3(8.2f, 0.4f, 0.45f), Black);
            Box(gate, "扁額", new Vector3(0, 8.4f, -2.2f), new Vector3(2.6f, 1.2f, 0.12f), Black);
            Box(gate, "扁額金", new Vector3(0, 8.4f, -2.28f), new Vector3(2.2f, 0.9f, 0.04f), GlowGold);
            Chochin(gate, new Vector3(-4.5f, 4.6f, -2.4f), GlowRed);
            Chochin(gate, new Vector3(4.5f, 4.6f, -2.4f), GlowRed);
            Lamp(gate, new Vector3(0, 4.5f, -3f), new Color(1f, 0.6f, 0.4f), 14f, 2.5f);
            Town.Reserve(new Vector2(0, 6), 9f);
        }

        // ───────────────────────── 場所取り・通り・石段 ─────────────────────────

        static readonly Vector2 PlazaC = new Vector2(40, 100);
        static readonly Vector2 PagodaC = new Vector2(36, 198);
        static readonly Vector2 BambooC = new Vector2(-28, 160);
        static readonly Vector2 KaguraC = new Vector2(23, 158);
        static readonly Vector2 BellC = new Vector2(-11, 157);
        static readonly Vector2 WisteriaC = new Vector2(20, 94);
        static Vector2 _lookEdge, _lookOut;

        static void ReserveLandmarks()
        {
            Town.Reserve(PlazaC, 16f);
            Town.Reserve(new Vector2(60, 100), 9f);
            Town.Reserve(Island.HillCenter, Island.HillRadius + 2f);
            Town.Reserve(Island.Pond.center, 17f);
            Town.Reserve(new Vector2(-40, 126), 8f);
            Town.Reserve(PagodaC, 9f);
            Town.Reserve(BambooC, 14f);
            Town.Reserve(KaguraC, 8f);
            Town.Reserve(BellC, 6f);
            Town.Reserve(WisteriaC, 8f);
            Town.Reserve(new Vector2(0, 204), 22f);
            var L2 = Island.Levels[1];
            float a = -55f * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            _lookEdge = L2.c + dir * (L2.RadiusAt(a) - 0.5f);
            _lookOut = dir;
            Town.Reserve(_lookEdge + dir * 4f, 13f);
            Town.Reserve(_lookEdge - dir * 6f, 10f);
            Town.Reserve(new Vector2(74, 44), 14f); // 風鈴の回廊
            // ワープした場所の目の前に家が来ないように
            foreach (var p in new[] { new Vector2(0, 12), new Vector2(0, 52), new Vector2(24, 100), new Vector2(-40, 100), new Vector2(-74, 127), new Vector2(0, 131), new Vector2(-38, 150), new Vector2(0, 184) })
                Town.Reserve(p, 5f);
        }

        static Material StairStone => Lit("stair_stone", new Color(0.62f, 0.6f, 0.56f), 0.15f);
        static Transform _stairs;

        static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        /// <summary>
        /// 道と石段を、つながった網にする。
        ///   大通り：大門 → 水路の橋 → 大石段 → 月見商店街 → 千本鳥居の石段 → 上段の参道 → 御殿への石段 → 頂上
        ///   環状通り：下町・中段・上段に1本ずつ
        ///   放射の路地：下段の環状通り → 石段 → 上段の環状通り（行き止まりにならないよう、かならず石段でつなぐ）
        /// </summary>
        static void Streets()
        {
            _stairs = Group(_env, "石段", Vector3.zero);
            var c0 = Island.Center;
            var L1 = Island.Levels[0]; var L2 = Island.Levels[1];

            // 環状通り
            Town.AddStreet("下町の環状通り", Town.Ring(c0, a => Island.EdgeRadius(a) - 13f, 0f, 360f, 2.5f), 3f, 0);
            Town.AddStreet("中段の環状通り", Town.Ring(L1.c, a => L1.RadiusAt(a) - 10f, 0f, 360f, 2.5f), 3.2f, 1);
            Town.AddStreet("上段の環状通り", Town.Ring(L2.c, a => L2.RadiusAt(a) - 11f, 0f, 360f, 3f), 2.6f, 2);

            // 大通り（南から頂上まで）
            var s1 = Town.Stairs(_stairs, "大石段", new Vector2(0, 34.2f), Vector2.up, 8f, false, StairStone);
            Town.AddStreet("大門通り", new[] { new Vector2(0, 2), s1.bottom }, 4f, 0);
            var s2 = Town.Stairs(_stairs, "千本鳥居の石段", new Vector2(0, 98), Vector2.up, 4.5f, true, StairStone);
            Town.AddStreet("月見商店街", new[] { s1.top, s2.bottom }, 6f, 1);
            var s3 = Town.Stairs(_stairs, "御殿への石段", new Vector2(0, 150), Vector2.up, 6f, false, StairStone);
            Town.AddStreet("上段の参道", new[] { s2.top, s3.bottom }, 4f, 2);
            Town.AddStreet("頂上の参道", new[] { s3.top, new Vector2(0, 196) }, 5f, 3);

            // 下町 → 中段（放射の路地と石段）。池・丘・広場の向きはさける
            int k = 0;
            foreach (float deg in new[] { -160f, -105f, -72f, -45f, 8f, 35f, 62f, 95f, 122f, 150f, 230f })
            {
                var d = Dir(deg);
                float r1 = L1.RadiusAt(deg * Mathf.Deg2Rad);
                var foot = L1.c + d * (r1 + 13f);
                if (Island.Inside(foot.x, foot.y) < 6f) foot = L1.c + d * (r1 + 9f);
                // 下町の環状通りからの路地
                var fromC0 = foot - c0;
                var ring0 = c0 + fromC0.normalized * (Island.EdgeRadius(Mathf.Atan2(fromC0.y, fromC0.x)) - 13f);
                var st = Town.Stairs(_stairs, "下町からの石段" + k, foot, -d, k % 3 == 1 ? 4f : 5f, k % 4 == 2, StairStone);
                if (!st.ok) { k++; continue; }
                if (Vector2.Distance(ring0, st.bottom) > 2f) Town.AddStreet("下町の路地" + k, new[] { ring0, st.bottom }, 2.2f, 0);
                var ring1 = L1.c + d * (r1 - 10f);
                if (Vector2.Distance(st.top, ring1) > 1.5f) Town.AddStreet("中段の路地" + k, new[] { st.top, ring1 }, 2.2f, 1);
                k++;
            }

            // 中段 → 上段
            foreach (float deg in new[] { -10f, 60f, 120f, 170f })
            {
                var d = Dir(deg);
                float r2 = L2.RadiusAt(deg * Mathf.Deg2Rad);
                var foot = L2.c + d * (r2 + 16f);
                var st = Town.Stairs(_stairs, "上段への石段" + k, foot, -d, 4f, k % 2 == 0, StairStone);
                if (!st.ok) { k++; continue; }
                // 中段の環状通りから石段の下まで
                var fromC1 = foot - L1.c;
                var ring1 = L1.c + fromC1.normalized * (L1.RadiusAt(Mathf.Atan2(fromC1.y, fromC1.x)) - 10f);
                Town.AddStreet("中段から上段への路地" + k, new[] { ring1, st.bottom }, 2.2f, 1);
                var ring2 = L2.c + d * (r2 - 11f);
                Town.AddStreet("上段の路地" + k, new[] { st.top, ring2 }, 2.2f, 2);
                k++;
            }

            // 中段の横の通り（商店街と環状通りをつなぐ）
            Town.AddStreet("東西通り", new[] { new Vector2(-100, 78), new Vector2(-40, 80), new Vector2(0, 80), new Vector2(40, 80), new Vector2(100, 78) }, 3f, 1);
        }

        static void StairsAll() { }

        static void SaveStreetMap()
        {
            var go = new GameObject("道の地図");
            var sm = go.AddComponent<StreetMap>();
            var pts = new List<Vector2>(); var counts = new List<int>(); var halves = new List<float>();
            foreach (var s in Town.Streets)
            {
                pts.AddRange(s.pts); counts.Add(s.pts.Count); halves.Add(s.half);
            }
            sm.points = pts.ToArray(); sm.counts = counts.ToArray(); sm.halves = halves.ToArray();
        }

        /// <summary>名所の場所に道が通っていないか（通っていたらログに出す）。</summary>
        static void CheckLandmarks()
        {
            foreach (var (name, c, r) in new[] {
                ("広場", PlazaC, 14f), ("五重塔", PagodaC, 7f), ("竹林の中心", BambooC, 4f), ("神楽殿", KaguraC, 6f), ("鐘楼", BellC, 4.5f),
                ("藤棚", WisteriaC, 3f), ("千年桜", Island.HillCenter, 10f), ("釣り堀", Island.Pond.center, 14f), ("茶屋", new Vector2(-40, 127), 7f) })
                if (Town.OnAnyStreet(c, r)) Debug.LogWarning($"[Tsukuyomi] street crosses landmark: {name}");
        }

        // ───────────────────────── 名所 ─────────────────────────

        static void LandmarksAll(out StreamStudio studio)
        {
            float l1 = L(1), l2 = L(2), l3 = L(3);
            var g = Group(_env, "名所", Vector3.zero);

            // 大画面広場
            Box(g, "広場の石畳", new Vector3(PlazaC.x, l1 + 0.04f, PlazaC.y), new Vector3(30f, 0.08f, 30f), Parts.Paving);
            ScreenHall(g, new Vector3(60, l1, 100), out studio);
            for (int i = 0; i < 8; i++)
            {
                float a = (i / 8f) * Mathf.PI * 2;
                var p = PlazaC + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12.5f;
                if (p.x > PlazaC.x + 8 || p.x < PlazaC.x - 10) continue; // 東は大画面、西はワープして来る所
                Buildings.Yatai(g, new Vector3(p.x, l1, p.y), Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg, i);
            }
            for (int i = 0; i < 4; i++)
            {
                var b = Group(g, "長椅子", new Vector3(PlazaC.x + 2, l1, PlazaC.y - 9 + i * 6), 90);
                Box(b, "座面", new Vector3(0, 0.45f, 0), new Vector3(4f, 0.12f, 0.7f), Wood, true);
                Box(b, "毛氈", new Vector3(0, 0.52f, 0), new Vector3(4f, 0.03f, 0.72f), Lit("felt_red", new Color(0.75f, 0.1f, 0.12f), 0.05f));
            }
            Marker("広場のざわめき", new Vector3(PlazaC.x, l1 + 2, PlazaC.y), CrowdPts);

            // 釣り堀と茶屋
            var pond = Island.Pond;
            foreach (float side in new[] { -1f, 1f })
            {
                Box(g, "釣り堀の縁", new Vector3(pond.center.x, l1 + 0.2f, pond.center.y + side * (pond.height / 2 + 0.4f)), new Vector3(pond.width + 1.6f, 0.5f, 0.8f), Stone, true);
                Box(g, "釣り堀の縁", new Vector3(pond.center.x + side * (pond.width / 2 + 0.4f), l1 + 0.2f, pond.center.y), new Vector3(0.8f, 0.5f, pond.height), Stone, true);
            }
            var pw = Shaded("water_pond", "Tsukuyomi/Water");
            pw.SetColor("_Deep", new Color(0.05f, 0.22f, 0.22f)); pw.SetColor("_Shallow", new Color(0.12f, 0.45f, 0.4f));
            pw.SetFloat("_Scale", 0.35f); pw.SetFloat("_Speed", 0.4f); pw.SetFloat("_Alpha", 0.8f); pw.SetFloat("_Grid", 0.4f);
            Prim(PrimitiveType.Quad, g, "釣り堀の水", new Vector3(pond.center.x, l1 - 0.55f, pond.center.y), new Vector3(pond.width, pond.height, 1), pw, false, new Vector3(90, 0, 0));
            for (int i = 0; i < 16; i++)
            {
                var koi = Ball(g, "鯉", new Vector3(pond.center.x + R(-12, 12), l1 - 0.9f, pond.center.y + R(-6, 6)), new Vector3(0.35f, 0.15f, 0.9f),
                    ColorMat(i % 3 == 0 ? new Color(1f, 0.45f, 0.15f) : i % 3 == 1 ? new Color(0.95f, 0.95f, 0.9f) : new Color(0.9f, 0.2f, 0.15f)));
                koi.transform.localRotation = Quaternion.Euler(0, R(0, 360), 0);
            }
            Buildings.TownHouse(g, new Vector3(-40, Hgt(-40, 127), 127), 180, 14f, 7.5f, 1, 0, true, 5).name = "茶屋";
            for (int i = 0; i < 3; i++) TeaSeat(g, new Vector3(-46 + i * 6, l1, 121.5f), 0);
            for (int i = 0; i < 4; i++) Toro(g, new Vector3(pond.center.x + (i % 2 == 0 ? -1 : 1) * (pond.width / 2 + 2f), l1, pond.center.y + (i < 2 ? -1 : 1) * (pond.height / 2 + 2f)), true, 0.8f);

            // 千年桜の丘
            var hc = Island.HillCenter;
            float hillTop = Island.TerraceHeight(hc.x, hc.y);
            Sakura(g, new Vector3(hc.x, hillTop - 0.4f, hc.y), 1.9f, true);
            Lamp(g, new Vector3(hc.x, hillTop + 4f, hc.y), new Color(1f, 0.65f, 0.8f), 30f, 3f);
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2 / 18;
                var p = hc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13f;
                var b = Group(g, "ぼんぼり", new Vector3(p.x, Island.TerraceHeight(p.x, p.y), p.y));
                Cyl(b, "柄", new Vector3(0, 0.8f, 0), 0.05f, 1.6f, WoodDark);
                Cyl(b, "火袋", new Vector3(0, 1.85f, 0), 0.22f, 0.6f, GlowSakura);
            }
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7 + 0.3f;
                var p = hc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 23f;
                if (Island.NearestWater(p.x, p.y) < 4 || Mathf.Abs(Island.TerraceHeight(p.x, p.y) - l1) > 0.3f) continue;
                if (Town.OnAnyStreet(p, 3f)) continue;
                Sakura(g, new Vector3(p.x, l1, p.y), 0.8f, false);
            }
            Petals(new Vector3(hc.x, hillTop + 16f, hc.y));

            // 藤棚・番傘の通り・風鈴の回廊
            Landmarks.Wisteria(g, _deco, new Vector3(WisteriaC.x, l1, WisteriaC.y), 90, 12f);
            var umb = new List<Vector2>();
            for (float x = -38; x >= -98; x -= 1f) umb.Add(new Vector2(x, 79.5f + (x + 40) * 0.033f));
            Landmarks.Umbrellas(_deco, umb, 6.6f);
            ChimePts.AddRange(Landmarks.ChimeCorridor(g, _deco, new Vector2(62, 42), new Vector2(86, 47)));

            // 上段
            _bell = Landmarks.BellTower(g, new Vector3(BellC.x, l2, BellC.y), 30);
            _festival = Landmarks.KaguraDen(g, new Vector3(KaguraC.x, l2, KaguraC.y), 210);
            Landmarks.Pagoda(g, new Vector3(PagodaC.x, l2, PagodaC.y), 15);
            Landmarks.Bamboo(g, BambooC, 13f, new Vector2(1, 1));
            _lookoutWarp = Landmarks.Lookout(g, _lookEdge, _lookOut);

            // 頂上：月の御殿
            var top = Group(g, "月の御殿", new Vector3(0, l3, 206));
            Box(top, "石畳", new Vector3(0, 0.04f, -6), new Vector3(30, 0.08f, 22), Parts.Paving);
            Buildings.TownHouse(top, new Vector3(0, 0, 4), 0, 24f, 12f, 2, 2, false, 9).name = "正殿";
            foreach (float x in new[] { -9f, 9f }) Toro(top, new Vector3(x, 0, -12f), true, 1.3f);
            Lamp(top, new Vector3(0, 6, -8), new Color(1f, 0.75f, 0.5f), 26f, 2.4f);

            // 屋形船
            for (int i = 0; i < 3; i++) Landmarks.Yakatabune(_deco, Island.Channels[1], 20f + i * 70f, i);

            // 大通りののぼり
            for (float z = 54; z < 100; z += 7f)
                foreach (float x in new[] { -6.5f, 6.5f })
                    if (!Town.OnAnyStreet(new Vector2(x, z), 0.2f) || Mathf.Abs(x) > 6.2f)
                        Buildings.Nobori(_deco, new Vector3(x, l1, z), x < 0 ? 90 : -90, (int)z);
            Marker("商店街のざわめき", new Vector3(0, l1 + 2, 78), CrowdPts);
            Marker("下町のざわめき", new Vector3(0, G + 2, 20), CrowdPts);
            Marker("上段のざわめき", new Vector3(0, l2 + 2, 140), CrowdPts);
        }

        static void Petals(Vector3 pos)
        {
            var petals = new GameObject("花びら");
            petals.transform.SetParent(_deco, false);
            petals.transform.position = pos;
            var ps = petals.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.9f), new Color(1f, 0.65f, 0.8f));
            main.gravityModifier = 0.03f;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 70;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(40, 6, 40);
            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(0.4f, 1.4f); vel.y = new ParticleSystem.MinMaxCurve(-0.9f, -0.5f); vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.6f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var rend = petals.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = Shaded("petal", "Tsukuyomi/Petal");
            ps.Play();
        }

        static void ScreenHall(Transform g, Vector3 pos, out StreamStudio studio)
        {
            var hall = Group(g, "大画面ビル", pos, 0);
            Box(hall, "建物", new Vector3(0, 11f, 0), new Vector3(8f, 22f, 30f), Lit("hall_dark", new Color(0.1f, 0.09f, 0.16f), 0.5f), true);
            Box(hall, "朱の帯", new Vector3(-4.05f, 2f, 0), new Vector3(0.2f, 1.2f, 30.2f), Vermilion);
            MeshObj(hall, "屋根", Roof(12f, 34f, 3.5f, 1.2f), new Vector3(0, 22.05f, 0), RoofTile);
            Box(hall, "画面の枠", new Vector3(-4.2f, 12.5f, 0), new Vector3(0.4f, 15.6f, 27.4f), Black);
            foreach (float z in new[] { -13.8f, 13.8f }) Box(hall, "光の枠", new Vector3(-4.45f, 12.5f, z), new Vector3(0.1f, 15.8f, 0.2f), GlowPink);
            foreach (float y in new[] { 4.7f, 20.3f }) Box(hall, "光の枠", new Vector3(-4.45f, y, 0), new Vector3(0.1f, 0.2f, 27.6f), GlowCyan);

            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(Gen + "/StreamRT.renderTexture");
            if (rt == null)
            {
                rt = new RenderTexture(1280, 720, 24) { name = "StreamRT", antiAliasing = 2 };
                AssetDatabase.CreateAsset(rt, Gen + "/StreamRT.renderTexture");
            }
            var screenMat = Shaded("stream_screen", "Tsukuyomi/Screen");
            screenMat.SetTexture("_MainTex", rt);
            var screen = Prim(PrimitiveType.Quad, hall, "大画面", new Vector3(-4.45f, 12.5f, 0), new Vector3(27f, 15.2f, 1), screenMat, false, new Vector3(0, 90, 0));
            screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var st = Group(_deco, "配信スタジオ（地下）", new Vector3(0, -300, 0));
            var back = Lit("studio_back", new Color(0.09f, 0.07f, 0.2f), 0.4f);
            Box(st, "床", new Vector3(0, -0.05f, 0), new Vector3(10, 0.1f, 8), Lit("studio_floor", new Color(0.18f, 0.14f, 0.3f), 0.7f));
            Box(st, "壁", new Vector3(0, 2.5f, 2.2f), new Vector3(10, 5, 0.2f), back);
            Box(st, "月", new Vector3(-1.4f, 2.6f, 2.05f), new Vector3(1.6f, 1.6f, 0.05f), GlowGold, false, new Vector3(0, 0, 45));
            Ball(st, "月の円", new Vector3(1.3f, 2.4f, 2.05f), new Vector3(1.5f, 1.5f, 0.05f), GlowGold);
            for (int i = 0; i < 6; i++)
                Box(st, "光の線", new Vector3(-3.5f + i * 1.4f, 2.5f, 2.08f), new Vector3(0.05f, 4.6f, 0.02f), i % 2 == 0 ? GlowPink : GlowCyan);
            var yachiyo = Doll.Build("ヤチヨ（配信）", DollLook.Yachiyo, ColorMat, st);
            yachiyo.root.transform.localPosition = new Vector3(0, 0, 0.3f);
            yachiyo.root.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var mendako = Ball(st, "メンダコ", new Vector3(0.75f, 1.55f, 0.3f), new Vector3(0.32f, 0.24f, 0.3f), ColorMat(new Color(1f, 0.6f, 0.75f)));
            var camGo = new GameObject("配信カメラ");
            camGo.transform.SetParent(st, false);
            camGo.transform.localPosition = new Vector3(0, 1.4f, -4.2f);
            camGo.transform.localRotation = Quaternion.Euler(4, 0, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt; cam.fieldOfView = 40; cam.nearClipPlane = 0.1f; cam.farClipPlane = 12f; cam.depth = -10;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.04f, 0.12f);
            var cdata = AddData(camGo);
            cdata.renderPostProcessing = false; cdata.renderShadows = false;
            foreach (var (p, c) in new[] { (new Vector3(-2, 3, -2), new Color(1f, 0.75f, 0.85f)), (new Vector3(2, 3, -2), new Color(0.7f, 0.85f, 1f)), (new Vector3(0, 2, -3.5f), Color.white) })
            {
                var l = new GameObject("スタジオの明かり").AddComponent<Light>();
                l.transform.SetParent(st, false);
                l.transform.localPosition = p;
                l.type = LightType.Point; l.range = 9; l.intensity = 3f; l.color = c;
            }
            studio = st.gameObject.AddComponent<StreamStudio>();
            studio.cam = cam;
            studio.yachiyo = yachiyo.root.GetComponent<DollAnimator>();
            studio.mendako = mendako.transform;
        }

        // ───────────────────────── 町並み ─────────────────────────

        static void Houses()
        {
            Physics.SyncTransforms();
            var g = Group(_env, "町並み", Vector3.zero);
            int seed = 0;
            int along = Town.FillAlongStreets(g, (s, i, c, y, rot, w, d) =>
            {
                seed++;
                float lv = y;
                bool main = s.name == "月見商店街" || s.name == "大門通り" || s.name == "東西通り";
                double r = Rng.NextDouble();
                if (s.level == 0)
                {
                    if (r < 0.15) Buildings.Kura(g, new Vector3(c.x, y, c.y), rot, Mathf.Min(w, 8f), Mathf.Min(d, 7f));
                    else if (r < 0.32) Buildings.Ryotei(g, new Vector3(c.x, y, c.y), rot, w, d, seed);
                    else Buildings.TownHouse(g, new Vector3(c.x, y, c.y), rot, w, d, r < 0.7 ? 2 : 1, Ri(0, 4), r < 0.8, seed);
                }
                else if (s.level == 1)
                {
                    int floors = r < 0.18 ? 3 : r < 0.68 ? 2 : 1;
                    if (!main && r > 0.9) Buildings.Kura(g, new Vector3(c.x, y, c.y), rot, Mathf.Min(w, 8f), Mathf.Min(d, 7f));
                    else Buildings.TownHouse(g, new Vector3(c.x, y, c.y), rot, w, d, floors, Ri(0, 4), main || r < 0.75, seed);
                }
                else
                {
                    if (r < 0.3) Buildings.Ryotei(g, new Vector3(c.x, y, c.y), rot, w, d, seed);
                    else Buildings.TownHouse(g, new Vector3(c.x, y, c.y), rot, w, d, r < 0.6 ? 2 : 1, r < 0.5 ? 2 : 0, r < 0.5, seed);
                }
                return true;
            });
            int gaps = Town.FillGaps(g, new Rect(-150, 0, 300, 300), 6000, (i, c, y, rot, w, d) =>
            {
                seed++;
                if (Rng.NextDouble() < 0.2) Buildings.Kura(g, new Vector3(c.x, y, c.y), rot, Mathf.Min(w, 7.5f), Mathf.Min(d, 7f));
                else Buildings.TownHouse(g, new Vector3(c.x, y, c.y), rot, w, d, Rng.NextDouble() < 0.5 ? 2 : 1, Ri(0, 4), false, seed);
                return true;
            });
            Debug.Log($"[Tsukuyomi] houses along streets: {along}, in gaps: {gaps}");

            // 通りの灯籠と提灯
            foreach (var s in Town.Streets)
            {
                for (int i = 4; i < s.pts.Count - 4; i += 9)
                {
                    var p = s.pts[i];
                    if (Island.NearestWater(p.x, p.y) < 2f || Island.Inside(p.x, p.y) < 3f) continue;
                    if (Mathf.Abs(Island.TerraceHeight(p.x, p.y) - L(Mathf.Max(0, s.level))) > 0.2f) continue;
                    var dir = (s.pts[i + 1] - s.pts[i - 1]).normalized;
                    var side = new Vector2(-dir.y, dir.x) * (s.half + 0.45f) * ((i / 9) % 2 == 0 ? 1 : -1);
                    var q = p + side;
                    if (Town.OnAnyStreet(q, 0.15f) || Island.NearestWater(q.x, q.y) < 1.5f) continue;
                    var q3 = new Vector3(q.x, Hgt(q.x, q.y), q.y);
                    if (Physics.CheckSphere(q3 + Vector3.up * 0.8f, 0.4f)) continue;
                    Toro(g, q3, i % 27 == 4, 0.75f);
                }
                if (s.name == "月見商店街" || s.name == "大門通り")
                    for (int i = 3; i < s.pts.Count - 3; i += 6)
                    {
                        var p = s.pts[i];
                        if (Island.NearestWater(p.x, p.y) < 2f) continue;
                        float y = Hgt(p.x, p.y);
                        Box(g, "綱", new Vector3(p.x, y + 5.6f, p.y), new Vector3(s.half * 2 + 1f, 0.04f, 0.04f), Black);
                        int n = Mathf.RoundToInt(s.half * 2 / 2.4f);
                        for (int k = 0; k < n; k++)
                            Chochin(g, new Vector3(p.x - s.half + 1.2f + k * 2.4f, y + 5.15f, p.y), (k + i) % 2 == 0 ? GlowRed : GlowWhite);
                    }
            }
        }

        // ───────────────────────── 滝 ─────────────────────────

        static void Waterfalls()
        {
            foreach (var ch in Island.Channels)
            {
                var pts = Island.Resample(ch.pts, 0.5f);
                for (int i = 1; i < pts.Count; i++)
                {
                    float a = Island.SurfaceNoWater(pts[i - 1].x, pts[i - 1].y), b = Island.SurfaceNoWater(pts[i].x, pts[i].y);
                    if (a - b < 1.5f) continue;
                    var bottom = new Vector3(pts[i].x, b - 0.6f, pts[i].y);
                    // 同じ滝の重なりは1つに
                    bool near = false;
                    foreach (var w in WaterPts) if (Vector3.Distance(w.position, bottom) < 6f) near = true;
                    if (near) continue;
                    Marker("滝の音", bottom, WaterPts);
                    var mist = new GameObject("滝のしぶき");
                    mist.transform.SetParent(_deco, false);
                    mist.transform.position = bottom;
                    var ps = mist.AddComponent<ParticleSystem>();
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                    main.startColor = new Color(0.85f, 0.95f, 1f, 0.35f);
                    main.maxParticles = 200;
                    var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(ch.half * 2, 0.5f, 2f);
                    var em = ps.emission; em.rateOverTime = 40;
                    mist.GetComponent<ParticleSystemRenderer>().sharedMaterial = Shaded("mist", "Tsukuyomi/Petal");
                    ps.Play();
                }
            }
            Marker("海の音", new Vector3(0, 1, -60), WaterPts);
        }

        static void Audio()
        {
            var go = new GameObject("音");
            var a = go.AddComponent<AmbientAudio>();
            a.waterPoints = WaterPts.ToArray();
            a.crowdPoints = CrowdPts.ToArray();
            a.chimePoints = ChimePts.ToArray();
            a.festivalPoint = _festival;
            a.bellPoint = _bell;
        }

        static Material SkyWarm => Landmarks.Holo("holo_sky_lantern", new Color(1f, 0.6f, 0.28f), 0.0f, 2.6f, 1.2f);
        static Material SkyPink => Landmarks.Holo("holo_sky_pink", new Color(1f, 0.45f, 0.75f), 0.0f, 2.6f, 1.2f);

        static void SkyLanterns()
        {
            var g = Group(_deco, "空に浮かぶ灯籠", Vector3.zero);
            for (int i = 0; i < 110; i++)
            {
                var pos = new Vector3(R(-160, 160), R(40, 85), R(-80, 300));
                var l = Group(g, "天灯", pos, R(0, 360));
                Box(l, "灯", Vector3.zero, new Vector3(0.7f, 0.95f, 0.7f) * R(0.8f, 1.4f), i % 7 == 0 ? SkyPink : SkyWarm);
                var f = l.gameObject.AddComponent<Floaty>();
                f.amplitude = R(0.6f, 1.6f); f.speed = R(0.15f, 0.4f); f.spin = R(-6, 6);
            }
        }

        static void Boundaries()
        {
            var g = Group(_env, "世界の果て", Vector3.zero);
            foreach (var (c, s) in new[]
            {
                (new Vector3(0, 10, -340), new Vector3(900, 40, 2)), (new Vector3(0, 10, 560), new Vector3(900, 40, 2)),
                (new Vector3(-420, 10, 110), new Vector3(2, 40, 900)), (new Vector3(420, 10, 110), new Vector3(2, 40, 900)),
            })
                Box(g, "見えない壁", c, s, Stone, true).GetComponent<Renderer>().enabled = false;
        }

        // ───────────────────────── 場所（名前・地図・ワープ） ─────────────────────────

        static void Zones()
        {
            var root = new GameObject("場所").transform;
            void Z(int order, string id, string title, string en, Vector2 center, Vector2 size, Color c, Vector3 warp, float yaw, bool warpable = true, float pitch = 14f)
            {
                var go = new GameObject(title);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(center.x, 0, center.y);
                var z = go.AddComponent<Zone>();
                z.order = order; z.id = id; z.title = title; z.english = en; z.size = size; z.mapColor = c;
                z.warpPos = warp; z.warpYaw = yaw; z.warpable = warpable; z.warpPitch = pitch;
            }
            Vector3 W(float x, float z) => new Vector3(x, Hgt(x, z) + 0.1f, z);
            Z(0, "torii", "海の大鳥居", "GREAT TORII", new Vector2(0, -72), new Vector2(40, 26), new Color(0.9f, 0.3f, 0.2f), new Vector3(0, G + 0.1f, -69), 0);
            Z(1, "stage", "ARライブステージ", "AR LIVE STAGE", new Vector2(0, -128), new Vector2(66, 66), new Color(0.6f, 0.4f, 1f), _stageWarp, 180, true, 6f);
            Z(2, "corridor", "海の回廊", "SEA CORRIDOR", new Vector2(0, -30), new Vector2(16, 60), new Color(0.8f, 0.4f, 0.3f), new Vector3(0, G + 0.1f, -40), 0, false);
            Z(3, "shitamachi", "水路の下町", "CANAL TOWN", new Vector2(0, 26), new Vector2(240, 46), new Color(0.4f, 0.65f, 0.85f), W(0, 12), 0);
            Z(4, "shopping", "月見商店街", "MOONLIGHT ARCADE", new Vector2(0, 78), new Vector2(60, 62), new Color(0.95f, 0.6f, 0.3f), W(0, 52), 0);
            Z(5, "plaza", "大画面広場", "SCREEN PLAZA", PlazaC, new Vector2(40, 34), new Color(0.9f, 0.4f, 0.7f), W(24, 100), 90, true, -6f);
            Z(6, "fishing", "釣り堀と茶屋", "FISHING POND", Island.Pond.center + new Vector2(0, 4), new Vector2(36, 34), new Color(0.3f, 0.7f, 0.6f), W(-40, 100), 0);
            Z(7, "sakura", "千年桜の丘", "THOUSAND-YEAR SAKURA", Island.HillCenter, new Vector2(48, 48), new Color(1f, 0.6f, 0.75f), W(-74, 127), 0, true, -16f);
            Z(8, "upper", "上段・五重塔", "UPPER TERRACE", new Vector2(10, 168), new Vector2(110, 90), new Color(0.75f, 0.55f, 0.35f), W(0, 131), 0);
            Z(9, "bamboo", "竹林の小道", "BAMBOO PATH", BambooC, new Vector2(28, 28), new Color(0.4f, 0.75f, 0.4f), W(-38, 150), 45);
            Z(10, "lookout", "懸造りの展望台", "CLIFF LOOKOUT", _lookEdge + _lookOut * 4f, new Vector2(24, 24), new Color(0.85f, 0.5f, 0.3f), _lookoutWarp, Mathf.Atan2(_lookOut.x, _lookOut.y) * Mathf.Rad2Deg, true, 4f);
            Z(11, "palace", "月の御殿", "MOON PALACE", new Vector2(0, 204), new Vector2(54, 54), new Color(0.95f, 0.85f, 0.4f), W(0, 184), 0);
        }
    }
}
