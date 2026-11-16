using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 町の人たち：自分のまわりを歩く人（60人）・お店の人・店先と名所で立ち止まっている人。起動のときに人形を作り、軽くするために
    /// 1人を「体・左腕・右腕」の3つの形にまとめ、色は小さな色見本の絵1枚（全員で1つのマテリアル）で塗る。
    /// 歩く人は島じゅうの通り（StreetMap）から自分の近くに出し、離れたら近くへ移す。30m より遠い人は影と腕振りなし、85m より遠い人は描かない。
    /// </summary>
    public class Crowd : MonoBehaviour
    {
        public Material litTemplate;
        public int seed = 2024;
        public float drawDistance = 85f;

        class Walker
        {
            public Transform root;
            public DollAnimator anim;
            public Renderer[] rends;
            public List<Vector3> path; // x, z, 道の半分の幅
            public float s, dir = 1, offset, speed, wait, nextStop, y, yT, rayT, len;
            public bool still, visible = true, near = true;
            public int bad; // 続けて引き返した回数（はさまれたら別の通りへ移す）
            public float stillYaw;
            public string[] talk; // その人がよく言うこと
        }

        readonly List<Walker> _walkers = new List<Walker>();
        Texture2D _atlas;
        Material _mat;
        readonly Dictionary<Color32, int> _cells = new Dictionary<Color32, int>();
        readonly Color32[] _pix = new Color32[AtlasSize * AtlasSize];
        const int AtlasSize = 64;
        readonly RaycastHit[] _hits = new RaycastHit[6];
        readonly HashSet<Collider> _fences = new HashSet<Collider>();
        System.Random _rng;
        float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        int Ri(int a, int b) => _rng.Next(a, b);

        // 歩く人は自分のまわりに出す（島じゅうの通りから）。離れた人は自分の近くの通りへ移す
        public int walkers = 60;
        public float spawnNear = 35f, spawnFar = 75f, recycle = 88f, detail = 30f;
        readonly List<List<Vector3>> _streets = new List<List<Vector3>>();
        readonly List<float> _lens = new List<float>();
        float _totalLen;
        Transform _cam;

        void Start()
        {
            if (Ts.Flag("--no-crowd")) { enabled = false; return; } // 重さを比べる確認用

            _rng = new System.Random(seed);
            // 見えない柵（橋や回廊のふち）の上には立たせない
            foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                var r = c.GetComponent<Renderer>();
                if (r != null && !r.enabled) _fences.Add(c);
            }
            _atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "crowd_colors" };
            _mat = new Material(litTemplate) { name = "crowd" };
            _mat.SetTexture("_BaseMap", _atlas);
            _mat.SetColor("_BaseColor", Color.white);
            _cam = Camera.main != null ? Camera.main.transform : null;

            var map = StreetMap.I != null ? StreetMap.I : FindFirstObjectByType<StreetMap>();
            if (map != null)
                foreach (var path in map.AllPaths())
                {
                    float len = Length(path);
                    if (len < 8f) continue; // 短すぎるつなぎの道はのぞく
                    _streets.Add(path); _lens.Add(len); _totalLen += len;
                }

            // 歩く人（はじめは自分のまわり 5〜75m に）
            for (int i = 0; i < walkers && _streets.Count > 0; i++)
            {
                var w = Make("通る人" + i, RandomLook(), R(0.8f, 0.95f));
                w.speed = R(1.0f, 1.6f);
                w.offset = R(-0.55f, 0.55f);
                w.nextStop = R(6f, 20f);
                if (!Respawn(w, 5f, spawnFar, 80)) w.root.position = new Vector3(0, -50, 0);
                _walkers.Add(w);
            }

            // お店の人と、店先の人（どの店にも2人）
            foreach (var shop in ShopStand.All)
            {
                var keeper = Make("お店の人（" + shop.title + "）", KeeperLook(shop.kind), 0.9f, true);
                keeper.talk = shop.kind == ItemKind.Food ? KeeperFood : shop.kind == ItemKind.Clothes ? KeeperClothes : KeeperAcc;
                keeper.still = true; keeper.stillYaw = shop.keeperYaw;
                Place(keeper, shop.keeper, shop.keeperYaw);
                _walkers.Add(keeper);
                for (int k = 0; k < 2; k++)
                {
                    var rot = Quaternion.Euler(0, shop.standYaw, 0);
                    var p = shop.stand + rot * new Vector3(k == 0 ? 1.5f : -1.4f, 0, k == 0 ? 0.2f : -1.1f);
                    var b = Make("店先の人", RandomLook(), R(0.82f, 0.92f), true);
                    b.talk = shop.kind == ItemKind.Food ? FrontFood : FrontWear;
                    b.still = true; b.stillYaw = shop.standYaw + R(-25f, 25f);
                    Stand(b, p, b.stillYaw);
                    _walkers.Add(b);
                }
            }

            // 名所で立ち止まっている人（島じゅう）
            foreach (var z in Zone.All)
            {
                if (z.id == "corridor") continue;
                int n = z.id == "plaza" ? 6 : z.id == "stage" ? 2 : 3;
                for (int k = 0; k < n; k++)
                {
                    float a = R(0f, Mathf.PI * 2), r = R(3f, 7f);
                    var p = z.warpPos + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    // 広場は大画面のほうを向く。ほかは、なんとなく名所のほうを見る
                    float yaw = z.id == "plaza" ? z.warpYaw + R(-20f, 20f) : z.warpYaw + R(-70f, 70f);
                    var b = Make("名所の人", RandomLook(), R(0.8f, 0.94f), true);
                    b.talk = ZoneLines(z.id);
                    b.still = true; b.stillYaw = yaw;
                    Stand(b, p, yaw);
                    _walkers.Add(b);
                }
            }
            _atlas.SetPixels32(_pix);
            _atlas.Apply(false, true);
            Debug.Log($"[Crowd] people {_walkers.Count}, streets {_streets.Count}, colors {_cells.Count}");
        }

        /// <summary>立ち止まる人：地面（橋や石段の上も）に立たせる。水の中なら出さない。</summary>
        void Stand(Walker w, Vector3 p, float yaw)
        {
            // 水の中・石垣の上や下（段ちがい）には立たせない。少しずらして8回までさがす
            float y = 0; bool ok = false;
            var p0 = p;
            for (int t = 0; t < 8 && !ok; t++)
            {
                if (t > 0) p = p0 + new Vector3(R(-3f, 3f), 0, R(-3f, 3f));
                y = Ground(p.x, p.y + 1.5f, p.z, Island.Height(p.x, p.z));
                ok = y >= 0.3f && Mathf.Abs(y - p.y) < 1.2f && !Wet(p.x, p.z, y) && Island.NearestWater(p.x, p.z) > 0.6f;
            }
            if (!ok) { w.root.gameObject.SetActive(false); return; }
            w.y = w.yT = y;
            w.root.SetPositionAndRotation(new Vector3(p.x, y, p.z), Quaternion.Euler(0, yaw, 0));
        }

        /// <summary>自分から near〜far の距離にある通りの上に出しなおす。</summary>
        bool Respawn(Walker w, float near, float far, int tries)
        {
            var me = _cam != null ? _cam.position : Vector3.zero;
            for (int t = 0; t < tries; t++)
            {
                // 長い通りほど選ばれやすく
                float pick = R(0f, _totalLen);
                int si = 0;
                while (si < _lens.Count - 1 && pick > _lens[si]) { pick -= _lens[si]; si++; }
                var path = _streets[si];
                var (pos, d, half) = At(path, pick);
                var at = pos + new Vector2(-d.y, d.x) * (w.offset * half);
                float dist = Vector2.Distance(at, new Vector2(me.x, me.z));
                if (dist < near || dist > far) continue;
                float h = Island.Height(at.x, at.y);
                float gy = Ground(at.x, h + 1f, at.y, h);
                if (gy < 0.3f || Wet(at.x, at.y, gy) || NoWalkArea.Inside(at.x, at.y)) continue;
                w.path = path; w.len = _lens[si]; w.s = pick;
                w.dir = _rng.NextDouble() < 0.5 ? 1 : -1;
                w.y = w.yT = gy;
                w.rayT = 0; w.wait = 0;
                w.root.position = new Vector3(at.x, w.y, at.y);
                float yaw = Mathf.Atan2(d.x * w.dir, d.y * w.dir) * Mathf.Rad2Deg;
                w.root.rotation = Quaternion.Euler(0, yaw, 0);
                return true;
            }
            return false;
        }

        void Place(Walker w, Vector3 p, float yaw)
        {
            w.y = w.yT = p.y; // 組み立てのときに地面の高さを入れてある
            w.root.SetPositionAndRotation(new Vector3(p.x, w.y, p.z), Quaternion.Euler(0, yaw, 0));
        }

        static float Length(List<Vector3> p)
        {
            float l = 0;
            for (int i = 1; i < p.Count; i++) l += Vector2.Distance(new Vector2(p[i - 1].x, p[i - 1].y), new Vector2(p[i].x, p[i].y));
            return l;
        }

        /// <summary>線の上の s の場所（x,z）と向きと道の半分の幅。</summary>
        static (Vector2 pos, Vector2 dir, float half) At(List<Vector3> p, float s)
        {
            for (int i = 1; i < p.Count; i++)
            {
                var a = new Vector2(p[i - 1].x, p[i - 1].y); var b = new Vector2(p[i].x, p[i].y);
                float l = Vector2.Distance(a, b);
                if (s <= l || i == p.Count - 1)
                {
                    var d = l > 0.001f ? (b - a) / l : Vector2.up;
                    return (a + d * Mathf.Clamp(s, 0, l), d, p[i].z);
                }
                s -= l;
            }
            return (new Vector2(p[0].x, p[0].y), Vector2.up, p[0].z);
        }

        // ───────── 見た目 ─────────

        static readonly Color[] Hairs =
        {
            new Color(0.1f, 0.08f, 0.09f), new Color(0.22f, 0.14f, 0.1f), new Color(0.35f, 0.22f, 0.14f), new Color(0.55f, 0.4f, 0.3f),
            new Color(0.8f, 0.8f, 0.86f), new Color(0.95f, 0.7f, 0.8f), new Color(0.4f, 0.5f, 0.85f), new Color(0.12f, 0.1f, 0.2f),
        };

        DollLook RandomLook()
        {
            Color Hsv(float s0, float s1, float v0, float v1) => Color.HSVToRGB(R(0f, 1f), R(s0, s1), R(v0, v1));
            var robe = _rng.NextDouble() < 0.3 ? Hsv(0.1f, 0.3f, 0.85f, 1f) : Hsv(0.3f, 0.75f, 0.3f, 0.9f);
            var k = Hsv(0.4f, 0.85f, 0.55f, 1f);
            var L = new DollLook
            {
                skin = Color.Lerp(new Color(1f, 0.9f, 0.84f), new Color(0.85f, 0.66f, 0.52f), R(0f, 0.6f)),
                hair = Hairs[Ri(0, Hairs.Length)],
                robe = robe, robe2 = Color.Lerp(robe, Color.black, 0.25f), sleeve = Color.Lerp(robe, Color.white, 0.15f),
                cuff = k, sash = k, eye = new Color(0.2f, 0.14f, 0.12f), accent = Hsv(0.3f, 0.6f, 0.8f, 1f),
            };
            int style = Ri(0, 5);
            L.longHair = style == 1 || style == 4; L.ponytail = style == 2; L.buns = style == 3; L.twinTails = style == 4 && _rng.NextDouble() < 0.5;
            return L;
        }

        static DollLook KeeperLook(ItemKind kind)
        {
            var L = DollLook.Player;
            L.hair = new Color(0.12f, 0.1f, 0.1f);
            L.longHair = false; L.buns = true;
            switch (kind == ItemKind.Clothes ? "gofuku" : kind == ItemKind.Accessory ? "komono" : "")
            {
                case "gofuku":
 L.robe = new Color(0.32f, 0.2f, 0.42f); L.robe2 = new Color(0.22f, 0.14f, 0.3f); L.sleeve = L.robe; L.sash = L.cuff = new Color(0.95f, 0.8f, 0.4f); break;
                case "komono": L.robe = new Color(0.2f, 0.45f, 0.5f); L.robe2 = new Color(0.14f, 0.3f, 0.34f); L.sleeve = L.robe; L.sash = L.cuff = new Color(1f, 0.6f, 0.7f); L.buns = false; L.ponytail = true; break;
                default: L.robe = new Color(0.95f, 0.92f, 0.85f); L.robe2 = new Color(0.8f, 0.3f, 0.3f); L.sleeve = L.robe; L.sash = L.cuff = new Color(0.8f, 0.3f, 0.3f); L.hair = new Color(0.8f, 0.8f, 0.85f); break;
            }
            return L;
        }

        Vector2 Cell(Color c)
        {
            Color32 k = c;
            k.a = 255;
            if (!_cells.TryGetValue(k, out int i))
            {
                i = Mathf.Min(_cells.Count, AtlasSize * AtlasSize - 1);
                _cells[k] = i;
                _pix[i] = k;
            }
            return new Vector2((i % AtlasSize + 0.5f) / AtlasSize, (i / AtlasSize + 0.5f) / AtlasSize);
        }

        readonly Dictionary<Color, Material> _tmp = new Dictionary<Color, Material>();
        Material TmpMat(Color c)
        {
            if (!_tmp.TryGetValue(c, out var m)) { m = new Material(litTemplate); m.SetColor("_BaseColor", c); _tmp[c] = m; }
            return m;
        }

        Walker Make(string name, DollLook look, float scale, bool still = false)
        {
            var parts = Doll.Build(name, look, TmpMat, transform);
            parts.root.transform.localScale = Vector3.one * scale;
            var anim = parts.root.GetComponent<DollAnimator>();
            var rs = new List<Renderer>();
            if (still)
            {
                // 立ち止まっている人は、腕もいっしょに1つの形にして動かさない（軽くするため）
                rs.Add(Bake(parts.body, null, null));
                anim.enabled = false;
                anim.head = null; anim.armL = anim.armR = null;
                return new Walker { root = parts.root.transform, anim = anim, rends = rs.ToArray(), still = true };
            }
            rs.Add(Bake(parts.body, parts.armL, parts.armR));
            rs.Add(Bake(parts.armL, null, null));
            rs.Add(Bake(parts.armR, null, null));
            anim.head = null; // 頭は体といっしょの形にした
            return new Walker { root = parts.root.transform, anim = anim, rends = rs.ToArray() };
        }

        /// <summary>g の下の形を1つにまとめる（skipA・skipB の下はのぞく）。色は色見本の絵の場所で表す。</summary>
        Renderer Bake(Transform g, Transform skipA, Transform skipB)
        {
            var list = new List<CombineInstance>();
            var temps = new List<Mesh>();
            var kill = new List<GameObject>();
            var toLocal = g.worldToLocalMatrix;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.transform == g) continue;
                if ((skipA && mf.transform.IsChildOf(skipA)) || (skipB && mf.transform.IsChildOf(skipB))) continue;
                var r = mf.GetComponent<Renderer>();
                var col = r.sharedMaterial.GetColor("_BaseColor");
                var m = Instantiate(mf.sharedMesh);
                var uv = new Vector2[m.vertexCount];
                var cell = Cell(col);
                for (int i = 0; i < uv.Length; i++) uv[i] = cell;
                m.uv = uv;
                temps.Add(m);
                list.Add(new CombineInstance { mesh = m, transform = toLocal * mf.transform.localToWorldMatrix });
                kill.Add(mf.gameObject);
                mf.gameObject.SetActive(false); // すぐに当たり判定からも消す
            }
            var mesh = new Mesh { name = g.name + "_baked" };
            mesh.CombineMeshes(list.ToArray(), true, true);
            foreach (var t in temps) Destroy(t);
            foreach (var k in kill) Destroy(k);
            g.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var rend = g.gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _mat;
            return rend;
        }

        // ───────── 動き ─────────

        /// <summary>そこが水の上か（橋の上ならちがう）。</summary>
        static bool Wet(float x, float z, float groundY)
        {
            float ws = Island.WaterSurface(x, z);
            return !float.IsNaN(ws) && groundY < ws + 0.25f;
        }

        float Ground(float x, float fromY, float z, float fallback)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(x, fromY + 2.5f, z), Vector3.down, _hits, 9f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider is CharacterController || _fences.Contains(_hits[i].collider)) continue;
                if (_hits[i].point.y > best) best = _hits[i].point.y;
            }
            return best > float.NegativeInfinity ? best : fallback;
        }

        // ───────── 吹き出し ─────────

        /// <summary>近くの人の吹き出し（メニューでオン・オフ。PlayerPrefs に残す）。</summary>
        public static bool BubblesOn
        {
            get => PlayerPrefs.GetInt("tsukuyomi_bubbles", 1) == 1;
            set { PlayerPrefs.SetInt("tsukuyomi_bubbles", value ? 1 : 0); PlayerPrefs.Save(); if (!value) Bubbles.Clear(); }
        }

        public class Bubble { public Transform who; public float height, t; public string text; }
        public static readonly List<Bubble> Bubbles = new List<Bubble>();
        float _talkT = 1f;
        readonly List<Walker> _near = new List<Walker>();


        static readonly string[] Common =
        {
            "ライブよかった〜！", "ヤチヨの新曲、聴いた？", "次のライブいつだろう", "今日も月がきれいだね", "おなかすいた〜",
            "あの屋台、おいしかったよ", "千年桜、見に行こうよ", "釣り堀で大物がつれたんだって", "ふじゅ〜ためなきゃ…",
            "この服、新しく買ったの！", "五重塔まで登ってみる？", "空のお魚、かわいい", "迷子になっちゃった…", "ツクヨミ、たのしいね",
        };
        static readonly string[] Night = { "夜店がきれい〜", "提灯がいっぱい！", "夜のツクヨミ、好きだな" };
        static readonly string[] Fireworks = { "花火きれい！", "たまや〜！", "花火、もっと近くで見よう" };
        static readonly string[] Morning = { "おはよう〜", "朝のツクヨミ、しずかだね" };
        static readonly string[] KeeperFood = { "おいしいよ〜！", "焼きたてだよ！", "あったかいよ〜", "いらっしゃい！", "ひとつどう？" };
        static readonly string[] KeeperClothes = { "きっと似合いますよ", "新しい柄、入りました", "試着だけでもどうぞ" };
        static readonly string[] KeeperAcc = { "かんざし、いかが？", "光る金魚が人気だよ", "つけてみるだけでもどうぞ" };
        static readonly string[] FrontFood = { "おいしそう…", "どれにしようかな", "これ、おいしいよ！", "いいにおい〜" };
        static readonly string[] FrontWear = { "この色かわいい", "どっちが似合うかな", "ふじゅ〜足りるかな…" };

        static string[] ZoneLines(string id) => id switch
        {
            "plaza" => new[] { "ヤチヨだ！", "大画面きれい〜", "ライブ、ここで見ようかな" },
            "sakura" => new[] { "桜きれい…", "千年も咲いてるんだって", "花びらがふってくる" },
            "fishing" => new[] { "つれるかな", "大物ねらうぞ〜", "茶屋でひと休みしよう", "鹿威しの音、落ち着くね", "今週の番付、だれが一位かな", "蓮の花がきれい" },
            "onsen" => new[] { "いいお湯〜", "湯けむりがきれい", "ぽかぽかする〜", "温泉のあとは甘味かな" },
            "lookout" => new[] { "いい眺め！", "海がひろ〜い", "ちょっとこわい…" },
            "palace" => new[] { "月の御殿だ…", "大きいね〜", "ここ、てっぺんだよ" },
            "torii" => new[] { "大鳥居、でっかい", "ここから入ってきたの", "海がきらきら" },
            "stage" => new[] { "ここでライブやるんだって", "ライブ楽しみ！", "舞台がひかってる" },
            "upper" => new[] { "五重塔、たかい！", "お参りしていこう", "鐘の音がきこえる" },
            "bamboo" => new[] { "竹の音がいいね", "すずしい〜", "竹林、しずか…" },
            "shitamachi" => new[] { "屋形船、乗りたい", "水路がきれい", "風鈴の音がする" },
            "shopping" => new[] { "なに買おうかな", "商店街、にぎやか！", "食べ歩きしよう" },
            _ => Common,
        };

        string PickLine(Walker w)
        {
            var now = Ts.Now; int h = now.Hour;
            double r = _rng.NextDouble();
            if (w.talk != null && r < 0.75) return w.talk[_rng.Next(w.talk.Length)];
            if (h >= 19 && h < 23 && r < 0.85) return Fireworks[_rng.Next(Fireworks.Length)];
            if ((h >= 18 || h < 4) && r < 0.9) return Night[_rng.Next(Night.Length)];
            if (h >= 5 && h < 10 && r < 0.9) return Morning[_rng.Next(Morning.Length)];
            return Common[_rng.Next(Common.Length)];
        }

        /// <summary>近くの人のだれかに、ときどき吹き出しを出す（同時に4つまで）。</summary>
        void TalkTick(Vector3 eye)
        {
            for (int i = Bubbles.Count - 1; i >= 0; i--)
            {
                Bubbles[i].t -= Time.deltaTime;
                if (Bubbles[i].t <= 0 || Bubbles[i].who == null) Bubbles.RemoveAt(i);
            }
            if (!BubblesOn) return;
            _talkT -= Time.deltaTime;
            if (_talkT > 0 || Bubbles.Count >= 4) return;
            _talkT = R(1.2f, 3f);
            // 近く（20m 以内）の人の中から1人えらぶ
            _near.Clear();
            foreach (var w in _walkers)
                if (w.root.gameObject.activeSelf && (w.root.position - eye).sqrMagnitude < 20f * 20f && !Bubbles.Exists(b => b.who == w.root)) _near.Add(w);
            if (_near.Count == 0) return;
            var who = _near[_rng.Next(_near.Count)];
            Bubbles.Add(new Bubble { who = who.root, height = 1.75f * who.root.localScale.y + 0.25f, t = R(3f, 4.5f), text = PickLine(who) });
        }

        void Update()
        {
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            var eye = _cam != null ? _cam.position : Vector3.zero;
            float dt = Time.deltaTime;
            int respawned = 0;
            TalkTick(eye);
            foreach (var w in _walkers)
            {
                if (!w.root.gameObject.activeSelf) continue;
                var p = w.root.position;
                float d2 = (p - eye).sqrMagnitude;

                // 遠くへ離れた歩く人は、自分の近くの通りへ（1フレームに2人まで）
                if (!w.still && d2 > recycle * recycle && respawned < 2)
                {
                    respawned++;
                    if (Respawn(w, spawnNear, spawnFar, 12)) { p = w.root.position; d2 = (p - eye).sqrMagnitude; }
                }

                bool vis = d2 < drawDistance * drawDistance;
                bool near = d2 < detail * detail;
                if (vis != w.visible)
                {
                    w.visible = vis;
                    foreach (var r in w.rends) r.enabled = vis;
                }
                // 遠くの人は影なし・腕振りなし
                if (near != w.near)
                {
                    w.near = near;
                    var mode = near ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    foreach (var r in w.rends) r.shadowCastingMode = mode;
                }
                if (!w.still) w.anim.enabled = vis && near;
                if (w.still || w.path == null) continue;

                float len = w.len;
                if (w.wait > 0)
                {
                    w.wait -= dt;
                    w.anim.speed = 0;
                    if (vis) w.root.rotation = Quaternion.Slerp(w.root.rotation, Quaternion.Euler(0, w.stillYaw, 0), 1 - Mathf.Exp(-3f * dt));
                    continue;
                }
                w.s += w.dir * w.speed * dt;
                if (w.s < 0 || w.s > len) { w.dir = -w.dir; w.s = Mathf.Clamp(w.s, 0, len); }
                w.nextStop -= dt;
                var (pos, d, half) = At(w.path, w.s);
                var move = d * w.dir;
                var side = new Vector2(-d.y, d.x);
                var at = pos + side * (w.offset * half);
                if (w.nextStop < 0)
                {
                    // 立ち止まって、わきの店のほうを見る
                    w.nextStop = R(10f, 25f);
                    w.wait = R(2f, 5f);
                    var look = side * (w.offset >= 0 ? 1 : -1);
                    w.stillYaw = Mathf.Atan2(look.x, look.y) * Mathf.Rad2Deg;
                }
                w.rayT -= dt;
                if (w.rayT <= 0)
                {
                    w.rayT = near ? 0.15f : vis ? 0.4f : 1f;
                    float gy = Ground(at.x, w.y, at.y, Island.Height(at.x, at.y));
                    // 水に入りそう（橋のわきなど）なら道のまんなかへ寄る。まんなかもだめ・石垣のような段ちがいなら引き返す
                    bool bad = Wet(at.x, at.y, gy) || Mathf.Abs(gy - w.y) > 0.9f || NoWalkArea.Inside(at.x, at.y);
                    if (bad)
                    {
                        float cy = Ground(pos.x, w.y, pos.y, Island.Height(pos.x, pos.y));
                        if (!Wet(pos.x, pos.y, cy) && Mathf.Abs(cy - w.y) <= 0.9f && !NoWalkArea.Inside(pos.x, pos.y)) { w.offset *= 0.2f; at = pos; gy = cy; }
                        else if (++w.bad > 3)
                        {
                            // 両がわとも行けない（はさまった）：別の通りへ移す
                            w.bad = 0;
                            if (!Respawn(w, spawnNear, spawnFar, 12)) { w.root.position = new Vector3(0, -50, 0); w.path = null; }
                            continue;
                        }
                        else
                        {
                            w.dir = -w.dir;
                            w.s = Mathf.Clamp(w.s + w.dir * 1.2f, 0, len);
                            w.rayT = 0;
                            continue;
                        }
                    }
                    w.yT = gy;
                    if (!bad) w.bad = 0;
                }
                w.y = Mathf.Lerp(w.y, w.yT, 1 - Mathf.Exp(-10f * dt));
                w.anim.speed = w.speed;
                w.root.position = new Vector3(at.x, w.y, at.y);
                if (vis)
                {
                    float yaw = Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg;
                    w.root.rotation = Quaternion.Slerp(w.root.rotation, Quaternion.Euler(0, yaw, 0), 1 - Mathf.Exp(-6f * dt));
                }
            }
        }
    }
}
