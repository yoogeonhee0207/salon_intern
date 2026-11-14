using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SalonIntern
{
    // 누르기, 드래그, 떼기를 받아서 패드 안의 좌표(중앙 기준)로 알려주는 도우미.
    public class DragPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2> onDown;
        public Action<Vector2> onDrag;
        public Action onUp;

        Vector2 Local(PointerEventData e)
        {
            Vector2 p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out p);
            return p;
        }

        public void OnPointerDown(PointerEventData e) { if (onDown != null) onDown(Local(e)); }
        public void OnDrag(PointerEventData e) { if (onDrag != null) onDrag(Local(e)); }
        public void OnPointerUp(PointerEventData e) { if (onUp != null) onUp(); }
    }

    // 승급시험 미니게임들의 공통 틀: 어두운 배경, 제목, 설명, 안내 줄, 결과 전달.
    public abstract class ExamBase : MonoBehaviour
    {
        Action<ExamResult> onFinished;
        ExamResult pending;
        float finishTimer = -1f;

        protected bool finished;
        protected RectTransform self;
        protected Text infoText;
        protected Text feedbackText;

        protected abstract string Title { get; }
        protected abstract string Rule { get; }
        protected abstract void Setup();
        protected virtual void Tick() { }
        protected virtual float DimAlpha { get { return 0.97f; } }

        public static T Show<T>(RectTransform root, Action<ExamResult> onFinished) where T : ExamBase
        {
            GameObject go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            T game = go.AddComponent<T>();
            game.onFinished = onFinished;
            game.BuildBase();
            return game;
        }

        void BuildBase()
        {
            self = (RectTransform)transform;
            UiFactory.Stretch(self, 0f, 0f, 1f, 1f);

            Image dim = UiFactory.Panel("Dim", self, new Color(0.10f, 0.10f, 0.16f, DimAlpha));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);

            Text title = UiFactory.Label("Title", self, Title, 48, Color.white, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0f, 470f), new Vector2(1700f, 90f));

            Text rule = UiFactory.Label("Rule", self, Rule, 28, new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter);
            Place(rule.rectTransform, new Vector2(0f, 395f), new Vector2(1700f, 80f));

            infoText = UiFactory.Label("Info", self, "", 36, Color.white, TextAnchor.MiddleCenter);
            Place(infoText.rectTransform, new Vector2(0f, -420f), new Vector2(1700f, 60f));

            feedbackText = UiFactory.Label("Feedback", self, "", 40, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            Place(feedbackText.rectTransform, new Vector2(0f, -490f), new Vector2(1700f, 60f));

            Setup();
        }

        void Update()
        {
            if (finishTimer >= 0f)
            {
                finishTimer -= Time.deltaTime;
                if (finishTimer <= 0f)
                {
                    if (onFinished != null) onFinished(pending);
                    Destroy(gameObject);
                }
                return;
            }
            Tick();
        }

        // 시험을 끝낸다. 결과 메시지를 잠깐 보여준 뒤 결과를 넘긴다.
        protected void End(bool passed, int hits, int rounds, int needed, string message)
        {
            if (finished) return;
            finished = true;
            feedbackText.text = message;
            pending = new ExamResult { passed = passed, hits = hits, rounds = rounds, needed = needed };
            finishTimer = 1.3f;
        }

        // ---------- 만들기 도우미 ----------

        protected static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        protected DragPad MakePad(Vector2 size, Vector2 pos, Color color)
        {
            Image img = UiFactory.Panel("Pad", self, color);
            Place(img.rectTransform, pos, size);
            return img.gameObject.AddComponent<DragPad>();
        }

        protected Image Dot(Transform parent, Vector2 pos, float size, Color color)
        {
            Image d = UiFactory.Circle("Dot", parent, color);
            d.raycastTarget = false;
            Place(d.rectTransform, pos, new Vector2(size, size));
            return d;
        }

        protected Image Box(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            Image b = UiFactory.Panel("Box", parent, color);
            b.raycastTarget = false;
            Place(b.rectTransform, pos, size);
            return b;
        }

        protected Button MakeButtonAt(string text, Vector2 pos, Vector2 size, int fontSize, Color color, Action onClick)
        {
            Button b = UiFactory.MakeButton("Btn_" + text, self, text, fontSize, color, onClick);
            Place(b.GetComponent<RectTransform>(), pos, size);
            return b;
        }

        protected Text LabelAt(string text, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            Text t = UiFactory.Label("Label", self, text, fontSize, color, TextAnchor.MiddleCenter);
            Place(t.rectTransform, pos, size);
            return t;
        }
    }
}
