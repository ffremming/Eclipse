using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Makes the one thing in this game a player drags: a horizontal slider, built from code like
    /// everything else on screen.
    ///
    /// <para>
    /// Unity's <see cref="Slider"/> wants a specific child arrangement — a fill that stretches and
    /// a handle that slides in its own area — and getting it wrong gives a control that draws but
    /// does not move. That arrangement is the whole reason this is a factory rather than four lines
    /// at the call site.
    /// </para>
    /// </summary>
    public static class HudDial
    {
        /// <summary>
        /// Fills <paramref name="frame"/> with a dial running <paramref name="minimum"/> to
        /// <paramref name="maximum"/>. The frame is what positions and sizes it.
        /// </summary>
        public static Slider Create(RectTransform frame, float minimum, float maximum, float value,
                                    Color track, Color fill, Color handle, float handleWidth)
        {
            var slider = frame.gameObject.AddComponent<Slider>();

            Image trackFace = HudImage.Create(frame, "Track", track);
            trackFace.raycastTarget = true;
            Stretch((RectTransform)trackFace.transform);

            Image fillFace = HudImage.Create(frame, "Fill", fill);
            var fillRect = (RectTransform)fillFace.transform;
            Stretch(fillRect);

            // The handle's area is inset by half a handle at each end, which is what keeps the
            // handle inside the track at both extremes instead of hanging off them.
            var handleArea = new GameObject("Handle Area", typeof(RectTransform))
                             .GetComponent<RectTransform>();
            handleArea.SetParent(frame, false);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(handleWidth * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-handleWidth * 0.5f, 0f);

            Image handleFace = HudImage.Create(handleArea, "Handle", handle);
            handleFace.raycastTarget = true;
            var handleRect = (RectTransform)handleFace.transform;
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(handleWidth, 0f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleFace;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.SetValueWithoutNotify(value);

            return slider;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
