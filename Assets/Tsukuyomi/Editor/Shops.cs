using UnityEngine;
using static Tsukuyomi.EditorTools.Kit;
using static Tsukuyomi.EditorTools.Parts;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 段階2：買い物ができるお店（14軒）。
    ///   服：浴衣の店・振袖と晴れ着の店・ふだん着の店（品ぞろえは店ごとにちがう）
    ///   飾り：小間物屋3軒（品ぞろえは同じで名前だけちがう）
    ///   食べ物：屋台8種（甘味・たこ焼き・らーめん・すし・焼き鳥・おでん・かき氷と飲み物・クレープ）
    /// 家を並べる前に、通りぞいの平らで空いている場所をさがしてとっておく。
    /// 乱数は別の種を使って、ほかの町並みの形が変わらないようにする。
    /// </summary>
    public static class Shops
    {
        struct Def
        {
            public string group, title, english, hello, street;
            public ItemKind kind;
            public Vector2 near;
            public bool stall;
            public Color cloth;
            public float t; // 0 より大きいとき：通りの線のこの割合の場所の近く（環状の通りなど）
        }

        static Def House(string group, ItemKind kind, string title, string en, string hello, string street, float x, float z) =>
            new Def { group = group, kind = kind, title = title, english = en, hello = hello, street = street, near = new Vector2(x, z) };

        static Def Stall(string group, string title, string en, string hello, string street, float x, float z, Color cloth) =>
            new Def { group = group, kind = ItemKind.Food, title = title, english = en, hello = hello, street = street, near = new Vector2(x, z), stall = true, cloth = cloth };

        static readonly Def[] Defs =
        {
            // 月見商店街
            House("yukata", ItemKind.Clothes, "浴衣の店「夕凪」", "YUKATA ▸ YUNAGI", "いらっしゃいませ。どうぞ、ゆっくり試着していってくださいね", "月見商店街", -11, 63),
            House("komono", ItemKind.Accessory, "小間物屋「星屑」", "ACCESSORY ▸ HOSHIKUZU", "いらっしゃい！かんざしもお面も、つけてみてね", "月見商店街", 11, 66.5f),
            Stall("kanmi", "甘味処「うさぎ堂」", "SWEETS ▸ USAGIDO", "あまいもの、いかがですか〜", "月見商店街", -8, 73, new Color(0.85f, 0.3f, 0.45f)),
            Stall("takoyaki", "たこ焼き「たこ八」", "TAKOYAKI ▸ TAKOHACHI", "焼きたて、あつあつだよ！", "月見商店街", 8, 56, new Color(0.9f, 0.45f, 0.15f)),
            // 東西通り（中段の横の通り）
            House("furisode", ItemKind.Clothes, "振袖と晴れ着「綾錦」", "FURISODE ▸ AYANISHIKI", "ようこそ。晴れの日の一枚を、ごいっしょに選びましょう", "東西通り", -30, 80),
            Stall("ramen", "らーめん屋台「月見軒」", "RAMEN ▸ TSUKIMIKEN", "へい、らっしゃい！", "東西通り", 28, 80, new Color(0.75f, 0.15f, 0.12f)),
            House("komono", ItemKind.Accessory, "小間物屋「月の雫」", "ACCESSORY ▸ TSUKI NO SHIZUKU", "いらっしゃいませ。月の光みたいな飾り、ありますよ", "東西通り", 66, 79),
            Stall("crepe", "クレープと縁日「ほしのわ」", "CREPE ▸ HOSHINOWA", "あまくてかわいいの、そろってます！", "東西通り", -62, 79, new Color(1f, 0.6f, 0.75f)),
            // 下町
            Stall("sushi", "すし屋台「波の花」", "SUSHI ▸ NAMI NO HANA", "いらっしゃい。今日もいいネタ入ってるよ", "下町の環状通り|大門通り", -22, 10, new Color(0.15f, 0.25f, 0.5f)),
            Stall("yakitori", "焼き鳥「とりこまち」", "YAKITORI ▸ TORIKOMACHI", "焼きたて、一本どうぞ！", "下町の環状通り", 55, 25, new Color(0.3f, 0.2f, 0.15f)),
            Stall("oden", "おでん「ゆげ」", "ODEN ▸ YUGE", "あったまっていきな", "東西通り|中段の環状通り", -90, 78, new Color(0.55f, 0.4f, 0.25f)),

            Stall("kori", "かき氷と飲み物「しろくま」", "SHAVED ICE ▸ SHIROKUMA", "つめた〜いの、あります", "下町の環状通り", 25, 0, new Color(0.3f, 0.6f, 0.9f)),
            // 上段の参道
            House("fudan", ItemKind.Clothes, "ふだん着の店「木綿屋」", "EVERYDAY WEAR ▸ MOMENYA", "まいど。毎日着るなら、うちの木綿がいちばん", "上段の参道", 0, 140),
            // 理髪店（髪の色と形）
            House("barber", ItemKind.Accessory, "理髪店「月の櫛」", "BARBER ▸ TSUKI NO KUSHI", "いらっしゃいませ。今日はどんな髪にしましょう？", "月見商店街|東西通り|中段の環状通り", 6, 58),
            // 服の形ごとの店
            House("miko", ItemKind.Clothes, "巫女装束「神楽屋」", "MIKO ▸ KAGURAYA", "ようこそお参りくださいました。装束の着つけ、お手伝いします", "上段の環状通り|上段の参道|中段の環状通り|下町の環状通り", 25, 150),
            House("dress", ItemKind.Clothes, "ドレス着物「月下美人」", "DRESS KIMONO ▸ GEKKABIJIN", "いらっしゃいませ！ ふわっと広がる裾、ためしてみて", "東西通り|月見商店街|中段の環状通り|下町の環状通り", 45, 80),
            House("juni", ItemKind.Clothes, "十二単「雅」", "JUNIHITOE ▸ MIYABI", "ようこそ。色のかさね、ひとつずつお見せしますね", "頂上の参道|上段の参道|上段の環状通り|中段の環状通り|下町の環状通り", 0, 180),
            new Def { group = "komono", kind = ItemKind.Accessory, title = "小間物の屋台「花かんざし」", english = "ACCESSORY ▸ HANAKANZASHI", hello = "いらっしゃいませ。お参りのおともに、ひとつどうぞ", street = "上段の参道|上段の環状通り", near = new Vector2(0, 124), stall = true, cloth = new Color(0.95f, 0.6f, 0.75f) },

        };

        /// <summary>島全体に置く、まったく同じ店（品ぞろえも名前も同じ）。通りの線の割合 t の近くに置く。</summary>
        static Def Again(Def d, string street, float t) { d.street = street; d.t = t; d.near = Vector2.zero; return d; }

        static Def[] All()
        {
            var l = new System.Collections.Generic.List<Def>(Defs);
            Def F(string g) => System.Array.Find(Defs, d => d.group == g);
            // 下町の環状通り
            l.Add(Again(F("kanmi"), "下町の環状通り", 0.12f));
            l.Add(Again(F("komono"), "下町の環状通り", 0.28f));
            l.Add(Again(F("takoyaki"), "下町の環状通り", 0.42f));
            l.Add(Again(F("yukata"), "下町の環状通り", 0.58f));
            l.Add(Again(F("ramen"), "下町の環状通り", 0.72f));
            l.Add(Again(F("crepe"), "下町の環状通り", 0.88f));
            // 中段の環状通り
            l.Add(Again(F("yakitori"), "中段の環状通り", 0.2f));
            l.Add(Again(F("kori"), "中段の環状通り", 0.5f));
            l.Add(Again(F("fudan"), "中段の環状通り|下町の環状通り", 0.8f));
            // 上段の環状通り
            l.Add(Again(F("oden"), "上段の環状通り", 0.15f));
            l.Add(Again(F("sushi"), "上段の環状通り", 0.4f));
            l.Add(Again(F("kanmi"), "上段の環状通り", 0.65f));
            l.Add(Again(F("furisode"), "上段の環状通り|下町の環状通り", 0.04f));
            // 頂上の参道
            l.Add(Again(System.Array.Find(Defs, d => d.group == "komono" && d.stall), "頂上の参道|上段の参道", 0.5f));
            // もっとお店を：新しい服の店の2軒目と、屋台をあちこちに
            l.Add(Again(F("barber"), "上段の環状通り|下町の環状通り", 0.5f));
            l.Add(Again(F("dress"), "下町の環状通り", 0.5f));
            l.Add(Again(F("miko"), "中段の環状通り", 0.35f));
            l.Add(Again(F("juni"), "中段の環状通り|上段の環状通り|下町の環状通り", 0.65f));
            l.Add(Again(F("kanmi"), "中段の環状通り", 0.92f));
            l.Add(Again(F("takoyaki"), "上段の環状通り", 0.9f));
            l.Add(Again(F("crepe"), "中段の環状通り", 0.08f));
            l.Add(Again(F("oden"), "下町の環状通り", 0.2f));
            l.Add(Again(F("kori"), "上段の環状通り", 0.3f));
            l.Add(Again(F("yukata"), "中段の環状通り", 0.6f));
            return l.ToArray();
        }

        public static void Build(Transform parent)
        {
            var keep = Rng;
            Rng = new System.Random(2700);
            var g = Group(parent, "お店", Vector3.zero);
            int made = 0, seed = 0;
            Placed.Clear();
            var all = All();
            foreach (var d in all) if (One(g, d, seed++)) made++;
            Rng = keep;
            Debug.Log($"[Shops] {made}/{all.Length} shops");
        }

        /// <summary>通りの near にいちばん近い所から前後をさがして、平らで空いた場所に置く。</summary>
        static bool One(Transform g, Def def, int seed)
        {
            foreach (var name in def.street.Split('|'))
            {
                var street = Town.Streets.Find(s => s.name == name);
                if (street == null) { Debug.LogWarning($"[Shops] {def.title}: no street {name}"); continue; }
                if (OnStreet(g, def, seed, street, false)) return true;
                if (def.stall && OnStreet(g, def, seed, street, true)) return true;
            }
            Debug.LogWarning($"[Shops] {def.title}: no room near {def.near} ({_why})");
            return false;
        }

        static string _why = "";
        static readonly System.Collections.Generic.List<Vector2> Placed = new System.Collections.Generic.List<Vector2>();

        static bool EdgeFree(Vector2 c)
        {
            foreach (var p in Placed) if (Vector2.Distance(p, c) < 9f) return false;
            foreach (var p in new[] { new Vector2(0, 12), new Vector2(0, 52), new Vector2(0, 6) }) if (Vector2.Distance(p, c) < 7f) return false;
            return true;
        }

        /// <summary>edge=false：通りわきの空き地。edge=true：屋台を通りのはしに置く（お祭りのように）。</summary>
        static bool OnStreet(Transform g, Def def, int seed, Town.Street street, bool edge)
        {
            float w = def.stall ? 3.4f : 8f, dep = def.stall ? 3.2f : 9f;
            int nWater = 0, nFree = 0, nStairs = 0, nRoad = 0, nFlat = 0, nLevel = 0;
            var near = def.t > 0 ? street.pts[Mathf.Clamp((int)(def.t * (street.pts.Count - 1)), 1, street.pts.Count - 2)] : def.near;
            int best = 0; float bd = float.MaxValue;
            for (int i = 1; i < street.pts.Count - 1; i++)
            {
                float dd = Vector2.Distance(street.pts[i], near);
                if (dd < bd) { bd = dd; best = i; }
            }
            for (int step = 0; step < 60; step++)
            {
                int i = best + (step % 2 == 0 ? step / 2 : -(step / 2 + 1));
                if (i < 1 || i >= street.pts.Count - 1) continue;
                var p = street.pts[i];
                var dir = (street.pts[i + 1] - street.pts[i - 1]).normalized;
                // near のある側を先にためす
                float first = def.t > 0 ? (((seed & 1) == 0) ? 1 : -1) : Vector2.Dot(new Vector2(-dir.y, dir.x), near - p) >= 0 ? 1 : -1;
                foreach (float sideSign in new[] { first, -first })
                {
                    var normal = new Vector2(-dir.y, dir.x) * sideSign;
                    var c = edge ? p + normal * (street.half - dep / 2 - 0.2f) : p + normal * (street.half + 0.7f + dep / 2);
                    var face = -normal;
                    float rotY = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                    float rr = Mathf.Max(w, dep) * 0.5f;
                    if (edge && street.half < 2.9f) return false;
                    if (Island.Inside(c.x, c.y) < 4f || Island.NearestWater(c.x, c.y) < 1.5f) { nWater++; continue; }
                    // 通りのはしは通り自体が予約ずみなので、ほかのお店とワープ先からの間かくだけ見る
                    if (edge ? !EdgeFree(c) : !Town.Free(c, rr * 0.85f)) { nFree++; continue; }
                    if (TerrainBuilder.NearStairs(c, rr + 1.4f)) { nStairs++; continue; }
                    if (!edge && Town.OnRoad(c, w, dep, rotY)) { nRoad++; continue; }
                    if (!Town.Flat(c, w, dep, rotY, out float y)) { nFlat++; continue; }
                    if (Mathf.Abs(y - Town.H(p)) > 0.8f) { nLevel++; continue; }
                    Town.Reserve(c, Mathf.Max(w, dep) * (def.stall ? 0.9f : 0.6f));
                    Placed.Add(c);
                    Make(g, def, c, y, rotY, w, dep, seed);
                    return true;
                }
            }
            _why = $"{street.name} edge={edge} water{nWater} free{nFree} stairs{nStairs} road{nRoad} flat{nFlat} level{nLevel}";
            return false;
        }


        static void Make(Transform g, Def def, Vector2 c, float y, float rotY, float w, float dep, int seed)
        {
            Transform shop;
            Vector3 standLocal, keeperLocal, signLocal;
            float keeperRot;
            if (def.stall)
            {
                shop = Buildings.Yatai(g, new Vector3(c.x, y, c.y), rotY, seed);
                shop.name = def.title;
                // 屋台ごとの色の幕と、縁台・野点傘・のぼり
                var cloth = ColorMat(def.cloth);
                Box(shop, "店の幕", new Vector3(0, 2.2f, 1.04f), new Vector3(2.82f, 0.5f, 0.03f), cloth);
                Box(shop, "店の屋根", new Vector3(0, 2.47f, 0.15f), new Vector3(3.02f, 0.12f, 1.82f), cloth, false, new Vector3(-8, 0, 0));
                var bench = Group(shop, "縁台", new Vector3(2.7f, 0, 0.9f));
                Box(bench, "台", new Vector3(0, 0.22f, 0), new Vector3(0.7f, 0.44f, 1.7f), Wood, true);
                Box(bench, "毛氈", new Vector3(0, 0.455f, 0), new Vector3(0.72f, 0.03f, 1.72f), ColorMat(new Color(0.78f, 0.12f, 0.12f)));
                if (seed % 2 == 0)
                {
                    Cyl(bench, "傘の柄", new Vector3(0.5f, 1.3f, 0), 0.04f, 2.6f, WoodDark);
                    var canopy = MeshObj(bench, "野点傘", Doll.Cone, new Vector3(0.5f, 2.35f, 0), ColorMat(new Color(0.82f, 0.14f, 0.12f)));
                    canopy.transform.localScale = new Vector3(3.2f, 0.55f, 3.2f);
                }
                Buildings.Nobori(shop, new Vector3(-1.9f, 0, 1.2f), 0, seed);
                Goods(shop, def.group);
                standLocal = new Vector3(0, 0, 2.0f);
                keeperLocal = new Vector3(0, 0, -0.9f);
                keeperRot = rotY;
                signLocal = new Vector3(0, 3.4f, 1.0f);
            }
            else
            {
                shop = Buildings.TownHouse(g, new Vector3(c.x, y, c.y), rotY, w, dep, 2, def.kind == ItemKind.Clothes ? 1 : 2, true, 31 + seed);
                shop.name = def.title;
                float wallFace = dep / 2 - 1.6f;
                if (def.kind == ItemKind.Clothes)
                {
                    // 衣桁にかけた、その店の着物の見本
                    var cols = new System.Collections.Generic.List<Color>();
                    foreach (var it in Catalog.InShop(def.group)) cols.Add(it.robe);
                    for (int i = 0; i < 4 && cols.Count > 0; i++)
                    {
                        float x = -2.7f + i * 1.8f;
                        var m = ColorMat(cols[(i * 2) % cols.Count]);
                        Box(shop, "衣桁", new Vector3(x, 0.62f + 2.55f, wallFace + 0.12f), new Vector3(1.7f, 0.06f, 0.06f), Black);
                        Box(shop, "着物の見本", new Vector3(x, 0.62f + 1.75f, wallFace + 0.1f), new Vector3(0.95f, 1.5f, 0.05f), m);
                        Box(shop, "着物の袖", new Vector3(x, 0.62f + 2.25f, wallFace + 0.1f), new Vector3(1.55f, 0.5f, 0.05f), m);
                        Box(shop, "帯", new Vector3(x, 0.62f + 1.85f, wallFace + 0.13f), new Vector3(0.97f, 0.16f, 0.03f), ColorMat(new Color(0.95f, 0.8f, 0.4f)));
                    }
                }
                else
                {
                    if (def.group == "barber")
                    {
                        // 理髪店：赤・白・青のしま模様の看板柱と、壁の鏡といす
                        var pole = Group(shop, "サインポール", new Vector3(w / 2 - 0.5f, 0.62f, dep / 2 + 0.4f));
                        for (int k = 0; k < 8; k++)
                            Cyl(pole, "しま", new Vector3(0, 0.4f + k * 0.25f, 0), 0.16f, 0.25f, k % 3 == 0 ? Glow("barber_red", new Color(1f, 0.2f, 0.2f), 0.8f, 2.5f) : k % 3 == 1 ? GlowWhite : Glow("barber_blue", new Color(0.3f, 0.5f, 1f), 0.8f, 2.5f));
                        Ball(pole, "玉", new Vector3(0, 2.5f, 0), Vector3.one * 0.3f, GlowWhite);
                        for (int k = 0; k < 3; k++)
                        {
                            float mx = -2.4f + k * 2.4f;
                            Box(shop, "鏡", new Vector3(mx, 0.62f + 1.6f, wallFace + 0.1f), new Vector3(1.2f, 1.4f, 0.04f), Lit("barber_mirror", new Color(0.75f, 0.85f, 0.95f), 0.95f, 0.8f));
                            Box(shop, "鏡の枠", new Vector3(mx, 0.62f + 1.6f, wallFace + 0.08f), new Vector3(1.36f, 1.56f, 0.03f), Gold);
                            Box(shop, "いす", new Vector3(mx, 0.62f + 0.5f, wallFace + 0.9f), new Vector3(0.7f, 0.15f, 0.7f), Lit("barber_chair", new Color(0.7f, 0.15f, 0.2f), 0.4f));
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
                }
                standLocal = new Vector3(0, 0, dep / 2 + 1.3f);
                keeperLocal = new Vector3(-w / 2 + 1.1f, 0, dep / 2 + 0.35f);
                keeperRot = rotY + 25f;
                signLocal = new Vector3(0, 4.9f, dep / 2 + 0.6f);
            }

            var st = shop.gameObject.AddComponent<ShopStand>();
            st.id = def.group + "_" + seed; st.group = def.group;
            st.title = def.title; st.english = def.english; st.kind = def.kind; st.hello = def.hello;
            Vector3 Wp(Vector3 l) { var p = shop.TransformPoint(l); p.y = Island.Height(p.x, p.z) + 0.03f; return p; }
            st.stand = Wp(standLocal);
            st.standYaw = rotY + 180f;
            st.keeper = Wp(keeperLocal);
            if (def.stall) st.keeper.y = y;
            st.keeperYaw = keeperRot;
            st.sign = shop.TransformPoint(signLocal);
            Debug.Log($"[Shops] {def.title} at ({c.x:0.0},{c.y:0.0}) y={y:0.00}");
        }

        /// <summary>屋台の台にならべる見本（その屋台の食べ物の色）。</summary>
        static void Goods(Transform shop, string group)
        {
            var cols = new System.Collections.Generic.List<Color>();
            int k = 0;
            foreach (var it in Catalog.InShop(group)) cols.Add(it.kind == ItemKind.Food ? it.c1 : Color.HSVToRGB((k++ * 0.17f) % 1f, 0.5f, 1f));

            for (int i = 0; i < 5 && cols.Count > 0; i++)
                Ball(shop, "見本", new Vector3(-0.9f + i * 0.45f, 1.1f, 0.62f), Vector3.one * 0.16f, ColorMat(cols[i % cols.Count]));
        }
    }
}
