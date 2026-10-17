using System;
using System.Linq;
using UnityEngine;

namespace KarachiTycoon.Game
{
    // Player preferences, kept apart from the save so a new game never resets them.
    public static class Settings
    {
        public static event Action Changed;

        public static float StreetVolume { get => PlayerPrefs.GetFloat("kt_vol_street", 0.7f); set => Set("kt_vol_street", value); }
        public static float SfxVolume { get => PlayerPrefs.GetFloat("kt_vol_sfx", 0.8f); set => Set("kt_vol_sfx", value); }
        // "auto", "phone" or "desktop".
        public static string Layout { get => PlayerPrefs.GetString("kt_layout", "auto"); set { PlayerPrefs.SetString("kt_layout", value); PlayerPrefs.Save(); Changed?.Invoke(); } }
        // Turns off post-processing and shadows for slower phones.
        public static bool BatterySaver { get => PlayerPrefs.GetInt("kt_battery", 0) == 1; set { PlayerPrefs.SetInt("kt_battery", value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); } }
        public static float UiScale { get => PlayerPrefs.GetFloat("kt_ui_scale", 1f); set => Set("kt_ui_scale", value); }

        static void Set(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); Changed?.Invoke(); }

        // Phone layout on phones and tablets, or when forced by settings or the -kt-layout argument.
        public static bool PhoneLayout
        {
            get
            {
                var forced = ShotRunner.Arg("-kt-layout") ?? Layout;
                if (forced == "phone") return true;
                if (forced == "desktop") return false;
                return Application.isMobilePlatform;
            }
        }
    }

    // Bundled config (Resources/Data/<key>.json) until a real Remote Config service is connected.
    public class LocalRemoteConfig : Ads.IRemoteConfig
    {
        public string Get(string key) => Resources.Load<TextAsset>("Data/" + key)?.text;
        public string Country
        {
            get { try { return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; } catch { return ""; } }
        }
    }

    // Analytics stand-in. Swap in a real backend (Unity Analytics, GameAnalytics...) behind this interface.
    public interface IAnalytics
    {
        void Event(string name, params (string key, object value)[] data);
    }

    public class LogAnalytics : IAnalytics
    {
        public void Event(string name, params (string key, object value)[] data) =>
            Debug.Log($"[analytics] {name} " + string.Join(" ", data.Select(d => $"{d.key}={d.value}")));
    }
}
