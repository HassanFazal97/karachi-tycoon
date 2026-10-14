using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KarachiTycoon.Game
{
    // Small helpers so views read like a layout description. Every view is built in code
    // (no UXML) and styled by Resources/UI/Main.uss.
    public static class UiKit
    {
        // Raised on every button press so the audio director can click without each view knowing about it.
        public static event Action Clicked;

        public static VisualElement Div(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        // Buttons never take keyboard focus: Space must pause the game, not press the last button clicked.
        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(() => { Clicked?.Invoke(); onClick?.Invoke(); }) { text = text, focusable = false };
            b.AddToClassList("btn");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static Button IconBtn(string icon, string text, Action onClick, params string[] classes)
        {
            var b = Btn("", onClick, classes);
            b.AddToClassList("icon-btn");
            b.Add(new Icon(icon));
            if (!string.IsNullOrEmpty(text)) b.Add(Text(text, "icon-btn-label"));
            return b;
        }

        public static T With<T>(this T e, params VisualElement[] children) where T : VisualElement
        {
            foreach (var c in children) if (c != null) e.Add(c);
            return e;
        }

        public static T Cls<T>(this T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        // Lets touches fall through to the street (layout containers, spacers, toasts).
        public static T Ghost<T>(this T e, bool deep = false) where T : VisualElement
        {
            e.pickingMode = PickingMode.Ignore;
            if (deep) foreach (var c in e.Children()) c.Ghost(true);
            return e;
        }

        public static T Show<T>(this T e, bool on) where T : VisualElement
        {
            e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            return e;
        }

        // A horizontal progress bar; Set(0..1).
        public static (VisualElement bar, VisualElement fill) Bar(params string[] classes)
        {
            var bar = Div("bar"); foreach (var c in classes) bar.AddToClassList(c);
            var fill = Div("bar-fill");
            bar.Add(fill);
            return (bar, fill);
        }

        public static void SetFill(VisualElement fill, float t) => fill.style.width = Length.Percent(Mathf.Clamp01(t) * 100);

        // "Rs 12.3k", "Rs 3.0 lakh": compact money for tight HUD space.
        public static string RsShort(int n)
        {
            string sign = n < 0 ? "-" : ""; n = Math.Abs(n);
            if (n < 1000) return $"{sign}Rs {n}";
            if (n < 100000) return $"{sign}Rs {n / 1000f:0.#}k";
            return $"{sign}Rs {n / 100000f:0.##} lakh";
        }
    }

    // Live bindings: each frame, re-evaluate and write only what changed.
    public class Binder
    {
        readonly List<Action> updates = new List<Action>();

        public void Text(TextElement l, Func<string> f)
        {
            string last = null;
            updates.Add(() => { var v = f(); if (v != last) { last = v; l.text = v; } });
        }

        public void Enabled(VisualElement e, Func<bool> f)
        {
            int last = -1;
            updates.Add(() => { int v = f() ? 1 : 0; if (v != last) { last = v; e.SetEnabled(v == 1); } });
        }

        public void Visible(VisualElement e, Func<bool> f)
        {
            int last = -1;
            updates.Add(() => { int v = f() ? 1 : 0; if (v != last) { last = v; e.Show(v == 1); } });
        }

        public void Class(VisualElement e, string cls, Func<bool> f)
        {
            int last = -1;
            updates.Add(() => { int v = f() ? 1 : 0; if (v != last) { last = v; e.EnableInClassList(cls, v == 1); } });
        }

        public void Fill(VisualElement fill, Func<float> f)
        {
            float last = -1;
            updates.Add(() => { float v = Mathf.Round(Mathf.Clamp01(f()) * 400) / 400; if (v != last) { last = v; UiKit.SetFill(fill, v); } });
        }

        public void Do(Action a) => updates.Add(a);

        public void Update() { foreach (var u in updates) u(); }
        public void Clear() => updates.Clear();
    }
}
