using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi
{
    /// <summary>画質：0=軽い 1=ふつう 2=きれい。描く解像度と影の距離を変える。選んだものは覚えておく。</summary>
    public static class Quality
    {
        static readonly string[] Names = { "軽い", "ふつう", "きれい" };
        static readonly float[] Scale = { 0.6f, 0.8f, 1.0f };
        static readonly float[] Shadow = { 40f, 70f, 110f };

        public static int Level { get; private set; } = 1;
        public static string Name => Names[Level];

        public static void Load()
        {
            int v = 1;
            try { v = PlayerPrefs.GetInt("quality", 1); } catch { }
            // 確認用：--quality 0|1|2（覚えない）
            if (int.TryParse(Ts.Arg("--quality"), out var q)) { ApplyOnly(Mathf.Clamp(q, 0, 2)); return; }
            Apply(Mathf.Clamp(v, 0, 2));
        }

        public static void Next() => Apply((Level + 1) % 3);

        public static void Apply(int level)
        {
            ApplyOnly(level);
            try { PlayerPrefs.SetInt("quality", level); PlayerPrefs.Save(); } catch { }
        }

        static void ApplyOnly(int level)
        {
            Level = level;
            // ふちのギザギザ消し：軽い＝なし・ふつう＝FXAA（軽い）・きれい＝SMAA
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data.antialiasing = level == 0 ? AntialiasingMode.None : level == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Scale[level];
                urp.shadowDistance = Shadow[level];
                urp.upscalingFilter = UpscalingFilterSelection.FSR;
            }
        }
    }
}
