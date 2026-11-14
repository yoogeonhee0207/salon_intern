using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // 평소 시술 단계에서 인턴이 직접 하는 짧은 미니게임 (승급시험의 가벼운 버전).
    // 실패는 없고, 얼마나 잘했는지(0~100)가 손님 만족도와 선생님 친분에 영향을 준다.
    public class StepGame : ExamBase
    {
        // 0 샴푸, 1 펌, 2 염색, 3 드라이, 4 커트, 5 탈색
        public static int Step;

        static readonly string[] Names = { "샴푸", "펌", "염색 도포", "드라이", "커트", "탈색" };
        static readonly string[] Rules =
        {
            "손님 머리를 손가락으로 문질러 거품을 내요.",
            "점선을 따라 끝까지 드래그해요.",
            "손님 머리를 문질러 염색약을 골고루 발라요.",
            "손가락으로 쓸듯이 문질러 머리를 말려요.",
            "점선을 따라 끝까지 드래그해서 가위질해요.",
            "손님 머리를 문질러 탈색제를 골고루 발라요.",
        };

        protected override string Title { get { return Names[Step] + "  -  직접 해봐요!"; } }
        protected override string Rule { get { return Rules[Step] + "  (제한시간 " + TimeLimit + "초)"; } }
        protected override float DimAlpha { get { return 0.88f; } }

        const float TimeLimit = 8f;
        const float HairRadius = 170f;
        static readonly Vector2 HairCenter = new Vector2(0f, 20f);

        bool scrubMode;
        float timeLeft = TimeLimit;

        // 문지르기
        Image[] cells;
        Vector2[] centers;
        float[] amount;
        Color fromColor;
        Color toColor;
        Vector2 last;

        // 따라 그리기
        const int PointCount = 45;
        readonly Vector2[] guide = new Vector2[PointCount];
        DragPad pad;
        bool drawing;
        int reached;
        int samples;
        int offSamples;
        Vector2 lastDot;

        protected override void Setup()
        {
            scrubMode = Step == 0 || Step == 2 || Step == 3 || Step == 5;
            pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.95f, 0.90f, 0.82f));
            if (scrubMode) SetupScrub(); else SetupTrace();
            SetCursor(Step == 3 ? CursorKind.Dryer : (Step == 4 ? CursorKind.Scissors : CursorKind.Brush));
            UpdateInfo();
        }

        // ---------- 문지르기 (샴푸, 염색, 드라이, 탈색) ----------

        void SetupScrub()
        {
            switch (Step)
            {
                case 0: fromColor = new Color(0.42f, 0.30f, 0.22f); toColor = new Color(0.97f, 0.99f, 1f); break;
                case 2: fromColor = new Color(0.42f, 0.30f, 0.22f); toColor = new Color(0.85f, 0.35f, 0.55f); break;
                case 3: fromColor = new Color(0.35f, 0.55f, 0.85f); toColor = new Color(0.92f, 0.82f, 0.62f); break;
                default: fromColor = new Color(0.42f, 0.30f, 0.22f); toColor = new Color(0.98f, 0.95f, 0.70f); break;
            }
            pad.onDown = p => { last = p; };
            pad.onDrag = Scrub;

            Dot(pad.transform, HairCenter, HairRadius * 2f, new Color(0.30f, 0.20f, 0.15f));
            var list = new List<Vector2>();
            const float spacing = 64f;
            for (float y = -HairRadius; y <= HairRadius; y += spacing)
            {
                for (float x = -HairRadius; x <= HairRadius; x += spacing)
                {
                    float ox = (Mathf.RoundToInt((y + HairRadius) / spacing) % 2 == 0) ? 0f : spacing / 2f;
                    Vector2 pos = new Vector2(x + ox, y);
                    if (pos.magnitude <= HairRadius - 28f) list.Add(HairCenter + pos);
                }
            }
            centers = list.ToArray();
            cells = new Image[centers.Length];
            amount = new float[centers.Length];
            for (int i = 0; i < centers.Length; i++) cells[i] = Dot(pad.transform, centers[i], 58f, fromColor);
        }

        void Scrub(Vector2 p)
        {
            if (finished) return;
            float dist = (p - last).magnitude;
            last = p;
            for (int i = 0; i < centers.Length; i++)
            {
                if ((centers[i] - p).magnitude > 80f) continue;
                amount[i] = Mathf.Min(1f, amount[i] + dist / 260f);
                cells[i].color = Color.Lerp(fromColor, toColor, amount[i]);
            }
            if (ScrubPercent() >= 100) Finish();
        }

        int ScrubPercent()
        {
            float s = 0f;
            for (int i = 0; i < amount.Length; i++) s += amount[i];
            return Mathf.RoundToInt(100f * s / amount.Length);
        }

        // ---------- 따라 그리기 (펌, 커트) ----------

        void SetupTrace()
        {
            pad.onDown = OnDown;
            pad.onDrag = OnDrag;
            pad.onUp = () => { drawing = false; };

            for (int i = 0; i < PointCount; i++)
            {
                float t = i / (float)(PointCount - 1);
                float x = -520f + t * 1040f;
                float y = Step == 1 ? 120f * Mathf.Sin(t * Mathf.PI) - 40f : 90f * Mathf.Sin(t * Mathf.PI * 3f);
                guide[i] = new Vector2(x, y);
                Dot(pad.transform, guide[i], i == 0 || i == PointCount - 1 ? 34f : 12f,
                    i == 0 ? new Color(1f, 0.85f, 0.25f) : (i == PointCount - 1 ? new Color(0.30f, 0.75f, 0.45f) : new Color(0.45f, 0.40f, 0.35f)));
            }
        }

        void OnDown(Vector2 p)
        {
            if (finished) return;
            drawing = true;
            lastDot = p;
        }

        void OnDrag(Vector2 p)
        {
            if (finished || !drawing) return;

            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < PointCount; i++)
            {
                float d = (guide[i] - p).magnitude;
                if (d < best) { best = d; nearest = i; }
            }

            bool off = best > 60f || nearest > reached + 6;
            samples++;
            if (off) offSamples++;
            else if (nearest > reached) reached = nearest;

            if ((p - lastDot).magnitude > 16f)
            {
                lastDot = p;
                Dot(pad.transform, p, 12f, off ? new Color(0.90f, 0.30f, 0.30f) : new Color(0.30f, 0.75f, 0.45f));
            }
            if (reached >= PointCount - 2) Finish();
        }

        int TracePercent()
        {
            float progress = reached / (float)(PointCount - 1);
            float accuracy = samples > 0 ? 1f - (float)offSamples / samples : 0f;
            return Mathf.RoundToInt(100f * progress * Mathf.Lerp(0.5f, 1f, accuracy));
        }

        // ---------- 공통 ----------

        int Percent() { return scrubMode ? ScrubPercent() : TracePercent(); }

        void Finish()
        {
            if (finished) return;
            int pct = Mathf.Clamp(Percent(), 0, 100);
            string grade = pct >= 85 ? "S" : (pct >= 60 ? "A" : (pct >= 30 ? "B" : "C"));
            End(true, pct, 100, 0, grade + "등급!  (" + pct + "%)");
        }

        protected override void Tick()
        {
            timeLeft -= Time.deltaTime;
            UpdateInfo();
            if (timeLeft <= 0f) Finish();
        }

        void UpdateInfo()
        {
            infoText.text = "완성도 " + Percent() + "%      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
