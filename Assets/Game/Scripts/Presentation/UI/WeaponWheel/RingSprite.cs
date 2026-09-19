using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Makes the one shape the weapon wheel is drawn from: an anti-aliased ring, or a solid disc when
    /// the hole has no size.
    ///
    /// <para>
    /// Generated rather than imported so the wheel has no art dependency and can be built entirely
    /// from code. A radial-filled Image clips this ring into a wedge, so one texture serves every
    /// slot, the hub and the pointer dot.
    /// </para>
    /// </summary>
    public static class RingSprite
    {
        /// <summary>Texture edge length. Large enough that the rim stays smooth at the wheel's biggest.</summary>
        private const int TextureSize = 512;

        /// <summary>How many texels the edge is softened over. One is a hard alias, three is blurry.</summary>
        private const float EdgeSoftness = 1.5f;

        /// <param name="innerFraction">Hole radius as a fraction of the outer radius. 0 gives a disc.</param>
        public static Sprite Create(float innerFraction)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = innerFraction > 0f ? "WheelRing" : "WheelDisc"
            };

            float outer = TextureSize * 0.5f - EdgeSoftness;
            float inner = outer * Mathf.Clamp01(innerFraction);
            float centre = (TextureSize - 1) * 0.5f;

            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));

                    float insideOuter = Mathf.Clamp01((outer - distance) / EdgeSoftness + 0.5f);
                    float outsideInner = inner <= 0f ? 1f : Mathf.Clamp01((distance - inner) / EdgeSoftness + 0.5f);

                    byte alpha = (byte)Mathf.RoundToInt(255f * insideOuter * outsideInner);
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

        /// <summary>Frees a sprite made here together with the texture behind it.</summary>
        public static void Destroy(Sprite sprite)
        {
            if (sprite == null) return;

            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);
            Object.Destroy(texture);
        }
    }
}
