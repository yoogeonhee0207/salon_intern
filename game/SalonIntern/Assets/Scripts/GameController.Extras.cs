using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // 손님 성격, 암행 손님, 선생님 이벤트, 원장 한마디, 졸업 연출, 업적, 오늘의 목표, 주간 평가, 상점 확장.
    // 본 파일(GameController.cs)에서는 연결 지점만 부른다.
    public partial class GameController
    {
        // 테스트용: true면 암행 손님이 항상 오고, 이벤트 기준이 낮아지고, 주간 평가가 매일 나오고, 포인트를 지급한다.
        // 확인이 끝나면 false로 돌릴 것!
        const bool ExtrasTest = false;
        bool testGiftGiven, gradPreviewShown;

        // ---------- 설정 화면 (소리 크기, 일시정지) ----------

        RectTransform settingsPanel;

        void OpenSettings()
        {
            if (settingsPanel != null) return;
            bool titleUp = titlePanel != null;
            bool pausing = !titleUp && phase == Phase.Business;
            if (pausing) Time.timeScale = 0f;

            Image dim = UiFactory.Panel("SettingsDim", canvasRoot, new Color(0f, 0f, 0f, 0.65f));
            UiFactory.Stretch(dim.rectTransform, 0f, 0f, 1f, 1f);
            settingsPanel = dim.rectTransform;
            settingsPanel.SetAsLastSibling();

            Image box = UiFactory.Panel("SettingsBox", settingsPanel, new Color(0.96f, 0.93f, 0.88f));
            UiFactory.Stretch(box.rectTransform, 0.30f, 0.14f, 0.70f, 0.86f);

            Text head = UiFactory.Label("SettingsTitle", box.transform, pausing ? "일시정지" : "설정", 44, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(head.rectTransform, 0f, 0.84f, 1f, 0.97f);

            Button sound = UiFactory.MakeButton("SoundToggle", box.transform, SoundLabel(), 32, new Color(0.40f, 0.50f, 0.45f), null);
            sound.onClick.AddListener(() => { Sfx.Toggle(); sound.GetComponentInChildren<Text>().text = SoundLabel(); });
            UiFactory.Stretch(sound.GetComponent<RectTransform>(), 0.12f, 0.66f, 0.88f, 0.80f);

            Text volLabel = UiFactory.Label("VolumeLabel", box.transform, VolumeLabel(), 32, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(volLabel.rectTransform, 0.34f, 0.48f, 0.66f, 0.62f);
            Button volDown = UiFactory.MakeButton("VolDown", box.transform, "-", 40, new Color(0.45f, 0.45f, 0.55f), null);
            volDown.onClick.AddListener(() => { Sfx.SetVolume(Sfx.Volume - 0.1f); volLabel.text = VolumeLabel(); Sfx.Play(Sfx.Kind.Click); });
            UiFactory.Stretch(volDown.GetComponent<RectTransform>(), 0.12f, 0.48f, 0.32f, 0.62f);
            Button volUp = UiFactory.MakeButton("VolUp", box.transform, "+", 40, new Color(0.30f, 0.60f, 0.45f), null);
            volUp.onClick.AddListener(() => { Sfx.SetVolume(Sfx.Volume + 0.1f); volLabel.text = VolumeLabel(); Sfx.Play(Sfx.Kind.Click); });
            UiFactory.Stretch(volUp.GetComponent<RectTransform>(), 0.68f, 0.48f, 0.88f, 0.62f);

            Button close = UiFactory.MakeButton("SettingsClose", box.transform, pausing ? "계속하기" : "닫기", 36, new Color(0.30f, 0.60f, 0.45f), CloseSettings);
            UiFactory.Stretch(close.GetComponent<RectTransform>(), 0.12f, 0.28f, 0.88f, 0.42f);

            // 시작 화면으로 나가기는 영업 중이 아닐 때만 (아침, 저녁에만)
            if (!titleUp && phase != Phase.Business)
            {
                Button toTitleBtn = UiFactory.MakeButton("SettingsToTitle", box.transform, "시작 화면으로", 28, new Color(0.40f, 0.45f, 0.55f), () => { CloseSettings(); ShowTitle(); });
                UiFactory.Stretch(toTitleBtn.GetComponent<RectTransform>(), 0.12f, 0.10f, 0.88f, 0.22f);
            }
            else if (pausing)
            {
                Text note = UiFactory.Label("SettingsNote", box.transform, "영업 중에는 시간이 멈춰요", 24, new Color(0.45f, 0.42f, 0.40f), TextAnchor.MiddleCenter);
                UiFactory.Stretch(note.rectTransform, 0.05f, 0.10f, 0.95f, 0.22f);
            }
        }

        void CloseSettings()
        {
            if (settingsPanel != null) Destroy(settingsPanel.gameObject);
            settingsPanel = null;
            Time.timeScale = 1f;
        }

        string VolumeLabel() { return "음량 " + Mathf.RoundToInt(Sfx.Volume * 100f) + "%"; }

        string SoundLabel() { return Sfx.Muted ? "소리 꺼짐" : "소리 켜짐"; }

        // ---------- 공통: 포인트 ----------

        int totalPointsEarned;

        void AddPoints(int n)
        {
            if (n <= 0) return;
            points += n;
            totalPointsEarned += n;
        }

        // ---------- 손님 성격과 대사, 암행 손님 ----------

        static readonly string[] CustomerTypes = { "일반", "까다로운", "수다쟁이", "아이" };
        static readonly string[] Surnames = { "김", "이", "박", "최", "정", "한", "송", "조", "윤", "강" };
        static readonly string[][] GreetingLines =
        {
            new[] { "안녕하세요, 잘 부탁드려요.", "예약한 손님이에요.", "오늘은 어떻게 해 볼까요?" },
            new[] { "저번엔 마음에 안 들었는데, 이번엔 잘 해 주세요.", "시간 없으니까 빨리 부탁해요.", "꼼꼼하게 해 주셔야 해요." },
            new[] { "어머, 새로 온 분이세요? 요즘 날씨가 참 그렇죠?", "있잖아요, 어제 글쎄…", "말 걸어도 괜찮죠?" },
            new[] { "엄마가 머리 예쁘게 해 달래요!", "가위 소리 무서워요…", "저도 미용사 할 거예요!" },
        };
        static readonly string[] ReactionGood = { "와, 마음에 쏙 들어요!", "손길이 좋네요. 또 올게요!", "딱 원하던 느낌이에요." };
        static readonly string[] ReactionOk = { "나쁘지 않네요.", "괜찮은 것 같아요.", "음, 무난하네요." };
        static readonly string[] ReactionBad = { "음… 조금 아쉬운데요.", "다음엔 더 신경 써 주세요.", "별로예요…" };

        bool inspectorToday;
        string lastReaction = "";
        int satSCount;       // 만족도 S 시술 누적
        int inspectorPassCount;

        void AssignCustomer(Request r)
        {
            if (!r.isProcedure || r.isMess || r.isLocked) return;

            float roll = Random.value;
            int type = roll < 0.55f ? 0 : (roll < 0.75f ? 1 : (roll < 0.90f ? 2 : 3));
            if (ExtrasTest && !inspectorToday) type = 0;
            r.customerType = type;
            string suffix = type == 3 ? " 어린이" : (type == 1 ? " 여사님" : "씨");
            r.customerName = Surnames[Random.Range(0, Surnames.Length)] + suffix;
            string[] lines = GreetingLines[type];
            r.customerLine = lines[Random.Range(0, lines.Length)];

            if (type == 1) r.timeLimit *= 0.85f;       // 까다로운 손님은 기다려 주지 않는다
            else if (type == 2) r.timeLimit *= 1.15f;  // 수다쟁이 손님은 느긋하다
            r.remaining = r.timeLimit;

            // 암행 손님: 평범한 손님인 척한다 (하루 최대 한 명, 3일째부터)
            if ((ExtrasTest || day >= 3) && !inspectorToday && type == 0 && Random.value < (ExtrasTest ? 1f : 0.15f))
            {
                r.isInspector = true;
                inspectorToday = true;
            }
        }

        string CustomerGreeting(Request r)
        {
            if (r.customerName == null) return r.designer.name + " 선생님의 손님을 모셔요";
            return CustomerTypes[r.customerType] + " 손님 " + r.customerName + ": \"" + r.customerLine + "\"";
        }

        // 시술 완료 때 불러서 손님 반응, 보너스 점수를 돌려준다
        int OnRequestCompleted(Request r, int sat)
        {
            if (sat >= 85) satSCount++;
            if (sat >= 60) dayGoodSat++;

            int bonus = 0;
            string[] pool = sat >= 85 ? ReactionGood : (sat >= 60 ? ReactionOk : ReactionBad);
            string reaction = r.customerName != null ? r.customerName + ": \"" + pool[Random.Range(0, pool.Length)] + "\"" : "";

            if (r.customerName != null)
            {
                if (r.customerType == 1) bonus += sat >= 85 ? 12 : (sat < 60 ? -8 : 0);   // 까다로운 손님: 잘하면 큰 보너스, 못하면 감점
                else if (r.customerType == 2) bonus += 3;                                      // 수다쟁이 손님: 팁
                else if (r.customerType == 3 && sat >= 60) AddCloseness(r.designer, 1f);       // 아이 손님: 선생님이 흐뭇해함
            }

            if (r.isInspector)
            {
                if (sat >= 60)
                {
                    bonus += 40;
                    inspectorPassCount++;
                    reaction = "본사 암행 점검 통과! 평가 점수 +40";
                    achieveQueue.Add("암행 손님이 만족했어요!");
                }
                else
                {
                    bonus -= 20;
                    reaction = "그 손님은 본사 암행 점검이었어요... 평가 점수 -20";
                }
            }

            lastReaction = reaction;
            CheckAchievements();
            return bonus;
        }

        // ---------- 선생님 친분 이벤트 ----------

        readonly HashSet<int> eventSeen = new HashSet<int>();
        static readonly int[] EventThresholds = ExtrasTest ? new[] { 1, 2, 3 } : new[] { 25, 50, 80 };
        int snackCount;      // 휴게실 간식 (다음 휴식 회복 1.3배)
        int buffDays;        // 남은 "피로도 천천히" 일수

        float FatigueMult()
        {
            return LevelConfig.FatigueScale[level] * (buffDays > 0 ? 0.8f : 1f);
        }

        float ApplySnack(float amount)
        {
            if (snackCount <= 0) return amount;
            snackCount--;
            Say("간식을 먹었어요! 회복량 1.3배 (남은 간식 " + snackCount + "개)");
            return amount * 1.3f;
        }

        void CheckDesignerEvents()
        {
            var pending = new List<KeyValuePair<DesignerData, int>>();
            foreach (DesignerData d in designers)
            {
                float c = Closeness(d);
                for (int st = 0; st < EventThresholds.Length; st++)
                {
                    if (c >= EventThresholds[st] && !eventSeen.Contains(d.id * 10 + st))
                    {
                        eventSeen.Add(d.id * 10 + st);
                        pending.Add(new KeyValuePair<DesignerData, int>(d, st));
                        break; // 한 번에 한 단계씩
                    }
                }
            }
            if (pending.Count > 0) ShowEventAt(pending, 0);
            else if (ExtrasTest && !gradPreviewShown)
            {
                gradPreviewShown = true;
                ShowDialog("졸업 연출 (미리보기)", GraduationPages(new ExamResult { hits = 9, rounds = 10 }), "확인", () => { UpdateMorningTexts(); });
            }
        }

        void ShowEventAt(List<KeyValuePair<DesignerData, int>> list, int index)
        {
            if (index >= list.Count) { UpdateMorningTexts(); SaveGame(); return; }
            DesignerData d = list[index].Key;
            int stage = list[index].Value;
            var pages = new List<string>();
            string title;
            string reward;

            if (stage == 0)
            {
                title = d.name + " 선생님의 비법";
                pages.Add(d.name + " 선생님이 다가와요.\n\n\"요즘 열심히 하네. 내가 아끼는 팁 하나 알려줄게.\"");
                pages.Add("\"문지르는 작업은 천천히 큼직하게 하는 게 훨씬 잘 돼. 너무 쪼잔하게 하면 손님이 불안해해.\"");
                AddPoints(20);
                reward = "포인트 +20";
            }
            else if (stage == 1)
            {
                title = d.name + " 선생님의 간식";
                pages.Add(d.name + " 선생님이 휴게실에서 손짓해요.\n\n\"이거 먹고 힘내. 쉬는 것도 일이야.\"");
                snackCount++;
                reward = "휴게실 간식 +1 (다음 휴식 회복 1.3배)";
            }
            else
            {
                title = d.name + " 선생님의 추천";
                pages.Add(d.name + " 선생님이 원장님께 " + playerName + "님 이야기를 했대요.\n\n\"쟤 물건이에요. 제가 보장해요.\"");
                pages.Add("원장님이 " + playerName + "님을 지켜보기 시작했어요.");
                AddPoints(100);
                buffDays = 3;
                reward = "포인트 +100, 3일간 피로도가 천천히 쌓여요";
            }
            Say(d.name + " 선생님과 친해졌어요! " + reward);
            ShowDialog(title, pages, "받기 (" + reward + ")", () => ShowEventAt(list, index + 1));
        }

        // ---------- 원장 한마디와 졸업 연출 ----------

        static readonly string[][] DirectorLines =
        {
            new[] { "오늘은 쉬엄쉬엄 하자. 내일은 더 잘할 수 있어.", "누구나 처음엔 그래. 포기하지 마.", "수건부터 다시 접어 볼까?" },
            new[] { "나쁘지 않았어. 조금만 더 집중해 봐.", "손님들이 기다린 시간을 생각해 보자.", "기본기를 다져. 그게 제일 빨라." },
            new[] { "잘했어. 선생님들도 칭찬하더라.", "오늘 같은 날이 쌓이면 가위를 잡게 될 거야.", "좋은 하루였어." },
            new[] { "…", "오늘은 정말 훌륭했어. 내일도 이렇게만.", "인턴이 아니라 벌써 디자이너 같네." },
        };

        string DirectorLine(string grade)
        {
            int g = grade == "S" ? 3 : (grade == "A" ? 2 : (grade == "B" ? 1 : 0));
            string[] pool = DirectorLines[g];
            return "\n원장님: \"" + pool[Random.Range(0, pool.Length)] + "\"";
        }

        List<string> GraduationPages(ExamResult result)
        {
            DesignerData best = null;
            float bestC = -1f;
            foreach (DesignerData d in designers)
            {
                float c = Closeness(d);
                if (c > bestC) { bestC = c; best = d; }
            }
            string friend = best != null ? best.name + " 선생님" : "선생님들";

            return new List<string>
            {
                "졸업시험 합격!\n\n원장님이 지켜보는 가운데, " + playerName + "님의 가위가 머리카락 사이를 지나가요.\n(" + result.hits + "/" + result.rounds + ")",
                "원장님이 천천히 고개를 끄덕여요.\n\n\"" + playerName + ". 수건 접던 막내가 여기까지 왔구나.\"",
                friend + "이(가) 눈시울을 붉히며 다가와요.\n\n\"처음 왔을 때가 어제 같은데... 졸업 축하해.\"",
                salonName + "에 새 막내가 들어왔어요.\n\n수건을 끌어안고 허둥대는 모습이 낯설지 않아요. " + playerName + "님은 가만히 웃으며 가위를 건네요.",
                "인턴 편 끝!\n\n수건 접던 막내는 이제 가위를 잡았어요.\n\n다음은 디자이너 과정이에요. (준비 중)",
            };
        }

        // ---------- 업적 ----------

        static readonly string[] AchNames =
        {
            "첫 출근", "첫 시술", "S등급 달인", "일주일 근무", "친해지기", "견학 완주",
            "첫 승급", "졸업", "포인트 부자", "완벽한 하루", "암행 손님 통과", "목표 달인",
        };
        static readonly string[] AchDescs =
        {
            "하루 영업을 마쳐요", "시술을 한 번 끝내요", "만족도 S 시술 10번", "7일 동안 출근해요", "선생님 한 명과 친분 60 이상", "시술 6종 견학을 모두 마쳐요",
            "Lv.1이 돼요", "졸업시험에 합격해요", "포인트를 누적 500 모아요", "하루 평가 S를 받아요", "암행 손님을 만족시켜요", "오늘의 목표를 5번 달성해요",
        };
        static readonly int[] AchRewards = { 10, 10, 50, 50, 30, 20, 20, 200, 50, 30, 50, 40 };

        readonly bool[] achieved = new bool[12];
        readonly List<string> achieveQueue = new List<string>();
        int daysPlayed;
        int perfectDays;
        int goalsDone;

        bool AchCondition(int i)
        {
            switch (i)
            {
                case 0: return daysPlayed >= 1;
                case 1: return done >= 1;
                case 2: return satSCount >= 10;
                case 3: return daysPlayed >= 7;
                case 4:
                    foreach (DesignerData d in designers) if (Closeness(d) >= 60f) return true;
                    return false;
                case 5:
                    foreach (bool t in tourSeen) if (!t) return false;
                    return true;
                case 6: return level >= 1;
                case 7: return graduated;
                case 8: return totalPointsEarned >= 500;
                case 9: return perfectDays >= 1;
                case 10: return inspectorPassCount >= 1;
                case 11: return goalsDone >= 5;
            }
            return false;
        }

        string CheckAchievements()
        {
            string text = "";
            for (int i = 0; i < achieved.Length; i++)
            {
                if (achieved[i] || !AchCondition(i)) continue;
                achieved[i] = true;
                AddPoints(AchRewards[i]);
                achieveQueue.Add("★ 업적 달성: " + AchNames[i] + "  (포인트 +" + AchRewards[i] + ")");
                text += "★ 업적 달성: " + AchNames[i] + " (포인트 +" + AchRewards[i] + ")\n";
            }
            return text;
        }

        // 업적 알림은 다른 알림이 끝난 뒤 하나씩 띄운다
        void FlushAchieveToast()
        {
            if (achieveQueue.Count == 0 || toastPanel == null || toastTimer > 0f || phase != Phase.Business) return;
            Toast(achieveQueue[0], true);
            Sfx.Play(Sfx.Kind.Coin);
            achieveQueue.RemoveAt(0);
        }

        RectTransform achPanel;

        void OpenAchievements()
        {
            if (achPanel != null) Destroy(achPanel.gameObject);
            Image bg = UiFactory.Panel("AchPanel", canvasRoot, new Color(0.96f, 0.93f, 0.88f));
            UiFactory.Stretch(bg.rectTransform, 0.10f, 0.04f, 0.90f, 0.96f);
            achPanel = bg.rectTransform;

            int count = 0;
            foreach (bool a in achieved) if (a) count++;
            Text title = UiFactory.Label("AchTitle", achPanel, "업적  " + count + " / " + achieved.Length, 40, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(title.rectTransform, 0f, 0.91f, 1f, 0.99f);

            for (int i = 0; i < achieved.Length; i++)
            {
                float yMax = 0.90f - i * 0.0715f;
                float yMin = yMax - 0.066f;
                Image row = UiFactory.Panel("Ach" + i, achPanel, achieved[i] ? new Color(0.80f, 0.92f, 0.80f) : new Color(0.88f, 0.86f, 0.84f));
                UiFactory.Stretch(row.rectTransform, 0.03f, yMin, 0.97f, yMax);
                Text t = UiFactory.Label("Text", row.transform, (achieved[i] ? "✔ " : "□ ") + AchNames[i] + "  -  " + AchDescs[i] + "   (+" + AchRewards[i] + "P)", 26, TextDark, TextAnchor.MiddleLeft);
                t.rectTransform.offsetMin = new Vector2(16f, 0f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            Button close = UiFactory.MakeButton("AchClose", achPanel, "닫기", 30, new Color(0.30f, 0.60f, 0.45f), () => { Destroy(achPanel.gameObject); achPanel = null; });
            UiFactory.Stretch(close.GetComponent<RectTransform>(), 0.38f, 0.005f, 0.62f, 0.05f);
        }

        // ---------- 오늘의 목표 ----------

        int goalType = -1;   // -1 없음, 0 만족도 A 이상 N건, 1 놓침 없이, 2 재료 안 떨어뜨리기
        int goalCount = 2;
        int dayGoodSat;
        int dayScolds;
        const int GoalReward = 15;

        void RollDailyGoal()
        {
            dayGoodSat = 0;
            dayScolds = 0;
            inspectorToday = false;
            if (TourDay) { goalType = -1; return; }
            goalType = Random.Range(0, 3);
            goalCount = level >= 3 ? 3 : 2;
        }

        string GoalText()
        {
            if (goalType == 0) return "만족도 A 이상 시술 " + goalCount + "건 (포인트 +" + GoalReward + ")";
            if (goalType == 1) return "요청을 하나도 놓치지 않기 (포인트 +" + GoalReward + ")";
            if (goalType == 2) return "재료가 떨어져서 혼나지 않기 (포인트 +" + GoalReward + ")";
            return "";
        }

        string GoalLine()
        {
            return goalType >= 0 ? "\n오늘의 목표: " + GoalText() : "";
        }

        // ---------- 하루 끝: 목표 평가, 주간 평가, 업적 ----------

        int weekScore;
        int weekTarget;

        string OnDayEnd(string grade, int dayScore, int target, int dayMissedNow)
        {
            string text = "";
            Sfx.Play(Sfx.Kind.Fanfare);
            daysPlayed++;
            if (grade == "S") perfectDays++;

            // 오늘의 목표
            if (goalType >= 0)
            {
                bool ok = goalType == 0 ? dayGoodSat >= goalCount : (goalType == 1 ? dayMissedNow == 0 : dayScolds == 0);
                if (ok)
                {
                    goalsDone++;
                    AddPoints(GoalReward);
                    text += "오늘의 목표 달성!  포인트 +" + GoalReward + "\n";
                }
                else text += "오늘의 목표 실패: " + GoalText() + "\n";
            }

            // 주간 평가: 7일마다 본사 평가
            weekScore += dayScore;
            weekTarget += target;
            if (ExtrasTest || day % 7 == 0)
            {
                string wg = DayConfig.Grade(weekScore, weekTarget);
                int wp = wg == "S" ? 100 : (wg == "A" ? 60 : (wg == "B" ? 30 : 10));
                AddPoints(wp);
                text += "★ 주간 본사 평가 " + wg + "등급!  포인트 +" + wp + "\n";
                weekScore = 0;
                weekTarget = 0;
            }

            if (buffDays > 0) buffDays--;
            text += CheckAchievements();
            return text;
        }

        // 아침이 시작될 때: 오늘의 목표 뽑기, 모양 적용, 이벤트와 업적 확인
        void OnMorningStart()
        {
            RollDailyGoal();
            RefreshChairs();
            if (ExtrasTest && !testGiftGiven)
            {
                testGiftGiven = true;
                AddPoints(300);
                if (designers != null && designers.Length > 1) { AddCloseness(designers[0], 3f); AddCloseness(designers[1], 3f); }
            }
            ApplyLook();
            CheckAchievements();
            if (designers != null && designers.Length > 0 && !TourDay) CheckDesignerEvents();
        }

        // ---------- 시술석 의자 ----------

        // 시술석에는 의자가 있고 의자 하나에 손님 한 명만 앉는다. 레벨이 오르면 의자가 늘어난다.
        const int MaxChairs = 3;
        static readonly float[] ChairY = { 30f, -55f, -140f };
        readonly Request[] chairOwner = new Request[MaxChairs];
        readonly Image[] chairImages = new Image[MaxChairs];
        readonly Text[] chairTexts = new Text[MaxChairs];
        Request chairWaiter;   // 의자가 나기를 기다리는 손님의 요청

        int ChairCount { get { return level >= 3 ? 3 : 2; } }

        Vector2 ChairPos(int i) { return new Vector2(775f, ChairY[Mathf.Clamp(i, 0, MaxChairs - 1)]); }

        int FreeChairs()
        {
            int n = 0;
            for (int i = 0; i < ChairCount; i++) if (chairOwner[i] == null) n++;
            return n;
        }

        // 이 요청의 idx번째 단계가 시술석이면 의자를 잡는다. 시술석이 아니거나 이미 앉았으면 true
        bool ReserveFor(Request r, int idx)
        {
            if (r.route == null || StepStation[r.route[idx]] != 1) return true;
            if (r.chair >= 0) return true;
            for (int i = 0; i < ChairCount; i++)
            {
                if (chairOwner[i] != null) continue;
                chairOwner[i] = r;
                r.chair = i;
                RefreshChairs();
                return true;
            }
            return false;
        }

        void ReleaseChair(Request r)
        {
            if (r == null || r.chair < 0) return;
            if (r.chair < MaxChairs && chairOwner[r.chair] == r) chairOwner[r.chair] = null;
            r.chair = -1;
            RefreshChairs();
        }

        void ResetChairs()
        {
            for (int i = 0; i < MaxChairs; i++) chairOwner[i] = null;
            foreach (Request q in active) q.chair = -1;
            chairWaiter = null;
            RefreshChairs();
        }

        void BuildChairs()
        {
            for (int i = 0; i < MaxChairs; i++)
            {
                Image c = UiFactory.Panel("Chair" + i, mapArea, new Color(0.95f, 0.85f, 0.70f));
                PlaceCenter(c.rectTransform, ChairPos(i), new Vector2(200f, 70f));
                chairImages[i] = c;
                chairTexts[i] = UiFactory.Label("Text", c.transform, "", 22, TextDark, TextAnchor.MiddleCenter);
            }
            RefreshChairs();
        }

        void RefreshChairs()
        {
            for (int i = 0; i < MaxChairs; i++)
            {
                if (chairImages[i] == null) continue;
                bool open = i < ChairCount;
                bool taken = open && chairOwner[i] != null;
                chairImages[i].color = !open ? new Color(0.80f, 0.80f, 0.82f) : (taken ? new Color(0.78f, 0.55f, 0.38f) : new Color(0.97f, 0.90f, 0.78f));
                chairTexts[i].text = !open ? "의자 (Lv.3~)" : (taken ? "손님이 앉아 있어요" : "빈 의자");
            }
        }

        // 의자가 날 때까지 기다리던 손님을 자리가 나면 데려간다
        void UpdateChairWait()
        {
            if (chairWaiter == null || phase != Phase.Business || minigameActive) return;
            Request task = chairWaiter;
            if (!active.Contains(task) || playerTask != task) { chairWaiter = null; return; }
            if (!ReserveFor(task, task.routeIdx)) return;
            chairWaiter = null;
            playerTarget = StepPos(task, task.routeIdx);
            playerWalking = true;
            Say("의자가 비었어요! " + StepNames[task.route[task.routeIdx]] + "하러 모셔요");
        }

        // ---------- 시술 중 대기 (염색약이 작용하는 시간 등) ----------

        // 단계를 끝낸 뒤 손님이 의자에 앉아서 기다리는 시간 (게임 속 분). 샴푸 0, 펌 1, 염색 2, 드라이 3, 커트 4, 탈색 5
        static readonly int[] StepWaitMin = { 0, 20, 15, 0, 0, 20 };

        float SecPerMinute { get { return DayConfig.BusinessSeconds / (float)(DayConfig.CloseMinutes - DayConfig.OpenMinutes); } }

        void BeginProcessing(Request task, int step)
        {
            int wait = StepWaitMin[step];
            task.processing = true;
            task.ready = false;
            task.procTotal = wait * SecPerMinute;
            task.procEnd = businessElapsed + task.procTotal;
            if (customerRt != null) { task.seated = customerRt; customerRt = null; }
            carrying = false;
            customerReturning = false;
            playerTask = null;
            playerWalking = false;
            waitingAtStation = false;
            task.background.color = new Color(0.80f, 0.88f, 0.98f);
            Say(StepNames[step] + " 약이 작용하는 중이에요 (" + wait + "분). 그동안 다른 일을 해요!");
        }

        // 대기 시간 보상과 방치 벌점 (게임 분)
        const float OverGraceMin = 10f;  // 대기가 끝난 뒤 이만큼은 괜찮음 (게임 분, 약 5초)
        const int WaitUseBonus = 6;      // 대기 중 다른 일을 하나 끝낼 때마다 보너스 점수
        const int WaitUseMax = 3;        // 보너스는 최대 3건까지

        // 어떤 일을 끝냈을 때, 지금 대기 중인 다른 손님들의 "대기 활용" 횟수를 올린다
        void CountWaitUse(Request finished)
        {
            for (int i = 0; i < active.Count; i++)
            {
                Request o = active[i];
                if (o != finished && o.processing) o.multi++;
            }
        }

        float OverMinutes(Request r)
        {
            if (!r.ready) return 0f;
            return Mathf.Max(0f, (businessElapsed - r.readyAt) / SecPerMinute - OverGraceMin);
        }

        void UpdateProcessing()
        {
            for (int i = 0; i < active.Count; i++)
            {
                Request r = active[i];
                if (!r.processing) continue;
                float left = r.procEnd - businessElapsed;
                int step = r.route[r.routeIdx - 1];
                if (left <= 0f)
                {
                    r.processing = false;
                    r.ready = true;
                    r.readyAt = businessElapsed;
                    r.bodyText.text = r.titleBase + "\n✔ 대기 끝!";
                    r.background.color = new Color(0.78f, 0.93f, 0.78f);
                    r.barFill.anchorMax = new Vector2(1f, 1f);
                    r.barFillImage.color = BarGreen;
                    Toast("✔ " + r.designer.name + " 선생님 손님의 " + StepNames[step] + " 대기가 끝났어요! 모시러 가요", true);
                    continue;
                }
                int leftMin = Mathf.CeilToInt(left / SecPerMinute);
                r.bodyText.text = r.titleBase + "\n⏱ " + StepNames[step] + " " + leftMin + "분";
                r.barFill.anchorMax = new Vector2(Mathf.Clamp01(1f - left / r.procTotal), 1f);
                r.barFillImage.color = new Color(0.35f, 0.60f, 0.90f);
            }
        }

        // 대기가 끝난 뒤 오래 두면 카드가 주황색으로 변하고 초과 시간을 보여준다
        void UpdateOverdue()
        {
            for (int i = 0; i < active.Count; i++)
            {
                Request r = active[i];
                if (!r.ready || r.seated == null) continue;
                float over = OverMinutes(r);
                if (over <= 0f) continue;
                int step = r.route[r.routeIdx - 1];
                r.bodyText.text = r.titleBase + "\n⚠ " + StepNames[step] + " 약 과해요 +" + Mathf.CeilToInt(over) + "분";
                r.background.color = new Color(0.98f, 0.82f, 0.60f);
                r.barFillImage.color = new Color(0.90f, 0.50f, 0.20f);
            }
        }

        // 손님 머리 위에 남은 시간을 보여주는 작은 표시
        void AddHeadLabel(RectTransform customer)
        {
            Image tag = UiFactory.Panel("Head", customer, new Color(1f, 1f, 1f, 0.9f));
            tag.raycastTarget = false;
            RectTransform rt = tag.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 46f);
            rt.sizeDelta = new Vector2(120f, 30f);
            Text t = UiFactory.Label("HeadText", tag.transform, "", 20, TextDark, TextAnchor.MiddleCenter);
            t.raycastTarget = false;
            tag.gameObject.SetActive(false);
        }

        void SetHeadLabel(RectTransform customer, string text, Color color)
        {
            if (customer == null) return;
            Transform tag = customer.Find("Head");
            if (tag == null) return;
            bool show = !string.IsNullOrEmpty(text);
            tag.gameObject.SetActive(show);
            if (!show) return;
            Text t = tag.GetComponentInChildren<Text>();
            t.text = text;
            t.color = color;
        }

        void UpdateHeadLabels()
        {
            Color dark = new Color(0.15f, 0.15f, 0.18f);
            Color red = new Color(0.80f, 0.15f, 0.15f);
            Color blue = new Color(0.15f, 0.35f, 0.75f);

            // 데리고 다니는 손님: 요청 제한시간(초)
            if (customerRt != null && carrying)
            {
                Request t = playerTask;
                if (t != null && active.Contains(t))
                {
                    int sec = Mathf.CeilToInt(Mathf.Max(0f, t.remaining));
                    SetHeadLabel(customerRt, "남은 " + sec + "초", sec <= 10 ? red : dark);
                }
                else SetHeadLabel(customerRt, "", dark);
            }

            // 의자에 앉은 손님: 약이 작용하는 남은 분
            foreach (Request r in active)
            {
                if (r.seated == null) continue;
                if (r.processing)
                {
                    int left = Mathf.CeilToInt((r.procEnd - businessElapsed) / SecPerMinute);
                    SetHeadLabel(r.seated, "⏱ " + Mathf.Max(0, left) + "분", blue);
                }
                else if (r.ready)
                {
                    float over = OverMinutes(r);
                    if (over > 0f) SetHeadLabel(r.seated, "⚠ +" + Mathf.CeilToInt(over) + "분", new Color(0.80f, 0.35f, 0.05f));
                    else SetHeadLabel(r.seated, "대기 끝!", new Color(0.10f, 0.50f, 0.20f));
                }
                else SetHeadLabel(r.seated, "", dark);
            }
        }

        // 의자에 도착해서 앉은 손님을 데리고 다음 단계로 간다
        void FinishPickup(Request task)
        {
            task.pickup = false;

            // 대기 시간을 알뜰하게 썼으면 보너스, 너무 오래 방치했으면 만족도 깎임
            float over = OverMinutes(task);
            string settle = "";
            if (task.multi > 0)
            {
                int bonus = WaitUseBonus * Mathf.Min(task.multi, WaitUseMax);
                score += bonus;
                settle += "  대기 시간 활용! +" + bonus + "점";
            }
            if (over > 0f)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(100f - over * 4f), 20, 100);
                task.satSum += pct;
                task.satCount++;
                settle += "  약이 과했어요 (만족도 하락)";
            }
            task.multi = 0;
            if (settle.Length > 0) Toast((over > 0f ? "⚠ " : "✔ ") + task.designer.name + " 선생님 손님" + settle, over <= 0f);
            RefreshTop();

            task.ready = false;
            task.bodyText.text = task.titleBase;
            task.background.color = task.baseColor;
            if (StepStation[task.route[task.routeIdx]] != 1) ReleaseChair(task);
            if (task.seated != null) { customerRt = task.seated; task.seated = null; carrying = true; }
            else PickUpCustomer();
            ReserveFor(task, task.routeIdx);
            playerTarget = StepPos(task, task.routeIdx);
            playerWalking = true;
            Say("다음은 " + StepNames[task.route[task.routeIdx]]);
        }

        void DestroySeated(Request r)
        {
            if (r != null && r.seated != null) Destroy(r.seated.gameObject);
            if (r != null) r.seated = null;
        }

        // ---------- 상점 확장: 유니폼, 인테리어, 간식 ----------

        Image playerImage;
        Image corridorImage;
        Image floorImage;

        void ApplyLook()
        {
            if (playerImage != null) playerImage.color = CursorSkins.MainColor(6);
            if (floorImage != null) floorImage.color = CursorSkins.MainColor(7);
            if (corridorImage != null) corridorImage.color = CursorSkins.AccentColor(7);
        }

        const int SnackCost = 40;

        void BuildSnackRow(RectTransform panel, float yMin, float yMax)
        {
            Text name = UiFactory.Label("SnackName", panel, "휴게실 간식", 30, TextDark, TextAnchor.MiddleCenter);
            UiFactory.Stretch(name.rectTransform, 0.01f, yMin, 0.16f, yMax);
            Button b = UiFactory.MakeButton("Snack", panel, "간식 사기  " + SnackCost + "P\n(보유 " + snackCount + "개, 다음 휴식 회복 1.3배)", 22, new Color(0.55f, 0.45f, 0.40f), BuySnack);
            UiFactory.Stretch(b.GetComponent<RectTransform>(), 0.17f, yMin, 0.17f + 0.4f, yMax);
        }

        void BuySnack()
        {
            if (points < SnackCost) { Say("포인트가 부족해요. (" + SnackCost + "P 필요)"); return; }
            points -= SnackCost;
            snackCount++;
            OpenShop();
        }

        // ---------- 저장 연동 ----------

        void FillExtraSave(SaveData d)
        {
            d.totalPointsEarned = totalPointsEarned;
            d.satSCount = satSCount;
            d.inspectorPass = inspectorPassCount;
            d.daysPlayed = daysPlayed;
            d.perfectDays = perfectDays;
            d.goalsDone = goalsDone;
            d.snackCount = snackCount;
            d.buffDays = buffDays;
            d.weekScore = weekScore;
            d.weekTarget = weekTarget;
            d.achievements = (bool[])achieved.Clone();
            var seen = new List<int>(eventSeen);
            d.eventSeen = seen.ToArray();
        }

        void ApplyExtraSave(SaveData d)
        {
            totalPointsEarned = d.totalPointsEarned;
            satSCount = d.satSCount;
            inspectorPassCount = d.inspectorPass;
            daysPlayed = d.daysPlayed;
            perfectDays = d.perfectDays;
            goalsDone = d.goalsDone;
            snackCount = d.snackCount;
            buffDays = d.buffDays;
            weekScore = d.weekScore;
            weekTarget = d.weekTarget;
            if (d.achievements != null)
                for (int i = 0; i < achieved.Length && i < d.achievements.Length; i++) achieved[i] = d.achievements[i];
            eventSeen.Clear();
            if (d.eventSeen != null) foreach (int e in d.eventSeen) eventSeen.Add(e);
        }

        void ResetExtras()
        {
            totalPointsEarned = 0;
            satSCount = 0;
            inspectorPassCount = 0;
            daysPlayed = 0;
            perfectDays = 0;
            goalsDone = 0;
            snackCount = 0;
            buffDays = 0;
            weekScore = 0;
            weekTarget = 0;
            for (int i = 0; i < achieved.Length; i++) achieved[i] = false;
            eventSeen.Clear();
            achieveQueue.Clear();
        }
    }
}
