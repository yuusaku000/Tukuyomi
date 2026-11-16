using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Tsukuyomi
{
    /// <summary>
    /// ツクヨミ全体で使う小物：保存先・起動オプション・時刻・日本語フォント。
    ///
    /// 起動オプション（動作確認用）
    ///   --from contact|iroha     どこから来たか（あいさつが変わる）
    ///   --time 21:30             時刻をずらして試す（現実の時刻とのリンクの確認用）
    ///   --warp torii|shopping|…  その場所から始める
    ///   --cam yaw,pitch,dist     カメラの向き
    ///   --fresh                  保存した場所を使わない（初回と同じ）
    ///   --shot path.png          数秒後に画面を保存して終わる（保存はしない）
    ///   --shot-delay 6           上の待ち秒数
    /// 環境変数 TSUKUYOMI_DATA で保存先を変えられる（テスト用）。
    /// </summary>
    public static class Ts
    {
        static string[] _args;
        static string[] Args => _args ??= Environment.GetCommandLineArgs();

        public static string Arg(string name)
        {
            var a = Args;
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == name) return a[i + 1];
            return null;
        }

        public static bool Flag(string name) => Array.IndexOf(Args, name) >= 0;

        public static float ArgFloat(string name, float fallback) =>
            float.TryParse(Arg(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public static string DataDir
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("TSUKUYOMI_DATA");
                var dir = string.IsNullOrEmpty(env)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tsukuyomi")
                    : env;
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        static TimeSpan? _offset;

        /// <summary>画面を保存して終わる確認用の起動（マウスやキーを受けつけない）。</summary>
        public static bool ShotMode => Arg("--shot") != null || Arg("--tour") != null;

        /// <summary>時刻を "21:30" の形でずらす（確認用）。</summary>
        public static void SetTime(string hhmm)
        {
            if (TimeSpan.TryParse(hhmm, CultureInfo.InvariantCulture, out var want))
                _offset = DateTime.Today + want - DateTime.Now;
        }

        /// <summary>現実の時刻（--time でずらせる）。</summary>
        public static DateTime Now
        {
            get
            {
                if (_offset == null)
                {
                    _offset = TimeSpan.Zero;
                    var t = Arg("--time");
                    if (t != null && TimeSpan.TryParse(t, CultureInfo.InvariantCulture, out var want))
                        _offset = DateTime.Today + want - DateTime.Now;
                }
                return DateTime.Now + _offset.Value;
            }
        }

        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic" }, 32);
                return _font;
            }
        }

        public static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}
