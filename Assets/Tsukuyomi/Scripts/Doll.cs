using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>人形の見た目（色と髪型）。</summary>
    [Serializable]
    public struct DollLook
    {
        public Color skin, hair, robe, robe2, sleeve, cuff, sash, eye, accent;
        public bool buns, twinTails, longHair, ponytail;

        public static DollLook Yachiyo => new DollLook
        {
            skin = new Color(1f, 0.9f, 0.84f),
            hair = new Color(0.86f, 0.89f, 0.98f),
            robe = new Color(0.13f, 0.12f, 0.22f),
            robe2 = new Color(0.2f, 0.18f, 0.32f),
            sleeve = new Color(0.16f, 0.14f, 0.26f),
            cuff = new Color(0.78f, 0.2f, 0.32f),
            sash = new Color(0.45f, 0.78f, 0.95f),
            eye = new Color(0.45f, 0.3f, 0.85f),
            accent = new Color(1f, 0.6f, 0.75f),
            buns = true, twinTails = true,
        };

        public static DollLook Player => new DollLook
        {
            skin = new Color(1f, 0.88f, 0.8f),
            hair = new Color(0.16f, 0.12f, 0.16f),
            robe = new Color(0.95f, 0.95f, 0.97f),
            robe2 = new Color(0.85f, 0.3f, 0.45f),
            sleeve = new Color(0.97f, 0.96f, 0.98f),
            cuff = new Color(0.85f, 0.3f, 0.45f),
            sash = new Color(0.85f, 0.3f, 0.45f),
            eye = new Color(0.25f, 0.15f, 0.12f),
            accent = new Color(1f, 0.85f, 0.4f),
            longHair = true,
        };
    }

    /// <summary>
    /// 基本の形（球・円柱・カプセル・箱）を組み合わせて、平安ふうの着物の人形を作る。
    /// 背の高さは約1.8（親で 0.9 倍にすると約1.6m）。色ごとのマテリアルは呼び出し側が渡す。
    /// </summary>
    public static class Doll
    {
        static Mesh _cone;

        /// <summary>着物のすそ（下が広い円すい台）。</summary>
        public static Mesh Cone
        {
            get
            {
                if (_cone != null) return _cone;
                const int n = 24;
                var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
                float r0 = 0.46f, r1 = 0.2f, h = 0.86f;
                for (int i = 0; i <= n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    v.Add(new Vector3(c * r0, 0, s * r0)); uv.Add(new Vector2(i / (float)n, 0));
                    v.Add(new Vector3(c * r1, h, s * r1)); uv.Add(new Vector2(i / (float)n, 1));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = i * 2;
                    t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
                // 底
                int center = v.Count; v.Add(Vector3.zero); uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < n; i++) t.AddRange(new[] { center, i * 2 + 2, i * 2 });
                _cone = new Mesh { name = "DollRobe" };
                _cone.SetVertices(v); _cone.SetUVs(0, uv); _cone.SetTriangles(t, 0);
                _cone.RecalculateNormals(); _cone.RecalculateBounds();
                return _cone;
            }
        }

        public struct Parts
        {
            public GameObject root;
            public Transform body, armL, armR, head;
        }

        static void NoCollider(GameObject g)
        {
            var c = g.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(c);
            else UnityEngine.Object.DestroyImmediate(c);
        }

        static GameObject P(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = m;
            NoCollider(g);
            return g;
        }

        public static Parts Build(string name, DollLook L, Func<Color, Material> mat, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            var robe = new GameObject("Robe");
            robe.transform.SetParent(body, false);
            robe.AddComponent<MeshFilter>().sharedMesh = Cone;
            robe.AddComponent<MeshRenderer>().sharedMaterial = mat(L.robe);
            P(PrimitiveType.Cylinder, body, "Hem", new Vector3(0, 0.04f, 0), new Vector3(0.93f, 0.04f, 0.93f), mat(L.robe2));
            P(PrimitiveType.Capsule, body, "Torso", new Vector3(0, 1.0f, 0), new Vector3(0.44f, 0.3f, 0.34f), mat(L.robe));
            P(PrimitiveType.Cylinder, body, "Sash", new Vector3(0, 0.93f, 0), new Vector3(0.46f, 0.05f, 0.37f), mat(L.sash));
            P(PrimitiveType.Cube, body, "Collar", new Vector3(0, 1.22f, 0.12f), new Vector3(0.16f, 0.2f, 0.06f), mat(L.robe2), new Vector3(10, 0, 45));
            P(PrimitiveType.Sphere, body, "Accent", new Vector3(0, 1.08f, 0.17f), new Vector3(0.13f, 0.11f, 0.07f), mat(L.accent));

            var head = new GameObject("Head").transform;
            head.SetParent(body, false);
            head.localPosition = new Vector3(0, 1.47f, 0);
            P(PrimitiveType.Sphere, head, "Face", Vector3.zero, Vector3.one * 0.38f, mat(L.skin));
            P(PrimitiveType.Sphere, head, "HairBack", new Vector3(0, 0.05f, -0.045f), new Vector3(0.42f, 0.42f, 0.4f), mat(L.hair));
            P(PrimitiveType.Sphere, head, "Bangs", new Vector3(0, 0.13f, 0.07f), new Vector3(0.41f, 0.2f, 0.32f), mat(L.hair));
            P(PrimitiveType.Cube, head, "SideL", new Vector3(-0.17f, -0.06f, 0.05f), new Vector3(0.06f, 0.24f, 0.12f), mat(L.hair));
            P(PrimitiveType.Cube, head, "SideR", new Vector3(0.17f, -0.06f, 0.05f), new Vector3(0.06f, 0.24f, 0.12f), mat(L.hair));
            P(PrimitiveType.Sphere, head, "EyeL", new Vector3(-0.075f, -0.01f, 0.172f), new Vector3(0.065f, 0.09f, 0.03f), mat(L.eye));
            P(PrimitiveType.Sphere, head, "EyeR", new Vector3(0.075f, -0.01f, 0.172f), new Vector3(0.065f, 0.09f, 0.03f), mat(L.eye));
            P(PrimitiveType.Sphere, head, "CheekL", new Vector3(-0.11f, -0.07f, 0.155f), new Vector3(0.06f, 0.03f, 0.02f), mat(new Color(1f, 0.62f, 0.66f)));
            P(PrimitiveType.Sphere, head, "CheekR", new Vector3(0.11f, -0.07f, 0.155f), new Vector3(0.06f, 0.03f, 0.02f), mat(new Color(1f, 0.62f, 0.66f)));
            if (L.buns)
            {
                P(PrimitiveType.Sphere, head, "BunL", new Vector3(-0.15f, 0.24f, -0.03f), Vector3.one * 0.17f, mat(L.hair));
                P(PrimitiveType.Sphere, head, "BunR", new Vector3(0.15f, 0.24f, -0.03f), Vector3.one * 0.17f, mat(L.hair));
            }
            if (L.twinTails)
            {
                P(PrimitiveType.Capsule, head, "TailL", new Vector3(-0.27f, -0.12f, -0.08f), new Vector3(0.12f, 0.26f, 0.12f), mat(L.hair), new Vector3(0, 0, -18));
                P(PrimitiveType.Capsule, head, "TailR", new Vector3(0.27f, -0.12f, -0.08f), new Vector3(0.12f, 0.26f, 0.12f), mat(L.hair), new Vector3(0, 0, 18));
            }
            if (L.longHair)
                P(PrimitiveType.Capsule, head, "Long", new Vector3(0, -0.3f, -0.15f), new Vector3(0.36f, 0.34f, 0.12f), mat(L.hair));
            if (L.ponytail)
                P(PrimitiveType.Capsule, head, "Pony", new Vector3(0, -0.12f, -0.24f), new Vector3(0.13f, 0.28f, 0.13f), mat(L.hair), new Vector3(-20, 0, 0));

            Transform Arm(string n, float side)
            {
                var pivot = new GameObject(n).transform;
                pivot.SetParent(body, false);
                pivot.localPosition = new Vector3(0.22f * side, 1.24f, 0);
                P(PrimitiveType.Capsule, pivot, "Arm", new Vector3(0, -0.2f, 0), new Vector3(0.1f, 0.2f, 0.1f), mat(L.robe2));
                P(PrimitiveType.Cube, pivot, "Sleeve", new Vector3(0.03f * side, -0.25f, 0), new Vector3(0.16f, 0.36f, 0.28f), mat(L.sleeve));
                P(PrimitiveType.Cube, pivot, "Cuff", new Vector3(0.03f * side, -0.44f, 0), new Vector3(0.17f, 0.06f, 0.29f), mat(L.cuff));
                P(PrimitiveType.Sphere, pivot, "Hand", new Vector3(0, -0.48f, 0), Vector3.one * 0.08f, mat(L.skin));
                pivot.localEulerAngles = new Vector3(0, 0, 8 * side);
                return pivot;
            }

            var parts = new Parts { root = root, body = body, head = head, armL = Arm("ArmL", -1), armR = Arm("ArmR", 1) };
            var anim = root.AddComponent<DollAnimator>();
            anim.body = body; anim.head = head; anim.armL = parts.armL; anim.armR = parts.armR;
            return parts;
        }
    }
}
