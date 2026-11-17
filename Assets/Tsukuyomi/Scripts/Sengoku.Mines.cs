using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// 雷の団扇の地雷（2026-11-18 ユーザーの指示）。
    ///  ・右クリックで足もとの少し前に置く。1人3つまで（4つ目を置くと、いちばん古いのが消える）。
    ///  ・100 ダメージで壊れる（攻撃・爆発で壊せる）。
    ///  ・相手の地雷は 2m まで近づかないと見えない（自分の組の地雷はいつも見える）。
    ///  ・相手の組が踏むと：50 ダメージ＋15m 打ち上げ＋3秒動けない（攻撃はできる）。
    /// </summary>
    public partial class Sengoku
    {
        class Mine { public SengokuTarget t; public int team; public object owner; public Transform tr; public Renderer[] rends; public float age; }
        readonly List<Mine> _mines = new List<Mine>();
        const int MaxMines = 3;
        const float MineSee = 2f, MineTrigger = 0.95f, MineDmg = 50f, MineLift = 15f, MineStun = 3f;

        /// <summary>自分が動けない残り時間（地雷を踏んだ）。</summary>
        float _stunT;

        int MinesOf(int team, object owner = null)
        {
            int n = 0;
            foreach (var m in _mines) if (m.team == team && (owner == null ? m.owner == null : m.owner == owner)) n++;
            return n;
        }

        /// <summary>足もとの少し前の地面。</summary>
        Vector3 FeetAhead(float dist)
        {
            var p = _rig.transform.position + Forward * dist + Vector3.up * 1.5f;
            return Physics.Raycast(p, Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore) && h.collider != _cc ? h.point : _rig.transform.position + Forward * dist;
        }

        /// <summary>地雷を置く。owner は置いた武者（自分なら null）。</summary>
        bool PlaceMine(Vector3 at, int team, object owner = null)
        {
            // 1人3つまで：多ければいちばん古いのを消す
            if (MinesOf(team, owner) >= MaxMines)
                for (int i = 0; i < _mines.Count; i++)
                    if (_mines[i].team == team && _mines[i].owner == owner) { RemoveMine(i, false); break; }
            var root = new GameObject("地雷");
            root.transform.SetParent(transform, false);
            root.transform.position = at + Vector3.up * 0.06f;
            var col = root.AddComponent<SphereCollider>(); col.radius = 0.32f; col.center = new Vector3(0, 0.08f, 0);
            var t = root.AddComponent<SengokuTarget>();
            t.team = team; t.maxHp = t.hp = 100f; t.visual = false; t.respawn = 99999f; t.label = "地雷";
            var dark = new Color(0.15f, 0.13f, 0.18f);
            var glow = team == 1 ? Board.Teal : new Color(1f, 0.35f, 0.3f);
            Block(PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(0.55f, 0.05f, 0.55f), dark, false);
            Block(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.06f, 0), new Vector3(0.3f, 0.03f, 0.3f), new Color(1f, 0.85f, 0.3f), false);
            Block(PrimitiveType.Sphere, root.transform, new Vector3(0, 0.1f, 0), Vector3.one * 0.1f, glow, false, null, true);
            var m = new Mine { t = t, team = team, owner = owner, tr = root.transform, rends = root.GetComponentsInChildren<Renderer>() };
            foreach (var r in m.rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mines.Add(m);
            if (Ts.Flag("--sengoku-log")) Debug.Log($"[SengokuMine] t={300 - _clock:0} 置いた team={team} at={at:F0} 数={_mines.Count}");
            if (team == 1 || Vector3.Distance(at, EyePos) < 20f) Play(_sClick, 0.5f);
            return true;
        }

        void RemoveMine(int i, bool boom)
        {
            var m = _mines[i];
            if (boom && m.tr != null) Sparks(m.tr.position + Vector3.up * 0.2f, new Color(1f, 0.9f, 0.4f), 16);
            if (m.tr != null) Destroy(m.tr.gameObject);
            _mines.RemoveAt(i);
        }

        void ClearMines() { for (int i = _mines.Count - 1; i >= 0; i--) RemoveMine(i, false); }

        /// <summary>毎フレーム：壊れた地雷を消す・見え方・踏んだかどうか。</summary>
        void MinesTick(float dt)
        {
            if (_stunT > 0) _stunT -= dt;
            var me = _rig != null ? _rig.transform.position : Vector3.zero;
            for (int i = _mines.Count - 1; i >= 0; i--)
            {
                var m = _mines[i];
                if (m.tr == null) { _mines.RemoveAt(i); continue; }
                if (m.t.Down) { RemoveMine(i, true); if (Vector3.Distance(m.tr.position, EyePos) < 25f) Play(_sBoom, 0.4f); continue; }
                m.age += dt;
                var mp = m.tr.position;
                // 見え方：自分の組の地雷はいつも。相手の地雷は 2m まで近づかないと見えない
                bool see = m.team == 1 || Vector3.Distance(me, mp) < MineSee;
                foreach (var r in m.rends) if (r != null && r.enabled != see) r.enabled = see;
                if (m.age < 0.5f) continue; // 置いた直後は踏んでも動かない
                // 踏んだか：自分
                if (m.team != 1 && !_dead && Stepped(me, mp)) { TriggerMe(); RemoveMine(i, true); continue; }
                // 武者
                bool hit = false;
                foreach (var u in _units)
                {
                    if (u.team == m.team || u.Dead || !u.go.activeSelf || !Stepped(u.Pos, mp)) continue;
                    u.target.Hit(MineDmg, mp + Vector3.up * 0.5f, Vector3.up, true);
                    if (u.skill != SkNone) EndSkill(u);
                    u.vel = new Vector3(0, Lift(), 0); u.stunT = MineStun;
                    hit = true;
                    if (Ts.Flag("--sengoku-log")) Debug.Log($"[SengokuMine] t={300 - _clock:0} {u.name} が踏んだ hp={u.target.hp:0}");
                    break;
                }
                if (hit) { RemoveMine(i, true); Play(_sBoom, 0.5f); }
            }
        }

        static bool Stepped(Vector3 who, Vector3 mine)
        {
            var d = who - mine; float dy = d.y; d.y = 0;
            return d.magnitude < MineTrigger && dy > -0.6f && dy < 1.2f;
        }

        /// <summary>15m 打ち上げる上向きの速さ（重力 Gravity で、高さ h まで上がる速さ √(2gh)）。</summary>
        static float Lift() => Mathf.Sqrt(2f * Gravity * MineLift);

        void TriggerMe()
        {
            _me.Hit(MineDmg, _rig.transform.position + Vector3.up * 0.5f, Vector3.up, true);
            EndAbilities();
            _vel = new Vector3(_vel.x * 0.2f, Lift(), _vel.z * 0.2f);
            _grounded = false;
            _stunT = MineStun;
            Kick(8f);
            Play(_sBoom, 0.8f);
            Center("地雷！", new Color(1f, 0.45f, 0.35f), 1.2f, "3秒間、動けない");
            Vr.Buzz(true, 1f, 0.3f); Vr.Buzz(false, 1f, 0.3f);
        }
    }
}
