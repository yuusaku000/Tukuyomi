using System;
using System.Collections;
using System.Collections.Generic;

using System.Globalization;
using System.IO;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// ログインとログアウト。初回は大鳥居から、2回目からは前回ログアウトした場所から始める。
    /// 20秒ごとと、終わるとき・ほかの画面に切りかえたときに、いまの場所を保存する。
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public PlayerController player;
        public OrbitCamera cam;
        public Transform spawn;

        SaveData _data;
        float _saveTimer;
        bool _shotMode;

        public static GameSession Instance { get; private set; }

        /// <summary>持ち物と着ているもの（ショップ・着がえから使う）。</summary>
        public SaveData Data => _data;

        /// <summary>買い物や着がえのあと、すぐ保存する（確認用の起動でも持ち物は残す）。</summary>
        public void SaveItems()
        {
            if (_data == null) return;
            SaveStore.Save(_data);
        }

        void Awake() => Instance = this;

        void Start()
        {
            _shotMode = Ts.ShotMode;
            Quality.Load();
            Application.runInBackground = true;
            // --fresh は「保存した場所を使わない」。--fresh-all は持ち物もふくめて初めから（確認用）
            _data = Ts.Flag("--fresh-all") ? new SaveData() : SaveStore.Load();
            if (Ts.Flag("--fresh")) _data.has = false;
            if (_data.owned == null) _data.owned = new List<string>();
            if (_data.wearing == null) _data.wearing = new List<string>();
            if (_data.fish == null) _data.fish = new List<FishRecord>();
            FishBook.RollWeek(_data);
            if (Catalog.Get(_data.clothes) == null) _data.clothes = Catalog.StartClothes;
            bool first = !_data.has;
            var ward = player.GetComponent<Wardrobe>();
            if (ward != null) ward.Wear(_data.clothes, _data.wearing);

            var warp = Ts.Arg("--warp");
            var z = warp != null ? Zone.Find(warp) : null;
            if (z != null)
                z.WarpTo(player, cam);
            else if (_data.has && _data.y > -5f && Mathf.Abs(_data.x) < 600 && Mathf.Abs(_data.z) < 600)
            {
                player.Teleport(new Vector3(_data.x, _data.y + 0.1f, _data.z), _data.yaw);
                cam.yaw = _data.camYaw; cam.pitch = _data.camPitch; cam.distance = _data.camDist; cam.firstPerson = _data.firstPerson;
                cam.Snap();
            }
            else
                player.Teleport(spawn.position, spawn.eulerAngles.y);

            ApplyCam(Ts.Arg("--cam"));

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (first) _data.firstLogin = now;
            _data.lastLogin = now;
            _data.logins++;
            Hud.Welcome(first, Ts.Arg("--from"));

            if (Ts.Arg("--shot") != null) StartCoroutine(Shot(Ts.Arg("--shot"), Ts.ArgFloat("--shot-delay", 6f)));
            if (Ts.Arg("--tour") != null) StartCoroutine(Tour(Ts.Arg("--tour"), Ts.Arg("--tour-dir") ?? Ts.DataDir));
        }

        void ApplyCam(string c)
        {
            if (string.IsNullOrEmpty(c)) return;
            var p = c.Split(',');
            if (p.Length >= 1 && float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) cam.yaw = y;
            if (p.Length >= 2 && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pi)) cam.pitch = pi;
            if (p.Length >= 3 && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) cam.distance = d;
            cam.Snap();
        }

        /// <summary>
        /// 確認用：いくつもの場所を1回の起動でまわって画面を保存する。
        /// --tour "torii|13:00|0,10,7;shopping|20:30;plaza|19:00||map" --tour-dir 保存先
        /// </summary>
        IEnumerator Tour(string spec, string dir)
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(4f);
            int i = 0;
            foreach (var item in spec.Split(';'))
            {
                var p = item.Split('|');
                Hud.CloseShop();
                Hud.CloseBook();
                Fishing.I?.Stop();
                Rest.I?.Stand();

                var z = Zone.Find(p[0]);
                if (z != null) z.WarpTo(player, cam);
                else if (p[0].StartsWith("@"))
                {
                    var xz = p[0].Substring(1).Split(',');
                    float x = float.Parse(xz[0], CultureInfo.InvariantCulture), zz = float.Parse(xz[1], CultureInfo.InvariantCulture);
                    float ground = Island.Height(x, zz);
                    if (Physics.Raycast(new Vector3(x, 200, zz), Vector3.down, out var hit, 400, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
                    player.Teleport(new Vector3(x, ground + 0.1f, zz), 0);
                }
                if (p.Length > 1 && p[1].Length > 0) Ts.SetTime(p[1]);
                if (p.Length > 2) ApplyCam(p[2]);
                Hud.OpenMap(p.Length > 3 && p[3] == "map");
                Hud.OpenMenu(p.Length > 3 && p[3] == "menu");
                // shop:gofuku[:品物] でお店の画面、wear で着がえ、buy:品物 で本当に買う（テスト用のミニヤチヨで）
                if (p.Length > 3 && p[3].StartsWith("shop:")) { var a = p[3].Split(':'); Hud.OpenShopFor(a[1], a.Length > 2 ? a[2] : null, true); }
                if (p.Length > 3 && p[3] == "wear") Hud.OpenWardrobe();
                if (p.Length > 3 && p[3].StartsWith("buy:")) Hud.TestBuy(p[3].Substring(4));
                // 段階3：fish:ready|charge|wait|bite|fight|catch[:魚] で釣り、book / rank で図鑑と番付、sit[:秒] で近くの席に座る
                bool again = false;
                if (p.Length > 3 && p[3].StartsWith("fish:")) { Fishing.I?.Test(p[3].Substring(5)); again = true; }
                if (p.Length > 3 && (p[3] == "book" || p[3] == "rank")) Hud.OpenBook(p[3] == "book" ? 0 : 1);
                if (p.Length > 3 && p[3].StartsWith("sit")) { Rest.I?.TestSit(p[3].Length > 4 && float.TryParse(p[3].Substring(4), NumberStyles.Float, CultureInfo.InvariantCulture, out var si) ? si : 0f); again = true; }
                if (again && p.Length > 2) ApplyCam(p[2]);
                cam.firstPerson = p.Length > 4 && p[4] == "fp";
                // 6つ目で待ち秒数を変えられる（食べている途中を撮るときなど）
                float wait = p.Length > 5 && float.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var wv) ? wv : 3.5f;
                yield return new WaitForSeconds(Mathf.Max(0.2f, wait - 1f));
                int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
                yield return new WaitForSeconds(Mathf.Min(1f, wait));

                Debug.Log($"[Tour] {p[0]} fps={(Time.frameCount - f0) / (Time.realtimeSinceStartup - t0):0}");
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{i:00}_{p[0].Replace("@", "at").Replace(",", "_")}.png"));
                yield return new WaitForSeconds(0.8f);
                i++;
            }
            Application.Quit();
        }

        IEnumerator Shot(string path, float delay)
        {
            yield return new WaitForSeconds(delay);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(1.5f);
            Application.Quit();
        }

        void Update()
        {
            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer > 20f) Save();
        }

        public void Save()
        {
            _saveTimer = 0;
            if (_shotMode || player == null) return;
            var p = player.Position;
            if (player.InWater) return; // 水の中は保存しない（岸にもどされる途中のため）
            if (Rest.Sitting || (SkyShip.I != null && SkyShip.I.OnDeck(p))) { SaveStore.Save(_data); return; } // 遊覧船の上も場所は保存しない // 座っている間は場所を変えない（ソファの中から始まらないように）
            _data.has = true;
            _data.x = p.x; _data.y = p.y; _data.z = p.z; _data.yaw = player.Yaw;
            _data.camYaw = cam.yaw; _data.camPitch = cam.pitch; _data.camDist = cam.distance; _data.firstPerson = cam.firstPerson;
            SaveStore.Save(_data);
        }

        public void Logout()
        {
            Save();
            Application.Quit();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) Save();
            // ほかの画面を見ている間は、PCに負担をかけない
            Application.targetFrameRate = focus || _shotMode ? -1 : 20;
        }

        void OnApplicationQuit() => Save();
    }
}
