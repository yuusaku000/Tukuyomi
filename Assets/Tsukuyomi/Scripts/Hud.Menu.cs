using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// Esc のメニュー（板）。左に分類（遊ぶ／設定／そのほか）、右にその中身。
    ///  ↑↓ でえらぶ・→／Enter で入る・←→ で値を変える・Q／E で設定の分類を切りかえ・Esc でもどる。マウスでも押せる。
    ///  設定は1か所だけに置き、行ごとにひとこと説明をつける。分類ごとに「初期設定にもどす」。
    ///  板（Board）なので、VR では目の前に浮かぶ。
    /// </summary>
    public partial class Hud
    {
        Board _mb;
        RectTransform _mbPane;
        Text _mbInfo;
        int _mbItem, _mbRow, _mbFocus; // _mbFocus：0 左の分類・1 右の行
        bool _mbWasOpen, _mbConfirm;
        readonly List<(Image bg, Image bar, Text label)> _mbNav = new List<(Image, Image, Text)>();
        readonly List<MRow> _mbRows = new List<MRow>();
        readonly List<(Text t, Func<string> f)> _mbLive = new List<(Text, Func<string>)>();

        enum MKind { Action, Page }
        struct MItem { public string group, name, en, desc; public MKind kind; public Action act; }

        class MRow
        {
            public string label, hint;
            public Func<string> value;           // 値の文字（null なら押すだけの行）
            public Action<int> change;           // ←→（-1／+1）
            public Action press;                 // Enter・クリック
            public Image bg;
        }

        MItem[] _mItems;
        MItem[] MItems => _mItems ??= new[]
        {
            new MItem { group = "遊ぶ", name = "つづける", en = "RESUME", kind = MKind.Action, desc = "メニューを閉じて、島にもどります。", act = () => _menu = false },
            new MItem { group = "遊ぶ", name = "地図とワープ", en = "MAP", kind = MKind.Action, desc = "島の地図。行きたい場所をえらんでワープできます。（M）", act = () => Sub(() => { _menu = false; _map = true; }) },
            new MItem { group = "遊ぶ", name = "図鑑と番付", en = "BOOK", kind = MKind.Action, desc = "釣った魚の図鑑と、大物番付。（B）", act = () => Sub(() => OpenBook(0)) },
            new MItem { group = "遊ぶ", name = "着がえ", en = "WARDROBE", kind = MKind.Action, desc = "買った服や髪型にかえます。（C）", act = () => Sub(() => { _menu = false; OpenWardrobe(); }) },
            new MItem { group = "設定", name = "音", en = "SOUND", kind = MKind.Page, desc = "全体・環境音・効果音・ライブの音楽の大きさ。" },
            new MItem { group = "設定", name = "画面と表示", en = "DISPLAY", kind = MKind.Page, desc = "画質・視点・方位の帯・照準・吹き出し。" },
            new MItem { group = "設定", name = "操作", en = "CONTROLS", kind = MKind.Page, desc = "見回す速さ・上下の向き・キーの一覧。" },
            new MItem { group = "設定", name = "VR", en = "VR", kind = MKind.Page, desc = "VR の画質・回り方・酔い防止・ワープ・背の高さ。" },
            new MItem { group = "そのほか", name = "遊び方", en = "HOW TO PLAY", kind = MKind.Action, desc = "操作や遊び方を、ページをめくって読めます。", act = () => Sub(() => OpenGuide()) },
            new MItem { group = "そのほか", name = "ふじゅ〜と両替", en = "FUJU", kind = MKind.Action, desc = "残高を見る・ミニヤチヨから両替する（片道）。", act = () => Sub(() => OpenExchange()) },
            new MItem { group = "そのほか", name = "写真のフォルダ", en = "PHOTOS", kind = MKind.Action, desc = "F12 で撮った写真のフォルダを開きます。", act = () => { System.IO.Directory.CreateDirectory(PhotoDir); Application.OpenURL("file:///" + PhotoDir.Replace('\\', '/')); } },
            new MItem { group = "そのほか", name = "データの初期化", en = "RESET DATA", kind = MKind.Action, desc = "服・髪・ふじゅ〜・図鑑・番付・KASSEN の記録を消して、初めてのログインからやりなおします（音量・画質・VR の設定と写真は残ります）。", act = () => { _mbConfirm = true; ShowMenuPane(); } },
            new MItem { group = "そのほか", name = "ログアウト", en = "LOGOUT", kind = MKind.Action, desc = "ツクヨミを終わります。", act = () => { _mbConfirm = true; ShowMenuPane(); } },
        };

        /// <summary>次に開くとき最初にえらぶ分類（確認用の tour から）。</summary>
        public static int MenuStart = -1;

        const float MbW = 1240, MbH = 760, NavX = 44, NavW = 250, PaneX = 340, PaneW = 856;

        void BuildMenuBoard()
        {
            if (_mb != null) return;
            _mb = Board.Create("メニュー", new Vector2(MbW, MbH), 25);
            _mb.screenAnchor = new Vector2(0.5f, 0.5f);
            _mb.worldOffset = new Vector3(0, -0.05f, 1.3f);
            _mb.Apply();
            var r = _mb.root;
            Board.Dim(r, MbW, MbH);
            Board.Frame(r, new Rect(0, 0, MbW, MbH));
            Board.Display(r, new Rect(NavX, 30, 400, 28), "SYSTEM MENU", 22, Board.Teal, TextAnchor.MiddleLeft);
            Board.Label(r, new Rect(NavX, 58, 400, 52), "ツクヨミ", 40, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            _mbInfo = Board.Label(r, new Rect(MbW - 640, 40, 596, 30), "", 20, Board.Gold, TextAnchor.MiddleRight);
            Board.Panel(r, new Rect(NavX, 116, MbW - NavX * 2, 1), new Color(1, 1, 1, 0.25f));
            Board.Panel(r, new Rect(PaneX - 24, 136, 1, MbH - 220), new Color(1, 1, 1, 0.18f));

            // 左：分類
            float y = 132; string g = null;
            for (int i = 0; i < MItems.Length; i++)
            {
                var it = MItems[i];
                if (it.group != g)
                {
                    g = it.group;
                    Board.Label(r, new Rect(NavX + 6, y, NavW, 26), g, 16, new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.75f));
                    y += 28;
                }
                int idx = i;
                var bg = Board.Panel(r, new Rect(NavX, y, NavW, 38), new Color(1, 1, 1, 0));
                bg.raycastTarget = true;
                var b = bg.gameObject.AddComponent<UnityEngine.UI.Button>();
                b.transition = Selectable.Transition.None; b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { NoSelect(); _mbItem = idx; _mbConfirm = false; _mbRow = 0; ShowMenuPane(); if (MItems[idx].kind == MKind.Action) MItems[idx].act(); else _mbFocus = 1; });
                var bar = Board.Panel(r, new Rect(NavX, y + 6, 4, 26), Board.Pink);
                var label = Board.Label(r, new Rect(NavX + 18, y, NavW - 18, 38), it.name, 22, Color.white);
                _mbNav.Add((bg, bar, label));
                y += 40;
            }
            _mbPane = new GameObject("中身", typeof(RectTransform)).GetComponent<RectTransform>();
            _mbPane.SetParent(r, false);
            _mbPane.anchorMin = _mbPane.anchorMax = _mbPane.pivot = new Vector2(0, 1);
            _mbPane.sizeDelta = new Vector2(MbW, MbH); _mbPane.anchoredPosition = Vector2.zero;
            Board.Label(r, new Rect(0, MbH - 58, MbW, 26), "↑↓ えらぶ　　→・Enter 決める　　←→ 値を変える　　Q・E 設定の分類　　Ctrl+Z ひとつ前にもどす　　Esc もどる・閉じる", 16, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);
        }

        /// <summary>メニューから開いた画面（地図・図鑑・着がえ・遊び方・両替）を閉じたら、メニューのこの分類にもどる。</summary>
        int _returnItem = -1;
        void Sub(Action open) { _returnItem = _mbItem; open(); }

        // ───────── ひとつ前にもどす（設定の取り消し）─────────

        struct MSnap
        {
            public float ma, am, fx, mu, look, vh; public bool mute, fp, comp, cross, bub, inv, vsnap, vvig, vwarp; public int q, vq;
        }

        readonly List<MSnap> _mbUndo = new List<MSnap>();

        MSnap Snap() => new MSnap
        {
            ma = SoundPrefs.Master, am = SoundPrefs.Ambient, fx = SoundPrefs.Effects, mu = SoundPrefs.Music, mute = SoundPrefs.Muted,
            q = Quality.Level, fp = cam != null && cam.firstPerson, comp = CompassOn, cross = CrosshairOn, bub = Crowd.BubblesOn,
            look = Act.LookSpeed, inv = Act.InvertY,
            vq = Vr.QualityLevel, vsnap = Vr.SnapTurn, vvig = Vr.Vignette, vwarp = Vr.Warp, vh = Vr.Height,
        };

        void Restore(MSnap s)
        {
            if (SoundPrefs.Master != s.ma) SoundPrefs.Master = s.ma;
            if (SoundPrefs.Ambient != s.am) SoundPrefs.Ambient = s.am;
            if (SoundPrefs.Effects != s.fx) SoundPrefs.Effects = s.fx;
            if (SoundPrefs.Music != s.mu) SoundPrefs.Music = s.mu;
            if (SoundPrefs.Muted != s.mute) SoundPrefs.Muted = s.mute;
            if (Quality.Level != s.q) Quality.Apply(s.q);
            if (cam != null && cam.firstPerson != s.fp) cam.Toggle();
            if (CompassOn != s.comp) CompassOn = s.comp;
            if (CrosshairOn != s.cross) CrosshairOn = s.cross;
            if (Crowd.BubblesOn != s.bub) Crowd.BubblesOn = s.bub;
            if (Act.LookSpeed != s.look) Act.LookSpeed = s.look;
            if (Act.InvertY != s.inv) Act.InvertY = s.inv;
            if (Vr.QualityLevel != s.vq) Vr.QualityLevel = s.vq;
            if (Vr.SnapTurn != s.vsnap) Vr.SnapTurn = s.vsnap;
            if (Vr.Vignette != s.vvig) Vr.Vignette = s.vvig;
            if (Vr.Warp != s.vwarp) Vr.Warp = s.vwarp;
            if (Vr.Height != s.vh) Vr.Height = s.vh;
        }

        /// <summary>設定を変えるときに通す：変わっていたら、変える前の値を取り消し用に積む。</summary>
        void Track(Action a)
        {
            var before = Snap();
            a();
            if (!before.Equals(Snap())) { _mbUndo.Add(before); if (_mbUndo.Count > 50) _mbUndo.RemoveAt(0); }
        }

        void Undo()
        {
            if (_mbUndo.Count == 0) { Toast("もどせる変更はありません"); return; }
            Restore(_mbUndo[_mbUndo.Count - 1]);
            _mbUndo.RemoveAt(_mbUndo.Count - 1);
            Sfx.Click();
        }

        void UndoRow() => AddRow("ひとつ前にもどす", "直前に変えた設定を取り消します（Ctrl+Z・Backspace）", () => _mbUndo.Count > 0 ? $"{_mbUndo.Count} 回分" : "なし", null, Undo, null, false);

        /// <summary>確認用（tour の menutest）：取り消しを試して記録し、メニューから地図を開く。</summary>
        public static void TestMenu()
        {
            if (_i == null) return;
            var h = _i; h._menu = true; h.BuildMenuBoard(); h._mbItem = 5; h.ShowMenuPane();
            bool c0 = CompassOn; float m0 = SoundPrefs.Master;
            h.Track(() => CompassOn = !CompassOn);
            h.Track(() => SoundPrefs.Master = 0.3f);
            h.Undo(); bool okM = Mathf.Approximately(SoundPrefs.Master, m0);
            h.Undo(); bool okC = CompassOn == c0;
            Debug.Log($"[MenuTest] undoMaster={okM} undoCompass={okC} left={h._mbUndo.Count}");
            h._mbItem = 1; h.MItems[1].act();
            Debug.Log($"[MenuTest] mapOpen={h._map} menu={h._menu} return={h._returnItem}");
        }
        /// <summary>確認用（tour の vr:settings）：VR の設定を変えて、取り消しと初期設定を試して記録する。</summary>
        public static void TestVrSettings()
        {
            if (_i == null) return;
            var h = _i; h._menu = true; h.BuildMenuBoard(); h._mbItem = 7; h._mbFocus = 1; h.ShowMenuPane();
            string Now() => $"画質={Vr.QualityName} 回り方={(Vr.SnapTurn ? "一気に" : "少しずつ")} ふち={Vr.Vignette} ワープ={Vr.Warp} 背={Vr.HeightText}";
            Debug.Log($"[VrTest] 行={h._mbRows.Count} 最初：{Now()}");
            foreach (var row in h._mbRows) if (row.change != null) row.change(1);
            Debug.Log($"[VrTest] ▶ を全部：{Now()} 取り消し={h._mbUndo.Count}");
            h.Undo(); h.Undo();
            Debug.Log($"[VrTest] 2回もどす：{Now()} 取り消し={h._mbUndo.Count}");
            h._mbRows[h._mbRows.Count - 1].press();
            Debug.Log($"[VrTest] 初期設定：{Now()}");
            h.ShowMenuPane();
        }

        public static void TestCloseMap() { if (_i != null) _i._map = false; }

        static void NoSelect() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }

        /// <summary>右の中身を、いまえらんでいる分類で作りなおす。</summary>
        void ShowMenuPane()
        {
            for (int i = _mbPane.childCount - 1; i >= 0; i--) Destroy(_mbPane.GetChild(i).gameObject);
            _mbRows.Clear(); _mbLive.Clear(); _mbBars.Clear();
            var it = MItems[_mbItem];
            var p = _mbPane;
            Board.Display(p, new Rect(PaneX, 136, PaneW, 30), it.en, 24, Board.Teal, TextAnchor.MiddleLeft);
            Board.Label(p, new Rect(PaneX, 166, PaneW, 48), it.name, 34, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            Board.Panel(p, new Rect(PaneX, 216, 140, 2), Board.Vermilion);
            Board.Label(p, new Rect(PaneX, 228, PaneW, 30), it.desc, 19, new Color(1, 1, 1, 0.75f));

            if (_mbConfirm && it.name == "データの初期化")
            {
                Board.Label(p, new Rect(PaneX, 300, PaneW, 40), "遊んだ記録を消して、最初からやりなおしますか？（もどせません）", 24, Board.Pink);
                AddRow("はい、初期化する", "記録を消して、ツクヨミを起動しなおします", null, null, () => GameSession.Instance?.ResetData());
                AddRow("いいえ", "メニューにもどります", null, null, () => { _mbConfirm = false; _mbFocus = 0; ShowMenuPane(); });
                _mbRow = Mathf.Clamp(_mbRow, 0, 1);
                LayoutRows(360);
                return;
            }
            if (_mbConfirm)
            {
                Board.Label(p, new Rect(PaneX, 300, PaneW, 40), "ログアウトして、ツクヨミを終わりますか？", 26, Color.white);
                AddRow("はい、終わる", "保存してから終わります", null, null, () => GameSession.Instance?.Logout());
                AddRow("いいえ", "メニューにもどります", null, null, () => { _mbConfirm = false; _mbFocus = 0; ShowMenuPane(); });
                _mbRow = Mathf.Clamp(_mbRow, 0, 1);
                LayoutRows(360);
                return;
            }
            switch (it.name)
            {
                case "音":
                    Slider("全体", "ツクヨミの音ぜんぶ", () => SoundPrefs.Master, v => SoundPrefs.Master = v);
                    Slider("環境音", "波・水・ざわめき・風鈴・祭囃子・花火", () => SoundPrefs.Ambient, v => SoundPrefs.Ambient = v);
                    Slider("効果音", "足音・ボタン・買い物・釣り・合戦", () => SoundPrefs.Effects, v => SoundPrefs.Effects = v);
                    Slider("ライブの音楽", "ヤチヨのライブの曲", () => SoundPrefs.Music, v => SoundPrefs.Music = v);
                    Toggle("ミュート", "すべての音を消す", () => SoundPrefs.Muted, v => SoundPrefs.Muted = v);
                    UndoRow();
                    AddRow("初期設定にもどす", "音の設定を最初の値にもどします", null, null, () => { SoundPrefs.Master = 0.8f; SoundPrefs.Ambient = 0.7f; SoundPrefs.Effects = 0.8f; SoundPrefs.Music = 0.8f; SoundPrefs.Muted = false; Sfx.Click(); });
                    break;
                case "画面と表示":
                    AddRow("画質", "重いときは「軽い」に。きれいなほど重くなります", () => Quality.Name, d => Quality.Apply((Quality.Level + d + 3) % 3), () => Quality.Next());
                    AddRow("視点", "V でも切りかえられます", () => cam != null && cam.firstPerson ? "一人称" : "三人称", d => cam?.Toggle(), () => cam?.Toggle());
                    Toggle("方位の帯", "画面の上の方角と名所の向き", () => CompassOn, v => CompassOn = v);
                    Toggle("照準", "一人称のときの、まん中の印", () => CrosshairOn, v => CrosshairOn = v);
                    Toggle("人の吹き出し", "近くの人のひとこと", () => Crowd.BubblesOn, v => Crowd.BubblesOn = v);
                    AddRow("表示を消す", "景色だけにする（H でもどす）", null, null, () => { HideAll = true; _hideHintT = 2.5f; _menu = false; });
                    UndoRow();
                    AddRow("初期設定にもどす", "画面と表示の設定を最初の値にもどします", null, null, () => { Quality.Apply(1); CompassOn = true; CrosshairOn = true; Crowd.BubblesOn = true; Sfx.Click(); });
                    break;
                case "操作":
                    AddRow("見回す速さ", "マウスと右スティックで見回す速さ", () => $"× {Act.LookSpeed:0.0}", d => Act.LookSpeed = Mathf.Round((Act.LookSpeed + d * 0.1f) * 10f) / 10f, null);
                    Toggle("上下を逆にする", "マウスを上に動かすと下を向く", () => Act.InvertY, v => Act.InvertY = v);
                    UndoRow();
                    AddRow("初期設定にもどす", "操作の設定を最初の値にもどします", null, null, () => { Act.LookSpeed = 1f; Act.InvertY = false; Sfx.Click(); });
                    LayoutRows(276);
                    Board.Label(p, new Rect(PaneX, 276 + 4 * 58 + 18, PaneW, 26), "キーの一覧", 18, Board.Cyan);
                    Board.Label(p, new Rect(PaneX, 276 + 4 * 58 + 46, PaneW, 150),
                        "WASD・矢印　歩く　　Shift　走る　　Space　ジャンプ　　V　視点　　ホイール　近づく・離れる\n" +
                        "E　お店・話す・座る・釣り　　C　着がえ　　M　地図　　B　図鑑　　H　表示を消す\n" +
                        "F12　写真　　Esc　閉じる・メニュー　　（KASSEN の操作は、それぞれの板に出ます）", 17, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft);
                    return;
                case "VR":
                    AddRow("画質", Vr.On ? "VR 用。カクカクするときは「軽い」に" : "VR 用（いまは画面モード。VR で遊ぶときに効きます）", () => Vr.QualityName, d => Vr.QualityLevel = (Vr.QualityLevel + d + 3) % 3, () => Vr.QualityLevel = (Vr.QualityLevel + 1) % 3);
                    AddRow("回り方", "右スティックを左右に倒したとき（一気に＝45度ずつ・酔いにくい）", () => Vr.SnapTurn ? "一気に" : "少しずつ", d => Vr.SnapTurn = !Vr.SnapTurn, () => Vr.SnapTurn = !Vr.SnapTurn);
                    Toggle("視界のふちを暗くする", "歩いたり回ったりしている間だけ。酔いにくくなります", () => Vr.Vignette, v => Vr.Vignette = v);
                    Toggle("ワープ移動", "右スティックを前に倒して行き先をえらび、離すとそこへ移る", () => Vr.Warp, v => Vr.Warp = v);
                    AddRow("SETSUNA の酔い防止", "揺れ＝当たったときの視界の揺れをなくす　体だけ＝視界は回らず体だけ相手へ（見えなくなったら一気に向く）", () => FightComfortName[FightComfort], d => FightComfort = (FightComfort + d + 4) % 4, () => FightComfort = (FightComfort + 1) % 4);
                    AddRow("背の高さ", "◀▶ で目の高さを上げ下げ。決定で、いまの背に合わせる", () => Vr.HeightText, d => Vr.Height += d * 0.05f, () => Vr.FitHeight());
                    UndoRow();
                    AddRow("初期設定にもどす", "VR の設定を最初の値にもどします", null, null, () => { Vr.ResetSettings(); Sfx.Click(); });
                    break;
                default:
                    AddRow("開く", it.desc, null, null, it.act);
                    break;
            }
            LayoutRows(276);
        }

        static readonly string[] FightComfortName = { "なし", "揺れを止める", "体だけ向く", "両方" };
        /// <summary>SETSUNA の酔い防止（0 なし・1 揺れを止める・2 体だけ相手へ向く・3 両方）。</summary>
        static int FightComfort
        {
            get => (Vr.FightNoShake ? 1 : 0) + (Vr.FightBodyOnly ? 2 : 0);
            set { Vr.FightNoShake = (value & 1) != 0; Vr.FightBodyOnly = (value & 2) != 0; }
        }

        void Slider(string label, string hint, Func<float> get, Action<float> set)
            => AddRow(label, hint, () => $"{get() * 100:0}%", d => { set(Mathf.Clamp01(Mathf.Round(get() * 10f + d) / 10f)); Sfx.Click(); }, null, get);

        void Toggle(string label, string hint, Func<bool> get, Action<bool> set)
            => AddRow(label, hint, () => get() ? "オン" : "オフ", d => set(!get()), () => set(!get()));

        readonly List<Func<float>> _mbBars = new List<Func<float>>();

        void AddRow(string label, string hint, Func<string> value, Action<int> change, Action press, Func<float> bar = null, bool track = true)
        {
            if (track && change != null) { var c0 = change; change = d => Track(() => c0(d)); }
            if (track && press != null) { var p0 = press; press = () => Track(p0); }
            _mbRows.Add(new MRow { label = label, hint = hint, value = value, change = change, press = press });
            _mbBars.Add(bar);
        }

        /// <summary>行を並べる（左に名前と説明、右に ◀ 値 ▶）。</summary>
        void LayoutRows(float y0)
        {
            var p = _mbPane;
            for (int i = 0; i < _mbRows.Count; i++)
            {
                var row = _mbRows[i]; int idx = i;
                float y = y0 + i * 58;
                row.bg = Board.Panel(p, new Rect(PaneX - 10, y, PaneW + 10, 52), new Color(1, 1, 1, 0));
                row.bg.raycastTarget = true;
                var b = row.bg.gameObject.AddComponent<UnityEngine.UI.Button>();
                b.transition = Selectable.Transition.None; b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { NoSelect(); _mbFocus = 1; _mbRow = idx; if (row.press != null) row.press(); else row.change?.Invoke(1); });
                Board.Panel(p, new Rect(PaneX - 10, y + 52, PaneW + 10, 1), new Color(1, 1, 1, 0.12f));
                Board.Label(p, new Rect(PaneX + 6, y + 2, 420, 30), row.label, 22, Color.white);
                Board.Label(p, new Rect(PaneX + 6, y + 28, 520, 22), row.hint, 15, new Color(1, 1, 1, 0.55f));
                if (row.value == null) continue;
                float vx = PaneX + PaneW - 290;
                if (row.change != null)
                {
                    var l = Board.ButtonNode(p, new Rect(vx, y + 8, 40, 36), "◀", 18, () => { NoSelect(); _mbFocus = 1; _mbRow = idx; row.change(-1); });
                    l.navigation = new Navigation { mode = Navigation.Mode.None };
                    var rr = Board.ButtonNode(p, new Rect(vx + 236, y + 8, 40, 36), "▶", 18, () => { NoSelect(); _mbFocus = 1; _mbRow = idx; row.change(1); });
                    rr.navigation = new Navigation { mode = Navigation.Mode.None };
                }
                var bar = idx < _mbBars.Count ? _mbBars[idx] : null;
                if (bar != null)
                {
                    Board.Panel(p, new Rect(vx + 52, y + 38, 172, 4), new Color(1, 1, 1, 0.15f));
                    var fill = Board.Panel(p, new Rect(vx + 52, y + 38, 172, 4), Board.Cyan);
                    var ft = (RectTransform)fill.transform; ft.pivot = new Vector2(0, 1);
                    ft.anchoredPosition = new Vector2(vx + 52, -(y + 38));
                    _mbLive.Add((null, () => { ft.localScale = new Vector3(bar(), 1, 1); return null; }));
                }
                var vt = Board.Label(p, new Rect(vx + 46, y + 4, 184, 34), "", 22, Board.Gold, TextAnchor.MiddleCenter);
                _mbLive.Add((vt, row.value));
            }
            _mbBars.Clear();
        }

        /// <summary>Esc：右の行にいれば左へもどる・確かめの画面ならやめる。使ったら true。</summary>
        bool MenuBack()
        {
            if (_mbConfirm) { _mbConfirm = false; _mbFocus = 0; ShowMenuPane(); return true; }
            if (_mbFocus == 1) { _mbFocus = 0; return true; }
            return false;
        }

        /// <summary>毎フレーム：表示・キー操作・値の文字。</summary>
        void MenuTick()
        {
            // メニューから開いた画面を閉じたら、メニューにもどる
            if (_returnItem >= 0 && !_menu && !_map && !_book && !ShopOpen && !_guide && !_exchange)
            {
                MenuStart = _returnItem; _returnItem = -1; _menu = true;
            }
            if (_menu && _mb == null) BuildMenuBoard();
            if (_mb == null) return;
            _mb.Alpha = _menu ? 1f : 0f;
            if (_menu && !_mbWasOpen && MenuStart < 0) _mbUndo.Clear(); // 新しく開いたときだけ、取り消しの履歴をまっさらに
            if (_menu && !_mbWasOpen) { _mbItem = Mathf.Clamp(MenuStart, 0, MItems.Length - 1); MenuStart = -1; _mbRow = 0; _mbFocus = 0; _mbConfirm = false; ShowMenuPane(); }
            _mbWasOpen = _menu;
            if (!_menu) return;
            if (MenuStart >= 0) { _mbItem = Mathf.Clamp(MenuStart, 0, MItems.Length - 1); MenuStart = -1; _mbRow = 0; _mbFocus = 1; ShowMenuPane(); }

            var k = Keyboard.current; var gp = Gamepad.current;
            bool Key(Key key) => k != null && k[key].wasPressedThisFrame;
            bool up = Key(UnityEngine.InputSystem.Key.UpArrow) || Key(UnityEngine.InputSystem.Key.W) || (gp != null && gp.dpad.up.wasPressedThisFrame);
            bool down = Key(UnityEngine.InputSystem.Key.DownArrow) || Key(UnityEngine.InputSystem.Key.S) || (gp != null && gp.dpad.down.wasPressedThisFrame);
            bool left = Key(UnityEngine.InputSystem.Key.LeftArrow) || Key(UnityEngine.InputSystem.Key.A) || (gp != null && gp.dpad.left.wasPressedThisFrame);
            bool right = Key(UnityEngine.InputSystem.Key.RightArrow) || Key(UnityEngine.InputSystem.Key.D) || (gp != null && gp.dpad.right.wasPressedThisFrame);
            bool ok = Key(UnityEngine.InputSystem.Key.Enter) || Key(UnityEngine.InputSystem.Key.NumpadEnter) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            bool prevTab = Key(UnityEngine.InputSystem.Key.Q) || (gp != null && gp.leftShoulder.wasPressedThisFrame);
            bool nextTab = Key(UnityEngine.InputSystem.Key.E) || (gp != null && gp.rightShoulder.wasPressedThisFrame);
            bool undo = (k != null && ((k.ctrlKey.isPressed && k.zKey.wasPressedThisFrame) || k.backspaceKey.wasPressedThisFrame)) || (gp != null && gp.buttonWest.wasPressedThisFrame);
            if (undo) Undo();

            if (prevTab || nextTab)
            {
                // 設定の分類だけを順にまわる
                var pages = new List<int>();
                for (int i = 0; i < MItems.Length; i++) if (MItems[i].kind == MKind.Page) pages.Add(i);
                int at = pages.IndexOf(_mbItem);
                at = at < 0 ? 0 : (at + (nextTab ? 1 : -1) + pages.Count) % pages.Count;
                _mbItem = pages[at]; _mbRow = 0; _mbConfirm = false; ShowMenuPane(); Sfx.Click();
            }
            else if (_mbFocus == 0 && !_mbConfirm)
            {
                if (up || down) { _mbItem = (_mbItem + (down ? 1 : -1) + MItems.Length) % MItems.Length; _mbRow = 0; ShowMenuPane(); Sfx.Click(); }
                else if (ok || right)
                {
                    var it = MItems[_mbItem];
                    if (it.kind == MKind.Action) it.act();
                    else if (_mbRows.Count > 0) _mbFocus = 1;
                }
            }
            else if (_mbRows.Count > 0)
            {
                _mbFocus = 1;
                _mbRow = Mathf.Clamp(_mbRow, 0, _mbRows.Count - 1);
                var row = _mbRows[_mbRow];
                if (up || down) { _mbRow = (_mbRow + (down ? 1 : -1) + _mbRows.Count) % _mbRows.Count; Sfx.Click(); }
                else if ((left || right) && row.change != null) row.change(right ? 1 : -1);
                else if (left && !_mbConfirm) _mbFocus = 0;
                else if (ok) { if (row.press != null) row.press(); else row.change?.Invoke(1); }
            }

            // 見た目：えらんでいる分類と行を光らせる
            for (int i = 0; i < _mbNav.Count; i++)
            {
                bool sel = i == _mbItem;
                _mbNav[i].bg.color = sel ? new Color(Board.Cyan.r * 0.3f, Board.Cyan.g * 0.3f, Board.Cyan.b * 0.35f, _mbFocus == 0 ? 0.9f : 0.5f) : new Color(1, 1, 1, 0);
                _mbNav[i].bar.enabled = sel;
                _mbNav[i].label.color = sel ? Color.white : new Color(1, 1, 1, 0.7f);
            }
            for (int i = 0; i < _mbRows.Count; i++)
                if (_mbRows[i].bg != null) _mbRows[i].bg.color = _mbFocus == 1 && i == _mbRow ? new Color(Board.Pink.r * 0.35f, Board.Pink.g * 0.2f, Board.Pink.b * 0.3f, 0.75f) : new Color(1, 1, 1, 0);
            foreach (var (t, f) in _mbLive) { var s = f(); if (t != null) t.text = s; }
            var now = Ts.Now;
            _mbInfo.text = $"{now:HH:mm}　{(_zone != null ? _zone.title : "ツクヨミ")}　　ふじゅ〜 {Mathf.Max(0, Fuju.Last):N0}";
        }
    }
}
