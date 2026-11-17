using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tsukuyomi
{
    /// <summary>
    /// M の地図とワープの板（Board）。もとは IMGUI で描いていたもの。
    ///  左に島の地図（道は地図の絵に焼きこむ）、場所の上を押すとワープ。右にワープの一覧。
    ///  板なので、VR では目の前に浮かび、光の線とトリガーで押せる。
    /// </summary>
    public partial class Hud
    {
        Board _mp;
        RectTransform _mpYou;
        Image _mpPulse;
        Texture2D _mapBoardTex;
        Rect _mpMap;

        const float MpW = 1120, MpH = 900;

        Vector2 MapP(Vector3 p) => new Vector2(
            _mpMap.x + (p.x - worldMin.x) / (worldMax.x - worldMin.x) * _mpMap.width,
            _mpMap.yMax - (p.z - worldMin.y) / (worldMax.y - worldMin.y) * _mpMap.height);

        Rect MapR(Rect w)
        {
            var a = MapP(new Vector3(w.xMin, 0, w.yMax));
            var b = MapP(new Vector3(w.xMax, 0, w.yMin));
            return new Rect(a.x, a.y, b.x - a.x, b.y - a.y);
        }

        /// <summary>地図の絵に、道を焼きこむ（板の上に小さな四角をたくさん並べないように）。</summary>
        Texture2D MapBoardTexture()
        {
            var low = MapTexture();
            // 道を細く描けるように、3倍の細かさの絵にする
            int tw = low.width * 3, th = low.height * 3;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                    px[y * tw + x] = low.GetPixelBilinear((x + 0.5f) / tw, (y + 0.5f) / th);
            Destroy(low);
            var sm = StreetMap.I;
            if (sm == null || sm.points == null) { tex.SetPixels(px); tex.Apply(); return tex; }
            var road = new Color(0.95f, 0.9f, 0.75f);
            float perM = tw / (worldMax.x - worldMin.x);
            int idx = 0;
            for (int si = 0; si < sm.counts.Length; si++)
            {
                float rad = Mathf.Max(0.6f, sm.halves[si] * perM);
                for (int k = 0; k < sm.counts[si]; k++)
                {
                    var q = sm.points[idx + k];
                    if (Island.NearestWater(q.x, q.y) < 0.5f || Mathf.Abs(Island.TerraceHeight(q.x, q.y) - Island.Height(q.x, q.y)) > 0.5f && Island.NearestWater(q.x, q.y) > 3f) continue;
                    float cx = (q.x - worldMin.x) * perM - 0.5f, cy = (q.y - worldMin.y) / (worldMax.y - worldMin.y) * th - 0.5f;
                    int r = Mathf.CeilToInt(rad);
                    for (int y = -r; y <= r; y++)
                        for (int x = -r; x <= r; x++)
                        {
                            int ix = Mathf.RoundToInt(cx) + x, iy = Mathf.RoundToInt(cy) + y;
                            if (ix < 0 || iy < 0 || ix >= tw || iy >= th) continue;
                            float d = new Vector2(ix - cx, iy - cy).magnitude;
                            if (d > rad + 0.5f) continue;
                            px[iy * tw + ix] = Color.Lerp(px[iy * tw + ix], road, 0.85f * Mathf.Clamp01(rad + 0.5f - d));
                        }
                }
                idx += sm.counts[si];
            }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        void BuildMapBoard()
        {
            if (_mp != null) return;
            _mp = Board.Create("地図", new Vector2(MpW, MpH), 25);
            _mp.worldOffset = new Vector3(0, -0.05f, 1.3f);
            _mp.Apply();
            var r = _mp.root;
            Board.Dim(r, MpW, MpH, 0.7f);
            float mh = MpH - 60;
            float mw = mh * (worldMax.x - worldMin.x) / (worldMax.y - worldMin.y);
            _mpMap = new Rect(30, 30, mw, mh);
            Board.Frame(r, new Rect(0, 0, MpW, MpH));
            if (_mapBoardTex == null) _mapBoardTex = MapBoardTexture();
            Board.Raw(r, _mpMap).texture = _mapBoardTex;
            Board.Outline(r, _mpMap, new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.5f), 1.5f);
            for (float gx = _mpMap.x + 40; gx < _mpMap.xMax; gx += 40) Board.Panel(r, new Rect(gx, _mpMap.y, 1, _mpMap.height), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.06f));
            for (float gy = _mpMap.y + 40; gy < _mpMap.yMax; gy += 40) Board.Panel(r, new Rect(_mpMap.x, gy, _mpMap.width, 1), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.06f));
            // 参道と大鳥居
            var red = new Color(0.85f, 0.3f, 0.22f);
            Board.Panel(r, MapR(new Rect(-3.5f, -74, 7, 74)), red);
            Board.Panel(r, MapR(new Rect(-8, -74, 16, 12)), red);
            Board.Panel(r, MapR(new Rect(-2.5f, -100, 5, 26)), new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.8f));
            var tp = MapP(new Vector3(0, 0, -82));
            Board.Panel(r, new Rect(tp.x - 14, tp.y - 3, 28, 6), new Color(1f, 0.3f, 0.2f));

            // 場所：上を押すとワープ（指すとうっすら光る）
            var zones = new List<Zone>(Zone.All);
            zones.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var z in zones)
            {
                if (!z.warpable) continue;
                var zz = z;
                var area = MapR(z.Area);
                var img = Board.Panel(r, area, new Color(Board.Cyan.r, Board.Cyan.g, Board.Cyan.b, 0.22f));
                img.raycastTarget = true;
                var b = img.gameObject.AddComponent<Button>();
                var cb = b.colors; cb.normalColor = new Color(1, 1, 1, 0); cb.highlightedColor = Color.white; cb.pressedColor = Color.white; cb.selectedColor = new Color(1, 1, 1, 0); b.colors = cb;
                b.onClick.AddListener(() => { Sfx.Click(); Warp(zz); });
            }
            foreach (var z in zones)
            {
                if (!z.warpable) continue;
                var dot = MapP(z.transform.position);
                var c = z.mapColor; c.a = 0.95f;
                Board.Panel(r, new Rect(dot.x - 5, dot.y - 5, 10, 10), c).sprite = Board.DiscSprite;
                Board.Label(r, new Rect(dot.x - 100, dot.y + 6, 200, 24), z.title, 17, Color.white, TextAnchor.UpperCenter);
            }
            Board.Label(r, new Rect(_mpMap.x + 12, _mpMap.y + 8, 400, 22), "MAP ▸ TSUKUYOMI ISLAND", 16, Board.Cyan, TextAnchor.MiddleLeft, true);

            // いまここ
            _mpYou = Board.Group(r, "いまここ", new Rect(0, 0, 0, 0));
            _mpPulse = Board.Panel(_mpYou, new Rect(-14, -14, 28, 28), new Color(Board.Pink.r, Board.Pink.g, Board.Pink.b, 0.9f));
            _mpPulse.sprite = Board.RingSprite;
            Board.Panel(_mpYou, new Rect(-5, -5, 10, 10), Color.white).sprite = Board.DiscSprite;
            Board.Label(_mpYou, new Rect(-80, -44, 160, 24), "YOU ▸ いまここ", 16, Board.Gold, TextAnchor.MiddleCenter);

            // ワープの一覧
            float lx = _mpMap.xMax + 30, lw = MpW - lx - 30;
            Board.Label(r, new Rect(lx, 34, lw, 24), "WARP", 20, Board.Cyan, TextAnchor.MiddleCenter, true);
            Board.Label(r, new Rect(lx, 58, lw, 44), "ワープ", 34, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            Board.Panel(r, new Rect(lx + lw / 2 - 80, 106, 160, 2), Board.Vermilion);
            float y = 122;
            int n = 0;
            foreach (var z in zones) if (z.warpable) n++;
            float step = Mathf.Min(58f, (MpH - 122 - 110) / Mathf.Max(1, n));
            foreach (var z in zones)
            {
                if (!z.warpable) continue;
                var zz = z;
                Board.Btn(r, new Rect(lx + 10, y, lw - 20, step - 8), z.title, step < 50 ? 19 : 21, () => Warp(zz));
                y += step;
            }
            Board.Btn(r, new Rect(lx + 10, MpH - 104, lw - 20, 52), "とじる（M / Esc）", 21, () => _map = false);
            Board.Wrap(r, new Rect(lx, MpH - 46, lw, 24), "地図の場所を押しても飛べます", 15, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter);
        }

        void MapBoardTick()
        {
            bool open = _map && !Kassen.Busy;
            if (!open) { if (_mp != null) _mp.Alpha = 0; return; }
            BuildMapBoard();
            _mp.Alpha = 1;
            if (player != null)
            {
                var p = MapP(player.Position);
                _mpYou.anchoredPosition = new Vector2(p.x, -p.y);
                var pr = _mpPulse.rectTransform; // 輪が広がったり縮んだり（まん中は動かさない）
                float s = 14f + Mathf.Sin(Time.unscaledTime * 6) * 4f;
                pr.anchoredPosition = new Vector2(-s, s); pr.sizeDelta = new Vector2(s * 2, s * 2);
            }
        }
    }
}
