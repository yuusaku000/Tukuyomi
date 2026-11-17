using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Tsukuyomi
{
    /// <summary>
    /// ふじゅ〜（ツクヨミのお金）。残高はツクヨミの save.json（fuju）に持つ。ミニヤチヨがいなくても使える。
    /// はじめて起動したとき（この版で初めてのときも）初ログインボーナスで 5000 ふじゅ〜。
    /// ミニヤチヨ（デスクトップのマスコット）の本物のふじゅ〜は、片道だけ両替できる（ミニヤチヨ → ツクヨミ、1:1、1回 1〜10000）。
    /// 両替はミニヤチヨのローカル窓口の「買う」を使う（ミニヤチヨ側は変えない）：
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
            /// <summary>ツクヨミの残高（わからなければ -1）。</summary>
            public int fuju;
            /// <summary>ミニヤチヨの残高（両替のときだけ。わからなければ -1）。</summary>
            public int mini;
            public string error;
        }

        public const int LoginBonus = 5000;
        public const int ExchangeMax = 10000;

        [Serializable] class Reply { public bool ok; public int fuju = -1; public string error; }
        [Serializable] class BuyBody { public string id, name; public int price; }

        static SaveData Data => GameSession.Instance != null ? GameSession.Instance.Data : null;

        /// <summary>ツクヨミの残高（まだ読めていなければ -1）。</summary>
        public static int Last => Data != null ? Data.fuju : -1;

        /// <summary>最後にわかったミニヤチヨの残高（わからなければ -1）。</summary>
        public static int MiniLast = -1;

        static void Save() => GameSession.Instance?.SaveItems();

        /// <summary>初ログインボーナス（まだなら渡して true）。GameSession の起動から呼ぶ。</summary>
        public static bool GiveLoginBonus(SaveData d)
        {
            if (d == null || d.fujuBonus) return false;
            d.fujuBonus = true;
            d.fuju += LoginBonus;
            return true;
        }

        /// <summary>稼いだとき（KASSEN など）。</summary>
        public static void Earn(int amount)
        {
            var d = Data;
            if (d == null || amount <= 0) return;
            d.fuju += amount;
            Save();
        }

        /// <summary>残高を見る（すぐ終わる。以前と同じ形で呼べるように IEnumerator のまま）。</summary>
        public static IEnumerator Wallet(Action<Result> done)
        {
            done(new Result { ok = Data != null, fuju = Last, mini = -1, error = Data == null ? "まだ準備中です" : null });
            yield break;
        }

        /// <summary>品物を買う（ツクヨミのふじゅ〜から払う）。</summary>
        public static IEnumerator Buy(Item item, Action<Result> done)
        {
            var d = Data;
            if (d == null) { done(new Result { fuju = -1, mini = -1, error = "まだ準備中です" }); yield break; }
            if (item.price < 0 || d.fuju < item.price) { done(new Result { ok = false, fuju = d.fuju, mini = -1, error = "ふじゅ〜が足りないよ" }); yield break; }
            d.fuju -= item.price;
            Save();
            done(new Result { ok = true, fuju = d.fuju, mini = -1 });
        }

        // ───────── ミニヤチヨからの両替（片道） ─────────

        /// <summary>ミニヤチヨの残高を見る。</summary>
        public static IEnumerator MiniWallet(Action<Result> done) =>
            Send("GET", "/api/tsukuyomi/wallet", null, r => { r.fuju = Last; done(r); });

        /// <summary>ミニヤチヨのふじゅ〜を amount だけツクヨミに移す（もどせない）。</summary>
        public static IEnumerator Exchange(int amount, Action<Result> done)
        {
            if (Data == null) { done(new Result { fuju = -1, mini = MiniLast, error = "まだ準備中です" }); yield break; }
            if (amount <= 0 || amount > ExchangeMax) { done(new Result { fuju = Last, mini = MiniLast, error = $"1回に両替できるのは 1〜{ExchangeMax:N0} ふじゅ〜です" }); yield break; }
            var body = JsonUtility.ToJson(new BuyBody { id = "tsukuyomi_exchange", name = "ツクヨミへの両替", price = amount });
            yield return Send("POST", "/api/tsukuyomi/buy", body, r =>
            {
                if (r.ok)
                {
                    Data.fuju += amount;
                    Save();
                }
                r.fuju = Last;
                done(r);
            });
        }

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

        const string Offline = "ミニヤチヨにつながりません（両替はミニヤチヨを起動してから）";

        /// <summary>ミニヤチヨの窓口に送る。結果の mini がミニヤチヨの残高。</summary>
        static IEnumerator Send(string method, string path, string json, Action<Result> done)
        {
            var token = Token();
            if (string.IsNullOrEmpty(token)) { done(new Result { mini = -1, error = Offline }); yield break; }
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
                    done(new Result { mini = -1, error = Offline });
                    yield break;
                }
                if (req.responseCode == 401) { done(new Result { mini = -1, error = "ミニヤチヨの合言葉が合いません" }); yield break; }
                Reply r = null;
                try { r = JsonUtility.FromJson<Reply>(req.downloadHandler.text); } catch { }
                if (r == null || req.responseCode != 200)
                {
                    done(new Result { mini = -1, error = "ミニヤチヨからの返事がおかしいです（" + req.responseCode + "）" });
                    yield break;
                }
                if (r.fuju >= 0) MiniLast = r.fuju;
                bool ok = method == "GET" || r.ok;
                done(new Result { ok = ok, mini = r.fuju, error = ok ? null : (string.IsNullOrEmpty(r.error) ? "両替できませんでした" : "ミニヤチヨの" + r.error) });
            }
        }
    }
}
