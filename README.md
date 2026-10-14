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
| Pause / speed | Space, 1 2 3 | pause button, speed button cycles 1x/2x/4x |
| Panels | M Menu, S Supplies, H Staff, B Shop | dock, bottom right |
| Shout | Q | big red button, bottom left |
| Pause menu and settings | Esc | gear |

### Phone and desktop layouts

The UI is UI Toolkit, built in code (`Assets/Scripts/Game/UI`) and styled by `Assets/Resources/UI/Main.uss`. The root element carries `.phone` or `.desktop`:

- **Phone** (landscape): compact top bar, the shout under the left thumb, the panel dock under the right, panels as a half-screen sheet, 40pt+ touch targets, safe-area aware.
- **Desktop:** fuller top bar with the next azaan, a labelled toolbar with key hints, panels as a side window.

Phones get the phone layout automatically. To preview it in the editor, set the Game view to a phone resolution (for example 844x390) and choose Layout: Phone in the pause menu (gear or Esc).

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

## Balance check without Unity

```
dotnet run --project Tools/BalanceRunner -- ghee 7
```

It plays a simple strategy for 7 days (`ghee` or `oil`) and prints each day's ledger and when each chapter goal is met. With ghee, Act 1 is done on day 3 and Act 2 on day 8, the same as the browser prototype.

## Ads and purchases

The game only talks to `IAdsService` and `IStoreService`. For now `SimulatedAds` shows a short fake ad and `SimulatedStore` grants products straight away, so the whole flow can be played in the editor.

- Rewarded ads: double tonight's tips on the end-of-day ledger, and refill the "Aao bhai" shout.
- Interstitials: only when you press Next day, never in the first two sessions, at most one every 10 minutes, and not with No Ads.
- `AdPolicy` blocks every ad within 8 game minutes of a prayer time (design doc §19 and §22).
- Products are in `Catalog`: No Ads, Dada's Trunk starter pack and three coin packs.
- Coins and No Ads are saved apart from the day save, so a reload never takes back a purchase.

To go live, add Unity LevelPlay (ads) and Unity IAP (stores), then write a `LevelPlayAds : IAdsService` and a `UnityIapStore : IStoreService` and swap them in `GameController`. On Steam, use Steamworks.NET for purchases and no ads.

## Checking the game without the editor

`BuildTools.BuildMac` makes a macOS build from the command line, and `ShotRunner` drives it: start the build with `-kt-shots <folder>` (and `-kt-layout phone` or `desktop`) and it plays a new game, clicks and drags with synthetic input, opens every panel, ends the day, saves a screenshot of each step and quits. It never saves the game.

```
Unity -batchmode -nographics -projectPath . -executeMethod KarachiTycoon.EditorTools.BuildTools.BuildMac -kt-test
Builds/mac/KarachiTycoon.app/Contents/MacOS/Karachi\ Tycoon\ Test -screen-fullscreen 0 -screen-width 1688 -screen-height 780 -kt-layout phone -kt-shots /tmp/shots
```

`-kt-test` names the build "Karachi Tycoon Test" so it keeps its own PlayerPrefs. Close the editor first; Unity will not open a project twice.

Analytics events (`session_start`, `game_start`, `panel_open`, `day_end`, `act_done`, `rewarded`, `interstitial`, `purchase`) go to the log through `LogAnalytics` until a real backend is plugged in behind `IAnalytics`.

## Next steps

See `design/upgrade-plan.md`. Done: M1 (UI Toolkit, phone and desktop layouts, touch camera, audio, analytics hooks). Next: M2, the guided first day with Uncle Rasheed and tap to serve.

- Set up URP with bloom for the tube lights and sodium lamps.
- Swap the primitive people and buildings for low-poly models (art style guide in the design doc).
- Wire up LevelPlay, Unity IAP, analytics and the consent prompts.
