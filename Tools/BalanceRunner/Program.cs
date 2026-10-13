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
        sim.ActDone += n => Console.WriteLine($"  ** act {n} done on day {sim.S.Day} at {(int)(sim.S.Minute / 60)}h");
        sim.Unlocked += d => Console.WriteLine($"  unlock {d} day {sim.S.Day}");
        sim.NewGame("Test", "ٹیسٹ", "pota");
        sim.SetFat(fat);
        for (int day = 0; day < 40 && !sim.S.Act2Done; day++)
        {
            if (sim.S.Day == 2) sim.Hire("chhotu");
            if (sim.S.Act1Done && sim.S.Stage == "cart" && sim.S.Cash > 45000) { sim.RentDhaba(); sim.Hire("karigar"); }
            if (sim.S.Stage == "dhaba" && !sim.S.Generator && sim.S.Cash > 80000) sim.BuyGenerator();
            while (!sim.S.DayOver) { sim.Tick(0.25f); if (sim.S.Minute > 360 && sim.S.Minute % 180 < 0.25f) sim.Shout(); }
            var t = sim.Today;
            Console.WriteLine($"day {t.Day,2} {t.Weekday} {sim.S.Stage,-5} rev {t.Revenue,6} tips {t.Tips,5} net {t.Net,6} served {t.Served,3} walk {t.Walkouts} refuse {t.Refused} full {t.QueueFull} sold {t.SoldOut} waste {t.WastageRs} | cash {sim.S.Cash} fame {sim.S.Fame:F2} auth {sim.S.Auth:F2}");
            foreach (var kv in t.Missed.Where(kv => kv.Value > 10))
                foreach (var k in sim.Dish[kv.Key].Ingredients.Keys) sim.SetAutoBuy(k, sim.S.AutoBuy[k] + (int)Math.Round(kv.Value * 0.6));
            sim.NextDay();
        }
    }
}
