using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 自分の人形の着せ替え。服は部品の色を塗りかえ、飾りは基本の形で作って頭・体・手につける。
    /// 食べ物は右手に持って口もとへ運ぶ。マテリアルは組み立てのときに渡されたひな形から作る。
    /// </summary>
    public class Wardrobe : MonoBehaviour
    {
        public DollAnimator doll;
        public Material litTemplate, glowTemplate;
        public OrbitCamera cam;

        public string Clothes { get; private set; } = Catalog.StartClothes;
        public readonly List<string> Wearing = new List<string>();
        public bool Eating => _eating != null;

        readonly Dictionary<string, GameObject> _acc = new Dictionary<string, GameObject>();
        readonly Dictionary<(Color, bool), Material> _mats = new Dictionary<(Color, bool), Material>();
        Transform _float;
        GameObject _food;
        Coroutine _eating;
        Color _hair = new Color(0.16f, 0.12f, 0.16f);
        bool _umbrella;
        Renderer _canopy;

        void Awake()
        {
            if (doll == null) doll = GetComponentInChildren<DollAnimator>();
            var h = Find(doll.head, "HairBack");
            if (h != null) _hair = h.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        Material Mat(Color c, bool glow = false)
        {
            if (_mats.TryGetValue((c, glow), out var m)) return m;
            m = new Material(glow && glowTemplate != null ? glowTemplate : litTemplate);
            if (glow && glowTemplate != null) m.SetColor("_Color", c);
            else { m.SetColor("_BaseColor", c); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null); }
            _mats[(c, glow)] = m;
            return m;
        }

        // ───────── 着る ─────────

        /// <summary>着るものを決めて見た目に出す（保存はしない）。</summary>
        public void Wear(string clothes, IEnumerable<string> wearing)
        {
            Clothes = Catalog.Get(clothes) != null ? clothes : Catalog.StartClothes;
            Wearing.Clear();
            if (wearing != null) foreach (var w in wearing) if (Catalog.Get(w)?.kind == ItemKind.Accessory) Wearing.Add(w);
            Show(Clothes, Wearing);
        }

        /// <summary>見た目だけ変える（お店の試着）。</summary>
        public void Show(string clothes, IList<string> wearing)
        {
            var it = Catalog.Get(clothes) ?? Catalog.Get(Catalog.StartClothes);
            foreach (var r in doll.body.GetComponentsInChildren<Renderer>(true))
            {
                if (_acc.ContainsValue(r.gameObject)) continue;
                switch (r.name)
                {
                    case "Robe": r.sharedMaterial = Mat(it.robe); break;
                    case "Torso": r.sharedMaterial = Mat(it.hasTop ? it.top : it.robe); break;
                    case "Hem": case "Collar": case "Arm": r.sharedMaterial = Mat(it.robe2); break;
                    case "Sleeve": r.sharedMaterial = Mat(it.sleeve); break;
                    case "Cuff": r.sharedMaterial = Mat(it.cuff, it.glowCuff); break;
                    case "Sash": r.sharedMaterial = Mat(it.sash); break;
                    case "Accent": r.sharedMaterial = Mat(it.accent); break;
                }
            }

            foreach (var g in _acc.Values) if (g) Destroy(g);
            _acc.Clear();
            _umbrella = false;
            bool odango = false;
            foreach (var id in wearing)
            {
                var g = BuildAcc(id);
                if (g != null) _acc[id] = g;
                if (id == "bangasa") _umbrella = true;
                if (id == "odango") odango = true;
            }
            var longHair = Find(doll.head, "Long");
            if (longHair) longHair.gameObject.SetActive(!odango);
            if (!Eating) doll.holdR = _umbrella ? 1 : 0;
            doll.holdREuler = _umbrella ? new Vector3(-62, 0, -28) : new Vector3(-120, 0, -25);
            if (cam != null) cam.RefreshBody();
        }

        // ───────── 飾り ─────────

        GameObject Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, bool glow = false, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Mat(c, glow);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Transform Holder(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static readonly Color Pink = new Color(1f, 0.72f, 0.82f), Red = new Color(0.85f, 0.12f, 0.18f), Gold = new Color(1f, 0.85f, 0.35f), White = new Color(0.97f, 0.97f, 0.97f), Black = new Color(0.08f, 0.07f, 0.08f);

        GameObject BuildAcc(string id)
        {
            var head = doll.head; var body = doll.body; var arm = doll.armR;
            Transform h;
            switch (id)
            {
                case "kanzashi":
                    h = Holder(head, id); h.localPosition = new Vector3(0.17f, 0.15f, -0.03f);
                    Part(h, PrimitiveType.Cube, new Vector3(-0.06f, -0.04f, 0), new Vector3(0.015f, 0.015f, 0.2f), Gold, false, new Vector3(30, 60, 0));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2;
                        Part(h, PrimitiveType.Sphere, new Vector3(0.012f, Mathf.Sin(a) * 0.04f, Mathf.Cos(a) * 0.04f), Vector3.one * 0.05f, Pink);
                    }
                    Part(h, PrimitiveType.Sphere, new Vector3(0.025f, 0, 0), Vector3.one * 0.03f, Gold);
                    return h.gameObject;
                case "nekomimi":
                    h = Holder(head, id);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, PrimitiveType.Cube, new Vector3(0.12f * s, 0.24f, -0.01f), new Vector3(0.11f, 0.11f, 0.04f), _hair, false, new Vector3(0, 0, 45 + 12 * s));
                        Part(h, PrimitiveType.Cube, new Vector3(0.12f * s, 0.235f, 0.008f), new Vector3(0.065f, 0.065f, 0.03f), Pink, false, new Vector3(0, 0, 45 + 12 * s));
                    }
                    return h.gameObject;
                case "ribbon":
                    h = Holder(head, id); h.localPosition = new Vector3(0, 0.12f, -0.22f);
                    foreach (float s in new[] { -1f, 1f })
                        Part(h, PrimitiveType.Cube, new Vector3(0.09f * s, 0, 0), new Vector3(0.15f, 0.11f, 0.05f), Red, false, new Vector3(0, 0, -20 * s));
                    Part(h, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.065f, Red);
                    Part(h, PrimitiveType.Cube, new Vector3(0.04f, -0.1f, 0), new Vector3(0.04f, 0.14f, 0.02f), Red, false, new Vector3(0, 0, 15));
                    Part(h, PrimitiveType.Cube, new Vector3(-0.04f, -0.1f, 0), new Vector3(0.04f, 0.14f, 0.02f), Red, false, new Vector3(0, 0, -15));
                    return h.gameObject;
                case "hoshikan":
                    h = Holder(head, id); h.localPosition = new Vector3(-0.17f, 0.17f, 0);
                    Part(h, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.06f, Gold, true);
                    for (int i = 0; i < 3; i++)
                        Part(h, PrimitiveType.Cube, Vector3.zero, new Vector3(0.02f, 0.17f, 0.02f), new Color(1f, 0.95f, 0.6f), true, new Vector3(0, 90, i * 60));
                    return h.gameObject;
                case "kitsune":
                    h = Holder(head, id); h.localPosition = new Vector3(0.17f, 0.06f, 0.04f); h.localEulerAngles = new Vector3(0, 70, -8);
                    Part(h, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.18f, 0.22f, 0.08f), White);
                    Part(h, PrimitiveType.Sphere, new Vector3(0, -0.06f, 0.03f), new Vector3(0.08f, 0.07f, 0.06f), White);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, PrimitiveType.Cube, new Vector3(0.05f * s, 0.11f, 0), new Vector3(0.06f, 0.06f, 0.03f), White, false, new Vector3(0, 0, 45));
                        Part(h, PrimitiveType.Cube, new Vector3(0.045f * s, 0.02f, 0.036f), new Vector3(0.05f, 0.012f, 0.01f), Red, false, new Vector3(0, 0, -20 * s));
                    }
                    Part(h, PrimitiveType.Sphere, new Vector3(0, -0.075f, 0.065f), Vector3.one * 0.018f, Black);
                    return h.gameObject;
                case "odango":
                    h = Holder(head, id);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, PrimitiveType.Sphere, new Vector3(0.15f * s, 0.24f, -0.03f), Vector3.one * 0.17f, _hair);
                        Part(h, PrimitiveType.Capsule, new Vector3(0.27f * s, -0.12f, -0.08f), new Vector3(0.12f, 0.26f, 0.12f), _hair, false, new Vector3(0, 0, -18 * s));
                        Part(h, PrimitiveType.Sphere, new Vector3(0.2f * s, 0.06f, -0.06f), Vector3.one * 0.05f, new Color(1f, 0.6f, 0.75f));
                    }
                    return h.gameObject;
                case "uchiwa":
                    h = Holder(arm, id); h.localPosition = new Vector3(0, -0.5f, 0.02f);
                    Part(h, PrimitiveType.Cylinder, new Vector3(0, -0.04f, 0.06f), new Vector3(0.018f, 0.07f, 0.018f), new Color(0.75f, 0.6f, 0.4f), false, new Vector3(60, 0, 0));
                    Part(h, PrimitiveType.Cylinder, new Vector3(0, -0.12f, 0.2f), new Vector3(0.24f, 0.006f, 0.24f), White, false, new Vector3(0, 0, 90));
                    Part(h, PrimitiveType.Sphere, new Vector3(0.008f, -0.12f, 0.21f), new Vector3(0.012f, 0.05f, 0.09f), new Color(1f, 0.42f, 0.18f));
                    return h.gameObject;
                case "bangasa":
                    h = Holder(body, id); h.localPosition = new Vector3(0.14f, 0, 0.12f);
                    var canopy = new GameObject("傘");
                    canopy.transform.SetParent(h, false);
                    canopy.transform.localPosition = new Vector3(0, 1.88f, 0);
                    canopy.transform.localScale = new Vector3(1.45f, 0.36f, 1.45f);
                    canopy.AddComponent<MeshFilter>().sharedMesh = Doll.Cone;
                    _canopy = canopy.AddComponent<MeshRenderer>();
                    _canopy.sharedMaterial = Mat(new Color(0.85f, 0.2f, 0.12f));
                    Part(h, PrimitiveType.Cylinder, new Vector3(0, 1.6f, 0), new Vector3(0.025f, 0.6f, 0.025f), new Color(0.35f, 0.22f, 0.12f));
                    Part(h, PrimitiveType.Sphere, new Vector3(0, 2.22f, 0), Vector3.one * 0.06f, Black);
                    return h.gameObject;
                case "kingyo":
                    h = Holder(transform, id);
                    _float = h;
                    var fish = Holder(h, "金魚");
                    var orange = new Color(1f, 0.45f, 0.15f);
                    Part(fish, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.1f, 0.09f, 0.17f), orange, true);
                    Part(fish, PrimitiveType.Cube, new Vector3(0, 0.02f, -0.12f), new Vector3(0.01f, 0.08f, 0.09f), orange, true, new Vector3(30, 0, 0));
                    Part(fish, PrimitiveType.Cube, new Vector3(0, -0.02f, -0.12f), new Vector3(0.01f, 0.08f, 0.09f), orange, true, new Vector3(-30, 0, 0));
                    foreach (float s in new[] { -1f, 1f })
                        Part(fish, PrimitiveType.Sphere, new Vector3(0.04f * s, 0.02f, 0.06f), Vector3.one * 0.022f, Black);
                    return h.gameObject;
                case "hagoromo":
                    h = Holder(body, id);
                    var silk = new Color(1f, 0.78f, 0.9f);
                    const int n = 9;
                    for (int i = 0; i < n; i++)
                    {
                        float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n;
                        var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 1.12f + Mathf.Sin(a0) * 0.42f, -0.24f);
                        var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 1.12f + Mathf.Sin(a1) * 0.42f, -0.24f);
                        var mid = (p0 + p1) / 2; var d = p1 - p0;
                        Part(h, PrimitiveType.Cube, mid, new Vector3(d.magnitude + 0.02f, 0.05f, 0.015f), silk, true, new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
                    }
                    foreach (float s in new[] { -1f, 1f })
                        Part(h, PrimitiveType.Cube, new Vector3(0.5f * s, 0.82f, -0.2f), new Vector3(0.05f, 0.6f, 0.015f), silk, true, new Vector3(0, 0, 6 * s));
                    return h.gameObject;
            }
            return null;
        }

        void Update()
        {
            // カメラが近いときは傘で前が見えなくなるので隠す
            if (_canopy != null && cam != null && !cam.firstPerson)
                _canopy.enabled = (cam.transform.position - _canopy.transform.position).sqrMagnitude > 3.2f * 3.2f;

            if (_float != null)
            {
                // 光る金魚：左肩のあたりをゆっくり泳ぐ
                float t = Time.time;
                _float.localPosition = new Vector3(-0.5f + Mathf.Sin(t * 0.7f) * 0.08f, 1.45f + Mathf.Sin(t * 1.6f) * 0.06f, Mathf.Cos(t * 0.7f) * 0.1f);
                _float.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.7f) * 30f, 0);
            }
        }

        static readonly Color Stick = new Color(0.85f, 0.75f, 0.55f), Tray = new Color(0.8f, 0.68f, 0.45f), Lacquer = new Color(0.55f, 0.12f, 0.1f), Glass = new Color(0.85f, 0.95f, 1f);

        /// <summary>食べ物の形を、手の上に（上向きで）作る。形の種類と色は Catalog から。</summary>
        void BuildFood(Transform hand, Item f)
        {
            // 食べるときは腕が前に上がるので、腕から見た +z がほぼ真上になる。その向きでそろえる
            var g = Holder(hand, "上向き");
            g.localEulerAngles = new Vector3(90, 0, 0);
            g.localPosition = new Vector3(0, -0.02f, 0);
            var S = PrimitiveType.Sphere; var Cy = PrimitiveType.Cylinder; var Cu = PrimitiveType.Cube; var Ca = PrimitiveType.Capsule;
            int n = Mathf.Max(1, f.count);
            switch (f.shape)
            {
                case FoodShape.Skewer:
                    Part(g, Cy, new Vector3(0, 0.15f, 0), new Vector3(0.012f, 0.15f, 0.012f), Stick);
                    for (int i = 0; i < n; i++) Part(g, S, new Vector3(0, 0.1f + i * 0.055f, 0), Vector3.one * 0.06f, i % 2 == 1 && f.id == "negima" ? new Color(0.9f, 0.95f, 0.8f) : f.c1);
                    break;
                case FoodShape.Bowl:
                    Part(g, Cy, new Vector3(0, 0.045f, 0), new Vector3(0.2f, 0.045f, 0.2f), f.shop == "oden" ? new Color(0.9f, 0.88f, 0.82f) : Lacquer);
                    Part(g, Cy, new Vector3(0, 0.092f, 0), new Vector3(0.18f, 0.004f, 0.18f), f.c1);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 2.1f;
                        Part(g, S, new Vector3(Mathf.Cos(a) * 0.04f, 0.1f, Mathf.Sin(a) * 0.04f), new Vector3(0.05f, 0.03f, 0.05f), i == 0 ? f.c2 : Color.Lerp(f.c2, Color.white, 0.4f));
                    }
                    if (f.shop == "ramen")
                        foreach (float x in new[] { -0.01f, 0.01f }) Part(g, Cy, new Vector3(x + 0.05f, 0.16f, -0.02f), new Vector3(0.008f, 0.1f, 0.008f), Stick, false, new Vector3(0, 0, -30));
                    break;
                case FoodShape.Cup:
                    Part(g, Cy, new Vector3(0, 0.08f, 0), new Vector3(0.065f, 0.08f, 0.065f), f.c1);
                    Part(g, Cy, new Vector3(0, 0.17f, 0), new Vector3(0.035f, 0.015f, 0.035f), f.c2);
                    break;
                case FoodShape.Tray:
                    Part(g, Cu, new Vector3(0, 0.015f, 0), new Vector3(0.17f, 0.02f, 0.11f), Tray);
                    for (int i = 0; i < n; i++)
                    {
                        var pos = new Vector3(-0.05f + (i % 3) * 0.05f, 0.045f, -0.025f + (i / 3) * 0.05f);
                        Part(g, S, pos, Vector3.one * 0.045f, f.c1);
                        Part(g, S, pos + new Vector3(0, 0.018f, 0), new Vector3(0.03f, 0.012f, 0.03f), f.c2);
                    }
                    break;
                case FoodShape.Sushi:
                    Part(g, Cu, new Vector3(0, 0.01f, 0), new Vector3(0.2f, 0.015f, 0.11f), Tray);
                    for (int i = 0; i < n; i++)
                    {
                        float x = n == 1 ? 0 : -0.045f + i * 0.09f / (n - 1);
                        Part(g, Ca, new Vector3(x, 0.035f, 0), new Vector3(0.04f, 0.035f, 0.04f), f.c2, false, new Vector3(0, 0, 90));
                        if (f.c1 != f.c2) Part(g, Cu, new Vector3(x, 0.057f, 0), new Vector3(0.085f, 0.014f, 0.045f), f.c1);
                    }
                    break;
                case FoodShape.Crepe:
                    var cone = new GameObject("クレープ");
                    cone.transform.SetParent(g, false);
                    cone.transform.localPosition = new Vector3(0, 0.2f, 0);
                    cone.transform.localEulerAngles = new Vector3(180, 0, 0);
                    cone.transform.localScale = new Vector3(0.17f, 0.2f, 0.17f);
                    cone.AddComponent<MeshFilter>().sharedMesh = Doll.Cone;
                    cone.AddComponent<MeshRenderer>().sharedMaterial = Mat(f.c1);
                    Part(g, S, new Vector3(0, 0.21f, 0), new Vector3(0.12f, 0.06f, 0.12f), White);
                    for (int i = 0; i < 3; i++) Part(g, S, new Vector3(-0.03f + i * 0.03f, 0.235f, 0.01f), Vector3.one * 0.03f, f.c2);
                    break;
                case FoodShape.Round:
                    if (f.id == "ringoame") Part(g, Cy, new Vector3(0, 0.07f, 0), new Vector3(0.012f, 0.07f, 0.012f), f.c2);
                    Part(g, S, new Vector3(0, f.id == "ringoame" ? 0.17f : 0.06f, 0), Vector3.one * 0.1f, f.c1);
                    if (f.id != "ringoame") Part(g, S, new Vector3(0, 0.1f, 0.03f), new Vector3(0.04f, 0.04f, 0.03f), f.c2);
                    break;
                case FoodShape.Fish:
                    Part(g, S, new Vector3(0, 0.1f, 0), new Vector3(0.05f, 0.17f, 0.1f), f.c1);
                    Part(g, Cu, new Vector3(0, 0.2f, 0), new Vector3(0.04f, 0.06f, 0.08f), f.c1, false, new Vector3(45, 0, 0));
                    break;
                case FoodShape.Parfait:
                    Part(g, Cy, new Vector3(0, 0.09f, 0), new Vector3(0.08f, 0.09f, 0.08f), Glass);
                    Part(g, Cy, new Vector3(0, 0.1f, 0), new Vector3(0.07f, 0.06f, 0.07f), f.c1);
                    Part(g, S, new Vector3(0, 0.2f, 0), Vector3.one * 0.075f, f.c1);
                    Part(g, S, new Vector3(0.03f, 0.235f, 0), Vector3.one * 0.035f, f.c2);
                    break;
                case FoodShape.StickBall:
                    Part(g, Cy, new Vector3(0, 0.06f, 0), new Vector3(0.012f, 0.06f, 0.012f), Stick);
                    Part(g, Ca, new Vector3(0, 0.19f, 0), new Vector3(0.05f, 0.08f, 0.05f), f.c1);
                    for (int i = 0; i < 4; i++) Part(g, S, new Vector3(0.024f * (i % 2 == 0 ? 1 : -1), 0.15f + i * 0.03f, 0.01f), Vector3.one * 0.015f, f.c2);
                    break;
                case FoodShape.Puff:
                    if (f.id == "wataame")
                    {
                        Part(g, Cy, new Vector3(0, 0.1f, 0), new Vector3(0.012f, 0.1f, 0.012f), f.c2);
                        Part(g, S, new Vector3(0, 0.27f, 0), new Vector3(0.22f, 0.2f, 0.22f), f.c1);
                    }
                    else
                    {
                        Part(g, Cy, new Vector3(0, 0.04f, 0), new Vector3(0.11f, 0.04f, 0.11f), f.c2);
                        Part(g, S, new Vector3(0, 0.12f, 0), Vector3.one * 0.14f, Color.Lerp(f.c1, Color.white, 0.25f));
                        Part(g, S, new Vector3(0, 0.15f, 0), new Vector3(0.11f, 0.07f, 0.11f), f.c1);
                    }
                    break;
                case FoodShape.Corn:
                    Part(g, Cy, new Vector3(0, 0.04f, 0), new Vector3(0.012f, 0.04f, 0.012f), Stick);
                    Part(g, Ca, new Vector3(0, 0.15f, 0), new Vector3(0.06f, 0.09f, 0.06f), f.c1);
                    Part(g, Ca, new Vector3(0.006f, 0.15f, 0.02f), new Vector3(0.045f, 0.08f, 0.03f), f.c2);
                    break;
                case FoodShape.Plate:
                    Part(g, Cy, new Vector3(0, 0.008f, 0), new Vector3(0.17f, 0.008f, 0.17f), f.id == "tsukimi" ? new Color(0.6f, 0.4f, 0.25f) : Tray);
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2;
                        var pos = new Vector3(Mathf.Cos(a) * 0.04f, 0.035f, Mathf.Sin(a) * 0.04f);
                        if (f.id == "makizushi")
                        {
                            Part(g, Cy, pos, new Vector3(0.05f, 0.02f, 0.05f), f.c1);
                            Part(g, Cy, pos + new Vector3(0, 0.002f, 0), new Vector3(0.04f, 0.02f, 0.04f), f.c2);
                        }
                        else if (f.id == "warabi") Part(g, Cu, pos, Vector3.one * 0.04f, f.c1, false, new Vector3(0, i * 25, 0));
                        else Part(g, S, pos, Vector3.one * 0.05f, f.c1);
                    }
                    if (f.id == "tsukimi") Part(g, S, new Vector3(0, 0.075f, 0), Vector3.one * 0.05f, f.c1);
                    break;
            }
        }

        // ───────── 食べる ─────────

        public void Eat(Item food)
        {
            if (_eating != null) StopCoroutine(_eating);
            if (_food) Destroy(_food);
            _eating = StartCoroutine(EatRoutine(food));
        }

        IEnumerator EatRoutine(Item food)
        {
            var arm = doll.armR;
            var h = Holder(arm, "食べ物");
            h.localPosition = new Vector3(0, -0.54f, 0.06f);
            _food = h.gameObject;
            BuildFood(h, food);
            if (cam != null) cam.RefreshBody();
            var keep = doll.holdREuler;
            doll.holdREuler = new Vector3(-120, 0, -25);
            for (float t = 0; t < 3.2f; t += Time.deltaTime)
            {
                doll.holdR = Mathf.Clamp01(t / 0.35f) * (t > 2.8f ? Mathf.Clamp01((3.2f - t) / 0.4f) : 1f);
                yield return null;
            }
            Destroy(_food);
            _food = null;
            doll.holdREuler = keep;
            doll.holdR = _umbrella ? 1 : 0;
            _eating = null;
            if (cam != null) cam.RefreshBody();
            Hud.Toast(food.say);
        }
    }
}
