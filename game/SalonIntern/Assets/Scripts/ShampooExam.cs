using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // 샴푸 시험: 1) 물로 적시기 2) 샴푸로 거품내기 3) 헹구기. 시험은 세 단계를 모두 끝내야 합격.
    // Lite 모드(평소 시술)는 짧고 실패 없이 완성도(0~100)만 돌려준다.
    public class ShampooExam : ExamBase
    {
        public static bool Lite;

        protected override string Title { get { return (lite || Lite) ? "샴푸  -  직접 해봐요!" : "샴푸 시험  -  적시기, 샴푸, 헹구기"; } }
        protected override string Rule { get { return "물 틀기  →  머리 적시기  →  샴푸 짜기  →  거품내기  →  물 틀기  →  헹구기\n수도꼭지·샴푸통을 눌러 준비하고, 머리는 손가락으로 문질러요."; } }
        protected override float DimAlpha { get { return (lite || Lite) ? 0.88f : 0.97f; } }

        // 버튼으로 준비하는 단계(물 틀기, 샴푸 짜기)와 머리를 문지르는 단계가 번갈아 나온다
        static readonly string[] StageNames = { "1단계 물 틀기", "2단계 머리 적시기", "3단계 샴푸 짜기", "4단계 거품내기", "5단계 물 틀기", "6단계 헹구기" };
        static readonly string[] StageTips =
        {
            "왼쪽 수도꼭지를 눌러 물을 틀어요",
            "샤워기로 머리를 문질러 적셔요",
            "오른쪽 샴푸통을 눌러 샴푸를 짜요",
            "샴푸 거품을 내요",
            "왼쪽 수도꼭지를 눌러 물을 틀어요",
            "거품을 깨끗이 헹궈요",
        };
        const int StageCount = 6;
        static readonly Color Dry = new Color(0.55f, 0.38f, 0.26f);
        static readonly Color Wet = new Color(0.27f, 0.18f, 0.14f);
        static readonly Color Foam = new Color(0.97f, 0.99f, 1f);
        static readonly Color Clean = new Color(0.30f, 0.21f, 0.16f);
        static readonly Color[] From = { Dry, Wet, Foam };   // 문지르는 단계(적시기, 거품내기, 헹구기)마다
        static readonly Color[] To = { Wet, Foam, Clean };

        const float HairRadius = 170f;
        static readonly Vector2 HairCenter = new Vector2(0f, 20f);

        bool lite;
        float timeLimit;
        float timeLeft;
        int stage;
        float need; // 단계를 넘기는 데 필요한 문지르기 비율

        Image hairImage;   // 머리 전체 한 장 (진행도에 따라 색이 변함)
        Vector2[] centers;
        float[] amount;
        Vector2 last;
        Button faucetButton;
        Button bottleButton;
        Image faucetGlow;
        Image bottleGlow;

        protected override void Setup()
        {
            lite = Lite;
            Lite = false;
            timeLimit = lite ? 15f : 40f;
            timeLeft = timeLimit;
            need = lite ? 0.85f : 0.9f;

            DragPad pad = MakePad(new Vector2(1400f, 440f), new Vector2(0f, 30f), new Color(0.72f, 0.82f, 0.86f));
            pad.onDown = p => { last = p; };
            pad.onDrag = Scrub;

            // 샴푸대에 누운 손님을 위에서 본 모습
            Color skin = new Color(0.96f, 0.80f, 0.68f);
            Image scene = Box(pad.transform, new Vector2(0f, 25f), new Vector2(760f, 440f), Color.white, "shampoo_scene");
            bool sceneArt = Art.Get("shampoo_scene") != null;
            if (!sceneArt) { scene.gameObject.SetActive(false);
            Box(pad.transform, new Vector2(0f, -205f), new Vector2(760f, 60f), new Color(0.95f, 0.95f, 0.98f));
            Dot(pad.transform, new Vector2(-175f, 5f), 70f, skin);
            Dot(pad.transform, new Vector2(175f, 5f), 70f, skin);
            Dot(pad.transform, new Vector2(0f, -115f), 250f, skin);
            Dot(pad.transform, HairCenter, HairRadius * 2f, new Color(0.25f, 0.17f, 0.13f));
            Box(pad.transform, new Vector2(-48f, -190f), new Vector2(40f, 6f), new Color(0.25f, 0.15f, 0.12f));
            Box(pad.transform, new Vector2(48f, -190f), new Vector2(40f, 6f), new Color(0.25f, 0.15f, 0.12f)); }

            var list = new System.Collections.Generic.List<Vector2>();
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
            amount = new float[centers.Length];
            hairImage = Dot(pad.transform, HairCenter, HairRadius * 2f, From[0], "hair_head");
            hairImage.raycastTarget = false;

            // 옆에 놓인 수도꼭지(왼쪽)와 샴푸통(오른쪽): 필요할 때 눌러서 쓴다
            faucetButton = MakeTool("수도꼭지", new Vector2(-590f, 30f), new Color(0.78f, 0.88f, 0.95f), () => PressTool(0), out faucetGlow);
            if (Art.Get("prop_faucet") != null) Dot(faucetButton.transform, new Vector2(0f, 45f), 200f, Color.white, "prop_faucet");
            else
            {
            Box(faucetButton.transform, new Vector2(-40f, 70f), new Vector2(34f, 90f), new Color(0.55f, 0.58f, 0.64f));
            Box(faucetButton.transform, new Vector2(0f, 105f), new Vector2(110f, 34f), new Color(0.55f, 0.58f, 0.64f));
            Box(faucetButton.transform, new Vector2(45f, 70f), new Vector2(26f, 40f), new Color(0.55f, 0.58f, 0.64f));
            Dot(faucetButton.transform, new Vector2(-40f, 135f), 56f, new Color(0.30f, 0.55f, 0.85f)); // 손잡이
            Dot(faucetButton.transform, new Vector2(45f, 35f), 16f, new Color(0.45f, 0.70f, 0.95f));  // 물방울
            }

            bottleButton = MakeTool("샴푸", new Vector2(590f, 30f), new Color(0.95f, 0.88f, 0.95f), () => PressTool(1), out bottleGlow);
            if (Art.Get("prop_bottle") != null) Dot(bottleButton.transform, new Vector2(0f, 45f), 200f, Color.white, "prop_bottle");
            else
            {
            Box(bottleButton.transform, new Vector2(0f, 70f), new Vector2(90f, 130f), new Color(0.85f, 0.45f, 0.70f)); // 통
            Box(bottleButton.transform, new Vector2(0f, 150f), new Vector2(40f, 30f), new Color(0.95f, 0.95f, 0.98f)); // 펌프
            Box(bottleButton.transform, new Vector2(30f, 168f), new Vector2(50f, 14f), new Color(0.95f, 0.95f, 0.98f));
            }

            StartStage(0);
        }

        bool IsAction { get { return stage % 2 == 0; } }
        int ScrubIndex { get { return stage / 2; } }

        // kind 0 수도꼭지, 1 샴푸통
        Button MakeTool(string label, Vector2 pos, Color bg, System.Action onClick, out Image glow)
        {
            Button b = MakeButtonAt(label, pos, new Vector2(230f, 290f), 30, bg, onClick);
            b.GetComponentInChildren<Text>().alignment = TextAnchor.LowerCenter;
            b.GetComponentInChildren<Text>().color = new Color(0.15f, 0.15f, 0.2f);
            glow = b.GetComponent<Image>();
            return b;
        }

        void PressTool(int kind)
        {
            if (finished) return;
            if (!IsAction)
            {
                feedbackText.text = "지금은 머리를 문질러요!";
                return;
            }
            bool needFaucet = stage != 2;
            if ((kind == 0) != needFaucet)
            {
                feedbackText.text = needFaucet ? "지금은 수도꼭지를 눌러요!" : "지금은 샴푸를 눌러요!";
                return;
            }
            Sfx.Play(kind == 0 ? Sfx.Kind.Water : Sfx.Kind.Pop);
            StartStage(stage + 1);
        }

        void StartStage(int s)
        {
            stage = s;
            int si = Mathf.Min(2, s / 2);
            for (int i = 0; i < amount.Length; i++) amount[i] = 0f;
            hairImage.color = From[si];
            bool action = s % 2 == 0;
            // 지금 눌러야 하는 도구는 밝게 강조
            faucetGlow.color = action && s != 2 ? new Color(0.55f, 0.85f, 1f) : new Color(0.78f, 0.88f, 0.95f, 0.6f);
            bottleGlow.color = action && s == 2 ? new Color(1f, 0.70f, 0.90f) : new Color(0.95f, 0.88f, 0.95f, 0.6f);
            feedbackText.text = StageTips[s];
            // 문지르는 단계: 적시기·헹구기는 샤워기 호스, 거품내기는 거품. 준비 단계는 원래 커서
            SetCursor(action ? CursorKind.None : (s == 3 ? CursorKind.Foam : CursorKind.Shower));
            UpdateInfo();
        }

        void Scrub(Vector2 p)
        {
            if (finished) return;
            float dist = (p - last).magnitude;
            last = p;

            if (IsAction)
            {
                feedbackText.text = "먼저 옆의 수도꼭지나 샴푸통을 눌러요!";
                return;
            }

            int si = ScrubIndex;
            for (int i = 0; i < centers.Length; i++)
            {
                if ((centers[i] - p).magnitude > 80f) continue;
                amount[i] = Mathf.Min(1f, amount[i] + dist / 260f);
            }
            hairImage.color = Color.Lerp(From[si], To[si], Mathf.Clamp01(Coverage() / need));

            if (Coverage() >= need)
            {
                if (stage >= StageCount - 1) Finish(true);
                else StartStage(stage + 1);
            }
        }

        float Coverage()
        {
            float s = 0f;
            for (int i = 0; i < amount.Length; i++) s += amount[i];
            return s / amount.Length;
        }

        int Percent()
        {
            float part = IsAction ? 0f : Mathf.Min(1f, Coverage() / need);
            return Mathf.Clamp(Mathf.RoundToInt(100f * (stage + part) / StageCount), 0, 100);
        }

        void Finish(bool allDone)
        {
            if (finished) return;
            if (lite)
            {
                // 끝까지 마쳐도 시간이 빠듯하면 A, 시간이 넉넉히(40% 이상) 남아야 S. 못 마치면 진행도만큼
                int pct = allDone
                    ? Mathf.RoundToInt(Mathf.Lerp(65f, 100f, Mathf.Clamp01(timeLeft / (timeLimit * 0.4f))))
                    : Percent();
                string grade = pct >= 85 ? "S" : (pct >= 60 ? "A" : (pct >= 30 ? "B" : "C"));
                End(true, pct, 100, 0, grade + "등급!  (" + pct + "%)");
            }
            else
            {
                End(allDone, allDone ? StageCount : stage, StageCount, StageCount, allDone ? "깨끗하게 감겼어요!" : "시간이 다 됐어요...");
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
            infoText.text = StageNames[stage] + (IsAction ? "" : "  " + Mathf.RoundToInt(Mathf.Min(1f, Coverage() / need) * 100f) + "%") + "      남은 시간 " + Mathf.Max(0f, timeLeft).ToString("0.0") + "초";
        }
    }
}
