using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 動かない部品（何万個もある）を、「同じマテリアル × 区画」ごとに1つの形にまとめる。
    /// 描く回数がぐっと減って軽くなる。当たり判定（コライダー）は元の物に残す。
    /// 小さい物（1.6m 未満）は影を落とさない。透明な物・大画面・大きな形（地形など）はまとめない。
    /// 色だけのマテリアル（ColorMat, 名前が c_ で始まる。約1万色ある）は、1枚の色見本の絵（128×128）の場所を UV で指して
    /// 1つのマテリアルにまとめる（色ごとに別々に描いていたのが重さのいちばんの原因だった）。
    /// </summary>
    public static class Combiner
    {
        const int Atlas = 128;

        public static void Run(Transform root, float cell = 48f)
        {
            var groups = new Dictionary<string, (Material mat, bool shadow, List<CombineInstance> list)>();
            var victims = new List<MeshRenderer>();
            var temps = new List<Mesh>();
            var cells = new Dictionary<Color32, int>();
            var pix = new Color32[Atlas * Atlas];
            Material atlasMat = null;
            string atlasName = "color_atlas_" + Mathf.Abs(root.name.GetHashCode());
            int count = 0, atlased = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (mesh.vertexCount > 20000 || !mesh.isReadable) continue;
                var mats = mr.sharedMaterials;
                bool skip = false;
                foreach (var m in mats)
                    if (m == null || m.renderQueue >= 2500 || m.shader.name == "Tsukuyomi/Screen") skip = true;
                if (skip) continue;
                var b = mr.bounds;
                bool shadow = mr.shadowCastingMode != ShadowCastingMode.Off && Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) > 1.6f;
                int cx = Mathf.FloorToInt(b.center.x / cell), cz = Mathf.FloorToInt(b.center.z / cell);

                // 色だけのマテリアル → 色見本の絵へ
                if (mats.Length == 1 && mesh.subMeshCount == 1 && mats[0].name.StartsWith("c_") && mats[0].HasProperty("_BaseColor"))
                {
                    Color32 col = mats[0].GetColor("_BaseColor");
                    col.a = 255;
                    if (!cells.TryGetValue(col, out int ci) && cells.Count < Atlas * Atlas)
                    {
                        ci = cells.Count; cells[col] = ci; pix[ci] = col;
                    }
                    if (cells.ContainsKey(col))
                    {
                        if (atlasMat == null)
                        {
                            atlasMat = Kit.Lit(atlasName, Color.white, 0.3f);
                        }
                        var copy = Object.Instantiate(mesh);
                        var uv = new Vector2[copy.vertexCount];
                        var c = new Vector2((ci % Atlas + 0.5f) / Atlas, (ci / Atlas + 0.5f) / Atlas);
                        for (int i = 0; i < uv.Length; i++) uv[i] = c;
                        copy.uv = uv;
                        temps.Add(copy);
                        string akey = "atlas_" + cx + "_" + cz + "_" + shadow;
                        if (!groups.TryGetValue(akey, out var ag)) { ag = (atlasMat, shadow, new List<CombineInstance>()); groups[akey] = ag; }
                        ag.list.Add(new CombineInstance { mesh = copy, subMeshIndex = 0, transform = mr.localToWorldMatrix });
                        victims.Add(mr);
                        count++; atlased++;
                        continue;
                    }
                }

                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    string key = mats[s].GetInstanceID() + "_" + cx + "_" + cz + "_" + shadow;
                    if (!groups.TryGetValue(key, out var gr))
                    {
                        gr = (mats[s], shadow, new List<CombineInstance>());
                        groups[key] = gr;
                    }
                    gr.list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = mr.localToWorldMatrix });
                }
                victims.Add(mr);
                count++;
            }

            // 色見本の絵を保存して、まとめ用のマテリアルにはる
            if (atlasMat != null)
            {
                var tex = new Texture2D(Atlas, Atlas, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = atlasName };
                tex.SetPixels32(pix);
                tex.Apply();
                string path = Kit.Gen + "/" + atlasName + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(tex, path);
                atlasMat.SetTexture("_BaseMap", tex);
                atlasMat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(atlasMat);
            }

            var parent = new GameObject("まとめた形").transform;
            parent.SetParent(root, false);
            foreach (var kv in groups)
            {
                var (mat, shadow, list) = kv.Value;
                var m = new Mesh { indexFormat = IndexFormat.UInt32, name = "combined_" + mat.name };
                m.CombineMeshes(list.ToArray(), true, true);
                m.RecalculateBounds();
                var go = new GameObject("まとめ_" + mat.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            foreach (var t in temps) Object.DestroyImmediate(t);
            foreach (var mr in victims)
            {
                var mf = mr.GetComponent<MeshFilter>();
                Object.DestroyImmediate(mr);
                if (mf != null) Object.DestroyImmediate(mf);
            }
            Debug.Log($"[Tsukuyomi] combined {count} renderers into {groups.Count} meshes ({atlased} by color atlas, {cells.Count} colors)");
        }
    }
}
