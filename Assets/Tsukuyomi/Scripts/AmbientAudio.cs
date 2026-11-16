using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// ツクヨミの音：海の波・滝と川・商店街のざわめき・風鈴・遠くの祭囃子（夕方から）・毎正時の鐘。
    /// 音はすべて Synth でその場で作る。音量はメニューの「音量」（SoundPrefs）で変えられる。
    /// 遠くて聞こえない音は止めておく（同時に鳴らせる数をこえると、音がとぎれたりブツブツしたりするため）。
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        public Transform[] waterPoints, crowdPoints, chimePoints;
        public Transform festivalPoint, bellPoint;

        AudioSource _sea, _festival, _bell;
        AudioSource[] _chimes;
        readonly List<(AudioSource s, float vol, bool crowd)> _loops = new List<(AudioSource, float, bool)>();
        AudioClip[] _chimeClips;
        int _lastHour = -1;
        float _chimeT, _cullT;
        Transform _ear;

        /// <summary>メニューのミュート（SoundPrefs に残す）。</summary>
        public static bool Muted
        {
            get => SoundPrefs.Muted;
            set => SoundPrefs.Muted = value;
        }

        AudioSource Src(Vector3 pos, AudioClip clip, float vol, float maxDist, bool loop, bool spatial = true, int priority = 200)
        {
            var go = new GameObject("音");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = vol; s.loop = loop;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f; s.maxDistance = maxDist;
            s.dopplerLevel = 0;
            s.priority = priority;
            s.playOnAwake = false;
            if (loop) { s.time = Random.Range(0f, clip.length * 0.9f); s.Play(); }
            return s;
        }

        void Start()
        {
            SoundPrefs.Apply();
            if (Ts.ShotMode) { enabled = false; return; }
            var sea = Synth.Water(14f, 0.035f);
            _sea = Src(Vector3.zero, sea, 0.1f, 1, true, false, 150);
            _loops.Add((_sea, 0.1f, false));
            var falls = Synth.Water(10f, 0.18f);
            foreach (var p in waterPoints) if (p) { var s = Src(p.position, falls, 0.38f, 42f, true); _loops.Add((s, 0.38f, false)); }
            var crowd = Synth.Crowd(14f);
            foreach (var p in crowdPoints) if (p) { var s = Src(p.position, crowd, 0.18f, 38f, true); _loops.Add((s, 0.18f, true)); }
            _chimeClips = new[] { Synth.Chime(1760f), Synth.Chime(2093f), Synth.Chime(2349f), Synth.Chime(2637f) };
            _chimes = new AudioSource[chimePoints.Length];
            for (int i = 0; i < chimePoints.Length; i++) if (chimePoints[i]) _chimes[i] = Src(chimePoints[i].position, _chimeClips[0], 0.35f, 28f, false, true, 120);
            if (festivalPoint) _festival = Src(festivalPoint.position, Synth.Festival(), 0.0f, 120f, true, true, 100);
            if (bellPoint) _bell = Src(bellPoint.position, Synth.Bell(), 0.9f, 600f, false, true, 10);
            if (_bell) _bell.minDistance = 30f;
            _lastHour = Ts.Now.Hour;
        }

        void Update()
        {
            if (_ear == null && Camera.main != null) _ear = Camera.main.transform;
            var now = Ts.Now;
            float amb = SoundPrefs.Ambient;
            // 夕方から夜にかけて祭囃子
            if (_festival)
            {
                float want = (now.Hour >= 17 && now.Hour < 23 ? 0.45f : 0.06f) * (1f - 0.6f * Rest.Calm) * amb;
                _festival.volume = Mathf.MoveTowards(_festival.volume, want, Time.deltaTime * 0.1f);
            }
            // 音量（座って休んでいる間は町のざわめきを小さく）と、遠くの音を止める
            _cullT -= Time.deltaTime;
            bool cull = _cullT <= 0;
            if (cull) _cullT = 0.5f;
            foreach (var (s, vol, crowd) in _loops)
            {
                if (!s) continue;
                s.volume = vol * amb * (crowd ? 1f - 0.65f * Rest.Calm : 1f);
                if (!cull || _ear == null || s.spatialBlend < 0.5f) continue;
                bool far = (s.transform.position - _ear.position).sqrMagnitude > (s.maxDistance + 6f) * (s.maxDistance + 6f);
                if (far && s.isPlaying) s.Pause();
                else if (!far && !s.isPlaying) s.UnPause();
            }
            if (_bell) _bell.volume = 0.9f * amb;
            // 風鈴：ときどきチリン（近くの風鈴だけ）
            _chimeT -= Time.deltaTime;
            if (_chimeT <= 0 && _chimes.Length > 0)
            {
                _chimeT = Random.Range(0.6f, 2.2f);
                var s = _chimes[Random.Range(0, _chimes.Length)];
                if (s && (_ear == null || (s.transform.position - _ear.position).sqrMagnitude < 30f * 30f))
                { s.pitch = Random.Range(0.97f, 1.03f); s.PlayOneShot(_chimeClips[Random.Range(0, _chimeClips.Length)], Random.Range(0.4f, 1f) * amb); }
            }
            // 毎正時の鐘
            if (now.Hour != _lastHour && Ts.Shifting) _lastHour = now.Hour; // 時刻を動かしている間は鳴らさない
            if (now.Hour != _lastHour)
            {
                _lastHour = now.Hour;
                if (_bell) _bell.Play();
                Hud.Toast($"ゴーン…… {now.Hour}時の鐘が鳴りました");
            }
        }
    }
}
