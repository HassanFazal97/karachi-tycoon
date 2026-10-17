using System.Collections.Generic;
using UnityEngine;

namespace KarachiTycoon.Game
{
    // Shared helpers for building the procedural street.
    public static class Util
    {
        // The simulation uses the browser prototype's right-handed layout (z toward the viewer).
        // Unity is left-handed, so z is flipped once here and nowhere else.
        public static Vector3 W(float x, float y, float z) => new Vector3(x, y, -z);

        static Material litTemplate, glowTemplate;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        // URP materials come from templates in Resources/Materials (made by BuildTools), which also makes
        // builds include the shader variants they need. Without URP the built-in Standard shader is used.
        static bool Urp => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        static Material Template(bool glow)
        {
            if (glow ? glowTemplate != null : litTemplate != null) return glow ? glowTemplate : litTemplate;
            var t = Urp ? Resources.Load<Material>(glow ? "Materials/KT_Glow" : "Materials/KT_Lit") : null;
            if (t == null) t = new Material(Shader.Find("Standard"));
            if (glow) glowTemplate = t; else litTemplate = t;
            return t;
        }
        public static Shader Lit => Template(false).shader;
        public static Material NewLit() => new Material(Template(false));

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }

        public static Material Mat(string hex, float smoothness = 0.15f, float metallic = 0f)
        {
            string key = hex + smoothness + metallic;
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Template(false)) { name = "kt_" + hex };
            var c = Hex(hex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            cache[key] = m;
            return m;
        }

        // A fresh (uncached) emissive material whose glow the day cycle drives.
        public static Material Glow(string hex)
        {
            var m = new Material(Template(true)) { name = "kt_glow_" + hex };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.black);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetGlow(m, Hex(hex), 0);
            return m;
        }

        public static void SetGlow(Material m, Color c, float intensity) => m.SetColor("_EmissionColor", c * intensity);

        // A lit material with a painted detail texture (Paint.cs), tiled to the object's size in metres.
        public static Material MatTex(string hex, string paint, Vector2 tiling, float smoothness = 0.1f)
        {
            string key = hex + paint + tiling;
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Template(false)) { name = "kt_" + paint + "_" + hex };
            var c = Hex(hex);
            var tex = Paint.Get(paint);
            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                if (m.HasProperty(prop)) { m.SetTexture(prop, tex); m.SetTextureScale(prop, tiling); }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            cache[key] = m;
            return m;
        }

        public static GameObject Cap(Transform parent, Vector3 pos, float radius, float height, Material m, string name = "cap")
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material m, string name = "box")
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        public static GameObject Cyl(Transform parent, Vector3 pos, float radius, float height, Material m, string name = "cyl")
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        public static GameObject Ball(Transform parent, Vector3 pos, float radius, Material m, string name = "ball")
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = Vector3.one * radius * 2;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        public static Font UiFont
        {
            get
            {
                Font f = null;
                try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
                return f;
            }
        }

        public static string Rs(int n) => "Rs " + n.ToString("N0");

        public static string Clock(float m)
        {
            int h = (int)(m / 60) % 24, mm = (int)(m % 60);
            return $"{(h + 11) % 12 + 1}:{mm:00} {(h < 12 ? "am" : "pm")}";
        }
    }
}
