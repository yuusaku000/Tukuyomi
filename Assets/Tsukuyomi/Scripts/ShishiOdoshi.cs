using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 鹿威し：竹の筒に水がたまってゆっくり傾き、水をこぼすと戻る。tube は支点のまわりで回す。
    /// 音（カコーン）は、高い「ポーン」に聞こえておかしかったので鳴らさない（2026-11-18 ユーザーの指示）。
    /// </summary>
    public class ShishiOdoshi : MonoBehaviour
    {
        public Transform tube;
        public float period = 11f;
        float _t;
        void Start()
        {
            _t = Random.Range(0f, period);
        }

        void Update()
        {
            if (tube == null) return;
            _t += Time.deltaTime;
            float fill = period - 1.2f;
            float ang;
            if (_t < fill) ang = Mathf.Lerp(-14f, 6f, Mathf.Pow(_t / fill, 2.2f));          // 水がたまって、先が下がっていく
            else if (_t < fill + 0.35f) ang = Mathf.Lerp(6f, 32f, (_t - fill) / 0.35f);       // 水がこぼれる
            else ang = Mathf.Lerp(32f, -14f, Mathf.Clamp01((_t - fill - 0.35f) / 0.18f));    // もどって石を打つ
            if (_t >= period) _t = 0;
            tube.localRotation = Quaternion.Euler(ang, 0, 0);
        }
    }
}
