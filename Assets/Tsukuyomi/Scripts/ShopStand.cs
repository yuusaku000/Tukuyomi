using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 月見商店街のお店（組み立てのときに置く）。店先（stand）に立って E で入る。
    /// kind: 服（gofuku）・飾り（komono）・食べ物（kanmi）。
    /// </summary>
    public class ShopStand : MonoBehaviour
    {
        public string id = "";
        public string title = "";
        public string english = "";
        public ItemKind kind;
        public Vector3 stand;      // 店先（ここに立つ）
        public float standYaw;     // 店先で店のほうを向く向き
        public Vector3 sign;       // AR の看板を出す高さ
        public Vector3 keeper;     // 店の人
        public float keeperYaw;
        public string hello = "いらっしゃいませ";

        public static readonly List<ShopStand> All = new List<ShopStand>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static ShopStand Find(string id) => All.Find(s => s.id == id);

        public static ShopStand Near(Vector3 p, float r)
        {
            ShopStand best = null; float bd = r;
            foreach (var s in All)
            {
                var d = s.stand - p; d.y *= 0.5f;
                float m = d.magnitude;
                if (m < bd) { bd = m; best = s; }
            }
            return best;
        }
    }
}
