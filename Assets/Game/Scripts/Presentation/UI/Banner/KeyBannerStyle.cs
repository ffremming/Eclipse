using System;
using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// How the key banner looks and how long it lasts. Lives on the component that owns it so it is
    /// tuned in the Inspector; the defaults are the look it was built with.
    ///
    /// <para>
    /// Distances are in reference pixels of a 1920x1080 canvas, not screen pixels, so the line is
    /// the same fraction of the screen at any resolution.
    /// </para>
    /// </summary>
    [Serializable]
    public class KeyBannerStyle
    {
        [Header("Place")]
        [Tooltip("How far above the middle of the screen the line sits. Above centre rather than on " +
                 "it, so it does not land over whatever the player is looking at.")]
        public float heightAboveCentre = 210f;

        [Tooltip("Width the line is laid out in. Wide enough for the longest key's name on one row.")]
        public float width = 1100f;

        [Tooltip("Size of the line, in reference pixels.")]
        public int fontSize = 58;

        [Header("Colours")]
        [Tooltip("The line itself: the same warm light the lantern burns with.")]
        public Color ink = new Color(1f, 0.93f, 0.78f, 1f);

        [Tooltip("The shadow behind it, which is what keeps the line legible over a bright orb as " +
                 "well as over the dark.")]
        public Color shadow = new Color(0f, 0f, 0f, 0.75f);

        [Tooltip("How far the shadow is offset, in reference pixels.")]
        public float shadowOffset = 3f;

        [Header("Timing")]
        [Tooltip("Seconds the line takes to come up.")]
        public float riseSeconds = 0.45f;

        [Tooltip("Seconds it stays. Long enough to read twice without becoming furniture.")]
        public float holdSeconds = 3.2f;

        [Tooltip("Seconds it takes to go away.")]
        public float fallSeconds = 1.1f;

        [Tooltip("How far the line drifts upward over its life, in reference pixels. Small: it is " +
                 "the difference between text appearing and something arriving.")]
        public float drift = 26f;

        [Header("Canvas")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Tooltip("Above the lantern and below the weapon wheel — it is the loudest thing on screen " +
                 "while it is up, but the wheel is a thing the player is holding open.")]
        public int sortingOrder = 150;
    }
}
