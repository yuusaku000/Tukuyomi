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
        static readonly Dictionary<string, (int n, long tris)> _byMesh = new();
        static readonly Dictionary<string, (int n, long tris)> _bySize = new();

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
                {   // 確かめ用の数え上げ：どの形・どの大きさが三角形を多く使っているか
                    long tri = 0; for (int q = 0; q < mesh.subMeshCount; q++) tri += (long)mesh.GetIndexCount(q) / 3;
                    string mk = mesh.name;
                    _byMesh.TryGetValue(mk, out var e); _byMesh[mk] = (e.n + 1, e.tris + tri);
                    float sz = b.size.magnitude;
                    string bk = sz < 0.6f ? "0.6m未満" : sz < 1.6f ? "1.6m未満" : sz < 4f ? "4m未満" : sz < 12f ? "12m未満" : "12m以上";
                    _bySize.TryGetValue(bk, out var e2); _bySize[bk] = (e2.n + 1, e2.tris + tri);
                }
                float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                bool shadow = mr.shadowCastingMode != ShadowCastingMode.Off && big > 1.6f;
                // 小物（1.6m 未満）は区画を半分にして別にまとめ、遠くでは描かない層に入れる（Quality.SmallLayer）
                bool small = big < 1.6f;
                float cs = small ? cell * 0.5f : cell;
                int cx = Mathf.FloorToInt(b.center.x / cs), cz = Mathf.FloorToInt(b.center.z / cs);

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
                        string akey = "atlas_" + cx + "_" + cz + "_" + shadow + (small ? "_s" : "");
                        if (!groups.TryGetValue(akey, out var ag)) { ag = (atlasMat, shadow, new List<CombineInstance>()); groups[akey] = ag; }
                        ag.list.Add(new CombineInstance { mesh = copy, subMeshIndex = 0, transform = mr.localToWorldMatrix });
                        victims.Add(mr);
                        count++; atlased++;
                        continue;
                    }
                }

                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    string key = mats[s].GetInstanceID() + "_" + cx + "_" + cz + "_" + shadow + (small ? "_s" : "");
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
                if (kv.Key.EndsWith("_s")) go.layer = Quality.SmallLayer;
            }
            foreach (var t in temps) Object.DestroyImmediate(t);
            foreach (var mr in victims)
            {
                var mf = mr.GetComponent<MeshFilter>();
                Object.DestroyImmediate(mr);
                if (mf != null) Object.DestroyImmediate(mf);
            }
            var top = new List<KeyValuePair<string, (int n, long tris)>>(_byMesh);
            top.Sort((a, c) => c.Value.tris.CompareTo(a.Value.tris));
            var sb = new System.Text.StringBuilder("[Tsukuyomi] 三角形の多い形:");
            for (int i = 0; i < top.Count && i < 30; i++) sb.Append($"\n  {top[i].Key} x{top[i].Value.n} = {top[i].Value.tris / 1000}k ({top[i].Value.tris / top[i].Value.n}/個)");
            foreach (var kv in _bySize) sb.Append($"\n  大きさ {kv.Key}: {kv.Value.n}個 {kv.Value.tris / 1000}k");
            Debug.Log(sb.ToString());
            _byMesh.Clear(); _bySize.Clear();
            Debug.Log($"[Tsukuyomi] combined {count} renderers into {groups.Count} meshes ({atlased} by color atlas, {cells.Count} colors)");
        }
    }
}
