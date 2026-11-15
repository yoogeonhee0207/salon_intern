using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SalonIntern
{
    // 같이 일하는 인턴(NPC) 3명: 인턴장, 중간 인턴, 동기.
    // 인턴장이 새 요청을 나/인턴들에게 나눠주고, NPC는 맡은 요청의 선생님 자리로 가서 처리한다.
    public partial class GameController
    {
        class Npc
        {
            public int id;                 // 1~3 (요청의 assignee 값과 같다. 0은 플레이어)
            public string name, role;
            public Image uniformImage;   // 인턴 유니폼은 모두 같은 색(상점에서 고른 색)
            public float workMult;         // 작업 시간 배율 (작을수록 빠름)
            public int sat;                // 평균 솜씨(만족도 기준)
            public Vector2 home;
            public RectTransform rt;
            public Text label;
            public Request task;
            public int state;              // 0 대기, 1 이동 중, 2 작업 중
            public float left, total;
            public Vector2 target;
            public Vector2 wander;
            public float wanderT;
            public float waitT;            // 의자를 기다린 시간
            public int restFrom, restTo;   // 휴게 시간 (게임 시계의 분)
            public bool resting;
            public int refillKind = -1;    // 재료를 채우러 가는 중이면 재료 번호
            public int level;              // 동료 인턴의 레벨 (내 레벨에 따라 바뀐다)
            public float stamina = 100f;   // 스태미너: 일하면 줄고 휴게 중에는 찬다. 낮을수록 작업이 느려진다
            public bool forceRest;         // 내가 휴식을 보냈거나 스태미너가 0이 되어 쉬는 중 (가득 찰 때까지)
        }

        readonly List<Npc> npcs = new List<Npc>();
        Image playerRing;    // 내 캐릭터 발밑의 노란 빛
        Text playerTag;      // 내 캐릭터 머리 위의 "▼ 나" 표시
        const float NpcSpeed = 380f;
        const float HelpAfterSeconds = 4f;   // 내가 고른 선생님 일을 내가 이 시간 안에 시작하지 않으면, 한가한 인턴이 도와준다

        // 내 캐릭터를 눈에 띄게: 발밑 노란 빛 + 머리 위 표시
        void BuildPlayerMarker()
        {
            if (playerRt == null) return;
            Image ring = UiFactory.Panel("PlayerRing", mapArea, new Color(1f, 0.88f, 0.25f, 0.9f));
            ring.raycastTarget = false;
            if (!Art.Apply(ring, "glow")) ring.color = new Color(1f, 0.88f, 0.25f, 0.35f);
            PlaceCenter(ring.rectTransform, playerRt.anchoredPosition, new Vector2(170f, 170f));
            ring.transform.SetSiblingIndex(playerRt.GetSiblingIndex()); // 내 캐릭터 바로 뒤에 깔린다
            playerRing = ring;

            Text tag = UiFactory.Label("PlayerTag", mapArea, "▼ 나", 28, new Color(0.10f, 0.45f, 0.20f), TextAnchor.MiddleCenter);
            tag.raycastTarget = false;
            tag.fontStyle = FontStyle.Bold;
            tag.horizontalOverflow = HorizontalWrapMode.Overflow;
            PlaceCenter(tag.rectTransform, playerRt.anchoredPosition + new Vector2(0f, 78f), new Vector2(120f, 34f));
            playerTag = tag;
        }

        void UpdatePlayerMarker()
        {
            if (playerRt == null || playerRing == null) return;
            Vector2 p = playerRt.anchoredPosition;
            playerRing.rectTransform.anchoredPosition = p;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            playerRing.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            if (playerTag != null) playerTag.rectTransform.anchoredPosition = p + new Vector2(0f, 78f + Mathf.Sin(Time.time * 5f) * 4f);
        }

        void BuildNpcs()
        {
            BuildPlayerMarker();
            npcs.Clear();
            string[] names = { "정하윤", "오지후", "김나래" };
            string[] roles = { "인턴장", "오지후", "김나래" }; // 인턴장 말고는 이름으로 부른다
            Color uniformColor = CursorSkins.MainColor(6);
            float[] mult = { 0.7f, 1.0f, 1.5f };
            int[] sat = { 88, 75, 58 };
            Vector2[] homes = { new Vector2(320f, 0f), new Vector2(450f, 0f), new Vector2(580f, 0f) };

            for (int i = 0; i < 3; i++) // 같이 일하는 인턴 3명
            {
                Image me = Art.Figure("Npc_" + names[i], mapArea, uniformColor, "intern");
                me.raycastTarget = false;
                PlaceCenter(me.rectTransform, homes[i], new Vector2(104f, 104f));
                Image uni = UiFactory.Panel("Uniform", me.transform, Color.white);
                uni.raycastTarget = false;
                if (Art.Apply(uni, "intern_uniform"))
                {
                    UiFactory.Stretch(uni.rectTransform, 0f, 0f, 1f, 1f);
                    uni.preserveAspect = true;
                    uni.color = uniformColor;
                }
                else { Destroy(uni.gameObject); uni = null; }

                Text label = UiFactory.Label("NpcName", mapArea, roles[i], 17, InternNameColor, TextAnchor.MiddleCenter);
                AddNameOutline(label);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.raycastTarget = false;
                PlaceCenter(label.rectTransform, homes[i] + new Vector2(0f, 54f), new Vector2(200f, 26f));

                npcs.Add(new Npc
                {
                    id = i + 1, name = names[i], role = roles[i], uniformImage = uni, workMult = mult[i], sat = sat[i],
                    home = homes[i], rt = me.rectTransform, label = label,
                    restFrom = 11 * 60 + 30 + i * 60, restTo = 12 * 60 + 30 + i * 60, // 인턴장 11:30, 중간 인턴 12:30, 동기 13:30부터 1시간
                });
            }
        }

        void ApplyNpcUniform()
        {
            Color c = CursorSkins.MainColor(6);
            foreach (Npc n in npcs) if (n.uniformImage != null) n.uniformImage.color = c;
        }

        // 내 레벨에 맞춰 동료 인턴의 레벨, 속도, 솜씨를 정한다
        void ApplyNpcRoster()
        {
            EnsureInternTraits();
            int[] lv = LevelConfig.NpcLevels[Mathf.Clamp(level, 0, LevelConfig.NpcLevels.Length - 1)];
            for (int i = 0; i < npcs.Count && i < lv.Length; i++)
            {
                npcs[i].level = lv[i];
                npcs[i].workMult = 0.7f + 0.15f * (6 - lv[i]); // 레벨이 높을수록 빠르다 (Lv.6 0.7배 ~ Lv.3 1.15배)
                npcs[i].sat = 55 + 6 * lv[i];
                InternTrait tr = TraitOf(npcs[i]);
                if (tr != null) { npcs[i].workMult *= tr.work; npcs[i].sat = Mathf.Clamp(npcs[i].sat + tr.sat, 30, 99); }
            }
            // Lv.5부터는 내가 인턴장: 선배 인턴장이 졸업하고 그 자리에 새 막내가 들어온다
            if (npcs.Count > 0)
            {
                bool newbie = level >= 5;
                npcs[0].role = newbie ? "윤새봄" : "인턴장";
                npcs[0].name = newbie ? "윤새봄" : "정하윤";
            }
        }

        // 아침 화면용: 오늘 같이 일하는 인턴들의 레벨
        string NpcRosterText()
        {
            string[] roles = { level >= 5 ? "윤새봄" : "인턴장", "오지후", "김나래" }; // 인턴장 말고는 이름으로 부른다 (Lv.5부터는 내가 인턴장, 새 막내가 들어옴)
            int[] lv = LevelConfig.NpcLevels[Mathf.Clamp(level, 0, LevelConfig.NpcLevels.Length - 1)];
            string t = "";
            for (int i = 0; i < roles.Length && i < lv.Length; i++) t += (i > 0 ? "  ·  " : "") + roles[i] + " Lv." + lv[i];
            return t;
        }

        // 인턴이 먼저 맡을 일의 순서: 내가 아직 못 하는 단계 수(x1000) + 시술 점수. 클수록 어려운 시술
        int HardScore(Request r)
        {
            if (r.route == null) return 0;
            int hard = 0;
            foreach (int step in r.route) if (level < StepUnlock[step]) hard++;
            return hard * 1000 + StepScore(r);
        }

        string NpcTitle(Npc n) { return n.role + " Lv." + n.level; }

        bool NpcOnBreak(Npc n)
        {
            int m = ClockMinutes();
            return m >= n.restFrom && m < n.restTo;
        }

        bool AnyNpcOnBreak()
        {
            foreach (Npc n in npcs) if (n.resting) return true;
            return false;
        }

        Vector2 NpcSupplySpot(Npc n) { return SupplyPos + new Vector2(105f, (n.id - 2) * 45f); }

        // 다 떨어지기 전에 인턴이 재료를 채운다: 40% 이하이고 아무도 채우는 중이 아닌 재료 중 가장 모자란 것
        int NpcPickRefill()
        {
            int best = -1;
            float bestRatio = 0.4f;
            for (int k = 0; k < stock.Length; k++)
            {
                float ratio = stock[k] / (float)StockMax[k];
                if (ratio > bestRatio) continue;
                if (refilling && refillKind == k) continue;
                bool taken = false;
                foreach (Npc o in npcs) if (o.refillKind == k) taken = true;
                if (taken) continue;
                best = k;
                bestRatio = ratio;
            }
            return best;
        }

        Vector2 NpcRestSpot(Npc n) { return RestPos + new Vector2(105f, (n.id - 2) * 45f); }

        string WorkerName(int assignee)
        {
            if (assignee == 0) return "나";
            if (assignee >= 1 && assignee <= npcs.Count) return npcs[assignee - 1].role;
            return "";
        }

        int LoadOf(int who)
        {
            int n = 0;
            foreach (Request q in active) if (q.assignee == who) n++;
            return n;
        }

        int PlayerRequestCount()
        {
            int n = 0;
            foreach (Request q in active) if (!q.npcOnly) n++;
            return n;
        }

        // 인턴장이 새 요청을 맡긴다: 내가 고른 선생님 일은 나, 나머지 선생님 일은 동료 인턴 중 가장 한가한 사람
        void AssignWork(Request r)
        {
            if (npcs.Count == 0) return;
            if (r.isMess) // 바닥 청소는 동기가 먼저 (쉬는 중이면 다음 인턴)
            {
                foreach (int w in new[] { 3, 2, 1 }) if (!npcs[w - 1].resting) { SetAssignee(r, w); return; }
                SetAssignee(r, 3);
                return;
            }
            if (!r.npcOnly) { SetAssignee(r, 0); return; }
            int best = int.MaxValue, who = 2;
            foreach (int w in new[] { 2, 3, 1 }) // 인턴장은 마지막
            {
                if (npcs[w - 1].resting) continue;
                int l = LoadOf(w);
                if (l < best) { best = l; who = w; }
            }
            SetAssignee(r, who);
        }

        void SetAssignee(Request r, int who)
        {
            r.assignee = who;
            r.assignAge = 0f;
            if (r.background == null) return;
            if (r.tagText == null)
            {
                r.tagText = UiFactory.Label("Tag", r.background.transform, "", 22, TextDark, TextAnchor.LowerCenter);
                r.tagText.raycastTarget = false;
                r.tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.Stretch(r.tagText.rectTransform, 0f, 0.98f, 1f, 1.3f);
            }
            r.tagText.text = who == 0 ? "▶ 나" : "▶ " + WorkerName(who);
            r.tagText.color = who == 0 ? new Color(0.10f, 0.45f, 0.20f) : new Color(0.30f, 0.30f, 0.38f);
        }

        void ResetNpcs()
        {
            ApplyNpcRoster();
            foreach (Npc n in npcs)
            {
                if (n.rt == null) continue;
                n.task = null; n.state = 0; n.resting = false; n.refillKind = -1; n.stamina = 100f; n.forceRest = false;
                n.rt.anchoredPosition = n.home;
                PlaceNpcLabel(n);
            }
        }

        static void PlaceNpcLabel(Npc n)
        {
            if (n.label != null) n.label.rectTransform.anchoredPosition = n.rt.anchoredPosition + new Vector2(0f, 54f);
        }

        Vector2 NpcWorkSpot(Npc n, Request r)
        {
            if (r.useAnchor) return r.anchor + new Vector2(60f, 0f);
            Vector2 seat = SeatPos(r.designer);
            return new Vector2(seat.x + 62f, seat.y > 0f ? 55f : -55f);
        }

        // 스태미너가 낮을수록 작업이 느려진다 (100이면 1배, 0이면 1.8배)
        static float StaminaSlow(Npc n) { return 1f + 1.2f * (1f - Mathf.Clamp01(n.stamina / 100f)); }

        float NpcWorkTime(Npc n, Request r)
        {
            if (r.isMess) return 5f * n.workMult * StaminaSlow(n);
            int len = r.route != null ? r.route.Length : 1;
            return (4f + 3.5f * len) * n.workMult * StaminaSlow(n);
        }

        // 아직 못 하는 단계(내 레벨 규칙과 같은 해금 레벨)는 선생님 도움을 받느라 두 배 걸린다
        float NpcStepTime(Npc n)
        {
            int step = n.task.route[n.task.routeIdx];
            float t = 4f * n.workMult * StaminaSlow(n);
            if (n.level < StepUnlock[step]) t *= 2f;
            return t;
        }

        // 이 요청의 다음 단계가 시술석인데 빈 의자가 없으면 지금은 시작할 수 없다
        bool NpcNeedsChairNow(Request r)
        {
            return r.route != null && r.chair < 0 && r.routeIdx < r.route.Length
                   && StepStation[r.route[r.routeIdx]] == 1 && FreeChairs() == 0;
        }

        // 단계마다 그 구역(샴푸실, 시술석 의자, 디자이너 자리)으로 걸어가서 일한다
        Vector2 NpcStepSpot(Npc n, Request r, int idx)
        {
            int st = StepStation[r.route[idx]];
            if (st == 1 && r.chair >= 0) return ChairStandPos(r.chair);
            if (st == 0) return WashStandPos(Mathf.Max(0, r.bed));
            if (st >= 0) return StationPos[st] + new Vector2(-80f, 0f);
            return NpcWorkSpot(n, r);
        }

        void NpcStart(Npc n)
        {
            Request t = n.task;
            n.waitT = 0f;
            if (t.route == null)
            {
                n.state = 1;
                n.target = NpcWorkSpot(n, t);
                return;
            }
            if (t.ready)
            {
                // 대기가 끝난 손님을 모시러 간다
                t.ready = false;
                t.multi = 0;
                t.bodyText.text = t.titleBase;
                t.background.color = t.baseColor;
                if (StepStation[t.route[t.routeIdx]] != 1) ReleaseChair(t);
            }
            NpcBeginStep(n);
        }

        void NpcBeginStep(Npc n)
        {
            Request t = n.task;
            n.waitT = 0f;
            if (!ReserveFor(t, t.routeIdx)) { n.state = 3; return; }
            n.state = 1;
            n.target = NpcStepSpot(n, t, t.routeIdx);
        }

        void NpcStepDone(Npc n)
        {
            Request t = n.task;
            int step = t.route[t.routeIdx];
            foreach (int k in StepUses[step]) if (stock[k] > 0) stock[k]--;
            if (step == 4) SpawnFloorMess(t.designer); // 커트 뒤에는 바닥에 머리카락이 남는다
            t.routeIdx++;
            if (t.delegated)
            {
                // 내가 맡긴 쉬운 단계: 인턴의 솜씨가 만족도에 들어가고, 끝나면 내가 이어받는다
                t.satSum += n.sat;
                t.satCount++;
                if (t.routeIdx >= t.route.Length)
                {
                    t.delegated = false;
                    SetAssignee(t, 0);
                    ReleaseNpc(n);
                    ReleaseChair(t);
                    PerformRequest(t);
                    return;
                }
                if (StepWaitMin[step] <= 0)
                {
                    t.delegated = false;
                    SetAssignee(t, 0);
                    t.ready = true;
                    t.readyAt = businessElapsed;
                    t.multi = 0;
                    t.bodyText.text = t.titleBase + "\n✔ " + StepNames[step] + " 끝!";
                    t.background.color = new Color(0.78f, 0.93f, 0.78f);
                    ReleaseNpc(n);
                    Toast("✔ " + Ga(n.role) + " " + Reul(StepNames[step]) + " 끝냈어요. 모시러 가요", true);
                    return;
                }
            }
            if (t.routeIdx >= t.route.Length) { NpcFinish(n, t); return; }

            if (StepWaitMin[step] > 0)
            {
                if (t.delegated) { t.delegated = false; SetAssignee(t, 0); } // 대기가 끝나면 내가 모시러 간다
                // 약이 작용하는 동안 손님은 앉아서 기다리고, 인턴은 다른 일을 하러 간다
                t.processing = true;
                t.ready = false;
                t.procTotal = StepWaitMin[step] * SecPerMinute;
                t.procEnd = businessElapsed + t.procTotal;
                t.background.color = new Color(0.80f, 0.88f, 0.98f);
                ReleaseNpc(n);
                return;
            }
            if (StepStation[step] == 1) ReleaseChair(t);
            NpcBeginStep(n);
        }

        void ReleaseNpc(Npc n)
        {
            if (n.task != null) { n.task.npcBusy = false; n.task.npcActive = false; }
            n.task = null;
            n.state = 0;
            PlaceNpcLabel(n);
        }

        float playerIdleTime;

        // 일을 끝내고 할 일이 없으면 자리에 남지 않고 복도로 돌아온다
        void UpdatePlayerReturn()
        {
            if (playerRt == null) return;
            bool busy = playerWalking || playerTask != null || carrying || customerReturning || resting || restWalking
                        || refilling || supplyWalking || waitingAtStation || minigameActive;
            Vector2 p = playerRt.anchoredPosition;
            if (busy || Mathf.Abs(p.y) < 30f) { playerIdleTime = 0f; return; }
            playerIdleTime += Time.deltaTime;
            if (playerIdleTime < 0.5f) return; // 바로 다음 일을 누를 수도 있으니 잠깐 기다린다
            playerTarget = new Vector2(Mathf.Clamp(p.x, -600f, 600f), 0f);
            playerWalking = true;
            playerIdleTime = 0f;
        }

        void UpdateNpcs()
        {
            UpdatePlayerReturn();
            UpdatePlayerMarker();
            if (npcs.Count == 0) return;
            float dt = Time.deltaTime;

            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].assignee >= 0) active[i].assignAge += dt;
            }

            foreach (Npc n in npcs)
            {
                if (n.rt == null) continue;

                if (n.task != null && (!active.Contains(n.task) || n.task.assignee != n.id)) ReleaseNpc(n);

                // 스태미너가 0이 되면 스스로 쉬러 가고, 가득 차면 돌아온다
                if (n.stamina <= 15f && !n.forceRest) { n.forceRest = true; NpcSpeak(n, LineTired); }   // 0이 되기 전에 미리 쉬러 간다
                else if (n.forceRest && n.stamina >= 100f) n.forceRest = false;
                bool brk = NpcOnBreak(n) || n.forceRest;
                if (brk != n.resting)
                {
                    n.resting = brk;
                    if (brk) { Toast("☕ " + Ga(n.role) + " 휴게실에 가요! 쉬는 동안 고르지 않은 선생님 일도 도와주면 추가 점수 +10", true); NpcSpeak(n, LineBreak, 0.7f); }
                    else Toast(Ga(n.role) + " 휴게를 마치고 돌아와요", false);
                }
                // 스태미너: 일하는 동안 줄고(재료 채우기 포함), 휴게 중에는 찬다
                if (n.resting && n.task == null) n.stamina = Mathf.Min(100f, n.stamina + (100f / 30f) * dt * (TraitOf(n) != null ? TraitOf(n).rest : 1f));
                else if (n.task != null || n.refillKind >= 0) n.stamina = Mathf.Max(3f, n.stamina - 0.4f * dt * (TraitOf(n) != null ? TraitOf(n).drain : 1f));   // 하던 일을 마치는 동안에도 0까지는 안 내려간다

                if (n.task == null && !n.resting && n.refillKind < 0)
                {
                    // 먼저 맡은 요청(다른 선생님 일)을 하고, 없으면 한가한 인턴이 내가 고른 선생님 일을 도와준다
                    Request pick = null;
                    for (int pass = 0; pass < 2 && pick == null; pass++)   // 대기가 끝난 손님을 먼저, 그다음 새 일
                    {
                        int bestHard = -1;
                        foreach (Request r in active)
                        {
                            bool mine = r.assignee == n.id || (r.assignee > 0 && npcs[r.assignee - 1].resting); // 쉬는 인턴의 일도 대신 맡는다
                            if (!mine || r.npcBusy || r.npcActive || playerTask == r || IsPlayerOnlyCut(r)) continue;
                            if (r.isLocked || r.processing || r.pickup || r.started) continue;
                            if (pass == 0 ? !r.ready : r.ready) continue;
                            if (NpcNeedsChairNow(r)) continue;
                            int hard = HardScore(r); // 내가 못 하는 단계가 든 어려운 시술부터 맡는다
                            if (hard > bestHard) { bestHard = hard; pick = r; }
                        }
                        if (pick != null && pick.assignee != n.id) SetAssignee(pick, n.id);
                    }
                    if (pick == null)
                    {
                        // 맡은 일이 없으면 재료부터 채운다 (재료가 떨어지면 내가 잔소리를 듣는다)
                        int refillK = NpcPickRefill();
                        if (refillK >= 0)
                        {
                            n.refillKind = refillK;
                            n.state = 4;
                        }
                    }
                    if (pick == null && n.refillKind < 0)
                    {
                        int bestHard = -1;
                        foreach (Request r in active)
                        {
                            if (r.npcOnly || r.assignee != 0 || r.isLocked || r.isMess || IsPlayerOnlyCut(r)) continue;
                            if (playerTask == r || r.npcBusy || r.npcActive || r.processing || r.ready || r.pickup || r.started || r.chair >= 0 || r.routeIdx != 0) continue;
                            int hard = HardScore(r);
                            // 내가 못 하는 단계가 든 시술은 곧바로(2초), 나머지는 내가 시작 안 하면 8초 뒤 도와준다
                            if (r.assignAge < (hard >= 1000 ? 1f : HelpAfterSeconds)) continue;
                            if (NpcNeedsChairNow(r)) continue;
                            if (hard > bestHard) { bestHard = hard; pick = r; }
                        }
                        if (pick != null)
                        {
                            SetAssignee(pick, n.id);
                            Toast(Ga(n.role) + " " + OwnerTitle(pick) + " " + Reul(pick.label) + " 도와줘요", true);
                            NpcSpeak(n, LineHelp, 0.4f);
                        }
                    }
                    if (pick != null)
                    {
                        n.task = pick;
                        pick.npcActive = true;
                        NpcStart(n);
                    }
                }

                if (n.task == null && n.resting)
                {
                    // 휴게 시간: 휴게실 옆에서 쉰다
                    n.rt.anchoredPosition = Vector2.MoveTowards(n.rt.anchoredPosition, NpcRestSpot(n), NpcSpeed * dt);
                    bool arrived = (n.rt.anchoredPosition - NpcRestSpot(n)).sqrMagnitude < 4f;
                    n.rt.localRotation = arrived ? Quaternion.identity : Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 14f) * 6f);
                    n.label.text = arrived ? "휴게 중" : n.role;
                }
                else if (n.task == null && n.refillKind >= 0)
                {
                    int k = n.refillKind;
                    Vector2 spot = NpcSupplySpot(n);
                    if (n.state == 4)
                    {
                        n.rt.anchoredPosition = Vector2.MoveTowards(n.rt.anchoredPosition, spot, NpcSpeed * dt);
                        n.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 14f) * 6f);
                        n.label.text = "재료 채우러";
                        if ((n.rt.anchoredPosition - spot).sqrMagnitude < 1f)
                        {
                            n.state = 5;
                            n.total = n.left = 3f + 2f * n.workMult;
                        }
                    }
                    else
                    {
                        n.left -= dt;
                        n.rt.anchoredPosition = spot + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 8f);
                        n.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 9f) * 8f);
                        n.label.text = StockNames[k] + " " + Mathf.Max(0f, n.left).ToString("0") + "초";
                        if (n.left <= 0f)
                        {
                            stock[k] = StockMax[k];
                            Toast("📦 " + Ga(n.role) + " " + Reul(StockNames[k]) + " 채웠어요", true);
                            n.refillKind = -1;
                            n.state = 0;
                        }
                    }
                }
                else if (n.task == null)
                {
                    // 일이 없을 땐 제자리 근처를 어슬렁거린다
                    n.rt.localRotation = Quaternion.identity;
                    n.wanderT -= dt;
                    if (n.wanderT <= 0f)
                    {
                        n.wanderT = Random.Range(1.5f, 3.5f);
                        n.wander = n.home + new Vector2(Random.Range(-45f, 45f), Random.Range(-40f, 40f));
                    }
                    n.rt.anchoredPosition = Vector2.MoveTowards(n.rt.anchoredPosition, n.wander, NpcSpeed * 0.35f * dt);
                    n.label.text = NpcTitle(n);
                }
                else if (n.state == 3)
                {
                    // 시술석 의자가 날 때까지 기다린다 (너무 오래 걸리면 일을 내려놓고 다른 일을 찾는다)
                    n.rt.localRotation = Quaternion.identity;
                    n.label.text = StepStation[n.task.route[n.task.routeIdx]] == 0 ? "샴푸대 대기" : "의자 대기";
                    n.waitT += dt;
                    if (ReserveFor(n.task, n.task.routeIdx)) NpcBeginStep(n);
                    else if (n.waitT > 6f) ReleaseNpc(n);
                }
                else if (n.state == 1)
                {
                    n.rt.anchoredPosition = Vector2.MoveTowards(n.rt.anchoredPosition, n.target, NpcSpeed * dt);
                    n.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 14f) * 6f); // 걸을 때 뒤뚱
                    n.label.text = NpcTitle(n);
                    if ((n.rt.anchoredPosition - n.target).sqrMagnitude < 1f)
                    {
                        n.state = 2;
                        n.total = n.left = n.task.route != null ? NpcStepTime(n) : NpcWorkTime(n, n.task);
                        n.task.npcBusy = true;
                        if (n.task.tagText != null) n.task.tagText.text = "▶ " + n.role + " 작업 중";
                    }
                }
                else
                {
                    n.left -= dt;
                    // 일하는 동안 통통 튀며 흔들린다
                    n.rt.anchoredPosition = n.target + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 8f);
                    n.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 9f) * 8f);
                    Request t = n.task;
                    bool routed = t.route != null && t.routeIdx < t.route.Length;
                    n.label.text = (routed ? StepNames[t.route[t.routeIdx]] : "작업") + " " + Mathf.Max(0f, n.left).ToString("0") + "초";
                    if (t.barFill != null)
                    {
                        float frac = Mathf.Clamp01(1f - n.left / n.total);
                        float ratio = routed ? (t.routeIdx + frac) / t.route.Length : frac;
                        t.barFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                    }
                    if (n.left <= 0f)
                    {
                        if (routed) NpcStepDone(n); else NpcFinish(n, t);
                    }
                }
                PlaceNpcLabel(n);
            }
        }

        // NPC가 요청을 끝냈을 때: 점수의 60%만 매장 점수로 들어오고 XP는 없다
        void NpcFinish(Npc n, Request r)
        {
            ReleaseNpc(n);
            if (!active.Contains(r)) return;
            ReleaseChair(r);

            if (r.isMess)
            {
                Toast("🧹 " + Ga(n.role) + " 바닥을 치웠어요", true);
                RemoveRequest(r);
                RefreshTop();
                return;
            }

            if (r.npcOnly)
            {
                // 내가 고르지 않은 선생님 일: 점수와 친분 없이 조용히 끝낸다
                Say(Ga(n.role) + " " + OwnerTitle(r) + "의 " + Reul(r.label) + " 끝냈어요");
                RemoveRequest(r);
                return;
            }

            // 건수와 점수는 내가 직접 한 일만 센다. 인턴이 끝낸 일은 손님이 만족하고 돌아갈 뿐이다.
            Toast("✔ " + Ga(n.role) + " " + OwnerTitle(r) + "의 " + r.label + " 완료", true);
            NpcSpeak(n, LineDone, 0.35f);
            RemoveRequest(r);
            RefreshTop();
        }

        // 내가 NPC가 맡은 요청을 누르면 가져온다 (이미 작업 중이면 못 가져온다)
        bool TryReclaimFromNpc(Request r)
        {
            if (r.npcBusy)
            {
                Toast(Ga(WorkerName(r.assignee)) + " 하고 있어요", false);
                return false;
            }
            if (r.assignee > 0)
            {
                Npc n = npcs[r.assignee - 1];
                if (n.task == r) ReleaseNpc(n);
                SetAssignee(r, 0);
                Say("내가 직접 맡아요");
            }
            return true;
        }
    }
}
