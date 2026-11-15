using UnityEngine;

namespace SalonIntern
{
    // 하루 구조 수치. 기획서 2장 기준이며 모두 시작점이라 테스트하며 조정한다.
    public static class DayConfig
    {
        // 영업 시간의 현실 길이(초). 240초 = 4분.
        // 테스트할 때는 60 정도로 줄이면 하루가 금방 끝나서 확인하기 편하다.
        public const float BusinessSeconds = 240f;

        // 영업 시간 (분 단위): 10:00 ~ 18:00
        public const int OpenMinutes = 10 * 60;
        public const int CloseMinutes = 18 * 60;

        // 현실 1초 동안 흐르는 게임 속 분 (240초 동안 480분 -> 2분)
        public const float MinutesPerSecond = (CloseMinutes - OpenMinutes) / BusinessSeconds;

        // 아침 예약표: 10시부터 30분 단위로 16칸(10:00~17:30), 칸마다 예약이 있을 확률
        // (칸이 두 배, 인턴이 늘었으니 칸당 확률 0.26 → 하루 전체 예약 약 33건. 고르지 않은 선생님의 예약은 동료 인턴이 처리하니 예약이 많아야 인턴이 놀지 않는다)
        public const int SlotMinutes = 30;
        public const int ReservationSlots = (CloseMinutes - OpenMinutes) / SlotMinutes;
        public const float ReservationChance = 0.26f;

        // 오늘의 목표 점수. 레벨별로 보수적으로 예상한 하루 점수의 약 70% (A등급), 1.2배면 S등급.
        // 예상 하루 점수(인턴이 도운 뒤 내가 직접 한 일만): Lv.1 약 160, Lv.2 약 180, Lv.3 약 240, Lv.4 약 260, Lv.5 약 300, Lv.6 약 325
        static readonly int[] TargetByLevel = { 60, 110, 125, 165, 180, 210, 230 };

        public static int TargetScore(int level)
        {
            return TargetByLevel[Mathf.Clamp(level, 0, TargetByLevel.Length - 1)];
        }

        // 하루 평가 등급
        public static string Grade(int score, int target)
        {
            if (score >= target * 1.2f) return "S";
            if (score >= target) return "A";
            if (score >= target * 0.6f) return "B";
            return "C";
        }

        public static string Comment(string grade)
        {
            switch (grade)
            {
                case "S": return "원장님이 말없이 고개를 끄덕였어요.";
                case "A": return "선생님들이 수고했다고 한마디씩 건네요.";
                case "B": return "내일은 조금 더 잘할 수 있을 거예요.";
                default: return "오늘은 정신없는 하루였어요. 내일 다시 힘내요!";
            }
        }

        public static string FormatClock(int minutes)
        {
            return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }
    }
}
