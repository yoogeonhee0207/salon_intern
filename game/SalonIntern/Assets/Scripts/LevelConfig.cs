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

        // 동시에 떠 있을 수 있는 요청 수 (Lv.5~6은 아직 Lv.4와 같게 둠)
        public static readonly int[] MaxConcurrent = { 2, 2, 3, 4, 5, 5, 5 };

        // 하루에 받는 시술 요청 수(이만큼 받아 모두 처리되면 그날 마감)
        public static readonly int[] DailyQuota = { 3, 4, 5, 6, 7, 8, 10 };

        // 피로도가 쌓이는 배율: 낮은 레벨은 빨리, 높은 레벨은 천천히
        public static readonly float[] FatigueScale = { 1.5f, 1.3f, 1.1f, 1.0f, 0.85f, 0.7f, 0.6f };

        // 요청 제한시간(초). 디자이너 참을성 배율이 곱해진다.
        public static readonly float[] TimeLimit = { 40f, 35f, 30f, 25f, 20f, 20f, 20f };

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
        public const bool TestMode = true;

        public static int XpToNext(int level)
        {
            if (TestMode) return 1;
            // Lv.0은 견학 4종(각 XP +2)만 해도 바로 Lv.1이 되도록 8로 낮춤
            if (level == 0) return 8;
            return 10 + 5 * level;
        }
    }
}
