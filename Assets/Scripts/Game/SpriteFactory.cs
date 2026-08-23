using System.Collections.Generic;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Every sprite in the game is generated at runtime, so the project carries no art
    /// binaries and nothing can fail to import. Textures are cached by shape+size, because
    /// a survivor game spawns hundreds of objects and re-baking a texture per spawn would
    /// be real allocation churn.
    /// </summary>
    public static class SpriteFactory
    {
        const int Resolution = 64;
        const float PixelsPerUnit = 64f;

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Circle() => Get("circle", (u, v) => InsideCircle(u, v) ? 1f : 0f);

        public static Sprite Ring(float thickness = 0.18f) =>
            Get("ring" + thickness, (u, v) =>
            {
                float d = Dist(u, v);
                return (d <= 0.5f && d >= 0.5f - thickness) ? 1f : 0f;
            });

        public static Sprite Square() => Get("square", (u, v) => 1f);

        /// <summary>A blunt teardrop, used for essence motes and projectiles.</summary>
        public static Sprite Mote() => Get("mote", (u, v) =>
        {
            float d = Dist(u, v);
            if (d > 0.5f) return 0f;
            return Mathf.Clamp01(1f - d * 1.4f);
        });

        /// <summary>Soft radial falloff for auras and glows.</summary>
        public static Sprite Glow() => Get("glow", (u, v) =>
        {
            float d = Dist(u, v) * 2f;
            return Mathf.Clamp01(1f - d * d);
        });

        /// <summary>The hero body: a capsule that reads as upright even at tiny sizes.</summary>
        public static Sprite Capsule() => Get("capsule", (u, v) =>
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float r = 0.62f;
            if (Mathf.Abs(y) <= 1f - r) return Mathf.Abs(x) <= r ? 1f : 0f;
            float dy = Mathf.Abs(y) - (1f - r);
            return (x * x + dy * dy) <= r * r ? 1f : 0f;
        });

        /// <summary>A spiked blob for monstrous appendages, so grafts look grown, not bolted on.</summary>
        public static Sprite Spiked(int spikes = 7) => Get("spiked" + spikes, (u, v) =>
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float d = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x);
            float edge = 0.72f + 0.26f * Mathf.Abs(Mathf.Cos(ang * spikes * 0.5f));
            return d <= edge ? 1f : 0f;
        });

        static bool InsideCircle(float u, float v) => Dist(u, v) <= 0.5f;

        static float Dist(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        delegate float Mask(float u, float v);

        static Sprite Get(string key, Mask mask)
        {
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[Resolution * Resolution];
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    // Supersample 2x2 so generated edges are not jagged.
                    float acc = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x + 0.25f + sx * 0.5f) / Resolution;
                            float v = (y + 0.25f + sy * 0.5f) / Resolution;
                            acc += Mathf.Clamp01(mask(u, v));
                        }
                    }
                    byte a = (byte)(Mathf.Clamp01(acc * 0.25f) * 255f);
                    pixels[y * Resolution + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, Resolution, Resolution),
                new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>Convenience: a configured SpriteRenderer child.</summary>
        public static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite,
            Color color, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
