using System;
using System.IO;
using KarachiTycoon.Game.Ads;

// dotnet run --project Tools/AdRulesCheck
static class Program
{
    static int failures;

    static void Check(string what, bool ok, string detail = "")
    {
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what} {detail}");
        if (!ok) failures++;
    }

    static string Hm(double h) => double.IsNaN(h) ? "--:--" : $"{(int)h:00}:{(int)Math.Round((h - (int)h) * 60):00}";

    static int Main()
    {
        // ---- prayer times against published timetables (within 10 minutes) ----
        var karachi = new PrayerTimes(24.86, 67.01);
        var k = karachi.Hours(new DateTime(2026, 10, 14), 5);
        Console.WriteLine($"Karachi 14 Oct 2026: Fajr {Hm(k[0])} Dhuhr {Hm(k[1])} Asr {Hm(k[2])}/{Hm(k[3])} Maghrib {Hm(k[4])} Isha {Hm(k[5])}");
        bool Near(double h, int hh, int mm) => Math.Abs(h - (hh + mm / 60.0)) < 10 / 60.0;
        Check("Karachi Dhuhr (solar noon) ~12:18", Near(k[1], 12, 18));
        Check("Karachi Maghrib ~18:04", Near(k[4], 18, 4));
        Check("Karachi Fajr ~05:12", Near(k[0], 5, 12));
        var toronto = new PrayerTimes(43.65, -79.38).Hours(new DateTime(2026, 10, 14), -4);
        Console.WriteLine($"Toronto 14 Oct 2026: Fajr {Hm(toronto[0])} Dhuhr {Hm(toronto[1])} Maghrib {Hm(toronto[4])} Isha {Hm(toronto[5])}");
        Check("Toronto Dhuhr (solar noon) ~13:04", Near(toronto[1], 13, 4));
        Check("Toronto Maghrib ~18:35", Near(toronto[4], 18, 35));

        var cfg = AdConfig.Load(File.ReadAllText("../../Assets/Resources/Data/ads.json"), "PK");
        Check("config loads placements", cfg.Rule("refill_shout").CooldownSeconds == 90 && cfg.RewardedSoftCapPerDay == 20);

        DateTime At(int h, int m) => new DateTime(2026, 10, 14, h, m, 0);
        Check("blackout at Karachi Maghrib", karachi.Blackout(At(18, 6), cfg, 5) == "prayer");
        Check("no blackout mid-morning", karachi.Blackout(At(10, 0), cfg, 5) == null);
        Check("Asr window spans Shafi to Hanafi", karachi.Blackout(At(16, 0), cfg, 5) == "prayer");

        var ramadan = new DateTime(2026, 3, 1);
        Check("1 Mar 2026 is Ramadan", PrayerTimes.Hijri(ramadan).month == 9);
        var rk = karachi.Hours(ramadan, 5);
        var beforeIftar = ramadan.AddHours(rk[4] - 25 / 60.0);
        Check("iftar blackout 25 min before Maghrib in Ramadan", karachi.Blackout(beforeIftar, cfg, 5) == "iftar");
        Check("no iftar blackout outside Ramadan", karachi.Blackout(new DateTime(2026, 10, 14).AddHours(k[4] - 25 / 60.0), cfg, 5) == null);
        DateTime ashura = default;
        for (var d = new DateTime(2026, 6, 15); d < new DateTime(2026, 7, 10); d = d.AddDays(1))
            if (PrayerTimes.Hijri(d) == (1, 10)) ashura = d;
        Check("Ashura found", ashura != default, ashura.ToString("d MMM yyyy"));
        Check("no ads on Ashura", karachi.Blackout(ashura.AddHours(10), cfg, 5) == "muharram");

        // ---- rules on a fake clock (10:00, nowhere near a prayer) ----
        var now = At(10, 0);
        var rules = new AdRules(cfg, new AdLedger(), karachi, () => now);
        var ctx = new AdContext { GameDay = 3, Session = 5, AdsSupported = true };
        Check("rewarded allowed normally", rules.CanRewarded("double_tips", ctx) == null);
        Check("no rewarded in the tutorial", rules.CanRewarded("double_tips", new AdContext { GameDay = 1, Session = 1, Tutorial = true, AdsSupported = true }) == "tutorial");
        Check("no rewarded on day 1", rules.CanRewarded("double_tips", new AdContext { GameDay = 1, Session = 1, AdsSupported = true }) == "too_early");
        Check("no rewarded on Steam", rules.CanRewarded("double_tips", new AdContext { GameDay = 3, Session = 5 }) == "unsupported");
        Check("Seth ji gets the reward on Steam too", rules.CanRewarded("double_tips", new AdContext { GameDay = 3, Session = 5 }, watching: false) == null);
        rules.MarkRewarded("refill_shout");
        Check("refill_shout cooldown", rules.CanRewarded("refill_shout", ctx) == "cooldown");
        now = now.AddSeconds(91);
        Check("refill_shout after cooldown", rules.CanRewarded("refill_shout", ctx) == null);
        for (int i = 0; i < 3; i++) rules.MarkRewarded("double_tips");
        Check("double_tips per-placement cap", rules.CanRewarded("double_tips", ctx) == "placement_cap");
        while (rules.Ledger.RewardedToday < 20) rules.MarkRewarded("finish_order_x");
        Check("daily soft cap", rules.CanRewarded("swap_goal", ctx) == "daily_cap");
        Check("welcome_back ignores the soft cap", rules.CanRewarded("welcome_back", ctx) == null);
        now = now.AddDays(1);
        Check("caps reset on a new day", rules.CanRewarded("swap_goal", ctx) == null);

        // interstitials
        var r2 = new AdRules(cfg, new AdLedger(), karachi, () => now);
        Check("no interstitial in session 3", r2.CanInterstitial(new AdContext { Session = 3, AdsSupported = true }) == "early_session");
        Check("no interstitial before 20 min of play", r2.CanInterstitial(ctx) == "early_playtime");
        r2.AddPlayTime(21 * 60);
        Check("interstitial allowed after that", r2.CanInterstitial(ctx) == null);
        Check("never with No Ads", r2.CanInterstitial(new AdContext { Session = 5, NoAds = true, AdsSupported = true }) == "no_ads");
        r2.MarkRewarded("double_tips");
        Check("skipped right after a rewarded view", r2.CanInterstitial(ctx) == "after_rewarded");
        now = now.AddMinutes(4);
        r2.MarkInterstitial();
        Check("gap between interstitials", r2.CanInterstitial(ctx) == "gap");
        for (int i = 0; i < 5; i++) { now = now.AddMinutes(11); r2.MarkInterstitial(); }
        now = now.AddMinutes(11);
        Check("6 a day at most", r2.CanInterstitial(ctx) == "daily_cap");
        now = now.AddDays(1);
        r2.MarkPurchase();
        Check("off after a purchase", r2.CanInterstitial(ctx) == "recent_purchase");
        now = now.AddDays(31);
        Check("back 30 days after a purchase", r2.CanInterstitial(ctx) == null);
        Check("blocked by the in-game azaan", r2.CanInterstitial(new AdContext { Session = 5, AdsSupported = true, InGameSensitive = true }) == "azaan");

        Console.WriteLine(failures == 0 ? "\nAll ad rule checks passed." : $"\n{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
