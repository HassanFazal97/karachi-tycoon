using System.Linq;
using System.Collections.Generic;
using KarachiTycoon.Sim;
using UnityEngine;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // A light the day cycle drives. Backed lights stay on with the player's generator.
    public class Practical
    {
        public Light Light;
        public float Base;
        public bool Backed, Flicker, DhabaOnly, StallLamp;
    }

    public class Glowing
    {
        public Material Mat;
        public Color Color;
        public float Base;
        public bool Backed, DhabaOnly, Facade, Coal, Early;
    }

    // Builds Burns Road procedurally from Data/burns_road.json (art style guide §8 old-city kit).
    // Placeholder geometry: the light, palette and layout are right; final art replaces the boxes.
    public class StreetBuilder : MonoBehaviour
    {
        public readonly List<Practical> Practicals = new List<Practical>();
        public readonly List<Glowing> Glows = new List<Glowing>();
        public readonly Dictionary<string, GameObject> Items = new Dictionary<string, GameObject>();
        public GameObject Dhaba, Stall;
        public readonly List<Transform> Grills = new List<Transform>();
        GameData d;
        System.Random rng;

        float R() => (float)rng.NextDouble();

        public void Build(GameData data, GameState s)
        {
            d = data; rng = new System.Random(31);
            BuildGround();
            for (int i = 0; i < d.District.Buildings.Count; i++) BuildBuilding(d.District.Buildings[i], -32 + i * 8 + 4);
            BuildSkyline();
            BuildPolesAndWires();
            BuildStall(s);
            BuildDhaba(s);
            BuildRivals();
            BuildStreetLife();
            // The street never moves: merge it into static batches so phones draw far fewer batches.
            // (Items and upgrades can still be shown or hidden; glowing materials keep their own colours.)
            StaticBatchingUtility.Combine(gameObject);
        }

        Transform Group(string name, Vector3 pos)
        {
            var g = new GameObject(name).transform;
            g.SetParent(transform, false); g.localPosition = pos;
            return g;
        }

        Practical AddLight(string hex, Vector3 pos, float baseIntensity, float range)
        {
            var go = new GameObject("light_" + hex);
            go.transform.SetParent(transform, false); go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = Hex(hex); l.range = range; l.intensity = 0; l.shadows = LightShadows.None;
            var p = new Practical { Light = l, Base = baseIntensity };
            Practicals.Add(p);
            return p;
        }

        Glowing AddGlow(Transform parent, Vector3 pos, Vector3 size, string hex, float baseIntensity)
        {
            var m = Glow(hex);
            Box(parent, pos, size, m, "glow");
            var g = new Glowing { Mat = m, Color = Hex(hex), Base = baseIntensity };
            Glows.Add(g);
            return g;
        }

        void BuildGround()
        {
            var t = Group("ground", Vector3.zero);
            Box(t, W(0, -0.05f, 0.9f), new Vector3(90, 0.1f, 7.2f), MatTex("#4A4744", "asphalt", new Vector2(30, 2.4f), 0.05f), "asphalt");
            Box(t, W(0, 0.08f, -4.5f), new Vector3(90, 0.16f, 3.6f), MatTex("#B9AD98", "paving", new Vector2(45, 1.8f)), "pavement_near");
            Box(t, W(0, 0.08f, 6.5f), new Vector3(90, 0.16f, 4f), MatTex("#B9AD98", "paving", new Vector2(45, 2f)), "pavement_far");
            // Faded lane paint, long since worn.
            var paint = Mat("#BDB6A6", 0.05f);
            for (int i = 0; i < 30; i++) Box(t, W(-44 + i * 3, 0.005f, 0.9f), new Vector3(1.4f, 0.01f, 0.12f), paint, "lane");
            // Black and yellow kerbs.
            for (int i = 0; i < 45; i++)
            {
                var m = Mat(i % 2 == 0 ? "#E2B53A" : "#1E1E1C");
                Box(t, W(-45 + i * 2 + 1, 0.1f, -2.7f), new Vector3(2, 0.2f, 0.18f), m, "kerb");
                Box(t, W(-45 + i * 2 + 1, 0.1f, 4.5f), new Vector3(2, 0.2f, 0.18f), m, "kerb");
            }
        }

        void BuildBuilding(BuildingDef b, float x0)
        {
            var t = Group("building_" + b.Seed, Vector3.zero);
            float h = 3.5f + 3 * b.Floors + 0.9f;
            var r = new System.Random(b.Seed);
            float tint = (float)r.NextDouble() * 0.12f - 0.06f;
            var stone = Hex("#D9B77E"); stone = new Color(stone.r + tint, stone.g + tint, stone.b + tint * 0.6f);
            var stoneMat = Mat("#" + ColorUtility.ToHtmlStringRGB(stone));
            var plaster = MatTex("#" + ColorUtility.ToHtmlStringRGB(stone), "plaster", new Vector2(2, Mathf.Max(1, h / 4)));
            Box(t, W(x0, h / 2, -11), new Vector3(8, h, 10), plaster, "mass");
            // Stone pilasters between the shop bays, and a carved parapet with a date plaque on top.
            var shadowStone = MatTex("#A8865A", "plaster", new Vector2(1, 2));
            foreach (var px in new[] { x0 - 3.9f, x0, x0 + 3.9f }) Box(t, W(px, 1.75f, -5.95f), new Vector3(0.32f, 3.5f, 0.2f), shadowStone, "pilaster");
            for (int i = 0; i < 14; i++) Box(t, W(x0 - 3.6f + i * 0.555f, h + 0.32f, -6.0f), new Vector3(0.26f, 0.64f, 0.22f), plaster, "parapet");
            Box(t, W(x0, h + 0.68f, -6.0f), new Vector3(8.1f, 0.1f, 0.34f), shadowStone, "coping");
            Box(t, W(x0, h - 0.5f, -5.97f), new Vector3(1.5f, 0.5f, 0.05f), Mat("#EDE6D6"), "date_plaque");
            if (!string.IsNullOrEmpty(b.Plaque)) Label(t, W(x0, h - 0.5f, -6.01f), b.Plaque, "#3B342E", 0.012f);
            var dark = Mat("#2A2420");
            var shutter = Mat(b.Shutter);
            var band = Mat("#E6CFA0");
            for (int f = 0; f <= b.Floors; f++) Box(t, W(x0, f == 0 ? 3.5f : 3.5f + 3 * f, -5.95f), new Vector3(8, 0.22f, 0.12f), band, "cornice");
            // Upper windows: dark openings, turquoise or green shutters, some lit at night.
            for (int f = 0; f < b.Floors; f++)
            {
                float y = 3.5f + 3 * f + 1.6f;
                for (int bay = 0; bay < 2; bay++)
                    for (int wi = 0; wi < 2; wi++)
                    {
                        float wx = x0 - 4 + bay * 4 + 1.33f + wi * 1.6f;
                        Box(t, W(wx, y, -5.97f), new Vector3(0.85f, 1.85f, 0.06f), dark, "window");
                        Box(t, W(wx - 0.62f, y, -5.94f), new Vector3(0.38f, 1.85f, 0.05f), shutter, "shutter");
                        Box(t, W(wx + 0.62f, y, -5.94f), new Vector3(0.38f, 1.85f, 0.05f), shutter, "shutter");
                        double lit = r.NextDouble();
                        if (lit < 0.42) { var g = AddGlow(t, W(wx, y, -5.95f), new Vector3(0.75f, 1.7f, 0.04f), lit < 0.16 ? "#96AAA5" : "#FFAA5A", 0.9f); g.Facade = true; }
                        // Split AC units bolted under some windows, with a drip stain below.
                        if (r.NextDouble() < 0.22)
                        {
                            Box(t, W(wx, y - 1.18f, -6.18f), new Vector3(0.78f, 0.42f, 0.32f), Mat("#D6D3CB", 0.3f), "ac_unit");
                            Cyl(t, W(wx + 0.16f, y - 1.18f, -6.35f), 0.14f, 0.02f, Mat("#4A4744"), "ac_fan").transform.localRotation = Quaternion.Euler(90, 0, 0);
                        }
                    }
            }
            // Jharoka balconies.
            var wood = Mat("#6B4A30");
            foreach (int bay in b.Jharoka)
                for (int f = 0; f < b.Floors; f += 2)
                {
                    float bx = x0 - 4 + bay * 4 + 2, by = 3.5f + 3 * f + 1.35f;
                    Box(t, W(bx, by, -5.6f), new Vector3(2.9f, 1.5f, 0.75f), wood, "jharoka");
                    Box(t, W(bx, by + 0.85f, -5.55f), new Vector3(3.1f, 0.12f, 0.95f), wood, "jharoka_roof");
                    // Washing hung out on a line below the balcony.
                    if (r.NextDouble() < 0.7)
                    {
                        Wire(t, W(bx - 1.4f, by - 0.85f, -5.15f), W(bx + 1.4f, by - 0.85f, -5.15f), 0.12f, Mat("#2A2420"));
                        string[] cloth = { "#C8202F", "#EDE6D6", "#2B6FA8", "#E58B2A", "#3E8E5E", "#B03A7A", "#E8C35A" };
                        int n = 3 + r.Next(4);
                        for (int k = 0; k < n; k++)
                        {
                            float cx = bx - 1.15f + k * (2.3f / n);
                            Box(t, W(cx, by - 1.12f - (float)r.NextDouble() * 0.1f, -5.15f), new Vector3(0.32f, 0.5f + (float)r.NextDouble() * 0.25f, 0.02f), Mat(cloth[r.Next(cloth.Length)], 0.2f), "laundry");
                        }
                    }
                }
            // Ground floor: two shop bays with signboards and tube lights.
            for (int bay = 0; bay < 2; bay++)
            {
                float sx = x0 - 4 + bay * 4 + 2;
                var sign = b.Signs.Find(s => s.Bay == bay);
                bool shut = sign != null && sign.Shut;
                Box(t, W(sx, 1.4f, -5.96f), new Vector3(3.3f, 2.75f, 0.06f), shut ? MatTex("#7A817A", "shutter", new Vector2(1, 3)) : Mat("#4A3A2C"), shut ? "shutter_down" : "shop_interior");
                // Posters pasted on the pilasters, newer over older.
                if (r.NextDouble() < 0.6)
                {
                    string[] posterCols = { "#C8202F", "#2B6FA8", "#E8C35A", "#3E8E5E", "#EDE6D6" };
                    for (int k = 0; k < 2; k++)
                        Box(t, W(sx - 1.92f + (float)r.NextDouble() * 0.1f, 1.3f + k * 0.55f, -6.07f), new Vector3(0.26f, 0.4f, 0.01f), Mat(posterCols[r.Next(posterCols.Length)], 0.2f), "poster")
                            .transform.localRotation = Quaternion.Euler(0, 0, (float)(r.NextDouble() - 0.5) * 10);
                }
                if (!shut) AddGlow(t, W(sx, 2.82f, -5.9f), new Vector3(1.6f, 0.05f, 0.05f), "#E8F4F0", 1.6f);
                if (sign != null) BuildSign(t, W(sx, 3.12f, -5.9f), new Vector2(3.6f, 0.64f), sign.En, sign.Ur, sign.Bg, sign.Fg);
            }
            // Cool tube light spilling out of the open shops onto the pavement (one light per building, phone friendly).
            if (b.Signs.Count(sg => !sg.Shut) > 0 || b.Signs.Count < 2) AddLight("#E8F4F0", W(x0, 2.5f, -4.9f), 0.7f, 7);

            // Roof clutter: black water tanks.
            var tank = Mat("#1E1E1E", 0.4f);
            Cyl(t, W(x0 - 2, h + 0.58f, -8.5f), 0.55f, 1.15f, tank, "water_tank");
            if (b.Seed % 2 == 1) Cyl(t, W(x0 + 1.2f, h + 0.58f, -9.5f), 0.55f, 1.15f, tank, "water_tank");
            if (b.Seed % 3 != 0)
            {
                var dish = Cyl(t, W(x0 + 2.6f, h + 0.75f, -7.2f), 0.42f, 0.04f, Mat("#CFCFCB", 0.4f), "satellite_dish");
                dish.transform.localRotation = Quaternion.Euler(60, 20, 0);
            }
        }

        // Signboards use pre-painted Nastaliq textures (Resources/Signs) where they exist, because
        // Unity text does not shape Urdu Nastaliq. Otherwise a plain panel with the English name.
        void BuildSign(Transform parent, Vector3 pos, Vector2 size, string en, string ur, string bg, string fg)
        {
            var tex = Resources.Load<Texture2D>("Signs/" + Slug(en));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "sign_" + Slug(en);
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = pos;
            // A Unity quad faces -z, which is toward the street camera after the z flip in Util.W.
            quad.transform.localScale = new Vector3(size.x, size.y, 1);
            var m = NewLit();
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            else
            {
                var c = Hex(bg);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                var label = new GameObject("label").AddComponent<TextMesh>();
                label.transform.SetParent(quad.transform, false);
                label.transform.localPosition = new Vector3(0, 0, -0.01f);
                label.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1) * 0.05f;
                label.text = en; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                label.fontSize = 48; label.characterSize = 0.5f; label.color = Hex(fg);
                var font = UiFont; if (font != null) { label.font = font; label.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            }
            quad.GetComponent<Renderer>().sharedMaterial = m;
        }

        public static string Slug(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.ToString().Trim('_');
        }

        void BuildSkyline()
        {
            var t = Group("skyline", Vector3.zero);
            string[] cols = { "#B49A78", "#A89076", "#9C8A74", "#C4AE8C" };
            for (int i = 0; i < 40; i++)
            {
                float w = 6 + R() * 12, h = 8 + R() * 26, dd = 6 + R() * 8;
                Box(t, W(-90 + R() * 180, h / 2, -32 - R() * 70), new Vector3(w, h, dd), Mat(cols[i % 4]), "far_block");
            }
            Cyl(t, W(-18, 17, -48), 1f, 34, Mat("#E8E2CF"), "minaret");
            Ball(t, W(-8, 14, -52), 6, Mat("#E8E2CF"), "dome");
            // The mosque's green neon is the first light on at Maghrib (style guide, lighting).
            var neon = AddGlow(t, W(-18, 30.5f, -48), new Vector3(2.4f, 0.25f, 2.4f), "#3BEA7A", 4); neon.Early = true;
            var neon2 = AddGlow(t, W(-8, 20.2f, -52), new Vector3(0.3f, 1.4f, 0.3f), "#3BEA7A", 4); neon2.Early = true;
        }

        void BuildPolesAndWires()
        {
            var t = Group("poles", Vector3.zero);
            var pole = Mat("#6A645C");
            float[] xs = { -24, -6, 5.2f, 17, 28 };
            foreach (var x in xs)
            {
                Cyl(t, W(x, 4, -2.95f), 0.11f, 8, pole, "pole");
                if (x == -6 || x == 17)
                {
                    var g = AddGlow(t, W(x, 7.12f, -2.45f), new Vector3(0.5f, 0.06f, 0.22f), "#FFB060", 4);
                    AddLight("#FFA448", W(x, 7f, -2.6f), 3.2f, 16);
                }
            }
            // Overhead wires: sagging catenaries between poles and facades (style guide §4 rule 3).
            var wireMat = Mat("#141210");
            for (int i = 0; i < xs.Length - 1; i++)
                for (int k = 0; k < 4; k++) Wire(t, W(xs[i], 7.5f - k * 0.15f, -2.95f), W(xs[i + 1], 7.5f - k * 0.15f, -2.95f), 0.5f + R() * 0.6f, wireMat);
            foreach (var x in xs)
                for (int k = 0; k < 5; k++) Wire(t, W(x, 7.4f, -2.95f), W(x + (R() - 0.5f) * 9, 4.2f + R() * 6, -6), 0.4f + R() * 0.7f, wireMat);
        }

        void Wire(Transform parent, Vector3 a, Vector3 b, float sag, Material m)
        {
            var go = new GameObject("wire"); go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.positionCount = 17; lr.widthMultiplier = 0.025f; lr.sharedMaterial = m;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                lr.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * sag * 4 * t * (1 - t));
            }
        }

        static Transform Group2(Transform parent, string name)
        {
            var g = new GameObject(name).transform; g.SetParent(parent, false);
            g.gameObject.SetActive(false);
            return g;
        }

        // The player's thela: wooden cart, truck-art panel, canopy, bulbs, kettle, tawa, karahi, matka.
        void BuildStall(GameState s)
        {
            var p = d.District.Pitch;
            var t = Group("thela", W(p.X, 0.16f, p.Z)); Stall = t.gameObject;
            Box(t, new Vector3(0, 0.62f, 0), new Vector3(2.1f, 0.75f, 0.95f), Mat("#8A5A32"), "cart_body");
            BuildSign(t, new Vector3(0, 0.62f, -0.49f), new Vector2(2f, 0.39f), s.ShopName, s.ShopUrdu, "#C8202F", "#FFF3D6");
            Box(t, new Vector3(0, 1.02f, 0), new Vector3(2.2f, 0.05f, 1.05f), Mat("#C9C6BE", 0.7f, 0.75f), "steel_top");
            foreach (var wx in new[] { -0.75f, 0.75f }) Cyl(t, new Vector3(wx, 0.3f, -0.5f), 0.3f, 0.06f, Mat("#3A2A1E"), "wheel").transform.localRotation = Quaternion.Euler(90, 0, 0);
            foreach (var c in new[] { new Vector2(-1, -0.45f), new Vector2(1, -0.45f), new Vector2(-1, 0.45f), new Vector2(1, 0.45f) })
                Box(t, new Vector3(c.x, 1.65f, c.y), new Vector3(0.04f, 1.25f, 0.04f), Mat("#5A5550"), "canopy_pole");
            for (int i = 0; i < 6; i++) Box(t, new Vector3(-1.0f + i * 0.4f + 0.2f, 2.3f, 0), new Vector3(0.4f, 0.03f, 1.3f), Mat(i % 2 == 0 ? "#C8202F" : "#F2D24A"), "canopy");
            var bulb = AddGlow(t, new Vector3(0, 2.12f, -0.35f), new Vector3(1.8f, 0.06f, 0.06f), "#FFC27A", 5); bulb.Backed = true;
            var lamp = AddLight("#FFC27A", W(p.X, 2.25f, p.Z + 0.4f), 2.6f, 10); lamp.Backed = true; lamp.StallLamp = true;
            var chai = new GameObject("chai").transform; chai.SetParent(t, false); chai.localPosition = new Vector3(-0.6f, 1.05f, 0);
            Cyl(chai, new Vector3(0, 0.06f, 0), 0.17f, 0.12f, Mat("#2A2826"), "stove");
            Cyl(chai, new Vector3(0, 0.25f, 0), 0.14f, 0.28f, Mat("#B8B6B0", 0.6f, 0.7f), "kettle");
            Items["chai"] = chai.gameObject;
            var tawa = new GameObject("bun_kabab").transform; tawa.SetParent(t, false); tawa.localPosition = new Vector3(0.2f, 1.05f, 0);
            Cyl(tawa, new Vector3(0, 0.08f, 0), 0.32f, 0.04f, Mat("#1E1C1A", 0.4f, 0.5f), "tawa");
            Items["bun_kabab"] = tawa.gameObject;
            // Upgrades, shown once bought (RefreshStage).
            var up = new GameObject("upgrades").transform; up.SetParent(t, false);
            var stools = Group2(up, "upgrade_stools");
            for (int i = 0; i < 3; i++)
            {
                var sx = -0.9f + i * 0.9f;
                Cyl(stools, new Vector3(sx, 0.22f, -1.25f), 0.17f, 0.04f, Mat(i % 2 == 0 ? "#C8202F" : "#2B6FA8", 0.4f), "stool_seat");
                Cyl(stools, new Vector3(sx, 0.1f, -1.25f), 0.13f, 0.2f, Mat(i % 2 == 0 ? "#A81A27" : "#235C8C", 0.4f), "stool_legs");
            }
            var bigKettle = Group2(up, "upgrade_kettle");
            bigKettle.localPosition = new Vector3(-0.6f, 1.05f, 0);
            Cyl(bigKettle, new Vector3(0, 0.33f, 0), 0.19f, 0.4f, Mat("#D2D0C8", 0.75f, 0.8f), "big_kettle");
            Cyl(bigKettle, new Vector3(0.22f, 0.42f, 0), 0.03f, 0.18f, Mat("#D2D0C8", 0.75f, 0.8f), "spout").transform.localRotation = Quaternion.Euler(0, 0, -50);
            var tawa2 = Group2(up, "upgrade_tawa2");
            Cyl(tawa2, new Vector3(0.2f, 1.13f, 0.32f), 0.26f, 0.04f, Mat("#1E1C1A", 0.4f, 0.5f), "tawa_2");
            var tube = Group2(up, "upgrade_tubelight");
            var tg = AddGlow(tube, new Vector3(0, 2.0f, -0.5f), new Vector3(1.6f, 0.05f, 0.05f), "#E8F4F0", 3.2f); tg.Backed = true;
            var art = Group2(up, "upgrade_sign");
            string[] truck = { "#C8202F", "#E8C35A", "#2B6FA8", "#3E8E5E", "#E58B2A" };
            for (int i = 0; i < 10; i++)
                Box(art, new Vector3(-0.99f + i * 0.22f, 2.42f, -0.62f), new Vector3(0.2f, 0.22f, 0.03f), Mat(truck[i % truck.Length], 0.5f), "truck_art");
            Box(art, new Vector3(0, 2.56f, -0.62f), new Vector3(2.2f, 0.05f, 0.04f), Mat("#F2D24A", 0.6f, 0.4f), "truck_trim");
            var karahi = new GameObject("samosa").transform; karahi.SetParent(t, false); karahi.localPosition = new Vector3(0.82f, 1.05f, -0.05f);
            Cyl(karahi, new Vector3(0, 0.08f, 0), 0.3f, 0.12f, Mat("#2A2826", 0.4f, 0.4f), "karahi");
            Cyl(karahi, new Vector3(0, 0.15f, 0), 0.26f, 0.01f, Mat("#C88A2A", 0.8f, 0.3f), "oil");
            Items["samosa"] = karahi.gameObject;
            var matka = new GameObject("dahi_baray").transform; matka.SetParent(t, false); matka.localPosition = new Vector3(-1.6f, 0, 0.3f);
            Box(matka, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.5f), Mat("#3A2A1E"), "stand");
            Ball(matka, new Vector3(0, 0.88f, 0), 0.3f, Mat("#B5532F"), "matka");
            Items["dahi_baray"] = matka.gameObject;
        }

        // The takhta (pavement dhaba): platform, nihari deg, tables and plastic chairs on the road edge.
        void BuildDhaba(GameState s)
        {
            var t = Group("dhaba", Vector3.zero); Dhaba = t.gameObject;
            Box(t, W(2.0f, 0.38f, -5.25f), new Vector3(3.3f, 0.45f, 1.3f), Mat("#6B4A30"), "takhta");
            Box(t, W(2.0f, 0.62f, -5.25f), new Vector3(3.3f, 0.04f, 1.3f), Mat("#C9B48A"), "dari");
            var deg = new GameObject("nihari").transform; deg.SetParent(t, false); deg.localPosition = W(1.2f, 0.62f, -5.3f);
            Cyl(deg, new Vector3(0, 0.1f, 0), 0.34f, 0.2f, Mat("#2A2826"), "burner");
            Cyl(deg, new Vector3(0, 0.48f, 0), 0.4f, 0.56f, Mat("#C9C6BE", 0.7f, 0.8f), "deg");
            Items["nihari"] = deg.gameObject;
            BuildSign(t, W(2.0f, 3.14f, -5.88f), new Vector2(3.7f, 0.72f), s.ShopName, s.ShopUrdu, "#C8202F", "#FFF3D6");
            var tube = AddGlow(t, W(2.0f, 2.72f, -5.88f), new Vector3(1.8f, 0.05f, 0.05f), "#E8F4F0", 2.6f); tube.Backed = true; tube.DhabaOnly = true;
            var l = AddLight("#E8F4F0", W(1.8f, 3.0f, -5.2f), 2.4f, 10); l.Backed = true; l.DhabaOnly = true;
            foreach (var tb in d.District.Tables)
            {
                Box(t, W(tb[0], 0.74f, tb[1]), new Vector3(1.3f, 0.04f, 0.8f), Mat("#D8D2C4", 0.5f), "table_top");
                Box(t, W(tb[0], 0.37f, tb[1]), new Vector3(0.1f, 0.74f, 0.1f), Mat("#C9C6BE", 0.7f, 0.75f), "table_leg");
            }
            string[] cols = { "#E8E2CF", "#C8202F", "#2B6FA8", "#3E8E5E" };
            for (int i = 0; i < d.District.Seats.Length; i++)
            {
                var st = d.District.Seats[i];
                float y = st[1] < -2.7f ? 0.16f : 0;
                Box(t, W(st[0], y + 0.44f, st[1]), new Vector3(0.46f, 0.05f, 0.44f), Mat(cols[i % 4], 0.5f), "chair_seat");
                Box(t, W(st[0], y + 0.22f, st[1]), new Vector3(0.4f, 0.44f, 0.38f), Mat(cols[i % 4], 0.5f), "chair_legs");
            }
        }

        // The street spills out (style guide rule 4): parked motorbikes, stacked plastic chairs,
        // crates, and strings of bunting across the road.
        void BuildStreetLife()
        {
            var t = Group("street_life", Vector3.zero);
            var r = new System.Random(77);
            float F() => (float)r.NextDouble();
            float pitch = d.District.Pitch.X;
            string[] bikeCols = { "#C8202F", "#1E1E1C", "#2B6FA8", "#7A1F1F" };
            foreach (var (bx, bz) in new[] { (-27f, 5.2f), (-21.5f, 5.0f), (-12f, 5.3f), (9f, 5.1f), (13.5f, 5.2f), (24f, 5.0f), (-30f, -4.2f), (21f, -4.0f) })
            {
                if (Mathf.Abs(bx - pitch) < 4) continue;
                var bike = new GameObject("motorbike").transform; bike.SetParent(t, false);
                bike.localPosition = W(bx, 0.16f, bz); bike.localRotation = Quaternion.Euler(0, 70 + F() * 40, 0);
                var body = Mat(bikeCols[r.Next(bikeCols.Length)], 0.5f, 0.2f);
                foreach (var wz in new[] { -0.55f, 0.55f }) Cyl(bike, new Vector3(0, 0.3f, wz), 0.3f, 0.08f, Mat("#1A1816"), "wheel").transform.localRotation = Quaternion.Euler(0, 0, 90);
                Box(bike, new Vector3(0, 0.55f, 0.05f), new Vector3(0.22f, 0.3f, 0.9f), body, "tank");
                Box(bike, new Vector3(0, 0.74f, -0.25f), new Vector3(0.24f, 0.08f, 0.55f), Mat("#2A2420"), "seat");
                Box(bike, new Vector3(0, 0.95f, 0.5f), new Vector3(0.7f, 0.04f, 0.04f), Mat("#9A9890", 0.6f, 0.6f), "handlebar");
                Ball(bike, new Vector3(0, 0.82f, 0.62f), 0.07f, Mat("#F4E7C8", 0.7f), "headlamp");
            }
            // Plastic chairs stacked outside the shops, and wooden crates.
            string[] chairCols = { "#E8E2CF", "#C8202F", "#2B6FA8", "#3E8E5E" };
            foreach (var cx in new[] { -25f, -10.5f, 11f, 26f })
            {
                if (Mathf.Abs(cx - pitch) < 4) continue;
                var col = Mat(chairCols[r.Next(chairCols.Length)], 0.5f);
                for (int k = 0; k < 4; k++) Box(t, W(cx, 0.4f + k * 0.12f, -5.2f), new Vector3(0.46f, 0.05f, 0.44f), col, "chair_stack");
                Box(t, W(cx, 0.24f, -5.2f), new Vector3(0.4f, 0.4f, 0.38f), col, "chair_legs");
                Box(t, W(cx + 0.8f, 0.36f, -5.3f), new Vector3(0.6f, 0.4f, 0.45f), MatTex("#8A6A42", "wood", new Vector2(1, 1)), "crate");
            }
            // Bunting strung across the road between the poles.
            string[] flagCols = { "#C8202F", "#E8C35A", "#2B6FA8", "#3E8E5E", "#E58B2A", "#B03A7A" };
            foreach (var (x1, x2) in new[] { (-24f, -6f), (5.2f, 17f) })
            {
                var a = W(x1, 6.6f, -2.95f); var bpt = W(x2, 6.6f, 4.5f);
                Wire(t, a, bpt, 0.6f, Mat("#2A2420"));
                for (int k = 1; k < 24; k++)
                {
                    float u = k / 24f;
                    var p = Vector3.Lerp(a, bpt, u) + Vector3.down * 0.6f * 4 * u * (1 - u) + Vector3.down * 0.12f;
                    var flag = Box(t, p, new Vector3(0.22f, 0.22f, 0.01f), Mat(flagCols[k % flagCols.Length], 0.2f), "flag");
                    flag.transform.localRotation = Quaternion.LookRotation(bpt - a) * Quaternion.Euler(0, 90, 45);
                }
            }
        }

        // A small painted label (plaques, dates).
        void Label(Transform parent, Vector3 pos, string text, string hex, float size)
        {
            var label = new GameObject("label").AddComponent<TextMesh>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = pos;
            label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.fontSize = 64; label.characterSize = size * 4; label.color = Hex(hex);
            var font = UiFont; if (font != null) { label.font = font; label.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
        }

        void BuildRivals()
        {
            foreach (var rv in d.District.Rivals)
            {
                var t = Group("rival_" + rv.Id, W(rv.X, 0.16f, rv.Z - 1.2f));
                if (rv.Kind == "deg")
                {
                    Cyl(t, new Vector3(0, 0.15f, 0), 0.4f, 0.3f, Mat("#2A2826"), "burner");
                    Cyl(t, new Vector3(0, 0.6f, 0), 0.5f, 0.67f, Mat("#C9C6BE", 0.7f, 0.8f), "deg");
                    AddLight("#E8F4F0", W(rv.X, 2.9f, -5.2f), 2.2f, 9);
                }
                else
                {
                    Box(t, new Vector3(0, 0.42f, 0), new Vector3(1.8f, 0.85f, 0.7f), Mat("#3A3836"), "grill");
                    Grills.Add(t);
                    var coal = AddGlow(t, new Vector3(0, 0.88f, 0), new Vector3(1.6f, 0.06f, 0.4f), "#FF6A2B", 3); coal.Coal = true;
                    var l = AddLight("#FF6A2B", W(rv.X, 1.25f, rv.Z - 1.2f), 2f, 6); l.Flicker = true;
                }
            }
        }

        public void RefreshStage(Sim.Sim sim)
        {
            var s = sim.S; bool dh = s.Stage == "dhaba";
            Dhaba.SetActive(dh);
            foreach (var kv in Items) kv.Value.SetActive(!sim.IsLocked(kv.Key) || (kv.Key != "nihari" && s.Unlocked[kv.Key]));
            foreach (var u in sim.D.Upgrades)
            {
                var g = Stall.transform.Find("upgrades/upgrade_" + u.Id);
                if (g != null) g.gameObject.SetActive(sim.Owns(u.Id));
            }
            // The bigger kettle replaces the small one.
            var small = Stall.transform.Find("chai/kettle");
            if (small != null) small.gameObject.SetActive(!sim.Owns("kettle"));
        }
    }
}
