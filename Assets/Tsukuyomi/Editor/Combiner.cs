using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 動かない部品（何万個もある）を、「同じマテリアル × 48m 四方の区画」ごとに1つの形にまとめる。
    /// 描く回数がぐっと減って軽くなる。当たり判定（コライダー）は元の物に残す。
    /// 小さい物（1.6m 未満）は影を落とさない。透明な物・大画面・大きな形（地形など）はまとめない。
    /// </summary>
    public static class Combiner
    {
        public static void Run(Transform root, float cell = 48f)
        {
            var groups = new Dictionary<string, (Material mat, bool shadow, List<CombineInstance> list)>();
            var victims = new List<MeshRenderer>();
            int count = 0;
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
            foreach (var mr in victims)
            {
                var mf = mr.GetComponent<MeshFilter>();
                Object.DestroyImmediate(mr);
                if (mf != null) Object.DestroyImmediate(mf);
            }
            Debug.Log($"[Tsukuyomi] combined {count} renderers into {groups.Count} meshes");
        }
    }
}
