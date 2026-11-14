using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // Lv.4 드라이 시험: 젖은 머리를 움직이며 말린다. 한곳에 오래 두면 과열된다.
    public class DryExam : ExamBase
    {
        protected override string Title { get { return "드라이 시험  -  골고루 말리기"; } }
        protected override string Rule
        {
            get { return "젖은 머리를 손가락으로 쓸듯이 움직이며 말려요.\n한곳에 오래 멈춰 있으면 과열돼요! (과열 " + AllowedDamage + "번까지 괜찮아요)"; }
        }

        const int Cols = 6;
        const int Rows = 4;
        const float TimeLimit = 30f;
        const int AllowedDamage = 2;
        const float Radius = 95f;
        const float SlowSpeed = 140f; // 이보다 느리면 열이 오른다

        readonly Image[] cells = new Image[Cols * Rows];
        readonly Vector2[] centers = new Vector2[Cols * Rows];
        readonly float[] wet = new float[Cols * Rows];

        Image dryer;
        RectTransform heatFill;
        bool dragging;
        Vector2 cur;
        Vector2 prev;
        float heat;
        int damage;
        float timeLeft = TimeLimit;

        static readonly Color WetColor = new Color(0.35f, 0.55f, 0.85f);
        static readonly Color DryColor = new Color(0.92f, 0.82f, 0.62f);

        protected override void Setup()
        {
            DragPad pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.95f, 0.90f, 0.82f));
            pad.onDown = p => { dragging = true; cur = p; prev = p; dryer.gameObject.SetActive(true); };
            pad.onDrag = p => { cur = p; };
            pad.onUp = () => { dragging = false; dryer.gameObject.SetActive(false); };

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int i = r * Cols + c;
                    wet[i] = 1f;
                    centers[i] = new Vector2((c - (Cols - 1) / 2f) * 230f, ((Rows - 1) / 2f - r) * 110f);
                    cells[i] = Box(pad.transform, centers[i], new Vector2(218f, 100f), WetColor);
                }
            }

            dryer = Dot(pad.transform, Vector2.zero, Radius * 2f, new Color(1f, 0.6f, 0.3f, 0.45f));
            dryer.gameObject.SetActive(false);

            // 열 막대
            Image bar = Box(self, new Vector2(0f, 330f), new Vector2(700f, 34f), new Color(0.30f, 0.30f, 0.38f));
            Image fill = UiFactory.Panel("Heat", bar.transform, new Color(0.95f, 0.45f, 0.25f));
            fill.raycastTarget = false;
            UiFactory.Stretch(fill.rectTransform, 0f, 0f, 0f, 1f);
            heatFill = fill.rectTransform;
            LabelAt("열", new Vector2(-400f, 330f), new Vector2(60f, 34f), 28, new Color(1f, 0.75f, 0.65f));

            UpdateInfo();
        }

        protected override void Tick()
        {
            float dt = Time.deltaTime;
            timeLeft -= dt;

            if (dragging && !finished)
            {
                dryer.rectTransform.anchoredPosition = cur;
                float speed = (cur - prev).magnitude / Mathf.Max(0.0001f, dt);
                prev = cur;

                for (int i = 0; i < cells.Length; i++)
                {
                    if ((centers[i] - cur).magnitude > Radius) continue;
                    wet[i] = Mathf.Max(0f, wet[i] - dt * 0.7f);
                    cells[i].color = Color.Lerp(DryColor, WetColor, wet[i]);
                }

                if (speed < SlowSpeed) heat += 70f * dt;
                else heat = Mathf.Max(0f, heat - 40f * dt);

                if (heat >= 100f)
                {
                    heat = 0f;
                    damage++;
                    feedbackText.text = "너무 오래 한곳에 댔어요! 머리카락이 상했어요.  (" + damage + "/" + AllowedDamage + ")";
                    if (damage > AllowedDamage)
                    {
                        End(false, CountDry(), cells.Length, cells.Length, "머리카락이 너무 상했어요...");
                        return;
                    }
                }
            }
            else
            {
                heat = Mathf.Max(0f, heat - 30f * dt);
            }

            UiFactory.Stretch(heatFill, 0f, 0f, Mathf.Clamp01(heat / 100f), 1f);
            UpdateInfo();

            if (!finished && CountDry() >= cells.Length)
                End(true, cells.Length, cells.Length, cells.Length, "골고루 잘 말랐어요!");
            else if (!finished && timeLeft <= 0f)
                End(false, CountDry(), cells.Length, cells.Length, "시간이 다 됐어요...");
        }

        int CountDry()
        {
            int n = 0;
            for (int i = 0; i < wet.Length; i++) if (wet[i] <= 0f) n++;
            return n;
        }

        void UpdateInfo()
        {
            infoText.text = "마른 구역 " + CountDry() + " / " + cells.Length + "      과열 " + damage + " / " + AllowedDamage + "      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
