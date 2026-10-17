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
            sheetTitle.text = id switch { "Menu" => "Menu board", "Shop" => sim.S.ShopName, _ => id };
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
            sheetBind.Text(cap, () => $"{(s.Stage == "cart" ? "The thela" : "The dhaba")} has room for {sim.Stage.StaffCap}. {sim.StaffCount} hired.");
            foreach (var st in gc.Data.Staff)
            {
                var id = st.Id;
                var card = Div("card", "staff-card");
                Add(card);
                card.Add(Div("row").With(Div("portrait").With(new Icon("staff")), Div("col", "grow").With(Text(st.Name, "h2"), Text(st.En, "muted", "small")), Text($"{Rs(st.Wage)}/day", "ink")));
                card.Add(Text(st.Does, "body"));
                var hire = Btn("", () => { if (s.Staff[id]) sim.Fire(id); else sim.Hire(id); }, "wide");
                card.Add(hire);
                sheetBind.Text(hire, () => s.Staff[id] ? "Let go" : "Hire");
                sheetBind.Class(hire, "btn-primary", () => !s.Staff[id]);
                sheetBind.Class(card, "hired", () => s.Staff[id]);
            }
        }

        // ---------- Shop ----------
        void ShopSheet()
        {
            var s = sim.S; var b = gc.Data.Balance;
            Add(Text("Thela upgrades", "section-title"));
            foreach (var u in gc.Data.Upgrades)
            {
                var id = u.Id;
                var card = Div("card", "upgrade-card");
                Add(card);
                card.Add(Div("row").With(Text(u.En, "h2", "grow"), Text(Rs(u.Cost), "chip", "chip-plain")));
                card.Add(Text(u.Note, "muted", "small"));
                var buy = Btn($"Buy · {Rs(u.Cost)}", () => sim.BuyUpgrade(id), "btn-primary", "wide");
                buy.name = "upgrade-" + id;
                var owned = Text("On your thela", "chip", "chip-good");
                card.With(buy, owned);
                sheetBind.Visible(buy, () => !sim.Owns(id));
                sheetBind.Visible(owned, () => sim.Owns(id));
                sheetBind.Enabled(buy, () => s.Cash >= u.Cost);
                sheetBind.Class(card, "hired", () => sim.Owns(id));
            }
            Add(Text("Bigger plans", "section-title"));
            var dh = b.Stages["dhaba"];
            var dhaba = Div("card");
            Add(dhaba);
            dhaba.Add(Text("Pavement dhaba", "h2"));
            dhaba.Add(Text($"A takhta and plastic chairs in front of the old \"To Let\" shop. Room for {dh.Seats} to sit and {dh.StaffCap} staff.", "muted", "small"));
            if (s.Stage == "dhaba") dhaba.Add(Text("Open", "chip", "chip-good"));
            else if (s.Act1Done)
            {
                var rent = Btn($"Set up the dhaba · {Rs(dh.SetupCost)}", () => sim.RentDhaba(), "btn-primary", "wide");
                sheetBind.Enabled(rent, () => s.Cash >= dh.SetupCost);
                dhaba.Add(rent);
            }
            else dhaba.Add(Text($"Finish Act 1 first: {Rs(b.Act1.Revenue)} earned and {b.Act1.Served} served.", "muted"));

            var gen = Div("card");
            Add(gen);
            gen.Add(Text("Generator", "h2"));
            gen.Add(Text($"Keeps the lights and kitchen going through load shedding. Burns about {Rs(b.Generator.FuelPerHour)} of fuel an hour while running.", "muted", "small"));
            if (s.Generator) gen.Add(Text("Installed", "chip", "chip-good"));
            else if (s.Stage == "dhaba")
            {
                var buy = Btn($"Buy a generator · {Rs(b.Generator.Cost)}", () => sim.BuyGenerator(), "btn-primary", "wide");
                sheetBind.Enabled(buy, () => s.Cash >= b.Generator.Cost);
                gen.Add(buy);
            }
            else gen.Add(Text("Needs the dhaba.", "muted"));

            var stats = Text("", "muted");
            Add(Div("card").With(Text("So far", "h2"), stats));
            sheetBind.Text(stats, () => $"{s.TotalServed:N0} customers served · {Rs(s.TotalRevenue)} taken in total · day {s.Day}");
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
