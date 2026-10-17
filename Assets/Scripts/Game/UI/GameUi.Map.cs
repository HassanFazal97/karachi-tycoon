using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static KarachiTycoon.Game.UiKit;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // The map, at two zoom levels: the Burns Road district view (the camera pulls back over the whole
    // food street, with pins for your place, the next rung of the ladder and the rivals) and the
    // painted Karachi city map with the districts that later chapters open.
    public partial class GameUi
    {
        VisualElement pinLayer, mapBar;
        readonly List<(VisualElement el, Func<Vector3> world)> pins = new List<(VisualElement, Func<Vector3>)>();
        readonly Binder mapBind = new Binder();
        public bool DistrictOpen => gc.Rig.District;

        Vector3 TierSpot(string tier)
        {
            var p = gc.Data.District.Pitch; float hx = gc.Street.HomeX, hh = gc.Street.HomeH;
            switch (tier)
            {
                case "cart": return W(p.X, 3.0f, p.Z);
                case "cart2": return W(p.X + 1.8f, 3.2f, p.Z);
                case "dhaba": return W(3.6f, 2.6f, -4.9f);
                case "dukaan": return W(hx - 2, 4.6f, -6f);
                case "restaurant": return W(hx, 7.6f, -6f);
                default: return W(hx, hh + 2.6f, -6f);
            }
        }

        void ToggleDistrict() { if (DistrictOpen) CloseDistrict(); else OpenDistrict(); }

        void OpenDistrict()
        {
            CloseSheet(); CloseModal();
            gc.Rig.SetDistrictView(true, phone);
            root.AddToClassList("district");
            pinLayer = Div("layer").Ghost();
            hud.Add(pinLayer);
            pins.Clear(); mapBind.Clear();

            // Your place, then every rung above it.
            for (int i = 0; i < sim.Tiers.Count; i++)
            {
                int k = i; var t = sim.Tiers[i];
                var pin = Div("pin");
                var title = Text("", "pin-title"); var sub = Text("", "pin-sub");
                pin.With(title, sub);
                pin.RegisterCallback<ClickEvent>(_ => { if (k > sim.TierIndex) { CloseDistrict(); OpenSheet("Shop"); } });
                pinLayer.Add(pin);
                pins.Add((pin, () => TierSpot(t.Id)));
                mapBind.Do(() =>
                {
                    int now = sim.TierIndex;
                    bool show = k == now || k > now && (k == now + 1 || TierSpot(t.Id) != TierSpot(sim.Tiers[k - 1].Id));
                    pin.Show(show && (k >= now));
                    pin.EnableInClassList("pin-you", k == now);
                    pin.EnableInClassList("pin-next", k == now + 1);
                    pin.EnableInClassList("pin-locked", k > now + 1);
                    title.text = k == now ? "YOURS · " + t.Name : t.Name;
                    if (k == now) sub.text = $"{sim.Stage.Seats} seats · {sim.StaffCount} staff";
                    else if (k == now + 1)
                    {
                        var need = sim.TierNeeds().FirstOrDefault(n => !n.met);
                        sub.text = need.text == null ? $"Ready · {RsShort(t.SetupCost)} · tap to build" : $"{RsShort(t.SetupCost)} · needs {need.text.Split('(')[0].Trim()}";
                    }
                    else sub.text = $"Locked · {RsShort(t.SetupCost)}";
                });
            }
            // The neighbours you compete with.
            foreach (var r in gc.Data.District.Rivals)
            {
                var pin = Div("pin", "pin-rival").With(Text(r.En, "pin-title"), Text(r.Kind == "deg" ? "Rival · nihari deg" : "Rival · charcoal grill", "pin-sub")).Ghost(true);
                pinLayer.Add(pin);
                var pos = W(r.X, 3.2f, r.Z);
                pins.Add((pin, () => pos));
            }
            // The road goes on to the rest of Karachi.
            var end = Div("pin", "pin-end").With(Text("SADDAR →", "pin-title"), Text("Next chapter · see the city map", "pin-sub"));
            end.RegisterCallback<ClickEvent>(_ => OpenCity());
            pinLayer.Add(end);
            pins.Add((end, () => W(30, 3.5f, 0.5f)));

            mapBar = Div("map-bar").With(
                IconBtn("close", "Back to the street", CloseDistrict, "btn-big"),
                Div("grow").Ghost(),
                IconBtn("map", "Karachi map", OpenCity, "btn-primary", "btn-big"));
            mapBar.Ghost();
            foreach (var b in mapBar.Query<Button>().ToList()) b.pickingMode = PickingMode.Position;
            hud.Add(mapBar);
            gc.Analytics.Event("map_open", ("view", "district"), ("tier", sim.S.Stage));
        }

        void CloseDistrict()
        {
            if (!DistrictOpen) return;
            gc.Rig.SetDistrictView(false, phone);
            root.RemoveFromClassList("district");
            pinLayer?.RemoveFromHierarchy(); mapBar?.RemoveFromHierarchy();
            pins.Clear(); mapBind.Clear();
        }

        // Pins follow their spot in the world (works for headless screenshots too).
        void UpdatePins()
        {
            if (!DistrictOpen || pinLayer == null) return;
            mapBind.Update();
            var cam = gc.Rig.Cam;
            float w = pinLayer.layout.width, h = pinLayer.layout.height;
            if (float.IsNaN(w) || w <= 0) return;
            float kx = w / cam.pixelWidth, ky = h / cam.pixelHeight;
            foreach (var (el, world) in pins)
            {
                var sp = cam.WorldToScreenPoint(world());
                el.style.left = sp.x * kx; el.style.top = (cam.pixelHeight - sp.y) * ky;
                el.style.visibility = sp.z > 0 && sp.x > -50 && sp.x < cam.pixelWidth + 50 ? Visibility.Visible : Visibility.Hidden;
            }
        }

        // ---------- the Karachi city map ----------
        void OpenCity()
        {
            var box = OpenModal("city", "city-card-box");
            box.parent.AddToClassList("city-backdrop");
            box.Add(Div("row", "city-head").With(new Icon("map").Cls("kitaab-icon"), Text("Karachi", "story-title", "grow"), IconBtn("close", null, CloseModal, "close-btn")));
            box.Add(Text("Your story starts on Burns Road. Each district is a new chapter, with its own kind of business.", "muted", "small"));
            var map = new CityMap().Cls("city-map");
            box.Add(map);
            var info = Div("card", "city-info");
            var name = Text("", "h2"); var format = Text("", "muted", "small"); var status = Text("", "body");
            var go = Btn("See Burns Road", () => { CloseModal(); OpenDistrict(); }, "btn-primary", "wide");
            info.With(name, format, status, go);
            box.Add(info);
            void Pick(KarachiTycoon.Sim.CityDistrictDef d)
            {
                name.text = d.Name; format.text = d.Format;
                status.text = d.Open ? $"Your home · {sim.Stage.Name}" : "Locked · " + d.Unlock;
                go.Show(d.Open);
                foreach (var dot in map.Query(className: "city-dot").ToList()) dot.EnableInClassList("picked", dot.name == "city-" + d.Id);
                gc.Analytics.Event("district_tap", ("district", d.Id));
            }
            foreach (var d in gc.Data.Business.Districts)
            {
                var dd = d;
                var dot = Btn("", () => Pick(dd), "city-dot").Named("city-" + d.Id);
                dot.EnableInClassList("open", d.Open);
                dot.EnableInClassList("locked", !d.Open);
                dot.Add(Div("city-dot-mark").With(new Icon(d.Open ? "target" : "lock").Cls("city-dot-icon")));
                dot.Add(Text(d.Name, "city-name"));
                dot.style.left = Length.Percent(d.X * 100); dot.style.top = Length.Percent(d.Y * 100);
                map.Add(dot);
            }
            Pick(gc.Data.Business.Districts.First());
            gc.Analytics.Event("map_open", ("view", "city"));
        }
    }

    // A painted Karachi: the Arabian Sea, the coastline and harbour, the Lyari and Malir rivers, main roads.
    public class CityMap : Drawn
    {
        public CityMap() { pickingMode = PickingMode.Position; }

        protected override void Paint(Painter2D p, Rect r)
        {
            Vector2 P(float x, float y) => new Vector2(r.x + x * r.width, r.y + y * r.height);
            p.fillColor = new Color32(0xE9, 0xD9, 0xB5, 0xFF);
            Poly(p, P(0, 0), P(1, 0), P(1, 1), P(0, 1)); p.Fill();
            // Old city blocks: a faint grid.
            p.strokeColor = new Color(0.55f, 0.42f, 0.28f, 0.12f); p.lineWidth = 1;
            for (int i = 1; i < 12; i++) { p.BeginPath(); p.MoveTo(P(i / 12f, 0)); p.LineTo(P(i / 12f, 1)); p.Stroke(); p.BeginPath(); p.MoveTo(P(0, i / 12f)); p.LineTo(P(1, i / 12f)); p.Stroke(); }
            // The sea and the coast.
            p.fillColor = new Color32(0x6F, 0xA8, 0xB8, 0xFF);
            p.BeginPath(); p.MoveTo(P(0, 0.52f));
            p.BezierCurveTo(P(0.12f, 0.6f), P(0.22f, 0.72f), P(0.28f, 0.74f));
            p.BezierCurveTo(P(0.33f, 0.76f), P(0.36f, 0.82f), P(0.46f, 0.9f));
            p.BezierCurveTo(P(0.58f, 0.95f), P(0.74f, 0.98f), P(1, 0.99f));
            p.LineTo(P(1, 1)); p.LineTo(P(0, 1)); p.ClosePath(); p.Fill();
            p.strokeColor = new Color32(0xF4, 0xEC, 0xD6, 0xFF); p.lineWidth = 3;
            p.BeginPath(); p.MoveTo(P(0, 0.52f));
            p.BezierCurveTo(P(0.12f, 0.6f), P(0.22f, 0.72f), P(0.28f, 0.74f));
            p.BezierCurveTo(P(0.33f, 0.76f), P(0.36f, 0.82f), P(0.46f, 0.9f));
            p.BezierCurveTo(P(0.58f, 0.95f), P(0.74f, 0.98f), P(1, 0.99f)); p.Stroke();
            // Rivers.
            p.strokeColor = new Color32(0x6F, 0xA8, 0xB8, 0xCC); p.lineWidth = 4;
            p.BeginPath(); p.MoveTo(P(0.15f, 0.02f)); p.BezierCurveTo(P(0.22f, 0.3f), P(0.18f, 0.5f), P(0.27f, 0.73f)); p.Stroke();
            p.BeginPath(); p.MoveTo(P(1, 0.22f)); p.BezierCurveTo(P(0.82f, 0.45f), P(0.78f, 0.7f), P(0.66f, 0.93f)); p.Stroke();
            // Main roads.
            p.strokeColor = new Color(0.98f, 0.95f, 0.88f, 0.9f); p.lineWidth = 5;
            p.BeginPath(); p.MoveTo(P(0.38f, 0.64f)); p.BezierCurveTo(P(0.55f, 0.55f), P(0.75f, 0.45f), P(0.98f, 0.4f)); p.Stroke(); // Shahrah-e-Faisal
            p.BeginPath(); p.MoveTo(P(0.4f, 0.62f)); p.BezierCurveTo(P(0.5f, 0.45f), P(0.62f, 0.3f), P(0.8f, 0.1f)); p.Stroke();      // University Road
            p.BeginPath(); p.MoveTo(P(0.42f, 0.66f)); p.BezierCurveTo(P(0.44f, 0.74f), P(0.5f, 0.82f), P(0.62f, 0.86f)); p.Stroke(); // to Clifton
        }
    }
}
