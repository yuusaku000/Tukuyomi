using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>遊覧船の切符売り場（海の桟橋と、頂上の月見台）。前で E → 乗る（ふじゅ〜を払う）。</summary>
    public class ShipBooth : MonoBehaviour
    {
        public Vector3 stand;
        public Vector3 sign;
        public bool atLookout;

        public static readonly List<ShipBooth> All = new List<ShipBooth>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static ShipBooth Near(Vector3 p, float r)
        {
            foreach (var b in All)
            {
                var d = b.stand - p; d.y *= 0.5f;
                if (d.magnitude < r) return b;
            }
            return null;
        }
    }
}
