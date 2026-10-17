using System;
using System.Collections.Generic;
using System.Linq;
using KarachiTycoon.Sim;
using Newtonsoft.Json;
using UnityEngine;

namespace KarachiTycoon.Game
{
    public class TutorialData
    {
        public class Card { public string Icon, Title, Text; }
        public class Step { public string Id, Text, Title, Target, Button; public bool Pause, Hidden; public float Auto; }
        public class Hint { public string Text, Action; }
        public List<Card> ColdOpen = new List<Card>();
        public List<Step> Steps = new List<Step>();
        public Dictionary<string, Hint> Hints = new Dictionary<string, Hint>();
        public Dictionary<string, Card> Unlocks = new Dictionary<string, Card>();
        public List<Card> Kitaab = new List<Card>();
    }

    // Uncle Rasheed's guided first day (upgrade plan, "The first ten minutes"), the one-time hints
    // that follow the player's behaviour, and the panels that unlock as the story reaches them.
    // The words live in Resources/Data/tutorial.json; when each step starts and ends lives here.
    // The UI only draws what this asks for (GameUi.ShowCoach).
    public class TutorialDirector : MonoBehaviour
    {
        public TutorialData Data { get; private set; }
        GameController gc;
        GameUi ui;
        Sim.Sim sim => gc.Sim;

        int index = -1;
        float stepStart, tutorialStart, enteredMinute;
        bool grumbled, waiting;
        TutorialData.Card explainer;           // an unlock card on screen
        // Uncle's cards waiting their turn: unlock explainers ("unlock:Staff") and story moments (quests).
        readonly Queue<(string key, TutorialData.Card card, string target)> pendingCards = new Queue<(string, TutorialData.Card, string)>();

        public TutorialData.Step Current => Active && index >= 0 && index < Data.Steps.Count ? Data.Steps[index] : null;
        public bool Active => sim != null && sim.S.Tutorial == "day1";
        // The game clock stops while a lesson or an explainer card waits for the player.
        public bool Pausing => (Current != null && Current.Pause && !Current.Hidden && !waiting) || explainer != null;

        public void Init(GameController g, GameUi u)
        {
            gc = g; ui = u;
            Data = JsonConvert.DeserializeObject<TutorialData>(Resources.Load<TextAsset>("Data/tutorial").text);
            gc.Started += OnStarted;
        }

        void OnStarted()
        {
            sim.Bubble += OnBubble;
            sim.Noticed += n => { if (n.Key == "outage") Hint("outage"); };
            sim.DayEnded += OnDayEnded;
            sim.DayStarted += _ => OnDayStarted();
            tutorialStart = Time.realtimeSinceStartup;
            if (Active)
            {
                sim.HoldCustomers = true; sim.NoRefusals = true;
                gc.Analytics.Event("tutorial_start");
                Go(0);
            }
            OnDayStarted();
        }

        // ---------- which panels exist yet ----------
        public bool Unlocked(string tab)
        {
            if (sim == null) return true;
            var s = sim.S;
            if (s.Tutorial == null) return true; // saves from before the tutorial
            int at = Active ? index : int.MaxValue;
            switch (tab)
            {
                case "Menu": return at >= StepIndex("menu_open");
                case "Supplies": return at >= StepIndex("supplies_open");
                case "Staff": return s.Day >= 2 || s.Staff.Values.Any(v => v);
                case "Shop": return s.Day >= 3 || s.Act1Done || s.Stage != "cart"; // thela upgrades first, the dhaba after Act 1
                case "Store": return !Active;
                case "fat": return s.Stage != "cart";
                default: return true;
            }
        }

        int StepIndex(string id) => Data.Steps.FindIndex(st => st.Id == id);

        // ---------- the guided day ----------
        void Go(int i)
        {
            if (Current != null) Exit(Current.Id);
            index = i;
            if (index >= Data.Steps.Count) { Finish(false); return; }
            var st = Current;
            stepStart = Time.realtimeSinceStartup;
            enteredMinute = sim.S.Minute;
            Enter(st.Id);
            gc.Analytics.Event("tutorial_step", ("step", st.Id), ("index", index), ("seconds", Mathf.RoundToInt(stepStart - tutorialStart)));
            if (st.Hidden) ui.HideCoach();
            else ui.ShowCoach(st.Title, st.Text, st.Target, st.Pause, st.Button, st.Button != null ? Next : (Action)null, Skip);
        }

        void Enter(string id)
        {
            switch (id)
            {
                case "first_customers": sim.HoldCustomers = false; break;
                case "wait_grumble": sim.NoRefusals = false; grumbled = false; break;
                case "rush": gc.SetSpeed(1); break;
            }
        }

        void Exit(string id)
        {
            switch (id)
            {
                case "chai_on":
                    // Uncle puts Dada's other start dishes up too, and the morning delivery arrives.
                    foreach (var d in gc.Data.Dishes) if (sim.S.Unlocked[d.Id]) sim.S.Menu[d.Id].On = true;
                    sim.RestockNow();
                    ui.CloseSheetNow();
                    break;
                case "price_done":
                case "topup":
                    ui.CloseSheetNow();
                    break;
            }
        }

        bool Done(string id)
        {
            var s = sim.S;
            switch (id)
            {
                case "menu_open": return ui.SheetOpen == "Menu";
                case "chai_on": return s.Menu["chai"].On;
                case "uncle_first": return s.TotalServed >= 1;
                case "first_customers": return s.TotalServed >= 4;
                case "shout": return s.Shouts > 0;
                case "wait_grumble": return grumbled || s.Minute - enteredMinute > 90;
                case "price_open": return ui.SheetOpen == "Menu";
                case "price_fix": return s.Menu["chai"].Price <= gc.Data.Dishes.First(d => d.Id == "chai").RefPrice;
                case "wait_milk": return s.Stock["milk_patti"] < 12 || s.Minute >= 840;
                case "supplies_open": return ui.SheetOpen == "Supplies";
                case "topup": return s.Stock["milk_patti"] >= s.AutoBuy["milk_patti"];
                case "autobuy": return s.AutoBuy["milk_patti"] >= 60;
                case "wait_afternoon": return s.Minute >= 840;
                case "speed": return gc.Speed >= 2;
                case "wait_maghrib": return s.Minute >= 1095;
                case "wait_ledger": return ui.ModalId == "ledger";
                default: return false; // steps with a button wait for Next
            }
        }

        // Some steps wait for the right moment before they appear.
        bool Ready(string id)
        {
            switch (id)
            {
                case "shout": return sim.Queue().Count <= 1 || Time.realtimeSinceStartup - stepStart > 20;
                default: return true;
            }
        }

        public void Next()
        {
            if (explainer != null) { explainer = null; ui.HideCoach(); ShowPendingUnlock(); return; }
            if (Current == null) return;
            if (Current.Id == "goal") { Finish(false); return; }
            Go(index + 1);
        }

        public void Skip() => Finish(true);

        void Finish(bool skipped)
        {
            if (!Active) return;
            float t = Time.realtimeSinceStartup - tutorialStart;
            var at = Current?.Id;
            index = -1;
            // A skip still leaves a working stall: dishes on, stock in.
            if (skipped && !sim.S.Menu.Values.Any(m => m.On))
                foreach (var d in gc.Data.Dishes) if (sim.S.Unlocked[d.Id]) sim.S.Menu[d.Id].On = true;
            if (skipped) sim.RestockNow();
            sim.FinishTutorial();
            ui.HideCoach();
            if (skipped) gc.Analytics.Event("tutorial_skip", ("step", at), ("seconds", Mathf.RoundToInt(t)));
            else gc.Analytics.Event("tutorial_complete", ("seconds", Mathf.RoundToInt(t)));
        }

        void Update()
        {
            if (sim == null) return;
            var st = Current;
            if (st != null)
            {
                // Steps that wait for a moment stay hidden until it comes.
                waiting = !st.Hidden && !Ready(st.Id);
                if (waiting) { ui.HideCoach(); return; }
                if (!st.Hidden && !ui.CoachVisible) ui.ShowCoach(st.Title, st.Text, st.Target, st.Pause, st.Button, st.Button != null ? Next : (Action)null, Skip);
                if (st.Auto > 0 && Time.realtimeSinceStartup - stepStart > st.Auto) { Go(index + 1); return; }
                if (Done(st.Id)) Go(index + 1);
                return;
            }
            if (explainer == null && pendingCards.Count > 0 && ui.ModalId == null) ShowPendingUnlock();
        }

        // ---------- unlock explainers ----------
        void OnDayStarted()
        {
            var s = sim.S;
            if (s.Tutorial == null) return;
            // The Store gets no card: it opens only when the player taps it.
            foreach (var tab in new[] { "Staff", "Shop", "fat" })
                if (Unlocked(tab) && !s.Seen.Contains("unlock_" + tab) && Data.Unlocks.TryGetValue(tab, out var card))
                {
                    s.Seen.Add("unlock_" + tab);
                    pendingCards.Enqueue(("unlock:" + tab, card, tab == "fat" ? null : "dock-" + tab));
                }
            if (s.Day >= 3 && s.Shouts == 0) Hint("noShout");
            if (s.Day >= 3 && s.PriceChanges == 0) Hint("noPrice");
        }

        // A story moment from Uncle (a quest starting or done), shown when nothing else is on screen.
        public void QueueCard(string key, string title, string text) =>
            pendingCards.Enqueue((key, new TutorialData.Card { Title = title, Text = text }, null));

        void ShowPendingUnlock()
        {
            if (Active || pendingCards.Count == 0) return;
            var (key, card, target) = pendingCards.Dequeue();
            explainer = card;
            if (key.StartsWith("unlock:")) gc.Analytics.Event("unlock", ("panel", key.Substring(7)), ("day", sim.S.Day));
            ui.ShowCoach(card.Title, card.Text, target, true, key.StartsWith("unlock:") ? "Got it" : "Ji, Uncle", Next, null);
        }

        // ---------- one-time hints ----------
        void OnBubble(BubbleEvent e)
        {
            if (e.Key == "tooExpensive" && Current?.Id == "wait_grumble") { grumbled = true; return; }
            if (e.Key == "walkout") Hint(Active ? "walkout_tutorial" : "walkout");
            else if (e.Key == "soldOut") Hint("soldOut");
            else if (e.Key == "queueFull" && !Active) Hint("queueFull");
            else if (e.Key == "tooExpensive" && !Active) Hint("tooExpensive");
        }

        void OnDayEnded(Ledger l)
        {
            if (l.Net < 0) Hint("loss");
        }

        void Hint(string key)
        {
            var s = sim.S;
            if (s.Tutorial == null || s.Seen.Contains("hint_" + key) || !Data.Hints.TryGetValue(key, out var h)) return;
            // During the lessons only the gentle tutorial hint is allowed; the rest wait for a later day.
            if (Active && key != "walkout_tutorial") return;
            s.Seen.Add("hint_" + key);
            string action = h.Action != null && Unlocked(h.Action) ? h.Action : null;
            ui.UncleHint(h.Text, action);
            gc.Analytics.Event("hint", ("key", key), ("day", s.Day));
        }
    }
}
