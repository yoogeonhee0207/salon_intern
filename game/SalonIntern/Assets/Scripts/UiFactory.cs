using System;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // 화면 요소(패널, 글자, 버튼)를 코드로 만들어주는 도우미.
    // 지금은 에디터에서 UI를 직접 배치하지 않고 코드로 만든다.
    public static class UiFactory
    {
        static Font font;

        // 한글이 보이도록 컴퓨터에 설치된 폰트를 사용한다.
        // 안드로이드/iOS 빌드 때는 한글 폰트를 게임에 포함하는 작업이 따로 필요하다.
        public static Font GetFont()
        {
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Noto Sans CJK KR", "Noto Sans KR", "Arial" },
                    32);
            }
            return font;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            RectTransform rt = Rect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
        {
            RectTransform rt = Rect(name, parent);
            Stretch(rt, 0f, 0f, 1f, 1f);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = GetFont();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false; // 글자가 클릭을 가로채지 않게
            return text;
        }

        public static Button MakeButton(string name, Transform parent, string content, int size, Color bg, Action onClick)
        {
            Image img = Panel(name, parent, bg);
            Button button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            Label("Text", img.transform, content, size, Color.white, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
