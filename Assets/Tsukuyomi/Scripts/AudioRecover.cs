using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 音の出口が変わったとき（VR に切りかえて Quest のヘッドホンになった・イヤホンを抜いた など）に、音をもとにもどす（2026-11-18）。
    /// Unity は出口が変わると音の仕組みを作りなおす。そのとき、プログラムで作った音（Synth・ライブの曲。このゲームの音のほぼ全部）は
    /// データが消えて二度と鳴らなくなる（確かめた：GetData が失敗し、Play しても鳴らない。新しく作った音は鳴る）。
    /// そこで、作った音のデータを控えておき（Keep）、作りなおされたら同じ音を作りなおして、
    /// 音源（AudioSource）と、音を持っている変数（Tsukuyomi のクラスの AudioClip・配列・リスト）を新しい音に差しかえ、
    /// 鳴っていた音を同じ所から鳴らしなおす。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AudioRecover : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("音の出口の見張り");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioRecover>();
        }

        // ───────── 作った音の控え ─────────

        static readonly Dictionary<AudioClip, float[]> Kept = new Dictionary<AudioClip, float[]>();

        /// <summary>プログラムで作った音（1チャンネル）のデータを控える（Synth・LiveShow から）。</summary>
        public static void Keep(AudioClip clip, float[] data)
        {
            if (clip == null || data == null) return;
            if (Kept.Count > 64) Prune();
            Kept[clip] = data;
        }

        static void Prune()
        {
            var dead = new List<AudioClip>();
            foreach (var k in Kept.Keys) if (k == null) dead.Add(k);
            foreach (var k in dead) Kept.Remove(k);
        }

        // ───────── 見張り ─────────

        static AudioRecover _i;
        readonly List<(AudioSource src, float time, float at)> _playing = new List<(AudioSource, float, float)>();
        float _snapT, _busyT;
        bool _pending;

        /// <summary>音を作りなおしている途中（ライブはこの間、曲が止まっていても次の曲へ進まない）。</summary>
        public static bool Busy => _i != null && (_i._pending || _i._busyT > 0);

        void Awake() => _i = this;
        void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnChanged;
        void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnChanged;

        // 音の仕組みの中から呼ばれることがあるので、ここでは印だけ。次の Update で作りなおす
        void OnChanged(bool deviceWasChanged) { _pending = true; _busyT = 1f; }

        void Update()
        {
            if (_busyT > 0) _busyT -= Time.unscaledDeltaTime;
            if (_pending) { _pending = false; Rebuild(); _snapT = 0; return; }
            // 1秒ごとに「いま鳴っている音と、その再生位置」を覚えておく
            _snapT -= Time.unscaledDeltaTime;
            if (_snapT > 0) return;
            _snapT = 1f;
            _playing.Clear();
            float now = Time.unscaledTime;
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && s.clip != null) _playing.Add((s, s.time, now));
        }

        void Rebuild()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SoundPrefs.Apply();
            // 1) 控えておいたデータから、同じ音を作りなおす
            Prune();
            var map = new Dictionary<AudioClip, AudioClip>();
            foreach (var kv in new List<KeyValuePair<AudioClip, float[]>>(Kept))
            {
                var old = kv.Key; var data = kv.Value;
                var nc = AudioClip.Create(old.name, data.Length, 1, old.frequency > 0 ? old.frequency : 44100, false);
                nc.SetData(data, 0);
                map[old] = nc;
                Kept.Remove(old);
                Kept[nc] = data;
            }
            // 2) 音源と、音を持っている変数を差しかえる
            int swapped = 0;
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.clip != null && map.TryGetValue(s.clip, out var nc)) { s.clip = nc; swapped++; }
            int fields = SwapFields(map);
            // 3) 鳴っていた音を、同じ所から鳴らしなおす
            float now = Time.unscaledTime;
            int played = 0;
            foreach (var (s, time, at) in _playing)
            {
                if (s == null || !s.isActiveAndEnabled || s.clip == null || s.isPlaying) continue;
                float t = time + (now - at) * Mathf.Abs(s.pitch); // 覚えた時からたった分だけ先へ（ライブの曲が、ヤチヨの動きとずれないように）
                if (s.loop) t = Mathf.Repeat(t, s.clip.length);
                else if (t >= s.clip.length - 0.05f) continue;    // もう終わっていたはずの音
                s.time = Mathf.Clamp(t, 0, s.clip.length - 0.01f);
                s.Play();
                if (s.isPlaying) played++;
            }
            foreach (var old in map.Keys) if (old != null) Destroy(old); // 古い音はすてる
            Debug.Log($"[Audio] 音の出口が変わったので音を作りなおしました：音 {map.Count}・音源 {swapped}・変数 {fields}・鳴らしなおし {played}/{_playing.Count}（{sw.ElapsedMilliseconds}ms）");
        }

        /// <summary>Tsukuyomi のクラスの、AudioClip を持つ変数（ふつうの変数・static・配列・リスト）を差しかえる。</summary>
        static int SwapFields(Dictionary<AudioClip, AudioClip> map)
        {
            if (map.Count == 0) return 0;
            int n = 0;
            const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            const BindingFlags Stat = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var t in typeof(AudioRecover).Assembly.GetTypes())
            {
                if (t.Namespace != "Tsukuyomi" || t.IsGenericTypeDefinition) continue;
                foreach (var f in t.GetFields(Stat)) if (Holds(f.FieldType)) n += SwapField(f, null, map);
            }
            var cache = new Dictionary<Type, List<FieldInfo>>();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Namespace != "Tsukuyomi") continue;
                if (!cache.TryGetValue(t, out var list))
                {
                    list = new List<FieldInfo>();
                    for (var tt = t; tt != null && tt != typeof(MonoBehaviour); tt = tt.BaseType)
                        foreach (var f in tt.GetFields(Inst)) if (Holds(f.FieldType)) list.Add(f);
                    cache[t] = list;
                }
                foreach (var f in list) n += SwapField(f, mb, map);
            }
            return n;
        }

        static bool Holds(Type t) => t == typeof(AudioClip) || t == typeof(AudioClip[]) || t == typeof(List<AudioClip>);

        static int SwapField(FieldInfo f, object owner, Dictionary<AudioClip, AudioClip> map)
        {
            try
            {
                var v = f.GetValue(owner);
                if (v is AudioClip c) { if (c != null && map.TryGetValue(c, out var nc)) { f.SetValue(owner, nc); return 1; } return 0; }
                int n = 0;
                if (v is AudioClip[] arr) { for (int i = 0; i < arr.Length; i++) if (arr[i] != null && map.TryGetValue(arr[i], out var nc)) { arr[i] = nc; n++; } }
                else if (v is List<AudioClip> l) { for (int i = 0; i < l.Count; i++) if (l[i] != null && map.TryGetValue(l[i], out var nc)) { l[i] = nc; n++; } }
                return n;
            }
            catch { return 0; }
        }
    }
}
