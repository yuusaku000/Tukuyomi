using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 音量（メニューの「音量」で変える。PlayerPrefs に残す）。
    ///   全体：AudioListener.volume　環境音：波・水・ざわめき・風鈴・祭囃子・鐘・鹿威し・花火　効果音：釣り・遊覧船など
    /// </summary>
    public static class SoundPrefs
    {
        static float Get(string k, float d) => PlayerPrefs.GetFloat(k, d);
        static void Set(string k, float v) { PlayerPrefs.SetFloat(k, Mathf.Clamp01(v)); PlayerPrefs.Save(); }

        public static float Master { get => Get("tsukuyomi_vol_master", 0.8f); set { Set("tsukuyomi_vol_master", value); Apply(); } }
        public static float Ambient { get => Get("tsukuyomi_vol_ambient", 0.7f); set => Set("tsukuyomi_vol_ambient", value); }
        public static float Effects { get => Get("tsukuyomi_vol_fx", 0.8f); set => Set("tsukuyomi_vol_fx", value); }

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("tsukuyomi_mute", 0) == 1;
            set { PlayerPrefs.SetInt("tsukuyomi_mute", value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
        }

        public static void Apply() => AudioListener.volume = Muted ? 0f : Master;
    }
}
