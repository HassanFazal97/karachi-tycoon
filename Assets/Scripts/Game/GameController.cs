using System;
using System.Linq;
using KarachiTycoon.Sim;
using Newtonsoft.Json;
using UnityEngine;

namespace KarachiTycoon.Game
{
    // Creates the game in any scene: open the project, press Play.
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (UnityEngine.Object.FindAnyObjectByType<GameController>() != null) return;
            new GameObject("Karachi Tycoon").AddComponent<GameController>();
        }
    }

    // Runs the Burns Road slice: simulation loop, save/load, ads and purchases. Everything on screen
    // is drawn by GameUi; the street camera is CameraRig.
    public class GameController : MonoBehaviour
    {
        const string SaveKey = "kt_save_v1", WalletKey = "kt_wallet_v1", SessionsKey = "kt_sessions";
        public static readonly (string en, string ur)[] Names = { ("Dada Jaan Chai Kabab", "دادا جان چائے کباب"), ("Dilli Wala Thela", "دلی والا ٹھیلا"), ("Asli Burns Road Wala", "اصلی برنس روڈ والا") };
        public static readonly float[] PrayerMinutes = { 300, 780, 1005, 1100, 1185 };
        public static readonly string[] PrayerNames = { "Fajr", "Zuhr", "Asr", "Maghrib", "Isha" };

        public GameData Data { get; private set; }
        public Sim.Sim Sim { get; private set; }
        public StreetBuilder Street { get; private set; }
        public CameraRig Rig { get; private set; }
        public AudioDirector Audio { get; private set; }
        public IAdsService Ads { get; private set; }
        public SimulatedAds SimAds { get; private set; }
        public IStoreService Store { get; private set; }
        public IAnalytics Analytics { get; } = new LogAnalytics();
        public AdPolicy Policy { get; } = new AdPolicy();
        public int Sessions { get; private set; }
        public float Speed { get; private set; } = 1;
        public bool Playing => Sim != null;
        public bool HasSave => PlayerPrefs.HasKey(SaveKey);
        public bool Paused => Speed == 0;

        // Raised for every message the player should see: (text, kind). Kinds: good, warn, tip, uncle.
        public event Action<string, string> Toasted;
        public event Action Started;

        float prevSpeed = 1;
        GameUi ui;

        void Awake()
        {
            Data = GameData.Load(name => Resources.Load<TextAsset>("Data/" + name).text);
            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true; cam.nearClipPlane = 1; cam.farClipPlane = 420;
            Sessions = PlayerPrefs.GetInt(SessionsKey, 0) + 1;
            PlayerPrefs.SetInt(SessionsKey, Sessions);
            SimAds = gameObject.AddComponent<SimulatedAds>();
            Ads = SimAds;
            foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            Rig = cam.gameObject.AddComponent<CameraRig>();
            Rig.Init(cam, Settings.PhoneLayout);
            Audio = gameObject.AddComponent<AudioDirector>();
            Audio.Init(PrayerMinutes);
            ui = gameObject.AddComponent<GameUi>();
            ui.Init(this);
            Analytics.Event("session_start", ("session", Sessions), ("platform", Application.platform), ("layout", Settings.PhoneLayout ? "phone" : "desktop"));
        }

        // ---------- start ----------
        public void StartGame(bool load, int nameChoice = 0, string hero = "pota")
        {
            Sim = new Sim.Sim(Data);
            if (load) Sim.Load(PlayerPrefs.GetString(SaveKey));
            else Sim.NewGame(Names[nameChoice].en, Names[nameChoice].ur, hero);
            ApplyWallet();
            Store = new SimulatedStore(Sim.S);
            Street = new GameObject("Burns Road").AddComponent<StreetBuilder>();
            Street.Build(Data, Sim.S);
            Street.RefreshStage(Sim);
            gameObject.AddComponent<DayCycle>().Init(Sim, Street, Rig.Cam);
            new GameObject("People").AddComponent<PeopleView>().Init(Sim, Rig.Cam);
            Audio.Attach(Sim);
            Sim.Noticed += OnNotice;
            Sim.Unlocked += id => { Toast($"New dish on the menu: {Sim.Dish[id].En}. {Sim.Dish[id].Note}", "good"); Street.RefreshStage(Sim); };
            Sim.StaffChanged += () => Street.RefreshStage(Sim);
            Sim.StageChanged += () => Street.RefreshStage(Sim);
            Sim.Shouted += () => Toast("\"Aao bhai, garma garam!\" People nearby turn to look.", "good");
            Sim.DayEnded += l => Analytics.Event("day_end", ("day", l.Day), ("net", l.Net), ("served", l.Served), ("walkouts", l.Walkouts), ("refused", l.Refused));
            Sim.ActDone += n => Analytics.Event("act_done", ("act", n), ("day", Sim.S.Day));
            Analytics.Event("game_start", ("mode", load ? "continue" : "new"), ("day", Sim.S.Day));
            Started?.Invoke();
            Toast(load ? $"Welcome back. Day {Sim.S.Day} on Burns Road." : "Customers queue at your thela. The bar over each head is their patience; it turns gold while you cook.", "tip");
        }

        void OnNotice(Notice n)
        {
            string text = n.Key switch
            {
                "noCash" => "Not enough cash in the galla.",
                "staffCap" => "No room for more staff here. The dhaba has space for more.",
                "dhabaOpen" => "The takhta is set up. Hire a karigar to start nihari.",
                "outage" => "Load shedding! The block goes dark. Service slows down.",
                "outageGen" => "Load shedding. Your generator kicks in.",
                "lowStock" => $"Almost out of {Sim.Ing[n.Ing].En}. Buy more in Supplies.",
                "mastery" => $"Your {Sim.Dish[n.Dish].En} is getting better. Mastery {n.Level} of 5.",
                _ => n.Who == "uncle" ? $"Uncle Rasheed: \"{n.Text}\"" : n.Text,
            };
            Toast(text, n.Who == "uncle" ? "uncle" : n.Kind);
        }

        public void Toast(string text, string kind = "") => Toasted?.Invoke(text, kind);

        // ---------- save ----------
        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, Sim.Serialize());
            SaveWallet();
            PlayerPrefs.Save();
        }
        // Purchases live outside the day-start snapshot so a reload never loses them.
        public void SaveWallet() { PlayerPrefs.SetString(WalletKey, JsonConvert.SerializeObject(new { Sim.S.Coins, Sim.S.NoAds })); PlayerPrefs.Save(); }
        void ApplyWallet()
        {
            var json = PlayerPrefs.GetString(WalletKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var w = JsonConvert.DeserializeAnonymousType(json, new { Coins = 0, NoAds = false });
            Sim.S.Coins = w.Coins; Sim.S.NoAds = w.NoAds;
        }

        // Design doc §19: no ads or prompts around azaan and prayer.
        public bool SensitiveMoment => Sim != null && PrayerMinutes.Any(p => Mathf.Abs(Sim.S.Minute - p) < 8);

        // ---------- actions the UI calls ----------
        public void SetSpeed(float s) { if (s > 0) prevSpeed = s; Speed = s; }
        public void TogglePause() => SetSpeed(Speed > 0 ? 0 : prevSpeed);

        public bool CanRefillShout => Sim.CanRefillShout && Policy.CanShowRewarded(SensitiveMoment) && Ads.RewardedReady;
        public void RefillShout() => Ads.ShowRewarded("refill_shout", ok => { if (ok) { Policy.MarkRewarded(); Sim.RewardRefillShout(); Analytics.Event("rewarded", ("placement", "refill_shout")); } });

        public bool CanDoubleTips => Sim.CanDoubleTips && Policy.CanShowRewarded(false) && Ads.RewardedReady;
        public void DoubleTips() => Ads.ShowRewarded("double_tips", ok => { if (ok) { Policy.MarkRewarded(); Sim.RewardDoubleTips(); Analytics.Event("rewarded", ("placement", "double_tips")); } });

        public void NextDay()
        {
            void Next() { Sim.NextDay(); Save(); }
            // Interstitials only at this natural break, never early in a player's life or near azaan.
            if (Policy.CanShowInterstitial(Sim.S, Sessions, SensitiveMoment)) { Policy.MarkInterstitial(); Analytics.Event("interstitial", ("placement", "day_end")); Ads.ShowInterstitial("day_end", Next); }
            else Next();
        }

        public void Buy(Product p) => Store.Purchase(p.Id, ok =>
        {
            if (!ok) return;
            SaveWallet();
            Toast($"Bought {p.Title}.", "good");
            Analytics.Event("purchase", ("product", p.Id), ("price", p.PriceUsd));
        });

        // ---------- loop ----------
        void Update()
        {
            if (Sim == null || Sim.S.DayOver || Speed <= 0 || SimAds.Showing != null || ui.Blocking) return;
            float minutesPerSecond = (Data.Balance.DayEnd - Data.Balance.DayStart) / Data.Balance.RealSecondsPerDay;
            float g = Mathf.Min(Time.deltaTime, 0.1f) * minutesPerSecond * Speed;
            while (g > 0 && !Sim.S.DayOver) { float step = Mathf.Min(0.5f, g); Sim.Tick(step); g -= step; }
        }
    }
}
