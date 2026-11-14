using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // 입문 시험 미니게임: 섞여 있는 단계 카드를 올바른 순서대로 누른다.
    // 견학에서 배운 시술 순서를 기억하는지 확인한다.
    public class ExamOrderGame : MonoBehaviour
    {
        public class Problem
        {
            public string title;       // 예: "염색 시술의 순서"
            public string[] steps;     // 올바른 순서의 단계 이름
        }

        class Card
        {
            public string name;
            public bool used;
            public Button button;
            public Image image;
        }

        const int AllowedMistakes = 2;

        Action<ExamResult> onFinished;
        List<Problem> problems;
        Text titleText;
        Text progressText;
        Text feedbackText;
        RectTransform cardArea;
        readonly List<Card> cards = new List<Card>();

        int problemIndex;
        int pos;           // 지금까지 맞춘 단계 수
        int mistakes;
        int totalSteps;
        int correct;
        float nextTimer = -1f;
        bool finished;

        public static void Show(RectTransform root, List<Problem> problems, Action<ExamResult> onFinished)
        {
            GameObject go = new GameObject("ExamOrderGame", typeof(RectTransform));
            go.transform.SetParent(root, false);
            ExamOrderGame game = go.AddComponent<ExamOrderGame>();
            game.onFinished = onFinished;
            game.problems = problems;
            game.Build();
        }

        void Build()
        {
            foreach (Problem p in problems) totalSteps += p.steps.Length;

            RectTransform self = (RectTransform)transform;
            UiFactory.Stretch(self, 0f, 0f, 1f, 1f);

            Image dim = UiFactory.Panel("Dim", self, new Color(0.10f, 0.10f, 0.16f, 0.96f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);

            Text title = UiFactory.Label("Title", self, "입문 시험  -  시술 순서 맞추기", 50, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.84f, 1f, 0.96f);

            titleText = UiFactory.Label("Problem", self, "", 44, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            UiFactory.Stretch(titleText.rectTransform, 0f, 0.72f, 1f, 0.83f);

            Text rule = UiFactory.Label("Rule", self,
                "견학에서 배운 순서대로 카드를 눌러요. (틀린 횟수 " + AllowedMistakes + "번까지 괜찮아요)",
                28, new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter);
            UiFactory.Stretch(rule.rectTransform, 0.05f, 0.64f, 0.95f, 0.72f);

            progressText = UiFactory.Label("Progress", self, "", 38, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(progressText.rectTransform, 0.03f, 0.50f, 0.97f, 0.62f);

            cardArea = UiFactory.Rect("Cards", self);
            UiFactory.Stretch(cardArea, 0f, 0.24f, 1f, 0.46f);

            feedbackText = UiFactory.Label("Feedback", self, "", 40, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            UiFactory.Stretch(feedbackText.rectTransform, 0f, 0.08f, 1f, 0.2f);

            LoadProblem();
        }

        void LoadProblem()
        {
            foreach (Transform child in cardArea) Destroy(child.gameObject);
            cards.Clear();
            pos = 0;

            Problem p = problems[problemIndex];
            titleText.text = "(" + (problemIndex + 1) + "/" + problems.Count + ")  " + p.title;

            // 카드 섞기 (이미 정답 순서면 다시 섞기)
            List<string> names = new List<string>(p.steps);
            for (int attempt = 0; attempt < 10; attempt++)
            {
                for (int i = names.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    string t = names[i]; names[i] = names[j]; names[j] = t;
                }
                bool sorted = true;
                for (int i = 0; i < names.Count; i++) if (names[i] != p.steps[i]) sorted = false;
                if (!sorted || names.Count < 3) break;
            }

            int n = names.Count;
            float w = 0.15f, gap = 0.02f;
            float total = n * w + (n - 1) * gap;
            float x0 = (1f - total) / 2f;
            for (int i = 0; i < n; i++)
            {
                Card c = new Card { name = names[i] };
                c.button = UiFactory.MakeButton("Card_" + names[i], cardArea, names[i], 40, new Color(0.35f, 0.55f, 0.75f), () => OnCard(c));
                c.image = c.button.GetComponent<Image>();
                UiFactory.Stretch(c.button.GetComponent<RectTransform>(), x0 + i * (w + gap), 0.1f, x0 + i * (w + gap) + w, 0.9f);
                cards.Add(c);
            }
            UpdateProgress();
        }

        void Update()
        {
            if (nextTimer >= 0f)
            {
                nextTimer -= Time.deltaTime;
                if (nextTimer <= 0f)
                {
                    nextTimer = -1f;
                    problemIndex++;
                    if (problemIndex >= problems.Count) Finish();
                    else
                    {
                        feedbackText.text = "";
                        LoadProblem();
                    }
                }
            }
        }

        void OnCard(Card c)
        {
            if (finished || nextTimer >= 0f || c.used) return;

            Problem p = problems[problemIndex];
            if (c.name == p.steps[pos])
            {
                c.used = true;
                correct++;
                pos++;
                c.button.interactable = false;
                c.image.color = new Color(0.30f, 0.65f, 0.40f);
                feedbackText.text = "맞아요!";
                UpdateProgress();

                if (pos >= p.steps.Length)
                {
                    feedbackText.text = "완성!";
                    nextTimer = 0.8f;
                }
            }
            else
            {
                mistakes++;
                feedbackText.text = "순서가 달라요...  (틀린 횟수 " + mistakes + "/" + AllowedMistakes + ")";
            }
        }

        void UpdateProgress()
        {
            Problem p = problems[problemIndex];
            string s = "";
            for (int i = 0; i < p.steps.Length; i++)
            {
                s += (i > 0 ? "  →  " : "") + (i < pos ? p.steps[i] : "?");
            }
            progressText.text = s;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;

            int needed = totalSteps - AllowedMistakes;
            ExamResult result = new ExamResult
            {
                passed = mistakes <= AllowedMistakes,
                hits = Mathf.Max(0, totalSteps - mistakes),
                rounds = totalSteps,
                needed = needed,
            };
            if (onFinished != null) onFinished(result);
            Destroy(gameObject);
        }
    }
}
