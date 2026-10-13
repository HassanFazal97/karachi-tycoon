using UnityEngine;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // Lighting presets per time of day (art style guide §7), interpolated by game minute.
    // Same keyframes as the browser prototype so both builds look alike.
    public class DayCycle : MonoBehaviour
    {
        struct Key { public float M, Sun, Hemi, Prac, El; public string Top, Bot, Haze, SunC; }

        static readonly Key[] Keys =
        {
            new Key { M = 300, Top = "#14162A", Bot = "#2C2A44", Haze = "#3A3550", Sun = 0.0f, SunC = "#8090C0", Hemi = 0.16f, Prac = 1.0f, El = 30 },
            new Key { M = 375, Top = "#7F93A8", Bot = "#D9B994", Haze = "#C9B9A0", Sun = 0.5f, SunC = "#FFC59A", Hemi = 0.32f, Prac = 0.7f, El = 6 },
            new Key { M = 480, Top = "#BFCBD2", Bot = "#F2D9A8", Haze = "#E6DCC6", Sun = 1.0f, SunC = "#FFE2BD", Hemi = 0.55f, Prac = 0.15f, El = 28 },
            new Key { M = 720, Top = "#D3D6CB", Bot = "#EDE6D6", Haze = "#EDE6D6", Sun = 1.3f, SunC = "#FFF5E4", Hemi = 0.75f, Prac = 0.0f, El = 72 },
            new Key { M = 960, Top = "#D3C3A0", Bot = "#F4C98A", Haze = "#EBD3A8", Sun = 1.1f, SunC = "#FFD49A", Hemi = 0.6f, Prac = 0.12f, El = 30 },
            new Key { M = 1080, Top = "#9A6E78", Bot = "#F08A5D", Haze = "#E79A70", Sun = 0.8f, SunC = "#FF9050", Hemi = 0.42f, Prac = 0.55f, El = 7 },
            new Key { M = 1125, Top = "#3E3664", Bot = "#6E5A8C", Haze = "#7A5C80", Sun = 0.1f, SunC = "#9080C0", Hemi = 0.26f, Prac = 1.0f, El = 2 },
            new Key { M = 1230, Top = "#16172C", Bot = "#24233F", Haze = "#2E2A40", Sun = 0.0f, SunC = "#7080B0", Hemi = 0.16f, Prac = 1.0f, El = 30 },
            new Key { M = 1440, Top = "#0B0C16", Bot = "#12131F", Haze = "#1C1C2A", Sun = 0.0f, SunC = "#7080B0", Hemi = 0.12f, Prac = 1.0f, El = 30 },
        };

        public Light Sun;
        public Camera Cam;
        StreetBuilder street;
        Sim.Sim sim;

        public void Init(Sim.Sim s, StreetBuilder st, Camera cam)
        {
            sim = s; street = st; Cam = cam;
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            Sun = go.AddComponent<Light>();
            Sun.type = LightType.Directional; Sun.shadows = LightShadows.Soft; Sun.shadowStrength = 0.8f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 75; RenderSettings.fogEndDistance = 170;
            Cam.clearFlags = CameraClearFlags.SolidColor;
        }

        void Update()
        {
            if (sim?.S == null) return;
            float m = sim.S.Minute;
            Key a = Keys[0], b = Keys[Keys.Length - 1];
            for (int i = 0; i < Keys.Length - 1; i++) if (m >= Keys[i].M && m <= Keys[i + 1].M) { a = Keys[i]; b = Keys[i + 1]; break; }
            float t = b.M == a.M ? 0 : Mathf.Clamp01((m - a.M) / (b.M - a.M));
            Color L(string x, string y) => Color.Lerp(Hex(x), Hex(y), t);
            float sun = Mathf.Lerp(a.Sun, b.Sun, t), hemi = Mathf.Lerp(a.Hemi, b.Hemi, t), prac = Mathf.Lerp(a.Prac, b.Prac, t), el = Mathf.Lerp(a.El, b.El, t);
            var top = L(a.Top, b.Top); var bot = L(a.Bot, b.Bot); var haze = L(a.Haze, b.Haze);

            Cam.backgroundColor = Color.Lerp(bot, top, 0.55f);
            RenderSettings.fogColor = haze;
            RenderSettings.ambientSkyColor = Color.Lerp(top, Color.white, 0.4f) * hemi * 1.4f;
            RenderSettings.ambientEquatorColor = haze * hemi * 1.2f;
            RenderSettings.ambientGroundColor = bot * hemi * 0.6f;

            // Sun moves east to west, leaning south so the shopfronts catch it; a cool moon at night.
            bool night = m > 1135 || m < 370;
            float az = Mathf.Clamp01((m - 360) / (1140 - 360)) * 180f;
            // Light travels toward +z (the facades); heading swings from -60° (morning, from the east) to +60° (evening).
            Sun.transform.rotation = night ? Quaternion.Euler(60, 20, 0) : Quaternion.Euler(Mathf.Max(6, el), -60 + az * (120f / 180f), 0);
            Sun.color = L(a.SunC, b.SunC);
            Sun.intensity = night ? 0.15f : sun;

            bool outage = sim.PowerOut, gen = sim.S.Generator, dhaba = sim.S.Stage == "dhaba";
            float time = Time.time;
            foreach (var p in street.Practicals)
            {
                float i = p.Base * prac;
                if (p.DhabaOnly && !dhaba) i = 0;
                if (outage) i = p.Backed ? (gen ? i * 0.7f : (p.StallLamp ? 1f * prac : 0)) : 0;
                if (p.Flicker) i *= 0.8f + Mathf.Sin(time * 13) * 0.08f + Mathf.Sin(time * 7.3f) * 0.1f;
                p.Light.intensity = i;
            }
            foreach (var g in street.Glows)
            {
                float i = g.Base * prac;
                if (g.Coal) i = m > 960 ? g.Base * (0.8f + Mathf.Sin(time * 9) * 0.2f) : g.Base * 0.3f;
                if (g.DhabaOnly && !dhaba) i = 0;
                if (outage && !g.Coal) i = g.Backed && gen ? i * 0.7f : g.Facade ? i * 0.04f : 0;
                SetGlow(g.Mat, g.Color, i);
            }
        }
    }
}
