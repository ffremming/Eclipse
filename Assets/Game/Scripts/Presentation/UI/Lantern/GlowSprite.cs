using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Makes the soft round glow the lantern is lit with: opaque at the middle, fading smoothly to
    /// nothing at the rim.
    ///
    /// <para>
    /// Generated for the same reason <see cref="RingSprite"/> is — the HUD is built from code and
    /// should have no art dependency. It is a separate shape because the ring's edge is deliberately
    /// crisp, and a glow with a crisp edge is a disc.
    /// </para>
    /// </summary>
    public static class GlowSprite
    {
        /// <summary>Texture edge length. The falloff is smooth, so it needs far fewer texels than a ring.</summary>
        private const int TextureSize = 128;

        /// <summary>How sharply the glow falls off. 1 is a straight ramp; higher hugs the middle.</summary>
        private const float Falloff = 2f;

        public static Sprite Create()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "LanternGlow"
            };

            float centre = (TextureSize - 1) * 0.5f;
            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Pow(Mathf.Clamp01(1f - distance), Falloff));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize),
                                       new Vector2(0.5f, 0.5f), TextureSize);
            sprite.name = texture.name;
            return sprite;
        }
    }
}
