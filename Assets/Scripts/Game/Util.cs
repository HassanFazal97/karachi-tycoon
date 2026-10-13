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

        static Shader litShader;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        // Works in both the built-in pipeline and URP: the URP shader only when a URP asset is active,
        // because URP shaders render pink under the built-in renderer.
        public static Shader Lit
        {
            get
            {
                if (litShader == null)
                {
                    bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
                    litShader = (urp ? Shader.Find("Universal Render Pipeline/Lit") : null) ?? Shader.Find("Standard");
                }
                return litShader;
            }
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }

        public static Material Mat(string hex, float smoothness = 0.15f, float metallic = 0f)
        {
            string key = hex + smoothness + metallic;
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Lit) { name = "kt_" + hex };
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
            var m = new Material(Lit) { name = "kt_glow_" + hex };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.black);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetGlow(m, Hex(hex), 0);
            return m;
        }

        public static void SetGlow(Material m, Color c, float intensity) => m.SetColor("_EmissionColor", c * intensity);

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
