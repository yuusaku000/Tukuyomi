using System;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 現実の時刻（東京の太陽の位置）に合わせて、空・太陽・光・霧・星を変える。
    /// ツクヨミの大きな月は、いつも大鳥居の向こう（南の空）に浮かんでいて、夜はその月が明かりになる。
    /// </summary>
    public class RealTimeSky : MonoBehaviour
    {
        public Light sun, moon;
        public Material skybox;
        public Transform moonBody, moonHalo, stars;
        public Camera follow;
        public float moonElevation = 66f, moonAzimuth = 180f, moonDistance = 1500f;

        /// <summary>0 = 昼、1 = 夜。</summary>
        public static float Night { get; private set; }
        public static float SunElevation { get; private set; }

        static readonly int IdNight = Shader.PropertyToID("_TsNight");
        static readonly int IdSunDir = Shader.PropertyToID("_TsSunDir");
        static readonly int IdSunColor = Shader.PropertyToID("_TsSunColor");
        static readonly int IdSkyTop = Shader.PropertyToID("_TsSkyTop");
        static readonly int IdSkyHorizon = Shader.PropertyToID("_TsSkyHorizon");

        Material _sky;
        float _timer;
        Vector3 _moonDir;

        void Awake()
        {
            if (skybox != null)
            {
                _sky = new Material(skybox);
                RenderSettings.skybox = _sky;
            }
            _moonDir = Dir(moonElevation, moonAzimuth);
            Apply();
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0) { _timer = 0.5f; Apply(); }
            if (follow != null)
            {
                var c = follow.transform.position;
                if (moonBody) moonBody.position = c + _moonDir * moonDistance;
                if (moonHalo)
                {
                    moonHalo.position = c + _moonDir * (moonDistance + 40f);
                    moonHalo.rotation = Quaternion.LookRotation(_moonDir);
                }
                if (stars) stars.position = c;
            }
        }

        static Vector3 Dir(float elevationDeg, float azimuthDeg)
        {
            float el = elevationDeg * Mathf.Deg2Rad, az = azimuthDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        }

        /// <summary>東京（北緯35.68・東経139.77）の太陽の高さと方位（北=0、東=90）。</summary>
        public static void SunPosition(DateTime local, out float elevation, out float azimuth)
        {
            const double lat = 35.68, lon = 139.77;
            int n = local.DayOfYear;
            double decl = 23.44 * Math.Sin(2 * Math.PI * (284 + n) / 365.0) * Math.PI / 180;
            double b = 2 * Math.PI * (n - 81) / 364.0;
            double eot = 9.87 * Math.Sin(2 * b) - 7.53 * Math.Cos(b) - 1.5 * Math.Sin(b);
            double tc = 4 * (lon - 135) + eot;
            double lst = local.TimeOfDay.TotalHours + tc / 60.0;
            double hra = 15 * (lst - 12) * Math.PI / 180;
            double phi = lat * Math.PI / 180;
            double sinEl = Math.Sin(decl) * Math.Sin(phi) + Math.Cos(decl) * Math.Cos(phi) * Math.Cos(hra);
            double el = Math.Asin(Math.Max(-1, Math.Min(1, sinEl)));
            double cosAz = (Math.Sin(decl) * Math.Cos(phi) - Math.Cos(decl) * Math.Sin(phi) * Math.Cos(hra)) / Math.Max(1e-6, Math.Cos(el));
            double az = Math.Acos(Math.Max(-1, Math.Min(1, cosAz)));
            if (hra > 0) az = 2 * Math.PI - az;
            elevation = (float)(el * 180 / Math.PI);
            azimuth = (float)(az * 180 / Math.PI);
        }

        void Apply()
        {
            SunPosition(Ts.Now, out float el, out float az);
            SunElevation = el;
            var sunDir = Dir(el, az);
            Night = 1f - Ts.Smooth(-7f, 5f, el);
            float day = 1f - Night;
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(el - 1f) / 13f);

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDir);
                sun.intensity = 1.5f * Ts.Smooth(-3f, 12f, el);
                sun.color = Color.Lerp(new Color(1f, 0.5f, 0.28f), new Color(1f, 0.96f, 0.9f), Ts.Smooth(0f, 25f, el));
                sun.enabled = el > -4f;
            }
            if (moon != null)
            {
                // 月の光は、真上寄りから少し斜めに（影が長くなりすぎないように）
                var md = Vector3.Slerp(_moonDir, Vector3.up, 0.45f);
                moon.transform.rotation = Quaternion.LookRotation(-md);
                moon.intensity = 0.55f * Night;
                moon.color = new Color(0.62f, 0.72f, 1f);
                moon.enabled = Night > 0.02f;
            }
            RenderSettings.sun = Night > 0.5f ? moon : sun;

            var lightDir = Night > 0.5f ? _moonDir : sunDir;
            var lightCol = Night > 0.5f ? new Color(0.75f, 0.82f, 1f) * 0.8f : (sun != null ? sun.color : Color.white) * 1.2f;

            // 空（プロシージャルスカイ）
            if (_sky != null)
            {
                _sky.SetFloat("_Exposure", Mathf.Lerp(0.07f, 1.25f, day) + dusk * 0.15f);
                _sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.55f, 1.0f, day) + dusk * 0.25f);
                _sky.SetColor("_SkyTint", Color.Lerp(new Color(0.25f, 0.28f, 0.6f), new Color(0.5f, 0.55f, 0.65f), day));
                _sky.SetColor("_GroundColor", Color.Lerp(new Color(0.03f, 0.04f, 0.09f), new Color(0.35f, 0.37f, 0.42f), day));
                _sky.SetFloat("_SunSize", Night > 0.5f ? 0f : 0.035f);
            }

            var skyTop = Color.Lerp(new Color(0.02f, 0.03f, 0.1f), new Color(0.25f, 0.45f, 0.8f), day);
            var horizon = Color.Lerp(new Color(0.08f, 0.1f, 0.24f), new Color(0.7f, 0.8f, 0.92f), day);
            horizon = Color.Lerp(horizon, new Color(1f, 0.55f, 0.35f), dusk * 0.6f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.16f, 0.18f, 0.36f), new Color(0.62f, 0.68f, 0.8f), day);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.13f, 0.12f, 0.26f), new Color(0.52f, 0.5f, 0.48f), day) + new Color(0.25f, 0.12f, 0.05f) * dusk;
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.06f, 0.05f, 0.1f), new Color(0.3f, 0.27f, 0.24f), day);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // 朝（5〜9時）は霧が濃い
            double hr = Ts.Now.TimeOfDay.TotalHours;
            float morning = (float)(hr >= 5 && hr < 9.5 ? 1 - System.Math.Abs(hr - 7) / 2.5 : 0);
            RenderSettings.fogStartDistance = Mathf.Lerp(Mathf.Lerp(90f, 140f, day), 15f, morning);
            RenderSettings.fogEndDistance = Mathf.Lerp(Mathf.Lerp(700f, 1100f, day), 320f, morning);
            RenderSettings.fogColor = Color.Lerp(new Color(0.06f, 0.07f, 0.17f), new Color(0.72f, 0.8f, 0.9f), day) + new Color(0.3f, 0.12f, 0.02f) * dusk;

            Shader.SetGlobalFloat(IdNight, Night);
            Shader.SetGlobalVector(IdSunDir, lightDir);
            Shader.SetGlobalColor(IdSunColor, lightCol);
            Shader.SetGlobalColor(IdSkyTop, skyTop);
            Shader.SetGlobalColor(IdSkyHorizon, horizon);
        }

        /// <summary>時間帯のことば（画面の左上に出す）。</summary>
        public static string PeriodName(DateTime t)
        {
            int h = t.Hour;
            if (h < 4) return "深夜";
            if (h < 6) return "明け方";
            if (h < 10) return "朝";
            if (h < 16) return "昼";
            if (h < 18) return "夕方";
            if (h < 23) return "夜";
            return "深夜";
        }
    }
}
