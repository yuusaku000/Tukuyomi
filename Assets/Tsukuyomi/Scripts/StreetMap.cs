using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>通りの線（地図に道を描くため）。組み立てのときに書きこむ。</summary>
    public class StreetMap : MonoBehaviour
    {
        public Vector2[] points;
        public int[] counts;
        public float[] halves;
        public Vector2[] stairsBottom, stairsTop;

        public static StreetMap I { get; private set; }
        void Awake() => I = this;
    }
}
