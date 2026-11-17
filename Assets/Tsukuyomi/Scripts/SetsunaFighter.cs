using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>攻撃の高さ。上段はしゃがむとかわせる、下段はしゃがんでガード、中段はどちらでもガード、空中から（Overhead）は立ってガード。</summary>
    public enum Height { High, Mid, Low, Overhead, Throw }

    /// <summary>技の決まり（秒・ダメージ・届く距離など）。</summary>
    public class SetsunaMove
    {
        public string id, name;
        public float startup, active, recovery;
        public int dmg;
        public float reach = 1.3f;
        public Height h = Height.Mid;
        public float hitstun = 0.35f, blockstun = 0.22f, push = 0.3f;
        public bool knockdown, launch, projectile, air, armorless;
        public float travel;          // 出ている間に前へ進む速さ（m/秒）
        public int hits = 1;          // 多段（超必殺）
        public float gauge = 6;       // 当てたときにたまる超必殺ゲージ
        public string[] cancel;       // つなげられる技
        public float invuln;          // 出始めの無敵（秒）
        public bool heavy => dmg >= 80;
    }

    /// <summary>技の一覧（みんな同じ技を持ち、style で強さと速さが変わる）。</summary>
    public static class SetsunaMoves
    {
        public static readonly SetsunaMove Jab = new() { id = "jab", name = "弱", startup = 0.07f, active = 0.06f, recovery = 0.14f, dmg = 35, reach = 1.25f, h = Height.High, hitstun = 0.34f, blockstun = 0.2f, push = 0.22f, cancel = new[] { "jab2", "heavy", "sp" } };
        public static readonly SetsunaMove Jab2 = new() { id = "jab2", name = "弱・二", startup = 0.08f, active = 0.06f, recovery = 0.16f, dmg = 35, reach = 1.3f, h = Height.Mid, hitstun = 0.36f, blockstun = 0.2f, push = 0.25f, cancel = new[] { "heavy", "sp" } };
        public static readonly SetsunaMove Heavy = new() { id = "heavy", name = "強", startup = 0.15f, active = 0.08f, recovery = 0.3f, dmg = 85, reach = 1.5f, h = Height.Mid, hitstun = 0.46f, blockstun = 0.28f, push = 0.55f, gauge = 9, cancel = new[] { "sp" } };
        public static readonly SetsunaMove CLight = new() { id = "clight", name = "しゃがみ弱", startup = 0.07f, active = 0.06f, recovery = 0.14f, dmg = 25, reach = 1.2f, h = Height.Low, hitstun = 0.32f, blockstun = 0.2f, push = 0.2f, cancel = new[] { "clight", "cheavy", "sp" } };
        public static readonly SetsunaMove CHeavy = new() { id = "cheavy", name = "足払い", startup = 0.14f, active = 0.1f, recovery = 0.42f, dmg = 70, reach = 1.55f, h = Height.Low, knockdown = true, blockstun = 0.26f, push = 0.4f, gauge = 8 };
        public static readonly SetsunaMove JLight = new() { id = "jlight", name = "飛び弱", startup = 0.06f, active = 0.22f, recovery = 0.05f, dmg = 40, reach = 1.4f, h = Height.Overhead, hitstun = 0.42f, blockstun = 0.22f, air = true, cancel = new string[0] };
        public static readonly SetsunaMove JHeavy = new() { id = "jheavy", name = "飛び強", startup = 0.1f, active = 0.16f, recovery = 0.05f, dmg = 75, reach = 1.55f, h = Height.Overhead, hitstun = 0.52f, blockstun = 0.28f, air = true, gauge = 9 };
        public static readonly SetsunaMove Rush = new() { id = "rush", name = "裂空突", startup = 0.18f, active = 0.24f, recovery = 0.36f, dmg = 120, reach = 1.35f, h = Height.Mid, hitstun = 0.6f, blockstun = 0.3f, push = 1.4f, travel = 7.5f, gauge = 12 };
        public static readonly SetsunaMove Fox = new() { id = "fox", name = "狐火", startup = 0.22f, active = 0.05f, recovery = 0.36f, dmg = 70, reach = 30f, h = Height.Mid, hitstun = 0.45f, blockstun = 0.26f, push = 0.6f, projectile = true, gauge = 10 };
        public static readonly SetsunaMove Upper = new() { id = "upper", name = "月輪昇", startup = 0.06f, active = 0.18f, recovery = 0.5f, dmg = 100, reach = 1.4f, h = Height.Mid, launch = true, invuln = 0.12f, gauge = 12 };
        public static readonly SetsunaMove Grab = new() { id = "throw", name = "投げ", startup = 0.06f, active = 0.06f, recovery = 0.42f, dmg = 120, reach = 1.05f, h = Height.Throw, knockdown = true, gauge = 10 };
        public static readonly SetsunaMove Super = new() { id = "super", name = "刹那・月華乱舞", startup = 0.28f, active = 0.9f, recovery = 0.5f, dmg = 340, reach = 2.2f, h = Height.Mid, hits = 8, launch = true, travel = 3f, invuln = 0.4f, gauge = 0 };
    }

    /// <summary>戦い方のちがい（速さ・強さ・体力・見た目の色・使う技）。</summary>
    public class SetsunaStyle
    {
        public string name, kana, desc;
        public float speed = 1, power = 1;
        public int hp = 1000;
        public DollLook look;
        public bool fox = true;
    }

    public static class SetsunaStyles
    {
        static Color C(float r, float g, float b) => new(r, g, b);

        public static readonly SetsunaStyle[] Cpu =
        {
            new() { name = "疾風", kana = "はやて", desc = "とにかく速い。\n踏みこみと連打に注意。", speed = 1.25f, power = 0.85f, hp = 900, fox = false,
                look = new DollLook { skin = C(1f, 0.86f, 0.76f), hair = C(0.2f, 0.45f, 0.4f), robe = C(0.12f, 0.35f, 0.32f), robe2 = C(0.9f, 0.9f, 0.85f), sleeve = C(0.15f, 0.42f, 0.38f), cuff = C(0.95f, 0.8f, 0.3f), sash = C(0.95f, 0.8f, 0.3f), eye = C(0.2f, 0.5f, 0.4f), accent = C(0.95f, 0.8f, 0.3f), ponytail = true } },
            new() { name = "岩鉄", kana = "がんてつ", desc = "重くて硬い。\n一撃がとても痛い。", speed = 0.8f, power = 1.3f, hp = 1200, fox = false,
                look = new DollLook { skin = C(0.92f, 0.74f, 0.6f), hair = C(0.12f, 0.1f, 0.1f), robe = C(0.32f, 0.3f, 0.34f), robe2 = C(0.55f, 0.18f, 0.12f), sleeve = C(0.36f, 0.34f, 0.38f), cuff = C(0.55f, 0.18f, 0.12f), sash = C(0.1f, 0.1f, 0.1f), eye = C(0.3f, 0.2f, 0.15f), accent = C(0.8f, 0.6f, 0.2f) } },
            new() { name = "朧", kana = "おぼろ", desc = "狐火を撃つ技巧派。\n刹那ガードも使う。", speed = 1.0f, power = 1.0f, hp = 1000, fox = true,
                look = new DollLook { skin = C(1f, 0.9f, 0.86f), hair = C(0.78f, 0.75f, 0.95f), robe = C(0.25f, 0.18f, 0.42f), robe2 = C(0.95f, 0.75f, 0.9f), sleeve = C(0.3f, 0.22f, 0.5f), cuff = C(0.4f, 0.95f, 1f), sash = C(0.4f, 0.95f, 1f), eye = C(0.5f, 0.3f, 0.9f), accent = C(0.4f, 0.95f, 1f), longHair = true } },
        };

        public static readonly SetsunaStyle Player = new() { name = "あなた", kana = "あなた", speed = 1f, power = 1f, hp = 1000, fox = true };
    }

    /// <summary>1人の闘士（自分も相手も同じしくみ）。位置は闘技場の床の上（y は跳んだ高さ）。</summary>
    public class SetsunaFighter
    {
        public enum St { Idle, Attack, Hitstun, Blockstun, Air, Juggle, Down, Dash, Backstep, KO }

        public SetsunaStyle style;
        public bool isPlayer;
        public Vector3 pos;            // 床の上の位置（y=0）
        public float y, vy;            // 高さと上下の速さ
        public Vector3 airVel;         // 跳んでいる間の横の速さ
        public float hp, shownHp, gauge;
        public St st = St.Idle;
        public float t;                // いまの状態になってからの時間
        public float stun;             // 残りのひるみ時間
        public SetsunaMove move;
        public bool moveHit;           // この技はもう当たった
        public int superHits;
        public bool crouch, guard, airAttackUsed;
        public float lastGuardPress = -9;
        public int juggle;
        public float invuln;
        public bool jgReady;           // コンピューターの刹那ガード
        public Vector3 face = Vector3.forward;
        public int wins;

        // そのフレームの入力（自分はキー、相手はコンピューターが決める）
        public Vector2 inMove;
        public bool inLight, inHeavy, inSpecial, inThrow, inSuper, inJump, inCrouch, inDashF, inDashB;
        public int inSpecialKind; // 0 月輪昇・1 裂空突・2 狐火

        public bool Airborne => y > 0.02f || st == St.Air || st == St.Juggle;
        public bool CanAct => st == St.Idle || st == St.Dash;
        public float Phase => move == null ? 0 : t < move.startup ? 0 : t < move.startup + move.active ? 1 : 2;

        public void ResetRound(Vector3 p, Vector3 f)
        {
            pos = p; face = f; y = vy = 0; airVel = Vector3.zero;
            hp = shownHp = style.hp; st = St.Idle; t = 0; stun = 0; move = null;
            crouch = guard = false; juggle = 0; invuln = 0;
        }

        public void ClearInput()
        {
            inMove = Vector2.zero;
            inLight = inHeavy = inSpecial = inThrow = inSuper = inJump = inCrouch = inDashF = inDashB = false;
        }

        public void StartMove(SetsunaMove m)
        {
            move = m; st = St.Attack; t = 0; moveHit = false; superHits = 0; stun = 0;
            if (m.invuln > 0) invuln = m.invuln;
            if (m == SetsunaMoves.Super) gauge = 0;
        }

        /// <summary>技の途中で、ほかの技につなげられる時か（当たったあと）。</summary>
        public bool CanCancel() => st == St.Attack && move != null && moveHit && move.cancel != null && move.cancel.Length > 0;

        /// <summary>今の技からつなげられるか（当たったかガードされたあと、または出ている間）。</summary>
        public bool CanCancelInto(string id)
        {
            if (st != St.Attack || move == null || move.cancel == null || !moveHit) return false;
            foreach (var c in move.cancel) if (c == id || (c == "sp" && (id == "rush" || id == "upper" || id == "fox" || id == "super"))) return true;
            return false;
        }
    }
}
