using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tsukuyomi
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public bool has;
        public float x, y, z, yaw;
        public float camYaw, camPitch = 14f, camDist = 6f;
        public bool firstPerson;
        public int logins;
        public string firstLogin = "";
        public string lastLogin = "";
        // 段階2：買ったもの・着ているもの
        public List<string> owned = new List<string>();
        public string clothes = Catalog.StartClothes;
        public List<string> wearing = new List<string>();
        public int eaten;
        // 段階3：魚の図鑑と、週ごとの大物番付（仮想。ふじゅ〜は増えない）
        public List<FishRecord> fish = new List<FishRecord>();
        public int fishTotal;
        public string weekKey = "";
        public float weekBest;
        public string weekFish = "";
        public int lastWeekRank, lastWeekCount;
        public float lastWeekCm;
        public float restMinutes;
        // 理髪店：髪の色と形（hairSet が false のあいだは、はじめの黒髪ロング）
        public bool hairSet;
        public Color hairColor = new Color(0.16f, 0.12f, 0.16f);
        public int hairStyle = 1;
        // ふじゅ〜（ツクヨミのお金）。ミニヤチヨから独立。fujuBonus は初ログインボーナスをもらったか
        public int fuju;
        public bool fujuBonus;
        // 遊び方の板を一度見たか（初めて島に入ったときに1回だけ自動で開く）
        public bool guideSeen;
        // KASSEN：SETSUNA の勝ち負け
        public int setsunaWins, setsunaLosses;
        public int sengokuWins, sengokuLosses;
        // 初めてのログインの演出（ヤチヨの案内と無料の着がえ）。introStarted は途中で終わったときにやりなおすため
        public bool introStarted, introDone;
        // 初めてのログインの夜：この現実の時刻（"yyyy-MM-dd HH:mm"、次の日の出）まで夜のまま。空ならふつう
        public string nightUntil = "";
    }

    /// <summary>%APPDATA%\Tsukuyomi\save.json に、ログアウトした場所などを残す。</summary>
    public static class SaveStore
    {
        static string FilePath => Path.Combine(Ts.DataDir, "save.json");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            }
            catch (Exception e) { Debug.LogWarning("save load: " + e.Message); }
            return new SaveData();
        }

        public static void Save(SaveData d)
        {
            try
            {
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d, true));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("save: " + e.Message); }
        }
    }
}
