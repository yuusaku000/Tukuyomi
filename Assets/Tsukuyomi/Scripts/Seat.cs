using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 座れる場所（ソファ・縁台・長椅子。組み立てのときに置く）。pos は座面の上の高さ（世界の座標）、yaw は座ったときに向く向き。
    /// bothSides なら背もたれがないので、近づいた側を向いて座る。
    /// </summary>
    public class Seat : MonoBehaviour
    {
        public Vector3[] spots = new Vector3[0];
        public float yaw;
        public bool bothSides;
        /// <summary>立つときはうしろへ出る（温泉のように、前が水のとき）。</summary>
        public bool standBehind;
        /// <summary>座ったときのお知らせ（空なら「座りました」）。</summary>
        public string sitText = "";

        public static readonly List<Seat> All = new List<Seat>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>いちばん近い座れる場所（r m 以内）。</summary>
        public static bool Near(Vector3 p, float r, out Seat seat, out int index)
        {
            seat = null; index = -1;
            float bd = r * r;
            foreach (var s in All)
                for (int i = 0; i < s.spots.Length; i++)
                {
                    var d = s.spots[i] - p;
                    if (Mathf.Abs(d.y) > 1.4f) continue;
                    d.y = 0;
                    if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; seat = s; index = i; }
                }
            return seat != null;
        }
    }
}
