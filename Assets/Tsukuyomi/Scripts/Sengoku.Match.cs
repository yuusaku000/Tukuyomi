using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の試合の決まり。西軍（1：自分と味方2人）対 東軍（2：敵3人）。3ラウンドのうち2本先取。1ラウンド5分、1人2機。
    ///  櫓（北の山・南の湖）：まわりの円に自分の組だけがいると、6秒で占拠（人数が多いほど速い・敵がいると止まる）。
    ///  櫓を1つでも持つと、相手の城の二の丸に「大将落とし」が出る。5段を全部叩き落とすと天守閣が落ちて勝ち。
    ///  両方の櫓を60秒持ちつづけてもコールド勝ち。相手の残機を全部なくしても勝ち。時間切れは櫓の数→残りの人数でくらべる。
    /// </summary>
    public partial class Sengoku
    {
        enum MPhase { Select, Intro, Fight, RoundEnd, Result }

        bool _match;
        int _level = 1, _round, _winsW, _winsE, _myLives = 2;
        MPhase _phase = MPhase.Fight;
        float _phaseT, _clock = 300f, _coldT, _deadT, _hurt;
        bool _dead, _healing;
        SengokuTarget _me;

        class TowerState { public string name; public Vector3 c; public int owner; public float prog; public readonly List<Renderer> ring = new List<Renderer>(); }
        readonly TowerState[] _towers = { new TowerState { name = "北の櫓", c = NorthCapture }, new TowerState { name = "南の櫓", c = SouthCapture } };

        class DarumaState { public bool active; public SengokuTarget target; public Transform root; public readonly List<Transform> layers = new List<Transform>(); public int left = Layers; }
        const int Layers = 8;
        readonly DarumaState[] _daruma = { null, new DarumaState(), new DarumaState() };

        readonly List<string> _feed = new List<string>();

        const float HeadY = 0.45f + Layers * 0.9f + 0.65f;

        /// <summary>両方の櫓を持ちつづけて勝つまでの秒。</summary>
        const float ColdSec = 60f;

        int Owned(int team) { int n = 0; foreach (var t in _towers) if (t.owner == team) n++; return n; }

        // ───────── はじめる ─────────

        void InitMatch()
        {
            BuildBattlefield();
            _towers[0].c = NorthCapture; _towers[1].c = SouthCapture; // 舞台ごとの櫓の場所
            foreach (var t in _towers) CollectRing(t);
            BuildGraph();
            BuildDaruma(1); BuildDaruma(2);
            _me = _rig.AddComponent<SengokuTarget>();
            _me.team = 1; _me.maxHp = _me.hp = 100; _me.respawn = 0; _me.visual = false; _me.quiet = true; _me.label = "あなた";
            _me.onHit = (t, d, p) => { _hurt = Mathf.Min(1, _hurt + d / 40f); Kick(-1.5f); };
            _me.onDown = t => OnDown("あなた", 1);
            SpawnUnits();
            BuildGuards();
            // 同じ組どうし（味方の見張り・武者・自分）はぶつからない（道で足止めされないように）
            foreach (var g in _guards)
            {
                foreach (var u in _units) if (u.team == g.team) Physics.IgnoreCollision(g.cc, u.cc);
                if (g.team == 1) Physics.IgnoreCollision(g.cc, _cc);
            }
            BuildMatchHud();
            BuildMarkers();
            _round = 0;
            NextRound();
        }

        void CollectRing(TowerState t)
        {
            foreach (var r in _map.GetComponentsInChildren<Renderer>())
            {
                var p = r.transform.position;
                if (Mathf.Abs(p.y - (t.c.y + 0.06f * KY)) < 0.15f && Mathf.Abs(new Vector2(p.x - t.c.x, p.z - t.c.z).magnitude - CaptureR) < 0.4f) t.ring.Add(r);
            }
        }

        void BuildDaruma(int team)
        {
            var d = _daruma[team];
            d.root = new GameObject("大将落とし").transform; d.root.SetParent(transform, false);
            d.root.position = DarumaSpot(team);
            var cols = new[] { new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.5f, 0.85f), new Color(0.95f, 0.8f, 0.3f), new Color(0.3f, 0.7f, 0.4f), new Color(0.6f, 0.35f, 0.75f), new Color(0.95f, 0.55f, 0.2f), new Color(0.3f, 0.75f, 0.85f), new Color(0.9f, 0.45f, 0.65f) };
            for (int i = 0; i < Layers; i++)
            {
                var g = Block(PrimitiveType.Cylinder, d.root, new Vector3(0, 0.45f + i * 0.9f, 0), new Vector3(2.6f, 0.45f, 2.6f), cols[i], false);
                d.layers.Add(g.transform);
            }
            var head = new GameObject("だるま").transform; head.SetParent(d.root, false); head.localPosition = new Vector3(0, HeadY, 0);
            Block(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(2.2f, 2.4f, 2.2f), new Color(0.85f, 0.12f, 0.1f), false);
            Block(PrimitiveType.Sphere, head, new Vector3(0, 0.2f, 0.75f * (team == 1 ? 1 : 1)), new Vector3(1.2f, 1f, 0.8f), new Color(1f, 0.92f, 0.85f), false);
            foreach (float sx in new[] { -0.25f, 0.25f }) Block(PrimitiveType.Sphere, head, new Vector3(sx, 0.35f, 1.1f), Vector3.one * 0.18f, Color.black, false);
            Block(PrimitiveType.Sphere, head, new Vector3(0, 0, 0), new Vector3(2.4f, 0.2f, 2.4f), new Color(1f, 0.8f, 0.3f), false, null, true);
            head.rotation = Quaternion.LookRotation(team == 1 ? Vector3.right : Vector3.left);
            var col = d.root.gameObject.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 4.6f, 0); col.height = 9.4f; col.radius = 1.4f;
            d.target = d.root.gameObject.AddComponent<SengokuTarget>();
            d.target.team = team; d.target.structure = true; d.target.meleeOnly = true; d.target.maxHp = d.target.hp = Layers * 100; d.target.respawn = 0; d.target.visual = false; d.target.label = "大将落とし";
            d.target.onHit = (t, dmg, p) => DarumaHit(team);
            d.root.gameObject.SetActive(false);
        }

        void DarumaHit(int team)
        {
            var d = _daruma[team];
            int left = Mathf.CeilToInt(d.target.hp / 100f);
            while (d.left > left && d.left > 0)
            {
                // いちばん下の段が飛んでいき、上が1段落ちる
                var layer = d.layers[Layers - d.left];
                Sparks(layer.position, Color.white, 16);
                _flyers.Add((layer, (team == 1 ? Vector3.left : Vector3.right) * -14f + Vector3.up * 6f, 2f));
                layer.SetParent(transform, true);
                d.left--;
                foreach (var l in d.layers) if (l.parent == d.root) l.localPosition -= Vector3.up * 0.9f;
                d.root.Find("だるま").localPosition -= Vector3.up * 0.9f;
                Play(_sHeavy, 1f);
                Center($"大将落とし　あと {d.left} 段", team == 1 ? Board.Vermilion : Board.Teal, 1.6f, team == 1 ? "西の大将落としが叩かれている！" : "その調子！");
            }
            if (d.target.Down && _phase == MPhase.Fight) EndRound(team == 1 ? 2 : 1, team == 1 ? "西の天守閣が落ちた" : "東の天守閣が落ちた");
        }

        readonly List<(Transform t, Vector3 v, float life)> _flyers = new List<(Transform, Vector3, float)>();

        /// <summary>前のラウンドの名残を全部片づける（飛んでいる弾・飛んでいる段・光・移動わざ・櫓の色・武者の目標）。</summary>
        void ClearRoundLeftovers()
        {
            foreach (var s in _shots) if (s.tr) Destroy(s.tr.gameObject);
            _shots.Clear(); _boomer = null;
            foreach (var f in _fx) if (f.tr) Destroy(f.tr.gameObject);
            _fx.Clear();
            _flyers.Clear();
            EndAbilities();
            _vel = Vector3.zero; _fuel = 1; _cd = _cd2 = _abilityCd = 0; _charge = 0; _zoom = false; _dashT = 0; _swing = 0;
            _hurt = 0; _healing = false; _deadT = 0; _coldT = 0; _planT = 0; _ropeShow = 0; _flameT = 0;
            Time.timeScale = 1f;
            foreach (var t in _towers) { t.owner = 0; t.prog = 0; foreach (var r in t.ring) r.sharedMaterial = Mat(Color.white, true); }
            foreach (var u in _units) { u.cd = u.cd2 = 0; u.healing = false; u.enemy = null; u.task = 0; u.stuck = 0; u.attackPose = 0; u.vel = Vector3.zero; }
        }

        void NextRound()
        {
            _round++;
            ClearRoundLeftovers();
            _clock = 300f; _coldT = 0;
            for (int team = 1; team <= 2; team++)
            {
                var d = _daruma[team];
                d.target.Revive(); d.left = Layers;
                for (int i = 0; i < Layers; i++)
                {
                    var l = d.layers[i];
                    l.SetParent(d.root, false); l.localPosition = new Vector3(0, 0.45f + i * 0.9f, 0); l.localRotation = Quaternion.identity; l.gameObject.SetActive(true);
                }
                d.root.Find("だるま").localPosition = new Vector3(0, HeadY, 0);
                d.active = false; d.root.gameObject.SetActive(false);
            }
            foreach (var u in _units) { u.lives = 2; u.respawnT = 0; Respawn(u); }
            ResetGuards();
            _myLives = 2; _dead = false; _me.Revive(); _hp = 100;
            Teleport(SpawnOf(1, 0)); _yaw = 90; _pitch = 2;
            _phase = MPhase.Select; _phaseT = 0; _centerT = 0;
            _feed.Clear(); _log.Clear();
            OpenSelect();
        }

        // ───────── 毎フレーム ─────────

        void MatchTick(float dt)
        {
            _phaseT += dt;
            _hurt = Mathf.MoveTowards(_hurt, 0, dt * 1.5f);
            _hp = _me.hp;
            _healing = !_dead && InBase(1, _rig.transform.position) && _me.hp < _me.maxHp;
            if (_healing) _me.hp = Mathf.Min(_me.maxHp, _me.hp + 25f * dt);
            switch (_phase)
            {
                case MPhase.Select:
                    SelectTick(dt);
                    break;
                case MPhase.Intro:
                    if (_phaseT > 2.2f) { _phase = MPhase.Fight; _phaseT = 0; Center("出陣！", Board.Gold, 1f, ""); Play(_sBoom, 0.5f); }
                    break;
                case MPhase.Fight:
                    _clock -= dt;
                    Towers(dt);
                    Rules(dt);
                    break;
                case MPhase.RoundEnd:
                    if (_phaseT > 4f)
                    {
                        if (_winsW >= 2 || _winsE >= 2 || _round >= 3) ShowMatchResult(_winsW > _winsE);
                        else NextRound();
                    }
                    break;
            }
            // 自分がやられたとき
            if (_dead)
            {
                _deadT -= dt;
                if (_deadT <= 0 && _myLives > 0 && _phase == MPhase.Fight) { _dead = false; _me.Revive(); _hp = 100; Teleport(SpawnOf(1, 0)); _yaw = 90; Center("復活", Board.Teal, 1f, $"残り {_myLives} 機"); }
            }
            for (int i = _flyers.Count - 1; i >= 0; i--)
            {
                var (t, v, life) = _flyers[i];
                life -= dt; v += Vector3.down * 18f * dt;
                if (t) { t.position += v * dt; t.Rotate(400 * dt, 0, 200 * dt); }
                if (life <= 0) { if (t) t.gameObject.SetActive(false); _flyers.RemoveAt(i); } else _flyers[i] = (t, v, life);
            }
            if (_match) _feedT -= dt;
        }

        float _feedT;

        IEnumerable<(int team, Vector3 p)> Alive()
        {
            foreach (var u in _units) if (!u.Dead && u.go.activeSelf) yield return (u.team, u.Pos);
            if (!_dead && !_me.Down) yield return (1, _rig.transform.position);
        }

        void Towers(float dt)
        {
            foreach (var t in _towers)
            {
                int w = 0, e = 0;
                foreach (var (team, p) in Alive())
                {
                    var d = p - t.c;
                    if (new Vector2(d.x, d.z).magnitude < CaptureR && d.y > -2f && d.y < 7f) { if (team == 1) w++; else e++; }
                }
                int before = t.owner;
                if (w > 0 && e == 0) t.prog = Mathf.Min(1, t.prog + dt / 12f * Mathf.Min(2, w));
                else if (e > 0 && w == 0) t.prog = Mathf.Max(-1, t.prog - dt / 12f * Mathf.Min(2, e));
                if (t.prog >= 1) t.owner = 1; else if (t.prog <= -1) t.owner = 2;
                else if (t.owner == 1 && t.prog < 0) t.owner = 0; else if (t.owner == 2 && t.prog > 0) t.owner = 0;
                if (t.owner != before)
                {
                    var col = t.owner == 1 ? new Color(0.3f, 1f, 1f) : t.owner == 2 ? new Color(1f, 0.4f, 0.3f) : Color.white;
                    foreach (var r in t.ring) r.sharedMaterial = Mat(col, true);
                    if (t.owner != 0) { Center($"{(t.owner == 1 ? "西軍" : "東軍")}が{t.name}を占拠", t.owner == 1 ? Board.Teal : Board.Vermilion, 2f, ""); Feed($"{t.name}：{(t.owner == 1 ? "西軍" : "東軍")}が占拠"); Play(_sBoom, 0.4f); }
                }
            }
            // 大将落とし：相手が櫓を持っていると出る
            for (int team = 1; team <= 2; team++)
            {
                var d = _daruma[team];
                bool show = Owned(team == 1 ? 2 : 1) > 0 && !d.target.Down;
                if (show != d.active)
                {
                    d.active = show; d.root.gameObject.SetActive(show);
                    if (show) Center($"{(team == 1 ? "西" : "東")}の大将落としが出た！", team == 1 ? Board.Vermilion : Board.Teal, 2.2f, team == 1 ? "守れ！ 二の丸へもどれ" : "東の城の二の丸を叩け！");
                }
            }
        }

        void Rules(float dt)
        {
            // コールド勝ち（両方の櫓を30秒）
            int both = Owned(1) == 2 ? 1 : Owned(2) == 2 ? 2 : 0;
            if (both != 0) { _coldT += dt; if (_coldT >= ColdSec) { EndRound(both, "両方の櫓を守りきった（コールド勝ち）"); return; } }
            else _coldT = 0;
            // 全滅
            bool wAlive = _myLives > 0 || !_dead, eAlive = false;
            foreach (var u in _units)
            {
                bool alive = !u.Dead || u.lives > 0;
                if (u.team == 1 && alive) wAlive = true;
                if (u.team == 2 && alive) eAlive = true;
            }
            if (!eAlive) { EndRound(1, "東軍が全滅"); return; }
            if (!wAlive) { EndRound(2, "西軍が全滅"); return; }
            if (_clock <= 0)
            {
                _clock = 0;
                int ow = Owned(1), oe = Owned(2);
                if (ow != oe) { EndRound(ow > oe ? 1 : 2, "時間切れ（櫓の数）"); return; }
                int lw = (_dead ? 0 : 1) + _myLives, le = 0;
                foreach (var u in _units) { int n = u.lives + (u.Dead ? 0 : 1); if (u.team == 1) lw += n; else le += n; }
                EndRound(lw == le ? 0 : lw > le ? 1 : 2, "時間切れ（残りの人数）");
            }
        }

        void EndRound(int winner, string why)
        {
            if (_phase != MPhase.Fight) return;
            if (winner == 1) _winsW++; else if (winner == 2) _winsE++;
            _phase = MPhase.RoundEnd; _phaseT = 0;
            Center(winner == 0 ? "引き分け" : winner == 1 ? "西軍の勝ち" : "東軍の勝ち", winner == 1 ? Board.Teal : winner == 2 ? Board.Vermilion : Board.Gold, 4f, why);
            if (winner != 0)
            {
                var c = CastleOf(winner == 1 ? 2 : 1);
                if (why.Contains("天守閣")) for (int i = 0; i < 6; i++) Boom(c + new Vector3(Random.Range(-6f, 6f), 6 + i * 2, Random.Range(-6f, 6f)), 2.5f, new Color(1f, 0.6f, 0.25f));
            }
            Play(_sBoom, 0.9f);
        }

        void OnDown(string who, int team)
        {
            Feed($"{who}（{(team == 1 ? "西" : "東")}）が倒れた");
            if (who == "あなた")
            {
                _dead = true; _myLives--; _deadT = 5f; EndAbilities();
                Center("やられた", Board.Vermilion, 2f, _myLives > 0 ? $"5秒で復活（残り {_myLives} 機）" : "残機なし　―　味方を見守ろう");
            }
        }

        void Feed(string s) { IntelLog(s); _feed.Insert(0, s); if (_feed.Count > 4) _feed.RemoveAt(4); _feedT = 6f; }

        // ───────── 試合の表示 ─────────

        Text _mClock, _mRound, _mCenter, _mSub, _mFeed, _mDar, _mCold, _mLives;
        readonly Text[] _mTower = new Text[2];
        readonly RectTransform[] _mTowerBar = new RectTransform[2];
        readonly Image[] _mTowerImg = new Image[2], _mTowerFill = new Image[2];
        readonly List<(Text name, RectTransform hp, Text lives)> _mRows = new List<(Text, RectTransform, Text)>();
        readonly Image[] _mWins = new Image[4];
        Image _hurtImg;
        float _centerT;
        Board _resultB;

        void BuildMatchHud()
        {
            var r = _hud.root;
            _kill.text = "";
            _hurtImg = Board.Panel(r, new Rect(0, 0, 1920, 1080), new Color(0.8f, 0.05f, 0.05f, 0));
            _hurtImg.transform.SetAsFirstSibling();
            // 時計とラウンド
            var disc = Board.Panel(r, new Rect(900, 18, 120, 120), new Color(0.03f, 0.05f, 0.12f, 0.85f)); disc.sprite = Board.DiscSprite;
            Board.Ring(r, new Vector2(960, 78), 60, new Color(1, 1, 1, 0.9f), false);
            Board.Ring(r, new Vector2(960, 78), 50, new Color(Board.Teal.r, Board.Teal.g, Board.Teal.b, 0.8f));
            _mClock = Board.Display(r, new Rect(880, 46, 160, 64), "5:00", 44, Color.white);
            _mRound = Board.Label(r, new Rect(860, 140, 200, 26), "", 18, Color.white, TextAnchor.MiddleCenter);
            Board.Label(r, new Rect(760, 166, 400, 26), "Tab　戦況を見る", 17, new Color(Board.Teal.r, Board.Teal.g, Board.Teal.b, 0.85f), TextAnchor.MiddleCenter);
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2(i < 2 ? 870 - i * 26 : 1050 + (i - 2) * 26, 78);
                _mWins[i] = Board.Panel(r, new Rect(c.x - 8, c.y - 8, 16, 16), new Color(1, 1, 1, 0.2f)); _mWins[i].sprite = Board.DiscSprite;
            }
            // 櫓・両軍・大将落とし・記録は、ふだんは隠す（Tab のホログラムの戦況で見る）
            var info = new GameObject("ふだんは隠す一覧", typeof(RectTransform)).GetComponent<RectTransform>();
            info.SetParent(r, false); info.anchorMin = info.anchorMax = info.pivot = new Vector2(0, 1); info.anchoredPosition = Vector2.zero; info.sizeDelta = r.sizeDelta;
            info.gameObject.SetActive(false);
            // 櫓
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 760 : 1060;
                _mTowerImg[i] = Board.Panel(info, new Rect(x, 180, 100, 100), new Color(0.03f, 0.05f, 0.12f, 0.8f)); _mTowerImg[i].sprite = Board.DiscSprite;
                Board.Ring(info, new Vector2(x + 50, 230), 50, new Color(1, 1, 1, 0.8f), false);
                _mTowerFill[i] = Board.Panel(info, new Rect(x - 6, 174, 112, 112), Board.Teal);
                _mTowerFill[i].sprite = Board.ThickRingSprite; _mTowerFill[i].type = Image.Type.Filled; _mTowerFill[i].fillMethod = Image.FillMethod.Radial360; _mTowerFill[i].fillOrigin = 2;
                _mTower[i] = Board.Label(info, new Rect(x, 200, 100, 60), i == 0 ? "北" : "南", 34, Color.white, TextAnchor.MiddleCenter);
                _mTower[i].font = Board.Mincho;
                Board.Panel(info, new Rect(x, 290, 100, 6), new Color(0, 0, 0, 0.6f));
                _mTowerBar[i] = (RectTransform)Board.Panel(info, new Rect(x + 50, 290, 0, 6), Board.Teal).transform;
            }
            _mCold = Board.Label(info, new Rect(760, 300, 400, 30), "", 20, Board.Gold, TextAnchor.MiddleCenter);
            _mDar = Board.Label(info, new Rect(560, 330, 800, 34), "", 24, Color.white, TextAnchor.MiddleCenter);
            // 両軍の一覧
            Board.Display(info, new Rect(60, 150, 300, 50), "西軍", 36, Board.Teal, TextAnchor.MiddleLeft);
            Board.Display(info, new Rect(1560, 150, 300, 50), "東軍", 36, Board.Vermilion, TextAnchor.MiddleRight);
            for (int i = 0; i < 6; i++)
            {
                bool w = i < 3; float y = 205 + (i % 3) * 44;
                float x = w ? 60 : 1560;
                var name = Board.Label(info, new Rect(x, y, 300, 24), "", 19, Color.white, w ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                Board.Panel(info, new Rect(x, y + 26, 300, 8), new Color(0, 0, 0, 0.55f));
                var hp = (RectTransform)Board.Panel(info, new Rect(x, y + 26, 300, 8), w ? Board.Teal : Board.Vermilion).transform;
                if (!w) { hp.pivot = new Vector2(1, 1); hp.anchoredPosition = new Vector2(x + 300, -(y + 26)); }
                var lives = Board.Label(info, new Rect(x, y, 300, 24), "", 18, Board.Gold, w ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                _mRows.Add((name, hp, lives));
            }
            _mFeed = Board.Label(info, new Rect(1360, 350, 500, 120), "", 18, new Color(1, 1, 1, 0.85f), TextAnchor.UpperRight);
            _mLives = Board.Label(r, new Rect(120, 880, 400, 30), "", 20, Board.Gold);
            _mCenter = Board.Display(r, new Rect(0, 400, 1920, 160), "", 110, Board.Teal);
            _mSub = Board.Label(r, new Rect(0, 560, 1920, 44), "", 30, Color.white, TextAnchor.MiddleCenter);
        }

        void Center(string text, Color c, float sec, string sub)
        {
            if (_mCenter == null) return;
            _mCenter.font = Ascii(text) ? Board.DisplayFont : Ts.Font;
            _mCenter.text = text; _mCenter.color = c; _mSub.text = sub; _centerT = sec;
            foreach (var o in _mCenter.GetComponents<Outline>()) o.effectColor = new Color(c.r, c.g, c.b, o.effectColor.a);
        }

        static bool Ascii(string s) { foreach (var ch in s) if (ch > 127) return false; return true; }

        void MatchHudTick()
        {
            if (_mClock == null) return;
            float udt = Time.unscaledDeltaTime;
            int sec = Mathf.CeilToInt(_clock);
            _mClock.text = $"{sec / 60}:{sec % 60:00}";
            _mRound.text = _round == 3 ? "FINAL ROUND" : $"ROUND {_round}";
            for (int i = 0; i < 4; i++) _mWins[i].color = i < 2 ? (_winsW > i ? Board.Teal : new Color(1, 1, 1, 0.2f)) : (_winsE > i - 2 ? Board.Vermilion : new Color(1, 1, 1, 0.2f));
            for (int i = 0; i < 2; i++)
            {
                var t = _towers[i];
                _mTowerImg[i].color = t.owner == 1 ? new Color(0.05f, 0.35f, 0.38f, 0.9f) : t.owner == 2 ? new Color(0.45f, 0.1f, 0.08f, 0.9f) : new Color(0.03f, 0.05f, 0.12f, 0.8f);
                float w = Mathf.Abs(t.prog) * 50;
                _mTowerBar[i].sizeDelta = new Vector2(w, 6);
                _mTowerBar[i].pivot = new Vector2(t.prog >= 0 ? 0 : 1, 1);
                _mTowerBar[i].GetComponent<Image>().color = t.prog >= 0 ? Board.Teal : Board.Vermilion;
                _mTowerFill[i].fillAmount = Mathf.Abs(t.prog);
                _mTowerFill[i].fillClockwise = t.prog >= 0;
                _mTowerFill[i].color = t.prog >= 0 ? Board.Teal : Board.Vermilion;
            }
            int both = Owned(1) == 2 ? 1 : Owned(2) == 2 ? 2 : 0;
            _mCold.text = both != 0 ? $"{(both == 1 ? "西軍" : "東軍")}のコールド勝ちまで {Mathf.Max(0, ColdSec - _coldT):0} 秒" : "";
            var dE = _daruma[2]; var dW = _daruma[1];
            _mDar.text = (dE.active ? $"東の大将落とし　あと {dE.left} 段　" : "") + (dW.active ? $"　西の大将落とし　あと {dW.left} 段（守れ！）" : "");
            _mDar.color = dW.active ? new Color(1f, 0.6f, 0.5f) : Board.Teal;
            // 両軍
            int row = 0;
            void Row(string n, float hp, int lives, bool dead)
            {
                var (name, bar, lv) = _mRows[row++];
                name.text = dead && lives <= 0 ? n + "　退場" : dead ? n + "　復活待ち" : n; name.color = dead ? new Color(1, 1, 1, 0.35f) : Color.white;
                bar.sizeDelta = new Vector2(300 * Mathf.Clamp01(hp), 8);
                lv.text = new string('●', Mathf.Max(0, lives)) + new string('○', Mathf.Max(0, 2 - lives));
            }
            Row($"あなた　{Weapons[_w].name}", _dead ? 0 : _me.hp / 100f, _myLives, _dead);
            foreach (var u in _units) if (u.team == 1) Row($"{u.name}　{Weapons[u.weapon].name}", u.Dead ? 0 : u.target.hp / 100f, u.lives, u.Dead);
            foreach (var u in _units) if (u.team == 2) Row($"{Weapons[u.weapon].name}　{u.name}", u.Dead ? 0 : u.target.hp / 100f, u.lives, u.Dead);
            _mFeed.text = _feedT > 0 ? string.Join("\n", _feed) : "";
            _mLives.text = _dead ? (_myLives > 0 ? $"復活まで {Mathf.Max(0, _deadT):0.0} 秒" : "観戦中") : $"残機 {new string('●', _myLives)}" + (_healing ? "　本陣で回復中 ＋" : "");
            _centerT -= udt;
            float a = Mathf.Clamp01(_centerT / 0.4f);
            _mCenter.canvasRenderer.SetAlpha(a); _mSub.canvasRenderer.SetAlpha(a);
            _hurtImg.color = new Color(0.8f, 0.05f, 0.05f, _hurt * 0.35f + (_dead ? 0.25f : 0));
            MarkersTick();
        }

        /// <summary>自分がやられていて残機もないとき：生きている味方のうしろから見る。</summary>
        bool SpectateCam()
        {
            if (!_match || !_dead) return false;
            Unit ally = null;
            foreach (var u in _units) if (u.team == 1 && !u.Dead && u.go.activeSelf) { ally = u; break; }
            Vector3 pos, look;
            if (ally != null) { pos = ally.Pos - ally.go.transform.forward * 5f + Vector3.up * 3.2f; look = ally.Pos + Vector3.up * 1.4f; }
            else { pos = new Vector3(-30, 40, -60); look = Vector3.zero; }
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, pos, 1 - Mathf.Exp(-5f * Time.deltaTime));
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation(look - _cam.transform.position), 1 - Mathf.Exp(-5f * Time.deltaTime));
            if (_viewRoot) _viewRoot.gameObject.SetActive(false);
            return true;
        }

        void ShowMatchResult(bool win)
        {
            _phase = MPhase.Result;
            string got = Ts.ShotMode ? (win ? $"＋{Kassen.SengokuRewards[_level]:N0} ふじゅ〜" : "") : Kassen.RewardSengoku(win, _level);
            ResultBoard(win, got);
        }

        Text _rTitle, _rMsg;

        void ResultBoard(bool win, string got)
        {
            _paused = false; Time.timeScale = 1f;
            if (_resultB == null)
            {
                const float w = 960, h = 560;
                _resultB = Board.Create("SENGOKU の結果", new Vector2(w, h), 50);
                _resultB.worldOffset = new Vector3(0, 0, 1.3f); _resultB.Apply();
                var r = _resultB.root;
                Board.Frame(r, new Rect(0, 0, w, h));
                _rTitle = Board.Display(r, new Rect(0, 40, w, 150), "", 130, Board.Teal);
                _rMsg = Board.Label(r, new Rect(60, 210, w - 120, 150), "", 28, Color.white, TextAnchor.MiddleCenter);
                Board.NameTag(r, new Rect(w - 110, 60, 50, 200), "戦国", Board.Vermilion, true, 30);
                Board.ButtonNode(r, new Rect(w / 2 - 330, h - 130, 300, 70), "もう一度", 28, () => { _resultB.Alpha = 0; _winsW = _winsE = 0; _round = 0; NextRound(); });
                Board.ButtonNode(r, new Rect(w / 2 + 30, h - 130, 300, 70), "島へもどる", 28, () =>
                {
                    Hud.Toast(_winsW > _winsE ? $"SENGOKU に勝ちました　{_rMsg.text.Split('\n')[1]}" : "SENGOKU：また挑戦してね");
                    Leave();
                });
            }
            _rTitle.font = Ts.Font; _rTitle.text = win ? "勝利" : "敗北"; _rTitle.color = win ? Board.Teal : Board.Vermilion;
            _rMsg.text = $"西軍 {_winsW} − {_winsE} 東軍（{Kassen.Levels[_level]}）\n{got}";
            _resultB.Alpha = 1;
        }

        /// <summary>その場所の地面の上（確認用）。</summary>
        Vector3 GroundAt(Vector3 p) => Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore) ? h.point + Vector3.up * 0.3f : p;

        /// <summary>確認用（--tour の sengokum:場面）。start・fight・capture・daruma・result。</summary>
        public static void TestMatch(string pose)
        {
            var s = I;
            if (s == null || !s._match) return;
            if (pose.StartsWith("select")) { if (pose.Length > 6) s._pick = pose[6] - '0'; } else { s._pick = s._w; s.ConfirmPick(); }
            switch (pose)
            {
                case "fight":
                    s._phase = MPhase.Fight; s._centerT = 0;
                    break;
                case "intel":
                    s._phase = MPhase.Fight; s._centerT = 0; s._intelOpen = true; s.Feed("確認：北の櫓を西軍が占拠"); s.Feed("確認：疾風（東）が倒れた");
                    break;
                case "overview":
                    s._phase = MPhase.Fight; s._centerT = 0;
                    s._camOverride = true; s._camPos = Sx(new Vector3(-6, 78, -82)) + Vector3.up * 60f; s._camLook = Sx(new Vector3(0, -6, 10));
                    break;
                case "town": s._phase = MPhase.Fight; s._centerT = 0; s.Teleport(s.GroundAt(Sx(new Vector3(-30, 0.3f, 2)))); s._yaw = 90; s._pitch = 2; break;
                case "valley": s._phase = MPhase.Fight; s._centerT = 0; s.Teleport(s.GroundAt(Sx(new Vector3(-14, 0.3f, 18)))); s._yaw = 90; s._pitch = 8; break;
                case "lake": s._phase = MPhase.Fight; s._centerT = 0; s.Teleport(s.GroundAt(Sx(new Vector3(-30, 0.3f, -42)))); s._yaw = 95; s._pitch = 3; break;
                case "mountain": s._phase = MPhase.Fight; s._centerT = 0; s.Teleport(s.GroundAt(Sx(new Vector3(-36, 0.3f, 36)))); s._yaw = 70; s._pitch = 12; break;
                case "capture":
                    s._phase = MPhase.Fight; s._centerT = 0;
                    s.Teleport(NorthCapture + new Vector3(-3, 0.3f, -4)); s._yaw = 30; s._pitch = 5;
                    s._towers[0].prog = 0.6f;
                    foreach (var u in s._units) if (u.team == 1) { u.cc.enabled = false; u.go.transform.position = NorthCapture + new Vector3(u.role * 2f, 0.3f, -1); u.cc.enabled = true; }
                    break;
                case "daruma":
                    s._phase = MPhase.Fight; s._centerT = 0;
                    s._towers[0].owner = 1; s._towers[0].prog = 1;
                    s.Towers(0);
                    s._daruma[2].target.Hit(210, DarumaSpot(2), Vector3.right, false, true);
                    s.Teleport(DarumaSpot(2) + new Vector3(-9, 0.3f, -2)); s._yaw = 80; s._pitch = 8;
                    foreach (var u in s._units) if (u.team == 2) u.go.SetActive(false);
                    break;
                case "round2":
                    // 1ラウンド目に櫓を取り、大将落としを3段落とし、弾を飛ばしたまま次のラウンドへ → 名残が消えているかを見る
                    s._phase = MPhase.Fight;
                    s._towers[0].owner = 1; s._towers[0].prog = 1; s.Towers(0);
                    s._daruma[2].target.Hit(300, DarumaSpot(2), Vector3.right, false, true);
                    for (int i = 0; i < 4; i++) s.Fire(s.EyePos, Vector3.forward * 5f, 0, 10, 0, false, ShotKind.Spark);
                    s.EndRound(1, "確認");
                    s.NextRound(); s._pick = s._w; s.ConfirmPick(); s._centerT = 0;
                    s._phase = MPhase.Fight;
                    s._towers[0].owner = 1; s._towers[0].prog = 1; s.Towers(0); s._centerT = 0;
                    s.Teleport(DarumaSpot(2) + Sx(new Vector3(-9, 0.3f, -2))); s._yaw = 80; s._pitch = 10;
                    foreach (var u in s._units) if (u.team == 2) u.go.SetActive(false);
                    break;
                case "result":
                    s._winsW = 2; s._winsE = 1; s._round = 3;
                    s.ResultBoard(true, $"＋{Kassen.SengokuRewards[s._level]:N0} ふじゅ〜");
                    s._phase = MPhase.Result;
                    break;
            }
        }
    }
}
