# Karachi Tycoon (Unity)

A tycoon game set in Karachi. You start with a chai and bun kabab thela on Burns Road and work up to a dhaba, and later to Clifton. This project is the Unity version of the Burns Road slice from the browser prototype, built for iOS, Android and Steam.

The design doc lives in the project files (`design/karachi-tycoon-game-design.md`). Section 20 is the scope of this slice and section 22 is the free-to-play business model.

## Open it

1. Install Unity 6 LTS (6000.0.x) with the iOS, Android and Windows/Mac build modules.
2. Unity Hub, Add, pick this folder. The first import pulls the packages in `Packages/manifest.json`.
3. Open any scene (the default `SampleScene` is fine) and press Play.

There is no scene to set up. `Bootstrap` in `GameController.cs` runs after the first scene loads and builds the street, people, lights and HUD in code. It also turns off any directional light the scene already has.

Controls: drag, A/D or the arrow keys to pan, the scroll wheel to zoom, Space to pause, 1/2/3 for speed. The HUD is a placeholder drawn with IMGUI.

## Layout

| Path | What it is |
|---|---|
| `Assets/Resources/Data/*.json` | All game data (dishes, ingredients, customers, staff, balance, the Burns Road layout). Shared with the browser prototype. |
| `Assets/Resources/Signs/*.png` | Signboards with Urdu in Nastaliq, painted ahead of time, because Unity text cannot shape Nastaliq. The file name is the English sign name in lower case with `_` for spaces. |
| `Assets/Scripts/Sim/` | The simulation in plain C# with no Unity calls: the day clock, customers, cooking, fame, authenticity, load shedding, Uncle Rasheed, chapter goals. |
| `Assets/Scripts/Game/` | The Unity side: `StreetBuilder` (Burns Road), `PeopleView` (customers), `DayCycle` (light from Fajr to late night, outages and the generator), `Monetization` (ads and store), `GameController` (start, save, HUD). |
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

## Before the first build

- Add `Standard` (or `Universal Render Pipeline/Lit` once URP is set up) to Project Settings, Graphics, Always Included Shaders. Materials are made in code, so a build would otherwise strip the shader.
- The project runs on the built-in renderer until a URP asset is assigned in Graphics settings. `Util.Lit` picks the right shader for either.

## Next steps

- Replace the IMGUI HUD with UI Toolkit, with Urdu UI text through an RTL plugin.
- Set up URP with bloom for the tube lights and sodium lamps.
- Swap the primitive people and buildings for low-poly models (art style guide in the design doc).
- Wire up LevelPlay, Unity IAP, analytics and the consent prompts.
