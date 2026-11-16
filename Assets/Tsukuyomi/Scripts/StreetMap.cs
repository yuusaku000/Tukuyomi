using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>通りの線（地図に道を描くため・町を歩く人の道すじ）。組み立てのときに書きこむ。</summary>
    public class StreetMap : MonoBehaviour
    {
        public Vector2[] points;
        public int[] counts;
        public float[] halves;
        public string[] names;
        public Vector2[] stairsBottom, stairsTop;

        public static StreetMap I { get; private set; }
        void Awake() => I = this;

        /// <summary>名前の通りの線（x, z, 道の半分の幅）。なければ null。</summary>
        public List<Vector3> Path(string name)
        {
            if (names == null) return null;
            int start = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (i < names.Length && names[i] == name)
                {
                    var l = new List<Vector3>(counts[i]);
                    for (int k = 0; k < counts[i]; k++) l.Add(new Vector3(points[start + k].x, points[start + k].y, halves[i]));
                    return l;
                }
                start += counts[i];
            }
            return null;
        }
    }
}
