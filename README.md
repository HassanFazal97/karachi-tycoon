# Karachi Tycoon (Unity)

A tycoon game set in Karachi. You start with a chai and bun kabab thela on Burns Road and work up to a dhaba, and later to Clifton. This project is the Unity version of the Burns Road slice from the browser prototype, built for iOS, Android and Steam.

The design doc lives in the project files (`design/karachi-tycoon-game-design.md`). Section 20 is the scope of this slice and section 22 is the free-to-play business model.

## Open it

1. Install Unity 6 (6000.6.5f1 is what the project is set to) with the iOS, Android and Mac/Windows build modules.
2. Unity Hub, Add, "Add project from disk". This Hub version scans the folder you pick for projects inside it, so pick the folder that contains `karachi-tycoon` (for example the Desktop), not `karachi-tycoon` itself.
3. Open `Assets/Scenes/Main.unity` and press Play.

The first time the editor opens the project, `BuildTools` (in `Assets/Editor`) sets it up: the scene, the UI panel asset, Active Input Handling set to "Both", and the lit shader in Always Included Shaders. Restart the editor once after that so the input setting takes effect. The same steps are under the **Karachi Tycoon** menu.

There is nothing to set up in the scene. `Bootstrap` in `GameController.cs` builds the street, people, lights, audio and UI in code.

### Controls

| | Desktop | Phone |
|---|---|---|
| Pan | drag, A/D or arrow keys | drag (with inertia) |
| Zoom | scroll wheel, +/- | pinch |
| Pause / speed | Space, 1 2 3 | buttons in the bottom-right corner; the speed button cycles 1x/2x/4x |
| Panels | M Menu, S Supplies, H Staff, B Shop | dock at the bottom; Store is the coin button at the top |
| Shout | Q | big red button in the middle of the dock |
| Pause menu and settings | Esc | gear |

### Phone and desktop layouts

The UI is UI Toolkit, built in code (`Assets/Scripts/Game/UI`) and styled by `Assets/Resources/UI/Main.uss`. The root element carries `.phone` or `.desktop`:

- **Phone** (portrait, one hand): a two-row top bar (cash, clock, Store and settings; then the act goal and stars), the street framed tall around the cart, a bottom dock with "Aao bhai" in the middle and two panels either side, speed in the corner, panels as half-height bottom sheets, 44pt touch targets, safe-area aware. Phones are locked to portrait.
- **Desktop / Steam** (landscape): fuller top bar with the next azaan, a labelled toolbar with key hints, panels as a side window.

Phones get the phone layout automatically. To preview it in the editor, set the Game view to a portrait phone resolution (for example 390x844) and choose Layout: Phone in the pause menu (gear or Esc).

## Layout

| Path | What it is |
|---|---|
| `Assets/Resources/Data/*.json` | All game data (dishes, ingredients, customers, staff, balance, the Burns Road layout). Shared with the browser prototype. |
| `Assets/Resources/Signs/*.png` | Signboards with Urdu in Nastaliq, painted ahead of time, because Unity text cannot shape Nastaliq. The file name is the English sign name in lower case with `_` for spaces. |
| `Assets/Scripts/Sim/` | The simulation in plain C# with no Unity calls: the day clock, customers, cooking, fame, authenticity, load shedding, Uncle Rasheed, chapter goals. |
| `Assets/Scripts/Game/` | The Unity side: `StreetBuilder` (Burns Road), `PeopleView` (customers), `DayCycle` (light from Fajr to late night, outages and the generator), `Monetization` (ads and store), `GameController` (start, save, the sim loop), `CameraRig` (pan, pinch, inertia), `AudioDirector` (synthesised street sounds, coins, bells; quiet around azaan), `Services` (settings, analytics stand-in). |
| `Assets/Scripts/Game/UI/` | The HUD, panels, ledger, title and settings (`GameUi`), vector-drawn icons, stars, clock dial and truck-art bunting (`Drawn`), layout helpers (`UiKit`). |
| `Assets/Resources/UI/` | `Main.uss` (all styling), the theme and the panel settings asset. |
| `Assets/Editor/BuildTools.cs` | Project setup and a command-line macOS build. |
| `design/upgrade-plan.md` | The plan for onboarding, UI/UX and engagement. |
| `Tools/BalanceRunner/` | Runs the simulation for many days without Unity, to check balance. |
| `Tools/AdRulesCheck/` | Checks the ad rules, caps and prayer-time blackouts without Unity. |

## Balance check without Unity

```
dotnet run --project Tools/BalanceRunner -- ghee 7
```

It plays a simple strategy for 7 days (`ghee` or `oil`) and prints each day's ledger and when each chapter goal is met. With ghee, Act 1 is done on day 3 and Act 2 on day 8, the same as the browser prototype.

## The first ten minutes (tutorial)

New players get Uncle Rasheed's guided first day (upgrade plan, "The first ten minutes"). Anyone with a save can choose "Skip" on the title screen.

- **Cold open:** three sepia cards of Dada's story, skippable.
- **The guided day:** Uncle is the first customer. The player puts chai on the board, watches him get served, sees the galla, then meets three easy customers (the street is quieter and more patient until Maghrib, with no load shedding or rivals). After that come the lessons: shout "Aao bhai", fix a "Mehenga hai!" price, raise milk's top-up level and buy up to it, speed up the afternoon. The Maghrib rush is the player's alone; then a guided hisaab and the first goal card.
- **How it looks:** the screen dims except for the one thing to tap, with a pulsing ring around it and Uncle's card on the far side. Lessons pause the clock. Every card has "Skip tutorial".
- **Panels unlock as they are taught:** Menu and Supplies during day 1, Staff on day 2, the Shop with Act 1, ghee vs oil with the dhaba. The Store stays hidden on the guided day. Each unlock gets one explainer card.
- **One-time hints from Uncle:** first walkout, sell-out, outage, long line, "Mehenga hai!" and losing day; on day 3, a nudge if the player has never shouted or changed a price. Hints that point at a panel have an "Open ..." button. Every tip is collected in **Dada ki Kitaab** (pause menu).
- **Gentle failure:** in Act 1, if the night's bills leave less than a day's stock, Uncle lends the difference (shown on the hisaab).
- **Analytics:** `cold_open`, `tutorial_start`, `tutorial_step` (id, index, seconds), `tutorial_skip`, `tutorial_complete`, `unlock`, `hint`, `kitaab_open`.
- **Where things live:** the words are in `Resources/Data/tutorial.json` (steps, hints, unlock cards, Kitaab). When each step starts and ends is in `TutorialDirector.cs`. The overlay is `GameUi.Coach.cs`. The Sim's part (`TutorialDay`, `HoldCustomers`, `NoRefusals`, the quiet street and the Act 1 floor) stays plain C#.
- **Old saves** (from before the tutorial) skip it and keep every panel.

The test build plays the whole tutorial by itself: add `-kt-scenario tutorial` to the `ShotRunner` command below.

## Goals, upgrades, streaks and feedback (M3)

- **Aaj ka kaam:** three daily goals from `Resources/Data/goals.json` (serve N of a dish, N customers, N before Zuhr, no walkouts in Shaam, keep Uncle happy, tips, a Rush streak, takings). Targets grow over the first week. Each pays cash at once (on the hisaab as "Aaj ka kaam rewards") and silver coins (into the wallet). One goal a day can be swapped with a rewarded ad. Desktop shows the goals card under the top bar; phones open it from the target button in the top row. None on the guided first day.
- **Thela upgrades** (`Resources/Data/upgrades.json`, bought in the Shop, which now unlocks on day 3): plastic stools (patience), a bigger kettle (chai cooks faster), tube lights (more night customers), a truck-art signboard (more people stop) and a second tawa (faster bun kabab). Each one appears on the cart.
- **Rush streak:** customers served in a row without a walkout. Tips rise by up to half; one walkout resets it. A pill shows it from x5, and a ding rises every 5.
- **Feedback:** "+Rs" coins fly from each served customer into the galla, the galla bumps, a SHABASH! stamp thumps down when a goal is done, and sounds play for the stamp, upgrades and streaks.
- **Readable crowds:** only the customer at the front calls out an order, one order bubble at a time, and complaints sit at staggered heights.
- **Analytics:** `goal_done`, `goal_swap`, `upgrade_buy`.

## Story and collections (M4)

Content lives in `Resources/Data/story.json`; the rules are in the Sim (plain C#).

- **Named regulars (8):** Bilal the rickshaw driver, Pappu the newspaper boy, Saima Baji, Hamza from NED, Akhtar Sahab from the bank, the Yusuf family, Rehana Khala and Constable Ghafoor. Each comes on set weekdays in a set time slot, orders their usual, and says a line (a toast and a name bubble). Served quickly at a fair price, they gain friendship (levels at 3, 6 and 10 visits, each with coins and a line). At level 2 they share a page of Dada's book. They remember prices: overcharge them and they say so.
- **Dada ki Kitaab collection (12 recipe pages):** each page is a small permanent bonus (better taste for a dish, more patience, more people stopping, bigger tips, less spoilage). Found through Uncle's requests, regulars and choice cards; a sepia postcard shows each one. The Kitaab (pause menu) is now a hub: the pages album with empty slots and hints, the regulars' register with friendship hearts, and every tip.
- **Choice cards (8):** at most one a day from day 2, at a random moment, with no repeats until the deck is used. Each has two or three answers with a short preview of what they cost or give. Examples: the beat constable wants free chai, a food blogger, flood relief, a wedding order, the kiryana's bulk offer, a student Rs 50 short, the broast man's truce, a TV crew at the dhaba.
- **Uncle Rasheed's quest line (4 requests):** "Dada's elaichi chai" (serve 60 chai), "The shami secret" (120 bun kababs), "Nihari, 1975" (40 nihari at the dhaba) and "Burns Road knows your name" (two stars). Each opens and closes with Uncle's card, and the current one sits at the top of the goals card.
- **Analytics:** `friend_level`, `page_found`, `card_shown`, `card_choice`, `quest_start`, `quest_done`.

## Art pass (M6)

Following the art style guide ("painted realism", the five Karachi rules, the Burns Road palette and lighting presets):

- **URP** (`Assets/Settings/KT_URP.asset`, made by `BuildTools`): HDR, one soft shadow cascade, per-pixel lights, FXAA, no MSAA, so it runs on mid-range Android. Template materials in `Resources/Materials` pull in only the shader variants used.
- **Post-processing** (`Visuals.cs`): bloom on the practical lights, a neutral film curve, slight warmth and contrast, a soft vignette. Settings, then Graphics, then "Battery saver" turns post-processing and shadows off.
- **Light:** three colour temperatures at night, as the guide asks: warm tungsten at the stall and the sodium pole lamps, cool tube light spilling from the shops, hot charcoal at the grills. The mosque's green neon comes on first at Maghrib.
- **Painted textures** (`Paint.cs`, made at start-up): lime plaster with rain streaks, worn asphalt, paving slabs, ribbed rolling shutters, wood grain.
- **Lived-in buildings:** stone pilasters, carved parapets and date plaques, split AC units, washing on lines under the jharokas, satellite dishes, posters pasted on pilasters, black water tanks, tangled wires.
- **The street spills out:** parked motorbikes, stacked plastic chairs, crates, bunting strung across the road, faded lane paint.
- **Sky** (`Ambient.cs`): kites over the rooftops and circling crows by day; steam off the kettle and the nihari deg; charcoal smoke and, after dark, sparks from the grills.
- **People:** about seven heads tall, with shalwar and a knee-length kameez (longer for women), dupattas, swinging arms and chappals. Uncle Rasheed wears a white kurta, a waistcoat and glasses, with a grey beard.
- **Performance:** the street is static-batched once it is built. In the test build it runs at 60 fps (vsync) with about 15 lights and 3 particle systems; check on a real mid-range phone before release.

Before and after screenshots are in `design/screenshots/m6`.

## The business ladder and upgrade tracks

Defined in `Resources/Data/business.json`, run by the Sim (plain C#), built on the street by `StreetBuilder`.

| Tier | What you see | Seats | Staff | Cost | Needs |
|---|---|---|---|---|---|
| Thela | Dada's cart | 0 | 1 | | |
| Bara thela | a second cart, a wider canopy, stools | 4 | 1 | Rs 12k | 120 served; Kitchen and Signboard at max |
| Pavement dhaba | takhta, nihari deg, plastic chairs on the road | 10 | 3 | Rs 35k | Act 1; Kitchen at max |
| Pakki dukaan | the "To Let" shutter goes up: lit shop, counter, your board | 24 | 5 | Rs 1.5 lakh | Act 2; Seating and Kitchen at max |
| Family restaurant | both shops, one long board, the first-floor family hall with AC | 40 | 8 | Rs 4.5 lakh | 2.8 stars, 6,000 served; Staff and Comfort at max |
| Famous restaurant | neon over the whole building, a lit rooftop | 60 | 10 | Rs 12 lakh | 3.6 stars; Kitchen, Seating and Signboard at max |

Bigger places draw people from across the city (`draw`), pay more rent, and raise every track's cap.

**Tracks** (Upgrades panel; level pips, now and next effect, cost; a MAX stamp at the tier's cap):
- **Staff:** hire the next person on the roster in `staff.json`: chhotu, karigar, waiters, cooks, a cashier, a manager, ten in all.
- **Training:** faster service.
- **Seating:** more seats; customers also wait longer.
- **Kitchen:** faster cooking.
- **Comfort:** fan, then cooler, then AC; customers wait longer.
- **Storage:** less overnight spoilage.
- **Signboard:** more people stop.

Many levels show on the cart: a bigger kettle, a second tawa, truck art, tube lights, a fan, a cooler, an ice box, and the dhaba's chairs.

Pacing with the sensible-player run (`dotnet run --project Tools/BalanceRunner -- ghee 7`): Act 1 on day 3, Bara thela day 3-4, Pavement dhaba day 5, Act 2 day 8, Pakki dukaan day 9, Family restaurant day 13-14, Famous restaurant day 21. `-- migrate` checks that an old save (M3 upgrades, two-person staff) loads into the tracks.

Headless screenshots: start a test build with `-batchmode -kt-offscreen` and the ShotRunner renders the camera and the UI into textures. That works with the screen locked or on a CI machine.

## Ads and purchases

Ads live in `Assets/Scripts/Game/Ads` (design doc §22):

- `AdsService` is the only way into ads. It asks `AdRules` whether an ad may show, plays it through the platform's `IAdsProvider`, gives Seth ji Club members the reward without an ad, and logs every outcome (`ad_rewarded_start/complete/skipped/blocked`, `ad_interstitial_shown/skipped` with a reason) to `IAnalytics`.
- Providers: `SimulatedAdsProvider` (editor, and test builds with `-kt-sim-ads`), `NoAdsProvider` (Steam, since Valve does not allow ads, and builds without an SDK), and `AppLovinMaxProvider`, a stub to fill in once the AppLovin MAX plugin is imported and `APPLOVIN_MAX` is defined.
- `AdRules` (plain C#): a rewarded soft cap of 20 a day with per-placement caps, cooldowns and a first day (welcome-back and generator offers don't count toward the cap). No ads in the tutorial or on day 1. Interstitials only at the day-end break: not in the first 3 sessions or 20 minutes of play, at most 6 a day and one per 10 minutes, skipped right after a rewarded view, off for 30 days after any purchase, never with No Ads.
- Blackouts (`PrayerTimes`, plain C#): no ads within 10 minutes of the real prayer times on the device's clock (sun position for a location guessed from the time zone, with Karachi and the main Canadian cities listed), around iftar in Ramadan, and all day on 9-10 Muharram. The in-game azaan also blocks them.
- Every number comes from `AdConfig`, loaded from `Resources/Data/ads.json` with per-country overrides under `"countries"`. A Remote Config provider can replace `LocalRemoteConfig` and serve the same JSON.

Purchases: the game talks to `IStoreService`; `SimulatedStore` grants products into the `Wallet` (saved apart from the day save). Products in `Catalog`: No Ads, Dada ki Barkat (permanent 2x income, applied in the Sim), Seth ji Club (30 days, ad bonuses without ads), Dada's Trunk and three coin packs. To go live, write `UnityIapStore` / `SteamStore : IStoreService` and swap them in `GameController`.

Check the ad rules without Unity:

```
dotnet run --project Tools/AdRulesCheck
```

## Checking the game without the editor

`BuildTools.BuildMac` makes a macOS build from the command line, and `ShotRunner` drives it: start the build with `-kt-shots <folder>` (and `-kt-layout phone` or `desktop`) and it plays a new game, clicks and drags with synthetic input, opens every panel, ends the day, saves a screenshot of each step and quits. It never saves the game.

```
Unity -batchmode -nographics -projectPath . -executeMethod KarachiTycoon.EditorTools.BuildTools.BuildMac -kt-test
Builds/mac/KarachiTycoon.app/Contents/MacOS/Karachi\ Tycoon\ Test -screen-fullscreen 0 -screen-width 1688 -screen-height 780 -kt-layout phone -kt-shots /tmp/shots
```

`-kt-test` names the build "Karachi Tycoon Test" so it keeps its own PlayerPrefs. Close the editor first; Unity will not open a project twice.

Analytics events (`session_start`, `game_start`, `panel_open`, `day_end`, `act_done`, `rewarded`, `interstitial`, `purchase`) go to the log through `LogAnalytics` until a real backend is plugged in behind `IAnalytics`.

## Next steps

See `design/upgrade-plan.md`. Done: M1 (UI Toolkit, portrait phone and landscape desktop layouts, touch camera, audio, analytics hooks), the ads refactor, M2 (the guided first day), M3 (daily goals, thela upgrades, Rush streaks, feedback) M4 (regulars, recipe pages, choice cards, Uncle's quests) and M6 (the art pass: URP, bloom, painted textures, lived-in street, better people). Balance with the sensible-player run: Act 1 on day 3, Act 2 late on day 7 (goal rewards and streak tips bring it about 20 game-hours earlier than before).

- Real low-poly models (CC0 packs) for people and props, and a Naskh font for Urdu UI text.
- Wire up LevelPlay, Unity IAP, analytics and the consent prompts.
