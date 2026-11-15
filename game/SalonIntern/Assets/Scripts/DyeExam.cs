using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // Lv.3 염색 시험: 1) 목표 색에 맞게 염색약을 배합하고 2) 뿌리부터 빠짐없이 도포한다.
    public class DyeExam : ExamBase
    {
        protected override string Title { get { return "염색 시험  -  배합과 도포"; } }
        protected override string Rule
        {
            get { return "1단계: 빨강·노랑·파랑 양을 조절해 목표 색에 맞춰요. (한 칸 차이는 괜찮아요)\n2단계: 두피 쪽(위)부터 아래로 문질러 염색약을 발라요. (80% 이상이면 합격)"; }
        }

        static readonly string[] ChannelNames = { "빨강", "노랑", "파랑" };
        static readonly Color[] ChannelColors =
        {
            new Color(1.00f, 0.25f, 0.25f), new Color(1.00f, 0.90f, 0.25f), new Color(0.25f, 0.35f, 1.00f)
        };

        const int Cols = 8;
        const int Rows = 4;
        const float TimeLimit = 40f;
        const int AllowedMistakes = 4;

        // 1단계
        readonly int[] values = new int[3];
        readonly int[] target = new int[3];
        readonly Text[] valueTexts = new Text[3];
        RectTransform stageA;
        Image currentSwatch;
        int mistakes;

        // 2단계
        DragPad pad;
        readonly Image[] cells = new Image[Cols * Rows];
        readonly Vector2[] centers = new Vector2[Cols * Rows];
        readonly float[] applied = new float[Cols * Rows];
        Color targetColor;
        bool stageB;
        float timeLeft = TimeLimit;
        Vector2 last;

        protected override void Setup()
        {
            // 목표 색 뽑기
            // 쉽게: 색은 최대 두 가지만 섞고 양도 0~3으로 제한
            do
            {
                for (int i = 0; i < 3; i++) target[i] = Random.Range(0, 4);
            } while (target[0] + target[1] + target[2] < 2 || (target[0] > 0 ? 1 : 0) + (target[1] > 0 ? 1 : 0) + (target[2] > 0 ? 1 : 0) > 2);
            targetColor = Mix(target);

            stageA = UiFactory.Rect("StageA", self);
            UiFactory.Stretch(stageA, 0f, 0f, 1f, 1f);

            LabelAt("목표 색", new Vector2(-300f, 290f), new Vector2(300f, 50f), 34, Color.white).transform.SetParent(stageA, false);
            Image targetSwatch = Box(stageA, new Vector2(-300f, 190f), new Vector2(260f, 130f), targetColor, "ui_bubble", 30f);
            LabelAt("내가 섞은 색", new Vector2(300f, 290f), new Vector2(300f, 50f), 34, Color.white).transform.SetParent(stageA, false);
            currentSwatch = Box(stageA, new Vector2(300f, 190f), new Vector2(260f, 130f), Mix(values), "ui_bubble", 30f);

            for (int i = 0; i < 3; i++)
            {
                int ch = i;
                float y = 60f - i * 95f;
                Text name = LabelAt(ChannelNames[i], new Vector2(-300f, y), new Vector2(200f, 70f), 40, ChannelColors[i]);
                name.transform.SetParent(stageA, false);
                Button minus = MakeButtonAt("-", new Vector2(-110f, y), new Vector2(100f, 70f), 46, new Color(0.45f, 0.45f, 0.55f), () => Change(ch, -1));
                minus.transform.SetParent(stageA, false);
                valueTexts[i] = LabelAt("0", new Vector2(10f, y), new Vector2(120f, 70f), 46, Color.white);
                valueTexts[i].transform.SetParent(stageA, false);
                Button plus = MakeButtonAt("+", new Vector2(130f, y), new Vector2(100f, 70f), 46, new Color(0.30f, 0.60f, 0.45f), () => Change(ch, 1));
                plus.transform.SetParent(stageA, false);
            }

            Button confirm = MakeButtonAt("색 확인", new Vector2(420f, -100f), new Vector2(360f, 110f), 44, new Color(0.80f, 0.50f, 0.25f), Confirm);
            confirm.transform.SetParent(stageA, false);

            // 2단계 도포판 (처음엔 숨김)
            pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.95f, 0.90f, 0.82f));
            pad.onDown = p => { last = p; };
            pad.onDrag = Apply;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int i = r * Cols + c;
                    centers[i] = new Vector2((c - (Cols - 1) / 2f) * 170f, ((Rows - 1) / 2f - r) * 110f);
                    cells[i] = Box(pad.transform, centers[i], new Vector2(160f, 100f), new Color(0.88f, 0.84f, 0.78f), "hair_block");
                }
            }
            LabelAt("▲ 두피(뿌리)", new Vector2(0f, 250f), new Vector2(400f, 40f), 28, new Color(0.85f, 0.85f, 0.9f)).transform.SetParent(pad.transform, false);
            pad.gameObject.SetActive(false);

            UpdateInfo();
        }

        Color Mix(int[] v)
        {
            float total = v[0] + v[1] + v[2];
            if (total <= 0f) return new Color(0.5f, 0.5f, 0.5f);
            Color c = Color.black;
            for (int i = 0; i < 3; i++) c += ChannelColors[i] * (v[i] / total);
            c.a = 1f;
            return c;
        }

        void Change(int ch, int delta)
        {
            if (stageB || finished) return;
            values[ch] = Mathf.Clamp(values[ch] + delta, 0, 6);
            valueTexts[ch].text = values[ch].ToString();
            currentSwatch.color = Mix(values);
        }

        void Confirm()
        {
            if (stageB || finished) return;

            int diff = 0;
            int worst = 0;
            int worstDiff = -1;
            for (int i = 0; i < 3; i++)
            {
                int d = Mathf.Abs(values[i] - target[i]);
                diff += d;
                if (d > worstDiff) { worstDiff = d; worst = i; }
            }

            if (diff <= 1) // 한 칸 차이까지는 비슷한 색으로 인정
            {
                StartStageB("색이 잘 나왔어요! 이제 도포해요.");
                return;
            }

            mistakes++;
            if (mistakes > AllowedMistakes)
            {
                End(false, 0, 2, 2, "배합에 계속 실패했어요...");
                return;
            }
            string hint = values[worst] < target[worst] ? ChannelNames[worst] + " 더 넣어요 (+)" : ChannelNames[worst] + " 덜 넣어요 (-)";
            feedbackText.text = "아직 달라요: " + hint + "  (기회 " + (AllowedMistakes - mistakes + 1) + "번 남음)";
        }

        void StartStageB(string message)
        {
            stageB = true;
            stageA.gameObject.SetActive(false);
            pad.gameObject.SetActive(true);
            SetCursor(CursorKind.Brush);
            feedbackText.text = message;
        }

        protected override void Tick()
        {
            if (!stageB) { UpdateInfo(); return; }

            timeLeft -= Time.deltaTime;
            UpdateInfo();
            if (timeLeft <= 0f)
            {
                int n = Count();
                int need = Needed();
                End(n >= need, n, cells.Length, need, n >= need ? "시간 안에 도포했어요!" : "시간이 다 됐어요...");
            }
        }

        int Needed() { return Mathf.CeilToInt(cells.Length * 0.8f); }

        // 위쪽 줄(뿌리)부터: 윗줄이 어느 정도 발려야 아랫줄에 바를 수 있다
        void Apply(Vector2 p)
        {
            if (!stageB || finished) return;
            float dist = (p - last).magnitude;
            last = p;

            for (int i = 0; i < cells.Length; i++)
            {
                if ((centers[i] - p).magnitude > 100f) continue;

                int row = i / Cols;
                if (row > 0 && RowAverage(row - 1) < 0.35f)
                {
                    feedbackText.text = "뿌리(위)부터 발라야 해요!";
                    continue;
                }
                feedbackText.text = "";
                applied[i] = Mathf.Min(1f, applied[i] + dist / 280f);
                cells[i].color = Color.Lerp(new Color(0.88f, 0.84f, 0.78f), targetColor, applied[i]);
            }

            if (Count() >= cells.Length) End(true, cells.Length, cells.Length, Needed(), "빠짐없이 발랐어요!");
        }

        float RowAverage(int row)
        {
            float s = 0f;
            for (int c = 0; c < Cols; c++) s += applied[row * Cols + c];
            return s / Cols;
        }

        int Count()
        {
            int n = 0;
            for (int i = 0; i < applied.Length; i++) if (applied[i] >= 1f) n++;
            return n;
        }

        void UpdateInfo()
        {
            if (!stageB)
                infoText.text = "1단계: 배합      남은 기회 " + (AllowedMistakes - mistakes + 1) + "번";
            else
                infoText.text = "2단계: 도포 " + Count() + " / " + cells.Length + " (" + Needed() + " 이상 필요)      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
