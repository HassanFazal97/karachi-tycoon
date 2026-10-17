using System.Collections.Generic;
using System.Linq;
using KarachiTycoon.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.UiKit;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // The game's reactions: "Aaj ka kaam" (three daily goals), the Rush streak, coins that fly from
    // a served customer into the galla, the galla bumping, and a SHABASH! stamp when a goal is done.
    public partial class GameUi
    {
        VisualElement fxLayer, goalsCard, goalsRows;
        Label cashLabel;
        bool goalsOpen, goalsDirty;
        readonly Binder goalsBind = new Binder();
        float lastPop;
        int pops;

        void BuildFeedback(VisualElement bannerRow, VisualElement phoneRow)
        {
            fxLayer = Div("layer").Ghost();
            hud.Add(fxLayer);

            // Rush streak pill, next to the load-shedding banner.
            var streak = Div("streak-pill").With(new Icon("flame").Cls("streak-icon"), Text("", "streak-text")).Ghost(true);
            bannerRow.Insert(0, streak);
            hudBind.Visible(streak, () => sim.Today != null && sim.Today.Streak >= 5 && !sim.S.DayOver);
            hudBind.Text(streak.Q<Label>(), () => $"Rush ×{sim.Today?.Streak ?? 0}");
            hudBind.Class(streak, "hot", () => (sim.Today?.Streak ?? 0) >= 15);

            // Aaj ka kaam.
            goalsCard = Div("goals-card");
            var count = Text("", "goals-count");
            goalsCard.Add(Div("row", "goals-head").With(new Icon("target").Cls("goals-icon"), Text("AAJ KA KAAM", "stat-label", "grow"), count));
            // Uncle's current request sits above the day's goals.
            var quest = Div("goal-row", "quest-row");
            var qName = Text("", "goal-name"); var qProg = Text("", "goal-prog");
            var (qBar, qFill) = Bar("goal-mini");
            quest.With(Div("portrait", "quest-portrait").With(new Icon("staff")), Div("col", "grow").With(qName, qBar), qProg);
            goalsCard.Add(quest);
            hudBind.Visible(quest, () => sim.ActiveQuest != null);
            hudBind.Text(qName, () => sim.ActiveQuest is var q && q != null ? "Uncle: " + q.Title : "");
            hudBind.Fill(qFill, () => sim.ActiveQuest is var q && q != null ? sim.S.QuestProgress / (float)q.Target : 0);
            hudBind.Text(qProg, () => sim.ActiveQuest is var q && q != null ? (q.Type == "fame" ? $"{sim.S.Fame:F1}/{q.Target / 10f:F0}★" : $"{sim.S.QuestProgress}/{q.Target}") : "");
            goalsRows = Div("col");
            goalsCard.Add(goalsRows);
            hud.Add(goalsCard);
            hudBind.Text(count, () => $"{sim.S.Goals.Count(g => g.Done)} / {sim.S.Goals.Count}");
            string shown = null;
            hudBind.Do(() =>
            {
                var sig = string.Join("|", sim.S.Goals.Select(g => g.Id + g.Target));
                if (sig != shown) { shown = sig; goalsDirty = true; } // rebuilt after this pass (GameUi.Update)
            });
            if (phone)
            {
                // Phones: a goals button in the top row opens the card as a dropdown.
                var btn = Btn("", () => goalsOpen = !goalsOpen, "gear-btn", "goals-btn");
                btn.name = "top-goals";
                btn.Add(new Icon("target"));
                var n = Text("", "goals-btn-count");
                btn.Add(n);
                hudBind.Text(n, () => $"{sim.S.Goals.Count(g => g.Done)}/{sim.S.Goals.Count}");
                hudBind.Visible(btn, () => sim.S.Goals.Count > 0 || sim.ActiveQuest != null);
                phoneRow.Insert(phoneRow.IndexOf(phoneRow.Q(className: "grow")) + 1, btn);
                hudBind.Visible(goalsCard, () => goalsOpen && (sim.S.Goals.Count > 0 || sim.ActiveQuest != null) && sheetOpen == null);
                hudBind.Class(btn, "on", () => goalsOpen);
                // Toasts sit above the HUD; step them aside while the dropdown is open.
                hudBind.Visible(toastLayer, () => !goalsOpen);
            }
            else hudBind.Visible(goalsCard, () => (sim.S.Goals.Count > 0 || sim.ActiveQuest != null) && sheetOpen == null && !sim.S.DayOver);

            // Galla bump when money comes in.
            cashLabel = hud.Q<Label>(className: "money");
            int lastCash = sim.S.Cash;
            hudBind.Do(() =>
            {
                if (sim.S.Cash > lastCash && cashLabel != null)
                {
                    cashLabel.AddToClassList("bump");
                    cashLabel.schedule.Execute(() => cashLabel.RemoveFromClassList("bump")).StartingIn(140);
                }
                lastCash = sim.S.Cash;
            });
        }

        void FillGoals()
        {
            goalsDirty = false;
            goalsRows.Clear(); goalsBind.Clear();
            for (int i = 0; i < sim.S.Goals.Count; i++)
            {
                int k = i;
                var g = sim.S.Goals[i];
                var row = Div("goal-row");
                var mark = new Icon("circle").Cls("goal-mark");
                var (bar, fill) = Bar("goal-mini");
                var prog = Text("", "goal-prog");
                var swap = IconBtn("ad", null, () => gc.SwapGoal(k), "goal-swap");
                swap.tooltip = "Watch an ad to swap this goal";
                row.With(mark, Div("col", "grow").With(Text(g.Text, "goal-name"), bar), prog, swap);
                goalsRows.Add(row);
                goalsBind.Do(() => mark.Set(g.Done ? "check" : g.Failed ? "close" : "circle"));
                goalsBind.Class(row, "done", () => g.Done);
                goalsBind.Class(row, "failed", () => g.Failed);
                goalsBind.Fill(fill, () => g.Target > 0 ? g.Progress / (float)g.Target : 0);
                goalsBind.Text(prog, () => g.Done ? $"+{RsShort(g.Reward)}" : g.Failed ? "missed" : g.Target > 1 ? $"{g.Progress:N0}/{g.Target:N0}" : "");
                goalsBind.Visible(swap, () => gc.CanSwapGoal(k) && !g.Failed);
            }
        }

        void OnGoalDone(DailyGoal g)
        {
            gc.Toast($"Aaj ka kaam: {g.Text}. +{Rs(g.Reward)}{(g.Coins > 0 ? $" and {g.Coins} silver coins" : "")}", "good");
            Stamp("SHABASH!", g.Text);
        }

        // A rubber stamp that thumps onto the screen and fades.
        void Stamp(string word, string note)
        {
            if (fxLayer == null) return;
            var st = Div("stamp-fx").With(Text(word, "stamp-word"), Text(note, "stamp-note")).Ghost(true);
            var row = Div("stamp-row").Ghost().With(st);
            fxLayer.Add(row);
            st.schedule.Execute(() => st.AddToClassList("in")).StartingIn(16);
            st.schedule.Execute(() => st.AddToClassList("out")).StartingIn(1300);
            row.schedule.Execute(() => row.RemoveFromHierarchy()).StartingIn(1800);
        }

        // "+Rs 120" rising from the customer and flying into the galla.
        void OnServeBubble(BubbleEvent e)
        {
            if (fxLayer == null || e.Bill <= 0 || !e.Served || cashLabel == null) return;
            if (Time.realtimeSinceStartup - lastPop < 0.1f || pops > 8) return;
            var cam = gc.Rig.Cam;
            var sp = cam.WorldToScreenPoint(W(e.C.X, 2.1f, e.C.Z));
            if (sp.z < 0) return;
            var panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
            var from = fxLayer.WorldToLocal(panelPos);
            var to = fxLayer.WorldToLocal(cashLabel.worldBound.center);
            if (from.x < 0 || from.y < 0 || from.x > fxLayer.layout.width || from.y > fxLayer.layout.height) return;
            lastPop = Time.realtimeSinceStartup; pops++;
            var pop = Text("+" + RsShort(e.Bill).Replace("Rs ", "Rs "), "coin-pop").Ghost();
            pop.style.left = from.x; pop.style.top = from.y;
            fxLayer.Add(pop);
            pop.schedule.Execute(() => pop.AddToClassList("rise")).StartingIn(16);
            pop.schedule.Execute(() =>
            {
                pop.AddToClassList("fly");
                pop.style.translate = new Translate(to.x - from.x, to.y - from.y);
            }).StartingIn(420);
            pop.schedule.Execute(() => { pop.RemoveFromHierarchy(); pops--; }).StartingIn(1050);
        }
    }
}
