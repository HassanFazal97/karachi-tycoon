using System.Collections.Generic;
using System.Linq;
using KarachiTycoon.Sim;
using Newtonsoft.Json;
using UnityEngine;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // Creates the game in any scene: open the project, press Play.
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindAnyObjectByType<GameController>() != null) return;
            new GameObject("Karachi Tycoon").AddComponent<GameController>();
        }
    }

    // Runs the Burns Road slice: title, simulation loop, camera, save/load, ads and purchases.
    // The HUD is immediate-mode GUI for now (placeholder until the ledger/galla UI from the
    // art style guide §13 is built with UI Toolkit).
    public class GameController : MonoBehaviour
    {
        const string SaveKey = "kt_save_v1", WalletKey = "kt_wallet_v1", SessionsKey = "kt_sessions";
        static readonly (string en, string ur)[] Names = { ("Dada Jaan Chai Kabab", "دادا جان چائے کباب"), ("Dilli Wala Thela", "دلی والا ٹھیلا"), ("Asli Burns Road Wala", "اصلی برنس روڈ والا") };
        static readonly float[] PrayerMinutes = { 300, 780, 1005, 1100, 1185 };
        static readonly string[] PrayerNames = { "Fajr", "Zuhr", "Asr", "Maghrib", "Isha" };

        GameData data;
        Sim.Sim sim;
        StreetBuilder street;
        PeopleView peopleView;
        Camera cam;
        IAdsService ads;
        SimulatedAds simAds;
        IStoreService store;
        readonly AdPolicy policy = new AdPolicy();
        int sessions;

        float speed = 1, prevSpeed = 1, viewX, viewWidth = 30;
        string panel;
        bool title = true, ledgerOpen, act1Card, act2Card;
        int nameChoice; string hero = "pota";
        readonly List<(string text, float until, string kind)> toasts = new List<(string, float, string)>();
        Vector2 panelScroll;
        GUIStyle box, label, small, big, button, heading;

        void Awake()
        {
            data = GameData.Load(name => Resources.Load<TextAsset>("Data/" + name).text);
            cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true; cam.nearClipPlane = 1; cam.farClipPlane = 420;
            sessions = PlayerPrefs.GetInt(SessionsKey, 0) + 1;
            PlayerPrefs.SetInt(SessionsKey, sessions);
            simAds = gameObject.AddComponent<SimulatedAds>();
            ads = simAds;
            foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            UpdateCamera();
        }

        // ---------- start ----------
        void StartGame(bool load)
        {
            sim = new Sim.Sim(data);
            if (load) sim.Load(PlayerPrefs.GetString(SaveKey));
            else sim.NewGame(Names[nameChoice].en, Names[nameChoice].ur, hero);
            ApplyWallet();
            store = new SimulatedStore(sim.S);
            street = new GameObject("Burns Road").AddComponent<StreetBuilder>();
            street.Build(data, sim.S);
            street.RefreshStage(sim);
            gameObject.AddComponent<DayCycle>().Init(sim, street, cam);
            peopleView = new GameObject("People").AddComponent<PeopleView>();
            peopleView.Init(sim, cam);
            sim.Noticed += OnNotice;
            sim.Unlocked += id => { Toast($"New dish on the menu: {sim.Dish[id].En}. {sim.Dish[id].Note}", "good"); street.RefreshStage(sim); };
            sim.StaffChanged += () => street.RefreshStage(sim);
            sim.StageChanged += () => street.RefreshStage(sim);
            sim.Shouted += () => Toast("\"Aao bhai, garma garam!\" People nearby turn to look.", "good");
            sim.DayEnded += _ => ledgerOpen = true;
            sim.ActDone += n => { if (n == 1) act1Card = true; else act2Card = true; };
            title = false;
            Toast(load ? $"Welcome back. Day {sim.S.Day} on Burns Road." : "Customers queue at your thela. The bar over each head is their patience; it turns gold while you cook.", "tip");
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
                "lowStock" => $"Almost out of {sim.Ing[n.Ing].En}. Buy more in Supplies.",
                "mastery" => $"Your {sim.Dish[n.Dish].En} is getting better. Mastery {n.Level} of 5.",
                _ => n.Who == "uncle" ? $"Uncle Rasheed: \"{n.Text}\"" : n.Text,
            };
            Toast(text, n.Kind);
        }

        void Toast(string text, string kind = "")
        {
            toasts.Insert(0, (text, Time.realtimeSinceStartup + 7, kind));
            if (toasts.Count > 4) toasts.RemoveAt(toasts.Count - 1);
        }

        // ---------- save ----------
        void Save()
        {
            PlayerPrefs.SetString(SaveKey, sim.Serialize());
            SaveWallet();
            PlayerPrefs.Save();
        }
        // Purchases live outside the day-start snapshot so a reload never loses them.
        void SaveWallet() { PlayerPrefs.SetString(WalletKey, JsonConvert.SerializeObject(new { sim.S.Coins, sim.S.NoAds })); PlayerPrefs.Save(); }
        void ApplyWallet()
        {
            var json = PlayerPrefs.GetString(WalletKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var w = JsonConvert.DeserializeAnonymousType(json, new { Coins = 0, NoAds = false });
            sim.S.Coins = w.Coins; sim.S.NoAds = w.NoAds;
        }

        // Design doc §19: no ads or prompts around azaan and prayer.
        bool SensitiveMoment => sim != null && PrayerMinutes.Any(p => Mathf.Abs(sim.S.Minute - p) < 8);

        // ---------- loop ----------
        void Update()
        {
            if (sim == null || title) return;
            if (!sim.S.DayOver && speed > 0 && simAds.Showing == null)
            {
                float minutesPerSecond = (data.Balance.DayEnd - data.Balance.DayStart) / data.Balance.RealSecondsPerDay;
                float g = Mathf.Min(Time.deltaTime, 0.1f) * minutesPerSecond * speed;
                while (g > 0 && !sim.S.DayOver) { float step = Mathf.Min(0.5f, g); sim.Tick(step); g -= step; }
            }
            toasts.RemoveAll(t => t.until < Time.realtimeSinceStartup);
        }

        void UpdateCamera()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            cam.orthographicSize = viewWidth / aspect / 2;
            var target = W(viewX, 3.2f + viewWidth * 0.06f, -3.5f);
            var dir = new Vector3(0.22f, 0.6f, -0.77f).normalized;
            cam.transform.position = target + dir * 90;
            cam.transform.LookAt(target);
        }

        // ---------- GUI ----------
        void Styles()
        {
            if (box != null) return;
            var wood = new Texture2D(1, 1); wood.SetPixel(0, 0, Hex("#3B2A1EEE")); wood.Apply();
            var paper = new Texture2D(1, 1); paper.SetPixel(0, 0, Hex("#F1E6CCF5")); paper.Apply();
            box = new GUIStyle(GUI.skin.box) { normal = { background = wood, textColor = Hex("#F1E6CC") }, padding = new RectOffset(12, 12, 8, 8) };
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = Hex("#F1E6CC") } };
            small = new GUIStyle(label) { fontSize = 12, normal = { textColor = Hex("#E2C27E") } };
            big = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold };
            heading = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Hex("#2A2320") } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 8, 8) };
            GUI.skin.window.normal.background = paper; GUI.skin.window.onNormal.background = paper;
            GUI.skin.window.normal.textColor = Hex("#8E2B25"); GUI.skin.window.fontSize = 18;
        }

        void OnGUI()
        {
            Styles();
            float scale = Mathf.Clamp(Screen.height / 800f, 0.8f, 3f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            if (title) { TitleGUI(w, h); return; }
            if (simAds.Showing != null)
            {
                GUI.Box(new Rect(0, 0, w, h), "", box);
                GUI.Label(new Rect(w / 2 - 200, h / 2 - 20, 400, 40), simAds.Showing + "  (simulated)", big);
                return;
            }
            HudGUI(w, h);
            if (ledgerOpen) GUI.Window(10, new Rect(w / 2 - 330, h / 2 - 260, 660, 520), LedgerWindow, "Hisaab");
            else if (act1Card) GUI.Window(11, new Rect(w / 2 - 260, h / 2 - 140, 520, 280), id => ActCard(1), "Thela se takhta");
            else if (act2Card) GUI.Window(12, new Rect(w / 2 - 260, h / 2 - 140, 520, 280), id => ActCard(2), "Burns Road knows your name");
            else if (panel != null) GUI.Window(1, new Rect(w - 480, 100, 464, h - 190), PanelWindow, PanelTitle());
            CameraInput();
        }

        void CameraInput()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDrag && GUIUtility.hotControl == 0) { viewX = Mathf.Clamp(viewX - e.delta.x / Screen.width * viewWidth * 1.1f, -20, 20); UpdateCamera(); e.Use(); }
            if (e.type == EventType.ScrollWheel) { viewWidth = Mathf.Clamp(viewWidth * (e.delta.y > 0 ? 1.1f : 0.9f), 12, 50); UpdateCamera(); e.Use(); }
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Space) SetSpeed(speed > 0 ? 0 : prevSpeed);
                if (e.keyCode == KeyCode.Alpha1) SetSpeed(1);
                if (e.keyCode == KeyCode.Alpha2) SetSpeed(2);
                if (e.keyCode == KeyCode.Alpha3) SetSpeed(4);
                if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.A) { viewX = Mathf.Max(-20, viewX - 2); UpdateCamera(); }
                if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.D) { viewX = Mathf.Min(20, viewX + 2); UpdateCamera(); }
            }
            if (e.type == EventType.Repaint) UpdateCamera(); // keeps the framing right after a resize or rotation
        }

        void SetSpeed(float s) { if (s > 0) prevSpeed = s; speed = s; }

        void TitleGUI(float w, float h)
        {
            GUILayout.BeginArea(new Rect(w / 2 - 280, Mathf.Max(20, h / 2 - 260), 560, 520), box);
            GUILayout.Label("KARACHI TYCOON", big);
            GUILayout.Label("Chapter 1 · Burns Road · Thela se Dukaan tak", small);
            GUILayout.Space(8);
            GUILayout.Label("In 1947 your Dada came to Karachi from Delhi with nothing but a handwritten recipe book. Today you push his old thela onto Burns Road with Rs 25,000, a kettle and his book.", label);
            GUILayout.Space(10);
            GUILayout.Label("Name your stall", small);
            nameChoice = GUILayout.SelectionGrid(nameChoice, Names.Select(n => n.en).ToArray(), 1, button);
            GUILayout.Label("You are Dada's", small);
            hero = GUILayout.Toolbar(hero == "pota" ? 0 : 1, new[] { "Grandson", "Granddaughter" }, button) == 0 ? "pota" : "poti";
            GUILayout.Space(10);
            if (GUILayout.Button("Start on Burns Road", button, GUILayout.Height(40))) StartGame(false);
            if (PlayerPrefs.HasKey(SaveKey) && GUILayout.Button("Continue", button, GUILayout.Height(34))) StartGame(true);
            GUILayout.EndArea();
        }

        void HudGUI(float w, float h)
        {
            var s = sim.S; var b = data.Balance;
            GUI.Box(new Rect(0, 0, w, 84), "", box);
            GUI.Label(new Rect(14, 8, 200, 20), "GALLA", small);
            GUI.Label(new Rect(14, 26, 220, 30), Rs(s.Cash), big);
            GUI.Label(new Rect(14, 56, 220, 20), $"Silver coins: {s.Coins}", small);
            GUI.Label(new Rect(240, 10, 260, 22), $"Shohrat (fame)   {Stars(s.Fame)}", label);
            GUI.Label(new Rect(240, 34, 260, 22), $"Asli-pan          {Stars(s.Auth)}", label);
            GUI.Label(new Rect(510, 8, 260, 20), $"{sim.Weekday} · Day {s.Day} · Burns Road", small);
            GUI.Label(new Rect(510, 26, 260, 30), $"{Clock(s.Minute)}  {SlotName(sim.Slot)}", big);
            int next = System.Array.FindIndex(PrayerMinutes, p => p > s.Minute);
            GUI.Label(new Rect(510, 56, 260, 20), next >= 0 ? $"Next azaan: {PrayerNames[next]} {Clock(PrayerMinutes[next])}" : "Isha has passed. The street is full.", small);
            float[] speeds = { 0, 1, 2, 4 }; string[] sl = { "II", "1x", "2x", "4x" };
            for (int i = 0; i < 4; i++)
            {
                var prev = GUI.color; if (speed == speeds[i]) GUI.color = Hex("#E2C27E");
                if (GUI.Button(new Rect(780 + i * 48, 24, 44, 34), sl[i], button)) SetSpeed(speeds[i]);
                GUI.color = prev;
            }
            if (w > 1200)
            {
                if (s.Stage == "cart")
                {
                    GUI.Label(new Rect(w - 300, 8, 290, 20), "ACT 1 · THE CART", small);
                    GUI.Label(new Rect(w - 300, 28, 290, 22), $"{Rs(Mathf.Min(s.TotalRevenue, b.Act1.Revenue))} / {Rs(b.Act1.Revenue)} earned", label);
                    GUI.Label(new Rect(w - 300, 50, 290, 22), $"{Mathf.Min(s.TotalServed, b.Act1.Served)} / {b.Act1.Served} customers served", label);
                }
                else
                {
                    GUI.Label(new Rect(w - 300, 8, 290, 20), s.Act2Done ? "BURNS ROAD KNOWS YOUR NAME" : "ACT 2 · THE PAVEMENT DHABA", small);
                    GUI.Label(new Rect(w - 300, 28, 290, 22), $"{Rs(Mathf.Min(s.Cash, b.Act2.Cash))} / {Rs(b.Act2.Cash)} saved", label);
                    GUI.Label(new Rect(w - 300, 50, 290, 22), $"Shohrat {s.Fame:F1} / {b.Act2.Fame} stars", label);
                }
            }
            if (sim.PowerOut) GUI.Label(new Rect(w / 2 - 120, 92, 240, 24), s.Generator ? "Load shedding · generator running" : "Load shedding · no power", small);

            // Toasts.
            float ty = 96;
            foreach (var t in toasts) { GUI.Box(new Rect(12, ty, 360, 52), "", box); GUI.Label(new Rect(22, ty + 4, 344, 48), t.text, label); ty += 58; }

            // Toolbar.
            string[] tabs = { "Menu", "Supplies", "Staff", "Shop", "Store" };
            float tw = tabs.Length * 96 + 2 * 130 + 96;
            float x = w / 2 - tw / 2, y = h - 62;
            GUI.Box(new Rect(x - 8, y - 8, tw + 16, 60), "", box);
            foreach (var tab in tabs) { if (GUI.Button(new Rect(x, y, 90, 44), tab, button)) panel = panel == tab ? null : tab; x += 96; }
            float cd = Mathf.Max(0, s.AaoReady - s.Minute);
            GUI.enabled = cd <= 0 && !s.DayOver;
            if (GUI.Button(new Rect(x, y, 124, 44), cd > 0 ? $"Aao bhai! {Mathf.CeilToInt(cd)}m" : "Aao bhai!", button)) sim.Shout();
            GUI.enabled = true; x += 130;
            // Rewarded ad: refill the shout (design doc §22).
            GUI.enabled = sim.CanRefillShout && policy.CanShowRewarded(SensitiveMoment) && ads.RewardedReady;
            if (GUI.Button(new Rect(x, y, 124, 44), "Ad: shout again", button))
                ads.ShowRewarded("refill_shout", ok => { if (ok) { policy.MarkRewarded(); sim.RewardRefillShout(); } });
            GUI.enabled = true; x += 130;
            if (GUI.Button(new Rect(x, y, 90, 44), "Save", button)) { Save(); Toast("Saved. Loading resumes from the start of today."); }
        }

        static string Stars(float v) { int full = Mathf.FloorToInt(v); return new string('*', full) + (v - full >= 0.5f ? "+" : "") + $"  {v:F1}"; }
        static string SlotName(string slot) => slot switch { "breakfast" => "Nashta", "day" => "Dopahar", "evening" => "Shaam", _ => "Raat" };

        string PanelTitle() => panel switch { "Menu" => "Menu board", "Supplies" => "Supplies", "Staff" => "Staff", "Shop" => sim.S.ShopName, _ => "Store" };

        void PanelWindow(int id)
        {
            var s = sim.S;
            if (GUI.Button(new Rect(424, 4, 32, 26), "x")) { panel = null; return; }
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            var dark = new GUIStyle(label) { normal = { textColor = Hex("#2A2320") } };
            var muted = new GUIStyle(small) { normal = { textColor = Hex("#5E5148") } };
            switch (panel)
            {
                case "Menu":
                    GUILayout.Label("Customers read the board at your thela. Price above what they expect and they walk off saying \"Mehenga hai!\".", muted);
                    foreach (var d in data.Dishes)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{d.En}", dark, GUILayout.Width(170));
                        if (sim.IsLocked(d.Id))
                            GUILayout.Label(d.Unlock.Type == "served" ? $"Unlocks after {d.Unlock.Value} customers" : "Needs the dhaba and a karigar", muted);
                        else
                        {
                            if (GUILayout.Button("-", GUILayout.Width(28))) sim.SetPrice(d.Id, s.Menu[d.Id].Price - 10);
                            GUILayout.Label(Rs(s.Menu[d.Id].Price), dark, GUILayout.Width(70));
                            if (GUILayout.Button("+", GUILayout.Width(28))) sim.SetPrice(d.Id, s.Menu[d.Id].Price + 10);
                            bool on = GUILayout.Toggle(s.Menu[d.Id].On, "Selling");
                            if (on != s.Menu[d.Id].On) sim.ToggleDish(d.Id);
                        }
                        GUILayout.EndHorizontal();
                        if (!sim.IsLocked(d.Id)) GUILayout.Label($"Plate cost {Rs(sim.PlateCost(d.Id))} · street price about {Rs(d.RefPrice)} · mastery {sim.MasteryLevel(d.Id)}/5", muted);
                    }
                    GUILayout.Space(10);
                    GUILayout.Label("Cooking fat", dark);
                    int fat = GUILayout.Toolbar(s.Fat == "ghee" ? 0 : 1, new[] { "Desi ghee (taste, Asli-pan)", "Cooking oil (margin)" });
                    sim.SetFat(fat == 0 ? "ghee" : "oil");
                    break;
                case "Supplies":
                    GUILayout.Label("Stock is bought at dawn up to your top-up level. Milk, dahi and meat spoil overnight.", muted);
                    var need = sim.NeededIngredients();
                    foreach (var i in data.Ingredients)
                    {
                        GUILayout.Label($"{i.En} ({(i.Supplier == "butcher" ? "qasai" : "kiryana")}) · {Rs(i.Cost)} a portion{(need.Contains(i.Id) ? "" : " · not on your menu yet")}", dark);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"In stock {s.Stock[i.Id]}", muted, GUILayout.Width(100));
                        if (GUILayout.Button("+10", GUILayout.Width(44))) sim.Buy(i.Id, 10);
                        if (GUILayout.Button("+50", GUILayout.Width(44))) sim.Buy(i.Id, 50);
                        GUILayout.Label("Top up to", muted, GUILayout.Width(66));
                        if (GUILayout.Button("-", GUILayout.Width(26))) sim.SetAutoBuy(i.Id, s.AutoBuy[i.Id] - 10);
                        GUILayout.Label(s.AutoBuy[i.Id].ToString(), dark, GUILayout.Width(36));
                        if (GUILayout.Button("+", GUILayout.Width(26))) sim.SetAutoBuy(i.Id, s.AutoBuy[i.Id] + 10);
                        GUILayout.EndHorizontal();
                    }
                    break;
                case "Staff":
                    GUILayout.Label(s.Stage == "cart" ? "The thela has room for one helper." : $"The dhaba has room for {sim.Stage.StaffCap}.", muted);
                    foreach (var st in data.Staff)
                    {
                        GUILayout.Label($"{st.Name} · {st.En} · {Rs(st.Wage)} a day", dark);
                        GUILayout.Label(st.Does, muted);
                        if (s.Staff[st.Id]) { if (GUILayout.Button("Let go")) sim.Fire(st.Id); }
                        else if (GUILayout.Button("Hire")) sim.Hire(st.Id);
                        GUILayout.Space(8);
                    }
                    break;
                case "Shop":
                    var dh = data.Balance.Stages["dhaba"];
                    if (s.Stage == "dhaba") GUILayout.Label("Pavement dhaba: open.", dark);
                    else if (s.Act1Done) { if (GUILayout.Button($"Set up the pavement dhaba for {Rs(dh.SetupCost)}")) sim.RentDhaba(); }
                    else GUILayout.Label($"Pavement dhaba: finish Act 1 first ({Rs(data.Balance.Act1.Revenue)} earned and {data.Balance.Act1.Served} served).", muted);
                    GUILayout.Space(6);
                    if (s.Generator) GUILayout.Label("Generator: installed.", dark);
                    else if (s.Stage == "dhaba") { if (GUILayout.Button($"Buy a generator for {Rs(data.Balance.Generator.Cost)}")) sim.BuyGenerator(); }
                    else GUILayout.Label("Generator: needs the dhaba.", muted);
                    GUILayout.Space(6);
                    GUILayout.Label($"{s.TotalServed:N0} customers served · {Rs(s.TotalRevenue)} taken in total · day {s.Day}", muted);
                    break;
                default:
                    GUILayout.Label("Purchases are simulated until Unity IAP is connected. Nothing is charged.", muted);
                    GUILayout.Label($"Silver coins: {s.Coins}{(s.NoAds ? " · No Ads owned" : "")}", dark);
                    foreach (var p in store.Products)
                    {
                        if (p.Id == "no_ads" && s.NoAds) continue;
                        GUILayout.Label($"{p.Title} · {p.PriceUsd}", dark);
                        GUILayout.Label(p.Description, muted);
                        if (GUILayout.Button("Buy")) store.Purchase(p.Id, ok => { if (ok) { SaveWallet(); Toast($"Bought {p.Title}.", "good"); } });
                        GUILayout.Space(6);
                    }
                    break;
            }
            GUILayout.EndScrollView();
        }

        void LedgerWindow(int id)
        {
            var l = sim.Today;
            var ink = new GUIStyle(label) { normal = { textColor = Hex("#2B3F8F") } };
            var dark = new GUIStyle(label) { normal = { textColor = Hex("#2A2320") } };
            GUILayout.Label($"{l.Weekday} · Day {l.Day}", ink);
            GUILayout.Label("Aamdani · sales", dark);
            foreach (var kv in l.Sales) GUILayout.Label($"   {sim.Dish[kv.Key].En} × {kv.Value.Qty}   {Rs(kv.Value.Rs)}", ink);
            GUILayout.Label($"   Tips from happy customers   {Rs(l.Tips)}{(l.TipsDoubled ? " (doubled)" : "")}", ink);
            GUILayout.Label("Kharcha · expenses", dark);
            GUILayout.Label($"   Stock {Rs(l.StockBought)} · Ghee/oil {Rs(l.FatCost)} · Wages {Rs(l.Wages)} · Rent {Rs(l.Rent)}{(l.Setup > 0 ? $" · Setup {Rs(l.Setup)}" : "")}", ink);
            int net = l.Net + (l.TipsDoubled ? l.Tips : 0);
            GUILayout.Label($"Net for the day: {(net < 0 ? "-" : "")}{Rs(Mathf.Abs(net))}   {(net >= 0 ? "MUNAFA" : "NUQSAN")}", new GUIStyle(big) { normal = { textColor = Hex(net >= 0 ? "#2B3F8F" : "#C8202F") } });
            GUILayout.Label($"{l.Served} served · {l.Walkouts} walked off waiting · {l.Refused} said too expensive · {l.QueueFull} saw a long line", dark);
            if (l.Missed.Count > 0) GUILayout.Label("Sold out: " + string.Join(", ", l.Missed.Select(kv => $"{kv.Value} {sim.Dish[kv.Key].En}")), dark);
            if (l.WastageRs > 0) GUILayout.Label($"Spoiled overnight: {Rs(l.WastageRs)}", dark);
            GUILayout.Label($"Shohrat {l.FameEnd - l.FameStart:+0.00;-0.00} · Asli-pan {l.AuthEnd - l.AuthStart:+0.00;-0.00}", dark);
            foreach (var n in l.Notes.Skip(System.Math.Max(0, l.Notes.Count - 2))) GUILayout.Label($"Uncle Rasheed: \"{n.Text}\"", ink);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            // Rewarded ad (design doc §22): always optional.
            GUI.enabled = sim.CanDoubleTips && policy.CanShowRewarded(false) && ads.RewardedReady;
            if (GUILayout.Button($"Watch an ad: double tonight's tips (+{Rs(l.Tips)})", button, GUILayout.Height(40)))
                ads.ShowRewarded("double_tips", ok => { if (ok) { policy.MarkRewarded(); sim.RewardDoubleTips(); } });
            GUI.enabled = true;
            if (GUILayout.Button("Agla din · Next day", button, GUILayout.Height(40), GUILayout.Width(190)))
            {
                ledgerOpen = false;
                void Next() { sim.NextDay(); Save(); }
                // Interstitials only at this natural break, never early in a player's life or near azaan.
                if (policy.CanShowInterstitial(sim.S, sessions, SensitiveMoment)) { policy.MarkInterstitial(); ads.ShowInterstitial("day_end", Next); }
                else Next();
            }
            GUILayout.EndHorizontal();
        }

        void ActCard(int n)
        {
            var dark = new GUIStyle(label) { normal = { textColor = Hex("#2A2320") } };
            if (n == 1)
            {
                GUILayout.Label("People on Burns Road know your chai now. The owner upstairs says you can put a takhta and plastic chairs on the pavement in front of the old \"To Let\" shop.", dark);
                if (GUILayout.Button("Open Shop", button)) { act1Card = false; panel = "Shop"; }
            }
            else
            {
                GUILayout.Label($"Two-star Shohrat and Rs 3 lakh saved, on day {sim.S.Day}. Uncle Rasheed has a table now, and the nihari deg is empty by nine. This is the end of the Burns Road slice.", dark);
                if (GUILayout.Button("Keep playing", button)) act2Card = false;
            }
        }
    }
}
