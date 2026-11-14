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
            public float timeLimit;
            public float remaining;

            public Image background;
            public RectTransform barFill;
            public Image barFillImage;
        }

        DesignerData[] designers;
        readonly List<Request> active = new List<Request>();

        RectTransform canvasRoot;
        RectTransform cardArea;
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

        // 오늘의 예약표: 세로줄 = 디자이너, 가로줄 = 시간, 칸 = 시술 (지금은 표시만 한다)
        static readonly Color[] ProcedureColors =
        {
            new Color(0.70f, 0.85f, 1.00f), // 샴푸
            new Color(1.00f, 0.80f, 0.90f), // 롤
            new Color(1.00f, 0.85f, 0.65f), // 염색
            new Color(0.75f, 0.92f, 0.75f), // 드라이
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

        // ---------- 요청 만들기 ----------

        void TrySpawn()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                DesignerData d = designers[Random.Range(0, designers.Length)];
                if (HasRequest(d)) continue; // 디자이너 한 명당 요청은 한 장

                bool procedure = Random.value >= LevelConfig.ChoreRatio[level];
                string label;
                bool locked = false;

                if (!procedure)
                {
                    label = LevelConfig.ChoreNames[Random.Range(0, LevelConfig.ChoreNames.Length)];
                }
                else
                {
                    int p = PickProcedure(d);
                    label = LevelConfig.ProcedureNames[p];
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
                    timeLimit = LevelConfig.TimeLimit[level] * Mathf.Max(0.3f, d.patienceMultiplier),
                };
                r.remaining = r.timeLimit;

                CreateCard(r);
                active.Add(r);
                return;
            }
        }

        bool HasRequest(DesignerData d)
        {
            foreach (Request r in active)
            {
                if (r.designer == d) return true;
            }
            return false;
        }

        // 디자이너의 시술 확률(샴푸/롤/염색/드라이)로 하나 뽑기
        static int PickProcedure(DesignerData d)
        {
            int[] weights = { d.shampoo, d.roll, d.dye, d.dry };
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

        void OnCardClicked(Request r)
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
                score += r.isProcedure ? 20 : 10;
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

            Say(r.designer.name + " 선배의 바닥 청소 " + grade + "등급! ("
                + result.cleaned + "/" + result.total + ", 점수 +" + points + ")");

            if (active.Contains(r)) RemoveRequest(r);
            RefreshTop();
        }

        void Expire(Request r)
        {
            missed++;
            Say(r.designer.name + " 선배가 " + r.label + "을(를) 직접 처리했어요");
            RemoveRequest(r);
            RefreshTop();
        }

        void RemoveRequest(Request r)
        {
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

            // 카드 영역 (한 줄에 최대 5장)
            cardArea = UiFactory.Rect("CardArea", root);
            UiFactory.Stretch(cardArea, 0.03f, 0.22f, 0.97f, 0.86f);
            GridLayoutGroup grid = cardArea.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(340f, 250f);
            grid.spacing = new Vector2(20f, 20f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

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
                "※ 예약은 아직 보여주기만 해요.\n다음 단계에서 실제 요청과 연결돼요.", 24, TextDark, TextAnchor.MiddleLeft);
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

        void CreateCard(Request r)
        {
            Image bg = UiFactory.Panel("Card_" + r.designer.name, cardArea, r.isLocked ? CardLocked : CardNormal);
            r.background = bg;

            Button button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => OnCardClicked(r));

            // 디자이너 이름 + 특기
            Text header = UiFactory.Label("Header", bg.transform,
                r.designer.name + "\n" + r.designer.specialty, 28, TextDark, TextAnchor.UpperCenter);
            UiFactory.Stretch(header.rectTransform, 0.04f, 0.60f, 0.96f, 0.97f);

            // 요청 내용
            string title = r.isLocked ? "[잠김] " + r.label : r.label;
            Text body = UiFactory.Label("Body", bg.transform, title, 40, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(body.rectTransform, 0.04f, 0.28f, 0.96f, 0.60f);

            // 남은 시간 막대
            Image barBg = UiFactory.Panel("BarBg", bg.transform, new Color(0.25f, 0.25f, 0.30f));
            UiFactory.Stretch(barBg.rectTransform, 0.06f, 0.16f, 0.94f, 0.25f);
            Image fill = UiFactory.Panel("BarFill", barBg.transform, BarGreen);
            UiFactory.Stretch(fill.rectTransform, 0f, 0f, 1f, 1f);
            r.barFill = fill.rectTransform;
            r.barFillImage = fill;

            // 안내 글자
            Text hint = UiFactory.Label("Hint", bg.transform,
                r.isLocked ? "눌러서 견학 (XP +1)"
                    : (r.label == "바닥 청소" ? "눌러서 청소 시작 (미니게임)" : "눌러서 처리"),
                24, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(hint.rectTransform, 0f, 0f, 1f, 0.15f);
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
