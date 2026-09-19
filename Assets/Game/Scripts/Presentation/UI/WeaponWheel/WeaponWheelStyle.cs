using System;
using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// How the weapon wheel looks and how fast it reacts. Lives on the component that owns the
    /// wheel so it is tuned in the Inspector; the defaults are the look the wheel was built with.
    ///
    /// <para>
    /// Distances are in reference pixels of a 1920x1080 canvas, not screen pixels — the canvas scales
    /// with the window, so the wheel is the same fraction of the screen everywhere.
    /// </para>
    /// </summary>
    [Serializable]
    public class WeaponWheelStyle
    {
        [Header("Shape")]
        [Tooltip("Radius of the dial's outer rim.")]
        public float outerRadius = 270f;

        [Tooltip("Radius of the hole in the middle, where the hub sits.")]
        public float innerRadius = 105f;

        [Tooltip("Empty angle left between neighbouring wedges, so they read as separate buttons.")]
        public float gapDegrees = 3f;

        [Tooltip("Clear space between the hub and the ring around it, in reference pixels.")]
        public float hubGap = 6f;

        [Tooltip("How much a hovered wedge grows.")]
        public float hoverScale = 1.06f;

        [Tooltip("Diameter of the dot that shows where the pointer is.")]
        public float pointerDotSize = 16f;

        [Tooltip("Height of an item's icon, for items that have one.")]
        public float iconSize = 64f;

        [Header("Type")]
        public int labelFontSize = 26;
        public int hubFontSize = 34;

        [Header("Colours")]
        public Color backdrop = new Color(0f, 0f, 0f, 0.4f);
        public Color slot = new Color(0.07f, 0.07f, 0.09f, 0.78f);
        public Color slotHovered = new Color(0.95f, 0.72f, 0.28f, 0.95f);
        public Color slotEquipped = new Color(0.22f, 0.2f, 0.3f, 0.9f);
        public Color slotEmpty = new Color(0.07f, 0.07f, 0.09f, 0.3f);
        public Color hub = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        public Color text = new Color(0.94f, 0.92f, 0.86f, 1f);
        public Color textHovered = new Color(0.08f, 0.06f, 0.03f, 1f);
        public Color textEmpty = new Color(0.94f, 0.92f, 0.86f, 0.35f);

        [Header("Motion")]
        [Tooltip("How quickly a wedge's colour and size settle on their target, per second. Higher is snappier.")]
        public float settleSpeed = 20f;

        [Tooltip("How quickly the whole wheel fades in and out, per second.")]
        public float fadeSpeed = 14f;

        [Header("Canvas")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Tooltip("Above every other canvas, so nothing draws over the wheel.")]
        public int sortingOrder = 200;
    }
}
