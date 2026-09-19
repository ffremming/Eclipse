using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Draws the one line of text this game has: the name of the key the player has just found,
    /// above the middle of the screen, arriving and leaving again.
    ///
    /// <para>
    /// Built from code at runtime by the component that owns it, the same as the lantern and the
    /// weapon wheel, so there is no prefab or scene to keep in step with it. It decides nothing: it
    /// is told what to say and how strongly to say it, and it runs on whatever time the owner hands
    /// it.
    /// </para>
    /// <para>
    /// A shadow under the line rather than a panel behind it. A panel would be the start of a HUD —
    /// a rectangle the game owns even when it is empty — and the offset shadow buys the same
    /// legibility over both the dark and the orb (<c>GDC-L1-UX-0003</c>: rank by salience, and do
    /// not let the unimportant hold the screen).
    /// </para>
    /// </summary>
    public sealed class KeyBannerView : MonoBehaviour
    {
        private KeyBannerStyle style;
        private RectTransform line;
        private Text ink;
        private Text shadow;
        private Vector2 restingPosition;

        /// <summary>Builds the banner's objects. Call once, before anything else.</summary>
        public void Build(KeyBannerStyle bannerStyle)
        {
            style = bannerStyle;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = style.sortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = style.referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            line = new GameObject("Key Banner", typeof(RectTransform)).GetComponent<RectTransform>();
            line.SetParent(transform, false);
            line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(style.width, style.fontSize * 2f);

            restingPosition = new Vector2(0f, style.heightAboveCentre);
            line.anchoredPosition = restingPosition;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // The shadow first, so the line itself draws over it.
            shadow = BuildText("Shadow", font, style.shadow,
                               new Vector2(style.shadowOffset, -style.shadowOffset));
            ink = BuildText("Ink", font, style.ink, Vector2.zero);

            Draw(string.Empty, 0f);
        }

        /// <summary>
        /// Says <paramref name="text"/> at <paramref name="strength"/>, 0 to 1. Called every frame
        /// by the owner, including while there is nothing to say.
        /// </summary>
        public void Draw(string text, float strength)
        {
            ink.text = text;
            shadow.text = text;

            SetAlpha(ink, style.ink, strength);
            SetAlpha(shadow, style.shadow, strength);

            // Drifts up as it arrives and keeps drifting as it goes, so the line reads as
            // something passing through rather than as a label being switched on and off.
            line.anchoredPosition = restingPosition + Vector2.up * (style.drift * strength);
        }

        private Text BuildText(string name, Font font, Color colour, Vector2 offset)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(line, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = offset;
            text.rectTransform.offsetMax = offset;

            text.raycastTarget = false;
            text.font = font;
            text.fontSize = style.fontSize;
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
