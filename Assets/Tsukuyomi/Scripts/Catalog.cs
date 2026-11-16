using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    public enum ItemKind { Clothes, Accessory, Food }

    /// <summary>飾りをつける場所（1か所に1つ）。</summary>
    public enum Slot { None, Hair, Head, Face, Hand, Shoulder, Back }

    /// <summary>食べ物を手に持ったときの形。</summary>
    public enum FoodShape { Skewer, Bowl, Cup, Tray, Sushi, Crepe, Round, Fish, Parfait, StickBall, Puff, Corn, Plate }

    public class Item
    {
        public string id, name, desc;
        public string shop; // どの種類のお店で売っているか（ShopStand.group）
        public ItemKind kind;
        public Slot slot;
        public int price;
        // 服の色（すそ・帯など）。hasTop のときは上半身だけ top の色（袴ふう）
        public Color robe, robe2, sleeve, cuff, sash, accent, top;
        public bool glowCuff, hasTop;
        // 食べ物：手に持つ形と色、食べたときのひとこと
        public FoodShape shape;
        public Color c1, c2, c3;
        public int count = 3;
        public string say;
    }

    /// <summary>
    /// 島のお店で売っているもの。値段は「安め」（食べ物 100〜500、飾り 1000〜3000、服 2000〜6000 ふじゅ〜）。
    /// 支払いはミニヤチヨの本物のふじゅ〜（Fuju.cs）。持ち物はツクヨミの save.json に残す。
    /// お店の種類（shop）：
    ///   服　yukata 浴衣の店／furisode 振袖と晴れ着の店／fudan ふだん着の店
    ///   飾り komono 小間物屋（どの支店も同じ品ぞろえ）
    ///   食べ物 kanmi 甘味処／takoyaki たこ焼き／ramen らーめん／sushi すし／yakitori 焼き鳥／oden おでん／kori かき氷と飲み物／crepe クレープと縁日のおやつ
    /// </summary>
    public static class Catalog
    {
        public const string StartClothes = "shiraume";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static Item Clothes(string shop, string id, string name, int price, string desc, Color robe, Color robe2, Color sleeve, Color cuff, Color sash, Color accent, bool glow = false) =>
            new Item { shop = shop, id = id, name = name, price = price, desc = desc, kind = ItemKind.Clothes, robe = robe, robe2 = robe2, sleeve = sleeve, cuff = cuff, sash = sash, accent = accent, glowCuff = glow };

        /// <summary>袴ふう：上（着物）と下（袴）で色がちがう。</summary>
        static Item Hakama(string shop, string id, string name, int price, string desc, Color top, Color hakama, Color sleeve, Color cuff, Color sash) =>
            new Item { shop = shop, id = id, name = name, price = price, desc = desc, kind = ItemKind.Clothes, robe = hakama, robe2 = Color.Lerp(hakama, Color.black, 0.3f), sleeve = sleeve, cuff = cuff, sash = sash, accent = sash, top = top, hasTop = true };

        static Item Acc(string id, string name, Slot slot, int price, string desc) =>
            new Item { shop = "komono", id = id, name = name, price = price, desc = desc, kind = ItemKind.Accessory, slot = slot };

        static Item Food(string shop, string id, string name, int price, FoodShape shape, Color c1, Color c2, string desc, string say, int count = 3) =>
            new Item { shop = shop, id = id, name = name, price = price, desc = desc, kind = ItemKind.Food, shape = shape, c1 = c1, c2 = c2, c3 = c2, count = count, say = say };

        static readonly Color White = C(0.97f, 0.97f, 0.97f), Navy = C(0.12f, 0.16f, 0.36f), Brown = C(0.62f, 0.38f, 0.16f), Dark = C(0.25f, 0.15f, 0.08f),
            Rice = C(0.98f, 0.97f, 0.92f), Soup = C(0.75f, 0.5f, 0.25f), Bowl = C(0.55f, 0.12f, 0.1f), Cream = C(1f, 0.96f, 0.85f), Cup = C(0.85f, 0.95f, 1f);

        public static readonly List<Item> All = new List<Item>
        {
            // ───────── 服：浴衣の店 ─────────
            Clothes("fudan", StartClothes, "白梅の小袖", 0, "はじめから持っている服。白地に梅色の帯。",
                C(0.95f, 0.95f, 0.97f), C(0.85f, 0.3f, 0.45f), C(0.97f, 0.96f, 0.98f), C(0.85f, 0.3f, 0.45f), C(0.85f, 0.3f, 0.45f), C(1f, 0.85f, 0.4f)),
            Clothes("yukata", "sakura", "桜の浴衣", 2000, "うすい桜色の浴衣。千年桜の丘に似合う。",
                C(1f, 0.78f, 0.84f), C(0.95f, 0.5f, 0.62f), C(1f, 0.86f, 0.9f), C(0.9f, 0.4f, 0.55f), C(0.55f, 0.2f, 0.4f), C(1f, 1f, 1f)),
            Clothes("yukata", "aizome", "藍染めの浴衣", 2000, "こい藍色に、山吹の帯。夏祭りの定番。",
                C(0.16f, 0.24f, 0.5f), C(0.9f, 0.92f, 0.98f), C(0.2f, 0.3f, 0.58f), C(0.95f, 0.95f, 1f), C(0.95f, 0.75f, 0.3f), C(1f, 0.9f, 0.5f)),
            Clothes("yukata", "asagao", "朝顔の浴衣", 2000, "白地に青むらさきの朝顔。すずしげな一枚。",
                C(0.95f, 0.96f, 1f), C(0.45f, 0.4f, 0.85f), C(0.88f, 0.9f, 1f), C(0.45f, 0.4f, 0.85f), C(0.3f, 0.25f, 0.6f), C(0.7f, 0.8f, 1f)),
            Clothes("yukata", "kingyo_y", "金魚の浴衣", 2200, "白地に赤い金魚が泳ぐ浴衣。",
                C(1f, 0.97f, 0.95f), C(0.92f, 0.25f, 0.18f), C(1f, 0.9f, 0.88f), C(0.92f, 0.25f, 0.18f), C(0.95f, 0.3f, 0.25f), C(1f, 0.85f, 0.3f)),
            Clothes("yukata", "ajisai", "紫陽花の浴衣", 2400, "あじさい色のうすむらさき。雨の日にも。",
                C(0.72f, 0.68f, 0.92f), C(0.45f, 0.55f, 0.9f), C(0.8f, 0.76f, 0.96f), C(0.45f, 0.55f, 0.9f), C(0.3f, 0.35f, 0.7f), C(0.9f, 0.7f, 0.95f)),
            Clothes("yukata", "himawari", "向日葵の浴衣", 2400, "ひまわり色の明るい浴衣に、こげ茶の帯。",
                C(1f, 0.82f, 0.25f), C(0.55f, 0.35f, 0.15f), C(1f, 0.88f, 0.4f), C(0.55f, 0.35f, 0.15f), C(0.4f, 0.25f, 0.12f), C(0.3f, 0.6f, 0.25f)),
            Clothes("yukata", "shirayuri", "白百合の浴衣", 2600, "白と若葉色の、すっきりした浴衣。",
                C(0.98f, 0.98f, 0.95f), C(0.45f, 0.7f, 0.4f), C(1f, 1f, 0.97f), C(0.45f, 0.7f, 0.4f), C(0.35f, 0.6f, 0.35f), C(1f, 0.95f, 0.6f)),
            Clothes("yukata", "hanabi", "花火の浴衣", 2800, "夜空の紺に、袖口が花火のように光る。",
                C(0.1f, 0.12f, 0.3f), C(0.95f, 0.5f, 0.3f), C(0.14f, 0.16f, 0.38f), C(1f, 0.6f, 0.35f), C(0.95f, 0.45f, 0.55f), C(1f, 0.9f, 0.5f), true),

            // ───────── 服：振袖と晴れ着の店 ─────────
            Clothes("furisode", "yamabuki", "山吹の振袖", 4000, "あざやかな山吹色の振袖。遠くからでも目立つ。",
                C(0.98f, 0.76f, 0.2f), C(0.85f, 0.25f, 0.2f), C(1f, 0.84f, 0.35f), C(0.85f, 0.25f, 0.2f), C(0.3f, 0.2f, 0.5f), C(1f, 0.5f, 0.6f)),
            Clothes("furisode", "botan", "牡丹の振袖", 4500, "こい桃色に金の帯。はなやかな晴れ着。",
                C(0.9f, 0.3f, 0.5f), C(0.6f, 0.12f, 0.3f), C(0.95f, 0.45f, 0.62f), C(1f, 0.8f, 0.35f), C(1f, 0.8f, 0.35f), C(1f, 1f, 1f)),
            Clothes("furisode", "wakatake", "若竹の振袖", 4500, "竹林の色をうつした、みどりと白の振袖。",
                C(0.3f, 0.6f, 0.38f), C(0.95f, 0.97f, 0.9f), C(0.4f, 0.7f, 0.45f), C(0.95f, 0.97f, 0.9f), C(0.95f, 0.85f, 0.45f), C(1f, 0.95f, 0.7f)),
            Clothes("furisode", "yukiusagi", "雪うさぎの打掛", 5000, "まっ白な打掛に、紅のふち。",
                C(0.97f, 0.97f, 1f), C(0.85f, 0.12f, 0.2f), C(1f, 1f, 1f), C(0.85f, 0.12f, 0.2f), C(0.85f, 0.12f, 0.2f), C(1f, 0.85f, 0.4f)),
            Clothes("furisode", "hoshizora", "星空の振袖", 5000, "夜空の色。袖口がほんのり光る。",
                C(0.08f, 0.1f, 0.28f), C(0.45f, 0.6f, 1f), C(0.12f, 0.16f, 0.4f), C(0.6f, 0.85f, 1f), C(0.85f, 0.7f, 1f), C(1f, 1f, 0.7f), true),
            Clothes("furisode", "yozakura", "夜桜の振袖", 5500, "黒地に、光る桜色の袖口。",
                C(0.08f, 0.06f, 0.1f), C(0.95f, 0.55f, 0.7f), C(0.12f, 0.1f, 0.15f), C(1f, 0.6f, 0.78f), C(0.95f, 0.55f, 0.7f), C(1f, 0.85f, 0.9f), true),
            Clothes("furisode", "tsukiyo", "月夜の振袖", 6000, "ヤチヨとおそろいの色。紺に紅の袖口、空色の帯。",
                C(0.13f, 0.12f, 0.22f), C(0.2f, 0.18f, 0.32f), C(0.16f, 0.14f, 0.26f), C(0.78f, 0.2f, 0.32f), C(0.45f, 0.78f, 0.95f), C(1f, 0.6f, 0.75f)),
            Clothes("furisode", "kinran", "金襴の打掛", 6000, "金と紅の、いちばん豪華な打掛。",
                C(0.95f, 0.75f, 0.3f), C(0.75f, 0.1f, 0.12f), C(1f, 0.82f, 0.4f), C(0.75f, 0.1f, 0.12f), C(0.75f, 0.1f, 0.12f), C(1f, 1f, 0.8f), true),

            // ───────── 服：ふだん着の店 ─────────
            Clothes("fudan", "wakakusa", "若草の小袖", 3000, "春の若草色。だいだいの帯でさわやかに。",
                C(0.62f, 0.82f, 0.5f), C(0.95f, 0.92f, 0.75f), C(0.7f, 0.88f, 0.58f), C(0.96f, 0.6f, 0.4f), C(0.95f, 0.6f, 0.35f), C(1f, 0.85f, 0.4f)),
            Clothes("fudan", "momiji", "紅葉の羽織", 4000, "深い紅葉色に金の帯。秋の夜長に。",
                C(0.7f, 0.16f, 0.12f), C(0.25f, 0.12f, 0.08f), C(0.8f, 0.3f, 0.15f), C(0.98f, 0.75f, 0.3f), C(0.98f, 0.75f, 0.3f), C(1f, 0.9f, 0.6f)),
            Clothes("fudan", "usuzumi", "薄墨の小袖", 2600, "うすい墨色の、おちついた小袖。",
                C(0.55f, 0.56f, 0.6f), C(0.3f, 0.3f, 0.35f), C(0.62f, 0.63f, 0.67f), C(0.85f, 0.4f, 0.4f), C(0.85f, 0.4f, 0.4f), C(0.95f, 0.95f, 0.95f)),
            Clothes("fudan", "samue", "藍の作務衣", 2500, "動きやすい藍色の作務衣。",
                C(0.18f, 0.25f, 0.42f), C(0.14f, 0.2f, 0.34f), C(0.2f, 0.28f, 0.46f), C(0.14f, 0.2f, 0.34f), C(0.9f, 0.9f, 0.9f), C(0.9f, 0.9f, 0.9f)),
            Clothes("fudan", "cha_haori", "茶の羽織", 2800, "あたたかい茶色の羽織。",
                C(0.5f, 0.33f, 0.2f), C(0.32f, 0.2f, 0.12f), C(0.56f, 0.38f, 0.24f), C(0.95f, 0.9f, 0.8f), C(0.95f, 0.9f, 0.8f), C(0.95f, 0.75f, 0.4f)),
            Hakama("fudan", "shosei", "書生の袴", 3000, "白い着物に紺の袴。",
                C(0.95f, 0.95f, 0.95f), C(0.15f, 0.18f, 0.35f), C(0.95f, 0.95f, 0.95f), C(0.15f, 0.18f, 0.35f), C(0.15f, 0.18f, 0.35f)),
            Hakama("fudan", "jogakusei", "女学生の袴", 3200, "矢絣のむらさきの着物に、えび茶の袴。",
                C(0.72f, 0.5f, 0.8f), C(0.5f, 0.15f, 0.18f), C(0.78f, 0.58f, 0.85f), C(0.5f, 0.15f, 0.18f), C(0.95f, 0.85f, 0.4f)),

            // ───────── 飾り（小間物屋。どの支店も同じ）─────────
            Acc("kanzashi", "桜のかんざし", Slot.Head, 1000, "髪にさす、桜の花のかんざし。"),
            Acc("uchiwa", "金魚のうちわ", Slot.Hand, 1000, "手に持つうちわ。金魚の絵入り。"),
            Acc("nekomimi", "ねこ耳", Slot.Head, 1200, "ふわふわのねこ耳。"),
            Acc("ribbon", "大きなリボン", Slot.Head, 1200, "頭のうしろに、紅の大きなリボン。"),
            Acc("hoshikan", "星のかんざし", Slot.Head, 1500, "夜に光る、星のかたちのかんざし。"),
            Acc("kitsune", "狐のお面", Slot.Face, 2000, "頭の横につける、お祭りのお面。"),
            Acc("bangasa", "番傘", Slot.Hand, 2500, "朱色の番傘をさして歩く。"),
            Acc("odango", "ヤチヨのおだんご髪", Slot.Hair, 3000, "ヤチヨとおなじ、おだんごとツインテール。"),
            Acc("kingyo", "光る金魚", Slot.Shoulder, 3000, "肩のあたりを、光る金魚がついてくる。"),
            Acc("hagoromo", "天女の羽衣", Slot.Back, 3000, "肩にかかる、光る羽衣。"),

            // ───────── 食べ物：甘味処 ─────────
            Food("kanmi", "mitarashi", "みたらし団子", 100, FoodShape.Skewer, C(0.8f, 0.55f, 0.25f), C(0.85f, 0.75f, 0.55f), "甘じょっぱいたれの団子。", "みたらし団子をたべた。たれがあまじょっぱくておいしい！"),
            Food("kanmi", "daifuku", "いちご大福", 200, FoodShape.Round, White, C(0.9f, 0.2f, 0.3f), "いちごまるごと一粒。", "いちご大福をたべた。あまずっぱい！"),
            Food("kanmi", "warabi", "わらび餅", 250, FoodShape.Plate, C(0.85f, 0.8f, 0.6f), C(0.75f, 0.6f, 0.35f), "きなこをたっぷりかけて。", "わらび餅をたべた。ぷるぷるで、きなこが香ばしい。", 4),
            Food("kanmi", "tsukimi", "月見だんご", 300, FoodShape.Plate, White, C(0.6f, 0.4f, 0.25f), "満月の下で食べる、まっ白なだんご。", "月見だんごをたべた。お月さまを見ながら、もちもち。", 3),
            Food("kanmi", "anmitsu", "あんみつ", 350, FoodShape.Bowl, C(0.45f, 0.2f, 0.25f), C(0.95f, 0.5f, 0.6f), "寒天と白玉とあんこ。", "あんみつをたべた。黒みつがとろっとあまい。"),
            Food("kanmi", "parfait", "抹茶パフェ", 500, FoodShape.Parfait, C(0.45f, 0.65f, 0.3f), White, "抹茶アイスと白玉のパフェ。", "抹茶パフェをたべた。ほろにがくてあまい。ごほうびの味！"),

            // ───────── 食べ物：たこ焼き ─────────
            Food("takoyaki", "takoyaki", "たこ焼き", 300, FoodShape.Tray, Brown, Dark, "ソースとかつおぶし。", "たこ焼きをたべた。あつあつ…！中がとろとろ。", 6),
            Food("takoyaki", "negitako", "ねぎだこ", 350, FoodShape.Tray, Brown, C(0.4f, 0.75f, 0.3f), "ねぎをどっさり、ぽん酢で。", "ねぎだこをたべた。ぽん酢でさっぱり！", 6),
            Food("takoyaki", "akashi", "明石焼き", 400, FoodShape.Tray, C(0.98f, 0.85f, 0.5f), C(0.8f, 0.6f, 0.3f), "だしにつけて食べる、ふわふわの玉子焼き。", "明石焼きをたべた。だしがしみて、ふわっふわ。", 6),
            Food("takoyaki", "ikayaki", "いか焼き", 300, FoodShape.Skewer, C(0.75f, 0.45f, 0.25f), C(0.85f, 0.75f, 0.55f), "まるごと一ぱい、しょうゆで焼いた。", "いか焼きをたべた。しょうゆのこげた香りがたまらない。", 2),

            // ───────── 食べ物：らーめん ─────────
            Food("ramen", "shoyu", "しょうゆらーめん", 450, FoodShape.Bowl, Soup, C(0.95f, 0.85f, 0.4f), "すみきった、むかしながらの味。", "しょうゆらーめんをすすった。ほっとする味。"),
            Food("ramen", "miso", "みそらーめん", 480, FoodShape.Bowl, C(0.8f, 0.55f, 0.25f), C(0.95f, 0.95f, 0.6f), "コーンとバターをのせて。", "みそらーめんをすすった。からだがぽかぽかする。"),
            Food("ramen", "tonkotsu", "とんこつらーめん", 500, FoodShape.Bowl, C(0.95f, 0.92f, 0.85f), C(0.9f, 0.4f, 0.4f), "白くて濃い、とろとろスープ。", "とんこつらーめんをすすった。こってり、しあわせ。"),
            Food("ramen", "tsukimiudon", "月見うどん", 380, FoodShape.Bowl, C(0.85f, 0.7f, 0.45f), C(1f, 0.85f, 0.2f), "まんまるの玉子をお月さまに見立てて。", "月見うどんをすすった。お月さまの玉子がとろり。"),

            // ───────── 食べ物：すし ─────────
            Food("sushi", "maguro", "まぐろの握り", 400, FoodShape.Sushi, C(0.8f, 0.15f, 0.2f), Rice, "あかみのまぐろ、2かん。", "まぐろの握りをたべた。とろける〜！", 2),
            Food("sushi", "salmon", "サーモンの握り", 350, FoodShape.Sushi, C(1f, 0.55f, 0.35f), Rice, "あぶらののったサーモン、2かん。", "サーモンの握りをたべた。あまくてとろとろ。", 2),
            Food("sushi", "tamago", "玉子の握り", 200, FoodShape.Sushi, C(1f, 0.85f, 0.3f), Rice, "あまい玉子焼きの握り。", "玉子の握りをたべた。ふんわりあまい。", 2),
            Food("sushi", "inari", "いなり寿司", 150, FoodShape.Sushi, C(0.8f, 0.55f, 0.25f), C(0.8f, 0.55f, 0.25f), "あまからいおあげに、すし飯。", "いなり寿司をたべた。おあげがじゅわっ。", 2),
            Food("sushi", "makizushi", "巻きずし", 250, FoodShape.Plate, C(0.1f, 0.15f, 0.1f), Rice, "かんぴょうと玉子ときゅうり。", "巻きずしをたべた。いろんな味がして楽しい。", 4),

            // ───────── 食べ物：焼き鳥 ─────────
            Food("yakitori", "negima", "ねぎま", 150, FoodShape.Skewer, C(0.75f, 0.5f, 0.3f), C(0.85f, 0.75f, 0.55f), "とり肉とねぎを交互に。", "ねぎまをたべた。たれがこうばしい！", 4),
            Food("yakitori", "tsukune", "つくね", 150, FoodShape.Skewer, C(0.55f, 0.32f, 0.18f), C(0.85f, 0.75f, 0.55f), "ふっくら、たれ焼き。", "つくねをたべた。ふわふわでジューシー。", 3),
            Food("yakitori", "kawa", "とり皮の塩", 120, FoodShape.Skewer, C(0.9f, 0.75f, 0.5f), C(0.85f, 0.75f, 0.55f), "かりかりに焼いた皮。", "とり皮の塩をたべた。かりっかり！", 4),
            Food("yakitori", "tomorokoshi", "焼きとうもろこし", 200, FoodShape.Corn, C(1f, 0.82f, 0.25f), C(0.6f, 0.4f, 0.15f), "しょうゆを塗って焼いた。", "焼きとうもろこしをかじった。しょうゆがあまい！"),
            Food("yakitori", "frank", "フランクフルト", 200, FoodShape.StickBall, C(0.75f, 0.3f, 0.2f), C(1f, 0.85f, 0.2f), "ケチャップとからし。", "フランクフルトをたべた。ぱりっ、じゅわっ。"),

            // ───────── 食べ物：おでん ─────────
            Food("oden", "daikon", "だいこん", 100, FoodShape.Bowl, C(0.85f, 0.7f, 0.45f), C(0.95f, 0.9f, 0.75f), "だしがしみしみ。", "おでんのだいこんをたべた。だしがじゅわーっ。"),
            Food("oden", "oden_tamago", "たまご", 120, FoodShape.Bowl, C(0.85f, 0.7f, 0.45f), C(0.75f, 0.55f, 0.3f), "茶色くそまった、ゆでたまご。", "おでんのたまごをたべた。黄身までだしの味。"),
            Food("oden", "chikuwa", "ちくわぶ串", 100, FoodShape.Skewer, C(0.9f, 0.85f, 0.7f), C(0.85f, 0.75f, 0.55f), "もちもちのちくわぶ。", "ちくわぶをたべた。もっちもち。", 2),
            Food("oden", "oden_mori", "おでん盛り合わせ", 400, FoodShape.Bowl, C(0.85f, 0.7f, 0.45f), C(0.95f, 0.95f, 0.85f), "だいこん・たまご・こんにゃく・ちくわ。", "おでん盛り合わせをたべた。しみるなあ…。"),

            // ───────── 食べ物：かき氷と飲み物 ─────────
            Food("kori", "ichigokori", "いちごかき氷", 250, FoodShape.Puff, C(1f, 0.45f, 0.55f), Cup, "まっかないちごシロップ。", "いちごかき氷をたべた。キーンとする〜！"),
            Food("kori", "ujikintoki", "宇治金時かき氷", 350, FoodShape.Puff, C(0.5f, 0.72f, 0.35f), Cup, "抹茶シロップとあずき。", "宇治金時をたべた。抹茶とあずきが合う！"),
            Food("kori", "ramune", "ラムネ", 120, FoodShape.Cup, C(0.55f, 0.85f, 0.95f), C(0.2f, 0.45f, 0.9f), "ビー玉入りのラムネ。", "ラムネをのんだ。しゅわっとしてすっきり！"),
            Food("kori", "hiyashiame", "冷やし飴", 150, FoodShape.Cup, C(0.85f, 0.65f, 0.3f), White, "しょうがのきいた、むかしの飲み物。", "冷やし飴をのんだ。しょうががぴりっ。"),
            Food("kori", "amazake", "甘酒", 180, FoodShape.Cup, Cream, C(0.6f, 0.4f, 0.25f), "あったかい、米こうじの甘酒。", "甘酒をのんだ。ほんのりあまくて、あったまる。"),

            // ───────── 食べ物：クレープと縁日のおやつ ─────────
            Food("crepe", "crepe", "いちごクレープ", 400, FoodShape.Crepe, C(0.95f, 0.8f, 0.55f), C(1f, 0.4f, 0.5f), "生クリームといちご。", "いちごクレープをたべた。クリームがふわふわ！"),
            Food("crepe", "chocobanana", "チョコバナナ", 250, FoodShape.StickBall, C(0.35f, 0.2f, 0.12f), C(1f, 0.6f, 0.8f), "カラフルなつぶつぶつき。", "チョコバナナをたべた。縁日の味！"),
            Food("crepe", "taiyaki", "たい焼き", 150, FoodShape.Fish, Brown, Brown, "しっぽまであんこ入り。", "たい焼きをたべた。しっぽまであんこがぎっしり！"),
            Food("crepe", "ringoame", "りんご飴", 200, FoodShape.Round, C(0.85f, 0.08f, 0.12f), C(0.85f, 0.75f, 0.55f), "つやつやの、まっかなりんご飴。", "りんご飴をかじった。カリッ、あまずっぱい！"),
            Food("crepe", "wataame", "わたあめ", 200, FoodShape.Puff, C(1f, 0.82f, 0.9f), C(0.85f, 0.75f, 0.55f), "ふわふわの雲みたい。", "わたあめをたべた。口のなかで、すっと消えた。"),
        };

        public static Item Get(string id) => All.Find(i => i.id == id);

        public static IEnumerable<Item> Of(ItemKind k)
        {
            foreach (var i in All) if (i.kind == k) yield return i;
        }

        public static IEnumerable<Item> InShop(string group)
        {
            foreach (var i in All) if (i.shop == group && i.price > 0) yield return i;
        }

        public static string Yen(int v) => v.ToString("#,0");
    }
}
