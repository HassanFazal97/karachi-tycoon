using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.UiKit;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // The management panels: Menu, Supplies, Staff, Shop and Store. One sheet that slides in from
    // the right (a side panel on desktop, a half-screen sheet on phones) and swaps its content.
    public partial class GameUi
    {
        VisualElement sheet;
        Label sheetTitle;
        Icon sheetIcon;
        ScrollView sheetBody;
        string sheetOpen;
        readonly HashSet<string> seenDishes = new HashSet<string>();

        void BuildSheet()
        {
            sheet = Div("sheet");
            sheetIcon = new Icon("menu").Cls("sheet-icon");
            sheetTitle = Text("", "sheet-title");
            sheet.Add(Div("sheet-head").With(sheetIcon, sheetTitle, Div("grow"), IconBtn("close", null, CloseSheet, "close-btn")));
            sheet.Add(new TruckStrip().Cls("sheet-strip"));
            sheetBody = new ScrollView(ScrollViewMode.Vertical).Cls("sheet-body");
            sheetBody.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sheet.Add(sheetBody);
            sheetLayer.Add(sheet);
        }

        void ToggleSheet(string id) { if (sheetOpen == id) CloseSheet(); else OpenSheet(id); }

        void OpenSheet(string id)
        {
            sheetOpen = id;
            var tab = Tabs.First(t => t.id == id);
            sheetIcon.Set(tab.icon);
            sheetTitle.text = id switch { "Menu" => "Menu board", "Shop" => "Upgrades", "Staff" => "Your team", _ => id };
            FillSheet();
            sheetBody.scrollOffset = Vector2.zero;
            sheet.AddToClassList("open");
            gc.Analytics.Event("panel_open", ("panel", id));
        }

        void CloseSheet() { sheetOpen = null; sheet?.RemoveFromClassList("open"); }

        void RefreshSheet() { if (sheetOpen != null) { var o = sheetBody.scrollOffset; FillSheet(); sheetBody.scrollOffset = o; } }

        void FillSheet()
        {
            sheetBody.Clear(); sheetBind.Clear();
            switch (sheetOpen)
            {
                case "Menu": MenuSheet(); break;
                case "Supplies": SuppliesSheet(); break;
                case "Staff": StaffSheet(); break;
                case "Shop": ShopSheet(); break;
                default: StoreSheet(); break;
            }
        }

        void Add(VisualElement e) => sheetBody.Add(e);

        // ---------- Menu ----------
        void MenuSheet()
        {
            var s = sim.S;
            Add(Text("Customers read the board at your thela. Price above what they expect and some walk off saying \"Mehenga hai!\".", "hint"));
            foreach (var d in gc.Data.Dishes)
            {
                var id = d.Id;
                var card = Div("card");
                Add(card);
                if (sim.IsLocked(id))
                {
                    card.AddToClassList("locked");
                    card.Add(Div("row").With(new Icon("lock").Cls("lock-icon"), Text(d.En, "h2")));
                    card.Add(Text(d.Unlock.Type == "served" ? $"Unlocks after {d.Unlock.Value} customers" : "Needs the dhaba and a karigar", "muted"));
                    continue;
                }
                seenDishes.Add(id);
                var chip = Text("", "chip");
                card.Add(Div("row").With(Text(d.En, "h2", "grow"), chip));
                if (!string.IsNullOrEmpty(d.Note)) card.Add(Text(d.Note, "muted", "small"));

                var price = Text("", "price");
                var selling = Btn("", () => sim.ToggleDish(id), "toggle");
                selling.name = "toggle-" + id;
                card.Add(Div("row", "controls").With(
                    Btn("−", () => sim.SetPrice(id, s.Menu[id].Price - 10), "btn-sm").Named("price-down-" + id), price,
                    Btn("+", () => sim.SetPrice(id, s.Menu[id].Price + 10), "btn-sm"),
                    Div("grow"), selling));
                var info = Text("", "muted", "small");
                var mastery = new Stars { Empty = new Color(0, 0, 0, 0.14f) }.Cls("mastery");
                card.Add(Div("row").With(info, Div("grow"), Text("Mastery", "muted", "small"), mastery));

                sheetBind.Text(price, () => Rs(s.Menu[id].Price));
                sheetBind.Text(selling, () => s.Menu[id].On ? "Selling" : "Off the board");
                sheetBind.Class(selling, "on", () => s.Menu[id].On);
                sheetBind.Class(card, "dim", () => !s.Menu[id].On);
                sheetBind.Text(info, () => { int plate = sim.PlateCost(id); return $"Plate {Rs(plate)} · margin {Rs(s.Menu[id].Price - plate)} · street {Rs(d.RefPrice)}"; });
                sheetBind.Do(() => mastery.Value = sim.MasteryLevel(id));
                sheetBind.Do(() =>
                {
                    float r = sim.PriceRatio(id);
                    var (text, cls) = r < 0.8f ? ("Bargain", "chip-info") : r <= 1.0f ? ("Fair price", "chip-good") : r <= 1.2f ? ("Pricey", "chip-warn") : ("Too pricey", "chip-bad");
                    if (chip.text != text)
                    {
                        chip.text = text;
                        foreach (var c in new[] { "chip-info", "chip-good", "chip-warn", "chip-bad" }) chip.EnableInClassList(c, c == cls);
                    }
                });
            }

            if (!TabUnlocked("fat")) return; // ghee or oil opens with the dhaba
            var fat = Div("card");
            Add(fat);
            fat.Add(Text("Cooking fat", "h2"));
            fat.Add(Text("Desi ghee tastes better and builds Asli-pan. Oil is cheaper per plate.", "muted", "small"));
            var ghee = Btn("Desi ghee", () => sim.SetFat("ghee"), "seg-btn");
            var oil = Btn("Cooking oil", () => sim.SetFat("oil"), "seg-btn");
            fat.Add(Div("seg").With(ghee, oil));
            sheetBind.Class(ghee, "on", () => s.Fat == "ghee");
            sheetBind.Class(oil, "on", () => s.Fat == "oil");
        }

        // ---------- Supplies ----------
        void SuppliesSheet()
        {
            var s = sim.S;
            Add(Text("Stock is bought at dawn up to each top-up level. Milk, dahi and meat spoil overnight.", "hint"));
            var topUp = Btn("", () =>
            {
                foreach (var i in sim.NeededIngredients()) { int want = s.AutoBuy[i] - s.Stock[i]; if (want > 0) sim.Buy(i, want); }
            }, "btn-primary", "wide");
            topUp.name = "topup-all";
            Add(topUp);
            int TopUpCost() => sim.NeededIngredients().Sum(i => Mathf.Max(0, s.AutoBuy[i] - s.Stock[i]) * sim.Ing[i].Cost);
            sheetBind.Text(topUp, () => { int c = TopUpCost(); return c > 0 ? $"Top up everything now · {Rs(c)}" : "Everything is topped up"; });
            sheetBind.Enabled(topUp, () => { int c = TopUpCost(); return c > 0 && c <= s.Cash; });

            var need = sim.NeededIngredients();
            foreach (var ing in gc.Data.Ingredients.OrderBy(i => need.Contains(i.Id) ? 0 : 1))
            {
                var id = ing.Id;
                bool used = need.Contains(id);
                var card = Div("card");
                if (!used) card.AddToClassList("dim");
                Add(card);
                card.Add(Div("row").With(Text(ing.En, "h2", "grow"), Text(ing.Supplier == "butcher" ? "qasai" : "kiryana", "chip", "chip-plain")));
                card.Add(Text(used ? $"{Rs(ing.Cost)} a portion" : $"{Rs(ing.Cost)} a portion · not on your menu yet", "muted", "small"));
                var (bar, fill) = Bar("stock-bar");
                var count = Text("", "small", "ink");
                card.Add(Div("row").With(bar, count));
                sheetBind.Fill(fill, () => s.AutoBuy[id] > 0 ? s.Stock[id] / (float)s.AutoBuy[id] : 0);
                sheetBind.Class(fill, "low", () => used && s.Stock[id] < 10);
                sheetBind.Class(card, "warn", () => used && s.Stock[id] < 10);
                sheetBind.Text(count, () => $"{s.Stock[id]} in stock");
                var auto = Text("", "price");
                card.Add(Div("row", "controls").With(
                    Btn("+10", () => sim.Buy(id, 10), "btn-sm"), Btn("+50", () => sim.Buy(id, 50), "btn-sm"),
                    Div("grow"), Text("Top up to", "muted", "small"),
                    Btn("−", () => sim.SetAutoBuy(id, s.AutoBuy[id] - 10), "btn-sm"), auto,
                    Btn("+", () => sim.SetAutoBuy(id, s.AutoBuy[id] + 10), "btn-sm").Named("auto-up-" + id)));
                sheetBind.Text(auto, () => s.AutoBuy[id].ToString());
            }
        }

        // ---------- Staff ----------
        void StaffSheet()
        {
            var s = sim.S;
            var cap = Text("", "hint");
            Add(cap);
            sheetBind.Text(cap, () => $"The {sim.Stage.Name} has room for {sim.Stage.StaffCap}. {sim.StaffCount} on the team, Rs {gc.Data.Staff.Where(st => sim.Hired(st.Id)).Sum(st => st.Wage):N0} a day in wages.");
            // The next hire, then the team.
            var next = Div("card", "next-hire");
            Add(next);
            var nextName = Text("", "h2"); var nextDoes = Text("", "body");
            var hireBtn = Btn("", () => sim.UpgradeTrack("staff"), "btn-primary", "wide").Named("hire-next");
            next.With(Text("NEXT HIRE", "section-title"), nextName, nextDoes, hireBtn);
            sheetBind.Visible(next, () => sim.NextHire != null);
            sheetBind.Text(nextName, () => sim.NextHire is var n && n != null ? $"{n.Name} · {n.En}" : "");
            sheetBind.Text(nextDoes, () => sim.NextHire?.Does ?? "");
            sheetBind.Text(hireBtn, () => sim.Maxed("staff") ? $"Team full for the {sim.Stage.Name}: build the next tier for more" : $"Hire · {Rs(sim.TrackCost("staff"))} now, {Rs(sim.NextHire?.Wage ?? 0)}/day");
            sheetBind.Enabled(hireBtn, () => !sim.Maxed("staff") && s.Cash >= sim.TrackCost("staff"));
            foreach (var st in gc.Data.Staff)
            {
                var id = st.Id;
                var card = Div("card", "staff-card", "hired");
                Add(card);
                card.Add(Div("row").With(Div("portrait").With(new Icon("staff")), Div("col", "grow").With(Text(st.Name, "h2"), Text(st.En, "muted", "small")), Text($"{Rs(st.Wage)}/day", "ink")));
                card.Add(Text(st.Does, "body"));
                card.Add(Btn("Let go", () => sim.Fire(id), "wide"));
                sheetBind.Visible(card, () => sim.Hired(id));
            }
        }

        // ---------- Upgrades: the business ladder and the tracks ----------
        void ShopSheet()
        {
            var s = sim.S; var b = gc.Data.Balance;
            var tiers = sim.Tiers;

            // Where you are on the ladder.
            var ladder = Div("card", "ladder-card");
            Add(ladder);
            ladder.Add(Text("YOUR PLACE ON BURNS ROAD", "section-title"));
            var steps = Div("row", "ladder");
            for (int i = 0; i < tiers.Count; i++)
            {
                int k = i;
                var step = Div("ladder-step").With(Div("ladder-dot"), Text(tiers[i].Name, "ladder-name"));
                sheetBind.Class(step, "done", () => sim.TierIndex > k);
                sheetBind.Class(step, "now", () => sim.TierIndex == k);
                steps.Add(step);
            }
            ladder.Add(steps);
            var now = Text("", "h2"); var nowNote = Text("", "muted", "small");
            ladder.With(now, nowNote);
            sheetBind.Text(now, () => $"Now: {sim.Stage.Name}");
            sheetBind.Text(nowNote, () => sim.Stage.Note);

            // The next rung.
            var nextCard = Div("card", "tier-card");
            Add(nextCard);
            var nextTitle = Text("", "h2"); var nextNote = Text("", "muted", "small");
            var needs = Div("col", "needs");
            var build = Btn("", () => sim.UpgradeTier(), "btn-primary", "wide").Named("tier-up");
            nextCard.With(Text("NEXT", "section-title"), nextTitle, nextNote, needs, build);
            string needSig = null;
            sheetBind.Do(() =>
            {
                var t = sim.NextTier;
                nextTitle.text = t == null ? "The top of Burns Road" : t.Name;
                nextNote.text = t == null ? "Next chapter: a branch in another part of Karachi (coming soon)." : t.Note;
                var list = sim.TierNeeds();
                var sig = string.Join("|", list.Select(n => n.met + n.text));
                if (sig != needSig)
                {
                    needSig = sig; needs.Clear();
                    foreach (var (text, met) in list)
                        needs.Add(Div("row", "need").Cls(met ? "met" : "unmet").With(new Icon(met ? "check" : "circle").Cls("need-mark"), Text(text, "need-text")));
                }
                build.text = t == null ? "Burns Road is yours" : $"Build the {t.Name} · {Rs(t.SetupCost)}";
                build.SetEnabled(sim.TierReady);
            });

            Add(Text("UPGRADES", "section-title"));
            Add(Text("Each track tops out at your current place. Build the next one to raise the limits.", "hint"));
            foreach (var tr in gc.Data.Business.Tracks)
            {
                var id = tr.Id;
                var card = Div("card", "track-card");
                Add(card);
                var level = Text("", "track-level");
                card.Add(Div("row").With(new Icon(tr.Icon).Cls("track-icon"), Text(tr.Name, "h2", "grow"), level));
                var pips = Div("row", "pips");
                card.Add(pips);
                card.Add(Text(tr.Note, "muted", "small"));
                var effect = Text("", "track-effect");
                card.Add(effect);
                var btn = Btn("", () => sim.UpgradeTrack(id), "btn-primary", "wide").Named("track-" + id);
                var max = Text("", "max-stamp");
                card.With(btn, max);
                int shownCap = -1;
                sheetBind.Do(() =>
                {
                    int cap = sim.Cap(id), L = sim.Level(id);
                    if (cap != shownCap) { shownCap = cap; pips.Clear(); for (int k = 0; k < Mathf.Max(cap, 1); k++) pips.Add(Div("pip")); }
                    for (int k = 0; k < pips.childCount; k++) pips[k].EnableInClassList("on", k < L);
                    level.text = cap == 0 ? "locked here" : $"{Mathf.Min(L, cap)} / {cap}";
                    effect.text = TrackEffect(tr, L, cap);
                    bool maxed = L >= cap;
                    btn.Show(!maxed); max.Show(maxed);
                    max.text = cap == 0 ? $"Opens with the next place" : L >= sim.FinalCap(id) ? "MAX" : $"MAX for the {sim.Stage.Name}";
                    btn.text = id == "staff"
                        ? $"Hire {sim.NextHire?.Name} ({sim.NextHire?.En}) · {Rs(sim.TrackCost(id))}"
                        : $"Upgrade · {Rs(sim.TrackCost(id))}";
                    btn.SetEnabled(!maxed && s.Cash >= sim.TrackCost(id));
                });
            }

            var gen = Div("card");
            Add(gen);
            gen.Add(Text("Generator", "h2"));
            gen.Add(Text($"Keeps the lights and kitchen going through load shedding. Burns about {Rs(b.Generator.FuelPerHour)} of fuel an hour while running.", "muted", "small"));
            if (s.Generator) gen.Add(Text("Installed", "chip", "chip-good"));
            else if (sim.AtLeast("dhaba"))
            {
                var buy = Btn($"Buy a generator · {Rs(b.Generator.Cost)}", () => sim.BuyGenerator(), "btn-primary", "wide");
                sheetBind.Enabled(buy, () => s.Cash >= b.Generator.Cost);
                gen.Add(buy);
            }
            else gen.Add(Text("Needs the pavement dhaba.", "muted"));

            var stats = Text("", "muted");
            Add(Div("card").With(Text("So far", "h2"), stats));
            sheetBind.Text(stats, () => $"{s.TotalServed:N0} customers served · {Rs(s.TotalRevenue)} taken in total · day {s.Day}");
        }

        // "Now 12% faster cooking · next 18%", in words a player can weigh against the price.
        string TrackEffect(KarachiTycoon.Sim.TrackDef tr, int level, int cap)
        {
            if (tr.Id == "staff") return $"{sim.StaffCount} on the team · room for {sim.Stage.StaffCap}";
            string Pct(int l) => $"{Mathf.RoundToInt(tr.Per * l * 100)}%";
            string now = level == 0 ? "Nothing yet" : $"Now {Pct(level)} {tr.Unit}";
            if (tr.Id == "seating") now += $" · {sim.SeatsAvailable} seats";
            return level >= cap ? now : $"{now} · next {Pct(level + 1)}";
        }

        // ---------- Store ----------
        void StoreSheet()
        {
            var w = gc.Wallet;
            Add(Text("Purchases are simulated until Unity IAP and Steamworks are connected. Nothing is charged.", "hint"));
            var wallet = Text("", "h2");
            var perks = Text("", "muted", "small");
            Add(Div("card").With(Div("row").With(new Icon("coin").Cls("coin-icon"), wallet), perks));
            sheetBind.Text(wallet, () => $"{w.Coins:N0} silver coins");
            sheetBind.Text(perks, () =>
            {
                var owned = new List<string>();
                if (w.NoAds) owned.Add("No Ads");
                if (w.DoubleIncome) owned.Add("2x income");
                if (w.SethJi) owned.Add($"Seth ji Club until {w.SethJiUntil:d MMM}");
                return owned.Count > 0 ? "Yours: " + string.Join(" · ", owned) : "Nothing bought yet.";
            });
            foreach (var p in gc.Store.Products)
            {
                var prod = p;
                var card = Div("card");
                Add(card);
                card.Add(Div("row").With(Text(p.Title, "h2", "grow"), Text(p.PriceUsd, "chip", "chip-plain")));
                card.Add(Text(p.Description, "muted", "small"));
                var buy = Btn("", () => gc.Buy(prod), "btn-primary", "wide");
                card.Add(buy);
                sheetBind.Text(buy, () => prod.SubscriptionDays > 0 && w.SethJi ? "Extend 30 days" : "Buy");
                sheetBind.Visible(card, () => !w.Owns(prod));
            }
        }
    }
}
