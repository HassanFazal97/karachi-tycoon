using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KarachiTycoon.Game
{
    // Free-to-play hooks (design doc §22). The game only talks to these interfaces, so the real
    // SDKs (Unity LevelPlay for ads, Unity IAP and Steamworks for purchases) plug in later without
    // touching gameplay code. The simulated versions below let the whole flow be played in the editor.

    public interface IAdsService
    {
        bool RewardedReady { get; }
        // placement: "double_tips", "refill_shout", ... onDone(true) only when the reward was earned.
        void ShowRewarded(string placement, Action<bool> onDone);
        // Interstitials only at natural breaks; the caller checks AdPolicy first.
        void ShowInterstitial(string placement, Action onDone);
    }

    public class Product
    {
        public string Id, Title, Description, PriceUsd;
        public int Coins;
        public bool Consumable;
    }

    public static class Catalog
    {
        public static readonly List<Product> Products = new List<Product>
        {
            new Product { Id = "no_ads", Title = "No Ads", Description = "Removes interstitial ads forever. Rewarded ads stay available for their bonuses.", PriceUsd = "$2.99" },
            new Product { Id = "starter_dadas_trunk", Title = "Dada's Trunk", Description = "One-time starter pack: 300 silver coins, a truck-art signboard style and a brass galla.", PriceUsd = "$1.99", Coins = 300 },
            new Product { Id = "coins_small", Title = "Pouch of sikkay", Description = "120 silver coins.", PriceUsd = "$0.99", Coins = 120, Consumable = true },
            new Product { Id = "coins_medium", Title = "Galla of sikkay", Description = "700 silver coins.", PriceUsd = "$4.99", Coins = 700, Consumable = true },
            new Product { Id = "coins_large", Title = "Seth's tijori", Description = "1,600 silver coins.", PriceUsd = "$9.99", Coins = 1600, Consumable = true },
        };
    }

    public interface IStoreService
    {
        IReadOnlyList<Product> Products { get; }
        void Purchase(string productId, Action<bool> onDone);
    }

    // Rules from design doc §19 and §22 that every ad call goes through.
    public class AdPolicy
    {
        public float MinSecondsBetweenInterstitials = 600;
        public int SessionsWithoutInterstitials = 2;
        public int MaxRewardedPerDay = 8;
        float lastInterstitial = -9999;
        int rewardedToday;
        string rewardedDay = "";

        public bool CanShowInterstitial(Sim.GameState s, int sessionCount, bool sensitiveMoment)
        {
            if (s.NoAds || sensitiveMoment) return false;
            if (sessionCount <= SessionsWithoutInterstitials) return false;
            return Time.realtimeSinceStartup - lastInterstitial >= MinSecondsBetweenInterstitials;
        }
        public void MarkInterstitial() => lastInterstitial = Time.realtimeSinceStartup;

        public bool CanShowRewarded(bool sensitiveMoment)
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (today != rewardedDay) { rewardedDay = today; rewardedToday = 0; }
            return !sensitiveMoment && rewardedToday < MaxRewardedPerDay;
        }
        public void MarkRewarded() => rewardedToday++;
    }

    // Stand-in used until the real SDKs are added: shows a short "ad" overlay and grants the reward.
    public class SimulatedAds : MonoBehaviour, IAdsService
    {
        public string Showing;
        public bool RewardedReady => Showing == null;

        public void ShowRewarded(string placement, Action<bool> onDone) => StartCoroutine(Play("Rewarded ad: " + placement, () => onDone(true)));
        public void ShowInterstitial(string placement, Action onDone) => StartCoroutine(Play("Ad break", onDone));

        IEnumerator Play(string label, Action done)
        {
            Showing = label;
            yield return new WaitForSecondsRealtime(1.5f);
            Showing = null;
            done();
        }
    }

    public class SimulatedStore : IStoreService
    {
        readonly Sim.GameState state;
        public SimulatedStore(Sim.GameState s) { state = s; }
        public IReadOnlyList<Product> Products => Catalog.Products;
        public void Purchase(string productId, Action<bool> onDone)
        {
            var p = Catalog.Products.Find(x => x.Id == productId);
            if (p == null) { onDone(false); return; }
            if (p.Id == "no_ads") state.NoAds = true;
            state.Coins += p.Coins;
            onDone(true);
        }
    }
}
