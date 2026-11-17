using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の戦況（ホログラムの板）。試合中に Tab で、目の前の空間に「フィン」と開く（もう一度 Tab で閉じる）。
    ///  開き方：細い光の線が横にのび、つぎに縦に広がって板になる。青緑の半透明、走査線、四隅の扇、うっすらちらつく。
    ///  中身：上＝見出し・ラウンド・残り時間／左右＝両軍の全員（武器・体力・残機・状態）／まん中＝戦場の見取り図
    ///  （城・谷・山・湖・櫓の持ち主と占拠・味方と敵と自分の位置）／下＝大将落としの残り段数・コールド勝ち・出来事の記録。
    /// 開いていても動ける・戦える。出来事のお知らせ（櫓を取った等）は、ふだんどおり画面のまん中に出る。
    /// </summary>
    public partial class Sengoku
    {
        Board _intel;
        bool _intelOpen;
        float _intelT; // 0〜1 の開き具合
        const float IW = 1500, IH = 860, MapX = 470, MapY = 150, MapW = 560, MapH = 400;
        Text _iTime, _iRound, _iDarW, _iDarE, _iCold, _iLog, _iHint;
        readonly List<(Text name, Text info, RectTransform bar, Image barImg)> _iRows = new List<(Text, Text, RectTransform, Image)>();
        readonly List<Image> _iDots = new List<Image>();
        readonly Image[] _iTower = new Image[2], _iTowerFill = new Image[2];
        Image _iMe, _iFlicker, _iMePulse;
        RectTransform _iMeG, _iMeDir;
        readonly List<string> _log = new List<string>();
        static AudioClip _sFin;

        static readonly Color Holo = new(0.35f, 1f, 1f), HoloDim = new(0.35f, 1f, 1f, 0.25f), HoloBg = new(0.02f, 0.14f, 0.18f, 0.55f);

        void BuildIntel()
        {
            _intel = Board.Create("戦況（ホログラム）", new Vector2(IW, IH), 45);
            _intel.forceWorld = true; _intel.worldScale = 0.00098f; _intel.follow = 22f;
            _intel.worldOffset = new Vector3(0, -0.03f, 0.78f);
            _intel.Apply();
            var r = _intel.root;
            // 地・縁・走査線・四隅の扇
            Board.Panel(r, new Rect(0, 0, IW, IH), HoloBg);
            foreach (var (x, y, w, h) in new[] { (0f, 0f, IW, 2f), (0f, IH - 2, IW, 2f), (0f, 0f, 2f, IH), (IW - 2, 0f, 2f, IH) })
                Board.Panel(r, new Rect(x, y, w, h), new Color(Holo.r, Holo.g, Holo.b, 0.9f));
            for (float y = 6; y < IH; y += 8) Board.Panel(r, new Rect(2, y, IW - 4, 1), new Color(Holo.r, Holo.g, Holo.b, 0.05f));
            Board.Fans(r, new Rect(8, 8, IW - 16, IH - 16), new Color(Holo.r, Holo.g, Holo.b, 0.85f), 70);
            _iFlicker = Board.Panel(r, new Rect(0, 0, IW, IH), new Color(Holo.r, Holo.g, Holo.b, 0));
            // 見出し
            Board.Display(r, new Rect(60, 24, 700, 70), "BATTLE INTEL", 52, Holo, TextAnchor.MiddleLeft);
            var jp = Board.Label(r, new Rect(64, 92, 300, 40), "戦況", 32, Color.white); jp.font = Board.Mincho; jp.fontStyle = FontStyle.Bold;
            Board.Panel(r, new Rect(64, 134, 260, 2), Board.Vermilion);
            _iRound = Board.Label(r, new Rect(IW - 560, 30, 500, 34), "", 24, Board.Gold, TextAnchor.MiddleRight);
            _iTime = Board.Display(r, new Rect(IW - 560, 62, 500, 70), "", 54, Color.white, TextAnchor.MiddleRight);
            _iHint = Board.Label(r, new Rect(IW - 560, 132, 500, 26), "Tab で閉じる", 17, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight);

            // 両軍
            foreach (var (x, title, col) in new[] { (60f, "西軍", Board.Teal), (IW - 420f, "東軍", Board.Vermilion) })
            {
                Board.Display(r, new Rect(x, 160, 360, 50), title, 38, col, TextAnchor.MiddleLeft);
                Board.Panel(r, new Rect(x, 210, 360, 1.5f), new Color(col.r, col.g, col.b, 0.8f));
                for (int i = 0; i < 3; i++)
                {
                    float y = 226 + i * 104;
                    Board.Panel(r, new Rect(x, y, 360, 92), new Color(0.03f, 0.18f, 0.22f, 0.45f));
                    Board.Panel(r, new Rect(x, y, 4, 92), col);
                    var name = Board.Label(r, new Rect(x + 16, y + 6, 330, 34), "", 26, Color.white);
                    var info = Board.Label(r, new Rect(x + 16, y + 40, 330, 24), "", 18, new Color(1, 1, 1, 0.75f));
                    Board.Panel(r, new Rect(x + 16, y + 70, 328, 10), new Color(0, 0, 0, 0.5f));
                    var bar = Board.Panel(r, new Rect(x + 16, y + 70, 328, 10), col);
                    _iRows.Add((name, info, (RectTransform)bar.transform, bar));
                }
            }

            // 見取り図
            var mapRc = new Rect(MapX, MapY, MapW, MapH);
            Board.Panel(r, mapRc, new Color(0.02f, 0.1f, 0.12f, 0.6f));
            Board.Label(r, new Rect(MapX, MapY - 30, MapW, 26), "戦場の見取り図", 18, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);
            Board.Fans(r, mapRc, new Color(Holo.r, Holo.g, Holo.b, 0.6f), 30);
            void Area(Vector2 a, Vector2 b, Color c)
            {
                var p0 = MapPt(Sx(new Vector3(a.x, 0, a.y))); var p1 = MapPt(Sx(new Vector3(b.x, 0, b.y)));
                Board.Panel(r, new Rect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y)), c);
            }
            if (_theme == 1)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Area(new Vector2(s * 30, -58), new Vector2(s * 52, 58), new Color(0.25f, 0.45f, 0.25f, 0.45f));      // 段A
                    Area(new Vector2(s * 52, -58), new Vector2(s * 64, 58), new Color(0.35f, 0.55f, 0.32f, 0.5f));       // 段B
                    Area(new Vector2(s * 64, -26), new Vector2(s * 84, 26), s < 0 ? new Color(Board.Teal.r, Board.Teal.g, Board.Teal.b, 0.45f) : new Color(Board.Vermilion.r, Board.Vermilion.g, Board.Vermilion.b, 0.45f));
                }
                Area(new Vector2(-8, 32), new Vector2(8, 54), new Color(0.55f, 0.55f, 0.6f, 0.6f));   // 岩山
                Area(new Vector2(-7, -49), new Vector2(7, -35), new Color(0.45f, 0.6f, 0.35f, 0.6f)); // 大樹の台地
                for (int i = 0; i < River.Length - 1; i++) // くねる川（小さな四角を並べて）
                    for (int k = 0; k < 6; k++) { var q = Vector2.Lerp(River[i], River[i + 1], k / 6f); Area(q - Vector2.one * 2f, q + Vector2.one * 2f, new Color(0.3f, 0.6f, 0.8f, 0.6f)); }
            }
            else
            {
            Area(new Vector2(-9, -34), new Vector2(9, 34), new Color(0.05f, 0.05f, 0.08f, 0.8f));                 // 谷
            Area(new Vector2(-26, -58), new Vector2(26, -34), new Color(0.1f, 0.4f, 0.6f, 0.6f));                 // 湖
            Area(new Vector2(-22, 34), new Vector2(22, 58), new Color(0.5f, 0.55f, 0.6f, 0.45f));                 // 山
            Area(new Vector2(-84, -22), new Vector2(-48, 22), new Color(Board.Teal.r, Board.Teal.g, Board.Teal.b, 0.28f));       // 西の陣
            Area(new Vector2(48, -22), new Vector2(84, 22), new Color(Board.Vermilion.r, Board.Vermilion.g, Board.Vermilion.b, 0.28f)); // 東の陣
            foreach (var z in new[] { 18f, -18f }) Area(new Vector2(-10, z - 1), new Vector2(10, z + 1), new Color(0.8f, 0.6f, 0.4f, 0.8f)); // 吊り橋
            }
            for (int i = 0; i < 2; i++)
            {
                var c = MapPt(_towers[i].c);
                _iTower[i] = Board.Panel(r, new Rect(c.x - 16, c.y - 16, 32, 32), Color.white); _iTower[i].sprite = Board.RingSprite;
                _iTowerFill[i] = Board.Panel(r, new Rect(c.x - 19, c.y - 19, 38, 38), Board.Teal);
                _iTowerFill[i].sprite = Board.ThickRingSprite; _iTowerFill[i].type = Image.Type.Filled; _iTowerFill[i].fillMethod = Image.FillMethod.Radial360; _iTowerFill[i].fillOrigin = 2;
                Board.Label(r, new Rect(c.x - 40, c.y + (i == 0 ? -46 : 18), 80, 26), i == 0 ? "北の櫓" : "南の櫓", 16, Color.white, TextAnchor.MiddleCenter);
            }
            for (int i = 0; i < 6; i++) { var d = Board.Panel(r, new Rect(0, 0, 14, 14), Color.white); d.sprite = Board.DiscSprite; _iDots.Add(d); }
            // 自分の印：味方の点よりずっと大きく、桃色の丸・白い縁・広がる輪・向きの三角・「あなた」の名札（2026-11-18 見にくいとの指摘で大きく）
            _iMeG = Board.Group(r, "自分の印", new Rect(0, 0, 0, 0));
            _iMePulse = Board.Panel(_iMeG, new Rect(-24, -24, 48, 48), Board.Pink); _iMePulse.sprite = Board.RingSprite;
            var rim = Board.Panel(_iMeG, new Rect(-15, -15, 30, 30), Color.white); rim.sprite = Board.DiscSprite;
            _iMe = Board.Panel(_iMeG, new Rect(-12, -12, 24, 24), Board.Pink); _iMe.sprite = Board.DiscSprite;
            _iMeDir = Board.Group(_iMeG, "向き", new Rect(0, 0, 0, 0));
            var tri = Board.Panel(_iMeDir, new Rect(-7, -34, 14, 18), Color.white); tri.sprite = Board.DiscSprite;
            var tag = Board.Panel(_iMeG, new Rect(-44, 30, 88, 28), new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.95f));
            Board.Label(_iMeG, new Rect(-44, 30, 88, 28), "あなた", 18, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);

            // 下：大将落とし・コールド・記録
            _iDarW = Board.Label(r, new Rect(60, 570, 420, 34), "", 22, Board.Teal);
            _iDarE = Board.Label(r, new Rect(IW - 480, 570, 420, 34), "", 22, Board.Vermilion, TextAnchor.MiddleRight);
            _iCold = Board.Display(r, new Rect(MapX, MapY + MapH + 10, MapW, 40), "", 26, Board.Gold);
            Board.Panel(r, new Rect(60, 620, IW - 120, 1.5f), new Color(Holo.r, Holo.g, Holo.b, 0.5f));
            Board.Label(r, new Rect(60, 628, 300, 26), "出来事の記録", 18, new Color(1, 1, 1, 0.7f));
            _iLog = Board.Label(r, new Rect(60, 656, IW - 120, 190), "", 20, Color.white, TextAnchor.UpperLeft);
            _intel.Alpha = 0;
        }

        /// <summary>試合場の位置 → 見取り図の位置（板の左上からのピクセル）。北が上。</summary>
        Vector2 MapPt(Vector3 w)
        {
            float u = Mathf.InverseLerp(-84f * KX, 84f * KX, w.x), v = Mathf.InverseLerp(58f * KX, -58f * KX, w.z);
            return new Vector2(MapX + u * MapW, MapY + v * MapH);
        }

        /// <summary>出来事の記録（Feed と同じものを、板にも残す）。</summary>
        void IntelLog(string s) { _log.Insert(0, $"{300 - _clock:0}秒　{s}"); if (_log.Count > 7) _log.RemoveAt(7); }

        void IntelTick(float udt)
        {
            if (!_match) return;
            if (_intel == null) BuildIntel();
            bool can = _phase == MPhase.Fight || _phase == MPhase.Intro || _phase == MPhase.RoundEnd;
            if (!can && _intelOpen) _intelOpen = false;
            if (can && !_paused && !Ts.ShotMode && Act.Info.Down)
            {
                _intelOpen = !_intelOpen;
                if (_intelOpen) { _intel.Apply(); if (_sFin == null) _sFin = Synth.Chime(1320f); Play(_sFin, 0.5f); }
                else Play(_sClick, 0.4f);
            }
            _intelT = Mathf.MoveTowards(_intelT, _intelOpen ? 1 : 0, udt / (_intelOpen ? 0.3f : 0.16f));
            // フィン：まず横に光の線がのび（前半）、つぎに縦に広がる（後半）
            float sx = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_intelT * 2f)), sy = Mathf.Lerp(0.012f, 1f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(_intelT * 2f - 1f)));
            _intel.root.localScale = new Vector3(sx, sy, 1);
            _intel.Alpha = _intelT > 0.001f ? Mathf.Clamp01(_intelT * 3f) * (0.92f + 0.08f * Mathf.PerlinNoise(Time.unscaledTime * 12f, 0)) : 0;
            if (_iFlicker) _iFlicker.color = new Color(Holo.r, Holo.g, Holo.b, _intelT < 1 ? (1 - _intelT) * 0.25f : 0.02f * Mathf.PerlinNoise(0, Time.unscaledTime * 8f));
            if (_intelT <= 0) return;

            int sec = Mathf.CeilToInt(_clock);
            _iTime.text = $"{sec / 60}:{sec % 60:00}";
            _iRound.text = $"{StageNamesSg[_theme]}　{(_round == 3 ? "FINAL ROUND" : $"ROUND {_round}")}　西 {_winsW} − {_winsE} 東";
            // 両軍の一覧
            int row = 0;
            void Row(string n, string weapon, float hp, int lives, bool dead, bool heal, bool gone)
            {
                if (row >= _iRows.Count) return;
                var (name, info, bar, img) = _iRows[row++];
                name.text = n;
                string state = gone ? "退場" : dead ? "復活待ち" : heal ? "回復中" : "戦闘中";
                info.text = $"{weapon}　残機 {new string('●', Mathf.Max(0, lives))}{new string('○', Mathf.Max(0, 2 - lives))}　{state}";
                name.color = dead ? new Color(1, 1, 1, 0.4f) : Color.white;
                bar.sizeDelta = new Vector2(328 * Mathf.Clamp01(hp), 10);
            }
            Row("あなた", Weapons[_w].name, _dead ? 0 : _me.hp / 100f, _myLives, _dead, _healing, _dead && _myLives <= 0);
            foreach (var u in _units) if (u.team == 1) Row(u.name, Weapons[u.weapon].name, u.Dead ? 0 : u.target.hp / 100f, u.lives, u.Dead, u.healing, u.Dead && u.lives <= 0);
            foreach (var u in _units) if (u.team == 2) Row(u.name, Weapons[u.weapon].name, u.Dead ? 0 : u.target.hp / 100f, u.lives, u.Dead, u.healing, u.Dead && u.lives <= 0);
            // 見取り図
            for (int i = 0; i < 2; i++)
            {
                var t = _towers[i];
                _iTower[i].color = t.owner == 1 ? Board.Teal : t.owner == 2 ? Board.Vermilion : Color.white;
                _iTowerFill[i].fillAmount = Mathf.Abs(t.prog); _iTowerFill[i].fillClockwise = t.prog >= 0;
                _iTowerFill[i].color = t.prog >= 0 ? Board.Teal : Board.Vermilion;
            }
            for (int i = 0; i < _iDots.Count; i++)
            {
                var d = _iDots[i];
                if (i >= _units.Count) { d.enabled = false; continue; }
                var u = _units[i];
                d.enabled = !u.Dead && u.go.activeSelf;
                if (!d.enabled) continue;
                var mp = MapPt(u.Pos);
                ((RectTransform)d.transform).anchoredPosition = new Vector2(mp.x - 7, -(mp.y - 7));
                d.color = u.team == 1 ? Board.Teal : Board.Vermilion;
            }
            var me = MapPt(_rig.transform.position);
            _iMeG.anchoredPosition = new Vector2(me.x, -me.y);
            _iMeG.SetAsLastSibling(); // いちばん上に描く（櫓や味方の点に隠れない）
            float pulse = Mathf.Repeat(Time.unscaledTime * 1.2f, 1f);
            var pr = _iMePulse.rectTransform; float ps = 30 + pulse * 40;
            pr.anchoredPosition = new Vector2(-ps / 2, ps / 2); pr.sizeDelta = new Vector2(ps, ps);
            _iMePulse.color = new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 1 - pulse);
            _iMeDir.localEulerAngles = new Vector3(0, 0, -(VrOn ? MoveYaw : _yaw)); // 見取り図は北が上：向いている方へ三角
            _iMeG.gameObject.SetActive(!_dead);
            // 下
            _iDarW.text = _daruma[1].active ? $"西の大将落とし　残り {_daruma[1].left} 段（守れ）" : "西の大将落とし　出ていない";
            _iDarE.text = _daruma[2].active ? $"東の大将落とし　残り {_daruma[2].left} 段（叩け）" : "東の大将落とし　出ていない";
            int both = Owned(1) == 2 ? 1 : Owned(2) == 2 ? 2 : 0;
            _iCold.font = Ts.Font;
            _iCold.text = both != 0 ? $"{(both == 1 ? "西軍" : "東軍")}のコールド勝ちまで {Mathf.Max(0, ColdSec - _coldT):0} 秒" : "";
            _iLog.text = _log.Count == 0 ? "まだ何も起きていない" : string.Join("\n", _log);
        }
    }
}
