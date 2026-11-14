using System;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    public struct ExamResult
    {
        public bool passed;  // 합격 여부
        public int hits;     // 성공 횟수
        public int rounds;   // 전체 라운드 수
        public int needed;   // 합격에 필요한 성공 횟수
    }

    // 승급시험 미니게임: 움직이는 막대를 초록 구간에서 멈춘다.
    // 레벨마다 제목, 라운드 수, 속도, 구간 크기가 달라진다. (나중에 레벨별 전용 미니게임으로 교체할 자리)
    public class ExamGame : MonoBehaviour
    {
        // 시험 보는 레벨(현재 레벨) 기준 제목과 안내
        static readonly string[] Titles =
        {
            "입문 시험  -  물 온도 맞추기",
            "샴푸 시험  -  두피 마사지 리듬",
            "펌 시험  -  펌 타이밍",
            "염색 시험  -  도포 타이밍",
            "드라이 시험  -  바람 세기 조절",
            "커트 시험  -  가위질 타이밍",
            "졸업 시험  -  종합 실기",
        };

        Action<ExamResult> onFinished;
        RectTransform zone;
        RectTransform marker;
        Text infoText;
        Text feedbackText;

        int level;
        int rounds;
        int needed;
        float speed;
        float zoneWidth;

        int round;
        int hits;
        float pos;
        float dir = 1f;
        float zoneCenter;
        float finishTimer = -1f;

        public static void Show(RectTransform root, int level, Action<ExamResult> onFinished)
        {
            GameObject go = new GameObject("ExamGame", typeof(RectTransform));
            go.transform.SetParent(root, false);
            ExamGame game = go.AddComponent<ExamGame>();
            game.onFinished = onFinished;
            game.level = Mathf.Clamp(level, 0, Titles.Length - 1);
            game.Build();
        }

        void Build()
        {
            rounds = 3 + level / 2;
            needed = Mathf.Max(2, rounds - 1);
            speed = 0.9f + 0.12f * level;
            zoneWidth = Mathf.Max(0.12f, 0.26f - 0.02f * level);

            RectTransform self = (RectTransform)transform;
            UiFactory.Stretch(self, 0f, 0f, 1f, 1f);

            Image dim = UiFactory.Panel("Dim", self, new Color(0.10f, 0.10f, 0.16f, 0.96f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);

            Text title = UiFactory.Label("Title", self, Titles[level], 50, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.78f, 1f, 0.92f);

            Text rule = UiFactory.Label("Rule", self,
                "움직이는 막대가 초록 구간에 있을 때 '멈춰!'를 누르세요.\n" + rounds + "번 중 " + needed + "번 이상 성공하면 합격!",
                30, new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter);
            UiFactory.Stretch(rule.rectTransform, 0.1f, 0.64f, 0.9f, 0.78f);

            // 막대
            Image barBg = UiFactory.Panel("Bar", self, new Color(0.30f, 0.30f, 0.38f));
            UiFactory.Stretch(barBg.rectTransform, 0.12f, 0.45f, 0.88f, 0.55f);

            Image z = UiFactory.Panel("Zone", barBg.transform, new Color(0.30f, 0.75f, 0.45f));
            zone = z.rectTransform;
            Image m = UiFactory.Panel("Marker", barBg.transform, new Color(1f, 0.85f, 0.25f));
            marker = m.rectTransform;

            infoText = UiFactory.Label("Info", self, "", 38, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(infoText.rectTransform, 0f, 0.33f, 1f, 0.43f);

            feedbackText = UiFactory.Label("Feedback", self, "", 44, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            UiFactory.Stretch(feedbackText.rectTransform, 0f, 0.24f, 1f, 0.33f);

            Button stop = UiFactory.MakeButton("Stop", self, "멈춰!", 56, new Color(0.85f, 0.40f, 0.35f), OnStop);
            UiFactory.Stretch(stop.GetComponent<RectTransform>(), 0.34f, 0.06f, 0.66f, 0.20f);

            NewZone();
            UpdateInfo();
            Layout();
        }

        void Update()
        {
            if (finishTimer >= 0f)
            {
                finishTimer -= Time.deltaTime;
                if (finishTimer <= 0f) Finish();
                return;
            }

            pos += dir * speed * Time.deltaTime;
            if (pos >= 1f) { pos = 1f; dir = -1f; }
            if (pos <= 0f) { pos = 0f; dir = 1f; }
            Layout();
        }

        void OnStop()
        {
            if (finishTimer >= 0f) return;

            bool hit = Mathf.Abs(pos - zoneCenter) <= zoneWidth / 2f;
            if (hit) hits++;
            feedbackText.text = hit ? "성공!" : "아쉬워요...";
            round++;

            if (round >= rounds)
            {
                finishTimer = 0.9f;
                feedbackText.text += hits >= needed ? "  합격 기준을 넘었어요!" : "  합격 기준에 못 미쳤어요.";
            }
            else
            {
                NewZone();
            }
            UpdateInfo();
        }

        void NewZone()
        {
            float margin = zoneWidth / 2f + 0.05f;
            zoneCenter = UnityEngine.Random.Range(margin, 1f - margin);
        }

        void Layout()
        {
            UiFactory.Stretch(zone, zoneCenter - zoneWidth / 2f, 0f, zoneCenter + zoneWidth / 2f, 1f);
            UiFactory.Stretch(marker, pos - 0.006f, -0.1f, pos + 0.006f, 1.1f);
        }

        void UpdateInfo()
        {
            int shown = Mathf.Min(round + 1, rounds);
            infoText.text = "라운드 " + shown + " / " + rounds + "      성공 " + hits + " / " + needed + " (필요)";
        }

        void Finish()
        {
            ExamResult result = new ExamResult
            {
                passed = hits >= needed,
                hits = hits,
                rounds = rounds,
                needed = needed,
            };
            if (onFinished != null) onFinished(result);
            Destroy(gameObject);
        }
    }
}
