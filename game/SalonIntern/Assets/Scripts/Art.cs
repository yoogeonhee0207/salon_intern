using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // Resources/Art 폴더의 그림(PNG)을 불러와서 화면 요소에 입히는 도우미.
    // 그림 파일이 없으면 false/null을 돌려주므로, 호출한 쪽은 예전 도형 모양을 그대로 쓴다.
    // 그림은 tools/make_art.py 로 만든다.
    public static class Art
    {
        static Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache = new Dictionary<string, Sprite>(); }

        // border > 0 이면 가장자리를 늘리지 않는 9분할 스프라이트(버튼·말풍선 틀)로 만든다
        public static Sprite Get(string name, float border = 0f)
        {
            string key = name + "|" + border;
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            Texture2D tex = Resources.Load<Texture2D>("Art/" + name);
            if (tex != null)
            {
                s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            }
            cache[key] = s;
            return s;
        }

        // 이미지에 그림을 입힌다. 성공하면 true.
        public static bool Apply(Image img, string name, float border = 0f, bool tiled = false)
        {
            if (img == null) return false;
            Sprite s = Get(name, border);
            if (s == null) return false;
            img.sprite = s;
            img.type = border > 0f ? Image.Type.Sliced : (tiled ? Image.Type.Tiled : Image.Type.Simple);
            return true;
        }

        // 도형 대신 그림으로 된 캐릭터를 만든다. 그림이 없으면 색깔 동그라미를 만든다.
        public static Image Figure(string name, Transform parent, Color fallback, string sprite)
        {
            Image img = UiFactory.Circle(name, parent, fallback);
            if (Apply(img, sprite))
            {
                img.color = Color.white;
                img.preserveAspect = true;
            }
            return img;
        }
    }
}
