using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>人形の動き：歩くと腕をふって弾む。dance で踊る（配信・ライブ用）。</summary>
    public class DollAnimator : MonoBehaviour
    {
        public Transform body, head, armL, armR;
        public float speed;
        public bool dance;
        /// <summary>座っている（ソファ・縁台・長椅子）。</summary>
        public bool sit;
        public float danceTempo = 2.1f;
        /// <summary>右手を決まった形にする（0〜1。食べる・傘をさすなど）。</summary>
        public float holdR;
        public Vector3 holdREuler = new Vector3(-120, 0, -25);

        float _phase, _seed;

        void Awake() => _seed = Random.value * 10f;

        void Update()
        {
            if (body == null) return;
            float t = Time.time + _seed;
            if (dance)
            {
                float b = t * Mathf.PI * danceTempo;
                body.localPosition = new Vector3(Mathf.Sin(b * 0.5f) * 0.06f, Mathf.Abs(Mathf.Sin(b)) * 0.09f, 0);
                body.localRotation = Quaternion.Euler(0, Mathf.Sin(b * 0.25f) * 25f, Mathf.Sin(b * 0.5f) * 7f);
                if (armL) armL.localRotation = Quaternion.Euler(Mathf.Sin(b) * 20f, 0, -150f + Mathf.Sin(b) * 25f);
                if (armR) armR.localRotation = Quaternion.Euler(-Mathf.Sin(b) * 20f, 0, 40f + Mathf.Cos(b * 0.5f) * 50f);
                if (head) head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(b * 0.5f) * 10f);
                return;
            }
            if (sit)
            {
                // 座っている：少しうしろへもたれて、手はひざの上
                body.localPosition = new Vector3(0, Mathf.Sin(t * 0.9f) * 0.006f, 0);
                body.localRotation = Quaternion.Euler(-5f, 0, 0);
                if (armL) armL.localRotation = Quaternion.Euler(-38f, 0, 12f);
                if (armR) armR.localRotation = Quaternion.Euler(-38f, 0, -12f);
                if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.21f) * 4f, Mathf.Sin(t * 0.13f) * 18f, 0);
                return;
            }
            bool moving = speed > 0.1f;
            if (moving) _phase += Time.deltaTime * (5f + speed * 0.9f);
            float k = Mathf.Clamp01(speed / 5f);
            float bob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * 0.06f : Mathf.Sin(t * 2f) * 0.008f;
            body.localPosition = new Vector3(0, bob, 0);
            body.localRotation = Quaternion.Euler(moving ? 6f * k : 0, 0, 0);
            float sw = moving ? Mathf.Sin(_phase) * 40f * Mathf.Max(0.4f, k) : Mathf.Sin(t * 1.3f) * 3f;
            if (armL) armL.localRotation = Quaternion.Euler(sw, 0, -8f);
            if (armR) armR.localRotation = Quaternion.Euler(-sw, 0, 8f);
            if (armR && holdR > 0) armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(holdREuler), holdR);

            if (head) head.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.4f) * 4f, 0);
        }
    }
}
