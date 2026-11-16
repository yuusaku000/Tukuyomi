using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 空にときどき現れるめずらしいもの（数分に1回、どれか1つ）：
    ///   光るクジラ・大きなクラゲの群れ・光の竜宮の使い（長いリボンのような魚）・彗星／流れ星／流星群（夜）
    /// 形はその場で基本の形から作り、横切り終わったら消す。--sky whale|jelly|oarfish|meteor で確認用にすぐ出せる。
    /// </summary>
    public class RareSky : MonoBehaviour
    {
        public Material whaleMat, jellyMat, jellyMat2, oarMat, starMat;
        public Vector3 center = new Vector3(0, 0, 150);
        public float minWait = 150f, maxWait = 320f;

        float _wait;
        Transform _cur;
        float _t, _len;
        string _kind;
        readonly List<Transform> _parts = new List<Transform>();
        readonly List<Vector3> _seeds = new List<Vector3>();
        Vector3 _from, _to;

        void Start()
        {
            _wait = Random.Range(40f, 120f);
            var force = Ts.Arg("--sky");
            if (force != null) { _wait = 0; _kind = force; }
        }

        GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        void Update()
        {
            if (_cur == null)
            {
                _wait -= Time.deltaTime;
                if (_wait > 0) return;
                var want = _kind;
                _kind = null;
                Spawn(want);
                return;
            }
            _t += Time.deltaTime;
            float k = _t / _len;
            if (k >= 1f) { Destroy(_cur.gameObject); _cur = null; _parts.Clear(); _seeds.Clear(); _wait = Random.Range(minWait, maxWait); return; }
            Animate(k, Time.time);
        }

        /// <summary>島の上を横切る道（入って出る）。</summary>
        void Path(float h, float span)
        {
            float a = Random.Range(0f, Mathf.PI * 2);
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = new Vector3(-d.z, 0, d.x) * Random.Range(-60f, 60f);
            _from = center + side - d * span + Vector3.up * h;
            _to = center + side + d * span + Vector3.up * h;
        }

        void Spawn(string kind)
        {
            float h = Ts.Now.Hour;
            bool night = h >= 18 || h < 5;
            if (kind == null)
            {
                float r = Random.value;
                kind = r < 0.32f ? "whale" : r < 0.56f ? "jelly" : r < 0.8f ? "oarfish" : "meteor";
                if (kind == "meteor" && !night) kind = "whale";
            }
            _cur = new GameObject("めずらしい空の生き物").transform;
            _cur.SetParent(transform, false);
            _t = 0; _kind = kind;
            switch (kind)
            {
                case "jelly": Jelly(); break;
                case "oarfish": Oarfish(); break;
                case "meteor": Meteor(); break;
                default: Whale(); break;
            }
            Hud.Toast(kind switch
            {
                "jelly" => "空に、大きなクラゲの群れがただよっています…",
                "oarfish" => "空を、光の竜宮の使いが泳いでいく…！",
                "meteor" => "流れ星！ 空を見上げて…",
                _ => "空に、光るクジラが…！",
            });
            _kind = kind;
        }

        void Whale()
        {
            _len = 95f;
            Path(Random.Range(95f, 120f), 330f);
            var w = _cur;
            Part(w, PrimitiveType.Sphere, Vector3.zero, new Vector3(9f, 8f, 30f), whaleMat);
            Part(w, PrimitiveType.Sphere, new Vector3(0, -1.6f, 9f), new Vector3(8f, 5.5f, 12f), whaleMat);
            var tail = new GameObject("尾").transform; tail.SetParent(w, false); tail.localPosition = new Vector3(0, 0, -14f);
            Part(tail, PrimitiveType.Sphere, new Vector3(0, 0, -3f), new Vector3(3f, 2f, 7f), whaleMat);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(tail, PrimitiveType.Sphere, new Vector3(s * 4f, 0, -6.5f), new Vector3(8f, 0.6f, 3.5f), whaleMat, new Vector3(0, s * 25f, 0));
                Part(w, PrimitiveType.Sphere, new Vector3(s * 5f, -3f, 5f), new Vector3(7f, 0.6f, 3f), whaleMat, new Vector3(0, s * -30f, s * 20f));
            }
            for (int i = 0; i < 14; i++)
                Part(w, PrimitiveType.Sphere, new Vector3(Random.Range(-4f, 4f), Random.Range(-3f, 3.5f), Random.Range(-12f, 12f)), Vector3.one * Random.Range(0.5f, 1.2f), starMat);
            _parts.Add(tail);
        }

        void Jelly()
        {
            _len = 120f;
            Path(Random.Range(70f, 95f), 220f);
            for (int i = 0; i < 9; i++)
            {
                var j = new GameObject("クラゲ").transform; j.SetParent(_cur, false);
                j.localPosition = new Vector3(Random.Range(-28f, 28f), Random.Range(-10f, 12f), Random.Range(-28f, 28f));
                float s = Random.Range(2.2f, 4.5f);
                var m = i % 3 == 0 ? jellyMat2 : jellyMat;
                Part(j, PrimitiveType.Sphere, Vector3.zero, new Vector3(2f, 1.2f, 2f) * s, m);
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3;
                    Part(j, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.55f * s, -1.8f * s, Mathf.Sin(a) * 0.55f * s), new Vector3(0.08f, 3.2f, 0.08f) * s, m);
                }
                _parts.Add(j);
                _seeds.Add(new Vector3(Random.value * 10f, s, j.localPosition.y));
            }
        }

        void Oarfish()
        {
            _len = 80f;
            Path(Random.Range(60f, 85f), 260f);
            const int n = 34;
            for (int i = 0; i < n; i++)
            {
                var seg = new GameObject("体").transform; seg.SetParent(_cur, false);
                float w = Mathf.Lerp(1.4f, 0.25f, i / (float)n);
                Part(seg, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, w * 1.6f, 1.9f), oarMat);
                if (i % 2 == 0) Part(seg, PrimitiveType.Cube, new Vector3(0, w * 0.9f + 0.4f, 0), new Vector3(0.05f, 0.9f, 1.2f), starMat); // 赤く光る背びれのかわり
                if (i == 0)
                    for (int k = 0; k < 4; k++) Part(seg, PrimitiveType.Cube, new Vector3(0, 1.4f + k * 0.4f, 0.8f - k * 0.5f), new Vector3(0.06f, 1.6f, 0.06f), starMat, new Vector3(-30 - k * 12, 0, 0));
                _parts.Add(seg);
            }
        }

        void Meteor()
        {
            _len = 22f;
            _from = center + Vector3.up * 160f;
            // 流星群：いくつもの光の線が、ばらばらの時刻に流れる。ひとつは大きな彗星
            for (int i = 0; i < 18; i++)
            {
                var m = new GameObject("流れ星").transform; m.SetParent(_cur, false);
                float s = i == 0 ? 4f : Random.Range(0.6f, 1.4f);
                Part(m, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.2f * s, starMat);
                Part(m, PrimitiveType.Cube, new Vector3(0, 0, -9f * s), new Vector3(0.35f, 0.35f, 18f) * s, starMat);
                m.gameObject.SetActive(false);
                _parts.Add(m);
                var start = new Vector3(Random.Range(-250f, 250f), Random.Range(150f, 220f), Random.Range(0f, 400f));
                _seeds.Add(start);
                _seeds.Add(new Vector3(i == 0 ? 0f : Random.Range(0f, 18f), i == 0 ? 20f : Random.Range(1.2f, 2.5f), 0)); // 始まる時刻・続く時間
            }
        }

        void Animate(float k, float t)
        {
            switch (_kind)
            {
                case "jelly":
                {
                    _cur.position = Vector3.Lerp(_from, _to, k) + Vector3.up * (k * 25f);
                    for (int i = 0; i < _parts.Count; i++)
                    {
                        var sd = _seeds[i];
                        float pulse = Mathf.Sin(t * 1.3f + sd.x);
                        _parts[i].localScale = new Vector3(1f + pulse * 0.12f, 1f - pulse * 0.15f, 1f + pulse * 0.12f);
                        var lp = _parts[i].localPosition; lp.y = sd.z + Mathf.Sin(t * 0.5f + sd.x) * 3f; _parts[i].localPosition = lp;
                    }
                    break;
                }
                case "oarfish":
                {
                    var dir = (_to - _from).normalized;
                    var side = Vector3.Cross(Vector3.up, dir);
                    for (int i = 0; i < _parts.Count; i++)
                    {
                        float kk = Mathf.Clamp01(k - i * 0.0045f);
                        var p = Vector3.Lerp(_from, _to, kk) + side * Mathf.Sin(t * 2.2f - i * 0.38f) * 3.2f + Vector3.up * Mathf.Sin(t * 1.1f - i * 0.2f) * 2f;
                        var next = Vector3.Lerp(_from, _to, kk + 0.001f) + side * Mathf.Sin(t * 2.2f - (i - 0.6f) * 0.38f) * 3.2f;
                        _parts[i].position = p;
                        var fwd = next - p;
                        if (fwd.sqrMagnitude > 1e-6f) _parts[i].rotation = Quaternion.LookRotation(fwd);
                    }
                    break;
                }
                case "meteor":
                {
                    float now = _t;
                    for (int i = 0; i < _parts.Count; i++)
                    {
                        var start = _seeds[i * 2]; var tm = _seeds[i * 2 + 1];
                        float u = (now - tm.x) / tm.y;
                        bool on = u >= 0 && u <= 1;
                        if (_parts[i].gameObject.activeSelf != on) _parts[i].gameObject.SetActive(on);
                        if (!on) continue;
                        var dir = new Vector3(-0.8f, -0.45f, -0.4f).normalized;
                        _parts[i].position = center + start + dir * (u * (i == 0 ? 120f : 160f));
                        _parts[i].rotation = Quaternion.LookRotation(dir);
                    }
                    break;
                }
                default:
                {
                    var p = Vector3.Lerp(_from, _to, k) + Vector3.up * Mathf.Sin(t * 0.25f) * 5f;
                    _cur.position = p;
                    _cur.rotation = Quaternion.LookRotation(_to - _from) * Quaternion.Euler(Mathf.Sin(t * 0.3f) * 4f, 0, Mathf.Sin(t * 0.2f) * 6f);
                    if (_parts.Count > 0) _parts[0].localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 18f, 0, 0);
                    break;
                }
            }
        }
    }
}
