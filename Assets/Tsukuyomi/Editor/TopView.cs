using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// 確認用：できた世界を真上から撮って PNG にする（空き地・橋・道のつながりを見るため）。
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Tsukuyomi.EditorTools.TopView.Shoot
    /// 出力先は環境変数 TSUKUYOMI_TOPVIEW_DIR（なければ Build/topview）。
    /// </summary>
    public static class TopView
    {
        [MenuItem("Tsukuyomi/真上から撮る")]
        public static void Shoot()
        {
            try
            {
                EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
                Shader.SetGlobalFloat("_TsNight", 0f);
                string dir = Environment.GetEnvironmentVariable("TSUKUYOMI_TOPVIEW_DIR");
                if (string.IsNullOrEmpty(dir)) dir = "Build/topview";
                Directory.CreateDirectory(dir);
                var go = new GameObject("真上カメラ");
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.transform.rotation = Quaternion.Euler(90, 0, 0);
                cam.nearClipPlane = 1f; cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
                const int px = 1000;
                const float size = 170f;   // 1枚の幅（m）
                var rt = new RenderTexture(px, px, 24);
                cam.targetTexture = rt;
                cam.orthographicSize = size / 2;
                var tex = new Texture2D(px, px, TextureFormat.RGB24, false);
                int k = 0;
                foreach (float cz in new[] { 70f, 240f })
                    foreach (float cx in new[] { -85f, 85f })
                    {
                        cam.transform.position = new Vector3(cx, 250f, cz);
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, px, px), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;
                        // 10m ごとの目盛り（黒い点線）
                        string file = Path.Combine(dir, $"top_{k}_x{cx}_z{cz}.png");
                        File.WriteAllBytes(file, tex.EncodeToPNG());
                        Debug.Log("[Tsukuyomi] topview " + file);
                        k++;
                    }
                UnityEngine.Object.DestroyImmediate(go);
            }
            catch (Exception e)
            {
                Debug.LogError("[Tsukuyomi] topview failed: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
        /// <summary>確認用：TSUKUYOMI_PROBE="x,z0,z1" の線の上で、地面の高さ・水・その上にある薄いもの（道など）を調べる。</summary>
        public static void Probe()
        {
            EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            var a = Environment.GetEnvironmentVariable("TSUKUYOMI_PROBE").Split(',');
            float x = float.Parse(a[0]), z0 = float.Parse(a[1]), z1 = float.Parse(a[2]);
            var rs = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (float z = z0; z <= z1; z += 1f)
            {
                var line = $"[Probe] x={x} z={z} H={Tsukuyomi.Island.Height(x, z):0.00} water={Tsukuyomi.Island.NearestWater(x, z):0.0} surf={Tsukuyomi.Island.WaterSurface(x, z):0.00}";
                if (Physics.Raycast(new Vector3(x, 100, z), Vector3.down, out var hit, 200)) line += $" hit={hit.collider.name}@{hit.point.y:0.00}";
                foreach (var r in rs)
                {
                    var b = r.bounds;
                    if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || b.size.y > 3f) continue;
                    // そのメッシュが本当にこの点の上を覆っているか（三角形で判定）
                    var m = mf.sharedMesh; var v = m.vertices; var t = m.triangles; var tr = r.transform;
                    for (int k = 0; k < t.Length; k += 3)
                    {
                        var p0 = tr.TransformPoint(v[t[k]]); var p1 = tr.TransformPoint(v[t[k + 1]]); var p2 = tr.TransformPoint(v[t[k + 2]]);
                        if (Inside(new Vector2(x, z), p0, p1, p2)) { line += $" | {r.name}({r.sharedMaterial?.name})@{p0.y:0.00}"; break; }
                    }
                }
                Debug.Log(line);
            }
        }

        static bool Inside(Vector2 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.y - b.z);
            float d2 = (p.x - c.x) * (b.z - c.z) - (b.x - c.x) * (p.y - c.z);
            float d3 = (p.x - a.x) * (c.z - a.z) - (c.x - a.x) * (p.y - a.z);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
