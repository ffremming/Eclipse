using System;
using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// How a <see cref="StopScreenView"/> looks. Lives on the component that owns it so it is tuned
    /// in the Inspector; the defaults are the look it was built with.
    ///
    /// <para>
    /// Distances are in reference pixels of a 1920x1080 canvas, not screen pixels, so the screen is
    /// the same fraction of the display at any resolution — the same as the key banner.
    /// </para>
    /// </summary>
    [Serializable]
    public class StopScreenStyle
    {
        [Header("Veil")]
        [Tooltip("The dim over the world. Not opaque: the world the player stopped in stays behind " +
                 "the screen, so a death reads as having happened somewhere rather than as a " +
                 "cut to a menu.")]
        public Color veil = new Color(0.02f, 0.02f, 0.03f, 0.78f);

        [Tooltip("Seconds the veil and the words take to arrive. Slow enough that the world is " +
                 "still readable underneath as it comes.")]
        public float fadeSeconds = 0.55f;

        [Header("Title")]
        [Tooltip("Size of the line at the top, in reference pixels.")]
        public int titleFontSize = 76;

        [Tooltip("How far above the middle of the screen the title sits.")]
        public float titleHeight = 180f;

        [Tooltip("The title itself: the same warm light the lantern burns with.")]
        public Color titleInk = new Color(1f, 0.93f, 0.78f, 1f);

        [Header("Choices")]
        [Tooltip("Size of a button's label, in reference pixels.")]
        public int choiceFontSize = 38;

        [Tooltip("Size of a button, in reference pixels.")]
        public Vector2 choiceSize = new Vector2(360f, 74f);

        [Tooltip("Gap between one button and the next, in reference pixels.")]
        public float choiceSpacing = 18f;

        [Tooltip("Where the first button sits relative to the middle of the screen.")]
        public float choiceTop = -20f;

        [Tooltip("A button at rest. Nearly nothing: an outline is enough when the screen behind " +
                 "it is this dark.")]
        public Color choiceFill = new Color(1f, 0.93f, 0.78f, 0.08f);

        [Tooltip("A button under the pointer.")]
        public Color choiceHoverFill = new Color(1f, 0.93f, 0.78f, 0.22f);

        [Tooltip("A button's label.")]
        public Color choiceInk = new Color(1f, 0.95f, 0.86f, 0.92f);

        [Header("Dial")]
        [Tooltip("Where a dial sits relative to the middle of the screen, when a screen has one. " +
                 "Above the choices, because it is a thing to adjust before deciding rather than " +
                 "a third decision.")]
        public float dialHeight = 70f;

        [Tooltip("Size of a dial's bar, in reference pixels.")]
        public Vector2 dialSize = new Vector2(360f, 12f);

        [Tooltip("Size of a dial's caption, in reference pixels.")]
        public int dialFontSize = 26;

        [Tooltip("Gap between a dial's caption and its bar, in reference pixels.")]
        public float dialCaptionGap = 30f;

        [Tooltip("The unfilled part of a dial's bar.")]
        public Color dialTrack = new Color(1f, 0.93f, 0.78f, 0.12f);

        [Tooltip("The filled part, left of the handle.")]
        public Color dialFill = new Color(1f, 0.93f, 0.78f, 0.45f);

        [Tooltip("The handle itself, which is the part the player is actually aiming at.")]
        public Color dialHandle = new Color(1f, 0.95f, 0.86f, 0.92f);

        [Tooltip("Width of the handle, in reference pixels. Wide enough to hit without care.")]
        public float dialHandleWidth = 22f;

        [Header("Shadow")]
        [Tooltip("The shadow behind every line, which is what keeps words legible over a lit " +
                 "world as well as over the dark.")]
        public Color shadow = new Color(0f, 0f, 0f, 0.75f);

        [Tooltip("How far the shadow is offset, in reference pixels.")]
        public float shadowOffset = 3f;

        [Header("Canvas")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Tooltip("Above everything else the game draws. The wheel is at 200 and the key banner at " +
                 "150; a stopped game outranks both, because neither can be acted on while it is up.")]
        public int sortingOrder = 400;
    }
}
