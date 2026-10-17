using System;
using System.Linq;
using KarachiTycoon.Game.Ads;
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
        public AdsService Ads { get; private set; }
        public IStoreService Store { get; private set; }
        public Wallet Wallet { get; private set; } = new Wallet();
        public IAnalytics Analytics { get; } = new LogAnalytics();
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
            LoadWallet();
            Store = new SimulatedStore(Wallet);
            Ads = new AdsService(AdsService.CreateProvider(gameObject), new LocalRemoteConfig(), Analytics, AdContextNow, () => Wallet);
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
        public void StartGame(bool load, int nameChoice = 0, string hero = "pota", bool tutorial = false)
        {
            Sim = new Sim.Sim(Data);
            if (load) Sim.Load(PlayerPrefs.GetString(SaveKey));
            else Sim.NewGame(Names[nameChoice].en, Names[nameChoice].ur, hero, tutorial);
            ApplyWallet();
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
            Sim.UpgradeBought += id => { Street.RefreshStage(Sim); Analytics.Event("upgrade_buy", ("upgrade", id), ("day", Sim.S.Day)); };
            Sim.FriendLevelUp += (id, level) =>
            {
                var r = Data.Story.Regulars.Find(x => x.Id == id);
                int coins = 10 * level;
                Wallet.Coins += coins; ApplyWallet(); SaveWallet();
                Toast($"{r.Name}: friendship level {level}. {r.Levels[Mathf.Clamp(level - 1, 0, r.Levels.Count - 1)]} +{coins} silver coins.", "good");
                Analytics.Event("friend_level", ("regular", id), ("level", level), ("day", Sim.S.Day));
            };
            Sim.PageFound += p => Analytics.Event("page_found", ("page", p.Id), ("day", Sim.S.Day), ("pages", Sim.S.Pages.Count));
            Sim.CardDrawn += c => Analytics.Event("card_shown", ("card", c.Id), ("day", Sim.S.Day));
            Sim.QuestStarted += q => Analytics.Event("quest_start", ("quest", q.Id), ("day", Sim.S.Day));
            Sim.QuestDone += q =>
            {
                if (q.Coins > 0) { Wallet.Coins += q.Coins; ApplyWallet(); SaveWallet(); }
                Analytics.Event("quest_done", ("quest", q.Id), ("day", Sim.S.Day));
            };
            Sim.GoalDone += g =>
            {
                // Goal coins are silver coins: they live in the wallet with the bought ones.
                if (g.Coins > 0) { Wallet.Coins += g.Coins; ApplyWallet(); SaveWallet(); }
                Analytics.Event("goal_done", ("goal", g.Id), ("day", Sim.S.Day), ("minute", (int)Sim.S.Minute));
            };
            Sim.DayEnded += l => Analytics.Event("day_end", ("day", l.Day), ("net", l.Net), ("served", l.Served), ("walkouts", l.Walkouts), ("refused", l.Refused));
            Sim.ActDone += n => Analytics.Event("act_done", ("act", n), ("day", Sim.S.Day));
            Analytics.Event("game_start", ("mode", load ? "continue" : "new"), ("day", Sim.S.Day), ("tutorial", Sim.S.Tutorial ?? "legacy"));
            Started?.Invoke();
            if (load) Toast($"Welcome back. Day {Sim.S.Day} on Burns Road.", "tip");
            else if (!tutorial) Toast("Customers queue at your thela. The bar over each head is their patience; it turns gold while you cook.", "tip");
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
                "regular" => $"{RegularName(n.Who)}: \"{n.Text}\"",
                "regularPrice" => $"{RegularName(n.Who)}: \"Prices have gone up, beta? I will remember that.\"",
                _ => n.Who == "uncle" ? $"Uncle Rasheed: \"{n.Text}\"" : n.Text,
            };
            Toast(text, n.Who == "uncle" ? "uncle" : n.Kind);
        }

        public void Toast(string text, string kind = "") => Toasted?.Invoke(text, kind);
        string RegularName(string id) => Data.Story.Regulars.Find(r => r.Id == id)?.Name ?? id;

        // ---------- save ----------
        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, Sim.Serialize());
            SaveWallet();
            PlayerPrefs.Save();
        }
        // Purchases live outside the day-start snapshot so a reload never loses them.
        public void SaveWallet() { PlayerPrefs.SetString(WalletKey, JsonConvert.SerializeObject(Wallet)); PlayerPrefs.Save(); }
        void LoadWallet()
        {
            var json = PlayerPrefs.GetString(WalletKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try { Wallet = JsonConvert.DeserializeObject<Wallet>(json) ?? new Wallet(); } catch { Wallet = new Wallet(); }
            // Saves from before the Owned list still own No Ads.
            if (Wallet.NoAds) Wallet.Owned.Add("no_ads");
        }
        // The Sim only needs to know about the purchases that change play.
        void ApplyWallet()
        {
            if (Sim == null) return;
            Sim.S.Coins = Wallet.Coins; Sim.S.NoAds = Wallet.NoAds; Sim.S.DoubleIncome = Wallet.DoubleIncome;
        }

        // Design doc §19: no ads or prompts around the in-game azaan. (Real-world prayer times are AdRules' job.)
        public bool SensitiveMoment => Sim != null && PrayerMinutes.Any(p => Mathf.Abs(Sim.S.Minute - p) < 8);

        AdContext AdContextNow() => new AdContext
        {
            GameDay = Sim?.S.Day ?? 0, Session = Sessions, InGameSensitive = SensitiveMoment,
            Tutorial = Sim?.TutorialDay ?? false, NoAds = Wallet.NoAds, AdsSupported = Ads?.Provider.Supported ?? false,
        };

        // ---------- actions the UI calls ----------
        public void SetSpeed(float s) { if (s > 0) prevSpeed = s; Speed = s; }
        public void TogglePause() => SetSpeed(Speed > 0 ? 0 : prevSpeed);

        public bool CanRefillShout => Sim.CanRefillShout && Ads.CanOffer("refill_shout");
        public void RefillShout() => Ads.Rewarded("refill_shout", Sim.RewardRefillShout);

        public bool CanDoubleTips => Sim.CanDoubleTips && Ads.CanOffer("double_tips");
        public void DoubleTips() => Ads.Rewarded("double_tips", Sim.RewardDoubleTips);

        // Interstitials only at this natural break; AdsService decides whether one shows.
        public bool CanSwapGoal(int i) => i < Sim.S.Goals.Count && !Sim.S.Goals[i].Done && Ads.CanOffer("swap_goal");
        public void SwapGoal(int i) => Ads.Rewarded("swap_goal", () => { if (Sim.SwapGoal(i)) Analytics.Event("goal_swap", ("index", i)); });

        public void NextDay() => Ads.AtBreak("day_end", () => { Sim.NextDay(); Save(); });

        public void Buy(Product p) => Store.Purchase(p.Id, ok =>
        {
            if (!ok) return;
            ApplyWallet();
            SaveWallet();
            Ads.OnPurchase();
            Toast($"Bought {p.Title}.", "good");
            Analytics.Event("purchase", ("product", p.Id), ("price", p.PriceUsd));
        });

        // ---------- loop ----------
        void Update()
        {
            if (Sim == null) return;
            Ads.Tick(Time.unscaledDeltaTime);
            if (Sim.S.DayOver || Speed <= 0 || Ads.Showing != null || ui.Blocking) return;
            float minutesPerSecond = (Data.Balance.DayEnd - Data.Balance.DayStart) / Data.Balance.RealSecondsPerDay;
            float g = Mathf.Min(Time.deltaTime, 0.1f) * minutesPerSecond * Speed;
            while (g > 0 && !Sim.S.DayOver) { float step = Mathf.Min(0.5f, g); Sim.Tick(step); g -= step; }
        }
    }
}
