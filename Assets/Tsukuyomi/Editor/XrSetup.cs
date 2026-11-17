using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace Tsukuyomi.EditorTools
{
    /// <summary>
    /// VR の設定（OpenXR）をそろえる。ビルドの前に毎回呼ぶ（何度呼んでも同じになる）。
    ///  ・PC（Standalone）で OpenXR を使う
    ///  ・起動したときに自動で VR を始めない（「画面／VR」をえらんでから始める）
    ///  ・Quest（Touch）・Index・Vive・シンプルなコントローラーの割り当てを使えるようにする
    ///  ・両目を1回で描く（Single Pass Instanced）
    /// </summary>
    public static class XrSetup
    {
        const string Dir = "Assets/XR";
        const string PerTargetPath = Dir + "/XRGeneralSettingsPerBuildTarget.asset";

        [MenuItem("Tsukuyomi/VR の設定をそろえる")]
        public static void Ensure()
        {
            const BuildTargetGroup g = BuildTargetGroup.Standalone;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget per) || per == null)
            {
                per = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(PerTargetPath);
                if (per == null)
                {
                    Directory.CreateDirectory(Dir);
                    per = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(per, PerTargetPath);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, per, true);
            }
            if (!per.HasSettingsForBuildTarget(g)) per.CreateDefaultSettingsForBuildTarget(g);
            if (!per.HasManagerSettingsForBuildTarget(g)) per.CreateDefaultManagerSettingsForBuildTarget(g);
            var gs = per.SettingsForBuildTarget(g);
            gs.InitManagerOnStart = false; // 起動時はまだ VR を始めない
            if (gs.Manager != null && gs.Manager.activeLoaders.Count == 0)
                XRPackageMetadataStore.AssignLoader(gs.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", g);
            EditorUtility.SetDirty(gs);
            if (gs.Manager != null) EditorUtility.SetDirty(gs.Manager);
            EditorUtility.SetDirty(per);

            var ox = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
            if (ox != null)
            {
                ox.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                Enable<OculusTouchControllerProfile>(ox);
                Enable<MetaQuestTouchPlusControllerProfile>(ox);
                Enable<ValveIndexControllerProfile>(ox);
                Enable<HTCViveControllerProfile>(ox);
                Enable<KHRSimpleControllerProfile>(ox);
                EditorUtility.SetDirty(ox);
            }
            else Debug.LogWarning("[Tsukuyomi] OpenXR の設定が見つかりません");
            AssetDatabase.SaveAssets();
            Debug.Log($"[Tsukuyomi] VR の設定：loaders={gs.Manager?.activeLoaders.Count} initOnStart={gs.InitManagerOnStart}");
        }

        static void Enable<T>(OpenXRSettings ox) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var f = ox.GetFeature<T>();
            if (f != null && !f.enabled) { f.enabled = true; EditorUtility.SetDirty(f); }
        }
    }
}
