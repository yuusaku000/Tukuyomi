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
    /// ツクヨミの世界をまるごと組み立てて Assets/Scenes/Tsukuyomi.unity に保存する。
    /// 何度実行しても同じ世界になる（乱数の種が固定）。
    ///
    /// 配置（上から見て、+z が北）
    ///   海の大鳥居 (0,-82) ─ 高舞台(スポーン) ─ 海の回廊 ─ 大門 (0,6)
    ///   南の島: 月見商店街（大通りと路地）・大画面広場(東)・釣り堀と茶屋(西)
    ///   天の川 z=100〜114（反り橋が3本）
    ///   北の島: 千年桜の丘(西)・ライブステージ(東)・月の御殿(北)
    /// </summary>
    public static class WorldBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tsukuyomi.unity";
        const float G = 1.2f; // 島の地面の高さ

        static Transform _env, _deco;

        [MenuItem("Tsukuyomi/ワールドを組み立てる")]
        public static void BuildAll()
        {
            Rng = new System.Random(8000);
            Begin();
            SaveMesh("doll_robe", Doll.Cone);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _env = new GameObject("ワールド").transform;
            _deco = new GameObject("動くもの").transform;

            var (sun, moon) = Lights();
            Sea();
            Entrance();
            Island();
            ShoppingStreet();
            ScreenPlaza(out var studio);
            FishingArea();
            River();
            SakuraHill();
            LiveStage();
            Palace();
            SkyLanterns();
            Boundaries();
            var player = Player(out var cam, out var spawn);
            Sky(sun, moon, cam);
            Post();
            Zones();

            var sys = new GameObject("ツクヨミ");
            var session = sys.AddComponent<GameSession>();
            session.player = player; session.cam = cam; session.spawn = spawn;
            var hud = sys.AddComponent<Hud>();
            hud.player = player; hud.cam = cam;

            foreach (var t in _env.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var settings = new LightingSettings { name = "TsukuyomiLighting", bakedGI = false, realtimeGI = false };
            string lsPath = Gen + "/Lighting.lighting";
            AssetDatabase.DeleteAsset(lsPath);
            AssetDatabase.CreateAsset(settings, lsPath);
            Lightmapping.lightingSettings = settings;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Tsukuyomi] world built: " + _env.GetComponentsInChildren<Renderer>().Length + " renderers");
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
            Prim(PrimitiveType.Quad, _env, "海の底", new Vector3(0, -3f, 80), new Vector3(5000, 5000, 1), Lit("seabed", new Color(0.12f, 0.2f, 0.22f)), false, new Vector3(90, 0, 0));
            var bed = Box(_env, "海の底（足場）", new Vector3(0, -1.4f, 80), new Vector3(1000, 1f, 1000), Sand, true);
            bed.GetComponent<Renderer>().enabled = false;
        }

        static void Entrance()
        {
            var g = Group(_env, "大鳥居と海の回廊", Vector3.zero);
            Torii(g, new Vector3(0, 0, -82), 1f);

            // 高舞台（スポーン）
            Box(g, "高舞台", new Vector3(0, G - 0.5f, -68), new Vector3(16, 1f, 12), Wood, true);
            Box(g, "高舞台のふち", new Vector3(0, G - 0.55f, -68), new Vector3(16.4f, 0.9f, 12.4f), Vermilion);
            for (int i = 0; i < 6; i++)
            {
                float x = -7.5f + i * 3f;
                foreach (float z in new[] { -73.8f, -62.2f })
                    Cyl(g, "舞台の脚", new Vector3(x, -0.9f, z), 0.25f, 3.6f, Vermilion);
            }
            foreach (float x in new[] { -8f, 8f })
            {
                Box(g, "高欄", new Vector3(x, G + 0.9f, -68), new Vector3(0.14f, 0.12f, 12f), Vermilion);
                var fence = Box(g, "見えない柵", new Vector3(x, G + 0.7f, -68), new Vector3(0.3f, 1.4f, 12f), Vermilion, true);
                fence.GetComponent<Renderer>().enabled = false;
                for (int k = 0; k < 5; k++) Cyl(g, "高欄の柱", new Vector3(x, G + 0.5f, -73.5f + k * 2.75f), 0.07f, 1f, Vermilion);
            }
            Box(g, "高欄", new Vector3(-4.5f, G + 0.9f, -74f), new Vector3(7f, 0.12f, 0.14f), Vermilion);
            Box(g, "高欄", new Vector3(4.5f, G + 0.9f, -74f), new Vector3(7f, 0.12f, 0.14f), Vermilion);
            var front = Box(g, "見えない柵", new Vector3(0, G + 0.7f, -74.1f), new Vector3(16f, 1.4f, 0.3f), Vermilion, true);
            front.GetComponent<Renderer>().enabled = false;
            Toro(g, new Vector3(-6.5f, G, -64.5f), true);
            Toro(g, new Vector3(6.5f, G, -64.5f), true);
            Box(g, "光の線", new Vector3(0, G + 0.02f, -68), new Vector3(0.12f, 0.02f, 11f), GlowCyan);
            Box(g, "光の線", new Vector3(0, G + 0.02f, -68), new Vector3(15f, 0.02f, 0.12f), GlowCyan);

            // 海の回廊
            const float w = 7f;
            Box(g, "回廊の床", new Vector3(0, G - 0.5f, -31), new Vector3(w, 1f, 62f), Wood, true);
            Box(g, "回廊の桁", new Vector3(0, G - 1.1f, -31), new Vector3(w + 0.3f, 0.4f, 62f), Vermilion);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (w / 2 - 0.1f);
                Box(g, "高欄", new Vector3(x, G + 0.9f, -31), new Vector3(0.14f, 0.12f, 62f), Vermilion);
                Box(g, "高欄", new Vector3(x, G + 0.45f, -31), new Vector3(0.1f, 0.1f, 62f), Vermilion);
                var fence = Box(g, "見えない柵", new Vector3(x, G + 0.7f, -31), new Vector3(0.3f, 1.4f, 62f), Vermilion, true);
                fence.GetComponent<Renderer>().enabled = false;
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
        }

        // ───────────────────────── 島 ─────────────────────────

        static void Island()
        {
            var g = Group(_env, "島", Vector3.zero);
            Box(g, "南の島", new Vector3(0, G - 2.1f, 50), new Vector3(260, 4.2f, 100), Sand, true);
            Box(g, "北の島", new Vector3(0, G - 2.1f, 182), new Vector3(260, 4.2f, 136), Sand, true);
            // 石垣（島のふち）
            Box(g, "石垣", new Vector3(0, G - 1.6f, -0.2f), new Vector3(260.6f, 3.4f, 0.8f), Stone);
            Box(g, "石垣", new Vector3(0, G - 1.6f, 250.2f), new Vector3(260.6f, 3.4f, 0.8f), Stone);
            foreach (float x in new[] { -130.2f, 130.2f })
            {
                Box(g, "石垣", new Vector3(x, G - 1.6f, 50), new Vector3(0.8f, 3.4f, 100.6f), Stone);
                Box(g, "石垣", new Vector3(x, G - 1.6f, 182), new Vector3(0.8f, 3.4f, 136.6f), Stone);
            }
            // 島のまわりの見えない柵（落ちないように。回廊の入口だけあける）
            void Fence(Vector3 c, Vector3 s) => Box(g, "見えない柵", c, s, Stone, true).GetComponent<Renderer>().enabled = false;
            Fence(new Vector3(-67, G + 1, -0.2f), new Vector3(126, 2, 0.6f));
            Fence(new Vector3(67, G + 1, -0.2f), new Vector3(126, 2, 0.6f));
            Fence(new Vector3(0, G + 1, 250.2f), new Vector3(260, 2, 0.6f));
            foreach (float x in new[] { -130.2f, 130.2f })
            {
                Fence(new Vector3(x, G + 1, 50), new Vector3(0.6f, 2, 100));
                Fence(new Vector3(x, G + 1, 182), new Vector3(0.6f, 2, 136));
            }

            // 大門
            var gate = Group(g, "大門", new Vector3(0, G, 5));
            foreach (float x in new[] { -6.5f, -2.6f, 2.6f, 6.5f })
                foreach (float z in new[] { -2f, 2f })
                    Cyl(gate, "柱", new Vector3(x, 3.2f, z), 0.32f, 6.4f, Vermilion, Mathf.Abs(x) > 5);
            Box(gate, "貫", new Vector3(0, 5.6f, -2), new Vector3(14.5f, 0.5f, 0.5f), Vermilion);
            Box(gate, "貫", new Vector3(0, 5.6f, 2), new Vector3(14.5f, 0.5f, 0.5f), Vermilion);
            MeshObj(gate, "屋根", Roof(17, 8, 2.2f, 0.9f), new Vector3(0, 6.4f, 0), RoofCopper);
            Box(gate, "二階", new Vector3(0, 8.3f, 0), new Vector3(10f, 2.6f, 4f), WhiteWall);
            Box(gate, "二階格子", new Vector3(0, 8.4f, -2.03f), new Vector3(9f, 1.6f, 0.06f), Lattice);
            Box(gate, "高欄", new Vector3(0, 7.3f, -2.6f), new Vector3(11f, 0.14f, 0.14f), Vermilion);
            MeshObj(gate, "屋根2", Roof(15, 7.5f, 3.2f, 1f), new Vector3(0, 9.6f, 0), RoofCopper);
            Box(gate, "棟", new Vector3(0, 12.9f, 0), new Vector3(8.2f, 0.4f, 0.45f), Black);
            Box(gate, "扁額", new Vector3(0, 8.4f, -2.2f), new Vector3(2.6f, 1.2f, 0.12f), Black);
            Box(gate, "扁額金", new Vector3(0, 8.4f, -2.28f), new Vector3(2.2f, 0.9f, 0.04f), GlowGold);
            Chochin(gate, new Vector3(-4.5f, 4.6f, -2.4f), GlowRed);
            Chochin(gate, new Vector3(4.5f, 4.6f, -2.4f), GlowRed);
            Lamp(gate, new Vector3(0, 4.5f, -3f), new Color(1f, 0.6f, 0.4f), 14f, 2.5f);

            // 松（島のふちに少し）
            for (int i = 0; i < 18; i++)
            {
                float x = R(-125, 125), z = R(2, 8);
                if (Mathf.Abs(x) < 14) continue;
                Pine(g, new Vector3(x, G, z), R(0.9f, 1.4f));
            }
        }

        static void Paving(Transform g, float x0, float x1, float z0, float z1, Material m = null) =>
            Box(g, "石畳", new Vector3((x0 + x1) / 2, G + 0.02f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.06f, z1 - z0), m ?? Parts.Paving);

        // ───────────────────────── 月見商店街 ─────────────────────────

        static void ShoppingStreet()
        {
            var g = Group(_env, "月見商店街", Vector3.zero);
            Paving(g, -6, 6, 8, 100);           // 大通り
            Paving(g, -110, 110, 40, 48);       // 東西通り
            Paving(g, -50, 50, 70, 75);         // 細い横丁
            Paving(g, -21, -17, 8, 100);        // 裏路地（西）
            Paving(g, 17, 21, 8, 100);          // 裏路地（東）
            foreach (float x in new[] { -5.6f, 5.6f })
                Box(g, "光の線", new Vector3(x, G + 0.06f, 54), new Vector3(0.1f, 0.02f, 92), GlowCyan);

            // 大通りの両側の店（1列目）。正面は大通り向き
            foreach (int side in new[] { -1, 1 })
            {
                float z = 11;
                int n = 0;
                while (z < 97)
                {
                    float w = R(7.5f, 11f);
                    if (z + w > 99) break;
                    bool cross1 = z + w > 38 && z < 50, cross2 = z + w > 68 && z < 77;
                    if (cross1) { z = 50.5f; continue; }
                    if (cross2) { z = 77.5f; continue; }
                    float d = 9.5f;
                    bool two = n % 3 == 1;
                    Building(g, "店", new Vector3(side * (6.6f + d / 2 + 0.4f), G, z + w / 2), side < 0 ? 90 : -90, w, d, R(3.6f, 4.4f), n % 4 == 3 ? 2 : 1, true, two);
                    z += w + 0.7f;
                    n++;
                }
            }
            // 裏路地ぞいの店・町家（2列目）。正面は裏路地向き
            foreach (int side in new[] { -1, 1 })
            {
                float z = 10;
                int n = 0;
                while (z < 97)
                {
                    float w = R(7f, 10f);
                    if (z + w > 99) break;
                    if (z + w > 38 && z < 50) { z = 50.5f; continue; }
                    if (side < 0 && z + w > 68 && z < 77) { z = 77.5f; continue; }
                    if (side > 0 && z + w > 68 && z < 77) { z = 77.5f; continue; }
                    float d = 9f;
                    Building(g, "町家", new Vector3(side * (21.6f + d / 2), G, z + w / 2), side < 0 ? 90 : -90, w, d, R(3.4f, 4f), n % 2, n % 3 != 2);
                    z += w + 0.6f;
                    n++;
                }
            }
            // 東西通りの南側の店（正面は北＝通り向き）。大通りから広場・釣り堀へ続く
            foreach (int side in new[] { -1, 1 })
            {
                float x = 33;
                int n = 0;
                while (x < 108)
                {
                    float w = R(8f, 11f);
                    if (x + w > 110) break;
                    Building(g, "店", new Vector3(side * (x + w / 2), G, 39.4f - 5f), 0, w, 9.5f, R(3.6f, 4.3f), n % 3, true, n % 4 == 2);
                    x += w + 0.7f;
                    n++;
                }
            }
            // 南の住宅地（入り組んだ町並み）
            foreach (int side in new[] { -1, 1 })
                for (float x = 34; x < 120; x += 15)
                    for (float z = 10; z < 26; z += 14)
                    {
                        if (Rng.NextDouble() < 0.2) continue;
                        float rot = new[] { 0f, 90f, 180f, -90f }[Ri(0, 4)];
                        Building(g, "町家", new Vector3(side * (x + R(-1f, 1f)), G, z + R(-1f, 1f)), rot, R(7f, 9.5f), R(7f, 8.5f), R(3.2f, 3.8f), Ri(0, 2), Rng.NextDouble() < 0.35);
                    }
            // 横丁（細い道）ぞいの小さな店：横丁の北側
            foreach (int side in new[] { -1, 1 })
                for (float x = 33; x < 46; x += 8.6f)
                    Building(g, "屋台", new Vector3(side * (x + 4), G, 80.5f), 180, 7.6f, 7f, 3.2f, 1, true);

            // 提灯の列（大通りの上）
            for (float z = 12; z < 98; z += 6.5f)
            {
                if (z > 38 && z < 50) continue;
                Box(g, "綱", new Vector3(0, G + 5.6f, z), new Vector3(12.5f, 0.04f, 0.04f), Black);
                for (int i = 0; i < 5; i++)
                    Chochin(g, new Vector3(-4.8f + i * 2.4f, G + 5.15f - Mathf.Abs(i - 2) * -0.12f, z), (i + (int)z) % 2 == 0 ? GlowRed : GlowWhite);
            }
            // 灯籠と夜の明かり
            int lamp = 0;
            for (float z = 14; z < 98; z += 10)
                foreach (float x in new[] { -5.2f, 5.2f })
                {
                    if (z > 38 && z < 50) continue;
                    Toro(g, new Vector3(x, G, z), lamp++ % 2 == 0, 0.8f);
                }
            for (float x = -100; x <= 100; x += 20)
                if (Mathf.Abs(x) > 10) Toro(g, new Vector3(x, G, 47.2f), true, 0.85f);

            // 浮かぶホログラムの看板
            var holo = Group(_deco, "ホロ看板", Vector3.zero);
            foreach (var (pos, mat) in new[] { (new Vector3(0, G + 9.5f, 30), GlowPink), (new Vector3(0, G + 9f, 62), GlowCyan), (new Vector3(0, G + 9.5f, 90), GlowGold) })
            {
                var sign = Group(holo, "看板", pos);
                Box(sign, "枠", new Vector3(0, 0, 0), new Vector3(7f, 0.12f, 0.12f), mat);
                Box(sign, "枠", new Vector3(0, -2.4f, 0), new Vector3(7f, 0.12f, 0.12f), mat);
                Box(sign, "枠", new Vector3(-3.5f, -1.2f, 0), new Vector3(0.12f, 2.5f, 0.12f), mat);
                Box(sign, "枠", new Vector3(3.5f, -1.2f, 0), new Vector3(0.12f, 2.5f, 0.12f), mat);
                Box(sign, "月", new Vector3(0, -1.2f, 0), new Vector3(1.4f, 1.4f, 0.1f), GlowGold, false, new Vector3(0, 0, 45));
                sign.gameObject.AddComponent<Floaty>().amplitude = 0.35f;
            }
        }

        // ───────────────────────── 大画面広場（ヤチヨの配信） ─────────────────────────

        static void ScreenPlaza(out StreamStudio studio)
        {
            var g = Group(_env, "大画面広場", Vector3.zero);
            Paving(g, 50, 112, 48, 96);
            // 広場のまわりの店（北と南）
            for (float x = 52; x < 100; x += 11.5f)
            {
                Building(g, "店", new Vector3(x + 5, G, 95f), 180, 10.5f, 9f, 4f, 2, true, x > 70 && x < 80);
            }
            // 大画面の建物
            var hall = Group(g, "大画面ビル", new Vector3(118, G, 72));
            Box(hall, "建物", new Vector3(0, 13f, 0), new Vector3(10f, 26f, 40f), Lit("hall_dark", new Color(0.1f, 0.09f, 0.16f), 0.5f), true);
            Box(hall, "朱の帯", new Vector3(-5.05f, 2f, 0), new Vector3(0.2f, 1.2f, 40.2f), Vermilion);
            MeshObj(hall, "屋根", Roof(14f, 44f, 4f, 1.2f), new Vector3(0, 26.05f, 0), RoofTile);
            Box(hall, "画面の枠", new Vector3(-5.2f, 14.5f, 0), new Vector3(0.4f, 18.6f, 32.4f), Black);
            foreach (float z in new[] { -16.3f, 16.3f })
                Box(hall, "光の枠", new Vector3(-5.45f, 14.5f, z), new Vector3(0.1f, 18.8f, 0.2f), GlowPink);
            foreach (float y in new[] { 5.2f, 23.8f })
                Box(hall, "光の枠", new Vector3(-5.45f, y, 0), new Vector3(0.1f, 0.2f, 32.6f), GlowCyan);

            // 地下のスタジオ（大画面に映す）
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(Gen + "/StreamRT.renderTexture");
            if (rt == null)
            {
                rt = new RenderTexture(1280, 720, 24) { name = "StreamRT", antiAliasing = 2 };
                AssetDatabase.CreateAsset(rt, Gen + "/StreamRT.renderTexture");
            }
            var screenMat = Shaded("stream_screen", "Tsukuyomi/Screen");
            screenMat.SetTexture("_MainTex", rt);
            var screen = Prim(PrimitiveType.Quad, hall, "大画面", new Vector3(-5.45f, 14.5f, 0), new Vector3(32f, 18f, 1), screenMat, false, new Vector3(0, 90, 0));
            screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var st = Group(_deco, "配信スタジオ（地下）", new Vector3(0, -300, 0));
            var back = Lit("studio_back", new Color(0.09f, 0.07f, 0.2f), 0.4f);
            Box(st, "床", new Vector3(0, -0.05f, 0), new Vector3(10, 0.1f, 8), Lit("studio_floor", new Color(0.18f, 0.14f, 0.3f), 0.7f));
            Box(st, "壁", new Vector3(0, 2.5f, 2.2f), new Vector3(10, 5, 0.2f), back);
            Box(st, "月", new Vector3(-1.4f, 2.6f, 2.05f), new Vector3(1.6f, 1.6f, 0.05f), GlowGold, false, new Vector3(0, 0, 45));
            Ball(st, "月の円", new Vector3(1.3f, 2.4f, 2.05f), new Vector3(1.5f, 1.5f, 0.05f), GlowGold);
            for (int i = 0; i < 6; i++)
                Box(st, "光の線", new Vector3(-3.5f + i * 1.4f, 2.5f, 2.08f), new Vector3(0.05f, 4.6f, 0.02f), i % 2 == 0 ? GlowPink : GlowCyan);
            Box(st, "光の線", new Vector3(0, 0.6f, 2.08f), new Vector3(9.6f, 0.06f, 0.02f), GlowPink);
            var yachiyo = Doll.Build("ヤチヨ（配信）", DollLook.Yachiyo, ColorMat, st);
            yachiyo.root.transform.localPosition = new Vector3(0, 0, 0.3f);
            yachiyo.root.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var mendako = Ball(st, "メンダコ", new Vector3(0.75f, 1.55f, 0.3f), new Vector3(0.32f, 0.24f, 0.3f), ColorMat(new Color(1f, 0.6f, 0.75f)));
            var camGo = new GameObject("配信カメラ");
            camGo.transform.SetParent(st, false);
            camGo.transform.localPosition = new Vector3(0, 1.4f, -4.2f);
            camGo.transform.localRotation = Quaternion.Euler(4, 0, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = 40;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 12f;
            cam.depth = -10;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.12f);
            var cdata = AddData(camGo);
            cdata.renderPostProcessing = false;
            cdata.renderShadows = false;
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

            // 広場の灯籠・ベンチ・見物人の場所
            for (float z = 52; z <= 92; z += 10)
            {
                Toro(g, new Vector3(56, G, z), true, 0.85f);
            }
            for (int i = 0; i < 5; i++)
            {
                var b = Group(g, "長椅子", new Vector3(80, G, 56 + i * 8), 90);
                Box(b, "座面", new Vector3(0, 0.45f, 0), new Vector3(4f, 0.12f, 0.7f), Wood, true);
                Box(b, "毛氈", new Vector3(0, 0.52f, 0), new Vector3(4f, 0.03f, 0.72f), Lit("felt_red", new Color(0.75f, 0.1f, 0.12f), 0.05f));
                for (int k = 0; k < 2; k++) Box(b, "脚", new Vector3(k == 0 ? -1.7f : 1.7f, 0.2f, 0), new Vector3(0.12f, 0.4f, 0.6f), WoodDark);
            }
        }

        // ───────────────────────── 釣り堀と茶屋 ─────────────────────────

        static void FishingArea()
        {
            var g = Group(_env, "釣り堀と茶屋", Vector3.zero);
            Paving(g, -112, -50, 48, 96, Lit("gravel", new Color(0.74f, 0.72f, 0.66f), 0.05f));
            // 釣り堀（石の枡）
            var pond = Group(g, "釣り堀", new Vector3(-84, G, 72));
            float pw = 30, pd = 20;
            foreach (float side in new[] { -1f, 1f })
            {
                Box(pond, "縁", new Vector3(0, 0.4f, side * (pd / 2 + 0.4f)), new Vector3(pw + 1.6f, 0.8f, 0.8f), Stone, true);
                Box(pond, "縁", new Vector3(side * (pw / 2 + 0.4f), 0.4f, 0), new Vector3(0.8f, 0.8f, pd), Stone, true);
                Box(pond, "縁の木", new Vector3(0, 0.83f, side * (pd / 2 + 0.4f)), new Vector3(pw + 1.6f, 0.06f, 0.9f), Wood);
                Box(pond, "縁の木", new Vector3(side * (pw / 2 + 0.4f), 0.83f, 0), new Vector3(0.9f, 0.06f, pd), Wood);
            }
            Box(pond, "底", new Vector3(0, 0.05f, 0), new Vector3(pw, 0.1f, pd), Lit("pond_bed", new Color(0.12f, 0.22f, 0.2f)));
            var water = Shaded("water_pond", "Tsukuyomi/Water");
            water.SetColor("_Deep", new Color(0.05f, 0.22f, 0.22f));
            water.SetColor("_Shallow", new Color(0.12f, 0.45f, 0.4f));
            water.SetFloat("_Scale", 0.35f);
            water.SetFloat("_Speed", 0.4f);
            water.SetFloat("_Alpha", 0.8f);
            water.SetFloat("_Grid", 0.4f);
            Prim(PrimitiveType.Quad, pond, "水", new Vector3(0, 0.62f, 0), new Vector3(pw, pd, 1), water, false, new Vector3(90, 0, 0));
            var block = Box(pond, "見えない蓋", new Vector3(0, 1.3f, 0), new Vector3(pw, 1f, pd), Stone, true);
            block.GetComponent<Renderer>().enabled = false;
            // 鯉の影（あとで釣りに使う）
            for (int i = 0; i < 14; i++)
            {
                var koi = Ball(pond, "鯉", new Vector3(R(-12, 12), 0.45f, R(-8, 8)), new Vector3(0.35f, 0.15f, 0.9f),
                    ColorMat(i % 3 == 0 ? new Color(1f, 0.45f, 0.15f) : i % 3 == 1 ? new Color(0.95f, 0.95f, 0.9f) : new Color(0.9f, 0.2f, 0.15f)));
                koi.transform.localRotation = Quaternion.Euler(0, R(0, 360), 0);
            }
            for (int i = 0; i < 4; i++)
                Toro(pond, new Vector3((i % 2 == 0 ? -1 : 1) * (pw / 2 + 2f), 0, (i < 2 ? -1 : 1) * (pd / 2 + 2f)), true, 0.8f);

            // 茶屋と休憩所
            Building(g, "茶屋", new Vector3(-84, G, 92), 180, 14, 7.5f, 3.6f, 0, true, false, new Color(0.15f, 0.35f, 0.25f));
            for (int i = 0; i < 3; i++) TeaSeat(g, new Vector3(-90 + i * 6, G, 85.5f), 0);
            var az = Group(g, "東屋", new Vector3(-58, G, 60));
            Box(az, "床", new Vector3(0, 0.25f, 0), new Vector3(7, 0.5f, 7), Wood, true);
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { -3f, 3f })
                    Cyl(az, "柱", new Vector3(x, 2.1f, z), 0.15f, 3.2f, Vermilion);
            MeshObj(az, "屋根", Roof(9.5f, 9.5f, 3.2f, 0.8f), new Vector3(0, 3.7f, 0), RoofBark);
            Ball(az, "宝珠", new Vector3(0, 7.0f, 0), Vector3.one * 0.5f, Gold);
            Box(az, "腰掛け", new Vector3(0, 0.75f, -2.6f), new Vector3(5f, 0.1f, 0.6f), WoodDark, true);
            Box(az, "腰掛け", new Vector3(0, 0.75f, 2.6f), new Vector3(5f, 0.1f, 0.6f), WoodDark, true);
            for (int i = 0; i < 8; i++) Pine(g, new Vector3(R(-110, -54), G, R(50, 58)), R(0.8f, 1.2f));
        }

        // ───────────────────────── 天の川と橋 ─────────────────────────

        static void River()
        {
            var g = Group(_env, "天の川", Vector3.zero);
            var bed = Box(g, "川底", new Vector3(0, -1.0f, 107), new Vector3(262, 1f, 14), Lit("river_bed", new Color(0.18f, 0.24f, 0.25f)), true);
            var water = Shaded("water_river", "Tsukuyomi/Water");
            water.SetColor("_Deep", new Color(0.05f, 0.2f, 0.32f));
            water.SetColor("_Shallow", new Color(0.12f, 0.5f, 0.6f));
            water.SetFloat("_Scale", 0.25f);
            water.SetVector("_Flow", new Vector4(0.35f, 0, 0, 0));
            Prim(PrimitiveType.Quad, g, "川", new Vector3(0, G - 0.8f, 107), new Vector3(262, 14, 1), water, false, new Vector3(90, 0, 0));
            Box(g, "護岸", new Vector3(0, G - 1.1f, 100.25f), new Vector3(260, 2.2f, 0.5f), Stone);
            Box(g, "護岸", new Vector3(0, G - 1.1f, 113.75f), new Vector3(260, 2.2f, 0.5f), Stone);
            foreach (float z in new[] { 99.6f, 114.4f })
            {
                for (float x = -126; x <= 126; x += 3)
                {
                    if (Mathf.Abs(x) < 6 || Mathf.Abs(x - 75) < 4 || Mathf.Abs(x + 75) < 4) continue;
                    Cyl(g, "柵", new Vector3(x, G + 0.45f, z), 0.06f, 0.9f, WoodDark);
                }
                var fence = Box(g, "見えない柵", new Vector3(0, G + 0.7f, z), new Vector3(260, 1.4f, 0.3f), Stone, true);
                fence.GetComponent<Renderer>().enabled = false;
            }
            // 柵の切れ目（橋の入口）をあける：見えない柵を3つに分けなおす
            Object.DestroyImmediate(g.Find("見えない柵").gameObject);
            Object.DestroyImmediate(g.Find("見えない柵").gameObject);
            foreach (float z in new[] { 99.6f, 114.4f })
                foreach (var (x0, x1) in new[] { (-130f, -79f), (-71f, -4.5f), (4.5f, 71f), (79f, 130f) })
                    Box(g, "見えない柵", new Vector3((x0 + x1) / 2, G + 0.7f, z), new Vector3(x1 - x0, 1.4f, 0.3f), Stone, true).GetComponent<Renderer>().enabled = false;

            Bridge(g, "大反り橋", new Vector3(0, G, 96), new Vector3(0, G, 118), 8f, 2.6f, 16);
            Bridge(g, "西の橋", new Vector3(-75, G, 97), new Vector3(-75, G, 117), 5f, 2f, 12);
            Bridge(g, "東の橋", new Vector3(75, G, 97), new Vector3(75, G, 117), 5f, 2f, 12);
            for (int i = 0; i < 16; i++)
            {
                float x = -122 + i * 16 + R(-2, 2);
                if (Mathf.Abs(x) < 10 || Mathf.Abs(Mathf.Abs(x) - 75) < 8) continue;
                Willow(g, new Vector3(x, G, i % 2 == 0 ? 97.5f : 116.5f), R(0.9f, 1.15f));
            }
            // 灯籠流し（川面の小さな明かり）
            var lights = Group(_deco, "灯籠流し", Vector3.zero);
            for (int i = 0; i < 40; i++)
            {
                var l = Group(lights, "流し灯籠", new Vector3(R(-125, 125), G - 0.72f, R(101.5f, 112.5f)), R(0, 360));
                Box(l, "台", Vector3.zero, new Vector3(0.45f, 0.08f, 0.45f), Wood);
                Box(l, "灯", new Vector3(0, 0.2f, 0), new Vector3(0.32f, 0.32f, 0.32f), GlowWarm);
                var f = l.gameObject.AddComponent<Floaty>();
                f.amplitude = 0.04f; f.speed = 1.2f; f.spin = R(-8, 8);
            }
        }

        // ───────────────────────── 北の島 ─────────────────────────

        static void SakuraHill()
        {
            var g = Group(_env, "千年桜の丘", Vector3.zero);
            var hill = MeshObj(g, "丘", Hill(42f, 8f), new Vector3(-70, G, 178), Grass, true);
            Paving(g, -100, -40, 120, 128, Lit("gravel", new Color(0.74f, 0.72f, 0.66f), 0.05f));
            Sakura(g, new Vector3(-70, G + 7.6f, 178), 2.2f, true);
            Lamp(g, new Vector3(-70, G + 12f, 178), new Color(1f, 0.65f, 0.8f), 30f, 3f);
            // 丘のまわりのぼんぼり
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16;
                var p = new Vector3(-70 + Mathf.Cos(a) * 30f, 0, 178 + Mathf.Sin(a) * 30f);
                float y = G + 8f * (1 - Ts.Smooth(0f, 1f, 30f / 42f)) - 0.6f * (30f / 42f);
                var b = Group(g, "ぼんぼり", new Vector3(p.x, y, p.z));
                Cyl(b, "柄", new Vector3(0, 0.8f, 0), 0.05f, 1.6f, WoodDark);
                Cyl(b, "火袋", new Vector3(0, 1.85f, 0), 0.22f, 0.6f, GlowSakura);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2 / 6 + 0.4f;
                if (Mathf.Sin(a) < -0.6f) a += 0.9f; // 南（入口）側はあけておく
                Sakura(g, new Vector3(-70 + Mathf.Cos(a) * 46f, G, 178 + Mathf.Sin(a) * 46f), 0.8f, false);
            }

            // 花びら
            var petals = new GameObject("花びら");
            petals.transform.SetParent(_deco, false);
            petals.transform.position = new Vector3(-70, G + 22f, 178);
            var ps = petals.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10; main.loop = true;
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
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }

        static void LiveStage()
        {
            var g = Group(_env, "ライブステージ", Vector3.zero);
            Paving(g, 40, 104, 140, 192, Lit("plaza_dark", new Color(0.3f, 0.28f, 0.34f), 0.4f));
            for (float x = 44; x <= 100; x += 8)
                Box(g, "光の線", new Vector3(x, G + 0.06f, 166), new Vector3(0.1f, 0.02f, 50f), GlowCyan);
            var st = Group(g, "舞台", new Vector3(72, G, 202));
            Box(st, "舞台", new Vector3(0, 1f, 0), new Vector3(40, 2f, 18), Lit("stage_floor", new Color(0.12f, 0.1f, 0.18f), 0.85f), true);
            Box(st, "ふち", new Vector3(0, 1.95f, -9f), new Vector3(40.2f, 0.12f, 0.2f), GlowPink);
            for (int i = 0; i < 8; i++)
                Box(st, "階段", new Vector3(-17f, (i + 1) * 0.125f, -9.175f - (7 - i) * 0.35f), new Vector3(4f, (i + 1) * 0.25f, 0.35f), Stone, true);
            // 舞台の後ろの大きな枠（鳥居の形のトラス）
            foreach (float x in new[] { -21f, 21f })
                Box(st, "トラス", new Vector3(x, 9f, 4f), new Vector3(1.2f, 18f, 1.2f), Lit("truss", new Color(0.35f, 0.35f, 0.4f), 0.7f, 0.8f), true);
            Box(st, "トラス", new Vector3(0, 17.5f, 4f), new Vector3(44f, 1.2f, 1.2f), Lit("truss", new Color(0.35f, 0.35f, 0.4f), 0.7f, 0.8f));
            Box(st, "笠木", new Vector3(0, 18.6f, 4f), new Vector3(48f, 1.2f, 2f), Vermilion);
            Box(st, "後ろの画面", new Vector3(0, 9.5f, 8.5f), new Vector3(36f, 14f, 0.4f), Lit("led_off", new Color(0.04f, 0.04f, 0.08f), 0.9f));
            for (int i = 0; i < 9; i++)
                Box(st, "光の柱", new Vector3(-16 + i * 4, 9.5f, 8.2f), new Vector3(0.15f, 13.5f, 0.15f), i % 2 == 0 ? GlowPink : GlowCyan);
            foreach (float x in new[] { -24f, 24f })
            {
                Box(st, "スピーカー", new Vector3(x, 3.5f, -4f), new Vector3(3f, 7f, 3f), Black, true);
                for (int k = 0; k < 3; k++)
                    Cyl(st, "コーン", new Vector3(x, 1.5f + k * 2.2f, -5.55f), 0.8f, 0.1f, GlowPink, false, new Vector3(90, 0, 0));
            }
            Box(st, "準備中の幕", new Vector3(0, 6f, -4f), new Vector3(30f, 8f, 0.1f), Lit("curtain", new Color(0.55f, 0.08f, 0.18f), 0.2f));
            for (int i = 0; i < 6; i++) Toro(g, new Vector3(44 + i * 11.5f, G, 142f), true, 0.9f);
        }

        static void Palace()
        {
            var g = Group(_env, "月の御殿", Vector3.zero);
            Paving(g, -5, 5, 118, 212);
            Paving(g, -30, 30, 205, 218);
            var p = Group(g, "御殿", new Vector3(0, G, 232));
            Box(p, "高い基壇", new Vector3(0, 1.2f, 0), new Vector3(52f, 2.4f, 26f), Stone, true);
            for (int i = 0; i < 10; i++)
                Box(p, "石段", new Vector3(0, (i + 1) * 0.12f, -13.17f - (9 - i) * 0.34f), new Vector3(12f, (i + 1) * 0.24f, 0.34f), Stone, true);
            Building(p, "正殿", new Vector3(0, 2.4f, 0), 0, 40f, 18f, 7f, 2, false, true);
            foreach (float x in new[] { -32f, 32f })
                Building(p, "対屋", new Vector3(x, 0, 6f), x < 0 ? 90 : -90, 16f, 10f, 4.5f, 2, false);
            foreach (float x in new[] { -9f, 9f })
                Toro(p, new Vector3(x, 0, -18f), true, 1.3f);
            for (int i = 0; i < 10; i++)
            {
                float x = R(-120, 120);
                if (Mathf.Abs(x) < 34) continue;
                Pine(g, new Vector3(x, G, R(225, 246)), R(1f, 1.5f));
            }
            for (float z = 125; z < 205; z += 12)
                foreach (float x in new[] { -4.4f, 4.4f })
                    Toro(g, new Vector3(x, G, z), (int)z % 24 == 5, 0.8f);
        }

        static void SkyLanterns()
        {
            var g = Group(_deco, "空に浮かぶ灯籠", Vector3.zero);
            for (int i = 0; i < 90; i++)
            {
                var pos = new Vector3(R(-140, 140), R(22, 60), R(-60, 250));
                var l = Group(g, "天灯", pos, R(0, 360));
                Box(l, "灯", Vector3.zero, new Vector3(0.7f, 0.95f, 0.7f) * R(0.8f, 1.4f), i % 7 == 0 ? GlowSkyPink : GlowSkyLantern);
                var f = l.gameObject.AddComponent<Floaty>();
                f.amplitude = R(0.6f, 1.6f); f.speed = R(0.15f, 0.4f); f.spin = R(-6, 6);
            }
        }

        static void Boundaries()
        {
            var g = Group(_env, "世界の果て", Vector3.zero);
            foreach (var (c, s) in new[]
            {
                (new Vector3(0, 10, -330), new Vector3(800, 40, 2)), (new Vector3(0, 10, 520), new Vector3(800, 40, 2)),
                (new Vector3(-400, 10, 95), new Vector3(2, 40, 860)), (new Vector3(400, 10, 95), new Vector3(2, 40, 860)),
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
            Z(0, "torii", "海の大鳥居", "GREAT TORII", new Vector2(0, -72), new Vector2(40, 30), new Color(0.9f, 0.3f, 0.2f), new Vector3(0, G + 0.1f, -69), 0);
            Z(1, "corridor", "海の回廊", "SEA CORRIDOR", new Vector2(0, -28), new Vector2(16, 56), new Color(0.8f, 0.4f, 0.3f), new Vector3(0, G + 0.1f, -40), 0, false);
            Z(2, "shopping", "月見商店街", "MOONLIGHT ARCADE", new Vector2(0, 50), new Vector2(64, 100), new Color(0.95f, 0.6f, 0.3f), new Vector3(0, G + 0.1f, 14), 0);
            Z(3, "plaza", "大画面広場", "SCREEN PLAZA", new Vector2(82, 72), new Vector2(64, 48), new Color(0.9f, 0.4f, 0.7f), new Vector3(58, G + 0.1f, 72), 90, true, -4f);
            Z(4, "fishing", "釣り堀と茶屋", "FISHING POND & TEAHOUSE", new Vector2(-82, 72), new Vector2(64, 48), new Color(0.3f, 0.7f, 0.6f), new Vector3(-60, G + 0.1f, 72), -90);
            Z(5, "river", "天の川", "AMANOGAWA RIVER", new Vector2(0, 107), new Vector2(260, 22), new Color(0.3f, 0.6f, 0.95f), new Vector3(0, G + 0.1f, 92), 0);
            Z(6, "sakura", "千年桜の丘", "THOUSAND-YEAR SAKURA", new Vector2(-70, 176), new Vector2(100, 104), new Color(1f, 0.6f, 0.75f), new Vector3(-70, G + 0.1f, 127), 0, true, -12f);
            Z(7, "live", "ライブステージ", "LIVE STAGE", new Vector2(72, 178), new Vector2(64, 76), new Color(0.65f, 0.4f, 0.95f), new Vector3(72, G + 0.1f, 148), 0);
            Z(8, "palace", "月の御殿", "MOON PALACE", new Vector2(0, 228), new Vector2(70, 44), new Color(0.95f, 0.85f, 0.4f), new Vector3(0, G + 0.1f, 200), 0);
        }
    }
}
