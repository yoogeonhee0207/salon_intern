using System;
using UnityEngine;

namespace SalonIntern
{
    // 디자이너 한 명의 데이터. Resources/Data/designers.json 에서 불러온다.
    [Serializable]
    public class DesignerData
    {
        public int id;
        public string name;
        public string specialty;

        // 요청 확률 가중치 (기획서 9장 특기표)
        public int chores;
        public int shampoo;
        public int roll;
        public int dye;
        public int dry;

        public float speed;               // 시술 속도 배율 (아직 사용 안 함)
        public string patience;           // 참을성 표시용 글자
        public float patienceMultiplier;  // 요청 제한시간에 곱해지는 값
    }

    [Serializable]
    public class DesignerDatabase
    {
        public DesignerData[] designers;
    }

    public static class DesignerLoader
    {
        public static DesignerData[] Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Data/designers");
            if (asset == null)
            {
                Debug.LogError("Resources/Data/designers.json 파일을 찾을 수 없어요.");
                return new DesignerData[0];
            }

            DesignerDatabase db = JsonUtility.FromJson<DesignerDatabase>(asset.text);
            if (db == null || db.designers == null)
            {
                Debug.LogError("designers.json 형식이 올바르지 않아요.");
                return new DesignerData[0];
            }
            return db.designers;
        }
    }
}
