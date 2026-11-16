using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>釣り堀の「大物番付」の立て札（組み立てのときに置く）。前で E → 番付を見る。</summary>
    public class RankBoard : MonoBehaviour
    {
        public Vector3 stand;   // 前に立つ所
        public Vector3 sign;    // AR の看板を出す所

        public static readonly List<RankBoard> All = new List<RankBoard>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static RankBoard Near(Vector3 p, float r)
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
