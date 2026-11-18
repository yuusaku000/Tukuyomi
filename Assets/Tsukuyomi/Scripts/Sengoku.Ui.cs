using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// SENGOKU の見やすい表示。
    ///  武器をえらぶ画面（ラウンドの始め）：左に武器の立体の見本（くるくる回る）・持ち主の名札・強さの目盛り・説明、右に6枚の札。
    ///  試合中：櫓と大将落とし（と本陣）の方向と距離の目印、今やることのひとこと、当たったときの照準の印。
    /// </summary>
    public partial class Sengoku
    {
        static readonly string[] Owners = { "彩葉", "かぐや", "ヤチヨ", "乃衣", "帝", "雷" };
        static readonly string[] Lines =
        {
            "速い連撃と光弾。ワイヤーで建物から建物へ飛び回る。",
            "一撃が重い。ミサイルで吹き飛ばし、ジェットで空を飛ぶ。",
            "守りが固い。投げればもどる。乗物に乗れば浮いて水の上も渡れる。",
            "遠くから射ぬく。自分が矢になって一気に飛びこむ。",
            "金色の金棒。一撃が重く、金の弾も撃てる。大きく振って一気に跳ぶ。",
            "大きな団扇。少し遠くまで届く。地雷をしかけ、あおいで飛ぶ。",
        };
        // 攻撃・射程・機動・守り（1〜5）
        static readonly int[,] Stats = { { 3, 3, 5, 2 }, { 5, 2, 4, 2 }, { 2, 3, 3, 5 }, { 3, 5, 3, 1 }, { 5, 3, 4, 2 }, { 3, 3, 4, 3 } };
        static readonly string[] StatNames = { "攻撃", "射程", "機動", "守り" };

        Board _selB;
        int _pick;
        RenderTexture _rt;
        Camera _pvCam;
        Transform _pvRoot;
        readonly GameObject[] _pvModels = new GameObject[6];
        Text _sName, _sEn, _sLine, _sOwnerTag, _sRound, _sStage, _sStageLine;
        readonly Image[,] _sBars = new Image[4, 5];
        readonly Image[] _cardBg = new Image[6], _cardBar = new Image[6];

        // ───────── 武器をえらぶ ─────────

        void BuildSelect()
        {
            _selB = Board.Create("武器をえらぶ", new Vector2(1920, 1080), 200);
            _selB.worldOffset = new Vector3(0, 0, 1.7f);
            _selB.Apply();
            var r = _selB.root;
            Board.Panel(r, new Rect(-400, -200, 2720, 1480), new Color(0.015f, 0.025f, 0.07f, 1f));
            Board.Fans(r, new Rect(20, 20, 1880, 1040), new Color(1, 1, 1, 0.75f), 110);
            // 見出し
            Board.Display(r, new Rect(90, 50, 900, 110), "CHOOSE YOUR WEAPON", 64, Board.Teal, TextAnchor.MiddleLeft);
            var jp = Board.Label(r, new Rect(96, 150, 600, 50), "武器をえらぶ", 36, Color.white); jp.font = Board.Mincho; jp.fontStyle = FontStyle.Bold;
            Board.Panel(r, new Rect(96, 206, 300, 2), Board.Vermilion);
            _sRound = Board.Label(r, new Rect(1300, 60, 530, 40), "", 26, Board.Gold, TextAnchor.MiddleRight);
            // 今回の舞台（試合ごとにランダム）
            Board.Label(r, new Rect(1000, 104, 830, 26), "STAGE ▸ 今回の舞台", 17, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight, true);
            _sStage = Board.Label(r, new Rect(1000, 130, 830, 50), "", 36, Color.white, TextAnchor.MiddleRight);
            _sStage.font = Board.Mincho; _sStage.fontStyle = FontStyle.Bold;
            _sStageLine = Board.Label(r, new Rect(1000, 182, 830, 30), "", 19, Board.Teal, TextAnchor.MiddleRight);

            // 左：見本
            var frame = new Rect(90, 240, 760, 560);
            Board.Panel(r, frame, new Color(0.04f, 0.08f, 0.16f, 0.9f));
            var raw = Board.Raw(r, new Rect(frame.x + 6, frame.y + 6, frame.width - 12, frame.height - 12));
            _rt = new RenderTexture(1024, 752, 16) { name = "武器の見本" };
            raw.texture = _rt;
            Board.Ring(r, new Vector2(frame.center.x, frame.y + frame.height * 0.52f), 250, new Color(1, 1, 1, 0.22f), false);
            Board.Ring(r, new Vector2(frame.center.x, frame.y + frame.height * 0.52f), 210, new Color(Board.Teal.r, Board.Teal.g, Board.Teal.b, 0.25f));
            Board.Fans(r, frame, new Color(1, 1, 1, 0.8f), 54);
            _sOwnerTag = Board.NameTag(r, new Rect(frame.x + 22, frame.y + 26, 58, 300), "彩葉の二刀流", Board.Vermilion, false, 30);
            _sEn = Board.Display(r, new Rect(frame.x, frame.yMax - 120, frame.width - 30, 80), "", 64, Color.white, TextAnchor.MiddleRight);
            _sName = Board.Label(r, new Rect(frame.x, frame.yMax - 50, frame.width - 34, 40), "", 26, Board.Gold, TextAnchor.MiddleRight);
            // 強さの目盛りと説明
            for (int s = 0; s < 4; s++)
            {
                float y = 830 + s * 40;
                var lab = Board.Label(r, new Rect(96, y, 90, 32), StatNames[s], 22, Color.white); lab.font = Board.Mincho;
                for (int k = 0; k < 5; k++)
                {
                    Board.Panel(r, new Rect(190 + k * 64, y + 8, 58, 16), new Color(1, 1, 1, 0.12f));
                    _sBars[s, k] = Board.Panel(r, new Rect(190 + k * 64, y + 8, 58, 16), Board.Teal);
                }
            }
            _sLine = Board.Label(r, new Rect(530, 830, 330, 160), "", 21, new Color(1, 1, 1, 0.9f), TextAnchor.UpperLeft);
            _sLine.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 右：6枚の札
            for (int i = 0; i < 6; i++)
            {
                int k = i;
                var rc = new Rect(920 + (i % 2) * 460, 240 + (i / 2) * 186, 440, 168);
                var b = Board.ButtonNode(r, rc, "", 10, () => { _pick = k; Play(_sClick, 0.5f); });
                _cardBg[i] = b.GetComponent<Image>();
                _cardBar[i] = Board.Panel(r, new Rect(rc.x, rc.y, 8, rc.height), Board.Teal);
                Board.Ring(r, new Vector2(rc.x + 56, rc.y + 60), 30, new Color(1, 1, 1, 0.85f), false);
                Board.Display(r, new Rect(rc.x + 26, rc.y + 32, 60, 56), (i + 1).ToString(), 34, Color.white);
                var nm = Board.Label(r, new Rect(rc.x + 104, rc.y + 18, 320, 56), Weapons[i].name, 40, Color.white); nm.font = Board.Mincho; nm.fontStyle = FontStyle.Bold;
                Board.Display(r, new Rect(rc.x + 104, rc.y + 74, 320, 40), Weapons[i].en, 26, Board.Teal, TextAnchor.MiddleLeft);
                Board.Label(r, new Rect(rc.x + 104, rc.y + 118, 320, 34), $"{Owners[i]}　攻{Stats[i, 0]}・射{Stats[i, 1]}・機{Stats[i, 2]}・守{Stats[i, 3]}", 19, new Color(1, 1, 1, 0.75f));
                Board.Fan(r, new Rect(rc.x + 2, rc.y + 2, rc.width - 4, rc.height - 4), 3, new Color(1, 1, 1, 0.55f), 34, 5);
            }
            _selGo = Board.ButtonNode(r, new Rect(1380, 830, 440, 100), "出陣", 40, ConfirmPick);
            _selKeys = Board.Label(r, new Rect(920, 950, 900, 70), "", 19, new Color(1, 1, 1, 0.7f), TextAnchor.UpperRight);
            BuildPreview();
        }

        /// <summary>見本の舞台：はるか下に武器と光とカメラを置き、RenderTexture に写す（試合場からは見えない）。</summary>
        void BuildPreview()
        {
            _pvRoot = new GameObject("武器の見本の舞台").transform;
            _pvRoot.SetParent(transform, false);
            _pvRoot.position = new Vector3(0, -400, 0);
            for (int i = 0; i < 6; i++)
            {
                if (_viewModels[i] == null) continue;
                var m = Instantiate(_viewModels[i], _pvRoot);
                m.name = "見本：" + Weapons[i].name;
                m.SetActive(true);
                // 見本には、ふだん隠れている部品（開いた傘・ジェットの炎）を入れない
                foreach (var t in m.GetComponentsInChildren<Transform>(true))
                    if (t != null && (t.name == "開いた傘" || !t.gameObject.activeSelf)) DestroyImmediate(t.gameObject);
                m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity; m.transform.localScale = Vector3.one;
                // 見えている部品の大きさをそろえて（いちばん長いところが 2.2m）、まん中に寄せる
                Bounds Measure()
                {
                    bool first = true; var bb = new Bounds(m.transform.position, Vector3.zero);
                    foreach (var rr in m.GetComponentsInChildren<Renderer>()) { if (first) { bb = rr.bounds; first = false; } else bb.Encapsulate(rr.bounds); }
                    return bb;
                }
                var b0 = Measure();
                float longest = Mathf.Max(b0.size.x, Mathf.Max(b0.size.y, b0.size.z));
                if (longest > 0.01f) m.transform.localScale = Vector3.one * (2.2f / longest);
                var bounds = Measure();
                m.transform.position -= bounds.center - _pvRoot.position;
                m.SetActive(false);
                _pvModels[i] = m;
            }
            var camGo = new GameObject("見本のカメラ");
            camGo.transform.SetParent(_pvRoot, false);
            camGo.transform.localPosition = new Vector3(0, 0.2f, -4.4f);
            camGo.transform.LookAt(_pvRoot.position);
            _pvCam = camGo.AddComponent<Camera>();
            _pvCam.targetTexture = _rt; _pvCam.clearFlags = CameraClearFlags.SolidColor;
            _pvCam.backgroundColor = new Color(0.04f, 0.08f, 0.16f, 1f);
            _pvCam.nearClipPlane = 0.1f; _pvCam.farClipPlane = 12f; _pvCam.fieldOfView = 34f;
            foreach (var (rot, col, inten) in new[] { (new Vector3(30, 40, 0), new Color(1f, 0.95f, 0.9f), 1.4f), (new Vector3(10, -140, 0), new Color(0.4f, 0.9f, 1f), 1.1f) })
            {
                var l = new GameObject("見本の光").AddComponent<Light>();
                l.transform.SetParent(_pvRoot, false); l.transform.localPosition = Vector3.zero; l.transform.localEulerAngles = rot;
                l.type = LightType.Point; l.range = 8f; l.intensity = inten * 3f; l.color = col;
                l.transform.localPosition = Quaternion.Euler(rot) * Vector3.back * 2.5f;
            }
        }

        void OpenSelect()
        {
            if (_selB == null) BuildSelect();
            _pick = _w;
            _selB.Alpha = 1;
            if (_pvCam) _pvCam.enabled = true;
            if (_hud) _hud.Alpha = 0;             // 選んでいるあいだは、試合中の表示と手の武器を隠す
            if (_viewRoot) _viewRoot.gameObject.SetActive(false);
            _sRound.text = _round == 3 ? "FINAL ROUND" : $"ROUND {_round}";
            _sStage.text = StageNamesSg[_theme];
            _sStageLine.text = StageLines[_theme];
        }

        void ConfirmPick()
        {
            if (_phase != MPhase.Select) return;
            Select(_pick);
            _selB.Alpha = 0;
            if (_pvCam) _pvCam.enabled = false;
            if (_hud) _hud.Alpha = 1;
            if (_viewRoot) _viewRoot.gameObject.SetActive(true);
            _phase = MPhase.Intro; _phaseT = 0;
            Center(_round == 3 ? "FINAL ROUND" : $"ROUND {_round}", Board.Teal, 2.2f, "櫓を取って、東の大将落としを叩き落とせ！（しばらく攻撃を受けなければ回復）");
            Play(_sBoom, 0.4f);
        }

        UnityEngine.UI.Button _selGo;
        Text _selKeys;
        readonly Pad.Edge _selX = new Pad.Edge(), _selY = new Pad.Edge();

        void SelectTick(float dt)
        {
            if (_selB == null || _selB.Alpha <= 0) return;
            for (int i = 0; i < 6; i++) if (Act.Number(i)) { _pick = i; Play(_sClick, 0.4f); }
            // スティック・矢印キーで札を動かす（2列×3段）
            var mv = Act.Move.ReadValue<Vector2>();
            int sx = _selX.Tick(mv.x), sy = _selY.Tick(mv.y);
            if (sx != 0) { _pick = (_pick / 2) * 2 + Mathf.Clamp(_pick % 2 + sx, 0, 1); Play(_sClick, 0.4f); }
            if (sy != 0) { _pick = Mathf.Clamp(_pick - sy * 2, _pick % 2, 4 + _pick % 2); Play(_sClick, 0.4f); }
            Pad.SetLabel(_selGo, Pad.Label("出陣", Pad.OkKey));
            if (_selKeys != null) _selKeys.text = (Vr.On ? "左スティックでもえらべます" : "1〜6 のキー・矢印キーでもえらべます") + $"。{Pad.OkKey} で出陣。武器はラウンドごとに選びなおせます。" + "\n倒されて復活しても、武器はこのラウンドのあいだ同じです。";
            if (Pad.Ok) { ConfirmPick(); return; }
            for (int i = 0; i < 6; i++)
            {
                bool on = i == _pick;
                _cardBg[i].color = on ? new Color(0.06f, 0.26f, 0.32f, 0.95f) : new Color(0.04f, 0.07f, 0.14f, 0.85f);
                _cardBar[i].color = on ? Board.Teal : new Color(1, 1, 1, 0.12f);
                if (_pvModels[i]) _pvModels[i].SetActive(on);
            }
            if (_pvModels[_pick]) _pvModels[_pick].transform.RotateAround(_pvRoot.position, Vector3.up, 40f * Time.unscaledDeltaTime);
            _sOwnerTag.text = Board.Vertical($"{Owners[_pick]}の{Weapons[_pick].name}");
            _sEn.text = Weapons[_pick].en;
            _sName.text = $"{Weapons[_pick].name}（{Owners[_pick]}）";
            _sLine.text = Lines[_pick];
            for (int s = 0; s < 4; s++) for (int k = 0; k < 5; k++)
                _sBars[s, k].color = k < Stats[_pick, s] ? (s == 3 ? Board.Gold : Board.Teal) : new Color(0, 0, 0, 0);
        }

        // ───────── 試合中：目印と、今やること ─────────

        class Marker { public Image ring; public Text label, dist; public Image arrow; }
        readonly List<Marker> _marks = new List<Marker>();
        Text _goalText;
        Image _hitX;
        float _hitT;

        void BuildMarkers()
        {
            var r = _hud.root;
            for (int i = 0; i < 4; i++)
            {
                var m = new Marker();
                m.ring = Board.Panel(r, new Rect(0, 0, 44, 44), Color.white); m.ring.sprite = Board.RingSprite;
                m.label = Board.Label(r, new Rect(0, 0, 220, 28), "", 20, Color.white, TextAnchor.MiddleCenter);
                m.dist = Board.Label(r, new Rect(0, 0, 220, 24), "", 17, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, true);
                m.arrow = Board.Panel(r, new Rect(0, 0, 14, 14), Color.white); m.arrow.sprite = Board.DiscSprite;
                _marks.Add(m);
            }
            _goalText = Board.Display(r, new Rect(360, 370, 1200, 44), "", 30, Color.white);
            _hitX = Board.Panel(r, new Rect(946, 526, 28, 28), Color.white); _hitX.sprite = Board.RingSprite; _hitX.color = new Color(1, 1, 1, 0);
        }

        /// <summary>当てたとき、照準のまわりに輪が出る（倒したときは朱色）。</summary>
        public void HitMark(bool kill) { _hitT = kill ? 0.35f : 0.18f; if (_hitX) _hitX.color = kill ? Board.Vermilion : Color.white; }

        void MarkersTick()
        {
            if (_marks.Count == 0 || _cam == null) return;
            var items = new List<(Vector3 p, string name, Color c)>();
            Color OwnerCol(int o) => o == 1 ? Board.Teal : o == 2 ? Board.Vermilion : Color.white;
            items.Add((_towers[0].c + Vector3.up * 3f, "北の櫓", OwnerCol(_towers[0].owner)));
            items.Add((_towers[1].c + Vector3.up * 3f, "南の櫓", OwnerCol(_towers[1].owner)));
            if (_daruma[2].active) items.Add((DarumaSpot(2) + Vector3.up * 6f, "叩け：東の大将落とし", Board.Teal));
            else if (_daruma[1].active) items.Add((DarumaSpot(1) + Vector3.up * 6f, "守れ：西の大将落とし", Board.Vermilion));
            float halfW = HudHalfW;
            bool show = (_phase == MPhase.Fight || _phase == MPhase.Intro) && _intelT <= 0.01f; // 戦況の板を開いているあいだは隠す
            for (int i = 0; i < _marks.Count; i++)
            {
                var m = _marks[i];
                bool on = show && i < items.Count && !_dead;
                m.ring.enabled = m.label.enabled = m.dist.enabled = m.arrow.enabled = on;
                if (!on) continue;
                var (p, name, c) = items[i];
                var sp = HudUi(p);
                bool behind = sp.z < 0;
                var ui = new Vector2(sp.x, sp.y);
                // 画面の外なら、ふちに寄せる
                float mx = Mathf.Min(halfW, 960) - 70, my = 470;
                bool edge = behind || Mathf.Abs(ui.x) > mx || Mathf.Abs(ui.y) > my;
                if (edge)
                {
                    float k = Mathf.Min(mx / Mathf.Max(0.01f, Mathf.Abs(ui.x)), my / Mathf.Max(0.01f, Mathf.Abs(ui.y)));
                    ui *= Mathf.Min(1f, k);
                }
                var ax = 960 + ui.x; var ay = 540 - ui.y;
                float d = Vector3.Distance(_rig.transform.position, p);
                Pos(m.ring, ax - 22, ay - 22); m.ring.color = new Color(c.r, c.g, c.b, 0.95f);
                Pos(m.label, ax - 110, ay - 56); m.label.text = name; m.label.color = c;
                Pos(m.dist, ax - 110, ay + 24); m.dist.text = $"{d:0}m";
                var dir = ui.sqrMagnitude > 1 ? ui.normalized : Vector2.up;
                Pos(m.arrow, ax - 7 + dir.x * 30, ay - 7 - dir.y * 30); m.arrow.color = c; m.arrow.enabled = edge;
            }
            // 今やること
            string goal;
            Color gc = Color.white;
            if (_daruma[1].active) { goal = $"西の大将落としが狙われている！　二の丸へもどって守れ（あと {_daruma[1].left} 段）"; gc = new Color(1f, 0.6f, 0.5f); }
            else if (_daruma[2].active) { goal = $"東の城の二の丸へ！　大将落としを近接攻撃（F でも殴れる）で叩き落とせ（あと {_daruma[2].left} 段）"; gc = Board.Teal; }
            else if (Owned(1) == 0) goal = _theme == 1 ? "まずは櫓を取れ：北の岩山か、南の大樹の台地へ" : "まずは櫓を取れ：北の山か、南の湖の浮き島へ";
            else goal = "櫓を守りながら、もう一つの櫓も取ろう";
            if (_dead || _phase != MPhase.Fight || _intelT > 0.01f) goal = "";
            _goalText.font = Ts.Font; _goalText.text = VrText(goal); _goalText.color = gc;
            _hitT -= Time.unscaledDeltaTime;
            if (_hitX) { var c2 = _hitX.color; c2.a = Mathf.Clamp01(_hitT * 6f); _hitX.color = c2; }
        }

        static void Pos(Graphic g, float x, float y) => ((RectTransform)g.transform).anchoredPosition = new Vector2(x, -y);
    }
}
