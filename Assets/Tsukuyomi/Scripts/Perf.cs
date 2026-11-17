using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi
{
    /// <summary>
    /// 重さをはかる確かめ用（tour の4つ目に perf）。その場所で、物の種類や設定を1つずつ消して、
    /// fps・描く側（GPU）の時間・描く回数をくらべてログに書く。遊ぶときには使わない。
    /// </summary>
    public static class Perf
    {
        struct Sample { public float fps, gpu, cpu; public long dc, setpass, tris; }

        static IEnumerator Measure(Sample[] outS, float secs = 1.5f)
        {
            yield return new WaitForSeconds(0.6f);
            using var dc = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var sp = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var tr = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
            double gpu = 0, cpu = 0; int n = 0;
            var ft = new FrameTiming[1];
            while (Time.realtimeSinceStartup - t0 < secs)
            {
                yield return null;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, ft) > 0 && ft[0].gpuFrameTime > 0) { gpu += ft[0].gpuFrameTime; cpu += ft[0].cpuFrameTime; n++; }
            }
            outS[0] = new Sample
            {
                fps = (Time.frameCount - f0) / (Time.realtimeSinceStartup - t0),
                gpu = n > 0 ? (float)(gpu / n) : 0, cpu = n > 0 ? (float)(cpu / n) : 0,
                dc = dc.LastValue, setpass = sp.LastValue, tris = tr.LastValue,
            };
        }

        static List<Renderer> Under(string root)
        {
            var l = new List<Renderer>();
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == root) l.AddRange(go.GetComponentsInChildren<Renderer>());
            return l;
        }

        static bool Transparent(Renderer r)
        {
            foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue >= 2500) return true;
            return false;
        }

        static bool ShaderIs(Renderer r, string part)
        {
            foreach (var m in r.sharedMaterials) if (m != null && m.shader.name.Contains(part)) return true;
            return false;
        }

        /// <summary>tour から：その場所で一通りはかる。</summary>
        public static IEnumerator Run(string place)
        {
            var cam = Camera.main;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            var all = new List<Renderer>(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None));
            all.RemoveAll(r => !r.enabled);
            var world = Under("ワールド");
            var combined = world.FindAll(r => r.transform.parent != null && r.transform.parent.name == "まとめた形");
            var worldOther = world.FindAll(r => !combined.Contains(r) && !Transparent(r));
            var deco = Under("動くもの").FindAll(r => !Transparent(r));
            var crowd = Under("町の人たち");
            var trans = all.FindAll(r => Transparent(r) && !(r is ParticleSystemRenderer));
            var parts = all.FindAll(r => r is ParticleSystemRenderer);
            var water = all.FindAll(r => ShaderIs(r, "Water"));
            var far = new List<Renderer>();
            var cp = cam.transform.position;
            foreach (var r in all) if ((r.bounds.ClosestPoint(cp) - cp).magnitude > 120f && !water.Contains(r)) far.Add(r);

            // 目に見えている数
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            int vis = 0; foreach (var r in all) if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) vis++;
            Debug.Log($"[Perf] {place} renderers={all.Count} 見える={vis} まとめ={combined.Count} ワールドの残り={worldOther.Count} 動くもの={deco.Count} 人={crowd.Count} 透ける={trans.Count} 粒={parts.Count} 水={water.Count} 120m先={far.Count} 画面={Screen.width}x{Screen.height} scale={urp.renderScale} msaa={urp.msaaSampleCount} hdr={urp.supportsHDR} 影={urp.shadowDistance} post={data.renderPostProcessing} far={cam.farClipPlane}");

            var s = new Sample[1];
            Sample baseS = default;
            IEnumerator Do(string label, System.Action on, System.Action off)
            {
                on?.Invoke();
                yield return Measure(s);
                off?.Invoke();
                if (label == "そのまま") baseS = s[0];
                Debug.Log($"[Perf] {place} {label,-14} fps={s[0].fps:0.0} gpu={s[0].gpu:0.0}ms cpu={s[0].cpu:0.0}ms dc={s[0].dc} setpass={s[0].setpass} tris={s[0].tris / 1000}k" + (label == "そのまま" ? "" : $"  (gpu {s[0].gpu - baseS.gpu:+0.0;-0.0}ms)"));
            }
            System.Action Hide(List<Renderer> l) => () => { foreach (var r in l) if (r) r.enabled = false; };
            System.Action Show(List<Renderer> l) => () => { foreach (var r in l) if (r) r.enabled = true; };

            yield return Do("そのまま", null, null);
            float sc = urp.renderScale;
            yield return Do("解像度半分", () => urp.renderScale = sc * 0.5f, () => urp.renderScale = sc);
            yield return Do("まとめた形", Hide(combined), Show(combined));
            yield return Do("ワールドの残り", Hide(worldOther), Show(worldOther));
            yield return Do("動くもの", Hide(deco), Show(deco));
            yield return Do("町の人", Hide(crowd), Show(crowd));
            yield return Do("透ける物", Hide(trans), Show(trans));
            yield return Do("粒", Hide(parts), Show(parts));
            yield return Do("水", Hide(water), Show(water));
            yield return Do("120m先", Hide(far), Show(far));
            var flags = cam.clearFlags;
            yield return Do("空", () => cam.clearFlags = CameraClearFlags.SolidColor, () => cam.clearFlags = flags);
            bool sh = data.renderShadows;
            yield return Do("影", () => data.renderShadows = false, () => data.renderShadows = sh);
            bool pp = data.renderPostProcessing;
            yield return Do("後処理", () => data.renderPostProcessing = false, () => data.renderPostProcessing = pp);
            int al = urp.maxAdditionalLightsCount;
            yield return Do("小さな明かり", () => urp.maxAdditionalLightsCount = 0, () => urp.maxAdditionalLightsCount = al);
            var ao = Quality.Ssao();
            if (ao != null && ao.isActive) yield return Do("SSAO", () => ao.SetActive(false), () => ao.SetActive(true));
            var crowdFar = crowd.FindAll(r => (r.bounds.center - cp).magnitude > 50f);
            yield return Do("人50m先", Hide(crowdFar), Show(crowdFar));
            float fc = cam.farClipPlane;
            yield return Do("見える距離150", () => cam.farClipPlane = 150, () => cam.farClipPlane = fc);
            yield return Do("全部消す", () => { foreach (var r in all) if (r) r.enabled = false; }, () => { foreach (var r in all) if (r) r.enabled = true; });
        }
    }
}
