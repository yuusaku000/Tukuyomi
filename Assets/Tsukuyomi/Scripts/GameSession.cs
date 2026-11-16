using System;
using System.Collections;
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

        void Awake() => Instance = this;

        void Start()
        {
            _shotMode = Ts.ShotMode;
            Quality.Load();
            Application.runInBackground = true;
            _data = Ts.Flag("--fresh") ? new SaveData() : SaveStore.Load();
            bool first = !_data.has;

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
                cam.firstPerson = p.Length > 4 && p[4] == "fp";
                yield return new WaitForSeconds(2.5f);
                int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
                yield return new WaitForSeconds(1f);
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
