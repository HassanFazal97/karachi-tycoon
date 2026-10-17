using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.UiKit;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // Full-screen moments: the day's hisaab (ledger), act cards, settings and the simulated ad.
    public partial class GameUi
    {
        VisualElement adOverlay;

        VisualElement OpenModal(string id, params string[] classes)
        {
            CloseModal();
            modal = id;
            var backdrop = Div("modal-backdrop");
            var box = Div("modal");
            foreach (var c in classes) box.AddToClassList(c);
            backdrop.Add(box);
            modalLayer.Add(backdrop);
            backdrop.schedule.Execute(() => backdrop.AddToClassList("in")).StartingIn(16);
            return box;
        }

        void CloseModal()
        {
            modal = null;
            modalBind.Clear();
            foreach (var c in modalLayer.Children().Where(c => c.ClassListContains("modal-backdrop")).ToList()) c.RemoveFromHierarchy();
        }

        // ---------- the day's hisaab ----------
        void OpenLedger()
        {
            CloseSheet();
            var l = sim.Today;
            var box = OpenModal("ledger", "khata");
            box.Add(new TruckStrip().Cls("modal-strip"));
            box.Add(Div("row", "khata-head").With(Text("Hisaab", "khata-title"), Div("grow"), Text($"{l.Weekday} · Day {l.Day}", "khata-date")));
            var scroll = new ScrollView(ScrollViewMode.Vertical).Cls("khata-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            box.Add(scroll);

            var cols = Div("khata-cols");
            scroll.Add(cols);
            var income = Div("khata-col").With(Text("Aamdani · sales", "khata-head2"));
            foreach (var kv in l.Sales) income.Add(Line($"{sim.Dish[kv.Key].En} × {kv.Value.Qty}", Rs(kv.Value.Rs)));
            var tips = Line("Tips from happy customers", Rs(l.Tips));
            income.Add(tips);
            var expenses = Div("khata-col").With(Text("Kharcha · expenses", "khata-head2"));
            expenses.Add(Line("Stock", Rs(l.StockBought)));
            expenses.Add(Line(sim.S.Fat == "ghee" ? "Desi ghee" : "Cooking oil", Rs(l.FatCost)));
            expenses.Add(Line("Wages", Rs(l.Wages)));
            expenses.Add(Line("Rent", Rs(l.Rent)));
            if (l.Setup > 0) expenses.Add(Line("Setup", Rs(l.Setup)));
            cols.With(income, expenses);

            var net = Text("", "khata-net");
            var stamp = Text("", "stamp");
            scroll.Add(Div("row", "khata-total").Named("khata-total").With(Text("Net for the day", "khata-head2", "grow"), net, stamp));
            if (l.Loan > 0) scroll.Add(Div("row", "khata-uncle").With(Div("portrait").With(new Icon("staff")),
                Text($"Uncle Rasheed lent you {Rs(l.Loan)} so the kiryana can deliver tomorrow. \"Pay me back when Burns Road knows your name.\"", "khata-quote")));
            modalBind.Do(() =>
            {
                int n = l.Net + (l.TipsDoubled ? l.Tips : 0);
                net.text = (n < 0 ? "-" : "") + Rs(Mathf.Abs(n));
                stamp.text = n >= 0 ? "MUNAFA" : "NUQSAN";
                stamp.EnableInClassList("loss", n < 0);
                net.EnableInClassList("loss", n < 0);
                tips.Q<Label>(className: "khata-amount").text = Rs(l.Tips) + (l.TipsDoubled ? " (doubled)" : "");
            });

            var chips = Div("row", "wrap", "khata-chips");
            chips.Add(Text($"{l.Served} served", "chip", "chip-good"));
            if (l.Walkouts > 0) chips.Add(Text($"{l.Walkouts} walked off waiting", "chip", "chip-bad"));
            if (l.Refused > 0) chips.Add(Text($"{l.Refused} said too expensive", "chip", "chip-warn"));
            if (l.QueueFull > 0) chips.Add(Text($"{l.QueueFull} saw a long line", "chip", "chip-warn"));
            scroll.Add(chips);
            if (l.Missed.Count > 0) scroll.Add(Text("Sold out: " + string.Join(", ", l.Missed.Select(kv => $"{kv.Value} {sim.Dish[kv.Key].En}")), "khata-note"));
            if (l.WastageRs > 0) scroll.Add(Text($"Spoiled overnight: {Rs(l.WastageRs)}", "khata-note"));
            scroll.Add(Text($"Shohrat {l.FameEnd - l.FameStart:+0.00;-0.00} · Asli-pan {l.AuthEnd - l.AuthStart:+0.00;-0.00}", "khata-note"));
            foreach (var n in l.Notes.Skip(System.Math.Max(0, l.Notes.Count - 2)))
                scroll.Add(Div("row", "khata-uncle").With(Div("portrait").With(new Icon("staff")), Text($"\"{n.Text}\"", "khata-quote")));

            var actions = Div("row", "modal-actions");
            box.Add(actions);
            // Rewarded ad (design doc §22): always optional.
            var ad = IconBtn("ad", gc.Ads.SethJi ? $"Seth ji Club: double tips (+{Rs(l.Tips)})" : $"Watch an ad: double tips (+{Rs(l.Tips)})", gc.DoubleTips, "ad-chip", "btn-big");
            modalBind.Visible(ad, () => gc.CanDoubleTips);
            actions.With(ad, Div("grow"), Btn("Agla din · Next day", () => { CloseModal(); gc.NextDay(); }, "btn-primary", "btn-big"));
        }

        static VisualElement Line(string label, string amount) =>
            Div("row", "khata-line").With(Text(label, "khata-label", "grow"), Text(amount, "khata-amount"));

        // ---------- act cards ----------
        void OpenActCard(int n)
        {
            var box = OpenModal(n == 1 ? "act1" : "act2", "story");
            box.Add(new TruckStrip().Cls("modal-strip"));
            box.Add(Text(n == 1 ? "ACT 1 COMPLETE" : "ACT 2 COMPLETE", "label-gold"));
            box.Add(Text(n == 1 ? "Thela se takhta" : "Burns Road knows your name", "story-title"));
            box.Add(Text(n == 1
                ? "People on Burns Road know your chai now. The owner upstairs says you can put a takhta and plastic chairs on the pavement in front of the old \"To Let\" shop."
                : $"Two-star Shohrat and Rs 3 lakh saved, on day {sim.S.Day}. Uncle Rasheed has a table now, and the nihari deg is empty by nine. This is the end of the Burns Road slice.", "story-body"));
            var actions = Div("row", "modal-actions");
            box.Add(actions);
            if (n == 1) actions.With(Div("grow"), Btn("Open the Shop", () => { CloseModal(); OpenSheet("Shop"); }, "btn-primary", "btn-big"));
            else actions.With(Div("grow"), Btn("Keep playing", CloseModal, "btn-primary", "btn-big"));
        }

        // ---------- settings / pause ----------
        void OpenSettings()
        {
            var box = OpenModal("settings", "settings");
            box.Add(Div("row").With(Text(gc.Playing ? "Paused" : "Settings", "story-title", "grow"), IconBtn("close", null, CloseModal, "close-btn")));

            box.Add(Slider("Street sounds", Settings.StreetVolume, v => Settings.StreetVolume = v));
            box.Add(Slider("Effects", Settings.SfxVolume, v => Settings.SfxVolume = v));

            box.Add(Text("Layout", "label-gold"));
            box.Add(Seg(new[] { ("auto", "Auto"), ("phone", "Phone"), ("desktop", "Desktop") }, Settings.Layout, v => Settings.Layout = v));
            box.Add(Text("Interface size", "label-gold"));
            box.Add(Seg(new[] { ("0.85", "Small"), ("1", "Medium"), ("1.2", "Large") }, Settings.UiScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                v => Settings.UiScale = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));

            if (gc.Playing) box.Add(IconBtn("book", "Dada ki Kitaab: every tip", () => { CloseModal(); OpenKitaab(); }, "wide"));
            if (!phone) box.Add(Text("Keys: Space pause · 1 2 3 speed · A/D pan · +/- zoom · M S H B panels · Q shout · Esc close", "muted", "small", "keys"));

            var actions = Div("row", "modal-actions");
            box.Add(actions);
            if (gc.Playing) actions.Add(IconBtn("save", "Save", () => { gc.Save(); gc.Toast("Saved. Loading resumes from the start of today.", "tip"); CloseModal(); }, "btn-big"));
            actions.With(Div("grow"), Btn(gc.Playing ? "Resume" : "Done", CloseModal, "btn-primary", "btn-big"));
        }

        static VisualElement Slider(string label, float value, System.Action<float> set)
        {
            var s = new Slider(0, 1) { value = value };
            s.AddToClassList("kt-slider");
            s.RegisterValueChangedCallback(e => set(e.newValue));
            return Div("row", "setting-row").With(Text(label, "setting-label"), s);
        }

        static VisualElement Seg((string value, string label)[] options, string current, System.Action<string> set)
        {
            var seg = Div("seg");
            foreach (var (v, l) in options)
            {
                var b = Btn(l, null, "seg-btn");
                b.clicked += () => { foreach (var c in seg.Children()) c.RemoveFromClassList("on"); b.AddToClassList("on"); set(v); };
                b.EnableInClassList("on", v == current);
                seg.Add(b);
            }
            return seg;
        }

        // ---------- simulated ad ----------
        void UpdateAdOverlay()
        {
            bool showing = gc.Ads.Showing != null;
            if (showing && adOverlay == null)
            {
                adOverlay = Div("ad-overlay").With(new Icon("ad").Cls("ad-icon"), Text(gc.Ads.Showing, "story-title"), Text("Simulated ad · the real SDK plugs in through IAdsProvider", "muted"));
                root.Add(adOverlay);
            }
            else if (!showing && adOverlay != null) { adOverlay.RemoveFromHierarchy(); adOverlay = null; }
        }
    }
}
