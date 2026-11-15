using UnityEngine;

namespace SalonIntern
{
    // 같이 일하는 인턴의 성격 10종. 새 게임마다 한 번 정해져 저장된다.
    // 성격은 작업 속도, 솜씨, 스태미너가 줄고 차는 속도에 아주 조금씩 영향을 주고, 상황마다 다른 대사를 한다.
    public partial class GameController
    {
        class InternTrait
        {
            public string name;
            public float work;    // 작업 시간 배율 (1보다 크면 느림)
            public int sat;       // 솜씨(만족도) 보정
            public float drain;   // 스태미너가 줄어드는 속도 배율
            public float rest;    // 휴게 중 스태미너가 차는 속도 배율
            public string[][] lines;   // 상황별 대사: 0 아침 소개, 1 휴게 갈 때, 2 도우러 올 때, 3 일 끝냈을 때, 4 지쳤을 때
        }

        const int LineIntro = 0, LineBreak = 1, LineHelp = 2, LineDone = 3, LineTired = 4;

        static readonly InternTrait[] InternTraits =
        {
            new InternTrait { name = "깐깐함", work = 1.10f, sat = 6, drain = 1f, rest = 1f, lines = new[] {
                new[] { "오늘도 수건 각 맞춰서 갑니다.", "대충은 없어요. 제대로 해요." },
                new[] { "손 씻고 오겠습니다. 금방 올게요.", "휴게실 정리부터 하고 쉴게요." },
                new[] { "제가 할게요. 이쪽 다시 볼게요.", "그대로 두면 마음에 걸려서요." },
                new[] { "마무리까지 확인했어요.", "한 올도 안 삐져나왔어요." },
                new[] { "집중이 흐트러져요. 잠깐 쉬어야겠어요.", "이 상태로는 실수할 것 같아요." } } },
            new InternTrait { name = "눈치 빠름", work = 0.90f, sat = 0, drain = 1f, rest = 1f, lines = new[] {
                new[] { "오늘 손님 많겠는데요. 미리 준비해둘게요.", "표정 보면 알아요. 바로 움직일게요." },
                new[] { "지금 한가할 때 잠깐 쉬다 올게요.", "손님 뜸할 때 다녀올게요." },
                new[] { "바쁘신 것 같아서 먼저 왔어요.", "말 안 하셔도 알죠. 제가 받을게요." },
                new[] { "다음 손님 오기 전에 끝냈어요.", "자리도 미리 비워뒀어요." },
                new[] { "다들 바쁜 거 알지만 잠깐만 쉴게요.", "더 버티면 오히려 민폐일 것 같아요." } } },
            new InternTrait { name = "느긋함", work = 1.10f, sat = 0, drain = 0.70f, rest = 1f, lines = new[] {
                new[] { "서두르지 말고 천천히 해요~", "오늘도 평화롭게 가요." },
                new[] { "차 한잔하고 올게요.", "잠깐 숨 좀 돌릴게요~" },
                new[] { "어머, 제가 할게요. 천천히 해도 되죠?", "같이 해요~ 급할 거 없어요." },
                new[] { "다 됐어요~ 어때요?", "여유롭게 끝냈어요." },
                new[] { "이제야 좀 피곤하네요... 쉬어요.", "느긋해도 지치긴 하네요." } } },
            new InternTrait { name = "덜렁이", work = 0.95f, sat = -6, drain = 1f, rest = 1f, lines = new[] {
                new[] { "어... 수건 어디 뒀더라? 아, 여기!", "오늘은 안 실수할 거예요! 아마도요." },
                new[] { "앗, 쉬는 시간이었네요! 다녀올게요!", "가위 어디 놨지... 아 일단 쉬러 가요." },
                new[] { "제가 도울게요! 어? 이게 맞나?", "달려왔어요! 뭐부터 하면 되죠?" },
                new[] { "끝났어요! 빠진 건 없겠죠?", "다 했어요! ...아마도요." },
                new[] { "어... 머리가 핑 도는데요.", "손에 힘이 안 들어가요. 쉬어야겠어요." } } },
            new InternTrait { name = "성실함", work = 1.00f, sat = 3, drain = 0.85f, rest = 1f, lines = new[] {
                new[] { "오늘도 열심히 하겠습니다!", "하나씩 차근차근 해요." },
                new[] { "정해진 시간만큼만 쉬고 올게요.", "충전하고 오겠습니다." },
                new[] { "도울 일 있으면 말씀하세요!", "제가 맡을게요. 믿고 맡겨주세요." },
                new[] { "맡은 일은 끝까지 했습니다.", "꼼꼼하게 마무리했어요." },
                new[] { "조금 무리한 것 같아요. 쉬고 올게요.", "쉬어야 더 잘할 수 있어요." } } },
            new InternTrait { name = "수다쟁이", work = 1.10f, sat = 0, drain = 1f, rest = 1.30f, lines = new[] {
                new[] { "어제 말이에요, 글쎄 손님이...", "오늘 날씨 진짜 좋죠? 아 참, 일해야지." },
                new[] { "쉬는 김에 동기랑 수다 떨고 올게요!", "휴게실 가면 할 얘기가 산더미예요." },
                new[] { "도와줄게요! 근데 그거 아세요?", "제가 할게요~ 하면서 얘기해요!" },
                new[] { "다 했어요! 근데 있잖아요...", "끝났어요! 손님이 방금 그러시는데요," },
                new[] { "말을 너무 많이 했나 봐요. 목이 아파요.", "입이 먼저 지쳤어요. 쉬고 올게요." } } },
            new InternTrait { name = "겁쟁이", work = 1.00f, sat = -3, drain = 1.10f, rest = 1f, lines = new[] {
                new[] { "오늘 원장님 안 오시죠...?", "실수하면 어떡하죠... 일단 해볼게요." },
                new[] { "너무 긴장돼서 잠깐 쉬어도 될까요?", "심호흡하고 올게요." },
                new[] { "저, 저라도 도울게요! 틀리면 말해주세요.", "제가 해도 될까요...? 해볼게요." },
                new[] { "끝났는데... 괜찮을까요?", "손님이 불만 없으셨으면 좋겠어요." },
                new[] { "손이 떨려서 못 하겠어요...", "너무 무서워서 힘이 빠졌어요." } } },
            new InternTrait { name = "승부욕", work = 0.90f, sat = 0, drain = 1.25f, rest = 1f, lines = new[] {
                new[] { "오늘은 제가 제일 많이 할 거예요!", "누가 더 빠른지 볼까요?" },
                new[] { "금방 충전하고 더 빨리 돌아올게요.", "잠깐만요, 곧 다시 달립니다." },
                new[] { "그 일, 제가 먼저 잡았어요!", "이건 제 몫이에요, 비켜보세요!" },
                new[] { "벌써 끝! 제가 제일 빠르죠?", "한 건 추가요!" },
                new[] { "너무 달렸나... 그래도 지지 않아요.", "잠깐 쉬고 바로 복귀합니다!" } } },
            new InternTrait { name = "낙천적", work = 1.00f, sat = 0, drain = 1f, rest = 1.30f, lines = new[] {
                new[] { "오늘도 좋은 일만 있을 거예요!", "뭐든 잘 될 거예요, 파이팅!" },
                new[] { "쉬는 시간이다! 다녀올게요~", "잠깐 쉬면 또 힘이 솟아요." },
                new[] { "걱정 마세요, 같이 하면 금방이에요!", "제가 도울게요! 잘될 거예요." },
                new[] { "다 했어요! 오늘도 잘 풀리네요~", "역시 되는 날은 되네요!" },
                new[] { "조금 지쳤지만 괜찮아요. 쉬면 나아져요!", "쉬고 오면 두 배로 할게요." } } },
            new InternTrait { name = "소심함", work = 1.10f, sat = 3, drain = 1f, rest = 1f, lines = new[] {
                new[] { "저... 오늘도 잘 부탁드려요.", "조용히 열심히 할게요." },
                new[] { "저, 잠깐 쉬다 와도 될까요...?", "죄송한데 잠깐만 쉬고 올게요." },
                new[] { "저기... 제가 도와드려도 될까요?", "방해될까 봐 조심스러운데... 도울게요." },
                new[] { "저... 다 했어요.", "혹시 부족하면 다시 할게요." },
                new[] { "말씀드리기 죄송한데 많이 피곤해요...", "조용히 쉬고 올게요." } } },
        };

        // 인턴장은 이 셋 중에서만 정해진다 (책임감 있는 역할)
        static readonly int[] LeaderTraits = { 0, 1, 2 };

        readonly int[] internTrait = { -1, -1, -1 };

        // 새 게임이나, 성격이 저장되지 않은 옛 저장을 불러왔을 때 정한다
        void EnsureInternTraits()
        {
            for (int i = 0; i < internTrait.Length; i++)
            {
                if (internTrait[i] >= 0 && internTrait[i] < InternTraits.Length) continue;
                internTrait[i] = i == 0 ? LeaderTraits[Random.Range(0, LeaderTraits.Length)] : Random.Range(0, InternTraits.Length);
            }
        }

        InternTrait TraitOf(Npc n)
        {
            int i = n.id - 1;
            if (i < 0 || i >= internTrait.Length || internTrait[i] < 0) return null;
            return InternTraits[internTrait[i]];
        }

        string NpcTraitName(Npc n) { InternTrait t = TraitOf(n); return t != null ? t.name : ""; }

        // 상황별 대사 한 줄을 아래쪽 안내 줄에 보여준다. chance는 말할 확률
        void NpcSpeak(Npc n, int situation, float chance = 1f)
        {
            InternTrait t = TraitOf(n);
            if (t == null || Random.value > chance) return;
            string[] pool = t.lines[situation];
            string line = n.name + ": \"" + pool[Random.Range(0, pool.Length)] + "\"";
            if (toastTimer <= 0f) Toast(line, false);   // 다른 알림이 떠 있으면 덮어쓰지 않고 아래쪽 안내 줄에 보여준다
            else Say(line);
        }
    }
}
