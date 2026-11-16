using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    public enum ItemKind { Clothes, Accessory, Food }

    /// <summary>飾りをつける場所（1か所に1つ）。</summary>
    public enum Slot { None, Hair, Head, Face, Hand, Shoulder, Back }

    public class Item
    {
        public string id, name, desc;
        public ItemKind kind;
        public Slot slot;
        public int price;
        // 服の色（すそ・帯など）
        public Color robe, robe2, sleeve, cuff, sash, accent;
        public bool glowCuff;
        // 食べ物を食べたときのひとこと
        public string say;
    }

    /// <summary>
    /// 月見商店街で売っているもの。値段は「安め」（食べ物 100〜500、飾り 1000〜3000、服 2000〜6000 ふじゅ〜）。
    /// 支払いはミニヤチヨの本物のふじゅ〜（Fuju.cs）。持ち物はツクヨミの save.json に残す。
    /// </summary>
    public static class Catalog
    {
        public const string StartClothes = "shiraume";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static Item Clothes(string id, string name, int price, string desc, Color robe, Color robe2, Color sleeve, Color cuff, Color sash, Color accent, bool glow = false) =>
            new Item { id = id, name = name, price = price, desc = desc, kind = ItemKind.Clothes, robe = robe, robe2 = robe2, sleeve = sleeve, cuff = cuff, sash = sash, accent = accent, glowCuff = glow };

        static Item Acc(string id, string name, Slot slot, int price, string desc) =>
            new Item { id = id, name = name, price = price, desc = desc, kind = ItemKind.Accessory, slot = slot };

        static Item Food(string id, string name, int price, string desc, string say) =>
            new Item { id = id, name = name, price = price, desc = desc, kind = ItemKind.Food, say = say };

        public static readonly List<Item> All = new List<Item>
        {
            // ── 服（呉服屋「月白」）──
            Clothes(StartClothes, "白梅の小袖", 0, "はじめから持っている服。白地に梅色の帯。",
                C(0.95f, 0.95f, 0.97f), C(0.85f, 0.3f, 0.45f), C(0.97f, 0.96f, 0.98f), C(0.85f, 0.3f, 0.45f), C(0.85f, 0.3f, 0.45f), C(1f, 0.85f, 0.4f)),
            Clothes("sakura", "桜の浴衣", 2000, "うすい桜色の浴衣。千年桜の丘に似合う。",
                C(1f, 0.78f, 0.84f), C(0.95f, 0.5f, 0.62f), C(1f, 0.86f, 0.9f), C(0.9f, 0.4f, 0.55f), C(0.55f, 0.2f, 0.4f), C(1f, 1f, 1f)),
            Clothes("aizome", "藍染めの浴衣", 2000, "こい藍色に、山吹の帯。夏祭りの定番。",
                C(0.16f, 0.24f, 0.5f), C(0.9f, 0.92f, 0.98f), C(0.2f, 0.3f, 0.58f), C(0.95f, 0.95f, 1f), C(0.95f, 0.75f, 0.3f), C(1f, 0.9f, 0.5f)),
            Clothes("wakakusa", "若草の小袖", 3000, "春の若草色。だいだいの帯でさわやかに。",
                C(0.62f, 0.82f, 0.5f), C(0.95f, 0.92f, 0.75f), C(0.7f, 0.88f, 0.58f), C(0.96f, 0.6f, 0.4f), C(0.95f, 0.6f, 0.35f), C(1f, 0.85f, 0.4f)),
            Clothes("yamabuki", "山吹の振袖", 4000, "あざやかな山吹色の振袖。遠くからでも目立つ。",
                C(0.98f, 0.76f, 0.2f), C(0.85f, 0.25f, 0.2f), C(1f, 0.84f, 0.35f), C(0.85f, 0.25f, 0.2f), C(0.3f, 0.2f, 0.5f), C(1f, 0.5f, 0.6f)),
            Clothes("momiji", "紅葉の羽織", 4000, "深い紅葉色に金の帯。秋の夜長に。",
                C(0.7f, 0.16f, 0.12f), C(0.25f, 0.12f, 0.08f), C(0.8f, 0.3f, 0.15f), C(0.98f, 0.75f, 0.3f), C(0.98f, 0.75f, 0.3f), C(1f, 0.9f, 0.6f)),
            Clothes("yukiusagi", "雪うさぎの打掛", 5000, "まっ白な打掛に、紅のふち。",
                C(0.97f, 0.97f, 1f), C(0.85f, 0.12f, 0.2f), C(1f, 1f, 1f), C(0.85f, 0.12f, 0.2f), C(0.85f, 0.12f, 0.2f), C(1f, 0.85f, 0.4f)),
            Clothes("hoshizora", "星空の振袖", 5000, "夜空の色。袖口がほんのり光る。",
                C(0.08f, 0.1f, 0.28f), C(0.45f, 0.6f, 1f), C(0.12f, 0.16f, 0.4f), C(0.6f, 0.85f, 1f), C(0.85f, 0.7f, 1f), C(1f, 1f, 0.7f), true),
            Clothes("tsukiyo", "月夜の振袖", 6000, "ヤチヨとおそろいの色。紺に紅の袖口、空色の帯。",
                C(0.13f, 0.12f, 0.22f), C(0.2f, 0.18f, 0.32f), C(0.16f, 0.14f, 0.26f), C(0.78f, 0.2f, 0.32f), C(0.45f, 0.78f, 0.95f), C(1f, 0.6f, 0.75f)),

            // ── 飾り（小間物屋「星屑」）──
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

            // ── 食べ物（甘味の屋台「うさぎ堂」）──
            Food("mitarashi", "みたらし団子", 100, "甘じょっぱいたれの団子。", "みたらし団子をたべた。たれがあまじょっぱくておいしい！"),
            Food("ramune", "ラムネ", 120, "ビー玉入りのラムネ。", "ラムネをのんだ。しゅわっとしてすっきり！"),
            Food("taiyaki", "たい焼き", 150, "しっぽまであんこ入り。", "たい焼きをたべた。しっぽまであんこがぎっしり！"),
            Food("daifuku", "いちご大福", 200, "いちごまるごと一粒。", "いちご大福をたべた。あまずっぱい！"),
            Food("tsukimi", "月見だんご", 300, "満月の下で食べる、まっ白なだんご。", "月見だんごをたべた。お月さまを見ながら、もちもち。"),
            Food("parfait", "抹茶パフェ", 500, "抹茶アイスと白玉のパフェ。", "抹茶パフェをたべた。ほろにがくてあまい。ごほうびの味！"),
        };

        public static Item Get(string id) => All.Find(i => i.id == id);

        public static IEnumerable<Item> Of(ItemKind k)
        {
            foreach (var i in All) if (i.kind == k) yield return i;
        }

        public static string Yen(int v) => v.ToString("#,0");
    }
}
