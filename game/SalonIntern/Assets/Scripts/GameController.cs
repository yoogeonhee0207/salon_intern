using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SalonIntern
{
    // 하루 구조(아침 -> 영업 -> 마감 정산)와 요청 카드 시스템 프로토타입.
    // 씬에 아무것도 놓지 않아도 Play를 누르면 이 스크립트가 자동으로 실행된다.
    public partial class GameController : MonoBehaviour
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
            public int chair = -1; // 앉아 있는 시술석 의자 번호
            public bool processing;  // 약이 작용하는 대기 중 (손님은 의자에 앉아 있음)
            public bool ready;       // 대기가 끝나서 손님을 모시러 갈 수 있음
            public bool pickup;      // 의자로 손님을 모시러 가는 중
            public float procEnd, procTotal;
            public float readyAt;    // 대기가 끝난 영업 시각
            public int multi;        // 대기 중에 끝낸 다른 일의 수
            public RectTransform seated; // 의자에 앉아 있는 손님 모양
            public Text bodyText;
            public Color baseColor;
            public string titleBase;
            public string customerName; public int customerType; public string customerLine; public bool isInspector;
            public bool started;     // 손님을 모시고 출발했는지
            public int satSum;       // 단계별 완성도(0~100) 합: 손님 만족도 계산용
            public int satCount;
            public bool isMess;      // 커트 뒤에 생긴 바닥 어지럼 (바닥 청소 미니게임)
            public bool useAnchor;   // 디자이너 자리가 아니라 anchor 위치에 말풍선을 띄운다
            public Vector2 anchor;
            public int station = -1; // 시술 구역 (0 샴푸실, 1 시술석, -1 자리에서 처리)
            public float timeLimit;
            public float remaining;
            public int assignee = -1;   // 인턴장이 맡긴 사람 (0 나, 1~3 NPC 인턴, -1 없음)
            public float assignAge;     // 맡긴 뒤 지난 시간
            public bool npcBusy;        // NPC 인턴이 처리하는 중
            public bool npcActive;      // NPC 인턴이 이 요청을 맡아 움직이는 중 (걷는 중 포함)
            public bool npcOnly;        // 고르지 않은 선생님의 요청 (동료 인턴만 처리, 점수 없음)
            public Text tagText;        // 말풍선 위 담당자 표시

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
        readonly Dictionary<DesignerData, Image> seatedCustomers = new Dictionary<DesignerData, Image>();
        readonly Dictionary<DesignerData, int> seatedLook = new Dictionary<DesignerData, int>();
        RectTransform playerRt;
        Vector2 playerTarget;
        bool playerWalking;
        Request playerTask;      // 도착하면 처리할 요청
        bool restWalking;        // 휴게실로 걸어가는 중
        const float PlayerSpeed = 1200f;

        // 시술 구역: 0 샴푸실, 1 시술석 (커트와 드라이는 선생님 자리에서 한다)
        static readonly Vector2[] StationPos = { new Vector2(775f, 215f), new Vector2(775f, -40f) };
        static readonly string[] StationNames = { "샴푸실", "시술석" };
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
        int dayRequests; // 오늘 받은 시술 요청 수

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
            public bool npcOnly;   // 고르지 않은 선생님의 예약: 동료 인턴이 처리한다
        }
        List<Booking>[] bookingsBySlot;
        // 오늘 같이 일할 선생님 고르기
        int PickMax { get { return LevelConfig.PickMax[Mathf.Clamp(level, 0, LevelConfig.PickMax.Length - 1)]; } }
        readonly List<DesignerData> picked = new List<DesignerData>();
        int[,] resProc;
        Vector2Int resVip;
        Button morningStartButton;
        Text morningNote;
        Text resetButtonText;
        Text shopButtonText;
        RectTransform shopPanel;
        Text saveText;          // 저장할 때 우측 상단에 잠깐 뜨는 안내
        float saveTimer;
        Text morningStartText;
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
        int points; // 상점에서 쓰는 포인트 (하루 마감 보상)
        string playerName = "막내";
        int gender;        // 0 여성, 1 남성, 2 밝히지 않음
        string salonName = "해피헤어 번화가점";
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
            Time.timeScale = 1f;
            Sfx.StartBgm();

            if (designers.Length == 0)
            {
                Say("디자이너 데이터를 불러오지 못했어요. Console 창을 확인해 주세요.");
                RefreshTop();
                return;
            }
            ShowTitle();
        }

        void Update()
        {
            if (designers == null || designers.Length == 0) return;
            UpdateToast();
            UpdateSaveText();
            FlushAchieveToast();
            UpdateChairWait();
            UpdateProcessing();
            UpdateOverdue();
            UpdateHeadLabels();
            if (phase != Phase.Business) { ResetNpcs(); return; }
            if (minigameActive) return;

            UpdateRest();
            UpdatePlayer();
            UpdateSupplies();
            UpdateRefill();
            UpdateSupplyUi();
            UpdateCustomer();
            UpdateSeatedCustomers();
            UpdateCutWait();
            UpdateNpcs();

            // 영업 시계 흐르기
            float dt = Time.deltaTime;
            bool idle = IsIdle();
            if (idle) dt *= IdleSpeed; // 할 일이 없는 시간은 빨리 감기
            if (idle != fastForward) { fastForward = idle; RefreshTop(); }
            businessElapsed += dt;
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

            // 오늘 목표 건수를 다 받았으면 새 요청은 받지 않고, 남은 요청이 끝나면 마감
            bool quotaReached = TourDay && dayRequests >= Quota(); // 견학 날만 건수로 마감. 평소에는 18시까지 계속
            if (quotaReached)
            {
                waitingBookings.Clear();
                bool onlyMess = true;
                foreach (Request q in active) if (!q.isMess && !q.npcOnly) onlyMess = false;
                if (onlyMess)
                {
                    EndBusiness();
                    return;
                }
            }
            else
            {
                // 예약 시간이 된 칸의 예약을 대기열에 넣고, 디자이너가 비면 카드로 만든다 (견학 날은 예약 없음)
                if (!TourDay)
                {
                    ReleaseBookings(minute);
                    SpawnWaitingBookings();
                }
            }

            // 제한시간 줄이기
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Request r = active[i];
                if (r.processing || r.npcBusy || r.npcActive) continue; // 약이 작용하는 동안, NPC 인턴이 처리하는 동안은 제한시간이 멈춘다
                r.remaining -= Time.deltaTime;

                float ratio = Mathf.Clamp01(r.remaining / r.timeLimit);
                r.barFill.anchorMax = new Vector2(ratio, 1f);
                r.barFillImage.color = ratio < 0.25f ? BarRed : (r.isLocked ? BarGray : BarGreen);

                if (r.remaining <= 0f) Expire(r);
            }

            // 빈 자리가 있으면 새 요청 만들기
            if (!quotaReached && PlayerRequestCount() < LevelConfig.MaxConcurrent[level])
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    TrySpawn();
                    spawnTimer = Random.Range(1.5f, 4f);
                }
            }
        }

        // ---------- 하루 흐름 ----------

        // 첫날(Lv0)은 모든 시술 종류를 한 번씩 견학하는 날
        bool TourDay { get { return day == 1 && level == 0; } }
        int Quota() { return TourDay ? LevelConfig.ProcedureNames.Length : LevelConfig.DailyQuota[level]; }

        int MissPenaltyNow { get { return LevelConfig.MissPenalty[Mathf.Clamp(level, 0, LevelConfig.MissPenalty.Length - 1)]; } } // 내 선생님 요청을 놓칠 때마다 깎이는 점수 (레벨이 높을수록 크다)
        const float IdleSpeed = 4f;
        bool fastForward;

        // 처리할 요청도 없고 하는 일도 없으면 시간이 빨리 흐른다
        bool IsIdle()
        {
            if (resting || restWalking || refilling || supplyWalking || playerWalking || waitingAtStation || playerTask != null) return false;
            foreach (Request r in active) if (!r.isMess && !r.processing && !r.npcOnly) return false;
            return true;
        }

        int ClockMinutes()
        {
            float t = Mathf.Clamp01(businessElapsed / DayConfig.BusinessSeconds);
            return DayConfig.OpenMinutes + Mathf.FloorToInt(t * (DayConfig.CloseMinutes - DayConfig.OpenMinutes));
        }

        string XpLabel()
        {
            return xp + "/" + LevelConfig.XpToNext(level) + (CanTakeExam() ? " (시험 가능)" : "");
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

            resetArmed = false;
            if (resetButtonText != null) resetButtonText.text = "처음부터 다시";
            SaveGame();
            if (shopButtonText != null) shopButtonText.text = "상점 (" + points + "P)";
            morningTitle.text = salonName + "   Day " + day + "   아침 준비";
            if (morningNote != null)
                morningNote.text = TourDay
                    ? "※ 첫날은 견학! 모든 시술을 한 번씩 보고 배워요.\n(예약 손님 없이 시술마다 견학 요청이 와요)"
                    : "※ 오늘 같이 일할 선생님을 " + PickMax + "명 골라요. 예약표에서 내가 할 수 있는 시술이 많은 선생님을 골라요.\n고른 선생님의 일은 내가 맡고, 나머지 선생님은 동료 인턴이 맡아요. (VIP는 보너스)";
            OnMorningStart();
            UpdateMorningTexts();
            BuildReservationGrid();

            morningPanel.gameObject.SetActive(true);
            eveningPanel.gameObject.SetActive(false);
            Say("아침 준비 중. 예약표를 확인하고 영업을 시작하세요.");
            RefreshTop();
        }

        void StartBusiness()
        {
            if (picked.Count != PickMax) return;
            BuildBookings();
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
            dayRequests = 0;

            morningPanel.gameObject.SetActive(false);
            Say("영업 시작! 말풍선을 눌러 요청을 처리하세요.");
            RefreshTop();
            if (!introShown)
            {
                introShown = true;
                ShowIntro();
            }
        }

        void EndBusiness()
        {
            phase = Phase.Evening;

            // 남은 요청은 디자이너들이 알아서 마무리한 것으로 처리 (감점 없음)
            foreach (Request r in active) { DestroySeated(r); Destroy(r.background.gameObject); }
            active.Clear();
            ResetChairs();
            waitingBookings.Clear();
            playerTask = null;
            playerWalking = false;
            restWalking = false;
            resting = false;
            ClearCustomer();
            waitingAtStation = false;
            supplyWalking = false;
            refilling = false;

            // 최소 건수를 못 채우면 부족한 건수마다 점수를 깎는다 (견학 날은 제외)
            string penaltyLine = "";
            int minDone = LevelConfig.DailyQuota[dayStartLevel];
            int doneToday = done - dayStartDone;
            if (!(day == 1 && dayStartLevel == 0) && doneToday < minDone)
            {
                int shortage = minDone - doneToday;
                int penalty = shortage * LevelConfig.ShortPenalty[dayStartLevel];
                score -= penalty;
                penaltyLine = "최소 건수 미달  " + shortage + "건 부족  -" + penalty + "점\n";
            }
            int dayScore = score - dayStartScore;
            int dayDone = done - dayStartDone;
            int dayMissed = missed - dayStartMissed;
            int target = DayConfig.TargetScore(dayStartLevel);
            string grade = DayConfig.Grade(dayScore, target);

            // 하루 보상: 함께 일한 선생님과 친분, S등급이면 XP 보너스
            int friendGain = grade == "S" ? 6 : (grade == "A" ? 3 : (grade == "B" ? 1 : 0));
            string rewardLine = penaltyLine;
            if (friendGain > 0 && picked.Count > 0)
            {
                string names = "";
                foreach (DesignerData d in picked)
                {
                    AddCloseness(d, friendGain);
                    names += (names.Length > 0 ? ", " : "") + d.name;
                }
                rewardLine += "보상  ♥ 친분 +" + friendGain + " (" + names + " 선생님)\n";
            }
            if (grade == "S")
            {
                AddXp(2);
                rewardLine += "보상  XP +2 (S등급 보너스)\n";
            }
            int pointGain = grade == "S" ? 30 : (grade == "A" ? 20 : (grade == "B" ? 10 : 3));
            AddPoints(pointGain);
            rewardLine += "보상  포인트 +" + pointGain + " (상점에서 도구를 꾸며요)\n";
            rewardLine += OnDayEnd(grade, dayScore, target, dayMissed);
            achieveQueue.Clear();

            string levelLine = level > dayStartLevel
                ? "레벨 업!  Lv." + dayStartLevel + " → Lv." + level + " " + LevelConfig.Names[level]
                : "Lv." + level + " " + LevelConfig.Names[level];

            eveningTitle.text = "Day " + day + "   마감 정산";
            eveningBody.text =
                "오늘의 평가   " + grade + "\n\n"
                + "오늘 점수   " + dayScore + "   (목표 " + target + ")\n"
                + "완료 " + dayDone + "건     놓침 " + dayMissed + "건\n\n"
                + rewardLine + "\n"
                + levelLine + "     XP " + XpLabel() + "\n\n"
                + DayConfig.Comment(grade) + DirectorLine(grade)
                + (CanTakeExamIgnoringDay() ? "\n\n승급시험을 볼 수 있어요! 내일 아침에 도전해 보세요." : "");

            eveningPanel.gameObject.SetActive(true);
            Say("영업 종료! 오늘 하루 수고했어요.");
            RefreshTop();
        }

        // ---------- 저장과 불러오기 ----------

        // 아침이 시작될 때와 승급시험이 끝날 때 저장한다. 영업 도중에 끝내면 그날 아침부터 다시 시작한다.
        void SaveGame()
        {
            var d = new SaveData
            {
                day = day,
                level = level,
                xp = xp,
                score = score,
                done = done,
                missed = missed,
                practice = (int[])practice.Clone(),
                tourSeen = (bool[])tourSeen.Clone(),
                introShown = introShown,
                lastExamDay = lastExamDay,
                graduated = graduated,
                points = points,
                playerName = playerName,
                gender = gender,
                salonName = salonName,
                ownedSkins = (bool[])CursorSkins.Owned.Clone(),
                equippedSkins = (int[])CursorSkins.Equipped.Clone(),
            };
            var ids = new List<int>();
            var vals = new List<float>();
            foreach (KeyValuePair<int, float> kv in closeness) { ids.Add(kv.Key); vals.Add(kv.Value); }
            d.closenessIds = ids.ToArray();
            d.closenessValues = vals.ToArray();
            FillExtraSave(d);
            d.Save();
            if (saveText != null)
            {
                saveText.text = "저장 중...";
                saveText.transform.SetAsLastSibling();
                saveText.gameObject.SetActive(true);
                saveTimer = 1.6f;
            }
        }

        void ApplySave(SaveData d)
        {
            if (d == null) return;
            day = Mathf.Max(1, d.day);
            level = Mathf.Clamp(d.level, 0, LevelConfig.MaxLevel);
            xp = Mathf.Max(0, d.xp);
            score = d.score;
            done = d.done;
            missed = d.missed;
            if (d.practice != null) for (int i = 0; i < practice.Length && i < d.practice.Length; i++) practice[i] = d.practice[i];
            if (d.tourSeen != null) for (int i = 0; i < tourSeen.Length && i < d.tourSeen.Length; i++) tourSeen[i] = d.tourSeen[i];
            introShown = d.introShown;
            lastExamDay = d.lastExamDay;
            graduated = d.graduated;
            points = Mathf.Max(0, d.points);
            if (!string.IsNullOrEmpty(d.playerName)) playerName = d.playerName;
            gender = Mathf.Clamp(d.gender, 0, 2);
            if (!string.IsNullOrEmpty(d.salonName)) salonName = d.salonName;
            CursorSkins.Reset();
            if (d.ownedSkins != null)
                for (int i = 0; i < d.ownedSkins.Length && i < CursorSkins.Owned.Length; i++) CursorSkins.Owned[i] = d.ownedSkins[i] || i % CursorSkins.SkinCount == 0;
            if (d.equippedSkins != null)
                for (int i = 0; i < CursorSkins.ToolCount && i < d.equippedSkins.Length; i++)
                    if (d.equippedSkins[i] >= 0 && d.equippedSkins[i] < CursorSkins.SkinCount && CursorSkins.IsOwned(i, d.equippedSkins[i])) CursorSkins.Equipped[i] = d.equippedSkins[i];
            closeness.Clear();
            ApplyExtraSave(d);
            if (d.closenessIds != null && d.closenessValues != null)
                for (int i = 0; i < d.closenessIds.Length && i < d.closenessValues.Length; i++) closeness[d.closenessIds[i]] = d.closenessValues[i];
        }

        void UpdateSaveText()
        {
            if (saveText == null || saveTimer <= 0f) return;
            saveTimer -= Time.unscaledDeltaTime;
            if (saveTimer <= 0f)
            {
                saveText.gameObject.SetActive(false);
                return;
            }
            if (saveTimer < 1.0f) saveText.text = "저장됨 ✔";
        }

        // ---------- 상점: 포인트로 도구 모양 꾸미기 ----------

        void OpenShop()
        {
            if (shopPanel != null) Destroy(shopPanel.gameObject);

            Image bg = UiFactory.Panel("ShopPanel", canvasRoot, new Color(0.96f, 0.93f, 0.88f));
            UiFactory.Stretch(bg.rectTransform, 0.04f, 0.04f, 0.96f, 0.96f);
            shopPanel = bg.rectTransform;

            Text title = UiFactory.Label("ShopTitle", shopPanel, "상점  -  도구 꾸미기      보유 포인트 " + points + "P", 40, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.88f, 1f, 0.99f);

            for (int t = 0; t < CursorSkins.ToolCount; t++)
            {
                float yMax = 0.87f - t * 0.088f;
                float yMin = yMax - 0.080f;
                Text name = UiFactory.Label("ToolName", shopPanel, CursorSkins.ToolNames[t], 30, TextDark, TextAnchor.MiddleCenter);
                UiFactory.Stretch(name.rectTransform, 0.01f, yMin, 0.16f, yMax);

                for (int k = 0; k < CursorSkins.SkinCount; k++)
                {
                    int tool = t, skin = k;
                    bool owned = CursorSkins.IsOwned(t, k);
                    bool equipped = CursorSkins.Equipped[t] == k;
                    string state = equipped ? "장착 중" : (owned ? "장착하기" : CursorSkins.Cost[k] + "P 구매");
                    Color baseColor = CursorSkins.MainColor(t, k);
                    Button b = UiFactory.MakeButton("Skin", shopPanel, CursorSkins.SkinNames[t][k] + "\n" + state, 22,
                        equipped ? new Color(0.30f, 0.60f, 0.45f) : (owned ? new Color(0.45f, 0.50f, 0.60f) : new Color(0.55f, 0.45f, 0.40f)),
                        () => OnShopSkin(tool, skin));
                    float x0 = 0.17f + k * 0.205f;
                    UiFactory.Stretch(b.GetComponent<RectTransform>(), x0, yMin, x0 + 0.195f, yMax);

                    // 모양 미리보기: 대표색 동그라미
                    Image swatch = UiFactory.Circle("Swatch", b.transform, baseColor);
                    swatch.raycastTarget = false;
                    UiFactory.Stretch(swatch.rectTransform, 0.02f, 0.2f, 0.16f, 0.8f);
                }
            }

            BuildSnackRow(shopPanel, 0.87f - CursorSkins.ToolCount * 0.088f - 0.080f, 0.87f - CursorSkins.ToolCount * 0.088f);

            Button close = UiFactory.MakeButton("ShopClose", shopPanel, "닫기", 32, new Color(0.30f, 0.60f, 0.45f), CloseShop);
            UiFactory.Stretch(close.GetComponent<RectTransform>(), 0.38f, 0.01f, 0.62f, 0.07f);
        }

        void CloseShop()
        {
            if (shopPanel != null) Destroy(shopPanel.gameObject);
            shopPanel = null;
            ApplyLook();
            if (shopButtonText != null) shopButtonText.text = "상점 (" + points + "P)";
            SaveGame();
        }

        void OnShopSkin(int tool, int skin)
        {
            if (!CursorSkins.IsOwned(tool, skin))
            {
                int cost = CursorSkins.Cost[skin];
                if (points < cost)
                {
                    Say("포인트가 부족해요. (" + cost + "P 필요)");
                    return;
                }
                points -= cost;
                CursorSkins.Owned[tool * CursorSkins.SkinCount + skin] = true;
            }
            CursorSkins.Equipped[tool] = skin;
            ApplyLook();
            OpenShop(); // 화면 새로고침
        }

        // ---------- 시작 화면 ----------

        RectTransform titlePanel;
        Text titleNewText;
        bool newArmed;

        void ShowTitle()
        {
            if (titlePanel != null) Destroy(titlePanel.gameObject);
            newArmed = false;
            SaveData save = SaveData.Load();

            Image bg = UiFactory.Panel("TitlePanel", canvasRoot, new Color(0.14f, 0.16f, 0.26f));
            UiFactory.Stretch(bg.rectTransform, 0f, 0f, 1f, 1f);
            titlePanel = bg.rectTransform;
            titlePanel.SetAsLastSibling();

            // 가위 장식 (도형으로 그린 모양)
            Image scissorsArt = UiFactory.Panel("TitleScissors", titlePanel, Color.white);
            bool titleArt = Art.Apply(scissorsArt, "title_scissors");
            scissorsArt.preserveAspect = true;
            scissorsArt.raycastTarget = false;
            UiFactory.Stretch(scissorsArt.rectTransform, 0.13f, 0.50f, 0.31f, 0.93f);
            scissorsArt.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            scissorsArt.gameObject.SetActive(titleArt);
            Image bladeL = UiFactory.Panel("BladeL", titlePanel, new Color(0.85f, 0.87f, 0.92f));
            UiFactory.Stretch(bladeL.rectTransform, 0.488f, 0.52f, 0.497f, 0.85f);
            bladeL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            Image bladeR = UiFactory.Panel("BladeR", titlePanel, new Color(0.95f, 0.96f, 1f));
            UiFactory.Stretch(bladeR.rectTransform, 0.503f, 0.52f, 0.512f, 0.85f);
            bladeR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            Image ringL = UiFactory.Circle("RingL", titlePanel, new Color(0.90f, 0.40f, 0.50f));
            UiFactory.Stretch(ringL.rectTransform, 0.470f, 0.42f, 0.492f, 0.52f);
            Image ringR = UiFactory.Circle("RingR", titlePanel, new Color(0.90f, 0.40f, 0.50f));
            UiFactory.Stretch(ringR.rectTransform, 0.508f, 0.42f, 0.530f, 0.52f);
            if (titleArt) { bladeL.gameObject.SetActive(false); bladeR.gameObject.SetActive(false); ringL.gameObject.SetActive(false); ringR.gameObject.SetActive(false); }

            Text title = UiFactory.Label("Title", titlePanel, "막내의 가위", 120, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.60f, 1f, 0.82f);
            Text tagline = UiFactory.Label("Tagline", titlePanel, "수건 접던 막내가 가위를 잡기까지", 36, new Color(0.80f, 0.84f, 0.95f), TextAnchor.MiddleCenter);
            UiFactory.Stretch(tagline.rectTransform, 0f, 0.52f, 1f, 0.60f);

            if (save != null)
            {
                string info = "Day " + save.day + "   Lv." + save.level + " " + LevelConfig.Names[Mathf.Clamp(save.level, 0, LevelConfig.MaxLevel)];
                Button cont = UiFactory.MakeButton("Continue", titlePanel, "이어하기\n" + info, 36, new Color(0.30f, 0.60f, 0.45f), () => StartFromTitle(true));
                UiFactory.Stretch(cont.GetComponent<RectTransform>(), 0.34f, 0.28f, 0.66f, 0.42f);
                Button fresh = UiFactory.MakeButton("New", titlePanel, "새로 시작", 32, new Color(0.45f, 0.50f, 0.62f), OnNewGamePressed);
                UiFactory.Stretch(fresh.GetComponent<RectTransform>(), 0.34f, 0.14f, 0.66f, 0.25f);
                titleNewText = fresh.GetComponentInChildren<Text>();
            }
            else
            {
                Button start = UiFactory.MakeButton("Start", titlePanel, "시작하기", 44, new Color(0.30f, 0.60f, 0.45f), () => StartFromTitle(false));
                UiFactory.Stretch(start.GetComponent<RectTransform>(), 0.34f, 0.24f, 0.66f, 0.38f);
            }

            Button snd = UiFactory.MakeButton("Sound", titlePanel, "설정", 24, new Color(0.40f, 0.45f, 0.55f), OpenSettings);
            UiFactory.Stretch(snd.GetComponent<RectTransform>(), 0.02f, 0.03f, 0.16f, 0.09f);

            Text ver = UiFactory.Label("Version", titlePanel, "프로토타입 v0.5", 22, new Color(0.60f, 0.64f, 0.75f), TextAnchor.LowerRight);
            UiFactory.Stretch(ver.rectTransform, 0.60f, 0.01f, 0.99f, 0.07f);
        }

        void OnNewGamePressed()
        {
            if (!newArmed)
            {
                newArmed = true;
                if (titleNewText != null) titleNewText.text = "저장이 지워져요. 한 번 더";
                return;
            }
            StartFromTitle(false);
        }

        void StartFromTitle(bool resume)
        {
            if (titlePanel != null) Destroy(titlePanel.gameObject);
            titlePanel = null;
            if (resume)
            {
                ApplySave(SaveData.Load());
                StartMorning();
            }
            else
            {
                ResetProgress();
                ShowSetup();
            }
        }

        // ---------- 새 게임 설정: 이름, 성별, 미용실 이름 ----------

        RectTransform setupPanel;
        int setupGender;
        Button[] genderButtons;
        static readonly string[] GenderNames = { "여성", "남성", "밝히지 않음" };

        InputField MakeInput(Transform parent, string placeholder, string initial, int limit)
        {
            Image bg = UiFactory.Panel("Input", parent, new Color(1f, 1f, 1f, 0.95f));
            InputField field = bg.gameObject.AddComponent<InputField>();
            Text text = UiFactory.Label("Text", bg.transform, "", 40, TextDark, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.rectTransform.offsetMin = new Vector2(20f, 0f);
            text.rectTransform.offsetMax = new Vector2(-20f, 0f);
            Text ph = UiFactory.Label("Placeholder", bg.transform, placeholder, 40, new Color(0.55f, 0.55f, 0.60f), TextAnchor.MiddleLeft);
            ph.rectTransform.offsetMin = new Vector2(20f, 0f);
            ph.rectTransform.offsetMax = new Vector2(-20f, 0f);
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = limit;
            field.text = initial;
            return field;
        }

        void ShowSetup()
        {
            if (setupPanel != null) Destroy(setupPanel.gameObject);
            Image bg = UiFactory.Panel("SetupPanel", canvasRoot, new Color(0.96f, 0.93f, 0.88f));
            UiFactory.Stretch(bg.rectTransform, 0f, 0f, 1f, 1f);
            setupPanel = bg.rectTransform;
            setupPanel.SetAsLastSibling();
            setupGender = 0;

            Text head = UiFactory.Label("Head", setupPanel, "입사 서류", 64, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(head.rectTransform, 0f, 0.84f, 1f, 0.97f);

            Text nameLabel = UiFactory.Label("NameLabel", setupPanel, "내 이름", 36, TextDark, TextAnchor.MiddleRight);
            UiFactory.Stretch(nameLabel.rectTransform, 0.10f, 0.68f, 0.30f, 0.78f);
            InputField nameField = MakeInput(setupPanel, "이름 (최대 6글자)", "", 6);
            UiFactory.Stretch(nameField.GetComponent<RectTransform>(), 0.32f, 0.68f, 0.88f, 0.78f);

            Text genderLabel = UiFactory.Label("GenderLabel", setupPanel, "성별", 36, TextDark, TextAnchor.MiddleRight);
            UiFactory.Stretch(genderLabel.rectTransform, 0.10f, 0.54f, 0.30f, 0.64f);
            genderButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int g = i;
                genderButtons[i] = UiFactory.MakeButton("Gender" + i, setupPanel, GenderNames[i], 32, new Color(0.45f, 0.50f, 0.60f), () => { setupGender = g; RefreshGenderButtons(); });
                float x0 = 0.32f + i * 0.19f;
                UiFactory.Stretch(genderButtons[i].GetComponent<RectTransform>(), x0, 0.54f, x0 + 0.17f, 0.64f);
            }
            RefreshGenderButtons();

            Text salonLabel = UiFactory.Label("SalonLabel", setupPanel, "미용실 이름", 36, TextDark, TextAnchor.MiddleRight);
            UiFactory.Stretch(salonLabel.rectTransform, 0.06f, 0.40f, 0.30f, 0.50f);
            InputField salonField = MakeInput(setupPanel, "미용실 이름 (최대 12글자)", "", 12);
            UiFactory.Stretch(salonField.GetComponent<RectTransform>(), 0.32f, 0.40f, 0.88f, 0.50f);

            Text hint = UiFactory.Label("Hint", setupPanel, "비워 두면 이름은 '막내', 미용실은 '해피헤어 번화가점'이 돼요.", 26, new Color(0.40f, 0.40f, 0.45f), TextAnchor.MiddleCenter);
            UiFactory.Stretch(hint.rectTransform, 0f, 0.30f, 1f, 0.38f);

            Button go = UiFactory.MakeButton("SetupDone", setupPanel, "출근하기", 48, new Color(0.30f, 0.60f, 0.45f), () => FinishSetup(nameField.text, salonField.text));
            UiFactory.Stretch(go.GetComponent<RectTransform>(), 0.35f, 0.10f, 0.65f, 0.24f);
        }

        void RefreshGenderButtons()
        {
            if (genderButtons == null) return;
            for (int i = 0; i < genderButtons.Length; i++)
            {
                Image img = genderButtons[i].GetComponent<Image>();
                if (img != null) img.color = i == setupGender ? new Color(0.30f, 0.60f, 0.45f) : new Color(0.45f, 0.50f, 0.60f);
            }
        }

        void FinishSetup(string name, string salon)
        {
            name = (name ?? "").Trim();
            salon = (salon ?? "").Trim();
            playerName = name.Length > 0 ? name : "막내";
            salonName = salon.Length > 0 ? salon : "해피헤어 번화가점";
            gender = setupGender;
            if (setupPanel != null) Destroy(setupPanel.gameObject);
            setupPanel = null;
            StartMorning();
        }

        bool resetArmed;

        // 저장을 지우고 처음부터 다시 시작 (두 번 눌러야 실행)
        void OnResetPressed()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                if (resetButtonText != null) resetButtonText.text = "정말 지울까요? 한 번 더";
                return;
            }
            resetArmed = false;
            ResetProgress();
            StartMorning();
        }

        void ResetProgress()
        {
            SaveData.Delete();
            day = 1;
            level = 0;
            xp = 0;
            score = 0;
            done = 0;
            missed = 0;
            for (int i = 0; i < practice.Length; i++) practice[i] = 0;
            for (int i = 0; i < tourSeen.Length; i++) tourSeen[i] = false;
            introShown = false;
            lastExamDay = 0;
            graduated = false;
            points = 0;
            playerName = "막내";
            gender = 0;
            salonName = "해피헤어 번화가점";
            CursorSkins.Reset();
            closeness.Clear();
            ResetExtras();
        }

        void NextDay()
        {
            day++;
            StartMorning(); // 아침이 시작되면 자동 저장
        }

        // 오늘의 예약표: 세로줄 = 디자이너, 가로줄 = 시간, 칸 = 시술 (해당 시간이 되면 실제 요청 카드가 된다)
        static readonly Color[] ProcedureColors =
        {
            new Color(0.70f, 0.85f, 1.00f), // 샴푸
            new Color(1.00f, 0.80f, 0.90f), // 펌
            new Color(1.00f, 0.85f, 0.65f), // 염색
            new Color(0.75f, 0.92f, 0.75f), // 드라이
            new Color(0.85f, 0.80f, 0.95f), // 커트
            new Color(0.98f, 0.95f, 0.70f), // 탈색
        };

        void BuildReservationGrid()
        {
            int rows = designers.Length;
            int cols = DayConfig.ReservationSlots;
            picked.Clear();

            // 예약 뽑기: 칸마다 확률로 예약이 생기고, 시술은 디자이너 확률표로 정한다
            resProc = new int[rows, cols];
            List<Vector2Int> booked = new List<Vector2Int>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    resProc[r, c] = -1;
                    if (Random.value < DayConfig.ReservationChance)
                    {
                        resProc[r, c] = PickProcedure(designers[r], true);
                        booked.Add(new Vector2Int(r, c));
                    }
                }
            }
            // 예약 중 하나는 VIP로 가려서 보여준다
            resVip = booked.Count > 0 ? booked[Random.Range(0, booked.Count)] : new Vector2Int(-1, -1);

            DrawReservationGrid();
        }

        // 고른 선생님들의 예약만 시간 칸별로 모아 영업에서 쓴다
        void BuildBookings()
        {
            int rows = designers.Length;
            int cols = DayConfig.ReservationSlots;
            bookingsBySlot = new List<Booking>[cols];
            for (int c = 0; c < cols; c++)
            {
                bookingsBySlot[c] = new List<Booking>();
                for (int r = 0; r < rows; r++)
                {
                    if (resProc[r, c] < 0) continue;
                    bookingsBySlot[c].Add(new Booking
                    {
                        designer = designers[r],
                        procedure = resProc[r, c],
                        isVip = picked.Contains(designers[r]) && (resVip.x == r && resVip.y == c),
                        npcOnly = !picked.Contains(designers[r]),
                    });
                }
            }
        }

        void TogglePicked(int row)
        {
            DesignerData d = designers[row];
            if (picked.Contains(d)) picked.Remove(d);
            else if (picked.Count < PickMax) picked.Add(d);
            else Say("선생님은 최대 " + PickMax + "명까지 고를 수 있어요.");
            DrawReservationGrid();
        }

        void DrawReservationGrid()
        {
            foreach (Transform child in reservationArea) Destroy(child.gameObject);

            int rows = designers.Length;
            int cols = DayConfig.ReservationSlots;

            float nameW = 0.26f;
            float colW = (1f - nameW) / cols;
            float rowH = 1f / (rows + 1);
            Color headerBg = new Color(0.20f, 0.20f, 0.26f);
            Color nameBg = new Color(0.90f, 0.86f, 0.80f);
            Color pickedBg = new Color(0.55f, 0.82f, 0.60f);
            Color emptyBg = new Color(1f, 1f, 1f, 0.85f);

            // 맨 윗줄: 시간
            GridCell(0f, nameW, 0, rowH, "선생님 이름을 눌러 고르기", headerBg, Color.white, 20);
            for (int c = 0; c < cols; c++)
            {
                string time = DayConfig.FormatClock(DayConfig.OpenMinutes + c * DayConfig.SlotMinutes);
                GridCell(nameW + c * colW, nameW + (c + 1) * colW, 0, rowH, time, headerBg, Color.white, 15);
            }

            // 디자이너 한 명당 한 줄
            for (int r = 0; r < rows; r++)
            {
                DesignerData d = designers[r];
                bool isPicked = picked.Contains(d);
                int row = r;
                Image nameCell = GridCell(0f, nameW, r + 1, rowH, (isPicked ? "✔ " : "") + d.name + " · " + d.specialty + HeartText(d), isPicked ? pickedBg : nameBg, TextDark, 20);
                Button nameButton = nameCell.gameObject.AddComponent<Button>();
                nameButton.targetGraphic = nameCell;
                nameButton.onClick.AddListener(() => TogglePicked(row));

                for (int c = 0; c < cols; c++)
                {
                    float x0 = nameW + c * colW;
                    float x1 = nameW + (c + 1) * colW;
                    float dim = isPicked ? 1f : 0.55f; // 고르지 않은 선생님 줄은 흐리게

                    if (resProc[r, c] < 0)
                    {
                        GridCell(x0, x1, r + 1, rowH, "", new Color(1f, 1f, 1f, 0.85f * dim), TextDark, 22);
                    }
                    else if (resVip.x == r && resVip.y == c)
                    {
                        GridCell(x0, x1, r + 1, rowH, "VIP", Color.Lerp(emptyBg, new Color(0.95f, 0.80f, 0.35f), dim), TextDark, 15);
                    }
                    else
                    {
                        GridCell(x0, x1, r + 1, rowH, LevelConfig.ProcedureShortNames[resProc[r, c]],
                            Color.Lerp(emptyBg, ProcedureColors[resProc[r, c]], dim), TextDark, 15);
                    }
                }
            }
            UpdateStartButton();
        }

        void UpdateStartButton()
        {
            if (morningStartButton == null) return;
            bool ready = picked.Count == PickMax;
            morningStartButton.interactable = ready;
            morningStartText.text = ready ? "영업 시작 (" + picked.Count + "/" + PickMax + "명)" : "선생님을 " + PickMax + "명 골라주세요 (" + picked.Count + "/" + PickMax + ")";
        }

        Image GridCell(float x0, float x1, int row, float rowH, string text, Color bg, Color fg, int size)
        {
            float yMax = 1f - row * rowH;
            float yMin = yMax - rowH;

            Image cell = UiFactory.Panel("Cell", reservationArea, bg);
            UiFactory.Stretch(cell.rectTransform, x0, yMin, x1, yMax);
            cell.rectTransform.offsetMin = new Vector2(1.5f, 1.5f);
            cell.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);

            Text label = UiFactory.Label("Text", cell.transform, text, size, fg, TextAnchor.MiddleCenter);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return cell;
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
        // 시술을 끝내면 줄어드는 재료: 샴푸 / 펌 / 염색 / 드라이
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
        // 선생님과의 친분 (0~100). 높을수록 제한시간이 길어지고 점수 보너스가 붙는다.
        readonly Dictionary<int, float> closeness = new Dictionary<int, float>();
        float Closeness(DesignerData d)
        {
            float v;
            return closeness.TryGetValue(d.id, out v) ? v : 0f;
        }
        void AddCloseness(DesignerData d, float delta)
        {
            closeness[d.id] = Mathf.Clamp(Closeness(d) + delta, 0f, 100f);
        }
        string HeartText(DesignerData d)
        {
            int h = Mathf.FloorToInt(Closeness(d) / 20f);
            return h > 0 ? " ♥" + h : "";
        }

        float TimeLimitFor(DesignerData d, int procedure = -1)
        {
            float tired = 1f - 0.3f * (fatigue / 100f);
            int[] steps = procedure >= 0 ? RouteFor(procedure) : null;
            float longer = steps != null ? 1f + 0.5f * (steps.Length - 1) : 1f; // 단계가 많은 시술은 시간을 더 준다
            return LevelConfig.TimeLimit[level] * Mathf.Max(0.3f, d.patienceMultiplier) * tired * longer * (1f + 0.2f * Closeness(d) / 100f);
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

        // 시간이 지나면 재료가 줄고, 0인 재료가 있으면 선생님에게 혼난다
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
            dayScolds++;
            score = Mathf.Max(0, score - ScoldSupplyPenalty);
            Say("선생님들 잔소리: " + StockNames[k] + "이(가) 떨어졌잖아! (점수 -" + ScoldSupplyPenalty + ")");
            Toast("✖ " + StockNames[k] + "이(가) 떨어졌어요!  점수 -" + ScoldSupplyPenalty, false);
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
                "휴게실  -  뭘 할까요? (시간이 건너뛰어요)   (남은 휴게시간 " + Mathf.CeilToInt(restBudget) + "분 / 피로도 " + Mathf.RoundToInt(fatigue) + ")", 44, Color.white, TextAnchor.MiddleCenter);
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

        // 휴식은 시간을 건너뛴다: 고르는 즉시 시계가 그만큼 흐르고 피로도가 줄어든다
        void PickRest(int kind)
        {
            CloseRestChoice();
            restKind = kind;
            int minutes = RestMinutes[kind];
            float amount = RestRecover[kind] * ((kind == 0 && IsLunchTime()) ? 1.5f : 1f);
            amount = ApplySnack(amount);

            fatigue = Mathf.Max(0f, fatigue - amount);
            restBudget = Mathf.Max(0f, restBudget - minutes);
            SkipClock(minutes);

            int limit = Mathf.Max(2, LevelConfig.MaxConcurrent[level] - 1);
            if (kind == 1) limit = Mathf.Max(1, limit - 1); // 낮잠 중에는 늦게 알아채서 더 빨리 잔소리
            int waiting = active.Count + waitingBookings.Count;

            string msg = RestNames[kind] + " " + minutes + "분 (지금 " + DayConfig.FormatClock(ClockMinutes())
                + ", 피로도 -" + Mathf.RoundToInt(amount) + ", 남은 휴게시간 " + Mathf.CeilToInt(restBudget) + "분)";
            if (waiting >= limit)
            {
                score = Mathf.Max(0, score - ScoldPenalty);
                msg = "선생님들 잔소리: 손님이 밀렸는데 쉬고 있었어요! (점수 -" + ScoldPenalty + ")  " + msg;
            }
            Say(msg);
            if (waiting >= limit) Toast("✖ 손님이 밀렸는데 쉬었어요!  점수 -" + ScoldPenalty, false);
            else Toast("✔ " + RestNames[kind] + " 끝!  피로도 -" + Mathf.RoundToInt(amount), true);
            RefreshTop();
        }

        // 시계를 minutes(게임 분)만큼 건너뛴다. 그동안 손님은 기다리고 재료도 줄어든다
        void SkipClock(int minutes)
        {
            float sec = minutes / DayConfig.MinutesPerSecond;
            businessElapsed = Mathf.Min(DayConfig.BusinessSeconds, businessElapsed + sec);

            for (int k = 0; k < stock.Length; k++)
            {
                stockDrain[k] -= sec;
                while (stockDrain[k] <= 0f)
                {
                    stockDrain[k] += Random.Range(StockDrainMin[k], StockDrainMax[k]);
                    if (stock[k] > 0) stock[k]--;
                }
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Request r = active[i];
                if (r.processing) continue;
                r.remaining -= sec;
                if (r.remaining <= 0f) Expire(r);
            }
            ReleaseBookings(ClockMinutes());
        }

        void CancelRestChoice()
        {
            CloseRestChoice();
            Say("쉬는 건 나중에. 일하러 가요!");
        }

        // 쉬는 중에 손님이 너무 밀리면 선생님들에게 잔소리를 듣고 쉬던 걸 멈춘다
        void InterruptRest()
        {
            if (!resting) return;
            int limit = Mathf.Max(2, LevelConfig.MaxConcurrent[level] - 1);
            if (restKind == 1) limit = Mathf.Max(1, limit - 1); // 낮잠 중에는 늦게 알아채서 더 빨리 잔소리
            if (active.Count < limit) return;

            resting = false;
            score = Mathf.Max(0, score - ScoldPenalty);
            Say("선생님들 잔소리: 손님이 밀렸는데 쉬고 있었어요! (점수 -" + ScoldPenalty + ")");
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
            while (nextSlot < bookingsBySlot.Length && minute >= DayConfig.OpenMinutes + nextSlot * DayConfig.SlotMinutes)
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

                bool locked = !b.npcOnly && level < LevelConfig.ProcedureUnlockLevel[b.procedure];
                if (locked && tourSeen[b.procedure])
                {
                    waitingBookings.RemoveAt(i); // 이미 견학한 시술의 예약은 디자이너가 알아서 처리
                    return;
                }
                Request r = new Request
                {
                    designer = b.designer,
                    label = ProcLabel(b.procedure),
                    isProcedure = true,
                    isLocked = locked,
                    isReserved = true,
                    isVip = b.isVip,
                    npcOnly = b.npcOnly,
                    procIdx = b.procedure,
                    route = locked ? null : RouteFor(b.procedure),
                    station = locked ? -1 : StationFor(b.procedure),
                    timeLimit = TimeLimitFor(b.designer, b.procedure),
                };
                r.remaining = r.timeLimit;
                AssignCustomer(r);
                CreateCard(r);
                if (!b.npcOnly) dayRequests++;
                active.Add(r);
                if (!b.npcOnly) InterruptRest();
                waitingBookings.RemoveAt(i);
                if (!b.npcOnly) Say((b.isVip ? "VIP " : "") + "예약 손님 도착: " + b.designer.name + " 선생님 / " + r.label);
                return; // 한 번에 한 장씩
            }
        }

        // 아직 견학하지 않았고 지금 카드로 떠 있지도 않은 시술 중 가장 앞 번호
        int NextTourProcedure()
        {
            for (int p = 0; p < LevelConfig.ProcedureNames.Length; p++)
            {
                if (tourSeen[p]) continue;
                bool shown = false;
                foreach (Request r in active) if (r.isProcedure && r.procIdx == p) shown = true;
                if (!shown) return p;
            }
            return -1;
        }

        void TrySpawn()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                DesignerData d = picked[Random.Range(0, picked.Count)];
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
                    int p;
                    if (TourDay)
                    {
                        p = NextTourProcedure();
                        if (p < 0) return;
                        locked = true;
                    }
                    else
                    {
                        p = PickProcedure(d);
                        locked = level < LevelConfig.ProcedureUnlockLevel[p];
                        if (locked && tourSeen[p]) continue; // 견학은 시술 종류마다 한 번만

                        // 못 하는 시술은 일부만 화면에 보여준다
                        if (locked && Random.value > LevelConfig.LockedShowRate[level]) continue;
                    }
                    procIndex = p;
                    label = ProcLabel(p);
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

                AssignCustomer(r);
                CreateCard(r);
                dayRequests++;
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

        // 커트 손님이 가고 나면 그 선생님 자리 앞 바닥에 머리카락이 쌓인다
        void SpawnFloorMess(DesignerData at)
        {
            foreach (Request q in active) if (q.isMess) return; // 이미 있으면 하나만

            Request m = new Request
            {
                designer = designers[0],
                label = "바닥 청소",
                isProcedure = false,
                isMess = true,
                useAnchor = true,
                anchor = MessPos(at),
                station = -1,
                procIdx = -1,
                timeLimit = 45f,
            };
            m.remaining = m.timeLimit;
            CreateCard(m);
            active.Add(m);
            Say("커트 손님이 가고 나서 바닥에 머리카락이 가득해요! 쓸어주세요.");
        }

        Vector2 MessPos(DesignerData d)
        {
            Vector2 seat = SeatPos(d);
            return new Vector2(seat.x, seat.y > 0f ? 100f : -100f);
        }

        bool HasRequest(DesignerData d)
        {
            foreach (Request r in active)
            {
                if (r.designer == d && !r.isMess) return true;
            }
            return false;
        }

        // 디자이너의 시술 확률(샴푸/펌/염색/드라이)로 하나 뽑기
        int PickProcedure(DesignerData d, bool reservation = false)
        {
            // 커트는 Lv.5부터 모든 디자이너가 같은 비율로 요청 (임시)
            // 예약은 디자이너 일정이라 커트도 처음부터 들어간다 (해금 전에는 견학 카드로 나온다)
            int cutWeight = (reservation || level >= LevelConfig.ProcedureUnlockLevel[4]) ? 25 : 0;
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
            if (r.npcOnly && !AnyNpcOnBreak())
            {
                Toast("동료 인턴이 맡은 선생님이에요. 인턴이 휴게 갈 때는 도와줄 수 있어요", false);
                return;
            }
            if (!TryReclaimFromNpc(r)) return;
            if (r.processing)
            {
                Toast("⏱ " + StepNames[r.route[r.routeIdx - 1]] + " 약이 작용하는 중이에요. 다른 일을 먼저 해요", true);
                return;
            }
            if (!r.isMess && r.route != null && r.chair < 0 && !(carrying && playerTask == r) && StepStation[r.route[0]] == 1 && FreeChairs() == 0)
            {
                Toast("시술석 의자가 모두 차 있어요! 다른 요청을 먼저 해요", false);
                return;
            }

            if (resting) Say("쉬다 말고 일하러 가요!");
            resting = false;
            restWalking = false;
            supplyWalking = false;
            refilling = false;
            if (carrying && playerTask != r)
            {
                if (playerTask.route != null) { playerTask.routeIdx = 0; playerTask.started = false; }
                ReleaseChair(playerTask);
                if (chairWaiter == playerTask) chairWaiter = null;
                SendCustomerBack(SeatPos(playerTask.designer));
            }
            if (carrying && playerTask == r)
            {
                // 이미 손님을 데리고 있으면 바로 구역으로
                chairWaiter = null;
                if (!ReserveFor(r, r.routeIdx)) { chairWaiter = r; Toast("시술석 의자가 모두 차 있어요! 자리가 날 때까지 기다려요", false); return; }
                playerTarget = StepPos(r, r.routeIdx);
                playerWalking = true;
                return;
            }
            if (r.ready)
            {
                // 대기가 끝났으니 의자에 앉은 손님을 모시러 간다
                playerTask = r;
                r.pickup = true;
                playerTarget = r.chair >= 0 ? ChairPos(r.chair) : new Vector2(SeatPos(r.designer).x, SeatPos(r.designer).y > 0f ? 55f : -55f);
                playerWalking = true;
                Say("손님을 모시러 가요");
                return;
            }
            playerTask = r;
            r.started = false;
            r.routeIdx = 0;
            Vector2 seat = SeatPos(r.designer);
            playerTarget = r.useAnchor ? r.anchor : new Vector2(seat.x, seat.y > 0f ? 55f : -55f);
            playerWalking = true;
            Say(r.isMess ? "바닥을 쓸러 가는 중..." : r.designer.name + " 선생님에게 가는 중... (" + r.label + ")");
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
                if (task.pickup)
                {
                    FinishPickup(task);
                    return;
                }
                if (!task.started)
                {
                    task.started = true;
                    task.routeIdx = 0;
                    if (StepStation[task.route[0]] >= 0)
                    {
                        // 디자이너 자리에서 손님을 모시고 첫 단계 구역으로 출발
                        if (!ReserveFor(task, 0))
                        {
                            task.started = false;
                            playerTask = null;
                            Toast("시술석 의자가 모두 차 있어요! 다른 요청을 먼저 해요", false);
                            return;
                        }
                        PickUpCustomer();
                        playerTarget = StepPos(task, 0);
                        playerWalking = true;
                        Say(CustomerGreeting(task) + " (" + StepNames[task.route[0]] + "하러 모셔요)");
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
        static readonly int[] StepStation = { 0, 1, 1, -1, -1, 1 };      // 구역 (-1은 디자이너 자리)
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
            if (st == 1 && r.chair >= 0) return ChairPos(r.chair);
            if (st >= 0) return StationPos[st];
            Vector2 seat = SeatPos(r.designer);
            return new Vector2(seat.x, seat.y > 0f ? 55f : -55f);
        }

        // 이번 단계 시작: 인턴이 할 수 있으면 금방, 못 하면 디자이너가 하는 동안 기다린다
        void BeginStep(Request task)
        {
            int step = task.route[task.routeIdx];
            bool mine = level >= StepUnlock[step];
            if (mine)
            {
                // 내가 할 수 있는 단계: 짧은 미니게임으로 직접 한다
                minigameActive = true;
                System.Action<ExamResult> done = r =>
                {
                    minigameActive = false;
                    if (task == null || playerTask != task || !active.Contains(task)) return;
                    string g = r.hits >= 85 ? "S" : (r.hits >= 60 ? "A" : (r.hits >= 30 ? "B" : "C"));
                    Say(StepNames[step] + " " + g + "등급 (" + r.hits + "%)");
                    CompleteStep(task, r.hits);
                };
                // 샴푸(적시기, 샴푸, 헹구기)와 펌(빗질, 파지, 롯드, 고무줄)은 전용 미니게임
                if (step == 0) { ShampooExam.Lite = true; ExamBase.Show<ShampooExam>(canvasRoot, done); }
                else if (step == 1) { PermExam.Lite = true; ExamBase.Show<PermExam>(canvasRoot, done); }
                else { StepGame.Step = step; ExamBase.Show<StepGame>(canvasRoot, done); }
                return;
            }
            waitingAtStation = true;
            stationWait = 4f;
            Say(task.designer.name + " 선생님이 " + StepNames[step] + "하는 중...");
        }

        void ConsumeStep(int step)
        {
            foreach (int k in StepUses[step])
            {
                if (stock[k] > 0) stock[k]--;
                else ScoldSupply(k);
            }
        }

        // 단계마다 점수가 다르다: 샴푸는 적게, 탈색은 많이. 시술 점수는 거치는 단계의 합이다.
        // 샴푸 0, 펌 1, 염색 2, 드라이 3, 커트 4, 탈색 5
        static readonly int[] StepPoints = { 5, 15, 12, 6, 0, 20 }; // 커트는 선생님 몫이라 점수 없음

        static int StepScore(Request r)
        {
            if (r.route == null) return 20;
            int sum = 0;
            foreach (int step in r.route) sum += StepPoints[step];
            return sum;
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

            CompleteStep(task, 70); // 선생님이 한 단계는 보통 솜씨
        }

        // 한 단계를 끝냈을 때: 완성도를 기록하고 다음 단계로 (마지막이면 시술 완료)
        void CompleteStep(Request task, int pct)
        {
            int step = task.route[task.routeIdx];
            task.satSum += pct;
            task.satCount++;
            ConsumeStep(step);
            if (step == 4) SpawnFloorMess(task.designer); // 커트 뒤에는 바닥에 머리카락이 남는다

            task.routeIdx++;
            if (task.routeIdx >= task.route.Length)
            {
                playerTask = null;
                SendCustomerBack(SeatPos(task.designer));
                PerformRequest(task);
                return;
            }

            if (StepWaitMin[step] > 0)
            {
                BeginProcessing(task, step); // 약이 작용하는 동안 손님은 의자에 앉아서 기다린다
                return;
            }
            if (StepStation[step] == 1) ReleaseChair(task);
            if (!ReserveFor(task, task.routeIdx))
            {
                chairWaiter = task;
                Toast("시술석 의자가 모두 차 있어요! 자리가 날 때까지 기다려요", false);
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
                case 1: return 1;      // 펌 -> 시술석
                case 2: return 1;      // 염색 -> 시술석
                default: return -1;    // 커트, 드라이 -> 선생님 자리에서
            }
        }

        void PickUpCustomer()
        {
            if (customerRt != null) Destroy(customerRt.gameObject);
            int look = playerTask != null ? Mathf.Abs(playerTask.customerType) % 4 : 0;
            Image c = Art.Figure("Customer", mapArea, new Color(0.96f, 0.78f, 0.66f), "customer_" + look);
            customerRt = c.rectTransform;
            PlaceCenter(customerRt, playerRt.anchoredPosition + new Vector2(-56f, 0f), new Vector2(84f, 84f));
            if (Art.Get("customer_" + look) == null) UiFactory.Label("CustomerText", c.transform, "손님", 18, TextDark, TextAnchor.MiddleCenter);
            AddHeadLabel(customerRt);
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

        // 자리 의자에 앉아 있는 손님: 요청이 있고, 인턴이 데려가지 않았을 때만 보인다
        void UpdateSeatedCustomers()
        {
            foreach (var kv in seatedCustomers)
            {
                if (kv.Value == null) continue;
                Request req = null;
                foreach (Request r in active)
                    if (r.designer == kv.Key && !r.isMess) { req = r; break; }
                bool away = customerRt != null && playerTask != null && playerTask.designer == kv.Key;
                bool show = req != null && !away;
                if (kv.Value.gameObject.activeSelf != show) kv.Value.gameObject.SetActive(show);
                if (!show) continue;
                int look = Mathf.Abs(req.customerType) % 4;
                int prev;
                if (!seatedLook.TryGetValue(kv.Key, out prev) || prev != look)
                {
                    seatedLook[kv.Key] = look;
                    Art.Apply(kv.Value, "customer_" + look);
                }
            }
        }

        void UpdateCustomer()
        {
            if (customerRt == null) return;
            if (carrying)
            {
                customerRt.anchoredPosition = playerRt.anchoredPosition + new Vector2(-56f, 0f);
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
                StartTour(r); // 견학: 튜토리얼처럼 시술 방법을 설명해 준다
                return;       // XP와 정리는 설명이 끝난 뒤 FinishTour에서 처리
            }
            else if (r.label == "바닥 청소")
            {
                StartFloorSweep(r);
                return; // 결과는 미니게임이 끝난 뒤 FinishFloorSweep에서 처리
            }
            else
            {
                if (!r.npcOnly) done++; // 건수는 내가 고른 선생님 일만 센다
                CountWaitUse(r);
                if (r.procIdx >= 0 && r.procIdx < practice.Length) practice[r.procIdx]++;
                int sat = r.satCount > 0 ? Mathf.RoundToInt((float)r.satSum / r.satCount) : 70; // 손님 만족도
                float mult = 0.7f + 0.6f * sat / 100f;
                int friendBonus = Mathf.FloorToInt(Closeness(r.designer) / 10f);
                int assignBonus = r.assignee == 0 ? 5 : 0; // 인턴장이 맡긴 일을 해냈을 때
                int helpBonus = r.npcOnly ? 10 : 0; // 동료 인턴이 휴게 간 사이 그 일을 대신하면 추가 점수 (못 해도 감점은 없다)
                int gained = Mathf.RoundToInt(StepScore(r) * mult) + (r.isVip ? 30 : (r.isReserved ? 10 : 0)) + friendBonus + assignBonus + helpBonus;
                gained = Mathf.Max(0, gained + OnRequestCompleted(r, sat));
                score += gained;
                string satGrade = sat >= 85 ? "S" : (sat >= 60 ? "A" : (sat >= 30 ? "B" : "C"));
                int closeDelta = sat >= 85 ? 4 : (sat >= 60 ? 2 : (sat >= 30 ? 0 : -2));
                AddCloseness(r.designer, closeDelta);
                fatigue = Mathf.Min(100f, fatigue + (r.isProcedure ? 6f : 4f) * FatigueMult());
                AddXp(r.isProcedure ? 2 : 1);
                Say(r.designer.name + " 선생님의 " + r.label + " 완료!" + (lastReaction.Length > 0 ? "  " + lastReaction : ""));
                Toast("✔ " + r.designer.name + " 선생님의 " + r.label + " 완료!  만족도 " + satGrade + "   +" + gained + "점  XP +2" + (assignBonus > 0 ? "  (맡은 일 +5)" : "") + (helpBonus > 0 ? "  (동료 대신 +10)" : "") + (closeDelta > 0 ? "  ♥친분 +" + closeDelta : (closeDelta < 0 ? "  ♥친분 " + closeDelta : "")), sat >= 30);
            }
            RemoveRequest(r);
            RefreshTop();
        }

        // ---------- 견학(튜토리얼)과 안내창 ----------

        bool introShown;
        readonly bool[] tourSeen = new bool[6]; // 시술별로 처음 견학하면 XP를 더 준다
        RectTransform dialogPanel;

        static readonly string[] StepHow =
        {
            "샴푸실에서 손님 머리를 감겨요. 손목으로 물 온도를 먼저 확인하고, 두피를 손끝으로 마사지하듯 씻어요.",
            "시술석에서 빗질로 머리를 정리하고, 파지로 머리를 나눠 롯드를 말고 고무줄로 고정해요. 롯드는 단단하고 고르게 말아야 해요.",
            "시술석에서 염색약을 배합해 뿌리부터 발라요. 시간을 지켜서 방치해야 색이 고르게 나와요.",
            "디자이너 자리에서 드라이로 말려 모양을 잡아요. 뿌리부터 말리고 끝을 마무리해요.",
            "선생님 자리에서 선생님이 커트해요. 끝나면 바닥에 머리카락이 남으니 바로 쓸어야 해요.",
            "시술석에서 탈색제를 발라 색을 빼요. 두피에 닿지 않게 조심하고 시간을 꼭 지켜요.",
        };

        void StartTour(Request r)
        {
            int[] route = RouteFor(r.procIdx);
            var pages = new List<string>();
            if (route == null)
            {
                pages.Add(r.label + "\n\n아직 자세한 설명이 없는 시술이에요.");
            }
            else
            {
                string order = "";
                for (int i = 0; i < route.Length; i++) order += (i > 0 ? " → " : "") + StepNames[route[i]];
                pages.Add(r.designer.name + " 선생님의 " + r.label + "을(를) 견학해요.\n\n"
                    + "이 시술은 " + route.Length + "단계예요.\n" + order);

                for (int i = 0; i < route.Length; i++)
                {
                    int step = route[i];
                    string where = StepStation[step] >= 0 ? StationNames[StepStation[step]] : "디자이너 자리";
                    string mine = level >= StepUnlock[step]
                        ? "지금 레벨에서 직접 할 수 있어요."
                        : "Lv." + StepUnlock[step] + "부터 직접 할 수 있어요. 그전엔 디자이너가 해요.";
                    string uses = "";
                    foreach (int k in StepUses[step]) uses += (uses.Length > 0 ? ", " : "") + StockNames[k];
                    pages.Add((i + 1) + " / " + route.Length + "  " + StepNames[step] + "\n\n"
                        + "장소: " + where + "\n" + StepHow[step] + "\n\n쓰는 재료: " + uses + "\n" + mine);
                }
            }

            int gain = 2; // 견학은 종류마다 한 번뿐이라 XP를 넉넉히
            ShowDialog("견학: " + r.label, pages, "견학 끝 (XP +" + gain + ")", () => FinishTour(r, gain));
        }

        void FinishTour(Request r, int gain)
        {
            if (r.procIdx >= 0 && r.procIdx < tourSeen.Length) tourSeen[r.procIdx] = true;
            AddXp(gain);
            Say(r.designer.name + " 선생님의 " + r.label + "을(를) 견학했어요 (XP +" + gain + ")");
            Toast("✔ " + r.label + " 견학 완료!   XP +" + gain, true);
            if (active.Contains(r)) RemoveRequest(r);

            // 같은 시술의 다른 견학 요청은 이제 필요 없으니 정리
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Request q = active[i];
                if (q.isLocked && q.procIdx == r.procIdx) RemoveRequest(q);
            }
            RefreshTop();
        }

        void ShowIntro()
        {
            var pages = new List<string>
            {
                playerName + "님, " + salonName + "에 온 걸 환영해요!\n오늘부터 인턴이에요.\n\n초록색 '나'가 여러분이에요.\n말풍선을 누르면 그 자리로 걸어가요.",
                "회색 말풍선은 아직 못 하는 시술이에요.\n\n눌러서 '견학'하면 방법을 배우고 XP를 얻어요.\n견학은 시술 종류마다 한 번만 할 수 있어요.",
                "왼쪽 아래 재료실에서 수건, 도구, 음료, 청소용품을 채워요.\n\n재료가 0이 되면 선생님에게 혼나요.",
                "왼쪽 위 휴게실에서 쉬면 피로도가 줄어요.\n\n하루 휴게시간은 1시간이고, 손님이 밀렸을 때 쉬면 잔소리를 들어요.",
            };
            ShowDialog("처음이에요", pages, "시작하기", null);
        }

        // 여러 쪽짜리 안내창. 떠 있는 동안 시계는 멈춘다.
        void ShowDialog(string title, List<string> pages, string finishLabel, System.Action onFinish)
        {
            minigameActive = true;

            Image dim = UiFactory.Panel("Dialog", canvasRoot, new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);
            dialogPanel = dim.rectTransform;

            Image box = UiFactory.Panel("DialogBox", dim.transform, new Color(1f, 0.98f, 0.94f));
            UiFactory.Stretch(box.rectTransform, 0.15f, 0.12f, 0.85f, 0.88f);

            Text titleText = UiFactory.Label("Title", box.transform, title, 44, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(titleText.rectTransform, 0.03f, 0.84f, 0.97f, 0.98f);

            Text body = UiFactory.Label("Body", box.transform, "", 34, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(body.rectTransform, 0.06f, 0.24f, 0.94f, 0.83f);

            Text pageText = UiFactory.Label("Page", box.transform, "", 26, TextDark, TextAnchor.MiddleLeft);
            UiFactory.Stretch(pageText.rectTransform, 0.04f, 0.03f, 0.3f, 0.2f);

            int index = 0;
            Text nextLabel = null;
            Button next = null;
            System.Action show = () =>
            {
                body.text = pages[index];
                pageText.text = (index + 1) + " / " + pages.Count;
                nextLabel.text = index >= pages.Count - 1 ? finishLabel : "다음";
            };
            next = UiFactory.MakeButton("Next", box.transform, "다음", 36, new Color(0.30f, 0.60f, 0.45f), () =>
            {
                if (index < pages.Count - 1)
                {
                    index++;
                    show();
                    return;
                }
                if (dialogPanel != null) Destroy(dialogPanel.gameObject);
                dialogPanel = null;
                minigameActive = false;
                if (onFinish != null) onFinish();
            });
            UiFactory.Stretch(next.GetComponent<RectTransform>(), 0.55f, 0.04f, 0.96f, 0.19f);
            nextLabel = next.GetComponentInChildren<Text>();
            show();
        }

        void StartFloorSweep(Request r)
        {
            minigameActive = true;
            FloorSweepGame.Show(canvasRoot, result => FinishFloorSweep(r, result));
        }

        void FinishFloorSweep(Request r, SweepResult result)
        {
            minigameActive = false;
            fatigue = Mathf.Min(100f, fatigue + 8f * FatigueMult());

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

            if (grade == "C") missed++; else { done++; CountWaitUse(r); }
            score += points;
            AddXp(gainXp);

            Say("바닥 청소 " + grade + "등급! ("
                + result.cleaned + "/" + result.total + ", 점수 +" + points + ")");
            Toast((grade == "C" ? "✖ " : "✔ ") + "바닥 청소 " + grade + "등급!   +" + points + "점", grade != "C");

            if (active.Contains(r)) RemoveRequest(r);
            RefreshTop();
        }

        void Expire(Request r)
        {
            if (r.npcOnly) { RemoveRequest(r); RefreshTop(); return; } // 동료 인턴 담당 요청은 놓쳐도 감점 없음
            missed++;
            if (!r.isMess)
            {
                AddCloseness(r.designer, -3f);
                score -= MissPenaltyNow; // 내가 쉬는 동안 인턴들이 못 끝낸 일도 포함해, 놓친 건마다 감점
            }
            Toast(r.isMess ? "✖ 바닥을 못 치웠어요" : "✖ " + r.designer.name + " 선생님의 " + r.label + " 놓침   -" + MissPenaltyNow + "점", false);
            Say(r.isMess ? "바닥이 지저분한 채로 남았어요" : r.designer.name + " 선생님이 " + r.label + "을(를) 직접 처리했어요");
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
            ReleaseChair(r);
            DestroySeated(r);
            if (chairWaiter == r) chairWaiter = null;
            active.Remove(r);
            Destroy(r.background.gameObject);
        }

        // XP는 다음 레벨 필요량까지만 차고, 레벨은 승급시험을 통과해야 오른다
        void AddXp(int amount)
        {
            int need = LevelConfig.XpToNext(level);
            bool wasFull = xp >= need;
            xp = Mathf.Min(need, xp + amount);
            if (!wasFull && xp >= need)
            {
                Say(CanTakeExamIgnoringDay() ? "XP가 가득! 다음 날 아침에 승급시험을 볼 수 있어요." : "XP가 가득 찼어요! 시험 조건(연습 횟수)을 채워 보세요.");
            }
        }

        // ---------- 승급시험 ----------

        // 시험을 보려면 XP가 가득 차야 하고, 그 레벨의 시술을 정해진 횟수만큼 연습해야 한다
        // 0: 견학 6종, 1: 샴푸, 2: 펌, 3: 염색, 4: 드라이, 5: 커트, 6(졸업): 시술 총 22회
        static readonly int[] ExamNeed = { 6, 12, 4, 4, 4, 4, 22 };
        readonly int[] practice = new int[6]; // 시술별로 끝낸 횟수
        int lastExamDay;
        bool graduated;
        Button examButton;
        Text examButtonText;
        Text examInfoText;

        void ExamProgress(out string text, out bool met)
        {
            int have;
            string what;
            if (level == 0)
            {
                have = 0;
                for (int i = 0; i < tourSeen.Length; i++) if (tourSeen[i]) have++;
                what = "견학";
            }
            else if (level >= LevelConfig.MaxLevel)
            {
                have = 0;
                foreach (int c in practice) have += c;
                what = "시술 총";
            }
            else
            {
                have = practice[level - 1];
                what = LevelConfig.ProcedureShortNames[level - 1];
            }
            int need = LevelConfig.TestMode ? 1 : ExamNeed[level];
            met = have >= need;
            text = what + " " + Mathf.Min(have, need) + "/" + need + (met ? " ✔" : "");
        }

        bool CanTakeExamIgnoringDay()
        {
            if (graduated) return false;
            string t;
            bool met;
            ExamProgress(out t, out met);
            return met && xp >= LevelConfig.XpToNext(level);
        }

        bool CanTakeExam()
        {
            return CanTakeExamIgnoringDay() && lastExamDay != day;
        }

        string ExamStatusText()
        {
            if (graduated) return "졸업했어요!";
            string t;
            bool met;
            ExamProgress(out t, out met);
            string xpOk = xp >= LevelConfig.XpToNext(level) ? " ✔" : "";
            string title = level >= LevelConfig.MaxLevel ? "졸업시험 조건" : "승급시험 조건";
            string state = lastExamDay == day ? "\n오늘은 이미 봤어요." : (CanTakeExamIgnoringDay() ? "\n시험을 볼 수 있어요!" : "");
            return title + "\nXP " + xp + "/" + LevelConfig.XpToNext(level) + xpOk + "\n" + t + state;
        }

        void StartExam()
        {
            if (!CanTakeExam()) return;
            lastExamDay = day;
            int examLevel = level;
            // 다음 레벨에서 해금되는 시술의 시험을 통과해야 그 시술을 할 수 있다
            // Lv0 -> 샴푸, Lv1 -> 펌, Lv2 -> 염색(탈색 포함), Lv3 -> 드라이, Lv4 -> 커트, Lv5 -> 종합 순서
            switch (examLevel)
            {
                case 0: ExamBase.Show<ShampooExam>(canvasRoot, r => FinishExam(examLevel, r)); return;
                case 1: ExamBase.Show<PermExam>(canvasRoot, r => FinishExam(examLevel, r)); return;
                case 2: ExamBase.Show<DyeExam>(canvasRoot, r => FinishExam(examLevel, r)); return;
                case 3: ExamBase.Show<DryExam>(canvasRoot, r => FinishExam(examLevel, r)); return;
                case 4: ExamBase.Show<CutExam>(canvasRoot, r => FinishExam(examLevel, r)); return;
                case 5:
                {
                    // 종합 시험: 파마, 염색, 탈색 시술의 순서 맞추기
                    var problems = new List<ExamOrderGame.Problem>();
                    foreach (int proc in new[] { 1, 2, 5 })
                    {
                        int[] route = RouteFor(proc);
                        string[] names = new string[route.Length];
                        for (int i = 0; i < route.Length; i++) names[i] = StepNames[route[i]];
                        problems.Add(new ExamOrderGame.Problem
                        {
                            title = LevelConfig.ProcedureNames[proc] + " 시술의 순서",
                            steps = names,
                        });
                    }
                    ExamOrderGame.Show(canvasRoot, problems, r => FinishExam(examLevel, r));
                    return;
                }
            }
            // 졸업시험: 탈색 순서 -> 커트 -> 타이밍 (한 단계라도 실패하면 불합격)
            int[] bleachRoute = RouteFor(5);
            string[] bleachNames = new string[bleachRoute.Length];
            for (int i = 0; i < bleachRoute.Length; i++) bleachNames[i] = StepNames[bleachRoute[i]];
            var gradProblems = new List<ExamOrderGame.Problem>
            {
                new ExamOrderGame.Problem { title = LevelConfig.ProcedureNames[5] + " 시술의 순서", steps = bleachNames },
            };
            ExamOrderGame.Show(canvasRoot, gradProblems, r1 =>
            {
                if (!r1.passed) { FinishExam(examLevel, r1); return; }
                ExamBase.Show<CutExam>(canvasRoot, r2 =>
                {
                    if (!r2.passed) { FinishExam(examLevel, r2); return; }
                    ExamGame.Show(canvasRoot, examLevel, r3 => FinishExam(examLevel, r3));
                });
            });
        }

        void FinishExam(int examLevel, ExamResult result)
        {
            if (result.passed)
            {
                xp = 0;
                if (examLevel >= LevelConfig.MaxLevel)
                {
                    graduated = true;
                    Say("졸업시험 합격!");
                    ShowDialog("졸업", GraduationPages(result), "확인", () => { UpdateMorningTexts(); SaveGame(); });
                    return;
                }
                level++;
                Say("승급시험 합격! Lv." + level + " " + LevelConfig.Names[level]);
                ShowDialog("승급 합격", new List<string>
                {
                    result.hits + "/" + result.rounds + "번 성공!\n\n승급시험에 합격했어요.\n\nLv." + level + " " + LevelConfig.Names[level] + " 이(가) 되었어요.",
                }, "확인", () => { UpdateMorningTexts(); SaveGame(); });
            }
            else
            {
                Say("승급시험 불합격... 내일 다시 도전해요.");
                ShowDialog("불합격", new List<string>
                {
                    result.hits + "/" + result.rounds + "번 성공 (필요 " + result.needed + "번)\n\n아쉽게 떨어졌어요.\n내일 아침에 다시 볼 수 있어요.",
                }, "확인", () => { UpdateMorningTexts(); SaveGame(); });
            }
            UpdateMorningTexts();
        }

        void UpdateMorningTexts()
        {
            morningBody.text =
                "오늘의 목표 점수  " + DayConfig.TargetScore(level)
                + "      현재  Lv." + level + " " + LevelConfig.Names[level] + "   XP " + XpLabel() + GoalLine();
            if (examInfoText != null) examInfoText.text = ExamStatusText();
            if (examButton != null)
            {
                examButton.gameObject.SetActive(CanTakeExam());
                examButtonText.text = level >= LevelConfig.MaxLevel ? "졸업시험 보기" : "승급시험 보기";
            }
        }

        void ChangeLevel(int delta)
        {
            if (phase != Phase.Business || minigameActive) return;
            level = Mathf.Clamp(level + delta, 0, LevelConfig.MaxLevel);
            xp = 0;
            Say("[테스트] Lv." + level + " " + LevelConfig.Names[level] + " 로 변경");
            RefreshChairs();
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
            topText.text = playerName + "  Day " + day + "  " + DayConfig.FormatClock(ClockMinutes())
                + "   Lv." + level + " " + LevelConfig.Names[level]
                + "   XP " + XpLabel()
                + "   점수 " + score
                + "   완료 " + done
                + "   놓침 " + missed
                + (fastForward && phase == Phase.Business ? "   ⏩ 빨리 감기" : "")
                + (phase == Phase.Business ? (TourDay ? "   오늘 " + Mathf.Min(dayRequests, Quota()) + "/" + Quota() : "   오늘 " + (done - dayStartDone) + "건 (최소 " + Quota() + ")") : "");
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

            // 설정 버튼 (정식 모드에서는 오른쪽 끝, 테스트 모드에서는 테스트 버튼 왼쪽)
            Button settingsButton = UiFactory.MakeButton("SettingsButton", top.transform, "설정", 28, new Color(0.40f, 0.45f, 0.55f), OpenSettings);
            if (LevelConfig.TestMode) UiFactory.Stretch(settingsButton.GetComponent<RectTransform>(), 0.64f, 0.15f, 0.74f, 0.85f);
            else UiFactory.Stretch(settingsButton.GetComponent<RectTransform>(), 0.87f, 0.15f, 0.99f, 0.85f);

            // 테스트용 버튼: 하루 끝내기, 레벨 조절 (TestMode일 때만 보임)
            Button skip = UiFactory.MakeButton("SkipDay", top.transform, "하루 끝", 28, new Color(0.65f, 0.40f, 0.30f), SkipToEvening);
            UiFactory.Stretch(skip.GetComponent<RectTransform>(), 0.76f, 0.15f, 0.85f, 0.85f);
            Button minus = UiFactory.MakeButton("LevelMinus", top.transform, "Lv -", 32, new Color(0.45f, 0.45f, 0.55f), () => ChangeLevel(-1));
            UiFactory.Stretch(minus.GetComponent<RectTransform>(), 0.86f, 0.15f, 0.925f, 0.85f);
            Button plus = UiFactory.MakeButton("LevelPlus", top.transform, "Lv +", 32, new Color(0.30f, 0.60f, 0.45f), () => ChangeLevel(1));
            UiFactory.Stretch(plus.GetComponent<RectTransform>(), 0.935f, 0.15f, 0.995f, 0.85f);
            skip.gameObject.SetActive(LevelConfig.TestMode);
            minus.gameObject.SetActive(LevelConfig.TestMode);
            plus.gameObject.SetActive(LevelConfig.TestMode);

            // 미용실 바닥 (위에서 내려다본 그림)
            Image floor = UiFactory.Panel("SalonFloor", root, new Color(0.86f, 0.80f, 0.72f));
            floorImage = floor;
            Art.Apply(floor, "floor_tile", 0f, true);
            mapArea = floor.rectTransform;
            UiFactory.Stretch(mapArea, 0.03f, 0.22f, 0.97f, 0.89f);
            BuildFloor();

            // 알림 띠 (평소엔 숨김)
            toastPanel = UiFactory.Panel("Toast", root, ToastGood);
            UiFactory.Stretch(toastPanel.rectTransform, 0.25f, 0.80f, 0.75f, 0.885f);
            toastText = UiFactory.Label("ToastText", toastPanel.transform, "", 34, Color.white, TextAnchor.MiddleCenter);
            toastPanel.raycastTarget = false;
            toastPanel.gameObject.SetActive(false);

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

            Button resetButton = UiFactory.MakeButton("ResetButton", morningPanel, "처음부터 다시", 22, new Color(0.55f, 0.35f, 0.35f), OnResetPressed);
            UiFactory.Stretch(resetButton.GetComponent<RectTransform>(), 0.80f, 0.945f, 0.98f, 0.99f);
            resetButtonText = resetButton.GetComponentInChildren<Text>();

            Button toTitle = UiFactory.MakeButton("TitleButton", morningPanel, "시작 화면", 22, new Color(0.40f, 0.45f, 0.55f), ShowTitle);
            UiFactory.Stretch(toTitle.GetComponent<RectTransform>(), 0.62f, 0.945f, 0.78f, 0.99f);

            Button shopButton = UiFactory.MakeButton("ShopButton", morningPanel, "상점", 24, new Color(0.35f, 0.50f, 0.65f), OpenShop);
            UiFactory.Stretch(shopButton.GetComponent<RectTransform>(), 0.02f, 0.945f, 0.20f, 0.99f);
            shopButtonText = shopButton.GetComponentInChildren<Text>();

            Button soundButton = UiFactory.MakeButton("SoundButton", morningPanel, "설정", 24, new Color(0.40f, 0.50f, 0.45f), OpenSettings);
            UiFactory.Stretch(soundButton.GetComponent<RectTransform>(), 0.40f, 0.945f, 0.60f, 0.99f);

            Button achButton = UiFactory.MakeButton("AchButton", morningPanel, "업적", 24, new Color(0.60f, 0.50f, 0.30f), OpenAchievements);
            UiFactory.Stretch(achButton.GetComponent<RectTransform>(), 0.22f, 0.945f, 0.38f, 0.99f);

            saveText = UiFactory.Label("SaveText", canvasRoot, "저장 중...", 22, new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleRight);
            saveText.raycastTarget = false;
            UiFactory.Stretch(saveText.rectTransform, 0.80f, 0.895f, 0.985f, 0.94f);
            saveText.gameObject.SetActive(false);

            Transform startTf = morningPanel.Find("NextButton");
            morningStartButton = startTf.GetComponent<Button>();
            morningStartText = startTf.GetComponentInChildren<Text>();

            // 아침 화면 배치: 제목, 요약 줄, 예약표 시간표, 안내 문구
            UiFactory.Stretch(morningTitle.rectTransform, 0f, 0.885f, 1f, 0.99f);
            UiFactory.Stretch(morningBody.rectTransform, 0.04f, 0.80f, 0.96f, 0.875f);
            morningBody.fontSize = 32;
            morningBody.alignment = TextAnchor.MiddleCenter;

            reservationArea = UiFactory.Rect("ReservationGrid", morningPanel);
            UiFactory.Stretch(reservationArea, 0.04f, 0.19f, 0.96f, 0.79f);

            morningNote = UiFactory.Label("Note", morningPanel,
                "※ 오늘 같이 일할 선생님을 최대 3명 골라요.\n고른 선생님의 일은 내가 맡고, 나머지 선생님은 동료 인턴이 맡아요. (VIP는 보너스)", 24, TextDark, TextAnchor.MiddleLeft);
            UiFactory.Stretch(morningNote.rectTransform, 0.04f, 0.02f, 0.33f, 0.17f);

            // 승급시험: 조건 안내와 응시 버튼 (조건을 채웠을 때만 버튼이 보인다)
            examInfoText = UiFactory.Label("ExamInfo", morningPanel, "", 20, TextDark, TextAnchor.UpperLeft);
            UiFactory.Stretch(examInfoText.rectTransform, 0.67f, 0.01f, 0.97f, 0.12f);
            examButton = UiFactory.MakeButton("ExamButton", morningPanel, "승급시험 보기", 30, new Color(0.80f, 0.50f, 0.25f), StartExam);
            UiFactory.Stretch(examButton.GetComponent<RectTransform>(), 0.67f, 0.125f, 0.97f, 0.185f);
            examButtonText = examButton.GetComponentInChildren<Text>();
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

        // ---------- 알림(토스트) ----------

        static readonly Color ToastGood = new Color(0.18f, 0.50f, 0.32f, 0.96f);
        static readonly Color ToastBad = new Color(0.72f, 0.25f, 0.25f, 0.96f);
        Image toastPanel;
        Text toastText;
        float toastTimer;

        // 화면 위쪽에 잠깐 떴다 사라지는 알림. 시술 완료, 놓침, 잔소리 같은 중요한 일을 알려준다.
        void Toast(string message, bool good)
        {
            if (toastPanel == null) return;
            toastText.text = message;
            toastPanel.color = good ? ToastGood : ToastBad;
            Sfx.Play(good ? Sfx.Kind.Good : Sfx.Kind.Bad);
            toastPanel.gameObject.SetActive(true);
            toastTimer = 2.4f;
        }

        void UpdateToast()
        {
            if (toastPanel == null || toastTimer <= 0f) return;
            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f) toastPanel.gameObject.SetActive(false);
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
            corridorImage = corridor;
            Art.Apply(corridor, "corridor_tile", 0f, true);
            PlaceCenter(corridor.rectTransform, new Vector2(0f, 0f), new Vector2(1500f, 220f));

            // 휴게실
            Image room = UiFactory.Panel("RestRoom", mapArea, new Color(0.62f, 0.72f, 0.82f));
            bool restArt = Art.Apply(room, "room_rest");
            if (restArt) room.color = Color.white;
            PlaceCenter(room.rectTransform, RestPos, new Vector2(190f, 200f));
            Button roomButton = room.gameObject.AddComponent<Button>();
            roomButton.targetGraphic = room;
            roomButton.onClick.AddListener(StartRest);
            Text roomText = UiFactory.Label("RestRoomText", room.transform, "휴게실", 30, restArt ? TextDark : Color.white, TextAnchor.UpperCenter);
            roomText.rectTransform.offsetMin = new Vector2(0f, 0f);
            roomText.rectTransform.offsetMax = new Vector2(0f, -8f);

            // 재료실: 남은 개수가 보이고, 누르면 걸어간다
            Image supply = UiFactory.Panel("SupplyRoom", mapArea, new Color(0.70f, 0.80f, 0.66f));
            if (Art.Apply(supply, "room_supply")) supply.color = Color.white;
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

            // 시술 구역 (오른쪽): 샴푸실 / 시술석 (커트와 드라이는 선생님 자리)
            string[] zoneNames = { "샴푸실", "시술석" };
            Color[] zoneColors =
            {
                new Color(0.70f, 0.85f, 1.00f), new Color(1.00f, 0.88f, 0.75f)
            };
            float[] zoneY = { 215f, -50f };
            for (int z = 0; z < 2; z++)
            {
                Image zone = UiFactory.Panel("Zone_" + zoneNames[z], mapArea, zoneColors[z]);
                if (Art.Apply(zone, z == 0 ? "zone_wash" : "zone_seat")) zone.color = Color.white;
                PlaceCenter(zone.rectTransform, new Vector2(775f, zoneY[z]), new Vector2(240f, z == 1 ? 340f : 190f));
                Text zt = UiFactory.Label("ZoneText", zone.transform, zoneNames[z], 26, TextDark, TextAnchor.UpperCenter);
                zt.rectTransform.offsetMax = new Vector2(0f, -6f);
            }
            BuildChairs();

            // 디자이너 자리: 위 6명, 아래 6명
            for (int i = 0; i < designers.Length; i++)
            {
                int row = (i / 4) % 2;
                int col = i % 4;
                float sy = row == 0 ? 200f : -200f;
                Vector2 pos = new Vector2(-400f + col * 240f, sy);
                seats[designers[i]] = pos;

                // 거울 (벽쪽) + 손님 의자 + 의자에 앉은 손님, 디자이너는 의자 옆에 선다
                Image mirror = UiFactory.Panel("Mirror", mapArea, new Color(0.80f, 0.90f, 0.97f));
                Art.Apply(mirror, "seat_mirror");
                PlaceCenter(mirror.rectTransform, pos + new Vector2(0f, row == 0 ? 80f : -80f), new Vector2(34f, 104f));
                mirror.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                Image chair = UiFactory.Panel("Chair", mapArea, new Color(0.55f, 0.50f, 0.50f));
                if (Art.Apply(chair, "seat_chair")) chair.color = Color.white;
                PlaceCenter(chair.rectTransform, pos, new Vector2(112f, 112f));

                Image seated = Art.Figure("SeatedCustomer_" + designers[i].name, mapArea, new Color(0.96f, 0.78f, 0.66f), "customer_0");
                seated.raycastTarget = false;
                PlaceCenter(seated.rectTransform, pos, new Vector2(92f, 92f));
                seated.gameObject.SetActive(false);
                seatedCustomers[designers[i]] = seated;

                Color body = Color.HSVToRGB((i * 0.083f) % 1f, 0.45f, 0.95f);
                Image figure = Art.Figure("Designer_" + designers[i].name, mapArea, body, "designer_" + ((designers[i].id - 1) % 12));
                PlaceCenter(figure.rectTransform, pos + new Vector2(0f, row == 0 ? -68f : 68f), new Vector2(112f, 112f));

                Text name = UiFactory.Label("Name", mapArea, designers[i].name, 24, TextDark, TextAnchor.MiddleCenter);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                PlaceCenter(name.rectTransform, new Vector2(pos.x, row == 0 ? sy - 142f : sy + 142f), new Vector2(190f, 30f));
            }

            // 내 인턴
            Image me = Art.Figure("Player", mapArea, new Color(0.25f, 0.55f, 0.35f), "intern");
            playerImage = me;
            playerRt = me.rectTransform;
            PlaceCenter(playerRt, new Vector2(-480f, 0f), new Vector2(120f, 120f));
            // 그림이 있으면 유니폼 층에만 색을 입힌다(상점의 유니폼 색)
            Image uniform = UiFactory.Panel("Uniform", me.transform, Color.white);
            uniform.raycastTarget = false;
            if (Art.Apply(uniform, "intern_uniform"))
            {
                UiFactory.Stretch(uniform.rectTransform, 0f, 0f, 1f, 1f);
                uniform.preserveAspect = true;
                playerUniform = uniform;
            }
            else
            {
                Destroy(uniform.gameObject);
                UiFactory.Label("PlayerText", me.transform, "나", 28, Color.white, TextAnchor.MiddleCenter);
            }
            BuildNpcs();
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
            if (r.npcOnly) baseColor = new Color(0.80f, 0.84f, 0.90f);
            Image bg = UiFactory.Panel("Bubble_" + r.designer.name, mapArea, baseColor);
            Art.Apply(bg, "ui_bubble", 30f);
            r.background = bg;
            r.baseColor = baseColor;
            PlaceCenter(bg.rectTransform, r.useAnchor ? r.anchor : new Vector2(seat.x, seat.y + dir * 112f), new Vector2(150f, 100f));

            {
                Button button = bg.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() => OnCardClicked(r));
            }

            string title = r.isLocked ? "[잠김] " + r.label : r.label;
            if (r.isVip) title = "VIP " + title;
            else if (r.isReserved) title = "예약 " + title;
            Text body = UiFactory.Label("Body", bg.transform, title, 21, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(body.rectTransform, 0.04f, 0.28f, 0.96f, 0.98f);
            r.bodyText = body;
            r.titleBase = title;

            Image barBg = UiFactory.Panel("BarBg", bg.transform, new Color(0.25f, 0.25f, 0.30f));
            UiFactory.Stretch(barBg.rectTransform, 0.06f, 0.08f, 0.94f, 0.22f);
            Image fill = UiFactory.Panel("BarFill", barBg.transform, BarGreen);
            UiFactory.Stretch(fill.rectTransform, 0f, 0f, 1f, 1f);
            r.barFill = fill.rectTransform;
            r.barFillImage = fill;
            AssignWork(r);
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
