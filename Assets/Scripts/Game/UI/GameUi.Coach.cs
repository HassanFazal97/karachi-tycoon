using System;
using UnityEngine;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.UiKit;

namespace KarachiTycoon.Game
{
    // Uncle Rasheed's coaching overlay: a card with his words, a pulsing ring around the thing to
    // touch, and (for lessons that pause the game) a dimmed screen with a hole over that thing, so
    // the only button that works is the one being taught. Also the cold open and Dada ki Kitaab.
    public partial class GameUi
    {
        VisualElement coachLayer, coachCard, coachRow, coachRing;
        readonly VisualElement[] dims = new VisualElement[4];
        string coachTarget;
        bool coachPause;
        TutorialDirector tutor;

        public bool CoachVisible => coachCard != null;
        public string SheetOpen => sheetOpen;
        public string ModalId => modal;
        public void CloseSheetNow() => CloseSheet();
        public bool TabUnlocked(string tab) => tutor == null || tutor.Unlocked(tab);

        public void ShowCoach(string title, string text, string target, bool pause, string button, Action onNext, Action onSkip)
        {
            HideCoach();
            coachTarget = target; coachPause = pause;
            for (int i = 0; i < 4; i++)
            {
                dims[i] = Div("coach-dim");
                dims[i].pickingMode = pause ? PickingMode.Position : PickingMode.Ignore;
                dims[i].Show(pause);
                coachLayer.Add(dims[i]);
            }
            coachRing = Div("coach-ring").Ghost();
            coachLayer.Add(coachRing);
            coachRing.schedule.Execute(() => coachRing?.ToggleInClassList("pulse")).Every(550);

            coachCard = Div("coach-card");
            coachCard.Add(Div("row").With(Div("portrait").With(new Icon("staff")),
                Div("col", "grow").With(Text("Uncle Rasheed", "coach-who"), title != null ? Text(title, "coach-title") : null)));
            coachCard.Add(Text(text, "coach-text"));
            var actions = Div("row", "coach-actions");
            if (onSkip != null) actions.Add(Btn("Skip tutorial", onSkip, "btn-link"));
            actions.Add(Div("grow"));
            if (button != null) actions.Add(Btn(button, onNext, "btn-primary"));
            if (actions.childCount > 1) coachCard.Add(actions);
            coachRow = Div("coach-row").Ghost().With(coachCard);
            coachLayer.Add(coachRow);
            coachCard.schedule.Execute(() => coachCard?.AddToClassList("in")).StartingIn(16);
            UpdateCoach();
        }

        public void HideCoach()
        {
            coachLayer?.Clear();
            coachCard = coachRow = coachRing = null;
            for (int i = 0; i < 4; i++) dims[i] = null;
        }

        void UpdateCoach()
        {
            if (coachCard == null) return;
            float w = coachLayer.layout.width, h = coachLayer.layout.height;
            if (float.IsNaN(w) || w <= 0) return;
            Rect hole = default; bool has = false;
            if (coachTarget != null)
            {
                var t = root.Q(coachTarget);
                if (t != null && t.resolvedStyle.display != DisplayStyle.None && t.worldBound.width > 0)
                {
                    var r = coachLayer.WorldToLocal(t.worldBound);
                    hole = new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12);
                    has = true;
                }
            }
            void Place(VisualElement e, float x, float y, float ww, float hh)
            {
                e.style.left = x; e.style.top = y; e.style.width = Mathf.Max(0, ww); e.style.height = Mathf.Max(0, hh);
            }
            if (!has)
            {
                Place(dims[0], 0, 0, w, h);
                for (int i = 1; i < 4; i++) dims[i].Show(false);
                coachRing.Show(false);
            }
            else
            {
                Place(dims[0], 0, 0, w, hole.yMin);
                Place(dims[1], 0, hole.yMax, w, h - hole.yMax);
                Place(dims[2], 0, hole.yMin, hole.xMin, hole.height);
                Place(dims[3], hole.xMax, hole.yMin, w - hole.xMax, hole.height);
                for (int i = 1; i < 4; i++) dims[i].Show(coachPause);
                coachRing.Show(true);
                Place(coachRing, hole.x, hole.y, hole.width, hole.height);
            }
            // The card sits on the far side of the screen from what it points at.
            bool top = has && hole.center.y > h * 0.5f;
            coachRow.EnableInClassList("top", top);
            coachRow.EnableInClassList("bottom", !top);
        }

        // ---------- hints with a shortcut ----------
        public void UncleHint(string text, string panel)
        {
            Toast(text, "uncle", panel);
        }

        // ---------- cold open ----------
        void OpenColdOpen(Action then)
        {
            var cards = tutor.Data.ColdOpen;
            int i = 0;
            var screen = Div("coldopen");
            var icon = new Icon("book").Cls("coldopen-icon");
            var title = Text("", "coldopen-title");
            var text = Text("", "coldopen-text");
            var dots = Div("row", "coldopen-dots");
            var next = Btn("", null, "btn-primary", "btn-big");
            var card = Div("coldopen-card").With(icon, title, text, dots,
                Div("row", "coldopen-actions").With(Btn("Skip", () => Done(true), "btn-link"), Div("grow"), next));
            screen.Add(card);
            modalLayer.Add(screen);
            modal = "coldopen";
            void Show()
            {
                icon.Set(cards[i].Icon); title.text = cards[i].Title; text.text = cards[i].Text;
                next.text = i == cards.Count - 1 ? "Begin" : "Next";
                dots.Clear();
                for (int k = 0; k < cards.Count; k++) dots.Add(Div("dot").Cls(k == i ? "on" : "off"));
                card.RemoveFromClassList("in");
                card.schedule.Execute(() => card.AddToClassList("in")).StartingIn(30);
            }
            void Done(bool skipped)
            {
                gc.Analytics.Event("cold_open", ("skipped", skipped), ("card", i));
                modal = null;
                screen.RemoveFromHierarchy();
                then();
            }
            next.clicked += () => { if (i < cards.Count - 1) { i++; Show(); } else Done(false); };
            Show();
        }
    }
}
