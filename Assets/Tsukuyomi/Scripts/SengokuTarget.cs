using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU で攻撃が当たるもの（練習場のわら人形・あとで武者や櫓・大将落とし）。
    /// コライダーと同じ GameObject か親につける。team：0 中立（練習の的）・1 東・2 西。
    /// </summary>
    public class SengokuTarget : MonoBehaviour
    {
        public float maxHp = 300, hp = 300;
        public int team;
        /// <summary>建物（櫓・大将落とし）：大筒のダメージが2倍。</summary>
        public bool structure;
        /// <summary>倒れてから起き上がるまでの秒（練習の的）。0 なら起き上がらない。</summary>
        public float respawn = 3f;
        public System.Action<SengokuTarget, float, Vector3> onHit;
        /// <summary>倒れたとき（試合の記録用）。</summary>
        public System.Action<SengokuTarget> onDown;
        /// <summary>傾いたり倒れたりする見た目にするか（自分の体は false）。</summary>
        public bool visual = true;
        /// <summary>ダメージの数字を出さない（自分が受けたとき）。</summary>
        public bool quiet;
        public string label = "";
        /// <summary>前からの攻撃を防ぐ（傘を開いているとき）。0〜1 で減らす割合。</summary>
        public float frontGuard;
        public Vector3 facing = Vector3.forward;

        public static readonly List<SengokuTarget> All = new List<SengokuTarget>();
        public bool Down => hp <= 0;

        float _downT, _flash;
        Vector3 _kick;
        Quaternion _rest;
        Renderer[] _rends;
        Vector3 _home;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Start()
        {
            _rest = transform.localRotation;
            _home = transform.position;
            _rends = GetComponentsInChildren<Renderer>();
        }

        /// <summary>ダメージを受ける。dir は押される向き。実際に減った量を返す。</summary>
        /// <summary>近接攻撃でしかダメージを受けない（大将落とし）。</summary>
        public bool meleeOnly;

        public float Hit(float dmg, Vector3 point, Vector3 dir, bool heavy = false, bool melee = false)
        {
            if (Down) return 0;
            if (meleeOnly && !melee) { Sengoku.I?.ShowText(point, "効かない", new Color(0.75f, 0.75f, 0.8f)); return 0; }
            if (structure && heavy) dmg *= 2f;
            if (frontGuard > 0 && Vector3.Dot(dir.normalized, facing) < -0.3f) dmg *= 1f - frontGuard;
            hp = Mathf.Max(0, hp - dmg);
            _flash = 0.12f;
            _kick += dir.normalized * (heavy ? 18f : 8f);
            onHit?.Invoke(this, dmg, point);
            if (!quiet) Sengoku.I?.ShowDamage(point, dmg, Down);
            if (Down) { _downT = respawn; onDown?.Invoke(this); }
            return dmg;
        }

        /// <summary>押し飛ばす（鎖鎌で引き寄せる・ハンマーで吹き飛ばすなど）。練習の的は少し動くだけ。</summary>
        public void Shove(Vector3 v) => _kick += v * 2f;

        /// <summary>起こす（試合で復活するとき）。</summary>
        public void Revive() { hp = maxHp; _kick = Vector3.zero; if (visual) transform.localRotation = _rest; }

        void Update()
        {
            if (!visual) return;
            float dt = Time.deltaTime;
            _kick = Vector3.Lerp(_kick, Vector3.zero, 1 - Mathf.Exp(-6f * dt));
            var tilt = Quaternion.Euler(_kick.z * 1.5f, 0, -_kick.x * 1.5f);
            if (Down)
            {
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(-85, transform.localEulerAngles.y, 0), 1 - Mathf.Exp(-8f * dt));
                if (respawn > 0) { _downT -= dt; if (_downT <= 0) { hp = maxHp; transform.localRotation = _rest; } }
            }
            else transform.localRotation = tilt * _rest;
            if (_rends != null && _flash > 0)
            {
                _flash -= dt;
                foreach (var r in _rends) if (r != null) r.enabled = _flash <= 0 || Mathf.Repeat(_flash * 40f, 2f) < 1.4f;
            }
        }
    }
}
