using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KarachiTycoon.Game.Ads
{
    // Every number in the ad rules (design doc §22). Local defaults live in Resources/Data/ads.json;
    // a Remote Config provider can override them per country without a new build.
    // Plain C# (no UnityEngine) so the rules can be checked from Tools/AdRulesCheck.
    public class PlacementRule
    {
        public int CapPerDay = 5;
        public int CooldownSeconds;
        // Placements that do not use up the daily soft cap (welcome-back and generator offers).
        public bool CountsTowardCap = true;
        // First in-game day the offer may appear (the tutorial day has no ads at all).
        public int MinDay = 2;
    }

    public class AdConfig
    {
        public int RewardedSoftCapPerDay = 20;

        public int InterstitialFirstSession = 4;          // none in sessions 1-3
        public int InterstitialMinPlayMinutes = 20;       // nor in the first 20 minutes of play, ever
        public int InterstitialMaxPerDay = 6;
        public int InterstitialMinGapSeconds = 600;
        public int InterstitialAfterRewardedSeconds = 180; // skipped right after a rewarded view
        public int InterstitialPauseDaysAfterPurchase = 30;

        public int PrayerBlackoutMinutes = 10;            // either side of each prayer time
        public bool RamadanIftarBlackout = true;
        public int IftarBlackoutBeforeMinutes = 30, IftarBlackoutAfterMinutes = 45;
        public bool MuharramBlackout = true;              // 9 and 10 Muharram, all day

        public Dictionary<string, PlacementRule> Placements = new Dictionary<string, PlacementRule>
        {
            ["double_tips"] = new PlacementRule { CapPerDay = 3 },
            ["refill_shout"] = new PlacementRule { CapPerDay = 6, CooldownSeconds = 90 },
            ["welcome_back"] = new PlacementRule { CapPerDay = 2, CountsTowardCap = false },
            ["generator"] = new PlacementRule { CapPerDay = 3, CountsTowardCap = false },
            ["finish_order"] = new PlacementRule { CapPerDay = 5, CooldownSeconds = 60 },
            ["swap_goal"] = new PlacementRule { CapPerDay = 1 },
            ["event_reward"] = new PlacementRule { CapPerDay = 1 },
        };

        public PlacementRule Rule(string placement) =>
            Placements.TryGetValue(placement, out var r) ? r : new PlacementRule();

        // json: { "default": { ...fields... }, "countries": { "PK": { ...overrides... } } }
        // Country overrides replace whole fields; a placement given there replaces that placement's rule.
        public static AdConfig Load(string json, string country)
        {
            var cfg = new AdConfig();
            if (string.IsNullOrEmpty(json)) return cfg;
            var root = JObject.Parse(json);
            var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Reuse };
            if (root["default"] is JObject d) JsonConvert.PopulateObject(d.ToString(), cfg, settings);
            if (!string.IsNullOrEmpty(country) && root["countries"]?[country] is JObject c) JsonConvert.PopulateObject(c.ToString(), cfg, settings);
            return cfg;
        }
    }

    // Where config values come from. LocalRemoteConfig reads the bundled defaults; a real
    // provider (Unity Remote Config, Firebase) returns its fetched JSON for the same key.
    public interface IRemoteConfig
    {
        string Get(string key);
        string Country { get; }
    }
}
