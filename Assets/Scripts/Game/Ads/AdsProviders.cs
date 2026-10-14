using System;
using System.Collections;
using UnityEngine;

namespace KarachiTycoon.Game.Ads
{
    // A thin wrapper around one ad SDK. It knows nothing about the rules: AdsService decides
    // whether an ad may show, the provider only loads and plays it.
    public interface IAdsProvider
    {
        string Name { get; }
        bool Supported { get; }
        bool RewardedReady { get; }
        bool InterstitialReady { get; }
        // A short label while a full-screen ad is up (the game pauses meanwhile), else null.
        string Showing { get; }
        void Initialize(bool personalizedAdsConsent);
        // onDone(true) only when the reward was earned.
        void ShowRewarded(string placement, Action<bool> onDone);
        void ShowInterstitial(string placement, Action onDone);
    }

    // Editor and test builds: a short fake ad overlay that always pays out.
    public class SimulatedAdsProvider : MonoBehaviour, IAdsProvider
    {
        public string Name => "simulated";
        public bool Supported => true;
        public bool RewardedReady => Showing == null;
        public bool InterstitialReady => Showing == null;
        public string Showing { get; private set; }
        public void Initialize(bool consent) { }
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

    // Steam (Valve does not allow ads in games) and any build without an SDK. Rewarded offers
    // simply never appear; Seth ji Club still grants its rewards.
    public class NoAdsProvider : IAdsProvider
    {
        public NoAdsProvider(string why) { Name = "none (" + why + ")"; }
        public string Name { get; }
        public bool Supported => false;
        public bool RewardedReady => false;
        public bool InterstitialReady => false;
        public string Showing => null;
        public void Initialize(bool consent) { }
        public void ShowRewarded(string placement, Action<bool> onDone) => onDone(false);
        public void ShowInterstitial(string placement, Action onDone) => onDone();
    }

    // AppLovin MAX for Android and iOS. Stub until the SDK is imported: add the AppLovin MAX Unity
    // plugin, define APPLOVIN_MAX in Player Settings, and fill in the ad unit ids below.
    public class AppLovinMaxProvider : IAdsProvider
    {
        public const string SdkKey = "";
        public static readonly string RewardedUnit = Application.platform == RuntimePlatform.IPhonePlayer ? "" : "";
        public static readonly string InterstitialUnit = Application.platform == RuntimePlatform.IPhonePlayer ? "" : "";

        public string Name => "applovin_max";
#if APPLOVIN_MAX
        // TODO: MaxSdk.SetSdkKey / InitializeSdk, MaxSdk.SetHasUserConsent, load and show using the units above,
        // and call back from MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent / OnAdHiddenEvent.
        public bool Supported => true;
#else
        public bool Supported => false;
#endif
        public bool RewardedReady => false;
        public bool InterstitialReady => false;
        public string Showing => null;
        public void Initialize(bool consent) { }
        public void ShowRewarded(string placement, Action<bool> onDone) => onDone(false);
        public void ShowInterstitial(string placement, Action onDone) => onDone();
    }
}
