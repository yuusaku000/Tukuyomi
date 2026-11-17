using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// コマンドから exe を作る：
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Tsukuyomi.EditorTools.BuildScript.Build
    /// 出力先は環境変数 TSUKUYOMI_BUILD_OUT（なければ Build/Tsukuyomi.exe）。
    /// </summary>
    public static class BuildScript
    {
        /// <summary>世界の組み立てと設定だけ（コマンドからは、これと BuildOnly を別々に実行する）。</summary>
        public static void Prepare()
        {
            try
            {
                WorldBuilder.BuildAll();
                Settings();
            }
            catch (Exception e)
            {
                Debug.LogError("[Tsukuyomi] prepare failed: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tsukuyomi/exe を作る")]
        public static void Build()
        {
            Prepare();
            BuildOnly();
        }

        public static void BuildOnly()
        {
            try
            {
                XrSetup.Ensure(); // VR（OpenXR）の設定をそろえる
                var outPath = Environment.GetEnvironmentVariable("TSUKUYOMI_BUILD_OUT");
                if (string.IsNullOrEmpty(outPath)) outPath = "Build/Tsukuyomi.exe";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { WorldBuilder.ScenePath },
                    locationPathName = outPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                Debug.Log($"[Tsukuyomi] build {report.summary.result} {report.summary.totalSize / 1048576} MB in {report.summary.totalTime}");
                if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
            }
            catch (Exception e)
            {
                Debug.LogError("[Tsukuyomi] build failed: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void Settings()
        {
            PlayerSettings.companyName = "TsukuyomiFan";
            PlayerSettings.productName = "Tsukuyomi";
            PlayerSettings.bundleVersion = DateTime.Now.ToString("yyyy.MMdd.HHmm");
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.enableFrameTimingStats = true; // 確認用：CPU と GPU の時間をはかる
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.usePlayerLog = true;
            try { PlayerSettings.SplashScreen.show = false; } catch { }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { Icon() }, IconKind.Any);

            // 日本語の文字（TextMesh）の GUI/Text Shader は Unity の標準の部品で、いつも exe に入っている

            // 影を遠くまで
            foreach (var rp in new[] { GraphicsSettings.defaultRenderPipeline, QualitySettings.renderPipeline })
                if (rp is UniversalRenderPipelineAsset urp)
                {
                    urp.shadowDistance = 90f;
                    urp.shadowCascadeCount = 2;
                    EditorUtility.SetDirty(urp);
                }
            XrSetup.Ensure();
            AssetDatabase.SaveAssets();
        }

        /// <summary>アプリのアイコン：夜空に月と鳥居。</summary>
        static Texture2D Icon()
        {
            const string path = "Assets/Tsukuyomi/Generated/icon.png";
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    var c = Color.Lerp(new Color(0.05f, 0.04f, 0.2f), new Color(0.35f, 0.15f, 0.45f), 1 - v);
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.62f));
                    if (d < 0.27f) c = Color.Lerp(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.85f, 0.55f), d / 0.27f);
                    else c += new Color(0.5f, 0.45f, 0.7f) * Mathf.Pow(Mathf.Clamp01(1 - (d - 0.27f) / 0.2f), 2) * 0.5f;
                    bool pillar = (Mathf.Abs(u - 0.3f) < 0.035f || Mathf.Abs(u - 0.7f) < 0.035f) && v < 0.62f;
                    bool kasagi = Mathf.Abs(v - (0.64f + Mathf.Pow(Mathf.Abs(u - 0.5f), 2) * 0.5f)) < 0.035f && Mathf.Abs(u - 0.5f) < 0.36f;
                    bool nuki = Mathf.Abs(v - 0.5f) < 0.022f && Mathf.Abs(u - 0.5f) < 0.3f;
                    if (pillar || nuki) c = new Color(0.85f, 0.2f, 0.12f);
                    if (kasagi) c = new Color(0.1f, 0.08f, 0.1f);
                    float r = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                    c.a = r < 0.5f ? 1 : 0;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
