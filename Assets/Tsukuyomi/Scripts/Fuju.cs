using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Tsukuyomi
{
    /// <summary>
    /// ミニヤチヨ（デスクトップのマスコット）のローカル窓口で、本物のふじゅ〜を見たり払ったりする。
    ///   GET  http://127.0.0.1:8770/api/tsukuyomi/wallet  → {fuju}
    ///   POST http://127.0.0.1:8770/api/tsukuyomi/buy     {id, name, price} → {ok, fuju, error?}
    /// 合言葉は %APPDATA%\MiniYachiyo\api_token.txt（MINIYACHIYO_DATA があればそちら）。
    /// 窓口の番号は --yachiyo-port（ミニヤチヨから起動したとき）→ MINIYACHIYO_PORT → 8770 の順。
    /// </summary>
    public static class Fuju
    {
        public struct Result
        {
            public bool ok;
            public int fuju;
            public string error;
        }

        [Serializable] class Reply { public bool ok; public int fuju = -1; public string error; }
        [Serializable] class BuyBody { public string id, name; public int price; }

        /// <summary>最後にわかった残高（わからなければ -1）。</summary>
        public static int Last = -1;

        static int Port
        {
            get
            {
                var a = Ts.Arg("--yachiyo-port");
                if (int.TryParse(a, out var p)) return p;
                if (int.TryParse(Environment.GetEnvironmentVariable("MINIYACHIYO_PORT"), out p)) return p;
                return 8770;
            }
        }

        static string Token()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("MINIYACHIYO_DATA");
                var dir = string.IsNullOrEmpty(env)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniYachiyo")
                    : env;
                var f = Path.Combine(dir, "api_token.txt");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : null;
            }
            catch { return null; }
        }

        const string Offline = "ミニヤチヨにつながりません（ミニヤチヨを起動してね）";

        static IEnumerator Send(string method, string path, string json, Action<Result> done)
        {
            var token = Token();
            if (string.IsNullOrEmpty(token)) { done(new Result { fuju = -1, error = Offline }); yield break; }
            using (var req = new UnityWebRequest($"http://127.0.0.1:{Port}{path}", method))
            {
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("x-yachiyo-token", token);
                req.timeout = 6;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.ConnectionError)
                {
                    done(new Result { fuju = -1, error = Offline });
                    yield break;
                }
                if (req.responseCode == 401) { done(new Result { fuju = -1, error = "ミニヤチヨの合言葉が合いません" }); yield break; }
                Reply r = null;
                try { r = JsonUtility.FromJson<Reply>(req.downloadHandler.text); } catch { }
                if (r == null || req.responseCode != 200)
                {
                    done(new Result { fuju = -1, error = "ミニヤチヨからの返事がおかしいです（" + req.responseCode + "）" });
                    yield break;
                }
                if (r.fuju >= 0) Last = r.fuju;
                done(new Result { ok = method == "GET" || r.ok, fuju = r.fuju, error = r.error });
            }
        }

        public static IEnumerator Wallet(Action<Result> done) => Send("GET", "/api/tsukuyomi/wallet", null, done);

        public static IEnumerator Buy(Item item, Action<Result> done) =>
            Send("POST", "/api/tsukuyomi/buy", JsonUtility.ToJson(new BuyBody { id = item.id, name = item.name, price = item.price }), done);
    }
}
