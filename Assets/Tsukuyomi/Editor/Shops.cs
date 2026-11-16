using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 段階2：月見商店街の買い物ができるお店3軒（呉服屋・小間物屋・甘味の屋台）。
    /// 家を並べる前に場所をとっておき、店先に ShopStand（E で入る場所）を置く。
    /// 乱数は別の種を使って、ほかの町並みの形が変わらないようにする。
    /// </summary>
    public static class Shops
    {
        struct Def
        {
            public string id, title, english, hello;
            public ItemKind kind;
            public float side, z;
        }

        static readonly Def[] Defs =
        {
            new Def { id = "gofuku", title = "呉服屋「月白」", english = "KIMONO ▸ TSUKISHIRO", hello = "いらっしゃいませ。どうぞ、ゆっくり試着していってくださいね", kind = ItemKind.Clothes, side = -1, z = 63f },
            new Def { id = "komono", title = "小間物屋「星屑」", english = "ACCESSORY ▸ HOSHIKUZU", hello = "いらっしゃい！かんざしもお面も、つけてみてね", kind = ItemKind.Accessory, side = 1, z = 66.5f },
            new Def { id = "kanmi", title = "甘味の屋台「うさぎ堂」", english = "SWEETS ▸ USAGIDO", hello = "あまいもの、いかがですか〜。その場で食べられますよ", kind = ItemKind.Food, side = -1, z = 73f },
        };

        public static void Build(Transform parent)
        {
            var street = Town.Streets.Find(s => s.name == "月見商店街");
            if (street == null) { Debug.LogWarning("[Shops] no shopping street"); return; }
            var keep = Rng;
            Rng = new System.Random(2700);
            var g = Group(parent, "月見商店街のお店", Vector3.zero);
            foreach (var d in Defs) One(g, d, street.half);
            Rng = keep;
        }

        static void One(Transform g, Def def, float half)
        {
            bool stall = def.kind == ItemKind.Food;
            float w = stall ? 3.4f : 8f, dep = stall ? 3.2f : 9f;
            float rotY = def.side < 0 ? 90f : -90f; // 正面（ローカル +z）を通りへ
            Vector2 c = default; float y = 0; bool ok = false;
            foreach (float dz in new[] { 0f, 1.5f, -1.5f, 3f, -3f })
            {
                c = new Vector2(def.side * (half + 0.7f + dep / 2), def.z + dz);
                if (Town.Flat(c, w, dep, rotY, out y) && !TerrainBuilder.NearStairs(c, Mathf.Max(w, dep) * 0.5f + 1f)) { ok = true; break; }
            }
            if (!ok) { y = Town.H(c); Debug.LogWarning($"[Shops] {def.id}: not flat, using terrain height"); }
            Town.Reserve(c, Mathf.Max(w, dep) * (stall ? 0.9f : 0.6f));

            Transform shop;
            Vector3 standLocal, keeperLocal, signLocal;
            float keeperRot;
            if (stall)
            {
                shop = Buildings.Yatai(g, new Vector3(c.x, y, c.y), rotY, 3);
                shop.name = def.title;
                // 縁台と野点傘、のぼり、だんごの見本
                var bench = Group(shop, "縁台", new Vector3(2.7f, 0, 0.9f));
                Box(bench, "台", new Vector3(0, 0.22f, 0), new Vector3(0.7f, 0.44f, 1.7f), Wood, true);
                Box(bench, "毛氈", new Vector3(0, 0.455f, 0), new Vector3(0.72f, 0.03f, 1.72f), ColorMat(new Color(0.78f, 0.12f, 0.12f)));
                Cyl(bench, "傘の柄", new Vector3(0.5f, 1.3f, 0), 0.04f, 2.6f, WoodDark);
                var canopy = MeshObj(bench, "野点傘", Doll.Cone, new Vector3(0.5f, 2.35f, 0), ColorMat(new Color(0.82f, 0.14f, 0.12f)));
                canopy.transform.localScale = new Vector3(3.2f, 0.55f, 3.2f);
                Buildings.Nobori(shop, new Vector3(-1.9f, 0, 1.2f), 0, 5);
                for (int i = 0; i < 3; i++)
                    for (int k = 0; k < 3; k++)
                        Ball(shop, "だんご", new Vector3(-0.7f + i * 0.7f, 1.12f + k * 0.07f, 0.55f), Vector3.one * 0.08f, ColorMat(k == 0 ? new Color(0.6f, 0.85f, 0.45f) : k == 1 ? Color.white : new Color(1f, 0.65f, 0.75f)));
                standLocal = new Vector3(0, 0, 2.0f);
                keeperLocal = new Vector3(0, 0, -0.9f);
                keeperRot = rotY;
                signLocal = new Vector3(0, 3.4f, 1.0f);
            }
            else
            {
                shop = Buildings.TownHouse(g, new Vector3(c.x, y, c.y), rotY, w, dep, 2, def.kind == ItemKind.Clothes ? 1 : 2, true, 31);
                shop.name = def.title;
                float wallFace = dep / 2 - 1.6f;
                if (def.kind == ItemKind.Clothes)
                {
                    // 衣桁にかけた着物の見本
                    var cols = new[] { new Color(1f, 0.78f, 0.84f), new Color(0.16f, 0.24f, 0.5f), new Color(0.98f, 0.76f, 0.2f), new Color(0.13f, 0.12f, 0.22f) };
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -2.7f + i * 1.8f;
                        var m = ColorMat(cols[i]);
                        Box(shop, "衣桁", new Vector3(x, 0.62f + 2.55f, wallFace + 0.12f), new Vector3(1.7f, 0.06f, 0.06f), Black);
                        Box(shop, "着物の見本", new Vector3(x, 0.62f + 1.75f, wallFace + 0.1f), new Vector3(0.95f, 1.5f, 0.05f), m);
                        Box(shop, "着物の袖", new Vector3(x, 0.62f + 2.25f, wallFace + 0.1f), new Vector3(1.55f, 0.5f, 0.05f), m);
                        Box(shop, "帯", new Vector3(x, 0.62f + 1.85f, wallFace + 0.13f), new Vector3(0.97f, 0.16f, 0.03f), ColorMat(new Color(0.95f, 0.8f, 0.4f)));
                    }
                }
                else
                {
                    // 軒につるしたお面と、かんざしの台
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -2.6f + i * 1.3f;
                        Ball(shop, "お面", new Vector3(x, 0.62f + 2.5f, dep / 2 - 0.1f), new Vector3(0.36f, 0.44f, 0.12f), ColorMat(i % 2 == 0 ? Color.white : new Color(1f, 0.85f, 0.4f)));
                        Box(shop, "お面のくま", new Vector3(x, 0.62f + 2.55f, dep / 2 - 0.03f), new Vector3(0.22f, 0.03f, 0.02f), ColorMat(new Color(0.85f, 0.15f, 0.2f)));
                    }
                    for (int i = 0; i < 6; i++)
                        Ball(shop, "かんざし", new Vector3(-2.5f + i, 0.62f + 1.0f, wallFace + 0.75f), Vector3.one * 0.12f, ColorMat(Color.HSVToRGB(i / 6f, 0.5f, 1f)));
                }
                standLocal = new Vector3(0, 0, dep / 2 + 1.3f);
                keeperLocal = new Vector3(-w / 2 + 1.1f, 0, dep / 2 + 0.35f);
                keeperRot = rotY + 25f;
                signLocal = new Vector3(0, 4.9f, dep / 2 + 0.6f);
            }

            var st = shop.gameObject.AddComponent<ShopStand>();
            st.id = def.id; st.title = def.title; st.english = def.english; st.kind = def.kind; st.hello = def.hello;
            Vector3 W(Vector3 l) { var p = shop.TransformPoint(l); p.y = Island.Height(p.x, p.z) + 0.03f; return p; }
            st.stand = W(standLocal);
            st.standYaw = rotY + 180f;
            st.keeper = W(keeperLocal);
            if (stall) st.keeper.y = y;
            st.keeperYaw = keeperRot;
            st.sign = shop.TransformPoint(signLocal);
            Debug.Log($"[Shops] {def.id} at ({c.x:0.0},{c.y:0.0}) y={y:0.00} stand=({st.stand.x:0.0},{st.stand.z:0.0})");
        }
    }
}
