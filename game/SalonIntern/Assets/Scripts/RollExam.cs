using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // Lv.2 펌 시험: 머리 가닥마다 왼쪽에서 오른쪽으로 곧게 드래그해서 롤을 만다.
    public class RollExam : ExamBase
    {
        protected override string Title { get { return "펌 시험  -  롤 말기"; } }
        protected override string Rule
        {
            get { return "머리 가닥의 왼쪽 끝에서 눌러서 오른쪽 끝까지 곧게 드래그해요.\n줄에서 벗어나면 롤이 풀려요!"; }
        }

        const int Strands = 6;
        const float TimeLimit = 30f;
        const int Needed = 5;
        const float Left = -550f;
        const float Width = 1100f;

        readonly RectTransform[] fills = new RectTransform[Strands];
        readonly float[] ys = new float[Strands];
        readonly float[] progress = new float[Strands];
        readonly bool[] done = new bool[Strands];

        int active = -1;
        int mistakes;
        float timeLeft = TimeLimit;

        protected override void Setup()
        {
            DragPad pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.95f, 0.90f, 0.82f));
            pad.onDown = OnDown;
            pad.onDrag = OnDrag;
            pad.onUp = OnUp;

            for (int i = 0; i < Strands; i++)
            {
                ys[i] = (2.5f - i) * 70f;
                Image band = Box(pad.transform, new Vector2(0f, ys[i]), new Vector2(Width, 50f), new Color(0.78f, 0.62f, 0.48f));
                Image fill = UiFactory.Panel("Fill", band.transform, new Color(0.85f, 0.35f, 0.55f));
                fill.raycastTarget = false;
                UiFactory.Stretch(fill.rectTransform, 0f, 0f, 0f, 1f);
                fills[i] = fill.rectTransform;
                Dot(pad.transform, new Vector2(Left - 30f, ys[i]), 40f, new Color(1f, 0.85f, 0.25f)); // 시작 표시
            }
            UpdateInfo();
        }

        protected override void Tick()
        {
            timeLeft -= Time.deltaTime;
            UpdateInfo();
            if (timeLeft <= 0f)
            {
                int n = Count();
                End(n >= Needed, n, Strands, Needed, n >= Needed ? "시간 안에 롤을 말았어요!" : "시간이 다 됐어요...");
            }
        }

        void OnDown(Vector2 p)
        {
            if (finished) return;
            active = -1;
            for (int i = 0; i < Strands; i++)
            {
                if (done[i]) continue;
                if (Mathf.Abs(p.y - ys[i]) <= 35f && p.x < Left + 120f) { active = i; break; }
            }
            if (active >= 0) progress[active] = 0f;
        }

        void OnDrag(Vector2 p)
        {
            if (finished || active < 0) return;

            if (Mathf.Abs(p.y - ys[active]) > 45f)
            {
                mistakes++;
                feedbackText.text = "줄에서 벗어나 롤이 풀렸어요!";
                ResetActive();
                return;
            }

            feedbackText.text = "";
            progress[active] = Mathf.Max(progress[active], Mathf.Clamp01((p.x - Left) / Width));
            UiFactory.Stretch(fills[active], 0f, 0f, progress[active], 1f);

            if (progress[active] >= 0.97f)
            {
                done[active] = true;
                UiFactory.Stretch(fills[active], 0f, 0f, 1f, 1f);
                active = -1;
                feedbackText.text = "한 줄 완성!";
                if (Count() >= Strands) End(true, Strands, Strands, Needed, "모든 롤을 말았어요!");
            }
        }

        void OnUp()
        {
            if (finished) return;
            if (active >= 0 && !done[active]) ResetActive();
            active = -1;
        }

        void ResetActive()
        {
            if (active < 0) return;
            progress[active] = 0f;
            UiFactory.Stretch(fills[active], 0f, 0f, 0f, 1f);
            active = -1;
        }

        int Count()
        {
            int n = 0;
            for (int i = 0; i < Strands; i++) if (done[i]) n++;
            return n;
        }

        void UpdateInfo()
        {
            infoText.text = "완성한 롤 " + Count() + " / " + Strands + " (" + Needed + " 이상 필요)      풀린 횟수 " + mistakes + "      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
