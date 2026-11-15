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
            "머리를 4구역으로 나눠, 번호 순서대로 한 구역씩 염색약을 발라요. 순서가 아닌 구역은 칠하면 감점!",
            "손가락으로 쓸듯이 문질러 머리를 말려요.",
            "점선을 따라 끝까지 드래그해서 가위질해요.",
            "탈색제를 머리 전체에 바르고, 색이 노랗게 빠지는 '목표 구간'에서 헹구기를 눌러요. 너무 오래 두면 머리가 상해요!",
        };

        protected override string Title { get { return Names[Step] + "  -  직접 해봐요!"; } }
        protected override string Rule { get { return Rules[Step] + "  (제한시간 " + LimitFor(Step) + "초)"; } }
        protected override float DimAlpha { get { return 0.88f; } }

        const float TimeLimit = 8f;
        static float LimitFor(int step) { return step == 2 ? 14f : (step == 5 ? 13f : TimeLimit); }
        const float HairRadius = 170f;
        static readonly Vector2 HairCenter = new Vector2(0f, 20f);

        bool scrubMode;
        float timeLeft = TimeLimit;

        // 염색(Step 2): 4구역을 번호 순서대로 칠한다
        int dyeActive;                 // 지금 칠해야 하는 구역 (0~3, 시계 방향: 왼쪽 위 → 오른쪽 위 → 오른쪽 아래 → 왼쪽 아래)
        int dyeMistakes;
        float wrongCooldown;
        readonly Image[] dyeMarkers = new Image[4];
        const float DyeNeed = 0.8f;

        // 탈색(Step 5): 먼저 바르고(0), 색이 빠지는 걸 기다렸다가 알맞을 때 헹군다(1)
        int bleachPhase;
        float bleachT;                 // 탈색 진행 0~1 (6초에 1이 됨)
        bool bleachStopped;
        Image bleachMarker;
        static readonly Vector2 GaugePos = new Vector2(480f, 20f);
        const float GaugeH = 320f;
        const float BleachMin = 0.65f, BleachMax = 0.80f, BleachTime = 6f;

        // 문지르기
        Image[] cells;
        Vector2[] centers;
        float[] amount;
        Color fromColor;
        Color toColor;
        Vector2 last;
        Image hairRoot;
        Vector2 lastStamp = new Vector2(1e6f, 1e6f);

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
            timeLeft = LimitFor(Step);
            scrubMode = Step == 0 || Step == 2 || Step == 3 || Step == 5;
            pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), scrubMode ? new Color(0.72f, 0.82f, 0.86f) : new Color(0.95f, 0.90f, 0.82f));
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
                case 5: fromColor = new Color(0.42f, 0.30f, 0.22f); toColor = new Color(0.95f, 0.93f, 0.88f); break;   // 탈색제는 하얀 크림
                case 3: fromColor = new Color(0.27f, 0.18f, 0.14f); toColor = new Color(0.55f, 0.38f, 0.26f); break; // 젖은 머리 -> 마른 머리
                default: fromColor = new Color(0.42f, 0.30f, 0.22f); toColor = new Color(0.97f, 0.97f, 0.97f); break;
            }
            pad.onDown = p => { last = p; };
            pad.onDrag = Scrub;

            // 샴푸처럼 두상 전체(얼굴·귀)를 위에서 본 모습 위에 머리를 올린다
            Color skin = new Color(0.96f, 0.80f, 0.68f);
            Image scene = Box(pad.transform, new Vector2(0f, 25f), new Vector2(760f, 440f), Color.white, "shampoo_scene");
            if (Art.Get("shampoo_scene") == null)
            {
                scene.gameObject.SetActive(false);
                Box(pad.transform, new Vector2(0f, -205f), new Vector2(760f, 60f), new Color(0.95f, 0.95f, 0.98f));
                Dot(pad.transform, new Vector2(-175f, 5f), 70f, skin);
                Dot(pad.transform, new Vector2(175f, 5f), 70f, skin);
                Dot(pad.transform, new Vector2(0f, -115f), 250f, skin);
                Box(pad.transform, new Vector2(-48f, -190f), new Vector2(40f, 6f), new Color(0.25f, 0.15f, 0.12f));
                Box(pad.transform, new Vector2(48f, -190f), new Vector2(40f, 6f), new Color(0.25f, 0.15f, 0.12f));
            }
            hairRoot = Dot(pad.transform, HairCenter, HairRadius * 2f, fromColor, "hair_head");
            hairRoot.gameObject.AddComponent<Mask>().showMaskGraphic = true; // 칠한 자국이 머리 모양 밖으로 나가지 않게
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
            for (int i = 0; i < centers.Length; i++) { cells[i] = Dot(pad.transform, centers[i], 58f, fromColor, "hair_blob"); cells[i].enabled = false; } // 진행도 계산용(보이지 않음)

            if (Step == 2) SetupDyeMarkers();
        }

        // ---------- 염색: 구역 표시 ----------

        static int Quadrant(Vector2 p)
        {
            Vector2 d = p - HairCenter;
            return d.y >= 0f ? (d.x < 0f ? 0 : 1) : (d.x >= 0f ? 2 : 3);
        }

        void SetupDyeMarkers()
        {
            // 머리를 십자로 나누는 가르마 선
            Box(pad.transform, HairCenter, new Vector2(HairRadius * 2f, 4f), new Color(1f, 1f, 1f, 0.55f)).raycastTarget = false;
            Box(pad.transform, HairCenter, new Vector2(4f, HairRadius * 2f), new Color(1f, 1f, 1f, 0.55f)).raycastTarget = false;
            Vector2[] offs = { new Vector2(-85f, 85f), new Vector2(85f, 85f), new Vector2(85f, -85f), new Vector2(-85f, -85f) };
            for (int i = 0; i < 4; i++)
            {
                Image m = Dot(pad.transform, HairCenter + offs[i], 60f, new Color(0.3f, 0.3f, 0.35f, 0.85f), "bead");
                Text t = UiFactory.Label("DyeNo", m.transform, (i + 1).ToString(), 34, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(t.rectTransform, 0f, 0f, 1f, 1f);
                t.raycastTarget = false;
                dyeMarkers[i] = m;
            }
            RefreshDyeMarkers();
        }

        void RefreshDyeMarkers()
        {
            for (int i = 0; i < 4; i++)
            {
                if (dyeMarkers[i] == null) continue;
                dyeMarkers[i].color = i < dyeActive ? new Color(0.30f, 0.65f, 0.40f, 0.9f)
                    : (i == dyeActive ? new Color(1f, 0.80f, 0.20f, 0.95f) : new Color(0.3f, 0.3f, 0.35f, 0.85f));
                dyeMarkers[i].GetComponentInChildren<Text>().text = i < dyeActive ? "OK" : (i + 1).ToString();
            }
        }

        float SectionProgress(int q)
        {
            float s = 0f; int n = 0;
            for (int i = 0; i < centers.Length; i++)
            {
                if (Quadrant(centers[i]) != q) continue;
                s += amount[i]; n++;
            }
            return n > 0 ? Mathf.Clamp01(s / n / DyeNeed) : 1f;
        }

        // ---------- 탈색: 기다리기 ----------

        static Color BleachColor(float t)
        {
            Color brown = new Color(0.42f, 0.30f, 0.22f), orange = new Color(0.92f, 0.52f, 0.20f);
            Color yellow = new Color(0.98f, 0.86f, 0.36f), pale = new Color(1f, 0.97f, 0.78f), burnt = new Color(0.62f, 0.58f, 0.50f);
            if (t < 0.35f) return Color.Lerp(brown, orange, t / 0.35f);
            if (t < 0.65f) return Color.Lerp(orange, yellow, (t - 0.35f) / 0.30f);
            if (t < 0.80f) return Color.Lerp(yellow, pale, (t - 0.65f) / 0.15f);
            return Color.Lerp(pale, burnt, Mathf.Clamp01((t - 0.80f) / 0.20f));
        }

        void StartBleaching()
        {
            bleachPhase = 1;
            feedbackText.text = "탈색제를 발랐어요! 노랗게 빠질 때까지 기다려요...";
            Box(pad.transform, GaugePos, new Vector2(46f, GaugeH), new Color(0.30f, 0.30f, 0.38f)).raycastTarget = false;
            float zoneMid = -GaugeH / 2f + GaugeH * (BleachMin + BleachMax) / 2f;
            Box(pad.transform, GaugePos + new Vector2(0f, zoneMid), new Vector2(46f, GaugeH * (BleachMax - BleachMin)), new Color(0.30f, 0.75f, 0.45f)).raycastTarget = false;
            bleachMarker = Box(pad.transform, GaugePos, new Vector2(70f, 8f), new Color(1f, 0.85f, 0.25f));
            bleachMarker.raycastTarget = false;
            MakeButtonAt("헹구기!", new Vector2(-480f, 20f), new Vector2(230f, 150f), 40, new Color(0.35f, 0.60f, 0.85f), StopBleach);
            SetCursor(CursorKind.None);
        }

        void StopBleach()
        {
            if (finished || bleachPhase != 1 || bleachStopped) return;
            bleachStopped = true;
            Sfx.Play(Sfx.Kind.Water);
            Finish();
        }

        float BleachTiming()
        {
            if (bleachPhase != 1 || bleachT >= 1f) return 0f;
            float err = Mathf.Max(0f, Mathf.Abs(bleachT - (BleachMin + BleachMax) / 2f) - (BleachMax - BleachMin) / 2f);
            return 1f - Mathf.Clamp01(err / 0.25f);
        }

        float BleachCoverage() { return Mathf.Clamp01(ScrubPercent() / 80f); }

        void Scrub(Vector2 p)
        {
            if (finished) return;
            float dist = (p - last).magnitude;
            last = p;
            if (Step == 5 && bleachPhase == 1) return;   // 탈색제를 바른 뒤에는 기다리는 단계

            // 염색: 순서가 아닌 구역은 칠하지 않고 감점
            if (Step == 2 && (p - HairCenter).magnitude < HairRadius && Quadrant(p) != dyeActive)
            {
                if (wrongCooldown <= 0f)
                {
                    dyeMistakes++;
                    wrongCooldown = 0.5f;
                    feedbackText.text = (dyeActive + 1) + "번 구역부터 발라요!";
                }
                return;
            }

            // 지나간 자리를 머리 위에 직접 칠한다
            if (hairRoot != null && (p - HairCenter).magnitude < HairRadius + 70f && ((p - lastStamp).magnitude > 22f || lastStamp.x > 99999f))
            {
                lastStamp = p;
                float stampSize = Step == 2 ? 100f : 150f;
                Image st = Dot(hairRoot.transform, p - HairCenter, stampSize, toColor, "paint");
                st.color = toColor;
            }
            for (int i = 0; i < centers.Length; i++)
            {
                if ((centers[i] - p).magnitude > 80f) continue;
                if (Step == 2 && Quadrant(centers[i]) != dyeActive) continue;
                amount[i] = Mathf.Min(1f, amount[i] + dist / 260f);
            }

            if (Step == 2)
            {
                if (SectionProgress(dyeActive) >= 1f)
                {
                    dyeActive++;
                    Sfx.Play(Sfx.Kind.Pop);
                    RefreshDyeMarkers();
                    if (dyeActive >= 4) Finish();
                    else feedbackText.text = (dyeActive + 1) + "번 구역!";
                }
            }
            else if (Step == 5)
            {
                if (ScrubPercent() >= 80) StartBleaching();
            }
            else if (ScrubPercent() >= 100) Finish();
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
                Dot(pad.transform, guide[i], i == 0 || i == PointCount - 1 ? 38f : 16f,
                    i == 0 ? new Color(1f, 0.85f, 0.25f) : (i == PointCount - 1 ? new Color(0.30f, 0.75f, 0.45f) : new Color(0.45f, 0.40f, 0.35f)), "bead");
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
                Dot(pad.transform, p, 16f, off ? new Color(0.90f, 0.30f, 0.30f) : new Color(0.30f, 0.75f, 0.45f), "bead");
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

        int Percent()
        {
            if (Step == 2)
            {
                float done = dyeActive >= 4 ? 4f : dyeActive + SectionProgress(dyeActive);
                return Mathf.Clamp(Mathf.RoundToInt(100f * done / 4f) - dyeMistakes * 3, 0, 100);
            }
            if (Step == 5) return Mathf.RoundToInt(100f * (0.4f * BleachCoverage() + 0.6f * BleachTiming()));
            return scrubMode ? ScrubPercent() : TracePercent();
        }

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
            if (wrongCooldown > 0f) wrongCooldown -= Time.deltaTime;
            if (Step == 5 && bleachPhase == 1 && !bleachStopped)
            {
                bleachT = Mathf.Min(1f, bleachT + Time.deltaTime / BleachTime);
                Color c = BleachColor(bleachT);
                hairRoot.color = c;
                foreach (Transform ch in hairRoot.transform) { Image im = ch.GetComponent<Image>(); if (im != null) im.color = c; }
                bleachMarker.rectTransform.anchoredPosition = GaugePos + new Vector2(0f, -GaugeH / 2f + GaugeH * bleachT);
                if (bleachT >= 1f) { feedbackText.text = "너무 오래 뒀어요... 머리가 상했어요!"; Finish(); return; }
            }
            UpdateInfo();
            if (timeLeft <= 0f) Finish();
        }

        void UpdateInfo()
        {
            if (Step == 5 && bleachPhase == 1)
            {
                infoText.text = "탈색 " + Mathf.RoundToInt(bleachT * 100f) + "%  (초록 구간에서 헹궈요)      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
                return;
            }
            infoText.text = (Step == 2 ? "구역 " + Mathf.Min(dyeActive + 1, 4) + " / 4      " : "") + "완성도 " + Percent() + "%      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
