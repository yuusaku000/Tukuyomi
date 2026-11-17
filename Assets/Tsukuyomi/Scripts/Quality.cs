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

        // 遠くでは描かない物（島を組み立てるときに、この番号の層に入れる）
        public const int SmallLayer = 24;   // 1.6m より小さい動かない小物（花瓶・桶・提灯など）
        public const int PeopleLayer = 25;  // 町を歩く人
        static readonly float[] SmallFar = { 100f, 140f, 200f };   // 画面：小物を描く距離（見た目はほぼ変わらない）
        static readonly float[] VrSmallFar = { 40f, 60f, 90f };     // VR：小物を描く距離
        static readonly float[] VrPeopleFar = { 50f, 60f, 80f };    // VR：人を描く距離

        static void SetCull(Camera cam, float small, float people)
        {
            if (cam == null) return;
            var d = new float[32];
            d[SmallLayer] = small; d[PeopleLayer] = people; // 0 は「カメラの見える距離まで」
            cam.layerCullDistances = d;
            cam.layerCullSpherical = true; // 頭を回しても、出たり消えたりしないように（向きでなく距離で）
        }

        /// <summary>URP の「物のかげ」（SSAO）。すべての物をもう1回（深さと向きだけ）描くので重い。</summary>
        public static ScriptableRendererFeature Ssao()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (urp == null || f == null || !(f.GetValue(urp) is ScriptableRendererData[] list)) return null;
            foreach (var d in list) if (d != null) foreach (var x in d.rendererFeatures) if (x != null && x.name.Contains("Ambient")) return x;
            return null;
        }
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
            if (Vr.On) { Level = level; try { PlayerPrefs.SetInt("quality", level); PlayerPrefs.Save(); } catch { } return; } // VR 中は VR の画質を使う（画面の画質は覚えるだけ）
            ApplyOnly(level);
            try { PlayerPrefs.SetInt("quality", level); PlayerPrefs.Save(); } catch { }
        }

        // VR：両目ぶん描くので、画面より控えめに。ギザギザ消しは MSAA（FXAA・SMAA は VR でにじむ・重い）
        static readonly float[] VrScale = { 0.75f, 0.9f, 1.0f };
        static readonly float[] VrShadow = { 0f, 35f, 70f };
        static readonly int[] VrMsaa = { 1, 2, 4 };
        static readonly float[] VrLod = { 0.5f, 0.75f, 1.0f };

        /// <summary>VR の画質（0 軽い・1 ふつう・2 きれい）。</summary>
        public static void ApplyVr(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                data.antialiasing = AntialiasingMode.None;
                data.renderPostProcessing = level > 0; // 軽い：光のにじみ（後処理）なし
                data.renderShadows = level > 0;        // 軽い：影なし
                // 画面の色・深さの写しは使っていないので、VR では作らない（毎回の写しの分だけ軽くなる）
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = VrScale[level];
                urp.shadowDistance = VrShadow[level];
                urp.msaaSampleCount = VrMsaa[level];
                urp.upscalingFilter = UpscalingFilterSelection.Auto; // FSR は VR では使えない
                urp.supportsHDR = level > 0;                 // 軽い：明るさの幅を広くとらない
                urp.maxAdditionalLightsCount = level == 0 ? 2 : 4; // 軽い：1つの物を照らす小さな明かりは2つまで
            }
            QualitySettings.lodBias = VrLod[level];
            Ssao()?.SetActive(false); // VR：物のかげは切る（物を2回描かずにすむ。いちばん効く）
            SetCull(cam, VrSmallFar[level], VrPeopleFar[level]);
            Debug.Log($"[VR] 画質 {level}: 描く細かさ {VrScale[level]} 影 {VrShadow[level]}m MSAA {VrMsaa[level]} 小物 {VrSmallFar[level]}m 人 {VrPeopleFar[level]}m SSAO なし");
        }

        static void ApplyOnly(int level)
        {
            Level = level;
            // ふちのギザギザ消し：軽い＝なし・ふつう＝FXAA（軽い）・きれい＝SMAA
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data.antialiasing = level == 0 ? AntialiasingMode.None : level == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            SetCull(cam, SmallFar[level], 0f);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Scale[level];
                urp.shadowDistance = Shadow[level];
                urp.upscalingFilter = UpscalingFilterSelection.FSR;
            }
        }
    }
}
