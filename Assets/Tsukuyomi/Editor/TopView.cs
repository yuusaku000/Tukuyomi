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
    }
}
