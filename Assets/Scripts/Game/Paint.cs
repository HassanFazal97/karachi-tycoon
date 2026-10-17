using System.Collections.Generic;
using UnityEngine;

namespace KarachiTycoon.Game
{
    // Small painted textures made at start-up (art style guide: "painted realism", soft and lived-in).
    // Greyscale detail that multiplies the material colour: plaster with water stains, worn asphalt,
    // paving slabs, ribbed rolling shutters, wood grain, and a soft dot for steam and smoke.
    public static class Paint
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string kind)
        {
            if (cache.TryGetValue(kind, out var t)) return t;
            t = kind switch
            {
                "plaster" => Make(128, 7, Plaster),
                "asphalt" => Make(128, 11, Asphalt),
                "paving" => Make(128, 13, Paving),
                "shutter" => Make(64, 17, Shutter),
                "wood" => Make(64, 19, Wood),
                "soft" => Soft(64),
                _ => Texture2D.whiteTexture,
            };
            cache[kind] = t;
            return t;
        }

        delegate float Pixel(int x, int y, int n, System.Random r, float[,] noise);

        static Texture2D Make(int n, int seed, Pixel f)
        {
            var r = new System.Random(seed);
            var noise = Noise(n, r);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "paint" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    byte v = (byte)(Mathf.Clamp01(f(x, y, n, r, noise)) * 255);
                    px[y * n + x] = new Color32(v, v, v, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // Tileable value noise: a few octaves of a wrapped random grid.
        static float[,] Noise(int n, System.Random r)
        {
            var o = new float[n, n];
            foreach (var (cells, amp) in new[] { (4, 0.5f), (8, 0.3f), (32, 0.2f) })
            {
                var g = new float[cells, cells];
                for (int i = 0; i < cells; i++) for (int j = 0; j < cells; j++) g[i, j] = (float)r.NextDouble();
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x * cells / (float)n, fy = y * cells / (float)n;
                        int x0 = (int)fx, y0 = (int)fy; float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
                        float a = g[x0 % cells, y0 % cells], b = g[(x0 + 1) % cells, y0 % cells];
                        float c = g[x0 % cells, (y0 + 1) % cells], d = g[(x0 + 1) % cells, (y0 + 1) % cells];
                        o[x, y] += amp * Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
                    }
            }
            return o;
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);

        // Lime plaster: soft blotches, grain, and rain streaks running down from the top.
        static float Plaster(int x, int y, int n, System.Random r, float[,] noise)
        {
            float v = 0.86f + noise[x, y] * 0.16f;
            float streak = Mathf.PerlinNoise(x * 0.35f, 3.7f);
            float fromTop = y / (float)n; // texture v=1 is the top of a face
            if (streak > 0.62f) v -= (streak - 0.62f) * 0.9f * fromTop * fromTop;
            if (noise[(x * 7) % n, (y * 5) % n] > 0.82f) v -= 0.06f;
            return v;
        }

        static float Asphalt(int x, int y, int n, System.Random r, float[,] noise)
        {
            float v = 0.78f + noise[x, y] * 0.18f + (float)(r.NextDouble() - 0.5) * 0.14f;
            if (noise[(x * 3) % n, (y * 3) % n] > 0.78f) v -= 0.12f; // patches
            return v;
        }

        static float Paving(int x, int y, int n, System.Random r, float[,] noise)
        {
            int tile = n / 4;
            bool joint = x % tile < 2 || y % tile < 2;
            float tone = 0.86f + ((x / tile * 7 + y / tile * 13) % 5) * 0.03f;
            return joint ? 0.62f : tone + noise[x, y] * 0.1f - 0.05f;
        }

        static float Shutter(int x, int y, int n, System.Random r, float[,] noise)
        {
            float rib = (y % 6) < 1 ? 0.62f : (y % 6) < 2 ? 0.8f : 0.95f;
            return rib - noise[x, y] * 0.12f;
        }

        static float Wood(int x, int y, int n, System.Random r, float[,] noise)
        {
            float grain = Mathf.Sin((x + noise[x, y] * 18) * 0.9f) * 0.06f;
            return 0.86f + grain + noise[x, y] * 0.08f;
        }

        // A soft round dot for particles (alpha falloff).
        static Texture2D Soft(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "soft" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                    byte a = (byte)(Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d) * 255);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
