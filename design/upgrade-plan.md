# Karachi Tycoon: Upgrade Plan

This plan upgrades the Burns Road slice in three areas: onboarding (a tutorial), the UI/UX, and engagement. It is based on the current code: `Sim/` (686-line simulation), `GameController.cs` (IMGUI HUD), `StreetBuilder`, `PeopleView`, `DayCycle`, `Monetization`, and the JSON data in `Resources/Data`.

## Where the game is today

What works:
- A solid simulation: day clock, footfall by time slot, patience, prices against what customers expect, stock and spoilage, ghee vs oil, mastery, fame and Asli-pan, load shedding, rivals, Uncle Rasheed, two acts.
- Strong flavour: Burns Road, Nastaliq signboards, azaan-aware ads, Jumma, the hisaab ledger.

What holds it back:
1. **The player has little to do during a shift.** Customers are served automatically. Mid-shift, the only actions are the "Aao bhai" shout and the speed buttons. Every real decision happens in menus.
2. **There's no onboarding.** One toast explains the patience bar, and then all five tabs are open at once. New players don't know what matters: price vs expected price, top-up levels, ghee vs oil.
3. **The UI is a placeholder.** It's text-heavy IMGUI with fixed pixel positions. The act goals are hidden on screens narrower than 1200px. Stars are drawn as `*` characters. There's no touch input (pinch, tap) and no safe areas, though mobile is the main platform.
4. **Feedback is thin.** No sounds, no floating money, no celebration when you hit a goal. Toasts are the only voice.
5. **There's little to aim for beyond the two act goals.** No daily objectives, no upgrades you can see, no collection, no events.

## Guiding principles

- **Act during the shift, manage between shifts.** Players tap to serve early on. Hiring staff automates those actions, so progress feels like moving from doing the work to running the place.
- **Introduce one system at a time.** Tabs and mechanics unlock as the story reaches them.
- **Uncle Rasheed is the guide.** He's already a recurring character, so the tutorial speaks through him rather than through system text.
- **Keep the Sim free of UnityEngine.** All new rules (tutorial mode, objectives, events, upgrades) go in `Sim/` as plain C# so `BalanceRunner` can test them. The Unity side only shows them.
- **Data-driven.** Tutorial steps, objectives, events and upgrades live in JSON next to the existing data.

---

## Phase 0: Foundations (needed by everything else)

| Work | Detail |
|---|---|
| UI Toolkit migration | Replace the IMGUI in `GameController` with UI Toolkit (UXML + USS). Split it into `HudView`, `PanelView`s, `LedgerView`, `TitleView`, `ModalView`. `GameController` keeps the loop and save; the views subscribe to Sim events. |
| UI skin | One USS theme from the art style guide §13: wood (`#3B2A1E`), paper (`#F1E6CC`), ledger ink (`#2B3F8F`), loss red (`#C8202F`), gold (`#E2C27E`). Truck-art borders as 9-slice sprites. |
| Urdu text | Short-term: pre-rendered Nastaliq sprites for headings (same approach as the signboards). Long-term: an RTL plugin for body text. Every string goes through a `Strings` table (en/ur) from the start. |
| Touch input | Pinch to zoom, one-finger pan, tap to select (raycast to customers, cooking stations, buildings). Use the Input System package. |
| Layout | Mobile-first and portrait-safe, honouring `Screen.safeArea`. Tap targets at least 44pt. Panels are bottom sheets on phones and side panels on desktop. |
| Audio | An `AudioDirector` with ambient loops per time slot, UI sounds and money sounds. Azaan fades all music and street noise down (fits §19). |
| Save v2 | Versioned migrations (`GameState.Version`), autosave at day start, and new fields for the tutorial, objectives and upgrades. |
| Analytics hooks | An `IAnalytics` interface (simulated, like ads) that logs tutorial steps, day ends, purchases and quits, so the onboarding funnel can be measured. |

## Phase 1: Onboarding (the first 10 minutes)

### Flow: "Dada's first morning"

| # | Beat | What the player does | What unlocks |
|---|---|---|---|
| 0 | Cold open: three illustrated panels (1947, Dada's recipe book, the old thela). Skippable. | Tap through | - |
| 1 | Name the stall, choose grandson or granddaughter | Same as today, restyled | - |
| 2 | Uncle Rasheed walks up: "Pehli chai mujhe pilao." The camera frames the thela. | Tap Uncle to take his order, then tap the kettle to pour | Tap-to-serve (Phase 2) |
| 3 | Three easy customers arrive one at a time. A patience bar is highlighted. | Serve them before the bar runs out | Patience concept |
| 4 | A customer says "Mehenga hai!" and walks off | Open the **Menu** board (highlighted) and lower chai by Rs 10. A gauge shows "fair / pricey". | Menu tab |
| 5 | The milk runs low | Open **Supplies**, set the top-up level. Uncle explains that milk spoils overnight. | Supplies tab |
| 6 | The street goes quiet | Press **Aao bhai!** | Shout |
| 7 | Speed buttons pulse | Try 2x | Speed |
| 8 | First day ends early (tutorial day is shorter) | A guided ledger: sales, expenses, then the MUNAFA stamp | Ledger |
| 9 | Day 2 opens with three "Aaj ka kaam" goals | - | Objectives (Phase 2) |

Later, systems unlock when the story reaches them, not all at once:
- **Staff** when bun kabab unlocks (first 40 customers): "Chhotu can take orders while you cook."
- **Shop** at the end of Act 1.
- **Ghee vs oil** on day 3, introduced by Uncle as a choice with a trade-off.
- **Load shedding** with an in-the-moment hint the first time it happens.
- **Store** only after the first session, and never inside the tutorial.

### Contextual hints after the tutorial

These trigger once, the first time something happens: first sold-out dish, first walkout because the queue was long, first spoilage, first rival steal, first loss-making day. Each one gets a short Uncle Rasheed line plus a tap-to-open link to the right panel.

### Implementation
- `Resources/Data/tutorial.json`: steps with `id`, `trigger` (a Sim event or condition), `highlight` (a UI element name or world target), `text` (en/ur), `complete` (event), `simMode` (pause / slow / normal).
- `Sim.TutorialMode`: lower footfall, scripted spawns (Uncle, then three easy customers), no outages or rivals on day 1, a shorter day. Plain C#, so it can be tested in BalanceRunner.
- `TutorialDirector` (Unity side): spotlight overlay that dims everything except the target, pointer hand, Uncle portrait bubble.
- Saved per step, so quitting mid-tutorial resumes in the right place. "Skip tutorial" on the title screen; "Replay tutorial" in Settings.
- Targets: at least 85% of players finish the tutorial, and the first session lasts at least 8 minutes.

## Phase 2: Engagement

### 2.1 Something to do during the shift
- **Tap to serve.** Tap a queued customer to take the order. The order shows as a bubble of dish icons. Cooking keeps its current timing in the Sim.
- **Perfect pour / tadka timing.** An optional timing tap while a dish cooks. A perfect hit gives a quality bonus, more tips and more mastery XP. Missing it costs nothing, so it never punishes.
- **Streaks.** Serving several customers in a row without a walkout builds a "Rush" multiplier on tips, shown with a flame on the galla.
- **Staff automate.** Chhotu takes orders on his own. The karigar handles the nihari deg. Players can still tap for bonuses. This is how the game moves from hands-on to managing as you progress.
- Balance: Sim gets `TakeOrder(customerId)` and `TimingHit(cookId, quality)`. BalanceRunner gets an "auto-tapper" strategy so the act timings stay where they are (Act 1 around day 3, Act 2 around day 8).

### 2.2 Goals at every scale
- **Aaj ka kaam (daily):** three small goals each day, e.g. "Serve 20 chai before Zuhr", "No walkouts during the evening rush", "Sell 5 nihari". Rewards are cash, a few silver coins, or recipe pages.
- **Weekly challenge:** one bigger goal, e.g. "Reach 1.5 Shohrat this week."
- **Act goals** move into a progress bar that is always visible on the HUD, at every screen size.

### 2.3 Visible upgrades
An upgrade tree per stage. Each upgrade appears on the street (via `StreetBuilder`) and does one clear thing:

| Upgrade | Effect |
|---|---|
| Bigger kettle | Faster chai |
| Second tawa | Two bun kababs at once |
| Painted signboard (truck art) | More people stop to look |
| Tube lights | Better night footfall, and bloom in Phase 3 |
| Plastic stools at the thela | More patience |
| Dhaba fan / cooler | Less summer patience loss |

Data in `upgrades.json`; effects applied as multipliers in Sim.

### 2.4 Dada's recipe book (collection meta)
The book Dada brought in 1947 has missing pages. You find them through daily goals, regulars and events. Each page unlocks a dish variant (e.g. "Dada's elaichi chai") or a permanent bonus. It's the emotional thread of the game and gives a reason to come back.

### 2.5 People with stories
- **Regulars:** named customers who return on certain days, remember your prices and ask for their usual. Serve them well and they bring friends (fame), which also gives the Sim's archetypes a face.
- **Uncle Rasheed's quest line:** chapter-by-chapter story beats that unlock as the acts progress.
- **Choices:** a beat constable wants free chai; a food blogger arrives; a rival offers a price truce. Each is a two-option card with a trade-off in cash, fame or Asli-pan.

### 2.6 Karachi events
Special days that change footfall and demand, defined in `events.json`:
- **Cricket night (Pakistan vs India):** a huge late-night crowd. Big chai demand and big queues.
- **Monsoon rain:** fewer people, but pakora and chai demand spikes.
- **Ramzan:** a whole mode. Closed by day, a sehri rush and an iftar rush. Respectful of the azaan/ad rules.
- **Eid / Chaand Raat:** family crowds and sweets.
- **Wedding catering order:** a bulk order with a deadline. Risk vs reward.

### 2.7 Small extras
- Achievements, and a stats page (best day, most popular dish, regulars served).
- An end-of-day "Kal ke liye" card that suggests one improvement based on the ledger, e.g. "You sold out of bun kabab at 8pm. Raise the top-up."

## Phase 3: UI/UX upgrade

### HUD (phone first)
- **Top bar:** galla with an animated count-up, fame and Asli-pan as star icons, and a clock dial (sun to moon) with azaan markers around the edge.
- **Goal strip:** an act progress bar plus today's three goals, collapsible.
- **Bottom bar:** icon buttons (Menu, Supplies, Staff, Shop) with red-dot badges for problems such as low stock or a dish that's too pricey. A large Aao bhai button with a radial cooldown.
- **Notifications:** one feed instead of four stacked text boxes. Important messages come from Uncle's portrait; minor ones are small pills that fade.

### Panels
- **Menu board:** a chalkboard look. Each dish has a price slider with a fairness gauge (based on the Sim's expected price), plate cost, and margin shown live.
- **Supplies:** a forecast ("lasts about 1.5 days"), spoilage warnings, and a one-tap "Restock all".
- **Staff:** portrait cards with what each person automates.
- **Ledger:** a real khata page with a handwritten font, lines filled in one by one, and the MUNAFA / NUQSAN stamp at the end.

### Feedback
- "+Rs 120" floating above a served customer, and a coin-drop sound into the galla.
- Mood emoji in customer bubbles (the Sim already emits `Bubble` events), plus a quick sound for "Mehenga hai!".
- Goal complete: a stamp, a sound and confetti in truck-art colours.
- Act complete: a full-screen story card with art in place of the plain text window.

### Settings and accessibility
- Music and sound sliders, language (English / Urdu UI), text size, colour-blind-safe patience bars, reduced motion, Replay tutorial, Restore purchases.

## Phase 4: Presentation (from the README's next steps)
- URP with bloom for tube lights and sodium lamps, plus a night colour grade.
- Low-poly people and buildings in place of primitives, with simple walk, sit and eat animations.
- Camera moments: a dawn establishing shot, and focus shots on goals and events.

## Phase 5: Live readiness
- Unity LevelPlay and Unity IAP behind the existing interfaces, and Steamworks.NET for Steam.
- Rewarded ads tied to the new systems, always optional: double daily rewards, or one extra recipe-page chance.
- Real analytics backend for the funnel, plus remote config for balance values.

---

## Order of work

| Milestone | Contents | Result |
|---|---|---|
| M1 | Phase 0: UI Toolkit shell, theme, touch input, Strings table, audio skeleton | Same game, new UI, playable on phone |
| M2 | Phase 1 tutorial + tap to serve (2.1) | New player is taught in about 8 minutes |
| M3 | Daily goals, upgrades, HUD goal strip, feedback | A reason to play another day |
| M4 | Recipe book, regulars, choice cards, Uncle quest line | The story and collection meta |
| M5 | Events (cricket, monsoon, Ramzan, Eid, catering) | Variety and memorable days |
| M6 | Phase 4 art and URP | Looks like a shipped game |
| M7 | Phase 5 live services | Ready for soft launch |

M1 and M2 are the most important: everything else builds on the new UI layer and the tap-to-serve loop. I'd check balance with BalanceRunner at the end of every milestone.

## Questions for Fazal
1. **Tap to serve:** OK to make the shift active (tap customers, timing bonus), or keep it a pure management sim?
2. **Platform priority:** phone portrait first, or landscape (phone and Steam share one layout)?
3. **Urdu UI:** must the first new UI ship in Urdu, or is English with Urdu headings OK to start?
4. **Art:** will someone make portraits and models, or should I start with stylised placeholders (and AI-generated concept art for direction)?
5. **Ramzan mode:** in scope for this slice, or later?
