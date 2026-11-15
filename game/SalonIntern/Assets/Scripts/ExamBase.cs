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

    // 미니게임에서 손가락(마우스) 대신 보여주는 도구 모양
    public enum CursorKind { None, Shower, Foam, Comb, Dryer, Scissors, Brush }

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

        RectTransform cursorRt;
        CursorKind cursorKind = CursorKind.None;

        // 도구 모양 커서로 바꾼다. None이면 원래 커서로 돌아간다.
        protected void SetCursor(CursorKind kind)
        {
            if (cursorRt != null) Destroy(cursorRt.gameObject);
            cursorRt = null;
            cursorKind = kind;
            Cursor.visible = kind == CursorKind.None;
            if (kind == CursorKind.None) return;

            cursorRt = UiFactory.Rect("Cursor", self);
            Place(cursorRt, Vector2.zero, new Vector2(10f, 10f));
            int tool = (int)kind - 1;
            Color m = CursorSkins.MainColor(tool);
            Color a = CursorSkins.AccentColor(tool);
            string tn = kind.ToString().ToLower();
            if (Art.Get("tool_" + tn + "_m") != null)
            {
                float sz = 120f;
                Vector2 off = new Vector2(0f, 0f);
                Image im = Dot(cursorRt, off, sz, m, "tool_" + tn + "_m");
                im.color = m;
                Image ia = Dot(cursorRt, off, sz, a, "tool_" + tn + "_a");
                ia.color = a;
                Image il = Dot(cursorRt, off, sz, Color.white, "tool_" + tn + "_l");
                il.color = Color.white;
                foreach (Image x in new[] { im, ia, il }) { x.preserveAspect = true; }
                MoveCursor();
                return;
            }
            switch (kind)
            {
                case CursorKind.Shower:
                    Box(cursorRt, new Vector2(0f, 60f), new Vector2(18f, 90f), m);                       // 호스
                    Dot(cursorRt, new Vector2(0f, 10f), 60f, a);                                           // 샤워 헤드
                    for (int i = -1; i <= 1; i++) Dot(cursorRt, new Vector2(i * 20f, -30f - Mathf.Abs(i) * 8f), 12f, new Color(0.35f, 0.65f, 0.95f)); // 물방울
                    break;
                case CursorKind.Foam:
                    Dot(cursorRt, new Vector2(-18f, -8f), 50f, new Color(m.r, m.g, m.b, 0.95f));
                    Dot(cursorRt, new Vector2(18f, -8f), 50f, new Color(a.r, a.g, a.b, 0.95f));
                    Dot(cursorRt, new Vector2(0f, 20f), 56f, new Color(m.r, m.g, m.b, 0.95f));
                    Dot(cursorRt, new Vector2(-30f, 22f), 20f, new Color(a.r, a.g, a.b, 0.9f));
                    break;
                case CursorKind.Comb:
                    Box(cursorRt, new Vector2(0f, 30f), new Vector2(80f, 22f), m);
                    for (int i = -3; i <= 3; i++) Box(cursorRt, new Vector2(i * 12f, -4f), new Vector2(5f, 36f), a);
                    break;
                case CursorKind.Dryer:
                    Box(cursorRt, new Vector2(10f, 20f), new Vector2(80f, 44f), m);                      // 몸통
                    Box(cursorRt, new Vector2(-8f, -22f), new Vector2(26f, 46f), m * 0.75f);              // 손잡이
                    Box(cursorRt, new Vector2(58f, 20f), new Vector2(20f, 34f), a);                      // 바람 나오는 곳
                    break;
                case CursorKind.Scissors:
                    Box(cursorRt, new Vector2(-12f, 30f), new Vector2(10f, 70f), m).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                    Box(cursorRt, new Vector2(12f, 30f), new Vector2(10f, 70f), m).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                    Dot(cursorRt, new Vector2(-22f, -22f), 26f, a);
                    Dot(cursorRt, new Vector2(22f, -22f), 26f, a);
                    break;
                case CursorKind.Brush:
                    Box(cursorRt, new Vector2(0f, 40f), new Vector2(14f, 80f), m);                       // 손잡이
                    Box(cursorRt, new Vector2(0f, -8f), new Vector2(34f, 34f), a);                       // 솔
                    break;
            }
            MoveCursor();
        }

        void MoveCursor()
        {
            if (cursorRt == null) return;
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(self, pointer.position.ReadValue(), null, out local))
            {
                cursorRt.anchoredPosition = local;
                cursorRt.SetAsLastSibling();
            }
        }

        void OnDestroy()
        {
            Cursor.visible = true;
        }

        void Update()
        {
            MoveCursor();
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
            Sfx.Play(passed ? Sfx.Kind.Good : Sfx.Kind.Bad);
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
            Art.Apply(img, "ui_panel", 32f);
            Place(img.rectTransform, pos, size);
            return img.gameObject.AddComponent<DragPad>();
        }

        protected Image Dot(Transform parent, Vector2 pos, float size, Color color, string art = null)
        {
            Image d = UiFactory.Circle("Dot", parent, color);
            if (art != null) Art.Apply(d, art);
            d.raycastTarget = false;
            Place(d.rectTransform, pos, new Vector2(size, size));
            return d;
        }

        protected Image Box(Transform parent, Vector2 pos, Vector2 size, Color color, string art = null, float border = 0f)
        {
            Image b = UiFactory.Panel("Box", parent, color);
            if (art != null) Art.Apply(b, art, border);
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
