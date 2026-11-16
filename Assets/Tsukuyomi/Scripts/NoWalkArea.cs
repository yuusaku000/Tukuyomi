using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>町の人が歩いて入らない所（温泉など、道の上に作った物）。向きは transform の y 回転。</summary>
    public class NoWalkArea : MonoBehaviour
    {
        public Vector2 size = new Vector2(10, 10);

        public static readonly List<NoWalkArea> All = new List<NoWalkArea>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static bool Inside(float x, float z)
        {
            foreach (var a in All)
            {
                var l = a.transform.InverseTransformPoint(new Vector3(x, a.transform.position.y, z));
                if (Mathf.Abs(l.x) < a.size.x / 2 && Mathf.Abs(l.z) < a.size.y / 2) return true;
            }
            return false;
        }
    }
}
