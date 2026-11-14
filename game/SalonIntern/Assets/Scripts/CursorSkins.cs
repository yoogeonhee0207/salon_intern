using UnityEngine;

namespace SalonIntern
{
    // 미니게임 도구(커서) 꾸미기: 도구마다 모양(색) 4가지, 포인트로 구매해서 바꿔 낀다.
    public static class CursorSkins
    {
        public const int ToolCount = 8;
        public const int SkinCount = 4;

        // 순서는 ExamBase.CursorKind (Shower, Foam, Comb, Dryer, Scissors, Brush)와 같다
        public static readonly string[] ToolNames = { "샤워 호스", "거품", "빗", "드라이기", "가위", "염색 붓", "유니폼", "인테리어" };
        public static readonly int[] Cost = { 0, 60, 120, 300 };

        public static readonly string[][] SkinNames =
        {
            new[] { "기본 회색", "파랑 호스", "금색 헤드", "분홍 호스" },
            new[] { "하얀 거품", "분홍 거품", "하늘 거품", "민트 거품" },
            new[] { "갈색 빗", "은색 빗", "금색 빗", "분홍 빗" },
            new[] { "회색 드라이기", "민트 드라이기", "로즈골드", "블랙" },
            new[] { "은색 가위", "금색 가위", "파랑 손잡이", "보라 손잡이" },
            new[] { "기본 붓", "금색 손잡이", "파랑 솔", "초록 솔" },
            new[] { "초록 유니폼", "파랑 유니폼", "분홍 유니폼", "검정 유니폼" },
            new[] { "베이지 인테리어", "민트 인테리어", "분홍 인테리어", "우드 인테리어" },
        };

        // 대표색(몸통)과 포인트색(끝부분)
        static readonly Color[][] MainColors =
        {
            new[] { C(0.55f, 0.58f, 0.64f), C(0.30f, 0.50f, 0.90f), C(0.55f, 0.58f, 0.64f), C(0.90f, 0.45f, 0.70f) },
            new[] { C(1.00f, 1.00f, 1.00f), C(1.00f, 0.80f, 0.88f), C(0.80f, 0.92f, 1.00f), C(0.80f, 1.00f, 0.90f) },
            new[] { C(0.60f, 0.40f, 0.25f), C(0.80f, 0.82f, 0.88f), C(0.95f, 0.78f, 0.30f), C(0.95f, 0.60f, 0.75f) },
            new[] { C(0.35f, 0.35f, 0.42f), C(0.40f, 0.80f, 0.70f), C(0.85f, 0.55f, 0.50f), C(0.15f, 0.15f, 0.18f) },
            new[] { C(0.80f, 0.82f, 0.88f), C(0.95f, 0.80f, 0.30f), C(0.80f, 0.82f, 0.88f), C(0.80f, 0.82f, 0.88f) },
            new[] { C(0.55f, 0.40f, 0.30f), C(0.95f, 0.80f, 0.30f), C(0.55f, 0.40f, 0.30f), C(0.55f, 0.40f, 0.30f) },
            new[] { C(0.25f, 0.55f, 0.35f), C(0.25f, 0.45f, 0.85f), C(0.90f, 0.45f, 0.65f), C(0.18f, 0.18f, 0.22f) },   // 유니폼: 내 캐릭터 색
            new[] { C(0.86f, 0.80f, 0.72f), C(0.78f, 0.90f, 0.85f), C(0.95f, 0.83f, 0.86f), C(0.72f, 0.58f, 0.42f) },   // 인테리어: 바닥색
        };
        static readonly Color[][] AccentColors =
        {
            new[] { C(0.70f, 0.74f, 0.80f), C(0.55f, 0.75f, 1.00f), C(0.95f, 0.80f, 0.30f), C(0.95f, 0.70f, 0.85f) },
            new[] { C(0.95f, 0.98f, 1.00f), C(1.00f, 0.70f, 0.82f), C(0.70f, 0.85f, 1.00f), C(0.70f, 0.95f, 0.85f) },
            new[] { C(0.60f, 0.40f, 0.25f), C(0.80f, 0.82f, 0.88f), C(0.95f, 0.78f, 0.30f), C(0.95f, 0.60f, 0.75f) },
            new[] { C(0.95f, 0.55f, 0.25f), C(0.90f, 0.95f, 0.60f), C(0.95f, 0.80f, 0.50f), C(0.90f, 0.20f, 0.20f) },
            new[] { C(0.85f, 0.35f, 0.40f), C(0.80f, 0.30f, 0.30f), C(0.30f, 0.50f, 0.90f), C(0.60f, 0.40f, 0.90f) },
            new[] { C(0.90f, 0.40f, 0.62f), C(0.90f, 0.40f, 0.62f), C(0.30f, 0.50f, 0.95f), C(0.35f, 0.75f, 0.45f) },
            new[] { C(0.25f, 0.55f, 0.35f), C(0.25f, 0.45f, 0.85f), C(0.90f, 0.45f, 0.65f), C(0.18f, 0.18f, 0.22f) },
            new[] { C(0.93f, 0.89f, 0.83f), C(0.88f, 0.96f, 0.92f), C(0.98f, 0.92f, 0.93f), C(0.82f, 0.70f, 0.55f) },   // 인테리어: 복도색
        };

        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        // 장착 중인 모양 번호와 보유 여부 (저장됨)
        public static readonly int[] Equipped = new int[ToolCount];
        public static readonly bool[] Owned = new bool[ToolCount * SkinCount];

        static CursorSkins() { Reset(); }

        public static void Reset()
        {
            for (int t = 0; t < ToolCount; t++) Equipped[t] = 0;
            for (int i = 0; i < Owned.Length; i++) Owned[i] = i % SkinCount == 0; // 기본 모양은 처음부터 가짐
        }

        public static bool IsOwned(int tool, int skin) { return Owned[tool * SkinCount + skin]; }
        public static Color MainColor(int tool, int skin) { return MainColors[tool][skin]; }
        public static Color AccentColor(int tool, int skin) { return AccentColors[tool][skin]; }
        public static Color MainColor(int tool) { return MainColors[tool][Equipped[tool]]; }
        public static Color AccentColor(int tool) { return AccentColors[tool][Equipped[tool]]; }
    }
}
