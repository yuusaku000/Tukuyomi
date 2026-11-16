using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 大画面広場の「ヤチヨの配信」。地下の小さなスタジオをカメラで映し、広場の大画面に流す。
    /// ヤチヨが踊り、コメントが右から左へ流れ、同時接続数が少しずつ変わる。
    /// </summary>
    public class StreamStudio : MonoBehaviour
    {
        public Camera cam;
        public DollAnimator yachiyo;
        public Transform mendako;

        static readonly string[] Comments =
        {
            "ヤチヨ〜！", "ヤオヨロ〜！", "かわいい", "888888", "初見です", "勉強しながら見てる",
            "今日もがんばる", "月きれい", "FUSHIいる？", "いろPいる？", "こんばんは〜", "神配信",
            "ダンスうま", "ふじゅ〜送ります", "ツクヨミから見てます", "鳥居のところにいる", "桜の丘いった？",
            "釣れた！", "作業用BGMにしてる", "ヤチヨちゃんの声すき", "メンダコかわいい", "おつかれさま",
            "8000年ぶりに来た", "サカバンバスピスどこ", "ライブいつ？", "推しが今日も尊い", "わこつ",
        };

        readonly List<(Transform t, float speed)> _flying = new List<(Transform, float)>();
        TextMesh _title, _viewers, _live;
        float _next, _viewerCount = 8000f, _viewerTimer;
        Font _font;

        void Start()
        {
            _font = Ts.Font;
            if (yachiyo != null) yachiyo.dance = true;
            _live = MakeText("● LIVE", new Vector3(-1.62f, 2.32f, -1.4f), 0.016f, new Color(1f, 0.35f, 0.45f), TextAnchor.UpperLeft);
            _viewers = MakeText("", new Vector3(1.62f, 2.32f, -1.4f), 0.014f, Color.white, TextAnchor.UpperRight);
            _title = MakeText("", new Vector3(-1.62f, 0.62f, -1.4f), 0.017f, Color.white, TextAnchor.LowerLeft);
        }

        TextMesh MakeText(string s, Vector3 local, float size, Color c, TextAnchor anchor)
        {
            var g = new GameObject("Text");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = local;
            var tm = g.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = anchor;
            tm.color = c;
            tm.text = s;
            g.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            return tm;
        }

        void Update()
        {
            float t = Time.time;
            if (mendako != null)
                mendako.localPosition = new Vector3(0.75f + Mathf.Sin(t * 0.9f) * 0.12f, 1.55f + Mathf.Sin(t * 1.7f) * 0.1f, 0.3f);

            _next -= Time.deltaTime;
            if (_next <= 0)
            {
                _next = Random.Range(0.35f, 1.2f);
                var tm = MakeText(Comments[Random.Range(0, Comments.Length)],
                    new Vector3(2.0f, Random.Range(0.85f, 2.05f), -1.5f), 0.012f,
                    Random.value < 0.15f ? new Color(1f, 0.85f, 0.4f) : Color.white, TextAnchor.MiddleLeft);
                _flying.Add((tm.transform, Random.Range(0.55f, 0.9f)));
            }
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var (tr, sp) = _flying[i];
                tr.localPosition += Vector3.left * sp * Time.deltaTime;
                if (tr.localPosition.x < -4.5f) { Destroy(tr.gameObject); _flying.RemoveAt(i); }
            }

            _viewerTimer -= Time.deltaTime;
            if (_viewerTimer <= 0)
            {
                _viewerTimer = 1.5f;
                _viewerCount = Mathf.Clamp(_viewerCount + Random.Range(-60f, 90f), 6000f, 30000f);
                _viewers.text = $"同時接続 {Mathf.RoundToInt(_viewerCount):N0}";
                var now = Ts.Now;
                _title.text = now.Hour < 10 ? "【朝配信】おはヤチヨ！ いっしょに一日はじめよ〜"
                    : now.Hour < 17 ? "【作業雑談】ツクヨミからまったり配信中♪"
                    : "【夜配信】月がきれいだね〜 ゆるっと雑談";
            }
            _live.color = new Color(1f, 0.35f, 0.45f, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 2f)));
        }
    }
}
