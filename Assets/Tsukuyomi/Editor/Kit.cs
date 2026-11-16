using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tsukuyomi.EditorTools
{
    /// <summary>世界を組み立てるための道具：マテリアル・基本の形・屋根の形を作って保存する。</summary>
    public static class Kit
    {
        public const string Gen = "Assets/Tsukuyomi/Generated";
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        static Shader _lit, _glow;

        public static void Begin()
        {
            Mats.Clear();
            Meshes.Clear();
            _lowSphere = null;
            foreach (var d in new[] { Gen, Gen + "/Materials", Gen + "/Meshes" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _glow = Shader.Find("Tsukuyomi/Glow");
            if (_lit == null) throw new System.Exception("URP Lit shader not found");
            if (_glow == null) throw new System.Exception("Tsukuyomi/Glow shader not found");
        }

        static Material Asset(string name, Shader shader)
        {
            if (Mats.TryGetValue(name, out var m)) return m;
            string path = $"{Gen}/Materials/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            Mats[name] = m;
            return m;
        }

        public static Material Lit(string name, Color c, float smooth = 0.25f, float metal = 0f, bool twoSided = false)
        {
            bool known = Mats.ContainsKey(name);
            var m = Asset(name, _lit);
            if (known) return m;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            m.doubleSidedGI = twoSided;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>絵（テクスチャ）つきのマテリアル。cutout なら葉っぱのように透けた部分を抜く。</summary>
        public static Material LitTex(string name, Texture2D tex, bool cutout, float smooth = 0.2f)
        {
            bool known = Mats.ContainsKey(name);
            var m = Asset(name, _lit);
            if (known) return m;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);
                m.renderQueue = 2450;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Shaded(string name, string shader)
        {
            var sh = Shader.Find(shader);
            if (sh == null) throw new System.Exception("shader not found: " + shader);
            return Asset(name, sh);
        }

        public static Material Glow(string name, Color c, float day = 0.7f, float night = 3f, float pulse = 0f)
        {
            bool known = Mats.ContainsKey(name);
            var m = Asset(name, _glow);
            if (known) return m;
            m.SetColor("_Color", c);
            m.SetFloat("_Day", day);
            m.SetFloat("_Night", night);
            m.SetFloat("_Pulse", pulse);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>人形などの色ごとのマテリアル。</summary>
        public static Material ColorMat(Color c) => Lit("c_" + ColorUtility.ToHtmlStringRGB(c), c, 0.3f);

        public static Transform Group(Transform parent, string name, Vector3 pos, float rotY = 0)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0, rotY, 0);
            return g;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider = false, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        public static GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, Material m, bool col = false, Vector3? euler = null) =>
            Prim(PrimitiveType.Cube, p, n, pos, size, m, col, euler);

        /// <summary>円柱。pos は中心、height は全体の高さ。</summary>
        public static GameObject Cyl(Transform p, string n, Vector3 pos, float radius, float height, Material m, bool col = false, Vector3? euler = null)
        {
            var g = Prim(PrimitiveType.Cylinder, p, n, pos, new Vector3(radius * 2, height / 2, radius * 2), m, false, euler);
            if (col)
            {
                var bc = g.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.4f, 2f, 1.4f);
            }
            return g;
        }

        static Mesh _lowSphere;

        /// <summary>軽い球（12×8）。Unity の球（約760三角形）のかわりに使う。</summary>
        public static Mesh LowSphere
        {
            get
            {
                if (_lowSphere != null) return _lowSphere;
                const int seg = 12, ring = 8;
                var v = new List<Vector3>(); var t = new List<int>();
                for (int r = 0; r <= ring; r++)
                    for (int s = 0; s <= seg; s++)
                    {
                        float th = Mathf.PI * r / ring, ph = 2 * Mathf.PI * s / seg;
                        v.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f);
                    }
                for (int r = 0; r < ring; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int k = r * (seg + 1) + s;
                        t.AddRange(new[] { k, k + 1, k + seg + 1, k + 1, k + seg + 2, k + seg + 1 });
                    }
                var m = new Mesh { name = "low_sphere" };
                m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
                _lowSphere = SaveMesh("low_sphere", m);
                return _lowSphere;
            }
        }

        public static GameObject Ball(Transform p, string n, Vector3 pos, Vector3 size, Material m, bool col = false)
        {
            if (col) return Prim(PrimitiveType.Sphere, p, n, pos, size, m, col);
            var g = MeshObj(p, n, LowSphere, pos, m);
            g.transform.localScale = size;
            return g;
        }

        public static GameObject MeshObj(Transform p, string n, Mesh mesh, Vector3 pos, Material m, bool collider = false)
        {
            var g = new GameObject(n);
            g.transform.SetParent(p, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            return g;
        }

        static Mesh Save(string key, Mesh mesh)
        {
            string path = $"{Gen}/Meshes/{key}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null && old == mesh) { Meshes[key] = old; return old; }
            if (old != null)
            {
                old.Clear();
                EditorUtility.CopySerialized(mesh, old);
                Meshes[key] = old;
                return old;
            }
            AssetDatabase.CreateAsset(mesh, path);
            Meshes[key] = mesh;
            return mesh;
        }

        public static Mesh SaveMesh(string key, Mesh mesh) => Meshes.TryGetValue(key, out var m) ? m : Save(key, mesh);

        /// <summary>同じ名前があっても作り直して保存する（地形など、毎回変わるもの）。</summary>
        public static Mesh SaveMeshForce(string key, Mesh mesh) => Save(key, mesh);

        /// <summary>
        /// 平安ふうの寄棟の屋根。軒は浅く、棟に向かって急になる反りと、四隅のはね上がり。w×d は軒先までの大きさ。
        /// </summary>
        public static Mesh Roof(float w, float d, float h, float lift = 0.7f)
        {
            string key = $"roof_{w:0.0}_{d:0.0}_{h:0.0}_{lift:0.0}".Replace('.', 'p');
            if (Meshes.TryGetValue(key, out var cached)) return cached;
            const int n = 16;
            float a = w / 2, b = d / 2, half = Mathf.Min(a, b);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    float u = -1 + 2f * i / n, vv = -1 + 2f * j / n;
                    float x = u * a, z = vv * b;
                    float sx = (a - Mathf.Abs(x)) / half, sz = (b - Mathf.Abs(z)) / half;
                    float s = Mathf.Clamp01(Mathf.Min(sx, sz));
                    float y = h * Mathf.Pow(s, 1.5f);
                    float corner = Mathf.Pow(Mathf.Abs(u), 8) * Mathf.Pow(Mathf.Abs(vv), 8);
                    y += lift * corner + lift * 0.18f * Mathf.Pow(1 - s, 5);
                    v.Add(new Vector3(x, y, z));
                    uv.Add(new Vector2(i / (float)n, j / (float)n));
                }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int k = i * (n + 1) + j;
                    t.AddRange(new[] { k, k + 1, k + n + 1, k + 1, k + n + 2, k + n + 1 });
                }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return Save(key, mesh);
        }

        /// <summary>なめらかな丘（てっぺんが平らぎみのお椀をふせた形）。歩いて登れる。</summary>
        public static Mesh Hill(float radius, float height)
        {
            string key = $"hill_{radius:0}_{height:0}";
            if (Meshes.TryGetValue(key, out var cached)) return cached;
            const int rings = 24, segs = 48;
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            for (int r = 0; r <= rings; r++)
            {
                float f = r / (float)rings;              // 0 = 中心, 1 = ふち
                float y = height * (1 - Ts.Smooth(0f, 1f, f)) - 0.6f * f;
                for (int s = 0; s <= segs; s++)
                {
                    float ang = s / (float)segs * Mathf.PI * 2;
                    v.Add(new Vector3(Mathf.Cos(ang) * radius * f, y, Mathf.Sin(ang) * radius * f));
                    uv.Add(new Vector2(s / (float)segs, f));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int k = r * (segs + 1) + s;
                    t.AddRange(new[] { k, k + 1, k + segs + 1, k + 1, k + segs + 2, k + segs + 1 });
                }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return Save(key, mesh);
        }
    }
}
