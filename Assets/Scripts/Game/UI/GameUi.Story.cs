using System;
using System.Collections.Generic;
using System.Linq;
using KarachiTycoon.Sim;
using UnityEngine;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.UiKit;

namespace KarachiTycoon.Game
{
    // Story moments: choice cards, recipe pages found, Uncle's requests, and the Kitaab hub
    // (recipe pages album, the regulars' register and every tip).
    public partial class GameUi
    {
        // Story pop-ups wait while another modal (the ledger, a card) is up.
        readonly Queue<Action> storyQueue = new Queue<Action>();

        void HookStory()
        {
            sim.CardDrawn += c => storyQueue.Enqueue(() => OpenChoice(c));
            sim.PageFound += p => storyQueue.Enqueue(() => OpenPage(p));
            sim.QuestStarted += q => tutor.QueueCard("quest:" + q.Id, "Uncle's request: " + q.Title, q.Intro);
            sim.QuestDone += q => tutor.QueueCard("questdone:" + q.Id, q.Title, q.Outro);
        }

        void UpdateStory()
        {
            if (storyQueue.Count > 0 && modal == null && !CoachVisible && gc.Playing && (tutor == null || !tutor.Active))
                storyQueue.Dequeue()();
        }

        // ---------- choice cards ----------
        void OpenChoice(CardDef card)
        {
            if (sim.PendingCard != card) return; // the day ended first
            var box = OpenModal("choice", "story", "choice-card");
            box.Add(new TruckStrip().Cls("modal-strip"));
            box.Add(Text("A MOMENT ON BURNS ROAD", "label-gold"));
            box.Add(Text(card.Title, "story-title"));
            var body = Text(card.Text, "story-body");
            box.Add(body);
            var options = Div("col", "choice-options");
            box.Add(options);
            for (int i = 0; i < card.Options.Count; i++)
            {
                int k = i;
                var o = card.Options[i];
                var hint = EffectsHint(o.Effects);
                var b = Btn("", () => Choose(k), "choice-btn");
                b.Add(Text(o.Label, "choice-label"));
                if (hint != null) b.Add(Text(hint, "choice-hint"));
                b.name = "choice-" + i;
                options.Add(b);
            }

            void Choose(int k)
            {
                var result = sim.ResolveCard(k);
                gc.Analytics.Event("card_choice", ("card", card.Id), ("option", k));
                body.text = result ?? "";
                options.Clear();
                options.Add(Btn("Chalo", CloseModal, "btn-primary", "btn-big"));
            }
        }

        // What an option costs or gives, in a few words (cash, fame, friends).
        static string EffectsHint(CardEffects e)
        {
            var parts = new List<string>();
            if (e.Cash != 0) parts.Add((e.Cash > 0 ? "+" : "−") + Util.Rs(Math.Abs(e.Cash)));
            if (e.Fame > 0) parts.Add("Shohrat up"); else if (e.Fame < 0) parts.Add("Shohrat down");
            if (e.Auth > 0) parts.Add("Asli-pan up"); else if (e.Auth < 0) parts.Add("Asli-pan down");
            if (e.Friend.Count > 0) parts.Add(e.Friend.Values.Sum() >= 0 ? "a friend made" : "a friend lost");
            if (e.RivalsOff) parts.Add("rivals quiet tonight");
            if (e.Stock.Count > 0) parts.Add(e.Stock.Values.Sum() > 0 ? "more stock" : "uses stock");
            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }

        // ---------- a page from Dada's book ----------
        void OpenPage(PageDef p)
        {
            var box = OpenModal("page", "page-card");
            box.Add(new Icon("book").Cls("page-icon"));
            box.Add(Text("A PAGE FROM DADA'S BOOK", "page-kicker"));
            box.Add(Text(p.Title, "page-title"));
            box.Add(Text("\"" + p.Text + "\"", "page-text"));
            box.Add(Text(BonusText(p), "page-bonus"));
            box.Add(Text($"{sim.S.Pages.Count} of {gc.Data.Story.Pages.Count} pages found", "page-count"));
            box.Add(Btn("Keep it in the Kitaab", CloseModal, "btn-primary", "btn-big"));
        }

        string BonusText(PageDef p)
        {
            if (p.Bonus.StartsWith("quality_"))
            {
                var d = gc.Data.Dishes.Find(x => x.Id == p.Bonus.Substring(8));
                return $"Your {d?.En ?? "dish"} tastes better for good.";
            }
            return p.Bonus switch
            {
                "patience" => $"Customers wait {Mathf.RoundToInt((p.Value - 1) * 100)}% longer.",
                "visit" => $"{Mathf.RoundToInt((p.Value - 1) * 100)}% more people stop at your thela.",
                "tips" => $"Tips are {Mathf.RoundToInt((p.Value - 1) * 100)}% bigger.",
                "spoil" => $"{Mathf.RoundToInt((1 - p.Value) * 100)}% less stock spoils overnight.",
                _ => "",
            };
        }

        // ---------- Dada ki Kitaab: pages, regulars, tips ----------
        void OpenKitaab(string tab = "pages")
        {
            var box = OpenModal("kitaab", "story", "kitaab");
            box.Add(new TruckStrip().Cls("modal-strip"));
            box.Add(Div("row").With(new Icon("book").Cls("kitaab-icon"), Text("Dada ki Kitaab", "story-title", "grow"), IconBtn("close", null, CloseModal, "close-btn")));
            var tabs = Div("seg", "kitaab-tabs");
            box.Add(tabs);
            var scroll = new ScrollView(ScrollViewMode.Vertical).Cls("kitaab-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            box.Add(scroll);
            var buttons = new Dictionary<string, Button>();
            foreach (var (id, label) in new[] { ("pages", $"Pages {sim.S.Pages.Count}/{gc.Data.Story.Pages.Count}"), ("regulars", "Regulars"), ("tips", "Tips") })
            {
                var b = Btn(label, () => Show(id), "seg-btn");
                buttons[id] = b; tabs.Add(b);
            }

            void Show(string id)
            {
                foreach (var kv in buttons) kv.Value.EnableInClassList("on", kv.Key == id);
                scroll.Clear();
                if (id == "pages")
                {
                    var grid = Div("page-grid");
                    foreach (var p in gc.Data.Story.Pages)
                    {
                        bool have = sim.HasPage(p.Id);
                        var cell = Div("page-cell").Cls(have ? "found" : "missing");
                        if (have) cell.With(Text(p.Title, "h2"), Text("\"" + p.Text + "\"", "page-cell-text"), Text(BonusText(p), "page-cell-bonus"));
                        else cell.With(Text("?", "page-missing"), Text(p.Hint, "muted", "small"));
                        grid.Add(cell);
                    }
                    scroll.Add(grid);
                }
                else if (id == "regulars")
                {
                    foreach (var r in gc.Data.Story.Regulars)
                    {
                        int level = sim.FriendLevel(r.Id);
                        var hearts = Div("row", "hearts");
                        for (int i = 0; i < 3; i++) hearts.Add(Div("heart").Cls(i < level ? "on" : "off"));
                        var usual = string.Join(", ", r.Order.Select(kv => $"{kv.Value} {gc.Data.Dishes.Find(d => d.Id == kv.Key)?.En}"));
                        scroll.Add(Div("kitaab-tip", "regular-row").With(
                            Div("row").With(Text(r.Name, "h2", "grow"), hearts),
                            Text($"{r.Role} · {string.Join(" ", r.Days)} · {SlotName(r.Slot)}", "muted", "small"),
                            Text("Usual: " + usual, "body"),
                            level > 0 ? Text(r.Levels[level - 1], "khata-quote") : Text("Serve them quickly at a fair price and they will come back friendlier.", "muted", "small")));
                    }
                }
                else foreach (var tip in tutor.Data.Kitaab) scroll.Add(Div("kitaab-tip").With(Text(tip.Title, "h2"), Text(tip.Text, "body")));
            }
            Show(tab);
            gc.Analytics.Event("kitaab_open", ("tab", tab));
        }
    }
}
