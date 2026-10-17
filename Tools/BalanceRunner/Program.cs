// Plays the Burns Road slice with a simple "sensible player" strategy and prints each day.
// Usage: dotnet run --project Tools/BalanceRunner -- [ghee|oil] [seed]
using System;
using System.IO;
using System.Linq;
using KarachiTycoon.Sim;

class Program
{
    static void Main(string[] args)
    {
        string fat = args.Length > 0 ? args[0] : "ghee";
        int seed = args.Length > 1 ? int.Parse(args[1]) : 7;
        string dataDir = Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Resources/Data");
        var data = GameData.Load(name => File.ReadAllText(Path.Combine(dataDir, name + ".json")));
        var rng = new Random(seed);
        var sim = new Sim(data, () => rng.NextDouble());
        if (fat == "migrate")
        {
            // An old save: M3 upgrades, two-person staff, the dhaba, no tracks.
            sim.NewGame("Old", "پرانا", "pota");
            var j = Newtonsoft.Json.Linq.JObject.Parse(sim.Serialize());
            j["Stage"] = "dhaba"; j["Act1Done"] = true;
            j["Owned"] = new Newtonsoft.Json.Linq.JArray("stools", "kettle", "tawa2", "sign");
            j["Staff"] = Newtonsoft.Json.Linq.JObject.Parse("{\"chhotu\":true,\"karigar\":true}");
            j.Remove("Tracks");
            var old = new Sim(data, () => rng.NextDouble());
            old.Load(j.ToString());
            Console.WriteLine($"migrated: tier {old.Stage.Name}, staff {old.Level("staff")}/{old.Cap("staff")}, kitchen {old.Level("kitchen")}, seating {old.Level("seating")}, signboard {old.Level("signboard")}, seats {old.SeatsAvailable}, next hire {old.NextHire?.Name}");
            while (!old.S.DayOver) old.Tick(0.5f);
            Console.WriteLine($"day ran: served {old.Today.Served}, net {old.Today.Net}");
            return;
        }
        sim.ActDone += n => Console.WriteLine($"  ** act {n} done on day {sim.S.Day} at {(int)(sim.S.Minute / 60)}h");
        sim.Unlocked += d => Console.WriteLine($"  unlock {d} day {sim.S.Day}");
        sim.NewGame("Test", "ٹیسٹ", "pota");
        sim.SetFat(fat);
        for (int day = 0; day < 60 && sim.NextTier != null; day++)
        {
            // A sensible player: keep a day's stock in reserve, build the next tier when ready,
            // otherwise buy the cheapest useful upgrade within the tier's caps.
            // Leftover stock carries over, so a sensible player keeps about half a day's stock in hand.
            int reserve = sim.OneDayOfStock() / 2 + 3000;
            if (Environment.GetEnvironmentVariable("KT_DEBUG") != null) Console.WriteLine($"  [d{sim.S.Day}] cash {sim.S.Cash} reserve {reserve} needs " + string.Join("; ", sim.TierNeeds().Select(n => (n.met ? "+" : "-") + n.text)));
            for (bool bought = true; bought;)
            {
                bought = false;
                if (sim.TierReady && sim.S.Cash >= sim.NextTier.SetupCost + reserve)
                {
                    var name = sim.NextTier.Name;
                    if (sim.UpgradeTier()) { Console.WriteLine($"  ## {name} on day {sim.S.Day}"); bought = true; continue; }
                }
                // Once the next tier only lacks cash, save for it (but still buy what it requires).
                var needs = sim.TierNeeds();
                bool saving = sim.NextTier != null && needs.Take(needs.Count - 1).All(n => n.met);
                int keep = reserve + (saving ? sim.NextTier.SetupCost : 0);
                var required = sim.NextTier?.Requires?.Maxed ?? new System.Collections.Generic.List<string>();
                foreach (var tr in new[] { "staff", "kitchen", "training", "signboard", "seating", "comfort", "storage" })
                    if (!sim.Maxed(tr) && sim.S.Cash >= sim.TrackCost(tr) + (required.Contains(tr) ? reserve : keep) && sim.UpgradeTrack(tr)) { bought = true; break; }
            }
            if (sim.AtLeast("dhaba") && !sim.S.Generator && sim.S.Cash > 80000 + reserve) sim.BuyGenerator();
            while (!sim.S.DayOver) { sim.Tick(0.25f); if (sim.S.Minute > 360 && sim.S.Minute % 180 < 0.25f) sim.Shout(); }
            var t = sim.Today;
            Console.WriteLine($"day {t.Day,2} {t.Weekday} {sim.S.Stage,-10} rev {t.Revenue,6} tips {t.Tips,5} net {t.Net,6} served {t.Served,3} walk {t.Walkouts} refuse {t.Refused} full {t.QueueFull} sold {t.SoldOut} waste {t.WastageRs} | cash {sim.S.Cash} fame {sim.S.Fame:F2} auth {sim.S.Auth:F2}");
            foreach (var kv in t.Missed.Where(kv => kv.Value > 10))
                foreach (var k in sim.Dish[kv.Key].Ingredients.Keys) sim.SetAutoBuy(k, sim.S.AutoBuy[k] + (int)Math.Round(kv.Value * 0.6));
            sim.NextDay();
        }
    }
}
