using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// ツクヨミの音：海の波・滝と川・商店街のざわめき・風鈴・遠くの祭囃子（夕方から）・毎正時の鐘。
    /// 音はすべて Synth でその場で作る。
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        public Transform[] waterPoints, crowdPoints, chimePoints;
        public Transform festivalPoint, bellPoint;

        AudioSource _sea, _festival, _bell;
        AudioSource[] _chimes;
        readonly System.Collections.Generic.List<AudioSource> _crowd = new System.Collections.Generic.List<AudioSource>();
        AudioClip[] _chimeClips;
        int _lastHour = -1;
        float _chimeT;

        public static bool Muted
        {
            get => AudioListener.volume < 0.01f;
            set => AudioListener.volume = value ? 0f : 1f;
        }

        AudioSource Src(Vector3 pos, AudioClip clip, float vol, float maxDist, bool loop, bool spatial = true)
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
            if (loop) { s.time = Random.Range(0f, clip.length * 0.9f); s.Play(); }
            return s;
        }

        void Start()
        {
            if (Ts.ShotMode) { enabled = false; return; }
            var sea = Synth.Water(12f, 0.05f);
            _sea = Src(Vector3.zero, sea, 0.18f, 1, true, false);
            var falls = Synth.Water(9f, 0.35f);
            foreach (var p in waterPoints) if (p) Src(p.position, falls, 0.55f, 45f, true);
            var crowd = Synth.Crowd(14f);
            foreach (var p in crowdPoints) if (p) _crowd.Add(Src(p.position, crowd, 0.3f, 40f, true));
            _chimeClips = new[] { Synth.Chime(1760f), Synth.Chime(2093f), Synth.Chime(2349f), Synth.Chime(2637f) };
            _chimes = new AudioSource[chimePoints.Length];
            for (int i = 0; i < chimePoints.Length; i++) if (chimePoints[i]) _chimes[i] = Src(chimePoints[i].position, _chimeClips[0], 0.4f, 28f, false);
            if (festivalPoint) _festival = Src(festivalPoint.position, Synth.Festival(), 0.0f, 120f, true);
            if (bellPoint) _bell = Src(bellPoint.position, Synth.Bell(), 1f, 600f, false);
            if (_bell) _bell.minDistance = 30f;
            _lastHour = Ts.Now.Hour;
        }

        void Update()
        {
            var now = Ts.Now;
            // 夕方から夜にかけて祭囃子
            if (_festival)
            {
                float want = (now.Hour >= 17 && now.Hour < 23 ? 0.55f : 0.08f) * (1f - 0.6f * Rest.Calm);
                _festival.volume = Mathf.MoveTowards(_festival.volume, want, Time.deltaTime * 0.1f);
            }
            // 座って休んでいる間は、町のざわめきを小さく
            foreach (var c in _crowd) if (c) c.volume = 0.3f * (1f - 0.65f * Rest.Calm);
            // 風鈴：ときどきチリン
            _chimeT -= Time.deltaTime;
            if (_chimeT <= 0 && _chimes.Length > 0)
            {
                _chimeT = Random.Range(0.6f, 2.2f);
                var s = _chimes[Random.Range(0, _chimes.Length)];
                if (s) { s.pitch = Random.Range(0.97f, 1.03f); s.PlayOneShot(_chimeClips[Random.Range(0, _chimeClips.Length)], Random.Range(0.4f, 1f)); }
            }
            // 毎正時の鐘
            if (now.Hour != _lastHour)
            {
                _lastHour = now.Hour;
                if (_bell) _bell.Play();
                Hud.Toast($"ゴーン…… {now.Hour}時の鐘が鳴りました");
            }
        }
    }
}
