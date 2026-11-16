using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 町を歩く人たち（30人）とお店の人。起動のときに人形を作り、軽くするために
    /// 1人を「体・左腕・右腕」の3つの形にまとめ、色は小さな色見本の絵1枚（全員で1つのマテリアル）で塗る。
    /// 通り（StreetMap）の線の上を行ったり来たりして、ときどき立ち止まる。遠くの人は止めて描かない。
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
            public bool still, visible = true;
            public float stillYaw;
        }

        readonly List<Walker> _walkers = new List<Walker>();
        Texture2D _atlas;
        Material _mat;
        readonly Dictionary<Color32, int> _cells = new Dictionary<Color32, int>();
        readonly Color32[] _pix = new Color32[AtlasSize * AtlasSize];
        const int AtlasSize = 32;
        readonly RaycastHit[] _hits = new RaycastHit[6];
        System.Random _rng;
        float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        int Ri(int a, int b) => _rng.Next(a, b);

        // 通りの名前と人数（30人＝通り 28人＋店先で品物を見ている人 2人）
        static readonly (string street, int n)[] Plan =
        {
            ("月見商店街", 10), ("東西通り", 5), ("大門通り", 4), ("下町の環状通り", 5), ("中段の環状通り", 4),
        };

        void Start()
        {
            if (Ts.Flag("--no-crowd")) { enabled = false; return; } // 重さを比べる確認用

            _rng = new System.Random(seed);
            _atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "crowd_colors" };
            _mat = new Material(litTemplate) { name = "crowd" };
            _mat.SetTexture("_BaseMap", _atlas);
            _mat.SetColor("_BaseColor", Color.white);

            var map = StreetMap.I != null ? StreetMap.I : FindFirstObjectByType<StreetMap>();
            int made = 0;
            foreach (var (street, n) in Plan)
            {
                var path = map != null ? map.Path(street) : null;
                if (path == null || path.Count < 2) { Debug.LogWarning("crowd: no street " + street); continue; }
                float len = Length(path);
                for (int i = 0; i < n; i++)
                {
                    var w = Make("通る人" + made++, RandomLook(), R(0.8f, 0.95f));
                    w.path = path;
                    w.s = (i + R(0.1f, 0.9f)) / n * len;
                    w.dir = _rng.NextDouble() < 0.5 ? 1 : -1;
                    w.offset = R(-0.55f, 0.55f);
                    w.speed = R(1.0f, 1.6f);
                    w.nextStop = R(6f, 20f);
                    w.len = len;
                    var (p0, _, _) = At(path, w.s);
                    w.y = w.yT = Island.Height(p0.x, p0.y);
                    w.root.position = new Vector3(p0.x, w.y, p0.y);
                    _walkers.Add(w);
                }
            }
            // 店先で品物を見ている人と、お店の人
            int k = 0;
            foreach (var shop in ShopStand.All)
            {
                var keeper = Make("お店の人（" + shop.title + "）", KeeperLook(shop.id), 0.9f);
                keeper.still = true; keeper.stillYaw = shop.keeperYaw;
                Place(keeper, shop.keeper, shop.keeperYaw);
                _walkers.Add(keeper);
                if (k++ < 2)
                {
                    var side = Quaternion.Euler(0, shop.standYaw, 0) * Vector3.right * (k == 1 ? 1.6f : -1.6f);
                    var b = Make("品物を見ている人" + k, RandomLook(), 0.86f);
                    b.still = true; b.stillYaw = shop.standYaw + (k == 1 ? -15 : 15);
                    Place(b, shop.stand + side, b.stillYaw);
                    _walkers.Add(b);
                }
            }
            _atlas.SetPixels32(_pix);
            _atlas.Apply(false, true);
            Debug.Log($"[Crowd] walkers {_walkers.Count}, colors {_cells.Count}");
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

        static DollLook KeeperLook(string id)
        {
            var L = DollLook.Player;
            L.hair = new Color(0.12f, 0.1f, 0.1f);
            L.longHair = false; L.buns = true;
            switch (id)
            {
                case "gofuku": L.robe = new Color(0.32f, 0.2f, 0.42f); L.robe2 = new Color(0.22f, 0.14f, 0.3f); L.sleeve = L.robe; L.sash = L.cuff = new Color(0.95f, 0.8f, 0.4f); break;
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

        Walker Make(string name, DollLook look, float scale)
        {
            var parts = Doll.Build(name, look, TmpMat, transform);
            parts.root.transform.localScale = Vector3.one * scale;
            var anim = parts.root.GetComponent<DollAnimator>();
            var rs = new List<Renderer>();
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

        float Ground(float x, float fromY, float z, float fallback)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(x, fromY + 2.5f, z), Vector3.down, _hits, 9f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider is CharacterController) continue;
                if (_hits[i].point.y > best) best = _hits[i].point.y;
            }
            return best > float.NegativeInfinity ? best : fallback;
        }

        void Update()
        {
            var c = Camera.main;
            var eye = c != null ? c.transform.position : Vector3.zero;
            float dt = Time.deltaTime;
            foreach (var w in _walkers)
            {
                var p = w.root.position;
                bool vis = (p - eye).sqrMagnitude < drawDistance * drawDistance;
                if (vis != w.visible)
                {
                    w.visible = vis;
                    foreach (var r in w.rends) r.enabled = vis;
                    w.anim.enabled = vis;
                }
                if (w.still) continue;

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
                    w.rayT = vis ? 0.15f : 1f;
                    w.yT = Ground(at.x, w.y, at.y, Island.Height(at.x, at.y));
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
