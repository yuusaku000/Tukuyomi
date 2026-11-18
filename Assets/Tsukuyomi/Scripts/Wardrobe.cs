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
        int _hairStyle = 1;
        bool _odango;
        GameObject _hairObj;
        /// <summary>きぐるみのフードをかぶっている（髪の一部を隠す）。</summary>
        bool _hood;
        Material _handMat;

        /// <summary>理髪店の髪型：0 ショート・1 ロング・2 ポニーテール・3 おだんご・4 ツインテール。</summary>
        public static readonly string[] HairStyles = { "ショート", "ロング", "ポニーテール", "おだんご", "ツインテール" };
        public Color HairColor => _hair;
        public int HairStyle => _hairStyle;

        /// <summary>髪の色と形を変える（保存はしない）。</summary>
        public void SetHair(Color c, int style)
        {
            _hair = c; _hairStyle = Mathf.Clamp(style, 0, HairStyles.Length - 1);
            var m = Mat(c);
            foreach (var n in new[] { "HairBack", "Bangs", "SideL", "SideR", "Long" })
            {
                var t = Find(doll.head, n);
                if (t != null) t.GetComponent<Renderer>().sharedMaterial = m;
            }
            var longHair = Find(doll.head, "Long");
            if (longHair) longHair.gameObject.SetActive(!_odango && !_hood && _hairStyle == 1);
            if (_hairObj != null) Destroy(_hairObj);
            _hairObj = null;
            if (_hairStyle >= 2)
            {
                var h = Holder(doll.head, "髪型"); _hairObj = h.gameObject;
                if (_hairStyle == 2) Part(h, PrimitiveType.Capsule, new Vector3(0, -0.12f, -0.24f), new Vector3(0.13f, 0.28f, 0.13f), c, false, new Vector3(-20, 0, 0));
                else if (_hairStyle == 3) foreach (float s in new[] { -1f, 1f }) Part(h, PrimitiveType.Sphere, new Vector3(0.15f * s, 0.24f, -0.03f), Vector3.one * 0.17f, c);
                else foreach (float s in new[] { -1f, 1f }) Part(h, PrimitiveType.Capsule, new Vector3(0.27f * s, -0.12f, -0.08f), new Vector3(0.12f, 0.26f, 0.12f), c, false, new Vector3(0, 0, -18 * s));
                _hairObj.SetActive(!_odango && !_hood);
            }
            if (cam != null) cam.RefreshBody();
        }
        bool _umbrella;
        Renderer _canopy;

        void Awake()
        {
            if (doll == null) doll = GetComponentInChildren<DollAnimator>();
            var h = Find(doll.head, "HairBack");
            if (h != null) _hair = h.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            var hand = Find(doll.body, "Hand");
            if (hand != null) _handMat = hand.GetComponent<Renderer>().sharedMaterial;
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
                    case "Hand": if (it.style == 4) r.sharedMaterial = Mat(it.cuff); else if (_handMat != null) r.sharedMaterial = _handMat; break;
                }
            }

            ApplyStyle(it);

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
            if (longHair) longHair.gameObject.SetActive(!odango && !_hood && _hairStyle == 1);
            _odango = odango;
            if (_hairObj != null) _hairObj.SetActive(!odango && !_hood);
            if (!Eating) doll.holdR = _umbrella ? 1 : 0;
            doll.holdREuler = _umbrella ? new Vector3(-62, 0, -28) : new Vector3(-120, 0, -25);
            if (cam != null) cam.RefreshBody();
        }

        // ───────── 服の形（巫女装束・ドレス・十二単ふう）─────────

        readonly List<GameObject> _styleObjs = new List<GameObject>();
        readonly Dictionary<Transform, Vector3> _baseScale = new Dictionary<Transform, Vector3>();

        void Scale(Transform t, Vector3 k)
        {
            if (t == null) return;
            if (!_baseScale.TryGetValue(t, out var b)) { b = t.localScale; _baseScale[t] = b; }
            t.localScale = Vector3.Scale(b, k);
        }

        void ApplyStyle(Item it)
        {
            foreach (var g in _styleObjs) if (g) Destroy(g);
            _styleObjs.Clear();
            var robe = Find(doll.body, "Robe");
            var sleeves = new List<Transform>();
            foreach (var t in doll.body.GetComponentsInChildren<Transform>(true)) if (t.name == "Sleeve") sleeves.Add(t);
            // いったん元の大きさにもどす
            Scale(robe, Vector3.one);
            foreach (var sl in sleeves) Scale(sl, Vector3.one);
            _hood = it.style == 4;
            if (it.style == 0) return;

            var body = Holder(doll.body, "服の形"); _styleObjs.Add(body.gameObject);
            Transform ArmHolder(Transform arm) { var h = Holder(arm, "服の形の袖"); _styleObjs.Add(h.gameObject); return h; }

            switch (it.style)
            {
                case 4: // 狐のきぐるみ：細めの胴（つなぎ）・クリーム色のお腹・こげ茶のブーツ・耳つきフード・ふさふさのしっぽ
                    Scale(robe, new Vector3(0.66f, 1f, 0.66f));
                    foreach (var sl in sleeves) Scale(sl, new Vector3(0.85f, 1f, 0.8f));
                    Part(body, PrimitiveType.Sphere, new Vector3(0, 0.95f, 0.1f), new Vector3(0.3f, 0.44f, 0.18f), it.robe2);
                    Part(body, PrimitiveType.Sphere, new Vector3(0, 0.5f, 0.12f), new Vector3(0.26f, 0.42f, 0.14f), it.robe2);
                    foreach (float sx in new[] { -1f, 1f })
                        Part(body, PrimitiveType.Capsule, new Vector3(sx * 0.11f, 0.07f, 0.04f), new Vector3(0.17f, 0.09f, 0.24f), it.cuff, false, new Vector3(90, 0, 0));
                    var tail = Holder(body, "しっぽ"); _styleObjs.Add(tail.gameObject);
                    tail.localPosition = new Vector3(0, 0.62f, -0.26f); tail.localEulerAngles = new Vector3(-50, 0, 0);
                    Part(tail, PrimitiveType.Capsule, new Vector3(0, 0.22f, 0), new Vector3(0.2f, 0.3f, 0.2f), it.robe);
                    Part(tail, PrimitiveType.Sphere, new Vector3(0, 0.5f, 0), new Vector3(0.17f, 0.17f, 0.17f), it.robe2);
                    var hood = Holder(doll.head, "フード"); _styleObjs.Add(hood.gameObject);
                    Part(hood, PrimitiveType.Sphere, new Vector3(0, 0.05f, -0.075f), Vector3.one * 0.47f, it.robe);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Part(hood, PrimitiveType.Cube, new Vector3(sx * 0.13f, 0.25f, -0.07f), new Vector3(0.15f, 0.15f, 0.06f), it.robe, false, new Vector3(0, 0, 45 + sx * 10));
                        Part(hood, PrimitiveType.Cube, new Vector3(sx * 0.13f, 0.245f, -0.04f), new Vector3(0.08f, 0.08f, 0.03f), it.robe2, false, new Vector3(0, 0, 45 + sx * 10));
                        Part(hood, PrimitiveType.Sphere, new Vector3(sx * 0.16f, -0.12f, 0.08f), new Vector3(0.12f, 0.11f, 0.1f), it.robe2);
                    }
                    break;
                case 1: // 巫女装束：袴のひだ・胸の紐・大きな白い袖・髪の水引
                    for (int i = -2; i <= 2; i++)
                    {
                        float a = i * 0.32f;
                        var p = new Vector3(Mathf.Sin(a) * 0.36f, 0.42f, Mathf.Cos(a) * 0.36f);
                        Part(body, PrimitiveType.Cube, p, new Vector3(0.012f, 0.8f, 0.02f), it.robe2, false, new Vector3(-15f, a * Mathf.Rad2Deg, 0));
                    }
                    Part(body, PrimitiveType.Cube, new Vector3(0, 0.84f, 0.19f), new Vector3(0.4f, 0.05f, 0.04f), it.robe2);
                    foreach (float sx in new[] { -1f, 1f })
                        Part(body, PrimitiveType.Cube, new Vector3(sx * 0.05f, 0.74f, 0.21f), new Vector3(0.03f, 0.2f, 0.02f), it.cuff, it.glowCuff, new Vector3(0, 0, sx * 12f));
                    foreach (var sl in sleeves) Scale(sl, new Vector3(1.05f, 1.35f, 1.4f));
                    var mizu = Holder(doll.head, "水引"); _styleObjs.Add(mizu.gameObject);
                    mizu.localPosition = new Vector3(0, -0.12f, -0.22f);
                    Part(mizu, PrimitiveType.Cube, Vector3.zero, new Vector3(0.14f, 0.05f, 0.04f), it.cuff, it.glowCuff);
                    Part(mizu, PrimitiveType.Cube, new Vector3(0, -0.06f, 0), new Vector3(0.06f, 0.12f, 0.03f), White);
                    break;
                case 2: // ドレス：裾を広げて2段のフリル、ふくらんだ肩、背中の大きなリボン
                    Scale(robe, new Vector3(1.45f, 1f, 1.45f));
                    Part(body, PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(1.38f, 0.045f, 1.38f), it.layers != null ? it.layers[0] : White, it.glowCuff);
                    Part(body, PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0), new Vector3(1.18f, 0.04f, 1.18f), it.robe2);
                    Part(body, PrimitiveType.Cylinder, new Vector3(0, 0.36f, 0), new Vector3(0.98f, 0.035f, 0.98f), it.layers != null ? it.layers[0] : White, it.glowCuff);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Part(body, PrimitiveType.Sphere, new Vector3(sx * 0.25f, 1.2f, 0), new Vector3(0.24f, 0.2f, 0.26f), it.sleeve);
                        Part(body, PrimitiveType.Cube, new Vector3(sx * 0.13f, 0.98f, -0.22f), new Vector3(0.22f, 0.16f, 0.05f), it.sash, false, new Vector3(0, 0, sx * -18f));
                        Part(body, PrimitiveType.Cube, new Vector3(sx * 0.06f, 0.78f, -0.23f), new Vector3(0.07f, 0.32f, 0.03f), it.sash, false, new Vector3(0, 0, sx * 10f));
                    }
                    Part(body, PrimitiveType.Sphere, new Vector3(0, 0.98f, -0.21f), Vector3.one * 0.09f, it.sash);
                    break;
                case 3: // 十二単ふう：裾と袖口に色を重ね、うしろに長い裾をひく
                    Scale(robe, new Vector3(1.22f, 1f, 1.22f));
                    var lay = it.layers ?? new[] { it.robe2 };
                    for (int i = 0; i < lay.Length; i++)
                    {
                        float k = 1.18f + i * 0.045f;
                        Part(body, PrimitiveType.Cylinder, new Vector3(0, 0.025f + (lay.Length - 1 - i) * 0.03f, 0), new Vector3(k, 0.022f, k), lay[i], it.glowCuff && i == lay.Length - 1);
                    }
                    for (int i = 0; i < lay.Length; i++)
                        Part(body, PrimitiveType.Cube, new Vector3(0, 0.02f + i * 0.012f, -0.62f - i * 0.05f), new Vector3(0.75f - i * 0.08f, 0.02f, 0.95f - i * 0.06f), i == 0 ? it.robe : lay[i - 1], false, new Vector3(-4f, 0, 0));
                    foreach (var sl in sleeves) Scale(sl, new Vector3(1.12f, 1.4f, 1.55f));
                    foreach (var arm in new[] { doll.armL, doll.armR })
                    {
                        if (arm == null) continue;
                        var h = ArmHolder(arm);
                        for (int i = 0; i < lay.Length; i++)
                            Part(h, PrimitiveType.Cube, new Vector3(0, -0.47f - i * 0.035f, 0), new Vector3(0.2f + i * 0.012f, 0.03f, 0.42f + i * 0.02f), lay[i], it.glowCuff && i == lay.Length - 1);
                    }
                    // 襟元の重ね
                    for (int i = 0; i < lay.Length; i++)
                        Part(body, PrimitiveType.Cube, new Vector3(0, 1.21f - i * 0.02f, 0.13f + i * 0.006f), new Vector3(0.2f - i * 0.025f, 0.05f, 0.03f), lay[i], false, new Vector3(10, 0, 45));
                    break;
            }
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
                case "usamimi":
                    h = Holder(head, id);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, PrimitiveType.Capsule, new Vector3(0.08f * s, 0.36f, -0.02f), new Vector3(0.08f, 0.17f, 0.04f), White, false, new Vector3(0, 0, -10 * s));
                        Part(h, PrimitiveType.Capsule, new Vector3(0.08f * s, 0.355f, 0.004f), new Vector3(0.045f, 0.13f, 0.03f), Pink, false, new Vector3(0, 0, -10 * s));
                    }
                    return h.gameObject;
                case "kitsunemimi":
                    h = Holder(head, id);
                    var fox = new Color(0.98f, 0.68f, 0.25f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, PrimitiveType.Cube, new Vector3(0.12f * s, 0.26f, -0.01f), new Vector3(0.1f, 0.15f, 0.04f), fox, false, new Vector3(0, 0, 40 + 14 * s));
                        Part(h, PrimitiveType.Cube, new Vector3(0.12f * s, 0.255f, 0.008f), new Vector3(0.055f, 0.09f, 0.03f), White, false, new Vector3(0, 0, 40 + 14 * s));
                    }
                    return h.gameObject;
                case "hanakanmuri":
                    h = Holder(head, id); h.localPosition = new Vector3(0, 0.17f, -0.01f);
                    var cols = new[] { Pink, White, new Color(1f, 0.9f, 0.3f) };
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2;
                        var at = new Vector3(Mathf.Sin(a) * 0.2f, 0, Mathf.Cos(a) * 0.2f);
                        Part(h, PrimitiveType.Sphere, at, Vector3.one * 0.06f, cols[i % 3]);
                        Part(h, PrimitiveType.Sphere, at * 1.02f + Vector3.down * 0.025f, new Vector3(0.05f, 0.02f, 0.05f), new Color(0.4f, 0.7f, 0.35f));
                    }
                    return h.gameObject;
                case "oni":
                    h = Holder(head, id);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, PrimitiveType.Capsule, new Vector3(0.08f * s, 0.25f, 0.08f), new Vector3(0.05f, 0.07f, 0.05f), new Color(0.95f, 0.88f, 0.7f), false, new Vector3(-15, 0, -14 * s));
                        Part(h, PrimitiveType.Sphere, new Vector3(0.09f * s, 0.31f, 0.065f), Vector3.one * 0.035f, Red, false);
                    }
                    return h.gameObject;
                case "hikarinowa":
                    h = Holder(head, id); h.localPosition = new Vector3(0, 0.36f, -0.02f); h.localEulerAngles = new Vector3(-12, 0, 0);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * 360f;
                        var q = Quaternion.Euler(0, a, 0);
                        Part(h, PrimitiveType.Cube, q * new Vector3(0, 0, 0.15f), new Vector3(0.062f, 0.018f, 0.022f), Gold, true, new Vector3(0, a, 0));
                    }
                    return h.gameObject;
                case "chou":
                    h = Holder(body, id); h.localPosition = new Vector3(0, 1.1f, -0.26f);
                    var up = new Color(0.45f, 0.85f, 1f); var low = new Color(0.8f, 0.55f, 1f);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(h, PrimitiveType.Sphere, new Vector3(0.22f * s, 0.14f, -0.02f), new Vector3(0.34f, 0.3f, 0.02f), up, true, new Vector3(0, 12 * s, -25 * s));
                        Part(h, PrimitiveType.Sphere, new Vector3(0.17f * s, -0.1f, -0.02f), new Vector3(0.22f, 0.2f, 0.02f), low, true, new Vector3(0, 12 * s, 30 * s));
                    }
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
