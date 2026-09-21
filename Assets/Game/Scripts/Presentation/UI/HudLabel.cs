using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// A line of text every code-built HUD element writes with: the words, and an offset copy of
    /// them in shadow underneath.
    ///
    /// <para>
    /// The shadow rather than a panel behind the text is the house style — a panel would be the
    /// start of a HUD, a rectangle the game owns even when it is empty, and the offset copy buys
    /// the same legibility over the dark and over a lit orb both (<c>GDC-L1-UX-0003</c>).
    /// </para>
    /// <para>
    /// Never a raycast target, the same as <see cref="HudImage"/>: words are read, not clicked, and
    /// a label that caught the pointer would swallow a click meant for the button under it.
    /// </para>
    /// </summary>
    public sealed class HudLabel
    {
        private readonly Text ink;
        private readonly Text shadow;
        private readonly Color inkColour;
        private readonly Color shadowColour;

        private HudLabel(Text ink, Text shadow)
        {
            this.ink = ink;
            this.shadow = shadow;

            inkColour = ink.color;
            shadowColour = shadow.color;
        }

        /// <summary>
        /// Fills <paramref name="frame"/> with a centred line. The frame is what positions and
        /// sizes it; this only ever draws inside one.
        /// </summary>
        public static HudLabel Create(RectTransform frame, int fontSize, Color ink, Color shadow,
                                      float shadowOffset)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // The shadow first, so the line itself draws over it.
            Text shadowText = CreateText(frame, "Shadow", font, fontSize, shadow,
                                         new Vector2(shadowOffset, -shadowOffset));
            Text inkText = CreateText(frame, "Ink", font, fontSize, ink, Vector2.zero);

            return new HudLabel(inkText, shadowText);
        }

        /// <summary>Says <paramref name="text"/>. Safe to call every frame with the same words.</summary>
        public void Say(string text)
        {
            ink.text = text;
            shadow.text = text;
        }

        /// <summary>
        /// How strongly the line is said, 0 to 1, against the colours it was built with. Both
        /// copies fade together, so the shadow never outlives the words it is under.
        /// </summary>
        public void SetStrength(float strength)
        {
            SetAlpha(ink, inkColour, strength);
            SetAlpha(shadow, shadowColour, strength);
        }

        private static Text CreateText(RectTransform frame, string name, Font font, int fontSize,
                                       Color colour, Vector2 offset)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(frame, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = offset;
            text.rectTransform.offsetMax = offset;

            text.raycastTarget = false;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = colour;
            return text;
        }

        private static void SetAlpha(Graphic graphic, Color colour, float strength)
            => graphic.color = new Color(colour.r, colour.g, colour.b, colour.a * strength);
    }
}
