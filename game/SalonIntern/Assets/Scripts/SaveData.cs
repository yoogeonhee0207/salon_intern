using System;
using UnityEngine;

namespace SalonIntern
{
    // 저장되는 진행 상황. 아침이 시작될 때와 승급시험이 끝날 때 저장한다.
    [Serializable]
    public class SaveData
    {
        public int day = 1;
        public int level;
        public int xp;
        public int score;
        public int done;
        public int missed;
        public int[] practice;
        public bool[] tourSeen;
        public bool introShown;
        public int lastExamDay;
        public bool graduated;
        public int[] closenessIds;
        public float[] closenessValues;
        public int points;
        public string playerName;
        public int gender;
        public string salonName;
        public bool[] ownedSkins;
        public int[] equippedSkins;

        // 확장 기능 (손님/업적/목표/이벤트)
        public int totalPointsEarned;
        public int satSCount;
        public int inspectorPass;
        public int daysPlayed;
        public int perfectDays;
        public int goalsDone;
        public int snackCount;
        public int buffDays;
        public int weekScore;
        public int weekTarget;
        public bool[] achievements;
        public int[] eventSeen;

        const string Key = "SalonIntern.Save";

        public static bool Exists()
        {
            try { return PlayerPrefs.HasKey(Key); }
            catch (Exception) { return false; }
        }

        public static SaveData Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(Key)) return null;
                return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
            }
            catch (Exception e)
            {
                Debug.LogWarning("저장 데이터를 읽지 못했어요: " + e.Message);
                return null;
            }
        }

        public void Save()
        {
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("저장하지 못했어요: " + e.Message);
            }
        }

        public static void Delete()
        {
            try
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
            catch (Exception) { }
        }
    }
}
