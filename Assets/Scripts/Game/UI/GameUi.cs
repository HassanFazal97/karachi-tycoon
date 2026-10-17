using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.UiKit;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // All on-screen UI, built with UI Toolkit in code and styled by Resources/UI/Main.uss.
    // Two layouts share the same views: "phone" (portrait, one hand: thin two-row top bar, a bottom
    // dock with the shout in the middle, half-height bottom sheets) and "desktop" (landscape for
    // Steam: denser HUD, labelled toolbar with key hints, side panel).
    public partial class GameUi : MonoBehaviour
    {
        GameController gc;
        UIDocument doc;
        PanelSettings panelSettings;
        VisualElement root, safe, world, hud, sheetLayer, toastLayer, modalLayer;
        readonly Binder hudBind = new Binder(), sheetBind = new Binder(), modalBind = new Binder();
        bool phone;
        Rect lastSafeArea;
        Sim.Sim sim => gc.Sim;

        // What is open. Only one modal at a time; the sheet can sit under a modal.
        string modal;
        public bool Blocking => modal == "settings" || modal == "act1" || modal == "act2" || modal == "kitaab" || modal == "choice" || modal == "page" || (tutor != null && tutor.Pausing);

        public void Init(GameController g)
        {
            gc = g;
            phone = Settings.PhoneLayout;
            // A copy of the panel asset (made by BuildTools), so runtime tweaks never touch the asset itself.
            var asset = Resources.Load<PanelSettings>("UI/KtPanel");
            panelSettings = asset != null ? Instantiate(asset) : ScriptableObject.CreateInstance<PanelSettings>();
            if (panelSettings.themeStyleSheet == null) panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/KtTheme");
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ApplyPanelScale();

            var go = new GameObject("UI");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            go.SetActive(true);

            root = doc.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Main"));
            root.AddToClassList("kt-root");
            root.pickingMode = PickingMode.Ignore;

            world = Div("world");
            gc.Rig.Attach(world);
            gc.Rig.Tapped += OnWorldTap;
            safe = Div("safe", "layer").Ghost();
            hud = Div("layer").Ghost();
            sheetLayer = Div("layer").Ghost();
            toastLayer = Div("layer", "toast-layer").Ghost();
            modalLayer = Div("layer").Ghost();
            coachLayer = Div("layer").Ghost();
            safe.With(hud, sheetLayer, toastLayer, modalLayer, coachLayer);
            tutor = gameObject.AddComponent<TutorialDirector>();
            root.With(world, safe);

            gc.Toasted += Toast;
            gc.Started += OnStarted;
            Settings.Changed += OnSettingsChanged;
            tutor.Init(gc, this);
            Rebuild();
        }

        void OnDestroy() => Settings.Changed -= OnSettingsChanged;

        void ApplyPanelScale()
        {
            // Phone: about one unit per iPhone point in portrait (matched on width). Desktop: laid out for 1600x900.
            panelSettings.referenceResolution = phone ? new Vector2Int(390, 844) : new Vector2Int(1600, 900);
            panelSettings.match = phone ? 0f : 0.5f;
            panelSettings.scale = Settings.UiScale;
        }

        void OnSettingsChanged()
        {
            bool p = Settings.PhoneLayout;
            ApplyPanelScale();
            if (p == phone) return;
            phone = p;
            Rebuild();
            OpenSettings();
        }

        void OnStarted()
        {
            sim.DayEnded += _ => OpenLedger();
            sim.ActDone += n => OpenActCard(n);
            sim.Unlocked += _ => RefreshSheet();
            sim.StaffChanged += RefreshSheet;
            sim.StageChanged += RefreshSheet;
            sim.UpgradeBought += _ => RefreshSheet();
            sim.GoalDone += OnGoalDone;
            gc.Street.Built += t => Stamp("MUBARAK!", $"Your {sim.Stage.Name} is open");
            HookStory();
            sim.Bubble += OnServeBubble;
            Rebuild();
        }

        // Rebuilds the whole tree for the current layout and game state.
        void Rebuild()
        {
            root.EnableInClassList("phone", phone);
            root.EnableInClassList("desktop", !phone);
            hud.Clear(); sheetLayer.Clear(); toastLayer.Clear(); modalLayer.Clear();
            hudBind.Clear(); sheetBind.Clear(); modalBind.Clear(); goalsBind.Clear();
            modal = null; sheetOpen = null;
            HideCoach();
            toasts.Clear();
            world.Show(gc.Playing);
            if (!gc.Playing) { BuildTitle(); return; }
            BuildHud();
            BuildSheet();
        }

        void Update()
        {
            if (root == null) return;
            if (panelSettings.targetTexture == null && Screen.safeArea != lastSafeArea && root.layout.width > 0) ApplySafeArea();
            hudBind.Update(); sheetBind.Update(); modalBind.Update();
            if (goalsDirty && goalsRows != null) FillGoals();
            goalsBind.Update();
            UpdateToasts();
            UpdateAdOverlay();
            UpdateCoach();
            UpdatePins();
            UpdateStory();
            Keyboard();
        }

        // Keep buttons out from under notches and the home indicator.
        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            float k = root.layout.width / Mathf.Max(1, Screen.width);
            var a = lastSafeArea;
            safe.style.left = a.x * k;
            safe.style.right = (Screen.width - a.xMax) * k;
            safe.style.top = (Screen.height - a.yMax) * k;
            safe.style.bottom = a.y * k;
        }

        void OnWorldTap(Vector2 screenPos)
        {
            // Tapping the street closes an open sheet on phones; tap-to-serve hooks in here next.
            if (phone && sheetOpen != null) CloseSheet();
        }

        // Used by ShotRunner to put the UI into a given state.
        public PanelSettings Panel => panelSettings;

        public void DebugShow(string what)
        {
            CloseModal(); CloseSheet();
            if (what == "settings") OpenSettings();
            else if (what == "act1") OpenActCard(1);
            else if (what == "kitaab") OpenKitaab();
            else if (what == "district") OpenDistrict();
            else if (what == "city") OpenCity();
            if (what == "none") CloseDistrict();
            else if (Tabs.Any(t => t.id == what)) OpenSheet(what);
        }

        // ---------- keyboard (desktop) ----------
        void Keyboard()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !gc.Playing) return;
            bool Pressed(Key k) => kb[k].wasPressedThisFrame;
            if (Pressed(Key.Escape)) { if (modal == "settings" || modal == "city") CloseModal(); else if (sheetOpen != null) CloseSheet(); else if (DistrictOpen) CloseDistrict(); else if (modal == null) OpenSettings(); }
            if (modal == null && Pressed(Key.N)) ToggleDistrict();
            if (modal != null) return;
            if (Pressed(Key.Space)) gc.TogglePause();
            if (Pressed(Key.Digit1)) gc.SetSpeed(1);
            if (Pressed(Key.Digit2)) gc.SetSpeed(2);
            if (Pressed(Key.Digit3)) gc.SetSpeed(4);
            foreach (var t in Tabs) if (t.key != Key.None && Pressed(t.key)) ToggleSheet(t.id);
            if (Pressed(Key.Q)) sim.Shout();
            float pan = 0;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) pan -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) pan += 1;
            if (pan != 0) gc.Rig.Pan(pan * gc.Rig.ViewWidth * 0.8f * Time.unscaledDeltaTime);
            if (Pressed(Key.Equals) || Pressed(Key.NumpadPlus)) gc.Rig.Zoom(0.9f);
            if (Pressed(Key.Minus) || Pressed(Key.NumpadMinus)) gc.Rig.Zoom(1.1f);
        }

        // ---------- title ----------
        int nameChoice;
        string hero = "pota";

        void BuildTitle()
        {
            var screen = Div("title-screen");
            screen.Add(new TruckStrip().Cls("title-strip"));
            var card = Div("title-card");
            var left = Div("title-col", "title-left");
            var right = Div("title-col");
            card.With(left, right);
            screen.Add(card);
            screen.Add(new TruckStrip().Cls("title-strip", "bottom"));
            modalLayer.Add(screen);

            left.Add(Div("row", "title-logo").With(new Icon("chai").Cls("title-icon"), Div("col").With(
                Text("KARACHI TYCOON", "title-name"),
                Text("Chapter 1 · Burns Road · Thela se Dukaan tak", "title-sub"))));
            left.Add(Text("In 1947 your Dada came to Karachi from Delhi with nothing but a handwritten recipe book. Today you push his old thela onto Burns Road with Rs 25,000, a kettle and his book.", "title-story"));
            var sign = Div("title-sign");
            left.Add(sign);

            right.Add(Text("Name your stall", "label-gold"));
            var nameButtons = new List<Button>();
            Button boy = null, girl = null;
            for (int i = 0; i < GameController.Names.Length; i++)
            {
                int k = i;
                var b = Btn(GameController.Names[i].en, () => { nameChoice = k; Refresh(); }, "choice");
                nameButtons.Add(b); right.Add(b);
            }
            right.Add(Text("You are Dada's", "label-gold"));
            var heroRow = Div("seg");
            boy = Btn("Grandson", () => { hero = "pota"; Refresh(); }, "seg-btn");
            girl = Btn("Granddaughter", () => { hero = "poti"; Refresh(); }, "seg-btn");
            heroRow.With(boy, girl);
            right.Add(heroRow);
            right.Add(Div("grow"));
            // New players get Uncle Rasheed's guided first day; anyone who has played before may skip it.
            bool guided = !gc.HasSave;
            right.Add(Text("Guided first day", "label-gold"));
            var guide = Div("seg");
            var on = Btn("With Uncle Rasheed", null, "seg-btn"); var off = Btn("Skip", null, "seg-btn");
            on.clicked += () => { guided = true; on.AddToClassList("on"); off.RemoveFromClassList("on"); };
            off.clicked += () => { guided = false; off.AddToClassList("on"); on.RemoveFromClassList("on"); };
            (guided ? on : off).AddToClassList("on");
            right.Add(guide.With(on, off));
            right.Add(Btn("Start on Burns Road", () =>
            {
                if (guided) OpenColdOpen(() => gc.StartGame(false, nameChoice, hero, true));
                else gc.StartGame(false, nameChoice, hero, false);
            }, "btn-primary", "btn-big"));
            if (gc.HasSave) right.Add(Btn("Continue", () => gc.StartGame(true), "btn-big"));

            void Refresh()
            {
                for (int i = 0; i < nameButtons.Count; i++) nameButtons[i].EnableInClassList("on", i == nameChoice);
                boy.EnableInClassList("on", hero == "pota"); girl.EnableInClassList("on", hero == "poti");
                // The stall's Nastaliq signboard, painted ahead of time like the ones on the street.
                var file = GameController.Names[nameChoice].en.ToLowerInvariant().Replace(' ', '_');
                var tex = Resources.Load<Texture2D>("Signs/" + file);
                sign.style.backgroundImage = tex;
                sign.Show(tex != null);
                float h = phone ? 54 : 78;
                if (tex != null) { sign.style.height = h; sign.style.width = h * tex.width / tex.height; }
            }
            Refresh();
        }

        // ---------- HUD ----------
        static readonly (string id, string icon, string label, Key key)[] Tabs =
        {
            ("Menu", "menu", "Menu", Key.M), ("Supplies", "supplies", "Supplies", Key.S),
            ("Staff", "staff", "Staff", Key.H), ("Shop", "shop", "Upgrades", Key.U), ("Store", "store", "Store", Key.None),
        };

        void BuildHud()
        {
            var s = sim.S; var b = gc.Data.Balance;
            var top = Div("topbar");
            hud.Add(top);
            // Phones stack the bar in two rows: money, stars, clock and buttons, then the goal.
            var row1 = phone ? Div("row", "top-row") : top;
            if (phone) top.Add(row1);
            hud.Add(new TruckStrip().Cls("hud-strip"));

            // Galla (cash box).
            var galla = Div("stat", "galla");
            galla.name = "galla";
            var cash = Text("", "money");
            var coins = Text("", "coins-label");
            galla.With(Text("GALLA", "stat-label"), cash, Div("row", "coins").With(new Icon("coin").Cls("coin-icon"), coins));
            hudBind.Text(cash, () => phone ? RsShort(sim.S.Cash) : Rs(sim.S.Cash));
            hudBind.Text(coins, () => sim.S.Coins.ToString("N0"));
            row1.Add(galla);

            // Reputation.
            var fame = new Stars(); var auth = new Stars();
            var rep = Div("stat", "rep").With(
                Div("row", "rep-row").With(Text("Shohrat", "rep-label"), fame),
                Div("row", "rep-row", "auth-row").With(Text("Asli-pan", "rep-label"), auth));
            hudBind.Do(() => { fame.Value = sim.S.Fame; auth.Value = sim.S.Auth; });
            if (!phone) row1.Add(rep);

            // Clock.
            var dial = new ClockDial(b.DayStart, b.DayEnd, GameController.PrayerMinutes);
            var time = Text("", "clock-time"); var when = Text("", "stat-label"); var azaan = Text("", "azaan");
            row1.Add(Div("stat", "clock").With(dial, Div("col").With(time, when, azaan)));
            hudBind.Do(() => dial.Minute = sim.S.Minute);
            hudBind.Text(time, () => Clock(sim.S.Minute));
            hudBind.Text(when, () => phone ? $"{SlotName(sim.Slot)} · Day {sim.S.Day}" : $"{SlotName(sim.Slot)} · {sim.Weekday} · Day {sim.S.Day}");
            hudBind.Text(azaan, () =>
            {
                int next = System.Array.FindIndex(GameController.PrayerMinutes, p => p > sim.S.Minute);
                return next >= 0 ? $"Next azaan: {GameController.PrayerNames[next]} {Clock(GameController.PrayerMinutes[next])}" : "Isha has passed. The street is full.";
            });

            // Chapter goal, always visible (it used to vanish on narrow screens).
            var goal = Div("stat", "goal");
            var goalTitle = Text("", "stat-label");
            var (bar1, fill1) = Bar("goal-bar"); var (bar2, fill2) = Bar("goal-bar");
            var l1 = Text("", "goal-text"); var l2 = Text("", "goal-text");
            var goalHead = Div("row").With(new Icon("target").Cls("goal-icon"), goalTitle);
            if (phone) goalHead.With(Div("grow"), rep); // phones: the stars sit on the goal row, the top row is full
            goal.With(goalHead, Div("goal-line").With(bar1, l1), Div("goal-line").With(bar2, l2));
            top.Add(goal);
            // Act 1, then Act 2, then the next rung of the ladder.
            int Stage3() => !sim.S.Act1Done ? 1 : !sim.S.Act2Done ? 2 : 3;
            hudBind.Text(goalTitle, () => Stage3() == 1 ? "ACT 1 · THE CART" : Stage3() == 2 ? "ACT 2 · BURNS ROAD KNOWS YOUR NAME"
                : sim.NextTier != null ? "NEXT: " + sim.NextTier.Name.ToUpperInvariant() : "BURNS ROAD IS YOURS");
            hudBind.Fill(fill1, () => Stage3() == 1 ? sim.S.TotalRevenue / (float)b.Act1.Revenue : Stage3() == 2 ? sim.S.Cash / (float)b.Act2.Cash
                : sim.NextTier != null ? sim.S.Cash / (float)sim.NextTier.SetupCost : 1);
            hudBind.Fill(fill2, () =>
            {
                if (Stage3() == 1) return sim.S.TotalServed / (float)b.Act1.Served;
                if (Stage3() == 2) return sim.S.Fame / b.Act2.Fame;
                var n = sim.TierNeeds(); return n.Count == 0 ? 1 : n.Count(x => x.met) / (float)n.Count;
            });
            string earned = phone ? "" : " earned", saved = phone ? "" : " saved", served = phone ? "" : " served";
            hudBind.Text(l1, () => Stage3() == 1
                ? $"{RsShort(Mathf.Min(sim.S.TotalRevenue, b.Act1.Revenue))} / {RsShort(b.Act1.Revenue).Replace("Rs ", "")}{earned}"
                : Stage3() == 2 ? $"{RsShort(Mathf.Min(sim.S.Cash, b.Act2.Cash))} / {RsShort(b.Act2.Cash).Replace("Rs ", "")}{saved}"
                : sim.NextTier != null ? $"{RsShort(Mathf.Min(sim.S.Cash, sim.NextTier.SetupCost))} / {RsShort(sim.NextTier.SetupCost).Replace("Rs ", "")}" : "");
            hudBind.Text(l2, () =>
            {
                if (Stage3() == 1) return $"{Mathf.Min(sim.S.TotalServed, b.Act1.Served)} / {b.Act1.Served}{served}";
                if (Stage3() == 2) return $"{(phone ? "" : "Shohrat ")}{sim.S.Fame:F1} / {b.Act2.Fame:F0} stars";
                var n = sim.TierNeeds(); return $"{n.Count(x => x.met)} / {n.Count} ready";
            });

            // Speed.
            var speed = Div("speed");
            if (phone)
            {
                // Two buttons on phones: play/pause, and one that steps 1x → 2x → 4x.
                var pp = IconBtn("pause", null, gc.TogglePause, "speed-btn");
                var step = Btn("1x", () => gc.SetSpeed(gc.Speed >= 4 ? 1 : gc.Speed >= 2 ? 4 : gc.Speed > 0 ? 2 : 1), "speed-btn", "speed-step");
                step.name = "speed-fast";
                speed.With(pp, step);
                hudBind.Do(() => pp.Q<Icon>().Set(gc.Paused ? "play" : "pause"));
                hudBind.Class(pp, "on", () => gc.Paused);
                hudBind.Text(step, () => gc.Paused ? "–" : $"{gc.Speed:0}x");
            }
            else
            {
                foreach (var (v, icon, hint) in new[] { (0f, "pause", "Space"), (1f, "play", "1"), (2f, "fast", "2"), (4f, "fastest", "3") })
                {
                    var btn = IconBtn(icon, null, () => gc.SetSpeed(v), "speed-btn");
                    if (v == 2) btn.name = "speed-fast";
                    btn.tooltip = hint;
                    hudBind.Class(btn, "on", () => gc.Speed == v);
                    speed.Add(btn);
                }
            }
            if (phone)
            {
                var store = IconBtn("store", null, () => ToggleSheet("Store"), "gear-btn");
                store.name = "top-Store";
                hudBind.Visible(store, () => TabUnlocked("Store"));
                hudBind.Class(store, "on", () => sheetOpen == "Store");
                row1.With(Div("grow"), store, IconBtn("settings", null, OpenSettings, "gear-btn"));
            }
            else top.With(speed, IconBtn("settings", null, OpenSettings, "gear-btn"));

            // Load shedding banner.
            var banner = Div("banner").With(new Icon("bolt").Cls("banner-icon"), Text("", "banner-text")).Ghost(true);
            var bannerRow = Div("banner-row").Ghost().With(banner);
            hud.Add(bannerRow);
            hudBind.Visible(banner, () => sim.PowerOut);
            hudBind.Text(banner.Q<Label>(), () => sim.S.Generator ? "Load shedding · generator running" : "Load shedding · no power, service is slower");

            // Bottom: panels, the shout, and (desktop) save.
            var bottom = Div("bottom-row").Ghost();
            hud.Add(bottom);
            var dock = Div("dock");
            foreach (var t in Tabs)
            {
                var id = t.id;
                if (phone && id == "Store") continue; // the coin button in the top bar opens it
                var btn = IconBtn(t.icon, t.label, () => ToggleSheet(id), "dock-btn");
                btn.name = "dock-" + id;
                if (phone) hudBind.Present(btn, () => TabUnlocked(id)); else hudBind.Visible(btn, () => TabUnlocked(id));
                if (!phone && t.key != Key.None) btn.Add(Text(t.key.ToString(), "key-hint"));
                var badge = Div("badge");
                btn.Add(badge);
                hudBind.Visible(badge, () => NeedsAttention(id));
                hudBind.Class(btn, "on", () => sheetOpen == id);
                dock.Add(btn);
            }

            var aao = Btn("", () => sim.Shout(), "aao");
            aao.name = "aao";
            var ring = new Ring();
            var aaoLabel = Text("", "aao-label");
            aao.With(ring, new Icon("shout").Cls("aao-icon"), aaoLabel);
            if (!phone) aao.Add(Text("Q", "key-hint"));
            hudBind.Enabled(aao, () => sim.S.AaoReady - sim.S.Minute <= 0 && !sim.S.DayOver);
            hudBind.Do(() => { float cd = Mathf.Max(0, sim.S.AaoReady - sim.S.Minute); ring.Value = cd <= 0 ? 1 : 1 - cd / gc.Data.Balance.Aao.Cooldown; });
            hudBind.Text(aaoLabel, () => { float cd = Mathf.Max(0, sim.S.AaoReady - sim.S.Minute); return cd > 0 ? $"{Mathf.CeilToInt(cd)}m" : "Aao bhai!"; });
            var adShout = IconBtn("ad", phone ? "Shout again" : "Ad: shout again", gc.RefillShout, "ad-chip");
            hudBind.Visible(adShout, () => gc.CanRefillShout);

            if (phone)
            {
                // One thumb: the shout in the middle of the dock, two panels either side, speed in the corner above.
                var zone = Div("col", "aao-zone").Ghost().With(adShout, aao);
                dock.Insert(2, zone);
                bottom.With(dock);
                var mapBtn = IconBtn("map", null, ToggleDistrict, "speed-btn").Named("dock-Map");
                hud.Add(Div("speed-corner").Ghost().With(mapBtn, speed));
            }
            else
            {
                var save = IconBtn("save", "Save", () => { gc.Save(); gc.Toast("Saved. Loading resumes from the start of today.", "tip"); }, "dock-btn");
                var mapBtn = IconBtn("map", "Map", ToggleDistrict, "dock-btn").Named("dock-Map");
                mapBtn.Add(Text("N", "key-hint"));
                dock.Add(mapBtn);
                bottom.With(Div("toolbar").With(dock, Div("toolbar-sep"), Div("col", "aao-zone").With(aao), adShout, Div("toolbar-sep"), save));
            }
            BuildFeedback(bannerRow, row1);
        }

        static string SlotName(string slot) => slot switch { "breakfast" => "Nashta", "day" => "Dopahar", "evening" => "Shaam", _ => "Raat" };

        // Red dots on the toolbar for things worth a look.
        bool NeedsAttention(string tab)
        {
            var s = sim.S;
            switch (tab)
            {
                case "Menu": return gc.Data.Dishes.Any(d => !sim.IsLocked(d.Id) && (!seenDishes.Contains(d.Id) || (s.Menu[d.Id].On && sim.PriceRatio(d.Id) > 1.2f)));
                case "Supplies": return sim.NeededIngredients().Any(i => s.Stock[i] < 10);
                case "Staff": return !sim.Maxed("staff") && s.Cash >= sim.TrackCost("staff") + 10000;
                case "Shop":
                    return sim.TierReady || gc.Data.Business.Tracks.Any(t => t.Id != "staff" && !sim.Maxed(t.Id) && s.Cash >= sim.TrackCost(t.Id) * 2);
                default: return false;
            }
        }

        // ---------- toasts ----------
        readonly List<(VisualElement el, float until)> toasts = new List<(VisualElement, float)>();

        void Toast(string text, string kind) => Toast(text, kind, null);

        // panel: optional shortcut button ("Open Supplies") on the toast.
        void Toast(string text, string kind, string panel)
        {
            if (!gc.Playing) return;
            var t = Div("toast", "toast-" + (string.IsNullOrEmpty(kind) ? "info" : kind));
            if (kind == "uncle")
            {
                t.Add(Div("portrait").With(new Icon("staff")));
                text = text.Replace("Uncle Rasheed: ", "");
                t.Add(Div("col", "grow").With(Text("Uncle Rasheed", "toast-who"), Text(text, "toast-text")));
            }
            else t.Add(Text(text, "toast-text"));
            t.Ghost(true);
            if (panel != null)
            {
                var go = Btn("Open " + panel, () => OpenSheet(panel), "btn-sm", "toast-action");
                t.Add(go); // added after Ghost, so this one button still takes taps
            }
            toastLayer.Insert(0, t);
            toasts.Insert(0, (t, Time.realtimeSinceStartup + (panel != null ? 11 : kind == "uncle" ? 8 : 6)));
            t.schedule.Execute(() => t.AddToClassList("in")).StartingIn(16);
            int max = phone ? 2 : 4;
            while (toasts.Count > max) { Remove(toasts[toasts.Count - 1].el); toasts.RemoveAt(toasts.Count - 1); }
        }

        void UpdateToasts()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
                if (toasts[i].until < Time.realtimeSinceStartup) { Remove(toasts[i].el); toasts.RemoveAt(i); }
        }

        static void Remove(VisualElement el)
        {
            el.RemoveFromClassList("in");
            el.schedule.Execute(() => el.RemoveFromHierarchy()).StartingIn(250);
        }
    }
}
