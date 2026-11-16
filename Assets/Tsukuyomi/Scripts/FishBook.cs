using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>釣れる時間帯。</summary>
    public enum FishTime { Any, Day, Night, Morning, Fireworks, Midnight, Dawn }

    /// <summary>魚の種類（図鑑の1行）。</summary>
    public class FishKind
    {
        public string id, name, desc;
        public int rank;            // 0 ふつう・1 めずらしい・2 まぼろし
        public float min, max;      // 大きさ（cm）
        public Color c1, c2;        // 体の色・もようの色
        public bool glow;           // ほのかに光る
        public float fat = 0.34f;   // 体の高さ / 長さ
        public int shape;           // 0 ふつう・1 まるい（琉金・出目金）・2 細長い（ドジョウ・ナマズ）・3 マンタ
        public int pattern;         // 0 なし・1 まだら・2 頭に赤丸・3 帯
        public bool longTail, whiskers, bigEyes;
        public FishTime time;
        public float weight = 10f;  // 出やすさ
        public float power = 0.5f;  // 引きの強さ 0〜1
        public float bite = 0.85f;  // 合わせられる時間（秒）

        public string RankName => rank == 0 ? "ふつう" : rank == 1 ? "めずらしい" : "まぼろし";
        public string Stars => rank == 0 ? "★" : rank == 1 ? "★★" : "★★★";

        public string TimeHint => time switch
        {
            FishTime.Day => "昼間（6〜18時）に見かけるらしい",
            FishTime.Night => "夜（18〜6時）に見かけるらしい",
            FishTime.Morning => "朝はやく（5〜10時）に見かけるらしい",
            FishTime.Fireworks => "花火の時間（19〜23時）に現れるらしい",
            FishTime.Midnight => "真夜中（0〜4時）にだけ現れるらしい",
            FishTime.Dawn => "夜明け（4〜7時）にだけ現れるらしい",
            _ => "いつでも釣れるらしい",
        };

        public bool Active(DateTime t)
        {
            int h = t.Hour;
            return time switch
            {
                FishTime.Day => h >= 6 && h < 18,
                FishTime.Night => h >= 18 || h < 6,
                FishTime.Morning => h >= 5 && h < 10,
                FishTime.Fireworks => h >= 19 && h < 23,
                FishTime.Midnight => h < 4,
                FishTime.Dawn => h >= 4 && h < 7,
                _ => true,
            };
        }
    }

    /// <summary>図鑑の記録（save.json）。</summary>
    [Serializable]
    public class FishRecord
    {
        public string id;
        public int count;
        public float best;
        public string first = "";
    }

    /// <summary>釣った1匹の結果（画面に出す）。</summary>
    public class CatchResult
    {
        public FishKind kind;
        public float cm;
        public bool isNew, isBest, weekBest;
        public int weekRank;
    }

    /// <summary>
    /// 釣り堀の魚20種と、図鑑・週ごとの大物番付。
    /// 番付の仮想の釣り人は、週ごとに決まった乱数で「いつ・何cmを釣ったか」が決まり、その時刻が来ると記録が出る。
    /// 釣りではふじゅ〜は増えない（記録だけ）。
    /// </summary>
    public static class FishBook
    {
        static FishKind F(string id, string name, int rank, float min, float max, Color c1, Color c2, FishTime time, float weight, float power, string desc)
            => new FishKind { id = id, name = name, rank = rank, min = min, max = max, c1 = c1, c2 = c2, time = time, weight = weight, power = power, desc = desc };

        static Color C(float r, float g, float b) => new Color(r, g, b);

        public static readonly FishKind[] All = Build();

        static FishKind[] Build()
        {
            var l = new List<FishKind>();
            // ふつう（12）
            l.Add(F("higoi", "緋鯉", 0, 30, 70, C(0.92f, 0.3f, 0.12f), C(0.95f, 0.55f, 0.2f), FishTime.Any, 10, 0.65f, "池の主役。赤い体でゆうゆうと泳ぐ"));
            l.Add(F("magoi", "真鯉", 0, 35, 80, C(0.2f, 0.19f, 0.18f), C(0.32f, 0.3f, 0.25f), FishTime.Any, 9, 0.8f, "黒くて力もち。大物ほど引きが強い"));
            var k = F("kohaku", "紅白", 0, 30, 75, C(0.96f, 0.95f, 0.92f), C(0.88f, 0.15f, 0.1f), FishTime.Any, 8, 0.7f, "白い体に赤のもよう。錦鯉の王道"); k.pattern = 1; l.Add(k);
            l.Add(F("wakin", "和金", 0, 8, 20, C(1f, 0.45f, 0.1f), C(1f, 0.7f, 0.3f), FishTime.Any, 11, 0.25f, "いちばん身近な金魚。すばしっこい"));
            k = F("demekin", "出目金", 0, 8, 18, C(0.1f, 0.09f, 0.12f), C(0.25f, 0.2f, 0.3f), FishTime.Any, 7, 0.2f, "ぎょろっとした目がチャームポイント"); k.shape = 1; k.bigEyes = true; k.longTail = true; l.Add(k);
            k = F("ryukin", "琉金", 0, 10, 20, C(0.95f, 0.25f, 0.15f), C(0.98f, 0.95f, 0.92f), FishTime.Day, 7, 0.25f, "まるい体と、ひらひらの長い尾"); k.shape = 1; k.pattern = 1; k.longTail = true; l.Add(k);
            k = F("medaka", "メダカ", 0, 2, 4, C(0.78f, 0.72f, 0.55f), C(0.9f, 0.85f, 0.7f), FishTime.Day, 9, 0.08f, "ちいさいけれど、合わせるのがむずかしい"); k.fat = 0.24f; k.bite = 0.55f; l.Add(k);
            k = F("funa", "フナ", 0, 10, 30, C(0.55f, 0.5f, 0.35f), C(0.65f, 0.6f, 0.45f), FishTime.Any, 9, 0.45f, "釣りの基本。のんびり食いつく"); k.bite = 1.1f; l.Add(k);
            k = F("dojo", "ドジョウ", 0, 8, 15, C(0.5f, 0.42f, 0.3f), C(0.35f, 0.3f, 0.2f), FishTime.Any, 6, 0.3f, "にょろっと逃げる。口にひげがある"); k.shape = 2; k.fat = 0.14f; k.whiskers = true; l.Add(k);
            k = F("oikawa", "オイカワ", 0, 10, 16, C(0.55f, 0.72f, 0.82f), C(0.95f, 0.45f, 0.55f), FishTime.Day, 6, 0.4f, "体に虹色の帯。川から遊びに来たらしい"); k.fat = 0.26f; k.pattern = 3; l.Add(k);
            k = F("tanago", "タナゴ", 0, 5, 9, C(0.72f, 0.76f, 0.82f), C(0.95f, 0.5f, 0.65f), FishTime.Morning, 6, 0.15f, "朝になると色づく、ちいさな宝石"); k.pattern = 3; k.fat = 0.4f; l.Add(k);
            k = F("namazu", "ナマズ", 0, 30, 60, C(0.3f, 0.3f, 0.26f), C(0.45f, 0.43f, 0.36f), FishTime.Night, 7, 0.85f, "夜になると動きだす。ひげが長い"); k.shape = 2; k.fat = 0.2f; k.whiskers = true; l.Add(k);
            // めずらしい（5）
            l.Add(F("ogon", "黄金", 1, 40, 80, C(1f, 0.8f, 0.3f), C(1f, 0.92f, 0.6f), FishTime.Any, 1.4f, 0.85f, "全身が金色にかがやく錦鯉。見られたら運がいい"));
            k = F("tancho", "丹頂", 1, 35, 70, C(0.97f, 0.97f, 0.95f), C(0.9f, 0.1f, 0.1f), FishTime.Any, 1.2f, 0.75f, "頭に赤い丸がひとつ。めでたい鯉"); k.pattern = 2; l.Add(k);
            k = F("sakuragoi", "桜鯉", 1, 30, 65, C(1f, 0.72f, 0.82f), C(1f, 0.9f, 0.94f), FishTime.Morning, 1.6f, 0.7f, "朝の池にだけ現れる、桜色の鯉"); k.pattern = 1; l.Add(k);
            k = F("tsukimi", "月見金魚", 1, 10, 22, C(1f, 0.9f, 0.55f), C(1f, 1f, 0.85f), FishTime.Night, 1.6f, 0.35f, "月の光をためこんで、夜にほのかに光る"); k.glow = true; k.longTail = true; l.Add(k);
            k = F("hoshikuzu", "星屑メダカ", 1, 3, 5, C(0.4f, 0.85f, 1f), C(1f, 1f, 1f), FishTime.Night, 1.8f, 0.12f, "体に星くずのような光の点がある"); k.glow = true; k.fat = 0.24f; k.pattern = 1; k.bite = 0.55f; l.Add(k);
            // まぼろし（3）
            k = F("manta", "空マンタの子", 2, 50, 90, C(0.35f, 0.75f, 1f), C(0.9f, 0.6f, 1f), FishTime.Fireworks, 0.35f, 1f, "空を泳ぐマンタの子ども。花火の音にさそわれて池へ降りてくる"); k.shape = 3; k.glow = true; k.fat = 0.15f; l.Add(k);
            k = F("ryugu", "竜宮の鯉", 2, 70, 110, C(0.3f, 0.9f, 0.8f), C(1f, 0.5f, 0.9f), FishTime.Midnight, 0.3f, 1f, "真夜中だけ、池の底の竜宮から上がってくるという"); k.glow = true; k.pattern = 1; k.longTail = true; l.Add(k);
            k = F("kaguya", "かぐや金魚", 2, 15, 25, C(1f, 0.96f, 0.8f), C(1f, 0.7f, 0.85f), FishTime.Dawn, 0.3f, 0.6f, "夜明けの月の光から生まれたといわれる、天女の金魚"); k.glow = true; k.longTail = true; k.shape = 1; l.Add(k);
            return l.ToArray();
        }

        public static FishKind Get(string id) => Array.Find(All, f => f.id == id);

        /// <summary>いまの時刻に釣れる魚から1匹えらぶ。</summary>
        public static FishKind Pick(DateTime t)
        {
            float sum = 0;
            foreach (var f in All) if (f.Active(t)) sum += f.weight;
            float r = UnityEngine.Random.value * sum;
            foreach (var f in All)
            {
                if (!f.Active(t)) continue;
                r -= f.weight;
                if (r <= 0) return f;
            }
            return All[0];
        }

        /// <summary>大きさ（大きいほど出にくい）。</summary>
        public static float Size(FishKind f) =>
            Mathf.Round((f.min + (f.max - f.min) * Mathf.Pow(UnityEngine.Random.value, 1.7f)) * 10f) / 10f;

        // ───────── 記録 ─────────

        static SaveData D => GameSession.Instance != null ? GameSession.Instance.Data : null;

        public static FishRecord Rec(string id)
        {
            var d = D;
            if (d == null || d.fish == null) return null;
            return d.fish.Find(r => r.id == id);
        }

        public static int Known
        {
            get { var d = D; return d == null || d.fish == null ? 0 : d.fish.Count; }
        }

        public static int Total
        {
            get { var d = D; return d == null ? 0 : d.fishTotal; }
        }

        /// <summary>1匹釣れたら記録して保存する。</summary>
        public static CatchResult Record(FishKind f, float cm)
        {
            var res = new CatchResult { kind = f, cm = cm };
            var d = D;
            if (d == null) return res;
            if (d.fish == null) d.fish = new List<FishRecord>();
            RollWeek(d);
            var rec = d.fish.Find(r => r.id == f.id);
            if (rec == null)
            {
                rec = new FishRecord { id = f.id, first = Ts.Now.ToString("yyyy-MM-dd HH:mm") };
                d.fish.Add(rec);
                res.isNew = true;
            }
            rec.count++;
            if (cm > rec.best) { res.isBest = !res.isNew; rec.best = cm; }
            d.fishTotal++;
            if (cm > d.weekBest)
            {
                d.weekBest = cm; d.weekFish = f.id;
                res.weekBest = true;
            }
            res.weekRank = PlayerRank(Ts.Now);
            GameSession.Instance.SaveItems();
            return res;
        }

        // ───────── 週ごとの大物番付 ─────────

        public static DateTime WeekStart(DateTime t)
        {
            int back = ((int)t.DayOfWeek + 6) % 7; // 月曜はじまり
            return t.Date.AddDays(-back);
        }

        static string Key(DateTime monday) => monday.ToString("yyyy-MM-dd");

        /// <summary>週が変わっていたら、先週の順位を残して今週の記録を空にする。</summary>
        public static void RollWeek(SaveData d)
        {
            if (d == null) return;
            var now = Ts.Now;
            string key = Key(WeekStart(now));
            if (d.weekKey == key) return;
            if (!string.IsNullOrEmpty(d.weekKey) && d.weekBest > 0 &&
                DateTime.TryParse(d.weekKey, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var prev))
            {
                var list = Board(prev, prev.AddDays(7), d.weekBest, d.weekFish);
                d.lastWeekRank = list.FindIndex(e => e.player) + 1;
                d.lastWeekCm = d.weekBest;
                d.lastWeekCount = list.Count;
            }
            else if (!string.IsNullOrEmpty(d.weekKey)) { d.lastWeekRank = 0; d.lastWeekCm = 0; }
            d.weekKey = key; d.weekBest = 0; d.weekFish = "";
        }

        public struct Entry
        {
            public string who, fish;
            public float cm;
            public bool player;
        }

        static readonly string[] Anglers =
        {
            "釣り好きの源さん", "月見うさぎ", "ねこまんま", "池の番人", "ぷかぷか", "たぬき屋", "夜ふかしの蛍", "まったり亭", "こいこい", "しずく",
            "竿一本の旅人", "茶屋のおばあちゃん", "ぽんぽこ", "鯉のぼり", "星見の子", "かえで", "水面のひかり", "のんびり太郎", "まんまる", "さざなみ",
        };

        static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }

        /// <summary>仮想の釣り人の記録（その週の始まりから now までに釣れたぶん）。</summary>
        static List<Entry> Virtual(DateTime monday, DateTime now)
        {
            var list = new List<Entry>();
            var rng = new System.Random((int)(Hash(Key(monday)) & 0x7fffffff));
            for (int i = 0; i < Anglers.Length; i++)
            {
                double skill = 0.25 + rng.NextDouble() * 0.75;
                int n = 2 + (int)(skill * 5);
                float best = 0; string bestFish = "";
                for (int k = 0; k < n; k++)
                {
                    var when = monday.AddHours(rng.NextDouble() * 24 * 7);
                    double u = rng.NextDouble(), v = rng.NextDouble(), w = rng.NextDouble();
                    float cm; string fish;
                    if (w < 0.012 * skill) { cm = 90f + (float)u * 18f; fish = "竜宮の鯉"; }
                    else
                    {
                        cm = 18f + 66f * (float)Math.Pow(u, 1.5 - skill * 0.8);
                        fish = cm > 62 ? (v < 0.45 ? "真鯉" : v < 0.7 ? "緋鯉" : v < 0.9 ? "紅白" : "黄金")
                             : cm > 32 ? (v < 0.3 ? "緋鯉" : v < 0.55 ? "紅白" : v < 0.75 ? "真鯉" : v < 0.9 ? "ナマズ" : "丹頂")
                             : (v < 0.5 ? "フナ" : v < 0.8 ? "和金" : "琉金");
                    }
                    cm = Mathf.Round(cm * 10f) / 10f;
                    if (when <= now && cm > best) { best = cm; bestFish = fish; }
                }
                if (best > 0) list.Add(new Entry { who = Anglers[i], fish = bestFish, cm = best });
            }
            return list;
        }

        static List<Entry> Board(DateTime monday, DateTime now, float mine, string mineFish)
        {
            var list = Virtual(monday, now);
            if (mine > 0) list.Add(new Entry { who = "あなた", fish = Get(mineFish)?.name ?? "", cm = mine, player = true });
            list.Sort((a, b) => b.cm != a.cm ? b.cm.CompareTo(a.cm) : b.player.CompareTo(a.player));
            return list;
        }

        /// <summary>今週の番付（大きい順）。</summary>
        public static List<Entry> ThisWeek()
        {
            var d = D;
            RollWeek(d);
            var now = Ts.Now;
            return Board(WeekStart(now), now, d != null ? d.weekBest : 0, d != null ? d.weekFish : "");
        }

        public static int PlayerRank(DateTime now)
        {
            var list = ThisWeek();
            return list.FindIndex(e => e.player) + 1;
        }

        // ───────── 図鑑の絵（その場で描く） ─────────

        static readonly Dictionary<(string, bool), Texture2D> Icons = new Dictionary<(string, bool), Texture2D>();

        public static Texture2D Icon(FishKind f, bool known)
        {
            if (Icons.TryGetValue((f.id, known), out var t)) return t;
            const int W = 128, H = 64;
            t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            var shade = new Color(0.08f, 0.13f, 0.2f, 0.95f);
            float cx = 70, cy = 32;
            float rx = f.shape == 2 ? 50 : f.shape == 1 ? 32 : 42;
            float ry = f.shape == 3 ? 12 : Mathf.Clamp(rx * f.fat * 2f, 7, 28);
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    float x = i + 0.5f, y = j + 0.5f;
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    bool body, tail = false, fin = false;
                    Color c = Color.clear;
                    if (f.shape == 3)
                    {
                        // マンタ：ひし形の体と長い尾
                        body = Mathf.Abs(x - 74) / 40f + Mathf.Abs(y - cy) / 26f < 1f && x > 40;
                        tail = x > 6 && x <= 44 && Mathf.Abs(y - cy) < 1.6f;
                    }
                    else
                    {
                        body = dx * dx + dy * dy < 1f;
                        float tx0 = cx - rx + 4, tw = f.longTail ? 30 : 20;
                        if (x < tx0 && x > tx0 - tw)
                        {
                            float k = (tx0 - x) / tw;
                            tail = Mathf.Abs(y - cy) < (2 + k * (f.longTail ? 22 : 16));
                        }
                        fin = !body && x > cx - rx * 0.3f && x < cx + rx * 0.2f && y > cy + ry - 1 && y < cy + ry + 6 - (x - (cx - rx * 0.3f)) * 0.12f;
                    }
                    if (body || tail || fin)
                    {
                        if (!known) c = shade;
                        else
                        {
                            c = tail || fin ? Color.Lerp(f.c1, f.c2, 0.45f) : f.c1;
                            if (body)
                            {
                                if (f.pattern == 1 && Mathf.PerlinNoise(x * 0.09f + f.id.Length, y * 0.11f) > 0.58f) c = f.c2;
                                if (f.pattern == 2 && (x - (cx + rx * 0.45f)) * (x - (cx + rx * 0.45f)) + (y - cy - ry * 0.3f) * (y - cy - ry * 0.3f) < 70) c = f.c2;
                                if (f.pattern == 3 && Mathf.Abs(y - cy) < ry * 0.22f) c = Color.Lerp(c, f.c2, 0.8f);
                                c *= 0.82f + 0.3f * Mathf.Clamp01((y - cy) / ry + 0.4f); // 上を明るく
                                if (f.glow && Mathf.PerlinNoise(x * 0.3f, y * 0.3f) > 0.7f) c = Color.Lerp(c, Color.white, 0.6f);
                            }
                            c.a = 1;
                        }
                    }
                    // 目
                    if (f.shape != 3)
                    {
                        float ex = cx + rx * 0.62f, ey = cy + ry * 0.2f, er = f.bigEyes ? 5.5f : 3f;
                        if ((x - ex) * (x - ex) + (y - ey) * (y - ey) < er * er) c = known ? (f.bigEyes ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.05f, 0.05f, 0.08f)) : shade;
                        if (known && f.bigEyes && (x - ex - 1.5f) * (x - ex - 1.5f) + (y - ey) * (y - ey) < 6) c = new Color(0.05f, 0.05f, 0.08f);
                        if (f.whiskers && known && x > cx + rx - 4 && x < cx + rx + 12 && Mathf.Abs(y - (cy - ry * 0.3f) + (x - cx - rx) * 0.35f) < 0.8f) c = new Color(0.2f, 0.18f, 0.15f);
                    }
                    px[j * W + i] = c;
                }
            t.SetPixels(px);
            t.Apply();
            Icons[(f.id, known)] = t;
            return t;
        }
    }
}
