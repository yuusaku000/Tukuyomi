using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 画面の音（ボタン・買う・足音・遊覧船の合図）。音量はメニューの「効果音」。
    /// 足音は自分が歩いているとき、歩いた距離に合わせて鳴らす。
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx _i;
        AudioSource _src;
        AudioClip _click, _coin, _gong;
        AudioClip[] _steps;
        float _walked;
        Vector3 _last;
        PlayerController _pc;

        static Sfx I
        {
            get
            {
                if (_i == null)
                {
                    var go = new GameObject("画面の音");
                    _i = go.AddComponent<Sfx>();
                }
                return _i;
            }
        }

        void Awake()
        {
            _i = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0; _src.priority = 0; _src.playOnAwake = false;
            _click = Synth.Click(); _coin = Synth.Coin(); _gong = Synth.Gong();
            // 足音：本物の足音の録音（OpenGameArt「Footsteps」, CC0）。なければ合成の音
            var rec = new System.Collections.Generic.List<AudioClip>();
            foreach (var c in Resources.LoadAll<AudioClip>("Sounds")) if (c.name.StartsWith("step_")) rec.Add(c);
            _steps = rec.Count > 0 ? rec.ToArray() : new[] { Synth.Step(0), Synth.Step(1), Synth.Step(2) };
        }

        public static void Play(AudioClip c, float vol)
        {
            if (Ts.ShotMode || c == null) return;
            I._src.PlayOneShot(c, vol * SoundPrefs.Effects);
        }

        public static void Click() => Play(I._click, 0.6f);
        public static void Coin() => Play(I._coin, 0.9f);
        public static void Gong() => Play(I._gong, 0.8f);

        /// <summary>はじめに一度作っておく（足音のため）。</summary>
        public static void Ensure() { var _ = I; }

        void Update()
        {
            if (_pc == null) { _pc = FindFirstObjectByType<PlayerController>(); if (_pc != null) _last = _pc.Position; return; }
            var p = _pc.Position;
            var d = p - _last; _last = p;
            float h = new Vector2(d.x, d.z).magnitude;
            // 歩いて地面にいるときだけ（ワープ・乗り物・飛行・水の中はのぞく）
            if (h > 2f || PlayerController.Fly || PlayerController.Busy || _pc.InWater || Mathf.Abs(d.y) > 0.5f) { _walked = 0; return; }
            if (SkyShip.I != null && SkyShip.I.where == SkyShip.Where.Flying && SkyShip.I.RiderAboard) return;
            _walked += h;
            if (_walked > 0.75f)
            {
                _walked = 0;
                var s = _steps[Random.Range(0, _steps.Length)];
                Play(s, 0.45f);
            }
        }
    }
}
