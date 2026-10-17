using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace KarachiTycoon.Game
{
    // Automated screenshots for checking the UI without a person at the keyboard.
    // Run a build with:  -kt-shots <folder>  (and optionally -kt-layout phone|desktop)
    // Default: plays a new game (tutorial skipped) for a while, opens every panel, ends the day and quits.
    // With -kt-scenario tutorial: starts from the title, plays Uncle Rasheed's guided day by tapping
    // whatever he points at, and screenshots every step. It never saves.
    public class ShotRunner : MonoBehaviour
    {
        string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var dir = Arg("-kt-shots");
            if (dir == null) return;
            var go = new GameObject("ShotRunner");
            DontDestroyOnLoad(go);
            go.AddComponent<ShotRunner>().dir = dir;
        }

        public static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            // Each run starts like a first launch (test builds have their own PlayerPrefs).
            PlayerPrefs.DeleteKey("kt_save_v1");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("01_title");

            var gc = FindAnyObjectByType<GameController>();
            var ui = gc.GetComponent<GameUi>();
            if (Arg("-kt-scenario") == "tutorial") { yield return Tutorial(gc, ui); Application.Quit(); yield break; }
            gc.StartGame(false, 0, "pota");
            // Two upgrades so they show on the thela, and a stamp the first time a daily goal completes.
            gc.Sim.BuyUpgrade("stools"); gc.Sim.BuyUpgrade("tubelight");
            gc.Sim.GoalDone += _ => stampDue = true;
            yield return Advance(gc, 1060);
            gc.Toast("Uncle Rasheed: \"Chai mein elaichi kam hai, beta. Dada ki tarah banao.\"", "uncle");
            gc.Toast("Almost out of Milk. Buy more in Supplies.", "warn");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("02_hud");
            var uiRoot = ui.GetComponentInChildren<UIDocument>().rootVisualElement;
            if (uiRoot.Q("top-goals") != null) { yield return Click(uiRoot, uiRoot.Q("top-goals")); yield return new WaitForSecondsRealtime(0.4f); yield return Shot("02b_goals"); yield return Click(uiRoot, uiRoot.Q("top-goals")); }
            // The thela up close, with its upgrades, in the evening light.
            float vx = gc.Rig.ViewX, vw = gc.Rig.ViewWidth;
            gc.Rig.ViewX = gc.Data.District.Pitch.X; gc.Rig.ViewWidth = gc.Rig.MinWidth;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("06_thela");
            gc.Rig.ViewX = vx; gc.Rig.ViewWidth = vw;
            yield return InputTest(gc, ui);
            // Story: a choice card, a recipe page, and the Kitaab album.
            gc.Sim.DrawCardNow("constable");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("07_choice");
            if (uiRoot.Q("choice-0") != null) { yield return Click(uiRoot, uiRoot.Q("choice-0")); yield return new WaitForSecondsRealtime(0.5f); yield return Shot("07b_choice_result"); }
            ui.DebugShow("none");
            gc.Sim.FindPage("elaichi_chai");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("08_page");
            ui.DebugShow("none");
            ui.DebugShow("kitaab");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("09_kitaab");
            foreach (var tab in new[] { "Menu", "Supplies", "Staff", "Shop", "Store", "settings", "act1" })
            {
                ui.DebugShow(tab);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("03_" + tab.ToLowerInvariant());
            }
            ui.DebugShow("none");
            yield return Advance(gc, 99999);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("04_ledger");
            Application.Quit();
        }

        IEnumerator Tutorial(GameController gc, GameUi ui)
        {
            var root = ui.GetComponentInChildren<UIDocument>().rootVisualElement;
            var tutor = gc.GetComponent<TutorialDirector>();
            yield return Click(root, root.Query<Button>(className: "seg-btn").Where(b => b.text.StartsWith("With Uncle")).First());
            yield return Click(root, root.Query<Button>(className: "btn-primary").Where(b => b.text.StartsWith("Start")).First());
            for (int card = 1; card <= 3; card++)
            {
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Shot($"t00_coldopen_{card}");
                yield return Click(root, root.Q(className: "coldopen-card").Q<Button>(className: "btn-primary"));
            }
            float start = Time.realtimeSinceStartup;
            string last = null; int n = 0, guard = 0;
            while (tutor.Active && guard++ < 3000)
            {
                var st = tutor.Current;
                if (st == null) break;
                bool showing = !st.Hidden && ui.CoachVisible;
                if (st.Id != last && showing)
                {
                    last = st.Id; n++;
                    yield return new WaitForSecondsRealtime(0.7f);
                    yield return Shot($"t{n:00}_{st.Id}");
                }
                if (showing && st.Button != null) yield return Click(root, root.Q(className: "coach-card").Q<Button>(className: "btn-primary"));
                else if (showing && st.Target != null) yield return Click(root, root.Q(st.Target));
                else
                {
                    // Let the day run (quickly) until the next lesson.
                    for (int i = 0; i < 12 && !ui.Blocking && !gc.Sim.S.DayOver; i++) gc.Sim.Tick(0.5f);
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(showing ? 0.4f : 0.02f);
            }
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("t99_done");
            // Day 2: the Staff panel unlocks with an explainer card.
            yield return Click(root, root.Query<Button>(className: "btn-primary").Where(b => b.text.StartsWith("Agla")).First());
            for (int i = 0; i < 30 && !ui.CoachVisible; i++) yield return new WaitForSecondsRealtime(0.1f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("t100_day2_unlock");
            if (ui.CoachVisible) yield return Click(root, root.Q(className: "coach-card").Q<Button>(className: "btn-primary"));
            // Day 2 also brings Uncle's first request.
            for (int i = 0; i < 30 && !ui.CoachVisible; i++) yield return new WaitForSecondsRealtime(0.1f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("t101_quest");
            if (ui.CoachVisible) yield return Click(root, root.Q(className: "coach-card").Q<Button>(className: "btn-primary"));
            ui.DebugShow("kitaab");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("t102_kitaab");
            Debug.Log($"[ShotRunner] tutorial: finished={!tutor.Active} steps={n} real seconds={Time.realtimeSinceStartup - start:F0} day={gc.Sim.S.Day} served={gc.Sim.S.TotalServed} cash={gc.Sim.S.Cash}");
        }

        // A synthetic mouse click in the middle of an element.
        IEnumerator Click(VisualElement root, VisualElement target)
        {
            if (target == null) { Debug.Log("[ShotRunner] click target missing"); yield break; }
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            float k = Screen.width / root.layout.width;
            var c = target.worldBound.center;
            var pos = new Vector2(c.x * k, Screen.height - c.y * k);
            foreach (var down in new[] { false, true, false })
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = (ushort)(down ? 1 : 0) });
                yield return null; yield return null;
            }
        }

        // Clicks the Menu button and drags the street with synthetic mouse events, then checks the effect.
        IEnumerator InputTest(GameController gc, GameUi ui)
        {
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            var root = ui.GetComponentInChildren<UIDocument>().rootVisualElement;
            float k = Screen.width / root.layout.width;
            Vector2 ToScreen(Vector2 panel) => new Vector2(panel.x * k, Screen.height - panel.y * k);
            IEnumerator Send(Vector2 pos, bool down) { InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = (ushort)(down ? 1 : 0) }); yield return null; yield return null; }

            var menu = root.Query<Button>(className: "dock-btn").First();
            var at = ToScreen(menu.worldBound.center);
            yield return Send(at, false); yield return Send(at, true); yield return Send(at, false);
            bool opened = root.Q(className: "sheet").ClassListContains("open");
            ui.DebugShow("none"); // on phones the open sheet covers the dock; players close it with x or a tap on the street
            yield return new WaitForSecondsRealtime(0.5f); // let the sheet slide away
            float x0 = gc.Rig.ViewX;
            var from = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            yield return Send(from, false); yield return Send(from, true);
            for (int i = 1; i <= 6; i++) yield return Send(from + new Vector2(-40 * i * k, 0), true);
            yield return Send(from + new Vector2(-240 * k, 0), false);
            Debug.Log($"[ShotRunner] input: menu click opened sheet={opened}, drag moved camera {x0:F2} -> {gc.Rig.ViewX:F2}");
        }

        // Runs the simulation quickly up to a game minute (or the end of the day), a chunk per frame.
        bool stampDue, stampShot;

        IEnumerator Advance(GameController gc, float minute)
        {
            while (!gc.Sim.S.DayOver && gc.Sim.S.Minute < minute)
            {
                for (int i = 0; i < 40 && !gc.Sim.S.DayOver; i++) gc.Sim.Tick(0.5f);
                yield return null;
                if (stampDue && !stampShot) { stampShot = true; yield return new WaitForSecondsRealtime(0.35f); yield return Shot("05_goal_stamp"); }
            }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return new WaitForSecondsRealtime(0.25f);
            Debug.Log("[ShotRunner] " + name);
        }
    }
}
