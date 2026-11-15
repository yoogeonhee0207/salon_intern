using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // Lv.5 커트 시험: 가이드 선을 따라 가위(손가락)를 끝까지 움직인다.
    public class CutExam : ExamBase
    {
        protected override string Title { get { return "커트 시험  -  가이드 선 따라 자르기"; } }
        protected override string Rule
        {
            get { return "왼쪽 시작점(노란 점)에서 눌러서, 점선을 따라 오른쪽 끝까지 드래그해요.\n선에서 벗어나면 빨간 점이 찍혀요. 정확도 75% 이상이면 합격!"; }
        }

        const int PointCount = 57;
        const float Tolerance = 55f;
        const float TimeLimit = 30f;
        const int AllowedRestarts = 2;

        readonly Vector2[] guide = new Vector2[PointCount];
        readonly List<Image> trail = new List<Image>();
        DragPad pad;
        bool drawing;
        int reached;
        int samples;
        int offSamples;
        int restarts;
        Vector2 lastDot;
        float timeLeft = TimeLimit;

        protected override void Setup()
        {
            pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.95f, 0.90f, 0.82f));
            pad.onDown = OnDown;
            pad.onDrag = OnDrag;
            pad.onUp = OnUp;

            for (int i = 0; i < PointCount; i++)
            {
                float x = -560f + i * 20f;
                guide[i] = new Vector2(x, 110f * Mathf.Sin(x / 150f));
                Dot(pad.transform, guide[i], i == 0 || i == PointCount - 1 ? 38f : 16f,
                    i == 0 ? new Color(1f, 0.85f, 0.25f) : (i == PointCount - 1 ? new Color(0.30f, 0.75f, 0.45f) : new Color(0.45f, 0.40f, 0.35f)), "bead");
            }
            SetCursor(CursorKind.Scissors);
            UpdateInfo();
        }

        protected override void Tick()
        {
            timeLeft -= Time.deltaTime;
            UpdateInfo();
            if (timeLeft <= 0f) End(false, 0, 100, 75, "시간이 다 됐어요...");
        }

        void OnDown(Vector2 p)
        {
            if (finished) return;
            Sfx.Play(Sfx.Kind.Snip);
            ClearTrail();
            if ((p - guide[0]).magnitude <= 90f)
            {
                drawing = true;
                reached = 0;
                samples = 0;
                offSamples = 0;
                lastDot = p;
                feedbackText.text = "";
            }
            else
            {
                drawing = false;
                feedbackText.text = "노란 시작점에서 시작해요!";
            }
        }

        void OnDrag(Vector2 p)
        {
            if (finished || !drawing) return;

            // 가장 가까운 가이드 점 찾기
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < PointCount; i++)
            {
                float d = (guide[i] - p).magnitude;
                if (d < best) { best = d; nearest = i; }
            }

            bool off = best > Tolerance || nearest > reached + 6; // 선에서 멀거나 건너뛰면 벗어난 것
            samples++;
            if (off) offSamples++;
            else if (nearest > reached) reached = nearest;

            if ((p - lastDot).magnitude > 14f)
            {
                lastDot = p;
                trail.Add(Dot(pad.transform, p, 18f, off ? new Color(0.90f, 0.30f, 0.30f) : new Color(0.30f, 0.75f, 0.45f), "bead"));
            }

            if (reached >= PointCount - 2)
            {
                drawing = false;
                float accuracy = 1f - (float)offSamples / Mathf.Max(1, samples);
                int pct = Mathf.RoundToInt(accuracy * 100f);
                bool pass = pct >= 75;
                End(pass, pct, 100, 75, pass ? "정확도 " + pct + "%  깔끔하게 잘랐어요!" : "정확도 " + pct + "%  선에서 많이 벗어났어요...");
            }
            UpdateInfo();
        }

        void OnUp()
        {
            if (finished || !drawing) return;
            drawing = false;
            restarts++;
            if (restarts > AllowedRestarts)
            {
                End(false, 0, 100, 75, "끝까지 가지 못했어요...");
                return;
            }
            feedbackText.text = "끝까지 가야 해요! 다시 시작점에서 해요.  (기회 " + (AllowedRestarts - restarts + 1) + "번 남음)";
            ClearTrail();
        }

        void ClearTrail()
        {
            foreach (Image d in trail) if (d != null) Destroy(d.gameObject);
            trail.Clear();
        }

        void UpdateInfo()
        {
            int pct = samples > 0 ? Mathf.RoundToInt((1f - (float)offSamples / samples) * 100f) : 100;
            infoText.text = "정확도 " + pct + "% (75 이상 필요)      진행 " + Mathf.RoundToInt(100f * reached / (PointCount - 1)) + "%      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
