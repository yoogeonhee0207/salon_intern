using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SalonIntern
{
    // 요청 카드 시스템 프로토타입.
    // 씬에 아무것도 놓지 않아도 Play를 누르면 이 스크립트가 자동으로 실행된다.
    public class GameController : MonoBehaviour
    {
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

        RectTransform cardArea;
        Text topText;
        Text logText;

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
            Say(designers.Length == 0
                ? "디자이너 데이터를 불러오지 못했어요. Console 창을 확인해 주세요."
                : "영업 시작! 카드를 눌러 요청을 처리하세요.");
            RefreshTop();
        }

        void Update()
        {
            if (designers == null || designers.Length == 0) return;

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
            if (!active.Contains(r)) return;

            if (r.isLocked)
            {
                AddXp(1);
                Say(r.designer.name + " 선배의 " + r.label + "을(를) 견학했어요 (XP +1)");
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
            level = Mathf.Clamp(level + delta, 0, LevelConfig.MaxLevel);
            xp = 0;
            Say("[테스트] Lv." + level + " " + LevelConfig.Names[level] + " 로 변경");
            RefreshTop();
        }

        // ---------- 화면 ----------

        void RefreshTop()
        {
            string xpText = level >= LevelConfig.MaxLevel
                ? "MAX"
                : xp + "/" + LevelConfig.XpToNext(level);

            topText.text = "Lv." + level + " " + LevelConfig.Names[level]
                + "   XP " + xpText
                + "   점수 " + score
                + "   완료 " + done
                + "   놓침 " + missed
                + "   (동시 " + LevelConfig.MaxConcurrent[level] + "건)";
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

            // 배경
            Image bg = UiFactory.Panel("Background", root, new Color(0.93f, 0.90f, 0.86f));
            UiFactory.Stretch(bg.rectTransform, 0f, 0f, 1f, 1f);

            // 상단 바
            Image top = UiFactory.Panel("TopBar", root, new Color(0.20f, 0.20f, 0.26f));
            UiFactory.Stretch(top.rectTransform, 0f, 0.91f, 1f, 1f);
            topText = UiFactory.Label("TopText", top.transform, "", 34, Color.white, TextAnchor.MiddleLeft);
            topText.rectTransform.offsetMin = new Vector2(30f, 0f);
            topText.rectTransform.offsetMax = new Vector2(-330f, 0f);

            // 테스트용 레벨 버튼
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
                r.isLocked ? "눌러서 견학 (XP +1)" : "눌러서 처리", 24, TextDark, TextAnchor.MiddleCenter);
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
