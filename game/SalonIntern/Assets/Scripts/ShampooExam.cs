using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // Lv.1 샴푸 시험: 물 온도를 맞추면서 두피 구역을 문질러 거품을 낸다.
    public class ShampooExam : ExamBase
    {
        protected override string Title { get { return "샴푸 시험  -  두피 거품 내기"; } }
        protected override string Rule
        {
            get { return "손가락으로 두피 구역을 문질러 거품을 내요. 물 온도가 초록 구간일 때만 거품이 나요!\n온도는 계속 변해서 '차갑게 / 뜨겁게' 버튼으로 맞춰야 해요."; }
        }

        const int Cols = 6;
        const int Rows = 3;
        const float TimeLimit = 25f;
        const int Needed = 16;

        readonly Image[] cells = new Image[Cols * Rows];
        readonly Vector2[] centers = new Vector2[Cols * Rows];
        readonly float[] foam = new float[Cols * Rows];

        RectTransform tempMarker;
        float temp = 0.5f;
        float drift;
        float driftTimer;
        float timeLeft = TimeLimit;
        Vector2 last;

        protected override void Setup()
        {
            DragPad pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.95f, 0.90f, 0.82f));
            pad.onDown = p => { last = p; };
            pad.onDrag = Scrub;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int i = r * Cols + c;
                    centers[i] = new Vector2((c - (Cols - 1) / 2f) * 230f, ((Rows - 1) / 2f - r) * 140f);
                    cells[i] = Box(pad.transform, centers[i], new Vector2(215f, 125f), new Color(0.80f, 0.68f, 0.55f));
                }
            }

            // 물 온도 막대: 가운데 초록 구간이 적당한 온도
            Image bar = Box(self, new Vector2(0f, 330f), new Vector2(900f, 34f), new Color(0.30f, 0.30f, 0.38f));
            Image zone = UiFactory.Panel("Zone", bar.transform, new Color(0.30f, 0.75f, 0.45f));
            zone.raycastTarget = false;
            UiFactory.Stretch(zone.rectTransform, 0.35f, 0f, 0.65f, 1f);
            Image marker = UiFactory.Panel("Marker", bar.transform, new Color(1f, 0.85f, 0.25f));
            marker.raycastTarget = false;
            tempMarker = marker.rectTransform;
            LabelAt("차갑게", new Vector2(-490f, 330f), new Vector2(120f, 34f), 24, new Color(0.7f, 0.85f, 1f));
            LabelAt("뜨겁게", new Vector2(490f, 330f), new Vector2(120f, 34f), 24, new Color(1f, 0.75f, 0.65f));

            MakeButtonAt("차갑게", new Vector2(-600f, -345f), new Vector2(300f, 90f), 36, new Color(0.30f, 0.55f, 0.80f), () => { temp = Mathf.Clamp01(temp - 0.2f); });
            MakeButtonAt("뜨겁게", new Vector2(600f, -345f), new Vector2(300f, 90f), 36, new Color(0.85f, 0.45f, 0.30f), () => { temp = Mathf.Clamp01(temp + 0.2f); });

            UpdateInfo();
        }

        protected override void Tick()
        {
            timeLeft -= Time.deltaTime;

            driftTimer -= Time.deltaTime;
            if (driftTimer <= 0f)
            {
                drift = Random.Range(-0.25f, 0.25f);
                driftTimer = Random.Range(0.6f, 1.4f);
            }
            temp = Mathf.Clamp01(temp + drift * Time.deltaTime);
            UiFactory.Stretch(tempMarker, temp - 0.008f, -0.15f, temp + 0.008f, 1.15f);

            UpdateInfo();
            if (timeLeft <= 0f)
            {
                int done = CountDone();
                End(done >= Needed, done, cells.Length, Needed, done >= Needed ? "시간 안에 거품을 냈어요!" : "시간이 다 됐어요...");
            }
        }

        void Scrub(Vector2 p)
        {
            if (finished) return;
            float dist = (p - last).magnitude;
            last = p;

            if (Mathf.Abs(temp - 0.5f) > 0.15f)
            {
                feedbackText.text = "물 온도가 안 맞아요!";
                return;
            }
            feedbackText.text = "";

            for (int i = 0; i < cells.Length; i++)
            {
                if ((centers[i] - p).magnitude > 120f) continue;
                foam[i] = Mathf.Min(1f, foam[i] + dist / 600f);
                cells[i].color = Color.Lerp(new Color(0.80f, 0.68f, 0.55f), new Color(0.95f, 0.98f, 1f), foam[i]);
            }

            if (CountDone() >= cells.Length)
                End(true, cells.Length, cells.Length, Needed, "거품이 가득! 깨끗하게 감았어요!");
        }

        int CountDone()
        {
            int n = 0;
            for (int i = 0; i < foam.Length; i++) if (foam[i] >= 1f) n++;
            return n;
        }

        void UpdateInfo()
        {
            infoText.text = "거품 " + CountDone() + " / " + cells.Length + " (" + Needed + " 이상 필요)      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
