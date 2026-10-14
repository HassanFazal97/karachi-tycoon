using System;
using Newtonsoft.Json;
using UnityEngine;

namespace KarachiTycoon.Game.Ads
{
    // The one door to ads for the rest of the game. It asks AdRules whether an ad may show,
    // plays it through the platform's IAdsProvider, gives Seth ji Club members the reward without
    // an ad, saves the counters and reports every outcome to analytics.
    public class AdsService
    {
        const string LedgerKey = "kt_ads_v1";
        public readonly IAdsProvider Provider;
        public readonly AdRules Rules;
        readonly IAnalytics analytics;
        readonly Func<AdContext> context;
        readonly Func<Wallet> wallet;
        float sinceSave;

        public AdsService(IAdsProvider provider, IRemoteConfig config, IAnalytics analytics, Func<AdContext> context, Func<Wallet> wallet)
        {
            Provider = provider; this.analytics = analytics; this.context = context; this.wallet = wallet;
            AdLedger ledger = null;
            try { ledger = JsonConvert.DeserializeObject<AdLedger>(PlayerPrefs.GetString(LedgerKey, "")); } catch { }
            Rules = new AdRules(AdConfig.Load(config.Get("ads"), config.Country), ledger, PrayerTimes.ForLocalZone(), () => DateTime.Now);
            Provider.Initialize(true);
            analytics.Event("ads_init", ("provider", provider.Name), ("country", config.Country));
        }

        // Picks the SDK for this platform. Steam builds never show ads (Valve's rules).
        public static IAdsProvider CreateProvider(GameObject host)
        {
            if (ShotRunner.Arg("-kt-sim-ads") != null || Application.isEditor) return host.AddComponent<SimulatedAdsProvider>();
#if APPLOVIN_MAX && (UNITY_ANDROID || UNITY_IOS)
            return new AppLovinMaxProvider();
#elif UNITY_STANDALONE
            return new NoAdsProvider("steam");
#else
            return new NoAdsProvider("no sdk");
#endif
        }

        public bool SethJi => wallet().SethJi;
        public string Showing => Provider.Showing;

        // Whether to show the offer button at all.
        public bool CanOffer(string placement)
        {
            bool watching = !SethJi;
            if (watching && !Provider.RewardedReady) return false;
            return Rules.CanRewarded(placement, context(), watching) == null;
        }

        public void Rewarded(string placement, Action grant)
        {
            bool watching = !SethJi;
            var why = Rules.CanRewarded(placement, context(), watching);
            if (why != null) { analytics.Event("ad_rewarded_blocked", ("placement", placement), ("reason", why)); return; }
            if (!watching)
            {
                Rules.MarkRewarded(placement); Save();
                analytics.Event("ad_rewarded_granted", ("placement", placement), ("via", "seth_ji"));
                grant();
                return;
            }
            analytics.Event("ad_rewarded_start", ("placement", placement), ("provider", Provider.Name));
            Provider.ShowRewarded(placement, ok =>
            {
                analytics.Event(ok ? "ad_rewarded_complete" : "ad_rewarded_skipped", ("placement", placement));
                if (!ok) return;
                Rules.MarkRewarded(placement); Save();
                grant();
            });
        }

        // At a natural break (the day-end ledger): maybe an interstitial, then carry on.
        public void AtBreak(string placement, Action next)
        {
            var why = Rules.CanInterstitial(context()) ?? (Provider.InterstitialReady ? null : "not_ready");
            if (why != null) { analytics.Event("ad_interstitial_skipped", ("placement", placement), ("reason", why)); next(); return; }
            Rules.MarkInterstitial(); Save();
            analytics.Event("ad_interstitial_shown", ("placement", placement), ("provider", Provider.Name));
            Provider.ShowInterstitial(placement, next);
        }

        public void OnPurchase() { Rules.MarkPurchase(); Save(); }

        // Counts play time (interstitials wait for 20 minutes of it) and saves now and then.
        public void Tick(float realSeconds)
        {
            Rules.AddPlayTime(realSeconds);
            if ((sinceSave += realSeconds) > 30) Save();
        }

        public void Save() { sinceSave = 0; PlayerPrefs.SetString(LedgerKey, JsonConvert.SerializeObject(Rules.Ledger)); }
    }
}
