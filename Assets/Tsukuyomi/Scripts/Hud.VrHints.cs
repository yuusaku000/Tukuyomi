using System.Collections.Generic;
using UnityEngine;
using UI = UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// VR のときだけ板で出す案内（2026-11-18）。画面モードは今までどおり IMGUI（Hud の OnGUI）で描く。
    ///  ・近くの物の案内（「A ▸ 〜に入る」など）……目の前の少し下
    ///  ・お店・大物番付・遊覧船の看板 …………………その場に浮かぶ板（こちらを向く）
    ///  ・町の人の吹き出し …………………………………頭の上に浮かぶ板
    ///  ・方位の帯 ……………………………………………目の前の少し上
    ///  ・場所の名前 …………………………………………右上
    /// 確かめるときは --board-world（画面モードでも VR と同じ板で出す）。
    /// </summary>
    public partial class Hud
    {
        /// <summary>板で案内を出すとき（VR、または --board-world）。このときは IMGUI の同じ表示を描かない。</summary>
        static bool BoardHints => Board.Current == Board.Mode.World;

        Board _vhPrompt, _vhCompass, _vhBanner;
        UI.Text _vhPromptText, _vhPromptSub, _vhBannerSub, _vhBannerTitle;
        string _vhPromptKey, _vhBannerKey;
        readonly Dictionary<Object, Board> _vhSigns = new Dictionary<Object, Board>();
        readonly List<(Board b, Crowd.Bubble who, string text)> _vhBubbles = new List<(Board, Crowd.Bubble, string)>();
        RectTransform _vhPromptBg;
        RectTransform[] _vhTicks; UI.Text[] _vhDirs; RectTransform[] _vhDots; UI.Text[] _vhZoneT, _vhZoneD;

        const float CpW = 620, CpH = 110;

        void VrHintsTick()
        {
            bool on = BoardHints && !Kassen.Busy && !HideAll && player != null;
            bool panel = _menu || _guide || _exchange || _map || _book || _shipAsk != null || _tipOpen || _intro || _bonusOpen || _talk || ShopOpen;
            bool calm = Rest.Idle > 6f || LiveShow.AutoCam;
            bool show = on && !panel && !Blocked;
            PromptBoard(show);
            CompassBoard(show && !calm && CompassOn);
            BannerBoard(on && !panel);
            SignBoards(show && !calm);
            BubbleBoards(show && !calm);
        }

        // ───────── 近くの物の案内 ─────────

        /// <summary>いま出す案内（Hud.Fish の DrawActivity・Hud.Shop の DrawShopSigns と同じ順番）。</summary>
        string PromptNow()
        {
            if (player == null || Fishing.Busy) return null;
            string k = Vr.On ? Vr.KeyUse : "E";
            if (Rest.Sitting) return Vr.On ? Vr.KeyUse + "・スティック ▸ 立つ" : "E・WASD ▸ 立つ";
            var shop = PlayerController.Busy ? null : ShopStand.Near(player.Position, 3.2f);
            if (shop != null) return $"{k} ▸ {shop.title}に入る";
            if (PlayerController.Fly) return null;
            var f = Fishing.I;
            if (YachiyoNpc.Near(player.Position, 2.8f) != null) return $"{k} ▸ ヤチヨに話しかける";
            if (ShipBooth.Near(player.Position, 2.4f) != null) return $"{k} ▸ 遊覧船に乗る（{SkyShip.Price} ふじゅ〜）";
            if (Rest.I != null && Rest.I.Near(out _, out _)) return $"{k} ▸ すわる";
            if (RankBoard.Near(player.Position, 2.6f) != null) return $"{k} ▸ 大物番付を見る";
            if (f != null && f.CanStart) return $"{k} ▸ 釣りをする";
            return null;
        }

        void PromptBoard(bool show)
        {
            string s = show ? PromptNow() : null;
            string sub = show && SkyShip.I != null && SkyShip.I.RiderAboard && !string.IsNullOrEmpty(SkyShip.I.Status) ? "遊覧船 ▸ " + SkyShip.I.Status : null;
            if (s == null && sub == null) { if (_vhPrompt != null) _vhPrompt.Alpha = 0; return; }
            if (_vhPrompt == null)
            {
                _vhPrompt = Passive("近くの案内", new Vector2(640, 104), new Vector2(0.5f, 0), new Vector2(0, 260), new Vector3(0, -0.32f, 1.2f), 6);
                var r = _vhPrompt.root;
                _vhPromptBg = Board.Group(r, "地", new Rect(0, 0, 640, 64));
                Board.Panel(_vhPromptBg, new Rect(0, 0, 640, 64), Board.Glass);
                Board.Corners(_vhPromptBg, new Rect(0, 0, 640, 64), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.9f), 12, 2);
                _vhPromptText = Board.Label(r, new Rect(0, 0, 640, 64), "", 30, Board.Gold, TextAnchor.MiddleCenter);
                _vhPromptSub = Board.Label(r, new Rect(0, 70, 640, 32), "", 20, Board.Gold, TextAnchor.MiddleCenter);
            }
            string key = s + "|" + sub;
            if (key != _vhPromptKey)
            {
                _vhPromptKey = key;
                _vhPromptText.text = s ?? "";
                _vhPromptSub.text = sub ?? "";
                // 案内がなく遊覧船の様子だけのときは、地を出さない
                _vhPromptBg.gameObject.SetActive(s != null);
            }
            float a = Rest.Sitting && Rest.Idle > 6f ? 0.35f : 1f;
            _vhPrompt.Alpha = a;
        }

        // ───────── 方位の帯 ─────────

        void CompassBoard(bool show)
        {
            if (!show || cam == null) { if (_vhCompass != null) _vhCompass.Alpha = 0; return; }
            if (_vhCompass == null)
            {
                _vhCompass = Passive("方位の帯", new Vector2(CpW, CpH), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector3(0, 0.36f, 1.3f), 4);
                var r = _vhCompass.root;
                Board.Panel(r, new Rect(0, 0, CpW, 30), new Color(0.02f, 0.06f, 0.13f, 0.45f));
                Board.Panel(r, new Rect(0, 29, CpW, 1), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.5f));
                _vhTicks = new RectTransform[24];
                for (int i = 0; i < 24; i++) _vhTicks[i] = (RectTransform)Board.Panel(r, new Rect(0, 20, 1.5f, 10), Board.Cyan).transform;
                _vhDirs = new UI.Text[4];
                for (int i = 0; i < 4; i++) _vhDirs[i] = Board.Label(r, new Rect(0, 1, 40, 18), "NESW".Substring(i, 1), 15, Color.white, TextAnchor.MiddleCenter, true);
                _vhDots = new RectTransform[10];
                for (int i = 0; i < 10; i++) _vhDots[i] = (RectTransform)Board.Panel(r, new Rect(0, 34, 8, 8), Color.white).transform;
                _vhZoneT = new UI.Text[3]; _vhZoneD = new UI.Text[3];
                for (int i = 0; i < 3; i++)
                {
                    _vhZoneT[i] = Board.Label(r, new Rect(0, 38, 180, 20), "", 14, Color.white, TextAnchor.UpperCenter);
                    _vhZoneD[i] = Board.Label(r, new Rect(0, 54, 180, 16), "", 12, Board.Cyan, TextAnchor.UpperCenter, true);
                }
                Board.Panel(r, new Rect(CpW / 2 - 1, 0, 2, 12), Board.Pink);
            }
            _vhCompass.Alpha = 1;
            const float fov = 120f;
            float cx = CpW / 2, yaw = cam.transform.eulerAngles.y;
            float Px(float bearing) => cx + Mathf.DeltaAngle(yaw, bearing) / (fov / 2) * (CpW / 2);
            for (int i = 0; i < 24; i++)
            {
                int d = i * 15;
                float dd = Mathf.DeltaAngle(yaw, d);
                bool vis = Mathf.Abs(dd) <= fov / 2;
                _vhTicks[i].gameObject.SetActive(vis);
                if (!vis) continue;
                bool major = d % 90 == 0;
                float a = 1 - Mathf.Abs(dd) / (fov / 2);
                _vhTicks[i].anchoredPosition = new Vector2(Px(d) - 0.75f, -(30 - (major ? 10 : 5)));
                _vhTicks[i].sizeDelta = new Vector2(1.5f, major ? 10 : 5);
                _vhTicks[i].GetComponent<UI.Image>().color = new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.8f * a);
                if (major)
                {
                    var t = _vhDirs[d / 90];
                    t.rectTransform.anchoredPosition = new Vector2(Px(d) - 20, -1);
                    t.color = new Color(1, 1, 1, a);
                }
            }
            for (int i = 0; i < 4; i++) _vhDirs[i].gameObject.SetActive(Mathf.Abs(Mathf.DeltaAngle(yaw, i * 90)) <= fov / 2);

            var marks = new List<(Zone z, float dd, float dist, float bearing)>();
            foreach (var z in Zone.All)
            {
                if (!z.warpable || z == _zone) continue;
                var v = z.transform.position - player.Position; v.y = 0;
                float dist = v.magnitude;
                if (dist < 8 || dist > 450) continue;
                float bearing = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                float dd = Mathf.DeltaAngle(yaw, bearing);
                if (Mathf.Abs(dd) > fov / 2 - 4) continue;
                marks.Add((z, dd, dist, bearing));
            }
            marks.Sort((p, q) => Mathf.Abs(p.dd).CompareTo(Mathf.Abs(q.dd)));
            var placed = new List<float>();
            int label = 0;
            for (int i = 0; i < _vhDots.Length; i++)
            {
                bool vis = i < marks.Count;
                _vhDots[i].gameObject.SetActive(vis);
                if (!vis) continue;
                var (z, dd, dist, bearing) = marks[i];
                float x = Px(bearing), a = 1 - Mathf.Abs(dd) / (fov / 2) * 0.7f;
                var c = z.mapColor; c.a = a;
                _vhDots[i].anchoredPosition = new Vector2(x - 4, -34);
                _vhDots[i].GetComponent<UI.Image>().color = c;
                // 名前は近い向きの3つまで（重なるものは出さない。板では段を分けない）
                if (label >= 3 || i >= 3) continue;
                bool clash = false;
                foreach (var px in placed) if (Mathf.Abs(px - x) < 170) clash = true;
                if (clash) continue;
                placed.Add(x);
                _vhZoneT[label].text = z.title; _vhZoneT[label].color = new Color(1, 1, 1, a * 0.9f);
                _vhZoneT[label].rectTransform.anchoredPosition = new Vector2(x - 90, -44);
                _vhZoneD[label].text = $"{dist:0}m"; _vhZoneD[label].color = new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, a * 0.85f);
                _vhZoneD[label].rectTransform.anchoredPosition = new Vector2(x - 90, -62);
                label++;
            }
            for (int i = 0; i < 3; i++) { _vhZoneT[i].gameObject.SetActive(i < label); _vhZoneD[i].gameObject.SetActive(i < label); }
        }

        // ───────── 場所の名前 ─────────

        void BannerBoard(bool on)
        {
            if (!on || _bannerT <= 0 || string.IsNullOrEmpty(_bannerTitle)) { if (_vhBanner != null) _vhBanner.Alpha = 0; return; }
            if (_vhBanner == null)
            {
                _vhBanner = Passive("場所の名前", new Vector2(520, 70), new Vector2(1, 1), new Vector2(-40, -34), new Vector3(0.42f, 0.26f, 1.3f), 4);
                var r = _vhBanner.root;
                _vhBannerSub = Board.Label(r, new Rect(0, 0, 520, 20), "", 15, Board.Cyan, TextAnchor.MiddleRight, true);
                _vhBannerTitle = Board.Label(r, new Rect(0, 20, 520, 40), "", 30, Color.white, TextAnchor.MiddleRight, false, FontStyle.Bold);
                Board.Panel(r, new Rect(300, 64, 220, 2), Board.Pink);
            }
            string key = _bannerTitle + "|" + _bannerSub;
            if (key != _vhBannerKey) { _vhBannerKey = key; _vhBannerTitle.text = _bannerTitle; _vhBannerSub.text = _bannerSub ?? ""; }
            _vhBanner.Alpha = Mathf.Clamp01(_bannerT / 0.6f) * Mathf.Clamp01((_bannerLen - _bannerT) / 0.3f + 0.2f);
        }

        // ───────── お店・番付・遊覧船の看板 ─────────

        Board SignBoard(Object owner, string english, string title, Color bar, float width)
        {
            if (_vhSigns.TryGetValue(owner, out var b) && b != null) return b;
            b = Board.Create("看板：" + title, new Vector2(width, 68), 2);
            b.passive = true; b.pinned = true;
            var r = b.root;
            Board.Panel(r, new Rect(0, 0, width, 68), Board.Glass);
            Board.Panel(r, new Rect(0, 0, 4, 68), bar);
            Board.Label(r, new Rect(0, 5, width, 18), english, 13, Board.Cyan, TextAnchor.UpperCenter, true);
            Board.Label(r, new Rect(0, 22, width, 40), title, 24, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            b.Apply();
            b.Alpha = 0;
            _vhSigns[owner] = b;
            return b;
        }

        /// <summary>空間に置いた板を、その場所・その遠さに合わせて見せる（遠いほど大きく、読める大きさを保つ）。</summary>
        static void PlaceSign(Board b, Vector3 pos, float alpha, Transform head, float perMeter = 0.0011f, float min = 0.004f)
        {
            b.Alpha = alpha;
            if (alpha <= 0.001f) return;
            float dist = Vector3.Distance(pos, head.position);
            b.pinPos = pos;
            b.transform.position = pos;
            b.transform.localScale = Vector3.one * Mathf.Max(min, dist * perMeter);
        }

        void SignBoards(bool show)
        {
            var c = Camera.main;
            if (!show || c == null)
            {
                foreach (var b in _vhSigns.Values) if (b != null) b.Alpha = 0;
                return;
            }
            var head = c.transform;
            bool flyOrShop = PlayerController.Fly || ShopStand.Near(player.Position, 3.2f) != null;
            foreach (var s in ShopStand.All)
            {
                float d = Vector3.Distance(s.sign, head.position);
                float a = d > 24f ? 0 : Mathf.Clamp01((24f - d) / 6f);
                if (a <= 0 && !_vhSigns.ContainsKey(s)) continue;
                PlaceSign(SignBoard(s, s.english, s.title, Board.Pink, 260), s.sign, a, head);
            }
            foreach (var rb in RankBoard.All)
            {
                float d = Vector3.Distance(rb.sign, head.position);
                float a = d > 40f ? 0 : Mathf.Clamp01((40f - d) / 10f);
                if (a <= 0 && !_vhSigns.ContainsKey(rb)) continue;
                PlaceSign(SignBoard(rb, "WEEKLY BIG FISH", "大物番付", Board.Pink, 260), rb.sign, a, head);
            }
            foreach (var sb in ShipBooth.All)
            {
                float d = Vector3.Distance(sb.sign, head.position);
                float a = flyOrShop || d > 45f ? 0 : Mathf.Clamp01((45f - d) / 10f);
                if (a <= 0 && !_vhSigns.ContainsKey(sb)) continue;
                PlaceSign(SignBoard(sb, "SKY CRUISE ▸ " + SkyShip.Price + " FUJU", "遊覧船「月の舟」", Board.Gold, 300), sb.sign, a, head);
            }
        }

        // ───────── 町の人の吹き出し ─────────

        void BubbleBoards(bool show)
        {
            var c = Camera.main;
            int n = show && c != null ? Mathf.Min(Crowd.Bubbles.Count, 6) : 0;
            for (int i = 0; i < _vhBubbles.Count || i < n; i++)
            {
                if (i >= n) { _vhBubbles[i].b.Alpha = 0; continue; }
                var bub = Crowd.Bubbles[i];
                if (i >= _vhBubbles.Count)
                {
                    var nb = Board.Create("吹き出し", new Vector2(400, 60), 3);
                    nb.passive = true; nb.pinned = true; nb.Apply(); nb.Alpha = 0;
                    _vhBubbles.Add((nb, null, null));
                }
                var (b, who, text) = _vhBubbles[i];
                if (bub.who == null) { b.Alpha = 0; continue; }
                if (who != bub || text != bub.text)
                {
                    // 文字の長さに合わせて作りなおす
                    float w = bub.text.Length * 21f + 36f;
                    var r = b.root;
                    Board.Clear(r);
                    r.sizeDelta = new Vector2(w, 60);
                    ((RectTransform)b.canvas.transform).sizeDelta = r.sizeDelta;
                    Board.Panel(r, new Rect(0, 0, w, 46), new Color(1f, 1f, 1f, 0.92f));
                    Board.Outline(r, new Rect(0, 0, w, 46), new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.8f), 2f);
                    for (int k = 0; k < 4; k++) Board.Panel(r, new Rect(w / 2 - 7 + k * 2, 46 + k * 3, 14 - k * 4, 3), new Color(1f, 1f, 1f, 0.92f));
                    Board.Label(r, new Rect(0, 0, w, 46), bub.text, 21, new Color(0.12f, 0.1f, 0.2f), TextAnchor.MiddleCenter);
                    _vhBubbles[i] = (b, bub, bub.text);
                }
                var wp = bub.who.position + Vector3.up * (bub.height + 0.25f);
                var v = wp - c.transform.position;
                float a = Mathf.Clamp01(bub.t / 0.4f) * Mathf.Clamp01((20f - v.magnitude) / 4f);
                PlaceSign(b, wp, a, c.transform, 0.0009f, 0.003f);
            }
        }
    }
}
