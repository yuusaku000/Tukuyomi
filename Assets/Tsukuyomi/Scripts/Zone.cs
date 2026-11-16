using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>名前のついた場所（大鳥居・商店街…）。入ると名前が出る。地図とワープにも使う。回転はしない前提。</summary>
    public class Zone : MonoBehaviour
    {
        public string id = "";
        public string title = "";
        public string english = "";
        public Vector2 size = new Vector2(40, 40);
        public Color mapColor = new Color(0.6f, 0.6f, 0.6f);
        public bool warpable = true;
        public int order;
        public Vector3 warpPos;
        public float warpYaw;
        public float warpPitch = 14f;

        public void WarpTo(PlayerController player, OrbitCamera cam)
        {
            player.Teleport(warpPos, warpYaw);
            if (cam != null) { cam.pitch = warpPitch; cam.distance = 6f; cam.Snap(); }
        }

        public static readonly List<Zone> All = new List<Zone>();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public Rect Area => new Rect(transform.position.x - size.x / 2, transform.position.z - size.y / 2, size.x, size.y);

        public bool Contains(Vector3 p) => Area.Contains(new Vector2(p.x, p.z));

        public static Zone At(Vector3 p)
        {
            Zone best = null;
            float bestArea = float.MaxValue;
            foreach (var z in All)
            {
                if (!z.Contains(p)) continue;
                float a = z.size.x * z.size.y;
                if (a < bestArea) { best = z; bestArea = a; }
            }
            return best;
        }

        public static Zone Find(string id)
        {
            foreach (var z in All) if (z.id == id) return z;
            return null;
        }
    }
}
