using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SalonIntern
{
    // 하루 구조(아침 -> 영업 -> 마감 정산)와 요청 카드 시스템 프로토타입.
    // 씬에 아무것도 놓지 않아도 Play를 누르면 이 스크립트가 자동으로 실행된다.
    public class GameController : MonoBehaviour
    {
        enum Phase { Morning, Business, Evening }

        class Request
        {
            public DesignerData designer;
            public string label;
            public bool isProcedure;
            public bool isLocked;
            public bool isReserved;
            public bool isVip;
            public int procIdx = -1; // 시술 번호 (0 샴푸 ~ 3 드라이)
            public int[] route;      // 시술 단계 순서 (예: 펌 = 샴푸, 커트, 펌, 샴푸, 드라이)
            public int routeIdx;     // 지금 진행 중인 단계 번호
            public bool started;     // 손님을 모시고 출발했는지
            public bool isMess;      // 커트 뒤에 생긴 바닥 어지럼 (바닥 청소 미니게임)
            public bool useAnchor;   // 디자이너 자리가 아니라 anchor 위치에 말풍선을 띄운다
            public Vector2 anchor;
            public int station = -1; // 시술 구역 (0 샴푸실, 1 시술석, -1 자리에서 처리)
            public float timeLimit;
            public float remaining;

            public Image background;
            public RectTransform barFill;
            public Image barFillImage;
        }

        DesignerData[] designers;
        readonly List<Request> active = new List<Request>();

        RectTransform canvasRoot;
        RectTransform mapArea;   // 미용실 바닥 (위에서 내려다본 그림)

        // 도형 캐릭터와 이동
        readonly Dictionary<DesignerData, Vector2> seats = new Dictionary<DesignerData, Vector2>();
        RectTransform playerRt;
        Vector2 playerTarget;
        bool playerWalking;
        Request playerTask;      // 도착하면 처리할 요청
        bool restWalking;        // 휴게실로 걸어가는 중
        const float PlayerSpeed = 1200f;

        // 시술 구역: 0 샴푸실, 1 시술석, (커트석은 Lv.5부터 쓸 예정이라 지금은 모양만)
        static readonly Vector2[] StationPos = { new Vector2(775f, 215f), new Vector2(775f, 0f), new Vector2(775f, -215f) };
        static readonly string[] StationNames = { "샴푸실", "시술석", "커트석" };
        RectTransform customerRt;      // 인턴이 데리고 다니는 손님
        bool waitingAtStation;         // 디자이너가 커트하는 동안 기다리는 중
        float stationWait;
        bool carrying;
        bool customerReturning;
        Vector2 customerReturnTo;
        static readonly Vector2 RestPos = new Vector2(-790f, 110f);
        static readonly Vector2 SupplyPos = new Vector2(-790f, -110f);
        bool minigameActive; // 미니게임 중에는 시계와 요청 시간이 멈춘다 (임시 규칙)
        Text topText;
        Text logText;

        // 하루 진행
        Phase phase = Phase.Morning;
        int day = 1;
        float businessElapsed;
        int lastClockMinute = -1;
        int dayStartLevel;
        int dayStartScore;
        int dayStartDone;
        int dayStartMissed;

        RectTransform morningPanel;
        RectTransform eveningPanel;
        Text morningTitle;
        Text morningBody;
        Text eveningTitle;
        Text eveningBody;
        RectTransform reservationArea;

        // 오늘의 예약 (아침에 뽑아 두고, 해당 시간이 되면 실제 요청 카드로 만든다)
        class Booking
        {
            public DesignerData designer;
            public int procedure;
            public bool isVip;
        }
        List<Booking>[] bookingsBySlot;
        readonly List<Booking> waitingBookings = new List<Booking>();
        int nextSlot;

        // 피로도와 휴식: 일을 하면 쌓이고, 손님이 없는 틈에 쉬면 줄어든다
        float fatigue;
        bool resting;
        float restTimer;          // 지금 하는 휴식 활동의 남은 시간(게임 시계 분)
        float restBudget = 60f;   // 오늘 남은 휴게시간(게임 시계 분). 일찍 나오면 남은 만큼 다시 쉴 수 있다
        Image fatigueFill;
        Text fatigueText;
        Button restButton;
        Button supplyButton;
        Text supplyButtonText;
        Text supplyRoomText;
        Text restButtonText;

        int level;
        int xp;
        int score;
        int done;
        int missed;
        float spawnTimer = 0.5f;

        static readonly Color CardNormal = new Color(1f, 0.98f, 0.94f);
        static readonly Color CardLocked = new Color(0.78f, 0.78f, 0.80f);
        static readonly Color BarGreen = new Color(0.30f, 0.72f, 0.40f);
        static readonly Color BarGray = new Color(0.50f, 0.50f, 0.55f);
        static readonly Color BarRed = new Color(0.88f, 0.28f, 0.28f);
        static readonly Color TextDark = new Color(0.15f, 0.15f, 0.18f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<GameController>() != null) return;
            new GameObject("GameController").AddComponent<GameController>();
        }

        void Start()
        {
            designers = DesignerLoader.Load();
            BuildUi();

            if (designers.Length == 0)
            {
                Say("디자이너 데이터를 불러오지 못했어요. Console 창을 확인해 주세요.");
                RefreshTop();
                return;
            }
            StartMorning();
        }

        void Update()
        {
            if (designers == null || designers.Length == 0) return;
            if (phase != Phase.Business) return;
            if (minigameActive) return;

            UpdateRest();
            UpdatePlayer();
            UpdateSupplies();
            UpdateRefill();
            UpdateSupplyUi();
            UpdateCustomer();
            UpdateCutWait();

            // 영업 시계 흐르기
            businessElapsed += Time.deltaTime;
            int minute = ClockMinutes();
            if (minute != lastClockMinute)
            {
                lastClockMinute = minute;
                RefreshTop();
            }
            if (businessElapsed >= DayConfig.BusinessSeconds)
            {
                EndBusiness();
                return;
            }

            // 예약 시간이 된 칸의 예약을 대기열에 넣고, 디자이너가 비면 카드로 만든다
            ReleaseBookings(minute);
            SpawnWaitingBookings();

            // 제한시간 줄이기
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Request r = active[i];
                r.remaining -= Time.deltaTime;

                float ratio = Mathf.Clamp01(r.remaining / r.timeLimit);
                r.barFill.anchorMax = new Vector2(ratio, 1f);
                r.barFillImage.color = ratio < 0.25f ? BarRed : (r.isLocked ? BarGray : BarGreen);

                if (r.remaining <= 0f) Expire(r);
            }

            // 빈 자리가 있으면 새 요청 만들기
            if (active.Count < LevelConfig.MaxConcurrent[level])
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    TrySpawn();
                    spawnTimer = Random.Range(1.5f, 4f);
                }
            }
        }

        // ---------- 하루 흐름 ----------

        int ClockMinutes()
        {
            float t = Mathf.Clamp01(businessElapsed / DayConfig.BusinessSeconds);
            return DayConfig.OpenMinutes + Mathf.FloorToInt(t * (DayConfig.CloseMinutes - DayConfig.OpenMinutes));
        }

        string XpLabel()
        {
            return level >= LevelConfig.MaxLevel ? "MAX" : xp + "/" + LevelConfig.XpToNext(level);
        }

        void StartMorning()
        {
            phase = Phase.Morning;
            fatigue = 0f;
            resting = false;
            restWalking = false;
            playerWalking = false;
            playerTask = null;
            businessElapsed = 0f;
            lastClockMinute = -1;

            morningTitle.text = "Day " + day + "   아침 준비";
            morningBody.text =
                "오늘의 목표 점수  " + DayConfig.TargetScore(level)
                + "      현재  Lv." + level + " " + LevelConfig.Names[level] + "   XP " + XpLabel();
            BuildReservationGrid();

            morningPanel.gameObject.SetActive(true);
            eveningPanel.gameObject.SetActive(false);
            Say("아침 준비 중. 예약표를 확인하고 영업을 시작하세요.");
            RefreshTop();
        }

        void StartBusiness()
        {
            phase = Phase.Business;
            businessElapsed = 0f;
            lastClockMinute = -1;
            spawnTimer = 0.5f;
            nextSlot = 0;
            waitingBookings.Clear();
            restBudget = RestSeconds;
            InitStocks();
            fatigue = 0f;
            resting = false;
            supplyWalking = false;
            refilling = false;

            dayStartLevel = level;
            dayStartScore = score;
            dayStartDone = done;
            dayStartMissed = missed;

            morningPanel.gameObject.SetActive(false);
            Say("영업 시작! 카드를 눌러 요청을 처리하세요.");
            RefreshTop();
        }

        void EndBusiness()
        {
            phase = Phase.Evening;

            // 남은 요청은 디자이너들이 알아서 마무리한 것으로 처리 (감점 없음)
            foreach (Request r in active) Destroy(r.background.gameObject);
            active.Clear();
            waitingBookings.Clear();
            playerTask = null;
            playerWalking = false;
            restWalking = false;
            resting = false;
            ClearCustomer();
            waitingAtStation = false;
            supplyWalking = false;
            refilling = false;

            int dayScore = score - dayStartScore;
            int dayDone = done - dayStartDone;
            int dayMissed = missed - dayStartMissed;
            int target = DayConfig.TargetScore(dayStartLevel);
            string grade = DayConfig.Grade(dayScore, target);

            string levelLine = level > dayStartLevel
                ? "레벨 업!  Lv." + dayStartLevel + " → Lv." + level + " " + LevelConfig.Names[level]
                : "Lv." + level + " " + LevelConfig.Names[level];

            eveningTitle.text = "Day " + day + "   마감 정산";
            eveningBody.text =
                "오늘의 평가   " + grade + "\n\n"
                + "오늘 점수   " + dayScore + "   (목표 " + target + ")\n"
                + "완료 " + dayDone + "건     놓침 " + dayMissed + "건\n\n"
                + levelLine + "     XP " + XpLabel() + "\n\n"
                + DayConfig.Comment(grade);

            eveningPanel.gameObject.SetActive(true);
            Say("영업 종료! 오늘 하루 수고했어요.");
            RefreshTop();
        }

        void NextDay()
        {
            day++;
            StartMorning();
        }

        // 오늘의 예약표: 세로줄 = 디자이너, 가로줄 = 시간, 칸 = 시술 (해당 시간이 되면 실제 요청 카드가 된다)
        static readonly Color[] ProcedureColors =
        {
            new Color(0.70f, 0.85f, 1.00f), // 샴푸
            new Color(1.00f, 0.80f, 0.90f), // 롤
            new Color(1.00f, 0.85f, 0.65f), // 염색
            new Color(0.75f, 0.92f, 0.75f), // 드라이
            new Color(0.85f, 0.80f, 0.95f), // 커트
            new Color(0.98f, 0.95f, 0.70f), // 탈색
        };

        void BuildReservationGrid()
        {
            foreach (Transform child in reservationArea) Destroy(child.gameObject);

            int rows = designers.Length;
            int cols = DayConfig.ReservationSlots;

            // 예약 뽑기: 칸마다 확률로 예약이 생기고, 시술은 디자이너 확률표로 정한다
            int[,] proc = new int[rows, cols];
            List<Vector2Int> booked = new List<Vector2Int>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    proc[r, c] = -1;
                    if (Random.value < DayConfig.ReservationChance)
                    {
                        proc[r, c] = PickProcedure(designers[r]);
                        booked.Add(new Vector2Int(r, c));
                    }
                }
            }
            // 예약 중 하나는 VIP로 가려서 보여준다
            Vector2Int vip = booked.Count > 0 ? booked[Random.Range(0, booked.Count)] : new Vector2Int(-1, -1);

            // 실제 영업에서 쓸 수 있게 시간 칸별로 저장
            bookingsBySlot = new List<Booking>[cols];
            for (int c = 0; c < cols; c++)
            {
                bookingsBySlot[c] = new List<Booking>();
                for (int r = 0; r < rows; r++)
                {
                    if (proc[r, c] < 0) continue;
                    bookingsBySlot[c].Add(new Booking
                    {
                        designer = designers[r],
                        procedure = proc[r, c],
                        isVip = (vip.x == r && vip.y == c),
                    });
                }
            }

            float nameW = 0.26f;
            float colW = (1f - nameW) / cols;
            float rowH = 1f / (rows + 1);
            Color headerBg = new Color(0.20f, 0.20f, 0.26f);
            Color nameBg = new Color(0.90f, 0.86f, 0.80f);
            Color emptyBg = new Color(1f, 1f, 1f, 0.85f);

            // 맨 윗줄: 시간
            GridCell(0f, nameW, 0, rowH, "디자이너 / 시간", headerBg, Color.white, 22);
            for (int c = 0; c < cols; c++)
            {
                string time = DayConfig.FormatClock(DayConfig.OpenMinutes + c * 60);
                GridCell(nameW + c * colW, nameW + (c + 1) * colW, 0, rowH, time, headerBg, Color.white, 22);
            }

            // 디자이너 한 명당 한 줄
            for (int r = 0; r < rows; r++)
            {
                DesignerData d = designers[r];
                GridCell(0f, nameW, r + 1, rowH, d.name + " · " + d.specialty, nameBg, TextDark, 20);

                for (int c = 0; c < cols; c++)
                {
                    float x0 = nameW + c * colW;
                    float x1 = nameW + (c + 1) * colW;

                    if (proc[r, c] < 0)
                    {
                        GridCell(x0, x1, r + 1, rowH, "", emptyBg, TextDark, 22);
                    }
                    else if (vip.x == r && vip.y == c)
                    {
                        GridCell(x0, x1, r + 1, rowH, "VIP", new Color(0.95f, 0.80f, 0.35f), TextDark, 22);
                    }
                    else
                    {
                        GridCell(x0, x1, r + 1, rowH, LevelConfig.ProcedureShortNames[proc[r, c]],
                            ProcedureColors[proc[r, c]], TextDark, 22);
                    }
                }
            }
        }

        void GridCell(float x0, float x1, int row, float rowH, string text, Color bg, Color fg, int size)
        {
            float yMax = 1f - row * rowH;
            float yMin = yMax - rowH;

            Image cell = UiFactory.Panel("Cell", reservationArea, bg);
            UiFactory.Stretch(cell.rectTransform, x0, yMin, x1, yMax);
            cell.rectTransform.offsetMin = new Vector2(1.5f, 1.5f);
            cell.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);

            Text label = UiFactory.Label("Text", cell.transform, text, size, fg, TextAnchor.MiddleCenter);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ---------- 피로도와 휴식 ----------

        const float RestSeconds = 60f;     // 하루 휴게시간 최대 60분 (게임 시계 기준)
        const int ScoldPenalty = 20;        // 손님이 밀렸는데 쉬면 깎이는 점수

        // 휴식 활동: 활동마다 걸리는 시간이 다르고, 하루 휴게시간(1시간)에서 빠진다 (밥 먹기는 점심시간에 1.5배, 낮잠은 잔소리 기준이 1 낮아짐)
        static readonly string[] RestNames = { "밥 먹기", "낮잠", "핸드폰 하기", "스트레칭" };
        static readonly int[] RestMinutes = { 30, 40, 15, 10 };  // 활동마다 걸리는 시간 (게임 시계 분)
        static readonly int[] RestRecover = { 30, 50, 10, 10 };  // 끝까지 했을 때 줄어드는 피로도
        const int MinRestMinutes = 10;                            // 가장 짧은 활동
        static readonly string[] RestNotes =
        {
            "든든하게 먹기\n(점심시간엔 1.5배)",
            "제일 많이 회복\n잠들면 손님이 밀려도 늦게 알아채요",
            "조금만 회복\n그래도 재밌어요",
            "가볍게 몸 풀기",
        };
        int restKind;
        float restRate;
        RectTransform restChoicePanel;

        // 재료실: 수건·도구·음료·청소용품. 시간이 지나거나 시술을 하면 줄고, 0이면 혼난다
        static readonly int[] StockMax = { 30, 20, 20, 5 };  // 수건 30, 도구 20, 음료 20, 청소용품 5
        const int ScoldSupplyPenalty = 10;
        static readonly string[] StockNames = { "수건", "도구", "음료", "청소용품" };
        static readonly string[] StockActions = { "수건 접기", "도구 정리", "음료 채우기", "청소용품 채우기" };
        static readonly float[] RefillMinutes = { 4f, 4f, 4f, 4f }; // 채우기를 누르면 전부 채워지는 데 걸리는 게임 분 (현실 약 2초)
        static readonly float[] StockDrainMin = { 8f, 10f, 10f, 14f };    // 시간으로 1개 줄어드는 간격(현실 초)
        static readonly float[] StockDrainMax = { 14f, 16f, 16f, 26f };
        // 시술을 끝내면 줄어드는 재료: 샴푸 / 롤 / 염색 / 드라이
        static readonly int[][] ProcedureUses = { new[] { 0 }, new[] { 1, 2 }, new[] { 1, 0 }, new[] { 0, 3 }, new[] { 1 } };
        readonly int[] stock = { 30, 20, 20, 5 };
        readonly float[] stockDrain = new float[4];      // 다음에 1개 줄어들 때까지 현실 초
        readonly float[] scoldCooldown = new float[4];   // 같은 재료로 연달아 혼나지 않게
        bool supplyWalking;
        bool refilling;
        int refillKind;
        float refillProgress;    // 채우기 진행 (0~1)
        int refillStartStock;
        int refilledTotal;
        RectTransform supplyChoicePanel;

        // 피로도가 쌓일수록 요청 제한시간이 짧아진다 (최대 30%)
        float TimeLimitFor(DesignerData d, int procedure = -1)
        {
            float tired = 1f - 0.3f * (fatigue / 100f);
            int[] steps = procedure >= 0 ? RouteFor(procedure) : null;
            float longer = steps != null ? 1f + 0.5f * (steps.Length - 1) : 1f; // 단계가 많은 시술은 시간을 더 준다
            return LevelConfig.TimeLimit[level] * Mathf.Max(0.3f, d.patienceMultiplier) * tired * longer;
        }

        bool IsLunchTime()
        {
            int m = ClockMinutes();
            return m >= 12 * 60 && m < 14 * 60;
        }

        // 하던 일을 그만두고 나갈 때: 데리고 가던 손님은 자리로 돌려보내고 그 요청은 처음부터 다시
        void AbandonTask()
        {
            if (playerTask != null)
            {
                if (playerTask.route != null) { playerTask.routeIdx = 0; playerTask.started = false; }
                if (carrying) SendCustomerBack(SeatPos(playerTask.designer));
            }
            waitingAtStation = false;
            playerTask = null;
        }

        bool CanRest()
        {
            return phase == Phase.Business && !minigameActive && !waitingAtStation && !resting && !restWalking && restBudget >= MinRestMinutes;
        }

        bool CanRestHere()
        {
            return phase == Phase.Business && !minigameActive;
        }

        void StartRest()
        {
            if (restBudget < MinRestMinutes) { Say("오늘 휴게시간은 다 썼어요."); return; }
            if (!CanRest() || restWalking) return;
            restWalking = true;
            supplyWalking = false;
            refilling = false;
            AbandonTask();
            playerTarget = RestPos;
            playerWalking = true;
            Say("휴게실로 가는 중...");
        }

        // ---------- 재료실 ----------

        // 재고가 최대의 20% 이하(최소 1개)면 부족 경고 색
        bool LowStock(int k)
        {
            return stock[k] <= Mathf.Max(1, StockMax[k] / 5);
        }

        void InitStocks()
        {
            for (int k = 0; k < stock.Length; k++)
            {
                stock[k] = StockMax[k];
                stockDrain[k] = Random.Range(StockDrainMin[k], StockDrainMax[k]);
                scoldCooldown[k] = 0f;
            }
        }

        // 시간이 지나면 재료가 줄고, 0인 재료가 있으면 선배에게 혼난다
        void UpdateSupplies()
        {
            for (int k = 0; k < stock.Length; k++)
            {
                scoldCooldown[k] -= Time.deltaTime;
                stockDrain[k] -= Time.deltaTime;
                if (stockDrain[k] <= 0f)
                {
                    stockDrain[k] = Random.Range(StockDrainMin[k], StockDrainMax[k]);
                    if (stock[k] > 0) stock[k]--;
                }
                if (stock[k] <= 0) ScoldSupply(k);
            }
        }

        void ScoldSupply(int k)
        {
            if (scoldCooldown[k] > 0f) return;
            if (refilling && refillKind == k) return; // 지금 채우는 중이면 봐준다
            scoldCooldown[k] = 20f;
            score = Mathf.Max(0, score - ScoldSupplyPenalty);
            Say("선배들 잔소리: " + StockNames[k] + "이(가) 떨어졌잖아! (점수 -" + ScoldSupplyPenalty + ")");
            RefreshTop();
        }

        // 시술을 끝내면 쓰이는 재료가 줄어든다. 없으면 바로 혼난다
        void ConsumeFor(int procedure)
        {
            if (procedure < 0 || procedure >= ProcedureUses.Length) return;
            foreach (int k in ProcedureUses[procedure])
            {
                if (stock[k] > 0) stock[k]--;
                else ScoldSupply(k);
            }
        }

        void StartSupply()
        {
            if (phase != Phase.Business || minigameActive || supplyWalking || waitingAtStation) return;
            resting = false;
            restWalking = false;
            refilling = false;
            AbandonTask();
            supplyWalking = true;
            playerTarget = SupplyPos;
            playerWalking = true;
            Say("재료실로 가는 중...");
        }

        void ShowSupplyChoice()
        {
            minigameActive = true;

            Image dim = UiFactory.Panel("SupplyChoice", canvasRoot, new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);
            supplyChoicePanel = dim.rectTransform;

            Text title = UiFactory.Label("Title", dim.transform, "재료실  -  무엇을 채울까요?", 44, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.78f, 1f, 0.92f);

            for (int i = 0; i < StockNames.Length; i++)
            {
                int kind = i;
                bool needs = stock[i] < StockMax[i];
                string text = StockActions[i] + "\n\n" + StockNames[i] + " " + stock[i] + " / " + StockMax[i]
                    + "\n\n" + (needs ? "전부 채우기 (" + RefillMinutes[i] + "분)" : "가득 찼어요");
                Button b = UiFactory.MakeButton("Stock_" + StockNames[i], dim.transform, text, 30,
                    LowStock(i) ? new Color(0.75f, 0.35f, 0.35f) : new Color(0.45f, 0.60f, 0.45f), () => PickRefill(kind));
                b.interactable = needs;
                float x0 = 0.05f + i * 0.2375f;
                UiFactory.Stretch(b.GetComponent<RectTransform>(), x0, 0.30f, x0 + 0.215f, 0.72f);
            }

            Button cancel = UiFactory.MakeButton("SupplyCancel", dim.transform, "그냥 일하러 가기", 34,
                new Color(0.55f, 0.40f, 0.40f), CancelSupplyChoice);
            UiFactory.Stretch(cancel.GetComponent<RectTransform>(), 0.35f, 0.10f, 0.65f, 0.21f);
        }

        void CloseSupplyChoice()
        {
            if (supplyChoicePanel != null) Destroy(supplyChoicePanel.gameObject);
            supplyChoicePanel = null;
            minigameActive = false;
        }

        void PickRefill(int kind)
        {
            CloseSupplyChoice();
            refilling = true;
            refillKind = kind;
            refillProgress = 0f;
            refillStartStock = stock[kind];
            Say(StockActions[kind] + " 시작! (손님이 부르면 말풍선을 눌러 중단)");
        }

        void CancelSupplyChoice()
        {
            CloseSupplyChoice();
            Say("재료는 나중에 채워요. 일하러 가요!");
        }

        // 채우는 동안 게임 시간이 흘러 한 개씩 채워진다. 중간에 나가도 채운 만큼은 남는다
        void UpdateRefill()
        {
            if (!refilling) return;
            int k = refillKind;
            refillProgress += Time.deltaTime * DayConfig.MinutesPerSecond / RefillMinutes[k];

            if (refillProgress >= 1f)
            {
                stock[k] = StockMax[k];
                refilling = false;
                AddXp(1); // 한 번 다 채울 때마다 XP +1
                Say(StockNames[k] + " 가득 채웠어요!");
                return;
            }

            // 채우는 중에는 조금씩 차오르고, 중간에 나가면 찬 만큼만 남는다
            int target = refillStartStock + Mathf.FloorToInt(refillProgress * (StockMax[k] - refillStartStock));
            if (target > stock[k]) stock[k] = target;
        }

        void UpdateSupplyUi()
        {
            if (supplyRoomText == null) return;
            string t = "";
            for (int k = 0; k < stock.Length; k++)
            {
                string line = StockNames[k] + " " + stock[k] + "/" + StockMax[k];
                t += (LowStock(k) ? "<color=#C00000>" + line + "</color>" : line) + "\n";
            }
            supplyRoomText.text = t;

            bool can = phase == Phase.Business && !minigameActive && !supplyWalking && !waitingAtStation;
            supplyButton.interactable = can;
            supplyButtonText.text = refilling ? StockNames[refillKind] + " 채우는 중..." : (supplyWalking ? "이동 중..." : "재료실 가기");
        }

        // 휴게실에 도착하면 무엇을 하며 쉴지 고른다 (고르는 동안 시계는 멈춤)
        void ShowRestChoice()
        {
            minigameActive = true;

            Image dim = UiFactory.Panel("RestChoice", canvasRoot, new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);
            restChoicePanel = dim.rectTransform;

            Text title = UiFactory.Label("Title", dim.transform,
                "휴게실  -  뭘 할까요?   (남은 휴게시간 " + Mathf.CeilToInt(restBudget) + "분 / 피로도 " + Mathf.RoundToInt(fatigue) + ")", 44, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.78f, 1f, 0.92f);

            bool lunch = IsLunchTime();
            for (int i = 0; i < RestNames.Length; i++)
            {
                int kind = i;
                int amount = (i == 0 && lunch) ? Mathf.RoundToInt(RestRecover[i] * 1.5f) : RestRecover[i];
                bool enough = restBudget >= RestMinutes[i];
                string text = RestNames[i] + "\n\n" + RestMinutes[i] + "분 소요\n피로도 -" + amount
                    + "\n\n" + (enough ? RestNotes[i] : "(휴게시간 부족)");
                Button b = UiFactory.MakeButton("Rest_" + RestNames[i], dim.transform, text, 30,
                    new Color(0.35f, 0.55f, 0.75f), () => PickRest(kind));
                b.interactable = enough;
                float x0 = 0.05f + i * 0.2375f;
                UiFactory.Stretch(b.GetComponent<RectTransform>(), x0, 0.30f, x0 + 0.215f, 0.72f);
            }

            Button cancel = UiFactory.MakeButton("RestCancel", dim.transform, "그냥 일하러 가기", 34,
                new Color(0.55f, 0.40f, 0.40f), CancelRestChoice);
            UiFactory.Stretch(cancel.GetComponent<RectTransform>(), 0.35f, 0.10f, 0.65f, 0.21f);
        }

        void CloseRestChoice()
        {
            if (restChoicePanel != null) Destroy(restChoicePanel.gameObject);
            restChoicePanel = null;
            minigameActive = false;
        }

        void PickRest(int kind)
        {
            CloseRestChoice();
            restKind = kind;
            float amount = RestRecover[kind] * ((kind == 0 && IsLunchTime()) ? 1.5f : 1f);
            restRate = amount / RestMinutes[kind];
            restTimer = RestMinutes[kind];
            resting = true;
            Say(RestNames[kind] + " " + RestMinutes[kind] + "분 (남은 휴게시간 " + Mathf.CeilToInt(restBudget) + "분, 손님이 밀리면 잔소리!)");
        }

        void CancelRestChoice()
        {
            CloseRestChoice();
            Say("쉬는 건 나중에. 일하러 가요!");
        }

        // 쉬는 중에 손님이 너무 밀리면 선배들에게 잔소리를 듣고 쉬던 걸 멈춘다
        void InterruptRest()
        {
            if (!resting) return;
            int limit = Mathf.Max(2, LevelConfig.MaxConcurrent[level] - 1);
            if (restKind == 1) limit = Mathf.Max(1, limit - 1); // 낮잠 중에는 늦게 알아채서 더 빨리 잔소리
            if (active.Count < limit) return;

            resting = false;
            score = Mathf.Max(0, score - ScoldPenalty);
            Say("선배들 잔소리: 손님이 밀렸는데 쉬고 있었어요! (점수 -" + ScoldPenalty + ")");
            RefreshTop();
        }

        void UpdateRest()
        {
            if (resting)
            {
                fatigue = Mathf.Max(0f, fatigue - restRate * Time.deltaTime * DayConfig.MinutesPerSecond);
                float passed = Time.deltaTime * DayConfig.MinutesPerSecond; // 이번 프레임에 흐른 게임 분
                restBudget -= passed;
                restTimer -= passed;
                if (restTimer <= 0f || restBudget <= 0f || fatigue <= 0f)
                {
                    resting = false;
                    Say(RestNames[restKind] + " 끝! 다시 힘내요!" + (restBudget >= MinRestMinutes ? " (남은 휴게시간 " + Mathf.CeilToInt(restBudget) + "분)" : " (오늘 휴게시간 소진)"));
                }
            }

            if (fatigueFill == null) return;
            float ratio = Mathf.Clamp01(fatigue / 100f);
            fatigueFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            fatigueFill.color = ratio > 0.7f ? BarRed : (ratio > 0.4f ? new Color(0.95f, 0.70f, 0.25f) : BarGreen);
            fatigueText.text = "피로도 " + Mathf.RoundToInt(fatigue) + (resting ? "  (" + RestNames[restKind] + " 중)" : "");

            bool can = CanRest();
            restButton.interactable = can;
            restButtonText.text = resting ? "쉬는 중..." : (restWalking ? "이동 중..." : (restBudget >= MinRestMinutes ? "휴게실 가기 (남은 " + Mathf.CeilToInt(restBudget) + "분)" : "휴게시간 소진"));
        }

        // ---------- 요청 만들기 ----------

        void ReleaseBookings(int minute)
        {
            if (bookingsBySlot == null) return;
            while (nextSlot < bookingsBySlot.Length && minute >= DayConfig.OpenMinutes + nextSlot * 60)
            {
                waitingBookings.AddRange(bookingsBySlot[nextSlot]);
                nextSlot++;
            }
        }

        void SpawnWaitingBookings()
        {
            for (int i = 0; i < waitingBookings.Count; i++)
            {
                Booking b = waitingBookings[i];
                if (HasRequest(b.designer)) continue; // 디자이너가 다른 요청 중이면 기다린다

                bool locked = level < LevelConfig.ProcedureUnlockLevel[b.procedure];
                Request r = new Request
                {
                    designer = b.designer,
                    label = ProcLabel(b.procedure),
                    isProcedure = true,
                    isLocked = locked,
                    isReserved = true,
                    isVip = b.isVip,
                    procIdx = b.procedure,
                    route = locked ? null : RouteFor(b.procedure),
                    station = locked ? -1 : StationFor(b.procedure),
                    timeLimit = TimeLimitFor(b.designer, b.procedure),
                };
                r.remaining = r.timeLimit;
                CreateCard(r);
                active.Add(r);
                InterruptRest();
                waitingBookings.RemoveAt(i);
                Say((b.isVip ? "VIP " : "") + "예약 손님 도착: " + b.designer.name + " 선배 / " + r.label);
                return; // 한 번에 한 장씩
            }
        }

        void TrySpawn()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                DesignerData d = designers[Random.Range(0, designers.Length)];
                if (HasRequest(d)) continue; // 디자이너 한 명당 요청은 한 장

                bool procedure = true; // 잡일은 이제 카드가 아니라 재료실에서 채운다
                string label;
                bool locked = false;
                int procIndex = -1;

                if (!procedure)
                {
                    label = LevelConfig.ChoreNames[Random.Range(0, LevelConfig.ChoreNames.Length)];
                }
                else
                {
                    int p = PickProcedure(d);
                    procIndex = p;
                    label = ProcLabel(p);
                    locked = level < LevelConfig.ProcedureUnlockLevel[p];

                    // 못 하는 시술은 일부만 화면에 보여준다
                    if (locked && Random.value > LevelConfig.LockedShowRate[level]) continue;
                }

                Request r = new Request
                {
                    designer = d,
                    label = label,
                    isProcedure = procedure,
                    isLocked = locked,
                    procIdx = procIndex,
                    route = (procedure && !locked && procIndex >= 0) ? RouteFor(procIndex) : null,
                    station = (procedure && !locked && procIndex >= 0) ? StationFor(procIndex) : -1,
                    timeLimit = TimeLimitFor(d, procIndex),
                };
                r.remaining = r.timeLimit;

                CreateCard(r);
                active.Add(r);
                InterruptRest();
                return;
            }
        }

        // 커트는 Lv.5에서 가발 커트, Lv.6에서 모델 커트
        string ProcLabel(int p)
        {
            if (p == 4) return level >= 6 ? "모델 커트" : "가발 커트";
            return LevelConfig.ProcedureNames[p];
        }

        // 커트 손님이 가고 나면 커트석 바닥에 머리카락이 쌓인다
        void SpawnFloorMess()
        {
            foreach (Request q in active) if (q.isMess) return; // 이미 있으면 하나만

            Request m = new Request
            {
                designer = designers[0],
                label = "바닥 청소",
                isProcedure = false,
                isMess = true,
                useAnchor = true,
                anchor = StationPos[2],
                station = -1,
                procIdx = -1,
                timeLimit = 45f,
            };
            m.remaining = m.timeLimit;
            CreateCard(m);
            active.Add(m);
            Say("커트 손님이 가고 나서 바닥에 머리카락이 가득해요! 쓸어주세요.");
        }

        bool HasRequest(DesignerData d)
        {
            foreach (Request r in active)
            {
                if (r.designer == d && !r.isMess) return true;
            }
            return false;
        }

        // 디자이너의 시술 확률(샴푸/롤/염색/드라이)로 하나 뽑기
        int PickProcedure(DesignerData d)
        {
            // 커트는 Lv.5부터 모든 디자이너가 같은 비율로 요청 (임시)
            int cutWeight = level >= LevelConfig.ProcedureUnlockLevel[4] ? 25 : 0;
            int bleachWeight = level >= LevelConfig.ProcedureUnlockLevel[5] ? 15 : 0; // 탈색도 임시로 모든 디자이너 공통
            int[] weights = { d.shampoo, d.roll, d.dye, d.dry, cutWeight, bleachWeight };
            int total = 0;
            foreach (int w in weights) total += w;
            if (total <= 0) return 0;

            int pick = Random.Range(0, total);
            for (int i = 0; i < weights.Length; i++)
            {
                if (pick < weights[i]) return i;
                pick -= weights[i];
            }
            return 0;
        }

        // ---------- 카드 처리 ----------

        // 말풍선을 누르면 내 인턴이 그 자리로 걸어간다
        void OnCardClicked(Request r)
        {
            if (phase != Phase.Business || minigameActive || waitingAtStation) return;
            if (!active.Contains(r)) return;

            if (resting) Say("쉬다 말고 일하러 가요!");
            resting = false;
            restWalking = false;
            supplyWalking = false;
            refilling = false;
            if (carrying && playerTask != r)
            {
                if (playerTask.route != null) { playerTask.routeIdx = 0; playerTask.started = false; }
                SendCustomerBack(SeatPos(playerTask.designer));
            }
            if (carrying && playerTask == r)
            {
                // 이미 손님을 데리고 있으면 바로 구역으로
                playerTarget = StepPos(r, r.routeIdx);
                playerWalking = true;
                return;
            }
            playerTask = r;
            r.started = false;
            r.routeIdx = 0;
            Vector2 seat = SeatPos(r.designer);
            playerTarget = r.useAnchor ? r.anchor : new Vector2(seat.x, seat.y > 0f ? 55f : -55f);
            playerWalking = true;
            Say(r.isMess ? "바닥을 쓸러 가는 중..." : r.designer.name + " 선배에게 가는 중... (" + r.label + ")");
        }

        void UpdatePlayer()
        {
            if (playerRt == null || !playerWalking) return;

            Vector2 pos = Vector2.MoveTowards(playerRt.anchoredPosition, playerTarget, PlayerSpeed * Time.deltaTime);
            playerRt.anchoredPosition = pos;
            if ((pos - playerTarget).sqrMagnitude > 1f) return;

            playerWalking = false;
            if (supplyWalking)
            {
                supplyWalking = false;
                if (CanRestHere()) ShowSupplyChoice();
                return;
            }

            if (restWalking)
            {
                restWalking = false;
                if (CanRestHere()) ShowRestChoice();
                return;
            }

            Request task = playerTask;
            if (task != null && active.Contains(task) && task.route != null)
            {
                if (!task.started)
                {
                    task.started = true;
                    task.routeIdx = 0;
                    if (StepStation[task.route[0]] >= 0)
                    {
                        // 디자이너 자리에서 손님을 모시고 첫 단계 구역으로 출발
                        PickUpCustomer();
                        playerTarget = StepPos(task, 0);
                        playerWalking = true;
                        Say(task.designer.name + " 선배의 손님을 " + StepNames[task.route[0]] + "하러 모셔요");
                        return;
                    }
                }

                BeginStep(task); // 지금 있는 자리에서 이번 단계 시작
                return;
            }

            playerTask = null;
            if (task != null && active.Contains(task)) PerformRequest(task);
            else if (task != null) Say("한발 늦었어요. 이미 정리됐어요.");
        }

        // 시술 단계: 0 샴푸, 1 펌, 2 염색, 3 드라이, 4 커트
        static readonly string[] StepNames = { "샴푸", "펌", "염색", "드라이", "커트", "탈색" };
        static readonly int[] StepStation = { 0, 1, 1, -1, 2, 1 };      // 구역 (-1은 디자이너 자리)
        static readonly int[] StepUnlock = { 1, 2, 3, 4, 5, 3 };        // 인턴이 직접 할 수 있는 레벨
        // 단계를 끝내면 줄어드는 재료: 수건0 도구1 음료2 청소용품3
        static readonly int[][] StepUses = { new[] { 0 }, new[] { 1, 2 }, new[] { 1 }, new[] { 0, 3 }, new[] { 1 }, new[] { 1 } };

        // 시술별 진행 순서
        static int[] RouteFor(int procedure)
        {
            switch (procedure)
            {
                case 0: return new[] { 0 };                    // 샴푸
                case 1: return new[] { 0, 4, 1, 0, 3 };        // 파마: 샴푸 > 커트 > 펌 > 샴푸 > 드라이
                case 2: return new[] { 2, 0, 3 };              // 염색: 염색 > 샴푸 > 드라이
                case 3: return new[] { 0, 3 };                 // 드라이: 샴푸 > 드라이
                case 4: return new[] { 0, 4, 3 };              // 커트: 샴푸 > 커트 > 드라이
                case 5: return new[] { 5, 0, 2, 0, 3 };        // 탈색: 탈색 > 샴푸 > 염색 > 샴푸 > 드라이
            }
            return null;
        }

        Vector2 StepPos(Request r, int idx)
        {
            int st = StepStation[r.route[idx]];
            if (st >= 0) return StationPos[st];
            Vector2 seat = SeatPos(r.designer);
            return new Vector2(seat.x, seat.y > 0f ? 55f : -55f);
        }

        // 이번 단계 시작: 인턴이 할 수 있으면 금방, 못 하면 디자이너가 하는 동안 기다린다
        void BeginStep(Request task)
        {
            int step = task.route[task.routeIdx];
            bool mine = level >= StepUnlock[step];
            waitingAtStation = true;
            stationWait = mine ? 1.5f : 4f;
            Say(mine ? StepNames[step] + " 하는 중..." : task.designer.name + " 선배가 " + StepNames[step] + "하는 중...");
        }

        void ConsumeStep(int step)
        {
            foreach (int k in StepUses[step])
            {
                if (stock[k] > 0) stock[k]--;
                else ScoldSupply(k);
            }
        }

        static int StepScore(Request r)
        {
            return r.route != null ? 10 + 5 * r.route.Length : 20;
        }

        // 한 단계가 끝나면 다음 단계로 손님을 데려가고, 마지막이면 시술 완료
        void UpdateCutWait()
        {
            if (!waitingAtStation) return;
            stationWait -= Time.deltaTime;
            if (stationWait > 0f) return;

            waitingAtStation = false;
            Request task = playerTask;
            if (task == null || !active.Contains(task)) return;

            int step = task.route[task.routeIdx];
            ConsumeStep(step);
            if (step == 4) SpawnFloorMess(); // 커트 뒤에는 바닥에 머리카락이 남는다

            task.routeIdx++;
            if (task.routeIdx >= task.route.Length)
            {
                playerTask = null;
                SendCustomerBack(SeatPos(task.designer));
                PerformRequest(task);
                return;
            }

            playerTarget = StepPos(task, task.routeIdx);
            playerWalking = true;
            Say(StepNames[step] + " 끝! 다음은 " + StepNames[task.route[task.routeIdx]]);
        }

        static int StationFor(int procedure)
        {
            switch (procedure)
            {
                case 0: return 0;      // 샴푸 -> 샴푸실
                case 1: return 1;      // 롤 -> 시술석
                case 2: return 1;      // 염색 -> 시술석
                case 4: return 2;      // 커트 -> 커트석
                default: return -1;    // 드라이 -> 자리에서
            }
        }

        void PickUpCustomer()
        {
            if (customerRt != null) Destroy(customerRt.gameObject);
            Image c = UiFactory.Circle("Customer", mapArea, new Color(0.96f, 0.78f, 0.66f));
            customerRt = c.rectTransform;
            PlaceCenter(customerRt, playerRt.anchoredPosition + new Vector2(-48f, 0f), new Vector2(52f, 52f));
            UiFactory.Label("CustomerText", c.transform, "손님", 18, TextDark, TextAnchor.MiddleCenter);
            carrying = true;
            customerReturning = false;
        }

        void SendCustomerBack(Vector2 seat)
        {
            carrying = false;
            if (customerRt == null) return;
            customerReturning = true;
            customerReturnTo = seat;
        }

        void ClearCustomer()
        {
            carrying = false;
            customerReturning = false;
            if (customerRt != null) Destroy(customerRt.gameObject);
            customerRt = null;
        }

        void UpdateCustomer()
        {
            if (customerRt == null) return;
            if (carrying)
            {
                customerRt.anchoredPosition = playerRt.anchoredPosition + new Vector2(-48f, 0f);
            }
            else if (customerReturning)
            {
                Vector2 pos = Vector2.MoveTowards(customerRt.anchoredPosition, customerReturnTo, 1000f * Time.deltaTime);
                customerRt.anchoredPosition = pos;
                if ((pos - customerReturnTo).sqrMagnitude < 4f) ClearCustomer();
            }
        }

        void PerformRequest(Request r)
        {
            if (phase != Phase.Business) return;
            if (!active.Contains(r)) return;

            if (r.isLocked)
            {
                AddXp(1);
                Say(r.designer.name + " 선배의 " + r.label + "을(를) 견학했어요 (XP +1)");
            }
            else if (r.label == "바닥 청소")
            {
                StartFloorSweep(r);
                return; // 결과는 미니게임이 끝난 뒤 FinishFloorSweep에서 처리
            }
            else
            {
                done++;
                score += StepScore(r) + (r.isVip ? 30 : (r.isReserved ? 10 : 0));
                fatigue = Mathf.Min(100f, fatigue + (r.isProcedure ? 6f : 4f));
                AddXp(r.isProcedure ? 2 : 1);
                Say(r.designer.name + " 선배의 " + r.label + " 완료!");
            }
            RemoveRequest(r);
            RefreshTop();
        }

        void StartFloorSweep(Request r)
        {
            minigameActive = true;
            FloorSweepGame.Show(canvasRoot, result => FinishFloorSweep(r, result));
        }

        void FinishFloorSweep(Request r, SweepResult result)
        {
            minigameActive = false;
            fatigue = Mathf.Min(100f, fatigue + 8f);

            string grade;
            int points;
            int gainXp;
            float fraction = result.total > 0 ? (float)result.cleaned / result.total : 0f;

            if (result.cleaned >= result.total)
            {
                if (result.elapsed <= 10f) { grade = "S"; points = 20; gainXp = 2; }
                else if (result.elapsed <= 15f) { grade = "A"; points = 15; gainXp = 1; }
                else { grade = "B"; points = 10; gainXp = 1; }
            }
            else if (fraction >= 0.6f) { grade = "B"; points = 10; gainXp = 1; }
            else { grade = "C"; points = 3; gainXp = 0; }

            if (grade == "C") missed++; else done++;
            score += points;
            AddXp(gainXp);

            Say("바닥 청소 " + grade + "등급! ("
                + result.cleaned + "/" + result.total + ", 점수 +" + points + ")");

            if (active.Contains(r)) RemoveRequest(r);
            RefreshTop();
        }

        void Expire(Request r)
        {
            missed++;
            Say(r.isMess ? "바닥이 지저분한 채로 남았어요" : r.designer.name + " 선배가 " + r.label + "을(를) 직접 처리했어요");
            RemoveRequest(r);
            RefreshTop();
        }

        void RemoveRequest(Request r)
        {
            if (playerTask == r)
            {
                // 처리 중이던 요청이 사라지면 데려가던 손님은 자리로 돌려보낸다
                playerTask = null;
                playerWalking = false;
                waitingAtStation = false;
                if (carrying) SendCustomerBack(SeatPos(r.designer));
            }
            active.Remove(r);
            Destroy(r.background.gameObject);
        }

        void AddXp(int amount)
        {
            xp += amount;
            while (level < LevelConfig.MaxLevel && xp >= LevelConfig.XpToNext(level))
            {
                xp -= LevelConfig.XpToNext(level);
                level++;
                Say("레벨 업! Lv." + level + " " + LevelConfig.Names[level]);
            }
        }

        void ChangeLevel(int delta)
        {
            if (phase != Phase.Business || minigameActive) return;
            level = Mathf.Clamp(level + delta, 0, LevelConfig.MaxLevel);
            xp = 0;
            Say("[테스트] Lv." + level + " " + LevelConfig.Names[level] + " 로 변경");
            RefreshTop();
        }

        // 테스트용: 하루를 바로 끝낸다
        void SkipToEvening()
        {
            if (phase != Phase.Business || minigameActive) return;
            EndBusiness();
        }

        // ---------- 화면 ----------

        void RefreshTop()
        {
            topText.text = "Day " + day + "  " + DayConfig.FormatClock(ClockMinutes())
                + "   Lv." + level + " " + LevelConfig.Names[level]
                + "   XP " + XpLabel()
                + "   점수 " + score
                + "   완료 " + done
                + "   놓침 " + missed;
        }

        void Say(string message)
        {
            if (logText != null) logText.text = message;
        }

        void BuildUi()
        {
            EnsureEventSystem();

            GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasGo.GetComponent<RectTransform>();
            canvasRoot = root;

            // 배경
            Image bg = UiFactory.Panel("Background", root, new Color(0.93f, 0.90f, 0.86f));
            UiFactory.Stretch(bg.rectTransform, 0f, 0f, 1f, 1f);

            // 상단 바
            Image top = UiFactory.Panel("TopBar", root, new Color(0.20f, 0.20f, 0.26f));
            UiFactory.Stretch(top.rectTransform, 0f, 0.91f, 1f, 1f);
            topText = UiFactory.Label("TopText", top.transform, "", 28, Color.white, TextAnchor.MiddleLeft);
            topText.rectTransform.offsetMin = new Vector2(30f, 0f);
            topText.rectTransform.offsetMax = new Vector2(-520f, 0f);

            // 테스트용 버튼: 하루 끝내기, 레벨 조절
            Button skip = UiFactory.MakeButton("SkipDay", top.transform, "하루 끝", 28, new Color(0.65f, 0.40f, 0.30f), SkipToEvening);
            UiFactory.Stretch(skip.GetComponent<RectTransform>(), 0.76f, 0.15f, 0.85f, 0.85f);
            Button minus = UiFactory.MakeButton("LevelMinus", top.transform, "Lv -", 32, new Color(0.45f, 0.45f, 0.55f), () => ChangeLevel(-1));
            UiFactory.Stretch(minus.GetComponent<RectTransform>(), 0.86f, 0.15f, 0.925f, 0.85f);
            Button plus = UiFactory.MakeButton("LevelPlus", top.transform, "Lv +", 32, new Color(0.30f, 0.60f, 0.45f), () => ChangeLevel(1));
            UiFactory.Stretch(plus.GetComponent<RectTransform>(), 0.935f, 0.15f, 0.995f, 0.85f);

            // 미용실 바닥 (위에서 내려다본 그림)
            Image floor = UiFactory.Panel("SalonFloor", root, new Color(0.86f, 0.80f, 0.72f));
            mapArea = floor.rectTransform;
            UiFactory.Stretch(mapArea, 0.03f, 0.22f, 0.97f, 0.89f);
            BuildFloor();

            // 피로도 막대와 휴식 버튼 (카드 영역과 하단 안내 사이)
            Image fBg = UiFactory.Panel("FatigueBg", root, new Color(0.30f, 0.30f, 0.35f));
            UiFactory.Stretch(fBg.rectTransform, 0.03f, 0.145f, 0.40f, 0.195f);
            fatigueFill = UiFactory.Panel("FatigueFill", fBg.transform, BarGreen);
            UiFactory.Stretch(fatigueFill.rectTransform, 0f, 0f, 0f, 1f);
            fatigueText = UiFactory.Label("FatigueText", fBg.transform, "", 28, Color.white, TextAnchor.MiddleCenter);
            restButton = UiFactory.MakeButton("RestButton", root, "휴식", 30, new Color(0.35f, 0.55f, 0.75f), StartRest);
            UiFactory.Stretch(restButton.GetComponent<RectTransform>(), 0.42f, 0.135f, 0.69f, 0.205f);
            restButtonText = restButton.GetComponentInChildren<Text>();
            supplyButton = UiFactory.MakeButton("SupplyButton", root, "재료실 가기", 30, new Color(0.45f, 0.60f, 0.45f), StartSupply);
            UiFactory.Stretch(supplyButton.GetComponent<RectTransform>(), 0.71f, 0.135f, 0.97f, 0.205f);
            supplyButtonText = supplyButton.GetComponentInChildren<Text>();

            // 하단 안내 문구
            Image bottom = UiFactory.Panel("BottomBar", root, new Color(0.20f, 0.20f, 0.26f));
            UiFactory.Stretch(bottom.rectTransform, 0f, 0f, 1f, 0.12f);
            logText = UiFactory.Label("LogText", bottom.transform, "", 36, Color.white, TextAnchor.MiddleLeft);
            logText.rectTransform.offsetMin = new Vector2(30f, 0f);
            logText.rectTransform.offsetMax = new Vector2(-30f, 0f);

            // 아침 준비 화면, 마감 정산 화면 (평소에는 하나만 켜진다)
            BuildDayPanel(root, "MorningPanel", "영업 시작", StartBusiness,
                out morningPanel, out morningTitle, out morningBody);
            BuildDayPanel(root, "EveningPanel", "다음 날 시작", NextDay,
                out eveningPanel, out eveningTitle, out eveningBody);

            // 아침 화면 배치: 제목, 요약 줄, 예약표 시간표, 안내 문구
            UiFactory.Stretch(morningTitle.rectTransform, 0f, 0.885f, 1f, 0.99f);
            UiFactory.Stretch(morningBody.rectTransform, 0.04f, 0.80f, 0.96f, 0.875f);
            morningBody.fontSize = 32;
            morningBody.alignment = TextAnchor.MiddleCenter;

            reservationArea = UiFactory.Rect("ReservationGrid", morningPanel);
            UiFactory.Stretch(reservationArea, 0.04f, 0.19f, 0.96f, 0.79f);

            Text note = UiFactory.Label("Note", morningPanel,
                "※ 예약 시간이 되면 그 디자이너의\n예약 손님이 카드로 나타나요. (VIP는 보너스 점수)", 24, TextDark, TextAnchor.MiddleLeft);
            UiFactory.Stretch(note.rectTransform, 0.04f, 0.02f, 0.33f, 0.17f);
            eveningPanel.gameObject.SetActive(false);
        }

        static void BuildDayPanel(RectTransform root, string name, string buttonText, System.Action onButton,
            out RectTransform panel, out Text title, out Text body)
        {
            Image p = UiFactory.Panel(name, root, new Color(0.96f, 0.93f, 0.88f));
            UiFactory.Stretch(p.rectTransform, 0f, 0f, 1f, 1f);
            panel = p.rectTransform;

            title = UiFactory.Label("Title", p.transform, "", 60, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.84f, 1f, 0.97f);

            body = UiFactory.Label("Body", p.transform, "", 36, TextDark, TextAnchor.UpperLeft);
            UiFactory.Stretch(body.rectTransform, 0.10f, 0.22f, 0.90f, 0.82f);

            Button button = UiFactory.MakeButton("NextButton", p.transform, buttonText, 48, new Color(0.30f, 0.60f, 0.45f), onButton);
            UiFactory.Stretch(button.GetComponent<RectTransform>(), 0.35f, 0.06f, 0.65f, 0.17f);
        }

        // ---------- 미용실 바닥 그리기 ----------

        static void PlaceCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        Vector2 SeatPos(DesignerData d)
        {
            Vector2 p;
            return seats.TryGetValue(d, out p) ? p : Vector2.zero;
        }

        void BuildFloor()
        {
            // 가운데 복도
            Image corridor = UiFactory.Panel("Corridor", mapArea, new Color(0.93f, 0.89f, 0.83f));
            PlaceCenter(corridor.rectTransform, new Vector2(0f, 0f), new Vector2(1500f, 150f));

            // 휴게실
            Image room = UiFactory.Panel("RestRoom", mapArea, new Color(0.62f, 0.72f, 0.82f));
            PlaceCenter(room.rectTransform, RestPos, new Vector2(190f, 200f));
            Button roomButton = room.gameObject.AddComponent<Button>();
            roomButton.targetGraphic = room;
            roomButton.onClick.AddListener(StartRest);
            Text roomText = UiFactory.Label("RestRoomText", room.transform, "휴게실", 30, Color.white, TextAnchor.UpperCenter);
            roomText.rectTransform.offsetMin = new Vector2(0f, 0f);
            roomText.rectTransform.offsetMax = new Vector2(0f, -8f);

            // 재료실: 남은 개수가 보이고, 누르면 걸어간다
            Image supply = UiFactory.Panel("SupplyRoom", mapArea, new Color(0.70f, 0.80f, 0.66f));
            PlaceCenter(supply.rectTransform, SupplyPos, new Vector2(190f, 200f));
            Button supplyRoomButton = supply.gameObject.AddComponent<Button>();
            supplyRoomButton.targetGraphic = supply;
            supplyRoomButton.onClick.AddListener(StartSupply);
            Text supplyTitle = UiFactory.Label("SupplyTitle", supply.transform, "재료실", 28, TextDark, TextAnchor.UpperCenter);
            supplyTitle.rectTransform.offsetMax = new Vector2(0f, -6f);
            supplyRoomText = UiFactory.Label("SupplyStock", supply.transform, "", 24, TextDark, TextAnchor.MiddleCenter);
            supplyRoomText.supportRichText = true;
            supplyRoomText.rectTransform.offsetMin = new Vector2(0f, 6f);
            supplyRoomText.rectTransform.offsetMax = new Vector2(0f, -40f);

            // 시술 구역 (오른쪽): 샴푸실 / 시술석 / 커트석
            string[] zoneNames = { "샴푸실", "시술석", "커트석 (Lv.5~)" };
            Color[] zoneColors =
            {
                new Color(0.70f, 0.85f, 1.00f), new Color(1.00f, 0.88f, 0.75f), new Color(0.80f, 0.80f, 0.82f)
            };
            float[] zoneY = { 215f, 0f, -215f };
            for (int z = 0; z < 3; z++)
            {
                Image zone = UiFactory.Panel("Zone_" + zoneNames[z], mapArea, zoneColors[z]);
                PlaceCenter(zone.rectTransform, new Vector2(775f, zoneY[z]), new Vector2(240f, 190f));
                Text zt = UiFactory.Label("ZoneText", zone.transform, zoneNames[z], 26, TextDark, TextAnchor.UpperCenter);
                zt.rectTransform.offsetMax = new Vector2(0f, -6f);
            }

            // 디자이너 자리: 위 6명, 아래 6명
            for (int i = 0; i < designers.Length; i++)
            {
                int row = (i / 6) % 2;
                int col = i % 6;
                float sy = row == 0 ? 170f : -170f;
                Vector2 pos = new Vector2(-480f + col * 200f, sy);
                seats[designers[i]] = pos;

                Image chair = UiFactory.Panel("Chair", mapArea, new Color(0.55f, 0.50f, 0.50f));
                PlaceCenter(chair.rectTransform, pos, new Vector2(100f, 100f));

                Color body = Color.HSVToRGB((i * 0.083f) % 1f, 0.45f, 0.95f);
                Image figure = UiFactory.Circle("Designer_" + designers[i].name, mapArea, body);
                PlaceCenter(figure.rectTransform, pos, new Vector2(78f, 78f));

                Text name = UiFactory.Label("Name", mapArea, designers[i].name, 24, TextDark, TextAnchor.MiddleCenter);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                PlaceCenter(name.rectTransform, new Vector2(pos.x, row == 0 ? sy - 62f : sy + 62f), new Vector2(190f, 30f));
            }

            // 내 인턴
            Image me = UiFactory.Circle("Player", mapArea, new Color(0.25f, 0.55f, 0.35f));
            playerRt = me.rectTransform;
            PlaceCenter(playerRt, new Vector2(-480f, 0f), new Vector2(70f, 70f));
            UiFactory.Label("PlayerText", me.transform, "나", 28, Color.white, TextAnchor.MiddleCenter);
        }

        // 디자이너 머리 위에 뜨는 요청 말풍선
        void CreateCard(Request r)
        {
            Vector2 seat = SeatPos(r.designer);
            float dir = seat.y > 0f ? 1f : -1f;

            Color baseColor = r.isLocked ? CardLocked : CardNormal;
            if (r.isVip && !r.isLocked) baseColor = new Color(1f, 0.93f, 0.62f);
            else if (r.isReserved && !r.isLocked) baseColor = new Color(0.90f, 0.95f, 1f);

            if (r.isMess) baseColor = new Color(0.90f, 0.82f, 0.70f);
            Image bg = UiFactory.Panel("Bubble_" + r.designer.name, mapArea, baseColor);
            r.background = bg;
            PlaceCenter(bg.rectTransform, r.useAnchor ? r.anchor : new Vector2(seat.x, seat.y + dir * 112f), new Vector2(190f, 130f));

            Button button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => OnCardClicked(r));

            string title = r.isLocked ? "[잠김] " + r.label : r.label;
            if (r.isVip) title = "VIP " + title;
            else if (r.isReserved) title = "예약 " + title;
            Text body = UiFactory.Label("Body", bg.transform, title, 24, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(body.rectTransform, 0.04f, 0.28f, 0.96f, 0.98f);

            Image barBg = UiFactory.Panel("BarBg", bg.transform, new Color(0.25f, 0.25f, 0.30f));
            UiFactory.Stretch(barBg.rectTransform, 0.06f, 0.08f, 0.94f, 0.22f);
            Image fill = UiFactory.Panel("BarFill", barBg.transform, BarGreen);
            UiFactory.Stretch(fill.rectTransform, 0f, 0f, 1f, 1f);
            r.barFill = fill.rectTransform;
            r.barFillImage = fill;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.UI.InputSystemUIInputModule module =
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
