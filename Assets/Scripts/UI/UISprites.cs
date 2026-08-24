using UnityEngine;

namespace Graft.UI
{
    /// <summary>
    /// Generates the handful of flat shapes the HUD needs, rather than importing art for them.
    /// BUILD_PLAN.md keeps the project on primitives until Phase 6, and a circle is a primitive.
    ///
    /// They are also generated rather than drawn with a filled <c>Image</c> because a filled or
    /// radial <c>Image</c> with no sprite assigned silently ignores <c>fillAmount</c> and draws at
    /// full extent - no error, no warning. Every shape here carries a real texture.
    /// </summary>
    public static class UISprites
    {
        const int Resolution = 256;

        static Sprite _ring;
        static Sprite _disc;

        /// <summary>An unfilled circle whose stroke is a fraction of its radius.</summary>
        public static Sprite Ring => _ring != null ? _ring : _ring = Build("UI_Ring", 0.82f);

        /// <summary>A solid circle.</summary>
        public static Sprite Disc => _disc != null ? _disc : _disc = Build("UI_Disc", 0f);

        /// <param name="innerRadius">
        /// As a fraction of the outer radius. Zero gives a solid disc.
        /// </param>
        static Sprite Build(string name, float innerRadius)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var centre = (Resolution - 1) * 0.5f;
            var outer = centre;
            var inner = outer * innerRadius;
            // One pixel of feather on each edge. Without it a 256 px circle scaled up to fill a
            // thumb's worth of screen has visibly stepped edges.
            const float Feather = 1f;

            var pixels = new Color32[Resolution * Resolution];
            for (var y = 0; y < Resolution; y++)
            {
                for (var x = 0; x < Resolution; x++)
                {
                    var dx = x - centre;
                    var dy = y - centre;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    var alpha = Mathf.Clamp01((outer - distance) / Feather);
                    if (inner > 0f)
                    {
                        alpha = Mathf.Min(alpha, Mathf.Clamp01((distance - inner) / Feather));
                    }

                    pixels[y * Resolution + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, Resolution, Resolution),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
