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

        static Sprite circleSprite;

        // 동그라미 모양 이미지 (도형 캐릭터용)
        public static Sprite CircleSprite()
        {
            if (circleSprite == null)
            {
                int n = 64;
                Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                float r = n / 2f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                    }
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply();
                circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
            return circleSprite;
        }

        public static Image Circle(string name, Transform parent, Color color)
        {
            Image img = Panel(name, parent, color);
            img.sprite = CircleSprite();
            return img;
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
