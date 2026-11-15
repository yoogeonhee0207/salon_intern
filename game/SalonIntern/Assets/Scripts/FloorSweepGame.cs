using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SalonIntern
{
    public struct SweepResult
    {
        public int cleaned;   // 치운 머리카락 수
        public int total;     // 전체 머리카락 수
        public float elapsed; // 걸린 시간(초)
        public bool timeUp;   // 시간 초과 여부
    }

    // 바닥 청소 미니게임: 화면을 드래그해서 머리카락을 쓰레기통으로 쓸어 넣는다.
    public class FloorSweepGame : MonoBehaviour
    {
        const int HairCount = 14;
        const float TimeLimit = 20f;     // 제한시간(초)
        const float BroomRadius = 90f;   // 빗자루 반경
        static readonly Vector2 PadSize = new Vector2(1500f, 700f);
        static readonly Vector2 BinSize = new Vector2(300f, 260f);

        Action<SweepResult> onFinished;
        RectTransform pad;
        RectTransform bin;
        RectTransform broom;
        RectTransform barFill;
        Text infoText;
        readonly List<RectTransform> hairs = new List<RectTransform>();

        float elapsed;
        int cleaned;
        bool finished;
        bool dragging;
        Vector2 lastLocal;

        public static void Show(RectTransform root, Action<SweepResult> onFinished)
        {
            GameObject go = new GameObject("FloorSweepGame", typeof(RectTransform));
            go.transform.SetParent(root, false);
            FloorSweepGame game = go.AddComponent<FloorSweepGame>();
            game.onFinished = onFinished;
            game.Build();
        }

        void Build()
        {
            RectTransform self = (RectTransform)transform;
            UiFactory.Stretch(self, 0f, 0f, 1f, 1f);

            // 뒤쪽 카드가 눌리지 않도록 화면 전체를 덮는 어두운 막
            Image dim = UiFactory.Panel("Dim", self, new Color(0f, 0f, 0f, 0.70f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);

            Text title = UiFactory.Label("Title", self,
                "바닥 청소  -  머리카락을 쓰레기통으로 쓸어 넣으세요!", 44, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.91f, 1f, 1f);

            // 남은 시간 막대
            Image barBg = UiFactory.Panel("TimeBg", self, new Color(0.30f, 0.30f, 0.35f));
            Art.Apply(barBg, "ui_bubble", 14f);
            UiFactory.Stretch(barBg.rectTransform, 0.2f, 0.875f, 0.8f, 0.905f);
            Image fill = UiFactory.Panel("TimeFill", barBg.transform, new Color(0.30f, 0.72f, 0.40f));
            Art.Apply(fill, "ui_bubble", 14f);
            UiFactory.Stretch(fill.rectTransform, 0f, 0f, 1f, 1f);
            barFill = fill.rectTransform;

            infoText = UiFactory.Label("Info", self, "", 36, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(infoText.rectTransform, 0f, 0.03f, 1f, 0.10f);

            // 청소하는 바닥
            Image padImg = UiFactory.Panel("Pad", self, new Color(0.95f, 0.90f, 0.82f));
            Art.Apply(padImg, "ui_panel", 32f);
            pad = padImg.rectTransform;
            SetCenter(pad, Vector2.zero, PadSize);
            SweepPad handler = padImg.gameObject.AddComponent<SweepPad>();
            handler.game = this;

            // 쓰레기통 (바닥 오른쪽 아래)
            Image binImg = UiFactory.Panel("Bin", pad, new Color(0.35f, 0.38f, 0.45f));
            if (Art.Apply(binImg, "trash_bin")) binImg.color = Color.white;
            binImg.raycastTarget = false;
            bin = binImg.rectTransform;
            SetCenter(bin, new Vector2(PadSize.x / 2f - BinSize.x / 2f - 20f, -PadSize.y / 2f + BinSize.y / 2f + 20f), BinSize);
            UiFactory.Label("BinText", bin, "쓰레기통", 40, Color.white, TextAnchor.MiddleCenter);

            // 머리카락 (쓰레기통과 겹치지 않게 흩뿌리기)
            for (int i = 0; i < HairCount; i++)
            {
                Vector2 pos;
                int guard = 0;
                do
                {
                    pos = new Vector2(
                        UnityEngine.Random.Range(-PadSize.x / 2f + 60f, PadSize.x / 2f - 60f),
                        UnityEngine.Random.Range(-PadSize.y / 2f + 60f, PadSize.y / 2f - 60f));
                    guard++;
                } while (InBin(pos, 60f) && guard < 50);

                Image h = UiFactory.Panel("Hair", pad, new Color(0.15f, 0.10f, 0.08f));
                Art.Apply(h, "hair_clump");
                h.raycastTarget = false;
                SetCenter(h.rectTransform, pos, new Vector2(80f, 24f));
                h.rectTransform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 180f));
                hairs.Add(h.rectTransform);
            }

            // 빗자루 (누르고 있는 동안 손가락을 따라다님)
            Image b = UiFactory.Panel("Broom", pad, new Color(0.60f, 0.40f, 0.20f, 0.45f));
            Art.Apply(b, "glow");
            b.raycastTarget = false;
            broom = b.rectTransform;
            SetCenter(broom, Vector2.zero, new Vector2(BroomRadius * 2f, BroomRadius * 2f));
            broom.gameObject.SetActive(false);

            UpdateInfo();
        }

        void Update()
        {
            if (finished) return;

            elapsed += Time.deltaTime;
            barFill.anchorMax = new Vector2(Mathf.Clamp01(1f - elapsed / TimeLimit), 1f);
            UpdateInfo();

            if (elapsed >= TimeLimit) Finish(true);
        }

        // ---------- 입력 (SweepPad가 호출) ----------

        public void OnPress(PointerEventData e)
        {
            if (finished) return;
            if (ToLocal(e, out lastLocal))
            {
                dragging = true;
                broom.gameObject.SetActive(true);
                broom.anchoredPosition = lastLocal;
            }
        }

        public void OnMove(PointerEventData e)
        {
            if (finished || !dragging) return;

            Vector2 p;
            if (!ToLocal(e, out p)) return;

            Vector2 delta = p - lastLocal;
            lastLocal = p;
            broom.anchoredPosition = p;
            Sweep(p, delta);
        }

        public void OnRelease()
        {
            dragging = false;
            if (broom != null) broom.gameObject.SetActive(false);
        }

        bool ToLocal(PointerEventData e, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(pad, e.position, e.pressEventCamera, out local);
        }

        // ---------- 게임 규칙 ----------

        // 빗자루 반경 안의 머리카락을 손가락이 움직인 만큼 같이 밀기
        void Sweep(Vector2 broomPos, Vector2 delta)
        {
            float limitX = PadSize.x / 2f - 20f;
            float limitY = PadSize.y / 2f - 20f;

            for (int i = hairs.Count - 1; i >= 0; i--)
            {
                RectTransform h = hairs[i];
                Vector2 hp = h.anchoredPosition;
                if ((hp - broomPos).sqrMagnitude > BroomRadius * BroomRadius) continue;

                hp += delta;
                hp.x = Mathf.Clamp(hp.x, -limitX, limitX);
                hp.y = Mathf.Clamp(hp.y, -limitY, limitY);
                h.anchoredPosition = hp;

                if (InBin(hp, 0f))
                {
                    hairs.RemoveAt(i);
                    Destroy(h.gameObject);
                    cleaned++;
                }
            }

            if (hairs.Count == 0) Finish(false);
        }

        bool InBin(Vector2 pos, float margin)
        {
            Vector2 c = bin.anchoredPosition;
            return Mathf.Abs(pos.x - c.x) <= BinSize.x / 2f + margin
                && Mathf.Abs(pos.y - c.y) <= BinSize.y / 2f + margin;
        }

        void Finish(bool timeUp)
        {
            if (finished) return;
            finished = true;

            SweepResult result = new SweepResult
            {
                cleaned = cleaned,
                total = HairCount,
                elapsed = elapsed,
                timeUp = timeUp,
            };
            if (onFinished != null) onFinished(result);
            Destroy(gameObject);
        }

        void UpdateInfo()
        {
            float left = Mathf.Max(0f, TimeLimit - elapsed);
            infoText.text = "치운 머리카락 " + cleaned + " / " + HairCount + "      남은 시간 " + left.ToString("0.0") + "초";
        }

        static void SetCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }

    // 바닥 영역에 붙어서 누르기/드래그/떼기를 FloorSweepGame에 전달한다.
    public class SweepPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public FloorSweepGame game;

        public void OnPointerDown(PointerEventData e) { game.OnPress(e); }
        public void OnDrag(PointerEventData e) { game.OnMove(e); }
        public void OnPointerUp(PointerEventData e) { game.OnRelease(); }
    }
}
