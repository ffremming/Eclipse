using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Draws the one line of text this game says on its own: the name of the key the player has
    /// just found, above the middle of the screen, arriving and leaving again.
    ///
    /// <para>
    /// Built from code at runtime by the component that owns it, the same as the lantern and the
    /// weapon wheel, so there is no prefab or scene to keep in step with it. It decides nothing: it
    /// is told what to say and how strongly to say it, and it runs on whatever time the owner hands
    /// it.
    /// </para>
    /// <para>
    /// A shadow under the line rather than a panel behind it — see <see cref="HudLabel"/>, which is
    /// where that lives now that the stop screens write the same way.
    /// </para>
    /// </summary>
    public sealed class KeyBannerView : MonoBehaviour
    {
        private KeyBannerStyle style;
        private RectTransform line;
        private HudLabel label;
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

            label = HudLabel.Create(line, style.fontSize, style.ink, style.shadow,
                                    style.shadowOffset);

            Draw(string.Empty, 0f);
        }

        /// <summary>
        /// Says <paramref name="text"/> at <paramref name="strength"/>, 0 to 1. Called every frame
        /// by the owner, including while there is nothing to say.
        /// </summary>
        public void Draw(string text, float strength)
        {
            label.Say(text);
            label.SetStrength(strength);

            // Drifts up as it arrives and keeps drifting as it goes, so the line reads as
            // something passing through rather than as a label being switched on and off.
            line.anchoredPosition = restingPosition + Vector2.up * (style.drift * strength);
        }
    }
}
