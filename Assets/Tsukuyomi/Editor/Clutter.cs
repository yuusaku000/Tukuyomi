using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 町をにぎやかにする小物を、通りにそってたくさん置く。
    ///   店先の樽・桶・木箱・鉢植え・壺・かご・腰かけ（Poly Haven の素材, CC0）
    ///   立て看板・荷車・天水桶・祠とお地蔵さま・井戸・路地の上の提灯のジグザグ・渡り廊下・浮かぶ看板
    /// </summary>
    public static class Clutter
    {
        const string Dir = "Assets/Tsukuyomi/Imported/";

        static readonly (string id, float height)[] PropDefs =
        {
            ("wine_barrel_01", 0.87f), ("wooden_bucket_01", 0.55f), ("wooden_bucket_02", 0.35f), ("wooden_crate_01", 0.35f), ("wooden_crate_02", 0.45f),
            ("potted_plant_01", 1.35f), ("potted_plant_02", 0.84f), ("planter_pot_clay", 0.22f), ("ceramic_vase_02", 0.31f), ("ceramic_vase_04", 0.34f),
            ("wicker_basket_01", 0.12f), ("wooden_stool_01", 0.44f),
        };

        static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();

        static GameObject Model(string id)
        {
            if (Models.TryGetValue(id, out var m)) return m;
            var imp = AssetImporter.GetAtPath(Dir + id + ".fbx") as ModelImporter;
            if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
            m = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + id + ".fbx");
            Models[id] = m;
            if (m == null) Debug.LogWarning("[Tsukuyomi] missing model " + id);
            return m;
        }

        /// <summary>素材を置く。高さを本当の大きさ（m）に合わせ、足もとを地面にそろえる。</summary>
        public static GameObject Prop(Transform parent, string id, Vector3 pos, float yaw, float scale = 1f)
        {
            var src = Model(id);
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = id;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = PropMat(mats[i] != null ? mats[i].name : id);
                r.sharedMaterials = mats;
            }
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return go;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float want = 1f;
            foreach (var (pid, h) in PropDefs) if (pid == id) want = h;
            float k = b.size.y > 0.001f ? want / b.size.y * scale : 1f;
            go.transform.localScale *= k;
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += Vector3.up * (pos.y - b.min.y);
            foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        static readonly Dictionary<string, Material> PropMats = new Dictionary<string, Material>();

        /// <summary>素材の絵（Imported/Textures/名前.jpg|png）をはったマテリアル。葉っぱは透けた部分を抜く。</summary>
        static Material PropMat(string name)
        {
            if (PropMats.TryGetValue(name, out var m)) return m;
            Texture2D tex = null;
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/" + name + ext);
                if (tex != null) break;
            }
            m = tex != null ? LitTex("prop_" + name, tex, name.Contains("leaves")) : Lit("prop_plain_" + name, new Color(0.55f, 0.4f, 0.28f), 0.2f);
            PropMats[name] = m;
            return m;
        }

        static string Pick(params string[] ids) => ids[Ri(0, ids.Length)];

        static bool Clear(Vector3 p, float r)
        {
            return !Physics.CheckSphere(p + Vector3.up * (r + 0.15f), r, ~0, QueryTriggerInteraction.Ignore);
        }

        static bool Walkable(Vector2 p, int level)
        {
            if (Island.NearestWater(p.x, p.y) < 1.5f || Island.Inside(p.x, p.y) < 3f) return false;
            if (level >= 0 && Mathf.Abs(Island.TerraceHeight(p.x, p.y) - Island.LevelTop(level)) > 0.2f) return false;
            return true;
        }

        public static void Build(Transform env, Transform deco)
        {
            Physics.SyncTransforms();
            var g = Group(env, "にぎわいの小物", Vector3.zero);
            int props = 0, strings = 0, walks = 0;
            var rope = Black;
            // 店先・家のわき・裏に、生活の小物（道のまんなかには置かない）
            int extras = 0;
            foreach (var lot in Town.Lots)
            {
                var rot = Quaternion.Euler(0, lot.rotY, 0);
                Vector3 At(float x, float z, float up = 0) { var v = lot.pos + rot * new Vector3(x, 0, z); v.y = lot.pos.y + up; return v; }
                if (lot.facing)
                {
                    // 正面の石の段（基壇）の上、入口の両わき
                    foreach (float sd in new[] { -1f, 1f })
                        if (Rng.NextDouble() < 0.7)
                            props += Cluster(g, At(sd * (lot.w / 2 - 0.45f), lot.d / 2 + 0.15f, 0.5f), lot.rotY + 90 * sd);
                    if (Rng.NextDouble() < 0.4) { StandSign(g, At((Rng.NextDouble() < 0.5 ? -1 : 1) * (lot.w / 2 - 1.3f), lot.d / 2 + 0.2f, 0.5f), lot.rotY); extras++; }
                    if (Rng.NextDouble() < 0.55) { ShopBoard(g, lot, rot); extras++; }
                }
                // 家のわき（となりとのすきま）と、裏
                foreach (float sd in new[] { -1f, 1f })
                {
                    if (Rng.NextDouble() > 0.45) continue;
                    var q = At(sd * (lot.w / 2 + 0.75f), R(-lot.d / 4, lot.d / 4));
                    if (!Walkable(new Vector2(q.x, q.z), -1)) continue;
                    q.y = Island.Height(q.x, q.z);
                    if (Mathf.Abs(q.y - lot.pos.y) > 0.4f || !Clear(q, 0.35f)) continue;
                    if (Rng.NextDouble() < 0.25) { RainBarrels(g, q, lot.rotY + 90 * sd); extras++; }
                    else props += Cluster(g, q, lot.rotY + 90 * sd);
                }
                if (Rng.NextDouble() < 0.3)
                {
                    var q = At(R(-lot.w / 3, lot.w / 3), -(lot.d / 2 + 0.9f));
                    if (Walkable(new Vector2(q.x, q.z), -1))
                    {
                        q.y = Island.Height(q.x, q.z);
                        if (Mathf.Abs(q.y - lot.pos.y) < 0.4f && Clear(q, 0.6f))
                        {
                            if (Rng.NextDouble() < 0.4) { Cart(g, q, lot.rotY + 90); extras++; }
                            else props += Cluster(g, q, lot.rotY + 180);
                        }
                    }
                }
            }

            // 頭の上：路地の提灯のジグザグと、渡り廊下
            foreach (var s in Town.Streets)
            {
                bool alley = s.half <= 2.7f;
                for (int i = 2; i < s.pts.Count - 2; i++)
                {
                    var p = s.pts[i];
                    if (!Walkable(p, s.level)) continue;
                    var dir = (s.pts[i + 1] - s.pts[i - 1]).normalized;
                    var nrm = new Vector2(-dir.y, dir.x);
                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    if (i % 7 == 3 && s.half < 5f && BuildingBothSides(p, nrm, s.half, Island.LevelTop(Mathf.Max(0, s.level))))
                    {
                        float y = Island.LevelTop(Mathf.Max(0, s.level));
                        var a = p + nrm * (s.half + 0.3f) - dir * 1.5f;
                        var b = p - nrm * (s.half + 0.3f) + dir * 1.5f;
                        if ((i / 7) % 2 == 0) { var t = a; a = b; b = t; }
                        var a3 = new Vector3(a.x, y + 4.4f, a.y); var b3 = new Vector3(b.x, y + 4.4f, b.y);
                        var mid = (a3 + b3) / 2;
                        var ropeGo = Box(g, "綱", mid, new Vector3(0.03f, 0.03f, Vector3.Distance(a3, b3)), rope);
                        ropeGo.transform.rotation = Quaternion.LookRotation(b3 - a3);
                        int n = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a3, b3) / 1.6f));
                        for (int k = 1; k < n; k++)
                        {
                            var lp = Vector3.Lerp(a3, b3, k / (float)n) + Vector3.down * (0.45f + Mathf.Sin(k / (float)n * Mathf.PI) * 0.3f);
                            Chochin(g, lp, (k + i) % 3 == 0 ? GlowWhite : GlowRed);
                        }
                        strings++;
                    }
                    if (alley && i % 26 == 13 && Rng.NextDouble() < 0.6)
                    {
                        float y = Island.Height(p.x, p.y);
                        var w = Group(g, "渡り廊下", new Vector3(p.x, y, p.y), yaw);
                        float span = s.half * 2 + 3.2f;
                        Box(w, "床", new Vector3(0, 4.9f, 0), new Vector3(span, 0.25f, 2.2f), WoodDark);
                        Box(w, "高欄", new Vector3(0, 5.6f, 1.05f), new Vector3(span, 0.1f, 0.1f), Vermilion);
                        Box(w, "高欄", new Vector3(0, 5.6f, -1.05f), new Vector3(span, 0.1f, 0.1f), Vermilion);
                        Box(w, "障子", new Vector3(0, 5.8f, 0), new Vector3(span - 0.4f, 1.6f, 1.9f), Glow("glow_shoji", new Color(1f, 0.85f, 0.6f), 0.7f, 2.2f));
                        MeshObj(w, "屋根", Roof(span + 1f, 3.2f, 0.9f, 0.3f), new Vector3(0, 6.6f, 0), RoofBark);
                        Chochin(w, new Vector3(0, 4.3f, 1.2f), GlowRed);
                        walks++;
                    }
                }
            }

            // 町のすきま（通りからはずれた所）に、祠・井戸・長椅子
            for (int k = 0; k < 900 && extras < 600; k++)
            {
                var c = new Vector2(R(-145, 145), R(5, 295));
                if (!Town.Free(c, 1.3f) || !Walkable(c, -1)) continue;
                if (!Town.Flat(c, 1.6f, 1.6f, 0, out float y, 0.25f)) continue;
                var c3 = new Vector3(c.x, y, c.y);
                if (!Clear(c3, 0.9f)) continue;
                int kind = Ri(0, 3);
                if (kind == 0) Hokora(g, c3, R(0, 360)); else if (kind == 1) Well(g, c3); else Bench(g, c3, R(0, 360));
                Town.Reserve(c, 1.3f);
                extras++;
            }

            // 空いた所を小さな庭にする（もみじ・松・植えこみ・石・花）
            int gardens = 0;
            var momiji = new[] { Lit("momiji1", new Color(0.85f, 0.25f, 0.12f), 0.1f), Lit("momiji2", new Color(0.95f, 0.5f, 0.15f), 0.1f), Lit("momiji3", new Color(0.75f, 0.12f, 0.15f), 0.1f) };
            var bush = Lit("bush", new Color(0.22f, 0.42f, 0.22f), 0.1f);
            var rock = Lit("rock", new Color(0.45f, 0.45f, 0.43f), 0.15f);
            var flowers = new[] { ColorMat(new Color(1f, 0.6f, 0.75f)), ColorMat(new Color(1f, 0.9f, 0.4f)), ColorMat(new Color(0.75f, 0.6f, 1f)) };
            for (int k = 0; k < 4000 && gardens < 900; k++)
            {
                var c = new Vector2(R(-155, 155), R(5, 312));
                if (!Town.Free(c, 1.8f) || !Walkable(c, -1) || Town.OnAnyStreet(c, 1.2f)) continue;
                if (!Town.Flat(c, 2.4f, 2.4f, 0, out float y, 0.3f)) continue;
                var c3 = new Vector3(c.x, y, c.y);
                if (!Clear(c3, 1.0f)) continue;
                var gd = Group(g, "庭", c3, R(0, 360));
                int kind = Ri(0, 6);
                if (kind <= 1)
                {
                    Cyl(gd, "幹", new Vector3(0, 1.1f, 0), 0.14f, 2.2f, Lit("bark", new Color(0.24f, 0.16f, 0.14f), 0.15f));
                    for (int i = 0; i < 4; i++) Ball(gd, "もみじ", new Vector3(R(-0.8f, 0.8f), R(2.2f, 3.2f), R(-0.8f, 0.8f)), new Vector3(R(1.6f, 2.4f), R(1.0f, 1.5f), R(1.6f, 2.4f)), momiji[Ri(0, momiji.Length)]);
                }
                else if (kind == 2) Pine(gd, Vector3.zero, R(0.6f, 0.9f));
                else if (kind == 3) { for (int i = 0; i < 4; i++) Ball(gd, "植えこみ", new Vector3(R(-1f, 1f), 0.4f, R(-1f, 1f)), new Vector3(R(0.8f, 1.4f), R(0.6f, 0.9f), R(0.8f, 1.4f)), bush); }
                else if (kind == 4) { for (int i = 0; i < 3; i++) Ball(gd, "石", new Vector3(R(-1f, 1f), 0.15f, R(-1f, 1f)), new Vector3(R(0.5f, 1.2f), R(0.35f, 0.7f), R(0.5f, 1f)), rock); if (!ToroNear(c3, 7f)) Toro(gd, new Vector3(0.6f, 0, 0.6f), false, 0.6f); }
                else { for (int i = 0; i < 10; i++) Ball(gd, "花", new Vector3(R(-1f, 1f), 0.15f, R(-1f, 1f)), Vector3.one * R(0.2f, 0.32f), flowers[Ri(0, flowers.Length)]); Ball(gd, "葉", new Vector3(0, 0.08f, 0), new Vector3(2.2f, 0.15f, 2.2f), bush); }
                Town.Reserve(c, 1.8f);
                gardens++;
            }
            Debug.Log($"[Tsukuyomi] gardens: {gardens}");

            // 広場・大通りの上に浮かぶ看板
            var holo = Group(deco, "浮かぶ看板", Vector3.zero);
            foreach (var s in Town.Streets)
            {
                if (s.half < 3.5f) continue;
                for (int i = 10; i < s.pts.Count - 10; i += 22)
                {
                    var p = s.pts[i];
                    if (!Walkable(p, s.level)) continue;
                    var dir = (s.pts[i + 1] - s.pts[i - 1]).normalized;
                    float y = Island.Height(p.x, p.y);
                    var sg = Group(holo, "看板", new Vector3(p.x, y + R(8f, 11f), p.y), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90);
                    Box(sg, "面", Vector3.zero, new Vector3(R(3f, 5f), R(1.4f, 2.2f), 0.06f), Buildings.Signage(i));
                    sg.gameObject.AddComponent<Floaty>().amplitude = 0.3f;
                }
            }
            Debug.Log($"[Tsukuyomi] clutter: {props} props, {strings} lantern strings, {walks} walkways, {extras} extras");
        }

        /// <summary>道の両がわ（4m 以内）に建物の壁があるか（提灯の綱を張れるか）。</summary>
        static bool BuildingBothSides(Vector2 p, Vector2 nrm, float half, float y)
        {
            foreach (float sd in new[] { -1f, 1f })
            {
                var o = new Vector3(p.x, y + 3.2f, p.y);
                var d = new Vector3(nrm.x, 0, nrm.y) * sd;
                if (!Physics.Raycast(o, d, out var hit, half + 4f, ~0, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }

        /// <summary>店先の小物のかたまり（1〜4こ）。置いた数を返す。</summary>
        static int Cluster(Transform g, Vector3 p, float yaw)
        {
            int kind = Ri(0, 7);
            switch (kind)
            {
                case 0: // 樽と桶
                    Prop(g, "wine_barrel_01", p, R(0, 360));
                    if (Rng.NextDouble() < 0.6) Prop(g, Pick("wooden_bucket_01", "wooden_bucket_02"), p + Quaternion.Euler(0, yaw, 0) * new Vector3(0.75f, 0, 0), R(0, 360));
                    return 2;
                case 1: // 木箱を積む
                    Prop(g, "wooden_crate_02", p, yaw + R(-8, 8));
                    if (Rng.NextDouble() < 0.6) Prop(g, "wooden_crate_01", p + Vector3.up * 0.45f, yaw + R(-15, 15));
                    if (Rng.NextDouble() < 0.5) Prop(g, "wicker_basket_01", p + Vector3.up * (0.45f + 0.35f), R(0, 360));
                    return 3;
                case 2: // 鉢植え
                    Prop(g, Pick("potted_plant_01", "potted_plant_02"), p, R(0, 360));
                    if (Rng.NextDouble() < 0.5) Prop(g, "planter_pot_clay", p + Quaternion.Euler(0, yaw, 0) * new Vector3(0.6f, 0, 0.1f), R(0, 360));
                    return 2;
                case 3: // 壺をならべる
                    for (int i = 0; i < 3; i++) Prop(g, Pick("ceramic_vase_02", "ceramic_vase_04", "planter_pot_clay"), p + Quaternion.Euler(0, yaw, 0) * new Vector3(-0.4f + i * 0.4f, 0, R(-0.1f, 0.1f)), R(0, 360));
                    return 3;
                case 4: // 腰かけとかご
                    Prop(g, "wooden_stool_01", p, R(0, 360));
                    Prop(g, "wicker_basket_01", p + Vector3.up * 0.44f, R(0, 360));
                    return 2;
                case 5: // 桶を重ねる
                    Prop(g, "wooden_bucket_02", p, R(0, 360));
                    Prop(g, "wooden_bucket_01", p + Quaternion.Euler(0, yaw, 0) * new Vector3(0.6f, 0, 0), R(0, 360));
                    return 2;
                default:
                    Prop(g, Pick("potted_plant_02", "wine_barrel_01", "wooden_crate_01"), p, R(0, 360));
                    return 1;
            }
        }

        static void StandSign(Transform g, Vector3 p, float yaw)
        {
            var s = Group(g, "立て看板", p, yaw);
            Box(s, "脚", new Vector3(0, 0.6f, -0.15f), new Vector3(0.6f, 1.2f, 0.05f), Wood, false, new Vector3(12, 0, 0));
            Box(s, "脚", new Vector3(0, 0.6f, 0.15f), new Vector3(0.6f, 1.2f, 0.05f), Wood, false, new Vector3(-12, 0, 0));
            Box(s, "面", new Vector3(0, 0.75f, 0.2f), new Vector3(0.5f, 0.85f, 0.02f), Buildings.Signage(Ri(0, 50)), false, new Vector3(-12, 0, 0));
        }

        static void Cart(Transform g, Vector3 p, float yaw)
        {
            var c = Group(g, "荷車", p, yaw);
            Box(c, "台", new Vector3(0, 0.75f, 0), new Vector3(1.3f, 0.1f, 2.4f), Wood);
            Box(c, "柄", new Vector3(0.45f, 0.62f, 1.9f), new Vector3(0.07f, 0.07f, 1.6f), Wood, false, new Vector3(-8, 0, 0));
            Box(c, "柄", new Vector3(-0.45f, 0.62f, 1.9f), new Vector3(0.07f, 0.07f, 1.6f), Wood, false, new Vector3(-8, 0, 0));
            foreach (float x in new[] { -0.75f, 0.75f })
                Cyl(c, "車輪", new Vector3(x, 0.5f, -0.2f), 0.5f, 0.08f, WoodDark, false, new Vector3(0, 0, 90));
            Prop(c, "wooden_crate_01", c.position + Vector3.up * 0.8f, yaw);
            Prop(c, "wicker_basket_01", c.position + Vector3.up * 0.8f + c.rotation * new Vector3(0, 0, 0.7f), yaw + 20);
        }

        static void RainBarrels(Transform g, Vector3 p, float yaw)
        {
            var r = Group(g, "天水桶", p, yaw);
            Box(r, "台", new Vector3(0, 0.25f, 0), new Vector3(1.6f, 0.5f, 0.8f), Stone);
            Prop(r, "wooden_bucket_02", p + Vector3.up * 0.5f + r.rotation * new Vector3(-0.35f, 0, 0), 0);
            Prop(r, "wooden_bucket_02", p + Vector3.up * 0.5f + r.rotation * new Vector3(0.35f, 0, 0), 0);
            Prop(r, "wooden_bucket_01", p + Vector3.up * 0.85f, 0);
        }

        static void Hokora(Transform g, Vector3 p, float yaw)
        {
            var h = Group(g, "祠", p, yaw);
            Box(h, "台", new Vector3(0, 0.4f, 0), new Vector3(0.9f, 0.8f, 0.8f), Stone);
            Box(h, "社", new Vector3(0, 1.1f, 0), new Vector3(0.6f, 0.6f, 0.55f), Wood);
            MeshObj(h, "屋根", Roof(1.1f, 1f, 0.35f, 0.1f), new Vector3(0, 1.4f, 0), RoofCopper);
            Box(h, "鈴", new Vector3(0, 1.0f, 0.3f), new Vector3(0.1f, 0.1f, 0.05f), Gold);
            // お地蔵さま
            var z = Group(h, "お地蔵さま", new Vector3(0.9f, 0, 0.1f));
            Cyl(z, "体", new Vector3(0, 0.35f, 0), 0.18f, 0.7f, Stone);
            Ball(z, "頭", new Vector3(0, 0.82f, 0), Vector3.one * 0.26f, Stone);
            Cyl(z, "よだれかけ", new Vector3(0, 0.56f, 0.02f), 0.2f, 0.18f, ColorMat(new Color(0.85f, 0.15f, 0.15f)));
            Box(h, "灯", new Vector3(-0.55f, 0.95f, 0.3f), new Vector3(0.12f, 0.16f, 0.12f), GlowWarm);
        }

        static void Well(Transform g, Vector3 p)
        {
            var w = Group(g, "井戸", p, R(0, 90));
            Cyl(w, "井筒", new Vector3(0, 0.4f, 0), 0.65f, 0.8f, Stone);
            Cyl(w, "水", new Vector3(0, 0.6f, 0), 0.55f, 0.02f, Lit("well_water", new Color(0.05f, 0.12f, 0.16f), 0.95f));
            foreach (float x in new[] { -0.6f, 0.6f }) Box(w, "柱", new Vector3(x, 1.2f, 0), new Vector3(0.1f, 2.4f, 0.1f), Wood);
            MeshObj(w, "屋根", Roof(1.8f, 1.2f, 0.4f, 0.1f), new Vector3(0, 2.4f, 0), RoofBark);
            Prop(w, "wooden_bucket_01", p + w.rotation * new Vector3(0.9f, 0, 0.4f), 30);
        }

        /// <summary>店の正面の上にかかげる横長の看板。</summary>
        static void ShopBoard(Transform g, Town.Lot lot, Quaternion rot)
        {
            var p = lot.pos + rot * new Vector3(0, 0, lot.d / 2 + 0.05f);
            var b = Group(g, "看板", new Vector3(p.x, lot.pos.y + 4.35f, p.z), lot.rotY);
            float w = Mathf.Min(lot.w - 2f, 4.2f);
            Box(b, "板", Vector3.zero, new Vector3(w, 0.75f, 0.1f), WoodDark);
            Box(b, "枠", new Vector3(0, 0, 0.03f), new Vector3(w + 0.15f, 0.85f, 0.06f), Gold);
            Box(b, "字", new Vector3(0, 0, 0.07f), new Vector3(w - 0.4f, 0.45f, 0.02f), Rng.NextDouble() < 0.5 ? Buildings.Signage(Ri(0, 40)) : ColorMat(new Color(0.95f, 0.9f, 0.78f)));
        }

        static void Bench(Transform g, Vector3 p, float yaw)
        {
            TeaSeat(g, p, yaw);
        }
    }
}
