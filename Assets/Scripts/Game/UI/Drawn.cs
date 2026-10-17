using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KarachiTycoon.Game
{
    // Vector-drawn UI pieces (no texture assets needed): icons, star ratings, progress rings,
    // the day clock and truck-art bunting.

    public abstract class Drawn : VisualElement
    {
        protected Drawn()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx => { var r = contentRect; if (r.width > 0 && r.height > 0) Paint(ctx.painter2D, r); };
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }
        protected abstract void Paint(Painter2D p, Rect r);

        protected static void Poly(Painter2D p, params Vector2[] pts)
        {
            p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
        }

        protected static void Circle(Painter2D p, Vector2 c, float radius)
        {
            p.BeginPath(); p.Arc(c, radius, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath();
        }

        protected static Vector2[] StarPoints(Vector2 c, float outer)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (-90 + i * 36) * Mathf.Deg2Rad, rad = i % 2 == 0 ? outer : outer * 0.45f;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            }
            return pts;
        }
    }

    // Simple line icons in the element's text colour.
    public class Icon : Drawn
    {
        string kind;
        public Icon(string kind) { this.kind = kind; AddToClassList("icon"); }
        public void Set(string k) { kind = k; MarkDirtyRepaint(); }

        protected override void Paint(Painter2D p, Rect r)
        {
            float s = Mathf.Min(r.width, r.height);
            var o = new Vector2(r.x + (r.width - s) / 2, r.y + (r.height - s) / 2);
            Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
            var col = resolvedStyle.color;
            p.strokeColor = col; p.fillColor = col;
            p.lineWidth = Mathf.Max(1.5f, s * 0.09f); p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;

            switch (kind)
            {
                case "menu": // a menu board
                    Poly(p, P(.18f, .12f), P(.82f, .12f), P(.82f, .88f), P(.18f, .88f)); p.Stroke();
                    for (int i = 0; i < 3; i++) { p.BeginPath(); p.MoveTo(P(.32f, .34f + i * .17f)); p.LineTo(P(.68f, .34f + i * .17f)); p.Stroke(); }
                    break;
                case "supplies": // a crate
                    Poly(p, P(.14f, .3f), P(.86f, .3f), P(.86f, .86f), P(.14f, .86f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.1f, .3f)); p.LineTo(P(.26f, .14f)); p.LineTo(P(.74f, .14f)); p.LineTo(P(.9f, .3f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.38f, .3f)); p.LineTo(P(.38f, .86f)); p.MoveTo(P(.62f, .3f)); p.LineTo(P(.62f, .86f)); p.Stroke();
                    break;
                case "staff": // a person
                    Circle(p, P(.5f, .3f), s * .16f); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.18f, .9f)); p.BezierCurveTo(P(.18f, .55f), P(.82f, .55f), P(.82f, .9f)); p.Stroke();
                    break;
                case "shop": // a shopfront with a scalloped awning
                    Poly(p, P(.18f, .45f), P(.82f, .45f), P(.82f, .88f), P(.18f, .88f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.1f, .3f)); p.LineTo(P(.2f, .12f)); p.LineTo(P(.8f, .12f)); p.LineTo(P(.9f, .3f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.1f, .3f));
                    for (int i = 0; i < 4; i++) p.Arc(P(.2f + i * .2f, .3f), s * .1f, Angle.Degrees(180), Angle.Degrees(0), ArcDirection.CounterClockwise);
                    p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.42f, .88f)); p.LineTo(P(.42f, .62f)); p.LineTo(P(.58f, .62f)); p.LineTo(P(.58f, .88f)); p.Stroke();
                    break;
                case "store": // a silver coin
                    Circle(p, P(.5f, .5f), s * .36f); p.Stroke();
                    Circle(p, P(.5f, .5f), s * .2f); p.Stroke();
                    break;
                case "coin":
                    Circle(p, P(.5f, .5f), s * .42f); p.Fill();
                    p.strokeColor = new Color(0, 0, 0, .3f); Circle(p, P(.5f, .5f), s * .26f); p.Stroke();
                    break;
                case "settings": // a gear
                    Circle(p, P(.5f, .5f), s * .2f); p.Stroke();
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45 * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        p.BeginPath(); p.MoveTo(P(.5f, .5f) + d * s * .3f); p.LineTo(P(.5f, .5f) + d * s * .42f); p.Stroke();
                    }
                    Circle(p, P(.5f, .5f), s * .3f); p.Stroke();
                    break;
                case "pause":
                    Poly(p, P(.28f, .2f), P(.42f, .2f), P(.42f, .8f), P(.28f, .8f)); p.Fill();
                    Poly(p, P(.58f, .2f), P(.72f, .2f), P(.72f, .8f), P(.58f, .8f)); p.Fill();
                    break;
                case "play": Poly(p, P(.3f, .18f), P(.8f, .5f), P(.3f, .82f)); p.Fill(); break;
                case "fast":
                    Poly(p, P(.12f, .22f), P(.5f, .5f), P(.12f, .78f)); p.Fill();
                    Poly(p, P(.5f, .22f), P(.88f, .5f), P(.5f, .78f)); p.Fill();
                    break;
                case "fastest":
                    for (int i = 0; i < 3; i++) { Poly(p, P(.06f + i * .29f, .25f), P(.35f + i * .29f, .5f), P(.06f + i * .29f, .75f)); p.Fill(); }
                    break;
                case "shout": // a megaphone with sound lines
                    Poly(p, P(.12f, .4f), P(.3f, .4f), P(.62f, .18f), P(.62f, .82f), P(.3f, .6f), P(.12f, .6f)); p.Fill();
                    p.BeginPath(); p.Arc(P(.62f, .5f), s * .16f, Angle.Degrees(-50), Angle.Degrees(50)); p.Stroke();
                    p.BeginPath(); p.Arc(P(.62f, .5f), s * .3f, Angle.Degrees(-50), Angle.Degrees(50)); p.Stroke();
                    break;
                case "save": // arrow into a tray
                    p.BeginPath(); p.MoveTo(P(.5f, .12f)); p.LineTo(P(.5f, .6f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.32f, .44f)); p.LineTo(P(.5f, .62f)); p.LineTo(P(.68f, .44f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.14f, .6f)); p.LineTo(P(.14f, .86f)); p.LineTo(P(.86f, .86f)); p.LineTo(P(.86f, .6f)); p.Stroke();
                    break;
                case "close":
                    p.BeginPath(); p.MoveTo(P(.25f, .25f)); p.LineTo(P(.75f, .75f)); p.MoveTo(P(.75f, .25f)); p.LineTo(P(.25f, .75f)); p.Stroke();
                    break;
                case "ad": // a small TV with a play mark
                    Poly(p, P(.1f, .22f), P(.9f, .22f), P(.9f, .78f), P(.1f, .78f)); p.Stroke();
                    Poly(p, P(.42f, .36f), P(.62f, .5f), P(.42f, .64f)); p.Fill();
                    break;
                case "lock":
                    Poly(p, P(.22f, .45f), P(.78f, .45f), P(.78f, .88f), P(.22f, .88f)); p.Fill();
                    p.BeginPath(); p.Arc(P(.5f, .45f), s * .2f, Angle.Degrees(180), Angle.Degrees(360)); p.Stroke();
                    break;
                case "bolt": // load shedding
                    Poly(p, P(.58f, .08f), P(.24f, .56f), P(.48f, .56f), P(.4f, .92f), P(.76f, .42f), P(.52f, .42f)); p.Fill();
                    break;
                case "chai": // a cutting-chai glass with steam
                    Poly(p, P(.26f, .4f), P(.74f, .4f), P(.66f, .9f), P(.34f, .9f)); p.Stroke();
                    for (int i = 0; i < 2; i++)
                    {
                        float x = .42f + i * .16f;
                        p.BeginPath(); p.MoveTo(P(x, .3f)); p.BezierCurveTo(P(x - .08f, .22f), P(x + .08f, .16f), P(x, .08f)); p.Stroke();
                    }
                    break;
                case "book": // Dada's recipe book
                    Poly(p, P(.5f, .22f), P(.86f, .14f), P(.86f, .8f), P(.5f, .88f)); p.Stroke();
                    Poly(p, P(.5f, .22f), P(.14f, .14f), P(.14f, .8f), P(.5f, .88f)); p.Stroke();
                    for (int i = 0; i < 3; i++) { p.BeginPath(); p.MoveTo(P(.6f, .34f + i * .14f)); p.LineTo(P(.76f, .31f + i * .14f)); p.Stroke(); }
                    break;
                case "thela": // a handcart with an awning
                    Poly(p, P(.12f, .5f), P(.8f, .5f), P(.8f, .66f), P(.12f, .66f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.2f, .5f)); p.LineTo(P(.2f, .26f)); p.MoveTo(P(.72f, .5f)); p.LineTo(P(.72f, .26f)); p.Stroke();
                    Poly(p, P(.1f, .26f), P(.82f, .26f), P(.76f, .16f), P(.16f, .16f)); p.Fill();
                    p.BeginPath(); p.MoveTo(P(.8f, .58f)); p.LineTo(P(.94f, .5f)); p.Stroke();
                    Circle(p, P(.3f, .78f), s * .1f); p.Stroke();
                    Circle(p, P(.64f, .78f), s * .1f); p.Stroke();
                    break;
                case "check":
                    p.BeginPath(); p.MoveTo(P(.18f, .52f)); p.LineTo(P(.42f, .76f)); p.LineTo(P(.84f, .26f)); p.Stroke();
                    break;
                case "circle":
                    Circle(p, P(.5f, .5f), s * .34f); p.Stroke();
                    break;
                case "flame": // Rush streak
                    p.BeginPath(); p.MoveTo(P(.5f, .06f));
                    p.BezierCurveTo(P(.78f, .34f), P(.86f, .56f), P(.78f, .74f));
                    p.BezierCurveTo(P(.7f, .92f), P(.3f, .92f), P(.22f, .74f));
                    p.BezierCurveTo(P(.14f, .56f), P(.3f, .4f), P(.36f, .3f));
                    p.BezierCurveTo(P(.4f, .44f), P(.46f, .5f), P(.5f, .52f));
                    p.BezierCurveTo(P(.56f, .4f), P(.54f, .22f), P(.5f, .06f));
                    p.ClosePath(); p.Fill();
                    break;
                case "seat": // a plastic chair
                    p.BeginPath(); p.MoveTo(P(.3f, .12f)); p.LineTo(P(.3f, .55f)); p.LineTo(P(.74f, .55f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.3f, .55f)); p.LineTo(P(.26f, .9f)); p.MoveTo(P(.72f, .55f)); p.LineTo(P(.76f, .9f)); p.Stroke();
                    break;
                case "fan": // a pedestal fan
                    Circle(p, P(.5f, .36f), s * .26f); p.Stroke();
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (i * 120 - 90) * Mathf.Deg2Rad;
                        p.BeginPath(); p.MoveTo(P(.5f, .36f)); p.LineTo(P(.5f, .36f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * .22f); p.Stroke();
                    }
                    p.BeginPath(); p.MoveTo(P(.5f, .62f)); p.LineTo(P(.5f, .88f)); p.MoveTo(P(.32f, .9f)); p.LineTo(P(.68f, .9f)); p.Stroke();
                    break;
                case "map": // a folded map with a pin
                    Poly(p, P(.1f, .22f), P(.37f, .12f), P(.63f, .22f), P(.9f, .12f), P(.9f, .78f), P(.63f, .88f), P(.37f, .78f), P(.1f, .88f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(.37f, .12f)); p.LineTo(P(.37f, .78f)); p.MoveTo(P(.63f, .22f)); p.LineTo(P(.63f, .88f)); p.Stroke();
                    break;
                case "target": // goals
                    Circle(p, P(.5f, .5f), s * .38f); p.Stroke();
                    Circle(p, P(.5f, .5f), s * .2f); p.Stroke();
                    Circle(p, P(.5f, .5f), s * .05f); p.Fill();
                    break;
            }
        }
    }

    // Five stars, filled to the nearest half.
    public class Stars : Drawn
    {
        float v;
        public static readonly Color On = new Color32(0xF2, 0xC2, 0x4A, 0xFF), Off = new Color32(0xFF, 0xFF, 0xFF, 0x30);
        public Color Empty = Off;
        public Stars() { AddToClassList("stars"); }
        public float Value
        {
            get => v;
            set { float n = Mathf.Clamp(value, 0, 5); bool redraw = Mathf.Round(n * 2) != Mathf.Round(v * 2); v = n; if (redraw) MarkDirtyRepaint(); }
        }

        protected override void Paint(Painter2D p, Rect r)
        {
            float size = Mathf.Min(r.height, r.width / 5), half = Mathf.Round(v * 2) / 2;
            for (int i = 0; i < 5; i++)
            {
                var c = new Vector2(r.x + size * (i + 0.5f), r.y + r.height / 2);
                var pts = StarPoints(c, size * 0.48f);
                p.fillColor = Empty; Poly(p, pts); p.Fill();
                if (half >= i + 1) { p.fillColor = On; Poly(p, pts); p.Fill(); }
                else if (half >= i + 0.5f) { p.fillColor = On; Poly(p, pts[5], pts[6], pts[7], pts[8], pts[9], pts[0]); p.Fill(); }
            }
        }
    }

    // A circular progress ring (cooldowns).
    public class Ring : Drawn
    {
        float v;
        public Color Track = new Color(0, 0, 0, .25f), Fill = new Color32(0xF2, 0xC2, 0x4A, 0xFF);
        public Ring() { AddToClassList("ring"); }
        public float Value { get => v; set { float n = Mathf.Clamp01(value); if (Mathf.Abs(n - v) > 0.002f) { v = n; MarkDirtyRepaint(); } } }

        protected override void Paint(Painter2D p, Rect r)
        {
            float w = Mathf.Max(3, Mathf.Min(r.width, r.height) * 0.08f), rad = Mathf.Min(r.width, r.height) / 2 - w / 2;
            var c = r.center;
            p.lineWidth = w; p.lineCap = LineCap.Round;
            p.strokeColor = Track; p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            if (v <= 0) return;
            p.strokeColor = Fill; p.BeginPath(); p.Arc(c, rad, Angle.Degrees(-90), Angle.Degrees(-90 + 360 * v)); p.Stroke();
        }
    }

    // The day as a dial: the ring fills from opening (6am) to closing (midnight); ticks mark azaan.
    public class ClockDial : Drawn
    {
        float minute = -1;
        readonly float start, end;
        readonly float[] prayers;
        public ClockDial(float dayStart, float dayEnd, float[] prayerMinutes) { start = dayStart; end = dayEnd; prayers = prayerMinutes; AddToClassList("clock-dial"); }
        public float Minute { get => minute; set { if (Mathf.Abs(value - minute) >= 2) { minute = value; MarkDirtyRepaint(); } } }

        static Color SkyAt(float m) =>
            m < 420 ? new Color32(0xD9, 0xB9, 0x94, 0xFF) : m < 1020 ? new Color32(0xF2, 0xD9, 0x8A, 0xFF)
            : m < 1110 ? new Color32(0xF0, 0x8A, 0x5D, 0xFF) : new Color32(0x3E, 0x36, 0x64, 0xFF);

        protected override void Paint(Painter2D p, Rect r)
        {
            float size = Mathf.Min(r.width, r.height), w = size * 0.12f, rad = size / 2 - w / 2;
            var c = r.center;
            float t = Mathf.InverseLerp(start, end, minute);
            p.fillColor = SkyAt(minute); Circle(p, c, rad - w * 0.9f); p.Fill();
            p.lineWidth = w;
            p.strokeColor = new Color(0, 0, 0, .3f); p.BeginPath(); p.Arc(c, rad, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            p.strokeColor = new Color32(0xE2, 0xC2, 0x7E, 0xFF);
            if (t > 0) { p.BeginPath(); p.Arc(c, rad, Angle.Degrees(-90), Angle.Degrees(-90 + 360 * t)); p.Stroke(); }
            p.lineWidth = Mathf.Max(1.5f, w * 0.35f); p.strokeColor = new Color32(0xF1, 0xE6, 0xCC, 0xFF);
            foreach (var pm in prayers)
            {
                float pt = Mathf.InverseLerp(start, end, pm);
                if (pm < start || pm > end) continue;
                float a = (-90 + 360 * pt) * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath(); p.MoveTo(c + d * (rad - w * 0.6f)); p.LineTo(c + d * (rad + w * 0.6f)); p.Stroke();
            }
            float ha = (-90 + 360 * t) * Mathf.Deg2Rad;
            p.fillColor = minute >= 1110 ? new Color32(0xE8, 0xE4, 0xF0, 0xFF) : new Color32(0xFF, 0xF1, 0xB0, 0xFF);
            Circle(p, c + new Vector2(Mathf.Cos(ha), Mathf.Sin(ha)) * rad, w * 0.75f); p.Fill();
        }
    }

    // Truck-art bunting: a row of little flags in the classic Karachi bus colours.
    public class TruckStrip : Drawn
    {
        static readonly Color[] Colors =
        {
            new Color32(0xC8, 0x20, 0x2F, 0xFF), new Color32(0xE8, 0xC3, 0x5A, 0xFF), new Color32(0x2B, 0x6F, 0xA8, 0xFF),
            new Color32(0x3E, 0x8E, 0x5E, 0xFF), new Color32(0xE5, 0x8B, 0x2A, 0xFF), new Color32(0xB0, 0x3A, 0x7A, 0xFF),
        };
        public TruckStrip() { AddToClassList("truck-strip"); }

        protected override void Paint(Painter2D p, Rect r)
        {
            float w = r.height * 1.3f;
            int n = Mathf.CeilToInt(r.width / w);
            for (int i = 0; i < n; i++)
            {
                float x = r.x + i * w;
                p.fillColor = Colors[i % Colors.Length];
                Poly(p, new Vector2(x, r.y), new Vector2(x + w, r.y), new Vector2(x + w / 2, r.yMax));
                p.Fill();
            }
        }
    }
}
