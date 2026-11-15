using UnityEngine;

namespace SalonIntern
{
    // 레벨별 규칙 수치. 기획서 4, 8, 10장 기준이며 모두 시작점이라 테스트하며 조정한다.
    public static class LevelConfig
    {
        public const int MaxLevel = 6;

        // Lv.0 ~ Lv.6
        public static readonly string[] Names =
        {
            "신입 인턴", "견습 인턴", "보조 인턴", "숙련 인턴", "베테랑 인턴", "수석 인턴", "졸업반 인턴"
        };

        // 감점은 레벨과 상관없이 같다: 요청 하나를 놓칠 때, 최소 건수에서 한 건 모자랄 때.
        // 레벨이 오르면 맡는 선생님이 늘어 놓칠 가능성이 커지는 것이 난이도다.
        public static readonly int[] MissPenalty = { 10, 10, 10, 10, 10, 10, 10 };
        public static readonly int[] ShortPenalty = { 15, 15, 15, 15, 15, 15, 15 };

        // 오늘 같이 일할 선생님 수 (Lv.0 1명 ~ Lv.6 8명). 예약표를 보고 정확히 이 수만큼 고르고, 나머지 선생님과 내 일도 동료 인턴이 함께 본다.
        public static readonly int[] PickMax = { 1, 1, 2, 3, 4, 5, 6 };   // 내 레벨과 같은 수(Lv.0 견학 날은 1명)

        // 내 레벨별 동료 인턴 3명의 레벨. 내 레벨이 오를수록 선배는 줄고 나보다 낮은 후배가 늘어난다.
        // (v2.18: 놓치는 손님이 많아 평균을 +1 올림. 최대 Lv.6)
        public static readonly int[][] NpcLevels =
        {
            new[] { 6, 6, 5 }, new[] { 6, 6, 4 }, new[] { 6, 5, 4 }, new[] { 6, 5, 4 },
            new[] { 5, 5, 4 }, new[] { 5, 4, 4 }, new[] { 4, 4, 4 },
        };

        // 동시에 떠 있을 수 있는 내 요청 수 (고른 선생님 일만 센다. 선생님 한 명당 한 장)
        public static readonly int[] MaxConcurrent = { 2, 2, 3, 4, 5, 5, 5 };

        // 하루에 받는 시술 요청 수(이만큼 받아 모두 처리되면 그날 마감)
        public static readonly int[] DailyQuota = { 3, 4, 5, 6, 7, 8, 10 };

        // 피로도가 쌓이는 배율: 낮은 레벨은 빨리, 높은 레벨은 천천히
        public static readonly float[] FatigueScale = { 1.5f, 1.3f, 1.1f, 1.0f, 0.85f, 0.7f, 0.6f };

        // 요청 제한시간(초). 디자이너 참을성 배율이 곱해진다.
        public static readonly float[] TimeLimit = { 38f, 33f, 28f, 23f, 18f, 18f, 18f };

        // 요청 중 잡무 비율 (나머지는 시술 요청)
        public static readonly float[] ChoreRatio = { 0.45f, 0.40f, 0.33f, 0.27f, 0.22f, 0.20f, 0.20f };

        // 아직 못 하는 시술 요청이 뽑혔을 때 화면에 보여줄 확률
        public static readonly float[] LockedShowRate = { 0.15f, 0.15f, 0.12f, 0.10f, 0f, 0f, 0f };

        // 시술 이름과 해금 레벨 (샴푸, 펌, 염색, 드라이, 커트 순서)
        public static readonly string[] ProcedureNames = { "샴푸", "펌", "염색 배합/도포", "드라이", "커트", "탈색" };
        public static readonly int[] ProcedureUnlockLevel = { 1, 2, 3, 4, 5, 3 };
        public static readonly string[] ProcedureShortNames = { "샴푸", "펌", "염색", "드라이", "커트", "탈색" };

        // Lv.0 잡무 7종
        public static readonly string[] ChoreNames =
        {
            "수건 접기", "도구 정리", "음료 서비스", "바닥 청소",
            "세면대·거울 청소", "손님 응대 보조", "전화·예약 메모"
        };

        // 다음 레벨까지 필요한 XP (임시 공식)
        // 테스트용: true면 경험치와 시험 응시 조건을 최하로 낮춘다. 출시 전에 false로 바꿀 것.
        public const bool TestMode = false;

        // 테스트용: true면 승급·졸업시험 미니게임을 건너뛰고 바로 합격 처리한다 (XP·연습 횟수 조건은 그대로).
        // 출시 전, 그리고 시험 난이도를 점검할 때는 false로 돌릴 것.
        public static readonly bool AutoPassExam = false;

        static readonly int[] NeedXp = { 8, 18, 22, 26, 27, 34, 35 };

        public static int XpToNext(int level)
        {
            if (TestMode) return 1;
            // 졸업까지 총 플레이 시간이 약 2시간(약 24~26일)이 되도록 맞춘 값. Lv.0은 견학만 해도 바로 Lv.1
            return NeedXp[Mathf.Clamp(level, 0, NeedXp.Length - 1)];
        }
    }
}
