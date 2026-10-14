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
    // It plays a new game for a while, opens every panel, ends the day and quits. It never saves.
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
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("01_title");

            var gc = FindAnyObjectByType<GameController>();
            var ui = gc.GetComponent<GameUi>();
            gc.StartGame(false, 0, "pota");
            yield return Advance(gc, 1060);
            gc.Toast("Uncle Rasheed: \"Chai mein elaichi kam hai, beta. Dada ki tarah banao.\"", "uncle");
            gc.Toast("Almost out of Milk. Buy more in Supplies.", "warn");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("02_hud");
            yield return InputTest(gc, ui);
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
            yield return Send(at, false); yield return Send(at, true); yield return Send(at, false);

            float x0 = gc.Rig.ViewX;
            var from = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            yield return Send(from, false); yield return Send(from, true);
            for (int i = 1; i <= 6; i++) yield return Send(from + new Vector2(-40 * i * k, 0), true);
            yield return Send(from + new Vector2(-240 * k, 0), false);
            Debug.Log($"[ShotRunner] input: menu click opened sheet={opened}, drag moved camera {x0:F2} -> {gc.Rig.ViewX:F2}");
        }

        // Runs the simulation quickly up to a game minute (or the end of the day), a chunk per frame.
        IEnumerator Advance(GameController gc, float minute)
        {
            while (!gc.Sim.S.DayOver && gc.Sim.S.Minute < minute)
            {
                for (int i = 0; i < 40 && !gc.Sim.S.DayOver; i++) gc.Sim.Tick(0.5f);
                yield return null;
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
