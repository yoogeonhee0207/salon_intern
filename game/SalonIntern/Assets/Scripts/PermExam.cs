using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // 펌 시험: 1) 휘어진 머리 빗기 2) 파지를 머리끝에 올리기 3) 롯드 놓기 4) 롯드 말기 5) 고무줄 놓기.
    // 시험은 다섯 단계를 모두 끝내야 합격. Lite 모드(평소 시술)는 짧고 실패 없이 완성도(0~100)만 돌려준다.
    public class PermExam : ExamBase
    {
        public static bool Lite;

        protected override string Title { get { return lite ? "펌  -  직접 해봐요!" : "펌 시험  -  빗질, 파지, 롯드, 말기, 고무줄"; } }
        protected override string Rule { get { return "1 휘어진 머리를 빗어 펴요  →  2 파지를 머리끝에 올려요  →  3 롯드를 놓아요\n4 롯드를 두피까지 말아 올려요  →  5 고무줄로 고정해요"; } }
        protected override float DimAlpha { get { return lite ? 0.88f : 0.97f; } }

        static readonly string[] StageNames = { "1단계 빗질", "2단계 파지 올리기", "3단계 롯드 놓기", "4단계 롯드 말기", "5단계 고무줄 놓기" };
        static readonly string[] StageTips =
        {
            "휘어진 머리를 위에서 아래로 쓸어서 곧게 빗어요",
            "아래의 흰 파지를 머리끝(동그란 표시)까지 끌어다 올려요",
            "아래의 롯드를 머리끝(파지 위)까지 끌어다 놓아요",
            "롯드를 두피(위쪽)까지 끌어올려 말아요",
            "말린 롯드를 눌러서 고무줄로 고정해요",
        };
        const int StageCount = 5;
        const int Rods = 4;
        const int Points = 12;
        const float TopY = 165f;    // 두피
        const float TipY = 15f;     // 머리끝
        const float PileY = -150f;  // 도구를 쌓아 둔 자리

        static readonly Color HairColor = new Color(0.25f, 0.17f, 0.13f);
        static readonly Color CurlColor = new Color(0.75f, 0.40f, 0.30f);
        static readonly Color PaperColor = new Color(0.98f, 0.98f, 0.95f);
        static readonly Color RodColor = new Color(0.60f, 0.62f, 0.70f);
        static readonly Color RolledColor = new Color(0.90f, 0.55f, 0.70f);

        bool lite;
        float timeLeft;
        int stage;
        DragPad pad;
        RectTransform box;
        Vector2 last;

        float[] StrandX = new float[Rods];

        // 1 빗질: 점마다 휘어짐 정도(1 -> 0)
        float[,] curl = new float[Rods, Points];
        Image[,] curlDots = new Image[Rods, Points];

        // 2~4 끌어다 놓기 공통
        readonly Vector2[] dragStart = new Vector2[Rods];
        readonly Vector2[] dragTarget = new Vector2[Rods];
        readonly bool[] dragDone = new bool[Rods];
        readonly RectTransform[] items = new RectTransform[Rods];
        int active = -1;

        // 5 고무줄
        readonly bool[] banded = new bool[Rods];

        protected override void Setup()
        {
            lite = Lite;
            Lite = false;
            timeLeft = lite ? 22f : 45f;
            for (int i = 0; i < Rods; i++) StrandX[i] = -450f + i * 300f;

            pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.95f, 0.90f, 0.82f));
            pad.onDown = OnDown;
            pad.onDrag = OnDrag;
            pad.onUp = () => { active = -1; };
            StartStage(0);
        }

        void StartStage(int s)
        {
            stage = s;
            if (box != null) Destroy(box.gameObject);
            box = UiFactory.Rect("Box", pad.transform);
            Place(box, Vector2.zero, new Vector2(1400f, 440f));
            active = -1;
            feedbackText.text = StageTips[s];
            SetCursor(CursorKind.Comb);

            Box(box, new Vector2(0f, TopY + 28f), new Vector2(1300f, 30f), new Color(0.96f, 0.80f, 0.68f)); // 두피
            if (s == 0) BuildComb();
            else if (s == 1) BuildDrag(false);
            else if (s == 2) BuildDrag(false);
            else if (s == 3) BuildDrag(false);
            else BuildBands();
            UpdateInfo();
        }

        void DrawStrands(bool papers, bool rods)
        {
            for (int i = 0; i < Rods; i++)
            {
                Box(box, new Vector2(StrandX[i], (TopY + TipY) / 2f), new Vector2(40f, TopY - TipY), HairColor);
                if (papers) Box(box, new Vector2(StrandX[i], TipY), new Vector2(64f, 36f), PaperColor);
                if (rods) Dot(box, new Vector2(StrandX[i], TipY), 54f, RodColor);
            }
        }

        // ---- 1 빗질 ----
        void BuildComb()
        {
            for (int i = 0; i < Rods; i++)
                for (int k = 0; k < Points; k++)
                {
                    curl[i, k] = 1f;
                    curlDots[i, k] = Dot(box, CurlPos(i, k), 22f, CurlColor);
                }
        }

        Vector2 CurlPos(int strand, int k)
        {
            float t = k / (float)(Points - 1);
            float y = TopY - t * (TopY - TipY);
            return new Vector2(StrandX[strand] + curl[strand, k] * 45f * Mathf.Sin(k * 1.2f + strand), y);
        }

        void Comb(Vector2 p)
        {
            float dist = (p - last).magnitude;
            last = p;
            for (int i = 0; i < Rods; i++)
                for (int k = 0; k < Points; k++)
                {
                    if ((curlDots[i, k].rectTransform.anchoredPosition - p).magnitude > 75f) continue;
                    curl[i, k] = Mathf.Max(0f, curl[i, k] - dist / 220f);
                    curlDots[i, k].rectTransform.anchoredPosition = CurlPos(i, k);
                    curlDots[i, k].color = Color.Lerp(HairColor, CurlColor, curl[i, k]);
                }
            if (CombRemaining() <= (lite ? 0.25f : 0.1f)) StartStage(1);
        }

        float CombRemaining()
        {
            float s = 0f;
            for (int i = 0; i < Rods; i++) for (int k = 0; k < Points; k++) s += curl[i, k];
            return s / (Rods * Points);
        }

        // ---- 2~4 끌어다 놓기 ----
        void BuildDrag(bool papersUnused)
        {
            for (int i = 0; i < Rods; i++) dragDone[i] = false;

            if (stage == 1)
            {
                DrawStrands(false, false);
                for (int i = 0; i < Rods; i++)
                {
                    dragStart[i] = new Vector2(StrandX[i], PileY);
                    dragTarget[i] = new Vector2(StrandX[i], TipY);
                    Dot(box, dragTarget[i], 56f, new Color(0.85f, 0.85f, 0.85f, 0.7f)); // 놓을 자리 표시
                    items[i] = Box(box, dragStart[i], new Vector2(64f, 36f), PaperColor).rectTransform;
                    Dot(box, dragStart[i] + new Vector2(0f, -34f), 14f, new Color(1f, 0.85f, 0.25f));
                }
            }
            else if (stage == 2)
            {
                DrawStrands(true, false);
                for (int i = 0; i < Rods; i++)
                {
                    dragStart[i] = new Vector2(StrandX[i], PileY);
                    dragTarget[i] = new Vector2(StrandX[i], TipY);
                    items[i] = Dot(box, dragStart[i], 54f, RodColor).rectTransform;
                    Dot(box, dragStart[i] + new Vector2(0f, -40f), 14f, new Color(1f, 0.85f, 0.25f));
                }
            }
            else
            {
                DrawStrands(true, false);
                for (int i = 0; i < Rods; i++)
                {
                    dragStart[i] = new Vector2(StrandX[i], TipY);
                    dragTarget[i] = new Vector2(StrandX[i], TopY);
                    Dot(box, dragTarget[i], 56f, new Color(0.85f, 0.85f, 0.85f, 0.7f));
                    items[i] = Dot(box, dragStart[i], 54f, RodColor).rectTransform;
                }
            }
        }

        void Drag(Vector2 p)
        {
            if (active < 0) return;
            Dot(box, p, 10f, new Color(0.95f, 0.85f, 0.40f));
            items[active].anchoredPosition = p;
            if ((p - dragTarget[active]).magnitude <= 55f)
            {
                dragDone[active] = true;
                items[active].anchoredPosition = dragTarget[active];
                if (stage == 3) items[active].GetComponent<Image>().color = RolledColor;
                active = -1;
                if (CountTrue(dragDone) >= Rods) StartStage(stage + 1);
            }
        }

        // ---- 5 고무줄 ----
        void BuildBands()
        {
            for (int i = 0; i < Rods; i++)
            {
                banded[i] = false;
                Box(box, new Vector2(StrandX[i], (TopY + TipY) / 2f), new Vector2(40f, TopY - TipY), HairColor);
                Box(box, new Vector2(StrandX[i], TipY), new Vector2(64f, 36f), PaperColor);
                Dot(box, new Vector2(StrandX[i], TopY), 70f, RolledColor);
            }
        }

        void Band(Vector2 p)
        {
            for (int i = 0; i < Rods; i++)
            {
                if (banded[i] || (p - new Vector2(StrandX[i], TopY)).magnitude > 65f) continue;
                banded[i] = true;
                Dot(box, new Vector2(StrandX[i], TopY), 34f, new Color(1f, 0.85f, 0.25f)); // 고무줄
                if (CountTrue(banded) >= Rods) Finish(true);
            }
        }

        // ---- 입력 ----
        void OnDown(Vector2 p)
        {
            if (finished) return;
            last = p;
            if (stage >= 1 && stage <= 3)
            {
                active = -1;
                for (int i = 0; i < Rods; i++)
                    if (!dragDone[i] && (p - dragStart[i]).magnitude <= 75f) active = i;
                if (active < 0) feedbackText.text = StageTips[stage];
            }
            else if (stage == 4) Band(p);
        }

        void OnDrag(Vector2 p)
        {
            if (finished) return;
            if (stage == 0) Comb(p);
            else if (stage <= 3) Drag(p);
        }

        static int CountTrue(bool[] a)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (a[i]) n++;
            return n;
        }

        int Percent()
        {
            float part;
            if (stage == 0) part = Mathf.Clamp01(1f - CombRemaining());
            else if (stage <= 3) part = CountTrue(dragDone) / (float)Rods;
            else part = CountTrue(banded) / (float)Rods;
            return Mathf.Clamp(Mathf.RoundToInt(100f * (stage + part) / StageCount), 0, 100);
        }

        void Finish(bool allDone)
        {
            if (finished) return;
            if (lite)
            {
                int pct = allDone ? 100 : Percent();
                string grade = pct >= 85 ? "S" : (pct >= 60 ? "A" : (pct >= 30 ? "B" : "C"));
                End(true, pct, 100, 0, grade + "등급!  (" + pct + "%)");
            }
            else
            {
                End(allDone, allDone ? StageCount : stage, StageCount, StageCount, allDone ? "예쁘게 말렸어요!" : "시간이 다 됐어요...");
            }
        }

        protected override void Tick()
        {
            timeLeft -= Time.deltaTime;
            UpdateInfo();
            if (timeLeft <= 0f) Finish(false);
        }

        void UpdateInfo()
        {
            infoText.text = StageNames[stage] + "      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
