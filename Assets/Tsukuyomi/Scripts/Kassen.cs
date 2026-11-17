using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// KASSEN（合戦）の入り口。竹林の小道に「KASSEN の門」を置き、E で入口の板を開く。
    /// 試合は別の場面（実行中に作るシーン）で遊び、そのあいだ島は止めて隠す（空と光とカメラだけ残す）。
    /// モード：SETSUNA（1対1、Setsuna.cs）／SENGOKU（3対3、準備中）。
    /// ごほうび：勝つとふじゅ〜（やさしい 100・ふつう 300・つよい 800）。「つよい」に初めて勝つと狐のきぐるみ。
    /// </summary>
    public class Kassen : MonoBehaviour
    {
        public static Kassen I;
        /// <summary>入口の板を開いているか、試合中。Hud はこの間、島の表示と操作を止める。</summary>
        public static bool Busy => I != null && (I._menuOpen || InMatch);
        /// <summary>試合の場面にいる。</summary>
        public static bool InMatch;

        public static readonly string[] Levels = { "やさしい", "ふつう", "つよい" };
        public static readonly int[] Rewards = { 100, 300, 800 };
        public static readonly int[] SengokuRewards = { 250, 500, 900 };

        Board _menu;
        bool _menuOpen;
        int _opp, _level = 1, _stageSel, _mode, _sgSel; // _mode：0 SETSUNA・1 SENGOKU // _stageSel：0 おまかせ・1〜 舞台
        Text _oppName, _oppDesc, _levelText, _record, _stageText;
        readonly Image[] _levelRing = new Image[3];
        Board _sign;
        Transform _gate;

        // ───────── 起動のときに門を置く ─────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameSession.Instance == null || I != null) return;
            new GameObject("KASSEN").AddComponent<Kassen>();
        }

        /// <summary>門の場所（竹林の小道）と向き。</summary>
        static readonly Vector3 Spot = new Vector3(-15.5f, 21.2f, 172.6f);
        const float SpotYaw = 43f;

        void Awake() => I = this;

        void Start()
        {
            // 竹林の小道（ユーザーが決めた場所）。小道に向かって立ち、門の輪をくぐって入る
            Vector3 p = Spot;
            var face = Quaternion.Euler(0, SpotYaw, 0);
            if (Physics.Raycast(p + Vector3.up * 30, Vector3.down, out var hit, 60, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            transform.SetPositionAndRotation(p, face);
            BuildGate();

            // 地図とワープにも出す
            var zgo = new GameObject("KASSEN の門");
            zgo.transform.SetParent(transform, false);
            var z = zgo.AddComponent<Zone>();
            z.id = "kassen"; z.title = "KASSEN の門"; z.english = "KASSEN GATE";
            z.size = new Vector2(10, 10); z.mapColor = new Color(0.13f, 0.86f, 0.86f); z.order = 13;
            z.warpPos = p + face * new Vector3(0, 0.1f, -5f); z.warpYaw = SpotYaw;
        }

        Material Mat(Color c, bool glow = false)
        {
            var w = GameSession.Instance != null ? GameSession.Instance.player.GetComponent<Wardrobe>() : null;
            var tmpl = glow && w != null && w.glowTemplate != null ? w.glowTemplate : w != null ? w.litTemplate : null;
            if (tmpl == null) return null;
            var m = new Material(tmpl);
            if (glow && w.glowTemplate != null) m.SetColor("_Color", c);
            else { m.SetColor("_BaseColor", c); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null); }
            return m;
        }

        void Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null, bool keepCollider = false)
        {
            var g = GameObject.CreatePrimitive(t);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler != null) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!keepCollider) Destroy(g.GetComponent<Collider>());
        }

        /// <summary>朱塗りの門と、青緑に光る輪（ゲートの向こうが KASSEN）。</summary>
        void BuildGate()
        {
            _gate = new GameObject("門").transform;
            _gate.SetParent(transform, false);
            var red = Mat(new Color(0.78f, 0.16f, 0.12f)); var black = Mat(new Color(0.08f, 0.07f, 0.07f));
            var stone = Mat(new Color(0.55f, 0.55f, 0.58f)); var teal = Mat(new Color(0.2f, 1f, 1f), true);
            foreach (float sx in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cylinder, _gate, new Vector3(sx * 2.4f, 2.6f, 0), new Vector3(0.42f, 2.6f, 0.42f), red, null, true);
                Prim(PrimitiveType.Cylinder, _gate, new Vector3(sx * 2.4f, 0.15f, 0), new Vector3(0.62f, 0.15f, 0.62f), black);
                Prim(PrimitiveType.Cube, _gate, new Vector3(sx * 3.2f, 0.4f, 0), new Vector3(0.7f, 0.8f, 0.7f), stone);
                Prim(PrimitiveType.Sphere, _gate, new Vector3(sx * 3.2f, 1.0f, 0), Vector3.one * 0.38f, teal);
            }
            Prim(PrimitiveType.Cube, _gate, new Vector3(0, 5.1f, 0), new Vector3(6.6f, 0.38f, 0.55f), black);
            Prim(PrimitiveType.Cube, _gate, new Vector3(0, 4.75f, 0), new Vector3(6.2f, 0.3f, 0.5f), red);
            Prim(PrimitiveType.Cube, _gate, new Vector3(0, 4.0f, 0), new Vector3(5.4f, 0.24f, 0.32f), red);
            Prim(PrimitiveType.Cube, _gate, new Vector3(0, 4.45f, 0.18f), new Vector3(0.9f, 0.6f, 0.08f), black);
            // 光る輪
            const int n = 40;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2;
                Prim(PrimitiveType.Cube, _gate, new Vector3(Mathf.Cos(a) * 1.75f, 2.0f + Mathf.Sin(a) * 1.75f, 0), new Vector3(0.28f, 0.07f, 0.07f), teal, new Vector3(0, 0, a * Mathf.Rad2Deg + 90));
            }
            // 看板（空間に置く板）
            _sign = Board.Create("KASSEN の看板", new Vector2(520, 220), 5);
            _sign.pinned = true; _sign.pinScale = 0.006f;
            _sign.pinPos = transform.position + Vector3.up * 6.4f;
            _sign.Apply();
            var r = _sign.root;
            Board.Panel(r, new Rect(0, 0, 520, 220), new Color(0.03f, 0.05f, 0.12f, 0.55f));
            Board.Fans(r, new Rect(6, 6, 508, 208), new Color(1, 1, 1, 0.8f), 46);
            Board.Display(r, new Rect(0, 18, 520, 110), "KASSEN", 92, Board.Teal);
            Board.Label(r, new Rect(0, 128, 520, 40), "合 戦　―　SETSUNA ・ SENGOKU", 28, Color.white, TextAnchor.MiddleCenter);
            Board.Label(r, new Rect(0, 170, 520, 36), "E で入る", 26, Board.Gold, TextAnchor.MiddleCenter);
        }

        void Update()
        {
            if (_sign != null) _sign.Alpha = InMatch ? 0 : 1;
            if (_menuOpen)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (Act.Back.Down) CloseMenu();
                MenuTick();
                return;
            }
            if (InMatch || Hud.Blocked || Ts.ShotMode || PlayerController.Busy || PlayerController.Fly) return;
            var pc = GameSession.Instance != null ? GameSession.Instance.player : null;
            if (pc == null) return;
            var d = pc.Position - transform.position; d.y = 0;
            if (d.magnitude < 4.5f && Act.Interact.Down) OpenMenu();
        }

        // ───────── 入口の板 ─────────

        public static void OpenMenu()
        {
            if (I == null) return;
            I.BuildMenu();
            I._menuOpen = true;
            I._menu.Alpha = 1;
        }

        void CloseMenu()
        {
            _menuOpen = false;
            if (_menu != null) _menu.Alpha = 0;
        }

        void BuildMenu()
        {
            if (_menu != null) return;
            const float w = 1240, h = 700;
            _menu = Board.Create("KASSEN の入口", new Vector2(w, h), 30);
            _menu.worldOffset = new Vector3(0, 0, 1.6f);
            _menu.Apply();
            var r = _menu.root;
            Board.Frame(r, new Rect(0, 0, w, h));
            Board.Display(r, new Rect(70, 34, 600, 110), "KASSEN", 100, Board.Teal, TextAnchor.MiddleLeft);
            Board.Label(r, new Rect(76, 138, 600, 30), "ツクヨミの合戦　―　勝てばふじゅ〜", 22, new Color(1, 1, 1, 0.8f));
            Board.Panel(r, new Rect(76, 176, 260, 2), Board.Vermilion);

            // モード
            _modeBar[0] = ModeCard(r, new Rect(70, 206, 330, 170), "SETSUNA", "刹那", "1対1 の格闘。一人称で、\nコンボをつなげて勝つ。", 0);
            _modeBar[1] = ModeCard(r, new Rect(70, 396, 330, 170), "SENGOKU", "戦国", "3対3 の陣取り。いまは\n6つの武器の練習場。", 1);

            // 相手
            float cx = 760;
            Board.Ring(r, new Vector2(cx, 300), 150, new Color(1, 1, 1, 0.5f));
            Board.Ring(r, new Vector2(cx, 300), 118, new Color(Board.Teal.r, Board.Teal.g, Board.Teal.b, 0.5f), false);
            _oppName = Board.Display(r, new Rect(cx - 150, 240, 300, 80), "", 64, Color.white);
            _oppDesc = Board.Label(r, new Rect(cx - 140, 318, 280, 60), "", 20, new Color(1, 1, 1, 0.85f), TextAnchor.UpperCenter);
            Board.Label(r, new Rect(cx - 200, 120, 400, 30), "相手をえらぶ", 20, Board.Gold, TextAnchor.MiddleCenter);
            Board.ButtonNode(r, new Rect(cx - 280, 270, 70, 60), "◀", 28, () => { _opp = (_opp + SetsunaStyles.Cpu.Length - 1) % SetsunaStyles.Cpu.Length; });
            Board.ButtonNode(r, new Rect(cx + 210, 270, 70, 60), "▶", 28, () => { _opp = (_opp + 1) % SetsunaStyles.Cpu.Length; });
            Board.Label(r, new Rect(cx - 290, 334, 90, 24), "PREV", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, true);
            Board.Label(r, new Rect(cx + 200, 334, 90, 24), "NEXT", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, true);

            // 強さ（丸いボタン）
            Board.Label(r, new Rect(cx - 300, 470, 600, 30), "強さ", 20, Board.Gold, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = Board.RoundButton(r, new Vector2(cx - 150 + i * 150, 545), 52, Levels[i], 22, () => _level = k);
                _levelRing[i] = b.GetComponent<Image>();
            }
            _levelText = Board.Label(r, new Rect(cx - 300, 602, 600, 28), "", 20, Color.white, TextAnchor.MiddleCenter);

            // 名札・記録・出陣
            Board.NameTag(r, new Rect(w - 120, 70, 54, 250), "合戦場", Board.Vermilion, true, 34);
            // 舞台
            Board.Label(r, new Rect(930, 340, 290, 28), "舞台", 20, Board.Gold, TextAnchor.MiddleCenter);
            _stageText = Board.Label(r, new Rect(980, 372, 190, 50), "", 24, Color.white, TextAnchor.MiddleCenter);
            Board.ButtonNode(r, new Rect(926, 372, 50, 50), "◀", 22, () => { if (_mode == 1) _sgSel = 1 - _sgSel; else _stageSel = (_stageSel + Setsuna.StageNames.Length) % (Setsuna.StageNames.Length + 1); });
            Board.ButtonNode(r, new Rect(1174, 372, 50, 50), "▶", 22, () => { if (_mode == 1) _sgSel = 1 - _sgSel; else _stageSel = (_stageSel + 1) % (Setsuna.StageNames.Length + 1); });
            _record = Board.Label(r, new Rect(70, 586, 400, 80), "", 18, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft);
            Board.ButtonNode(r, new Rect(w - 300, h - 150, 220, 64), "出陣", 32, StartSetsuna);
            Board.ButtonNode(r, new Rect(w - 300, h - 76, 220, 44), "閉じる（Esc）", 20, CloseMenu);
        }

        readonly Image[] _modeBar = new Image[2];

        /// <summary>モードの札（押すとそのモードをえらぶ）。左の帯を返す（えらんでいると青緑）。</summary>
        Image ModeCard(RectTransform r, Rect rc, string en, string jp, string desc, int mode)
        {
            var bg = Board.ButtonNode(r, rc, "", 10, () => _mode = mode);
            bg.GetComponent<Image>().color = new Color(0.05f, 0.2f, 0.26f, 0.85f);
            var bar = Board.Panel(r, new Rect(rc.x, rc.y, 7, rc.height), Board.Teal);
            Board.Display(r, new Rect(rc.x + 22, rc.y + 10, 260, 60), en, 44, Board.Teal, TextAnchor.MiddleLeft);
            Board.Label(r, new Rect(rc.x + 24, rc.y + 74, 290, 90), desc, 20, new Color(1, 1, 1, 0.9f), TextAnchor.UpperLeft);
            var tag = new Rect(rc.xMax - 56, rc.y + 14, 40, 110);
            Board.NameTag(r, tag, jp, Board.Teal, false, 26);
            return bar;
        }

        void MenuTick()
        {
            var st = SetsunaStyles.Cpu[_opp];
            _oppName.text = _mode == 1 ? "西 対 東" : st.name;
            for (int i = 0; i < 2; i++) _modeBar[i].color = i == _mode ? Board.Teal : new Color(1, 1, 1, 0.15f);
            _stageText.text = _mode == 1 ? (_sgSel == 0 ? "3対3 の試合" : "練習場") : _stageSel == 0 ? "おまかせ" : Setsuna.StageNames[_stageSel - 1];
            _oppDesc.text = _mode == 1 ? "味方：茜・楓\n敵：疾風・岩鉄・朧" : st.desc;
            _levelText.text = _mode == 1 ? (_sgSel == 0 ? $"勝てば {SengokuRewards[_level]:N0} ふじゅ〜（相手と味方の強さ）" : "練習場はごほうびなし") : $"勝てば {Rewards[_level]:N0} ふじゅ〜" + (_level == 2 && !HardCleared ? "　＋　初めて勝つと「狐のきぐるみ」" : "");
            for (int i = 0; i < 3; i++)
                _levelRing[i].color = i == _level ? new Color(0.5f, 0.12f, 0.1f, 0.95f) : new Color(0.04f, 0.1f, 0.18f, 0.85f);
            var d = Data;
            _record.text = d != null ? $"SETSUNA　{d.setsunaWins} 勝 {d.setsunaLosses} 敗\nいまのふじゅ〜　{Mathf.Max(0, Fuju.Last):N0}" : "";
        }

        static SaveData Data => GameSession.Instance != null ? GameSession.Instance.Data : null;
        public static bool HardCleared => Data != null && Data.owned.Contains(Catalog.KitsuneKigurumi);

        void StartSetsuna()
        {
            CloseMenu();
            if (_mode == 1) BeginSengoku(_sgSel == 0, _level);
            else BeginMatch(_opp, _level, _stageSel - 1);
        }

        // ───────── 試合の場面へ（島は止めて隠す）─────────

        readonly List<GameObject> _hidden = new List<GameObject>();
        Scene _scene;
        CameraClearFlags _clear;
        float _fov;
        OrbitCamera _orbit;

        public static void BeginMatch(int opp, int level, int stage = -1)
        {
            if (I == null || InMatch) return;
            I.HideIsland();
            InMatch = true;
            Setsuna.Begin(I._scene, opp, level, stage);
        }

        /// <summary>SENGOKU（いまは練習場）をはじめる。</summary>
        public static void BeginSengoku(bool match = false, int level = 1)
        {
            if (I == null || InMatch) return;
            I.HideIsland();
            InMatch = true;
            Sengoku.Begin(I._scene, match, level);
        }

        /// <summary>SENGOKU の試合の結果を記録して、ふじゅ〜を渡す。</summary>
        public static string RewardSengoku(bool win, int level)
        {
            var d = Data;
            if (d == null) return "";
            if (!win) { d.sengokuLosses++; GameSession.Instance.SaveItems(); return "ふじゅ〜はもらえませんでした"; }
            d.sengokuWins++;
            int amt = SengokuRewards[Mathf.Clamp(level, 0, 2)];
            Fuju.Earn(amt);
            return $"＋{amt:N0} ふじゅ〜";
        }

        void HideIsland()
        {
            var island = SceneManager.GetActiveScene();
            var cam = Camera.main;
            _orbit = cam != null ? cam.GetComponent<OrbitCamera>() : null;
            if (_orbit != null) _orbit.enabled = false;
            _fov = cam != null ? cam.fieldOfView : 58f;
            _hidden.Clear();
            foreach (var g in island.GetRootGameObjects())
            {
                if (!g.activeSelf || g == gameObject) continue;
                if (cam != null && cam.transform.root == g.transform) continue;
                if (g.GetComponentInChildren<GameSession>(true) != null || g.GetComponentInChildren<Sfx>(true) != null) continue;
                if (g.GetComponentInChildren<RealTimeSky>(true) != null) continue;       // 空と太陽・月の光は残す
                bool keep = false;
                foreach (var v in g.GetComponentsInChildren<Volume>(true)) if (v.isGlobal) keep = true; // 光のにじみ（ポストプロセス）も残す
                foreach (var l in g.GetComponentsInChildren<Light>(true)) if (l.type == LightType.Directional) keep = true; // 太陽と月
                if (keep) continue;
                g.SetActive(false);
                _hidden.Add(g);
            }
            if (_gate != null) _gate.gameObject.SetActive(false);
            _scene = SceneManager.CreateScene("KASSEN_SETSUNA_" + Time.frameCount);
        }

        /// <summary>試合を終えて島へもどる。</summary>
        public static void EndMatch()
        {
            if (I == null || !InMatch) return;
            InMatch = false;
            Time.timeScale = 1f;
            foreach (var g in I._hidden) if (g != null) g.SetActive(true);
            I._hidden.Clear();
            if (I._gate != null) I._gate.gameObject.SetActive(true);
            if (I._scene.IsValid()) SceneManager.UnloadSceneAsync(I._scene);
            var cam = Camera.main;
            if (cam != null) cam.fieldOfView = I._fov;
            if (I._orbit != null) { I._orbit.enabled = true; I._orbit.Snap(); }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        /// <summary>確認用（--tour）：次の場所へ行く前に、入口の板と試合を片づける。</summary>
        public static void ResetForTour()
        {
            if (I == null) return;
            I.CloseMenu();
            if (Setsuna.I != null) Destroy(Setsuna.I.gameObject);
            if (Sengoku.I != null) Destroy(Sengoku.I.gameObject);
            EndMatch();
        }

        /// <summary>試合の結果を記録して、ごほうびを渡す。もらったものの説明を返す。</summary>
        public static string Reward(bool win, int level)
        {
            var d = Data;
            if (d == null) return "";
            if (!win) { d.setsunaLosses++; GameSession.Instance.SaveItems(); return "ふじゅ〜はもらえませんでした"; }
            d.setsunaWins++;
            int amt = Rewards[Mathf.Clamp(level, 0, 2)];
            string msg = $"＋{amt:N0} ふじゅ〜";
            if (level == 2 && !d.owned.Contains(Catalog.KitsuneKigurumi))
            {
                d.owned.Add(Catalog.KitsuneKigurumi);
                msg += "\n「狐のきぐるみ」を手に入れました！（C の着がえで着られます）";
            }
            Fuju.Earn(amt); // ここで保存もする
            return msg;
        }
    }
}
