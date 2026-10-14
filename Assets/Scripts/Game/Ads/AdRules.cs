using System;
using System.Collections.Generic;

namespace KarachiTycoon.Game.Ads
{
    // What the rules need to know about the moment an ad would show.
    public struct AdContext
    {
        public int GameDay;
        public int Session;
        public bool InGameSensitive;  // the in-game azaan (the game clock, not the real one)
        public bool Tutorial;
        public bool NoAds;            // bought No Ads: no interstitials (rewarded stays, it is the player's choice)
        public bool AdsSupported;     // false on Steam and on builds without an ad SDK
    }

    // Saved counters. Days are the device's local calendar days.
    public class AdLedger
    {
        public string Day = "";
        public int RewardedToday, InterstitialsToday;
        public Dictionary<string, int> PlacementToday = new Dictionary<string, int>();
        public Dictionary<string, double> PlacementLast = new Dictionary<string, double>();
        public double LastInterstitial = -1e9, LastRewarded = -1e9, LastPurchase = -1e9;
        public double PlaySeconds;
    }

    // The ad rules from design doc §22, as pure decisions. Each check returns null when an ad may show,
    // or the reason it may not (logged to analytics, so tuning is visible).
    public class AdRules
    {
        public AdConfig Config;
        public readonly AdLedger Ledger;
        readonly PrayerTimes prayers;
        readonly Func<DateTime> now;

        public AdRules(AdConfig config, AdLedger ledger, PrayerTimes prayerTimes, Func<DateTime> clock)
        {
            Config = config; Ledger = ledger ?? new AdLedger(); prayers = prayerTimes; now = clock;
        }

        static double Seconds(DateTime t) => (t - new DateTime(2020, 1, 1)).TotalSeconds;

        void Roll()
        {
            var today = now().ToString("yyyy-MM-dd");
            if (Ledger.Day == today) return;
            Ledger.Day = today; Ledger.RewardedToday = 0; Ledger.InterstitialsToday = 0; Ledger.PlacementToday.Clear();
        }

        string Blackout(AdContext c) => c.InGameSensitive ? "azaan" : prayers?.Blackout(now(), Config);

        // watching=false for Seth ji Club members, who get the reward without an ad: then only the caps apply.
        public string CanRewarded(string placement, AdContext c, bool watching = true)
        {
            Roll();
            var rule = Config.Rule(placement);
            if (c.Tutorial) return "tutorial";
            if (c.GameDay < rule.MinDay) return "too_early";
            if (watching && !c.AdsSupported) return "unsupported";
            if (watching) { var b = Blackout(c); if (b != null) return b; }
            if (Ledger.PlacementToday.TryGetValue(placement, out var n) && n >= rule.CapPerDay) return "placement_cap";
            if (rule.CountsTowardCap && Ledger.RewardedToday >= Config.RewardedSoftCapPerDay) return "daily_cap";
            if (Ledger.PlacementLast.TryGetValue(placement, out var last) && Seconds(now()) - last < rule.CooldownSeconds) return "cooldown";
            return null;
        }

        public void MarkRewarded(string placement)
        {
            Roll();
            var rule = Config.Rule(placement);
            Ledger.PlacementToday[placement] = (Ledger.PlacementToday.TryGetValue(placement, out var n) ? n : 0) + 1;
            Ledger.PlacementLast[placement] = Seconds(now());
            if (rule.CountsTowardCap) Ledger.RewardedToday++;
            Ledger.LastRewarded = Seconds(now());
        }

        public string CanInterstitial(AdContext c)
        {
            Roll();
            double t = Seconds(now());
            if (!c.AdsSupported) return "unsupported";
            if (c.NoAds) return "no_ads";
            if (c.Tutorial) return "tutorial";
            if (c.Session < Config.InterstitialFirstSession) return "early_session";
            if (Ledger.PlaySeconds < Config.InterstitialMinPlayMinutes * 60) return "early_playtime";
            if (t - Ledger.LastPurchase < Config.InterstitialPauseDaysAfterPurchase * 86400.0) return "recent_purchase";
            if (Ledger.InterstitialsToday >= Config.InterstitialMaxPerDay) return "daily_cap";
            if (t - Ledger.LastInterstitial < Config.InterstitialMinGapSeconds) return "gap";
            if (t - Ledger.LastRewarded < Config.InterstitialAfterRewardedSeconds) return "after_rewarded";
            return Blackout(c);
        }

        public void MarkInterstitial() { Roll(); Ledger.InterstitialsToday++; Ledger.LastInterstitial = Seconds(now()); }
        public void MarkPurchase() => Ledger.LastPurchase = Seconds(now());
        public void AddPlayTime(double seconds) => Ledger.PlaySeconds += seconds;
    }
}
