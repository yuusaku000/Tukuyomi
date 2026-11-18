using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// 初めてのログインの演出（2026-11-18 ユーザーと決めた流れ）。
    ///  1. 昼の「海の大鳥居」の高舞台。町の人は隠し、ヤチヨが空から降りてくる。
    ///  2. 「ツクヨミへようこそ！」「そんな格好じゃあ遊べないよ！」
    ///  3. 無料の着がえ：服・飾り・髪の形・髪の色を1つずつ（お店の全部の中から）。
    ///  4. 「いい格好じゃーん！」「それでは……日が沈み、夜がやってきます」→ 夜にして、島のほうの景色を見せる。
    ///  5. 「じゃあ、いってらっしゃーい！」→ 自由に遊ぶ（遊び方の板が出る）。
    ///  ・この夜は、次の現実の日の出（朝6時）まで続く（ログアウトしても）。ヤチヨの「もとの時間にもどして」ですぐもどる。
    ///  ・初ログインボーナス（5,000 ふじゅ〜）は、月見商店街に着いたときに受け取る板を出す。
    /// 板（Board）で作るので、VR でも光の線とトリガーで進められる。
    /// </summary>
    public partial class Hud
    {
        static bool _intro, _introDress, _bonusOpen;
        public static bool IntroOn => _intro;
        static bool _introHold; // 確認用（tour）：セリフで止めておく

        Board _ib, _ibDress, _ibBonus;
        Text _ibLine;
        GameObject _ibNextGo;
        Text _ibNextText;
        bool _ibWait, _ibAdvance;
        string _dressKey;
        int _dTab;
        Item _dClothes, _dAcc;
        Color _dColor = new Color(0.16f, 0.12f, 0.16f);
        int _dStyle = 1;
        bool _dDone;
        GameObject _crowdHidden;

        // ───────── はじめる ─────────

        public static void StartIntro()
        {
            if (_i == null) return;
            _i._introCo = _i.StartCoroutine(_i.IntroRun(0));
        }

        /// <summary>確認用（--tour の intro・intro:dress・intro:bye・bonus）。そのセリフ・場面で止める。</summary>
        public static void TestIntro(string step)
        {
            if (_i == null) return;
            _introHold = true;
            if (_i._introCo != null) { _i.StopCoroutine(_i._introCo); _i._introCo = null; } // 前の場面を止める
            _introDress = false; _bonusOpen = false;
            if (step == "bonus") { var d = Data; if (d != null) { d.introDone = true; d.fujuBonus = false; } _bonusOpen = true; return; }
            _i._introCo = _i.StartCoroutine(_i.IntroRun(step == "dress" ? 1 : step == "bye" ? 2 : 0));
        }
        Coroutine _introCo;

        IEnumerator IntroRun(int from)
        {
            _intro = true;
            var d = Data;
            if (d != null && !Ts.ShotMode) { d.introStarted = true; GameSession.Instance.SaveItems(); }
            // 「画面で遊ぶ／VR で遊ぶ」をえらんでいる間は待つ
            while (Vr.Choosing) yield return null;
            yield return null;
            var ya = YachiyoNpc.I;
            // 昼にする・町の人を隠す
            if (from < 2) Ts.SetTime("12:00");
            var crowd = FindFirstObjectByType<Crowd>();
            if (crowd != null) { crowd.gameObject.SetActive(false); _crowdHidden = crowd.gameObject; }
            // ヤチヨの前（話しかける場所）に立つ
            float faceYaw = 0;
            if (ya != null && player != null)
            {
                var v = ya.transform.position - ya.stand; v.y = 0;
                faceYaw = v.sqrMagnitude > 0.01f ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : ya.baseYaw + 180;
                // 話しかける場所から少しうしろ（ヤチヨと重ならないように）
                var back = -v.normalized * 0.9f;
                player.Teleport(ya.stand + back + Vector3.up * 0.1f, faceYaw);
                YachiyoNpc.Talking = true;
            }
            if (cam != null)
            {
                if (Vr.On) cam.yaw = faceYaw;
                else { cam.firstPerson = false; cam.yaw = faceYaw + 28f; cam.pitch = 10f; cam.distance = 4.6f; }
                cam.Snap();
            }

            if (from == 0)
            {
                // ヤチヨが空から降りてくる
                if (ya != null)
                {
                    var home = ya.transform.position;
                    for (float t = 0; t < 2.4f; t += Time.deltaTime)
                    {
                        float k = Mathf.SmoothStep(0, 1, t / 2.4f);
                        ya.transform.position = Vector3.Lerp(home + Vector3.up * 7f, home, k);
                        yield return null;
                    }
                    ya.transform.position = home;
                }
                yield return Say("ヤオヨロ〜！ ツクヨミへようこそ！");
                yield return Say("……って、ちょっと待って！ そんな格好じゃあ遊べないよ！");
                yield return Say("今日はとくべつに、服と飾りと髪型を1つずつプレゼント！ 好きなのをえらんでね");
            }
            if (from <= 1)
            {
                // 無料の着がえ
                HideLine();
                _dDone = false; _dressKey = null; _dTab = 0;
                var ward = Ward;
                _dClothes = Catalog.Get(d != null ? d.clothes : Catalog.StartClothes);
                _dAcc = null;
                if (ward != null) { _dColor = ward.HairColor; _dStyle = ward.HairStyle; }
                _introDress = true;
                // 自分は横を向き、正面から写す（ヤチヨがカメラと自分の間に入らないように）
                if (player != null) player.transform.rotation = Quaternion.Euler(0, faceYaw - 90f, 0);
                if (!Vr.On) FrontCam(faceYaw - 90f + 180f);
                else if (cam != null) cam.yaw = faceYaw - 90f; // VR：板がヤチヨと重ならないように、横を向く
                while (!_dDone) yield return null;
                _introDress = false;
                RestoreCam();
                if (cam != null && !Vr.On) { cam.firstPerson = false; cam.yaw = faceYaw + 28f; cam.pitch = 10f; cam.distance = 4.6f; cam.Snap(); }
                if (player != null) player.transform.rotation = Quaternion.Euler(0, faceYaw, 0);
                if (Vr.On && cam != null) cam.yaw = faceYaw;
                yield return Say("いい格好じゃーん！");
            }
            // 夜にして、島のほうの景色を見せる
            yield return Say("それでは……日が沈み、夜がやってきます", 2.6f);
            HideLine();
            if (ya != null) ya.MakeNight();
            YachiyoNpc.Talking = false;
            if (cam != null)
            {
                // 大鳥居から北（島のほう）を見る
                if (Vr.On) cam.yaw = 0;
                else { cam.firstPerson = false; cam.yaw = 0; cam.pitch = 4f; cam.distance = 3.2f; }
                cam.Snap();
            }
            if (player != null) player.transform.rotation = Quaternion.Euler(0, 0, 0);
            float wait = Ts.ShotMode && from == 2 ? 0.5f : 11.5f;
            for (float t = 0; t < wait; t += Time.deltaTime) yield return null;
            yield return Say("じゃあ、いってらっしゃーい！", 0, "いってきます ▶");
            EndIntro();
        }

        void EndIntro()
        {
            HideLine();
            _intro = false; _introDress = false; _introHold = false;
            YachiyoNpc.Talking = false;
            if (_crowdHidden != null) { _crowdHidden.SetActive(true); _crowdHidden = null; }
            var d = Data;
            if (d != null && !Ts.ShotMode)
            {
                d.introDone = true;
                d.nightUntil = NextSunrise().ToString("yyyy-MM-dd HH:mm");
                d.has = true;
                GameSession.Instance.SaveItems();
            }
            if (cam != null && !Vr.On) { cam.firstPerson = false; cam.pitch = 14f; cam.distance = 6f; cam.Snap(); }
            Toast("参道の橋をわたって、月見商店街へ行ってみよう！");
        }

        /// <summary>次の現実の日の出（朝6時）。</summary>
        static DateTime NextSunrise()
        {
            var now = DateTime.Now;
            var six = now.Date.AddHours(6);
            return now < six ? six : six.AddDays(1);
        }

        // ───────── セリフ ─────────

        IEnumerator Say(string line, float auto = 0, string next = "次へ ▶")
        {
            BuildIntroLine();
            _ibLine.text = line;
            _ibNextText.text = next;
            _ibNextGo.SetActive(auto <= 0);
            _ib.Alpha = 1;
            Sfx.Click();
            _ibAdvance = false; _ibWait = auto <= 0;
            float t = 0;
            while (true)
            {
                t += Time.deltaTime;
                if (auto > 0 && t >= auto) break;
                if (auto <= 0 && _ibAdvance) break;
                if (Ts.ShotMode && !_introHold && t > 1.2f) break; // 確認用：自動で進む
                yield return null;
            }
            _ibWait = false;
        }

        void HideLine() { if (_ib != null) _ib.Alpha = 0; }

        void BuildIntroLine()
        {
            if (_ib != null) return;
            const float w = 1100, h = 230;
            _ib = Board.Create("ヤチヨのことば", new Vector2(w, h), 35);
            _ib.screenAnchor = new Vector2(0.5f, 0); _ib.screenOffset = new Vector2(0, 70);
            _ib.worldOffset = new Vector3(0, -0.28f, 1.15f);
            _ib.Apply();
            var r = _ib.root;
            Board.Panel(r, new Rect(0, 30, w, h - 30), new Color(0.01f, 0.03f, 0.08f, 0.9f));
            Board.Corners(r, new Rect(0, 30, w, h - 30), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.9f), 18, 3);
            Board.Panel(r, new Rect(0, 30, 6, h - 30), Board.Pink);
            Board.Panel(r, new Rect(34, 0, 200, 48), new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.95f));
            Board.Label(r, new Rect(34, 0, 200, 48), "ヤチヨ", 28, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _ibLine = Board.Wrap(r, new Rect(50, 70, w - 340, 140), "", 32, Color.white, TextAnchor.UpperLeft);
            var b = Board.Btn(r, new Rect(w - 270, h - 96, 240, 66), "次へ ▶", 26, () => _ibAdvance = true);
            _ibNextGo = b.gameObject; _ibNextText = b.GetComponentInChildren<Text>();
        }

        // ───────── 無料の着がえ ─────────

        /// <summary>はじめての着がえでえらべる服（全部だと多すぎるので、浴衣・振袖・ふだん着・巫女・ドレス・十二単から20種類）。</summary>
        static readonly string[] GiftClothes =
        {
            "sakura", "aizome", "asagao", "kingyo_y", "hanabi",
            "botan", "wakatake", "yukiusagi", "hoshizora", "tsukiyo", "kinran",
            "momiji", "samue", "jogakusei", "miko",
            "dress_sakura", "dress_sora", "dress_kurobara",
            "juni_sakura", "juni_tsukikage",
        };

        static List<Item> Gifts(ItemKind k)
        {
            var l = new List<Item>();
            if (k == ItemKind.Clothes)
            {
                foreach (var id in GiftClothes) { var it = Catalog.Get(id); if (it != null) l.Add(it); }
                return l;
            }
            foreach (var it in Catalog.Of(k)) if (!string.IsNullOrEmpty(it.shop) && it.price > 0) l.Add(it);
            return l;
        }

        void DressPreview()
        {
            var w = Ward;
            if (w == null) return;
            var acc = new List<string>();
            if (_dAcc != null) acc.Add(_dAcc.id);
            w.Show(_dClothes != null ? _dClothes.id : Catalog.StartClothes, acc);
            w.SetHair(_dColor, _dStyle);
        }

        void DressConfirm()
        {
            var d = Data; var w = Ward;
            if (d != null)
            {
                if (_dClothes != null) { if (!d.owned.Contains(_dClothes.id)) d.owned.Add(_dClothes.id); d.clothes = _dClothes.id; }
                d.wearing.Clear();
                if (_dAcc != null) { if (!d.owned.Contains(_dAcc.id)) d.owned.Add(_dAcc.id); d.wearing.Add(_dAcc.id); }
                d.hairSet = true; d.hairColor = _dColor; d.hairStyle = _dStyle;
                if (!Ts.ShotMode) GameSession.Instance.SaveItems();
                if (w != null) { w.Wear(d.clothes, d.wearing); w.SetHair(_dColor, _dStyle); }
            }
            Sfx.Coin();
            _dDone = true;
        }

        const float DbW = 760, DbH = 900;

        void DressBoardTick()
        {
            if (!_introDress) { if (_ibDress != null) _ibDress.Alpha = 0; _dressKey = null; return; }
            if (_ibDress == null)
            {
                _ibDress = Board.Create("はじめての着がえ", new Vector2(DbW, DbH), 30);
                _ibDress.screenAnchor = new Vector2(1, 0.5f); _ibDress.screenOffset = new Vector2(-40, 0);
                _ibDress.worldOffset = new Vector3(0.3f, -0.1f, 1.15f);
                _ibDress.Apply();
            }
            _ibDress.Alpha = 1;
            string key = $"{_dTab}|{_dClothes?.id}|{_dAcc?.id}|{_dColor}|{_dStyle}";
            if (key == _dressKey) return;
            _dressKey = key;
            DressPreview();
            var r = _ibDress.root;
            Board.Clear(r);
            if (Board.Current != Board.Mode.Screen) Board.Dim(r, DbW, DbH);
            Board.Frame(r, new Rect(0, 0, DbW, DbH));
            Board.Label(r, new Rect(36, 26, 420, 24), "WELCOME GIFT", 18, Board.Cyan, TextAnchor.MiddleLeft, true);
            Board.Label(r, new Rect(34, 50, 500, 52), "はじめての着がえ", 38, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            Board.Label(r, new Rect(DbW - 420, 34, 384, 26), "ヤチヨからのプレゼント（無料）", 19, Board.Gold, TextAnchor.MiddleRight);
            Board.Label(r, new Rect(DbW - 420, 62, 384, 22), "それぞれ1つずつ。ほかは、あとでお店で買えます", 15, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleRight);
            Board.Panel(r, new Rect(36, 108, 220, 2), Board.Vermilion);
            string[] tabs = { "服", "飾り", "髪の形", "髪の色" };
            float tw = (DbW - 60 - 3 * 10) / 4f;
            for (int i = 0; i < tabs.Length; i++)
            {
                int ii = i;
                var t = Board.Pick(r, new Rect(30 + i * (tw + 10), 122, tw, 50), i == _dTab, () => _dTab = ii);
                Board.Label(t, new Rect(0, 0, tw, 50), tabs[i], 22, Color.white, TextAnchor.MiddleCenter);
            }
            float top = 186, area = 560;
            if (_dTab == 0 || _dTab == 1)
            {
                var list = Gifts(_dTab == 0 ? ItemKind.Clothes : ItemKind.Accessory);
                if (_dTab == 1) list.Insert(0, null); // 飾りなし
                int rows = Mathf.CeilToInt(list.Count / 2f);
                float step = Mathf.Min(52f, area / Mathf.Max(1, rows));
                float colW = (DbW - 70) / 2f;
                for (int k = 0; k < list.Count; k++)
                {
                    var it = list[k];
                    bool sel = _dTab == 0 ? it == _dClothes : it == _dAcc;
                    var cell = Board.Pick(r, new Rect(30 + (k % 2) * (colW + 10), top + (k / 2) * step, colW, step - 6), sel, () =>
                    {
                        if (_dTab == 0) _dClothes = it; else _dAcc = it;
                    });
                    string name = it == null ? "なし" : it.name;
                    Board.Label(cell, new Rect(14, 0, colW - 70, step - 6), name, step < 44 ? 17 : 20, Color.white);
                    if (it != null && it.kind == ItemKind.Accessory) Board.Label(cell, new Rect(colW - 64, 0, 52, step - 6), SlotName(it.slot), 15, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleRight);
                }
            }
            else if (_dTab == 2)
            {
                for (int i = 0; i < Wardrobe.HairStyles.Length; i++)
                {
                    int ii = i;
                    var cell = Board.Pick(r, new Rect(30 + (i % 3) * 236, top + (i / 3) * 76, 224, 64), i == _dStyle, () => _dStyle = ii);
                    Board.Label(cell, new Rect(0, 0, 224, 64), Wardrobe.HairStyles[i], 22, Color.white, TextAnchor.MiddleCenter);
                }
            }
            else
            {
                for (int i = 0; i < HairColors.Length; i++)
                {
                    var (name, c) = HairColors[i];
                    var cc = c;
                    var cell = Board.Pick(r, new Rect(30 + (i % 6) * 118, top + (i / 6) * 110, 106, 100), Near(c, _dColor), () => _dColor = cc);
                    Board.Panel(cell, new Rect(20, 8, 66, 56), c);
                    Board.Label(cell, new Rect(0, 66, 106, 28), name, 18, Color.white, TextAnchor.MiddleCenter);
                }
            }
            string colName = "そのまま";
            foreach (var (n, c) in HairColors) if (Near(c, _dColor)) colName = n;
            Board.Wrap(r, new Rect(36, DbH - 150, DbW - 72, 56),
                $"服：{(_dClothes != null ? _dClothes.name : "―")}　飾り：{(_dAcc != null ? _dAcc.name : "なし")}　髪：{Wardrobe.HairStyles[_dStyle]}・{colName}", 19, new Color(1, 1, 1, 0.85f));
            Board.Btn(r, new Rect(DbW - 316, DbH - 92, 280, 66), "これにする ▶", 26, DressConfirm);
        }

        // ───────── 初ログインボーナス ─────────

        void BonusTick()
        {
            var d = Data;
            if (!_bonusOpen && d != null && d.introDone && !d.fujuBonus && !_intro && !Kassen.Busy && _zone != null && _zone.id == "shopping" && !Ts.ShotMode)
                _bonusOpen = true;
            if (!_bonusOpen) { if (_ibBonus != null) _ibBonus.Alpha = 0; return; }
            if (_ibBonus == null)
            {
                const float w = 700, h = 400;
                _ibBonus = Board.Create("初ログインボーナス", new Vector2(w, h), 36);
                _ibBonus.worldOffset = new Vector3(0, -0.05f, 1.2f);
                _ibBonus.Apply();
                var r = _ibBonus.root;
                Board.Dim(r, w, h, 0.5f);
                Board.Frame(r, new Rect(0, 0, w, h));
                Board.Label(r, new Rect(0, 30, w, 26), "LOGIN BONUS", 20, Board.Cyan, TextAnchor.MiddleCenter, true);
                Board.Label(r, new Rect(0, 62, w, 56), "初ログインボーナス！", 42, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                Board.Panel(r, new Rect(w / 2 - 110, 124, 220, 2), Board.Vermilion);
                Board.Display(r, new Rect(0, 140, w, 90), $"{Fuju.LoginBonus:N0}", 72, Board.Gold);
                Board.Label(r, new Rect(0, 226, w, 30), "ふじゅ〜（ツクヨミのお金。お店や遊覧船で使えます）", 19, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter);
                Board.Btn(r, new Rect(w / 2 - 150, h - 112, 300, 72), "受け取る", 30, () =>
                {
                    var dd = Data;
                    if (dd != null && Fuju.GiveLoginBonus(dd)) { GameSession.Instance.SaveItems(); Sfx.Coin(); Toast($"{Fuju.LoginBonus:N0} ふじゅ〜を受け取りました！（残高はメニューの「ふじゅ〜と両替」）"); }
                    _bonusOpen = false;
                });
            }
            _ibBonus.Alpha = 1;
        }

        /// <summary>毎フレーム（BoardsTick から）：演出の板と、セリフを進めるキー。</summary>
        void IntroTick()
        {
            if (_ib != null && !_intro) _ib.Alpha = 0;
            if (_intro && _ibWait && (Act.Confirm.Down || Act.Interact.Down)) _ibAdvance = true;
            DressBoardTick();
            BonusTick();
            NightTick();
        }

        // ───────── 初めてのログインの夜を保つ ─────────

        /// <summary>夜の時刻にするずれ（いまが夜中なら、その日の21時へはもどさない）。YachiyoNpc.MakeNight と同じ考え方。</summary>
        static TimeSpan NightOffset()
        {
            var now = DateTime.Now;
            var target = now.Date.AddHours(21);
            if (now.Hour < 4) target = now.Date.AddHours(-3);
            return target - now;
        }

        static bool KeptNight(SaveData d, out DateTime until)
        {
            until = default;
            return d != null && !string.IsNullOrEmpty(d.nightUntil)
                && DateTime.TryParseExact(d.nightUntil, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out until);
        }

        /// <summary>起動したとき：初めてのログインの夜が続いていれば、夜にする。</summary>
        public static void KeepNightOnStart(SaveData d)
        {
            if (Ts.ShotMode || !KeptNight(d, out var until)) return;
            if (DateTime.Now >= until) { d.nightUntil = ""; return; }
            YachiyoNpc.KeepNight(NightOffset());
        }

        /// <summary>ヤチヨの「もとの時間にもどして」から：夜を保つのをやめる。</summary>
        public static void ClearKeptNight()
        {
            var d = Data;
            if (d == null || string.IsNullOrEmpty(d.nightUntil)) return;
            d.nightUntil = "";
            GameSession.Instance?.SaveItems();
        }

        float _nightCheckT;

        void NightTick()
        {
            if (_intro || Ts.ShotMode) return;
            _nightCheckT -= Time.unscaledDeltaTime;
            if (_nightCheckT > 0) return;
            _nightCheckT = 5f;
            var d = Data;
            if (!KeptNight(d, out var until) || Ts.Shifting) return;
            if (DateTime.Now >= until) { YachiyoNpc.I?.RestoreTime(); ClearKeptNight(); return; } // 日の出：現実の時刻へ
            int h = Ts.Now.Hour;
            if (h >= 4 && h < 19) YachiyoNpc.KeepNight(NightOffset()); // 長く遊んで朝になりそうなら、夜の時刻にもどす
        }
    }
}
