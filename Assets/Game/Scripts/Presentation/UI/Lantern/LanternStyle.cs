using System;
using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// How the lantern looks and how it reacts. Lives on the component that owns the lantern so it is
    /// tuned in the Inspector; the defaults are the look it was built with.
    ///
    /// <para>
    /// Distances are in reference pixels of a 1920x1080 canvas, not screen pixels, so the lantern is
    /// the same fraction of the screen at any resolution.
    /// </para>
    /// </summary>
    [Serializable]
    public class LanternStyle
    {
        [Header("Shape")]
        [Tooltip("Width of the lantern, handle and all.")]
        public float width = 120f;

        [Tooltip("Height of the lantern, handle and all.")]
        public float height = 210f;

        [Tooltip("Gap between the lantern and the right and bottom edges of the screen.")]
        public Vector2 margin = new Vector2(56f, 48f);

        [Header("Colours")]
        public Color frame = new Color(0.1f, 0.09f, 0.08f, 0.95f);
        public Color glass = new Color(0.04f, 0.04f, 0.05f, 0.55f);
        public Color light = new Color(1f, 0.62f, 0.2f, 0.95f);
        public Color surface = new Color(1f, 0.9f, 0.6f, 1f);
        public Color core = new Color(1f, 0.85f, 0.55f, 0.9f);
        public Color halo = new Color(1f, 0.6f, 0.2f, 0.45f);

        [Header("Motion")]
        [Tooltip("How much of the lantern the level moves per second when it changes. Slow enough that a " +
                 "hit is seen draining rather than simply being different.")]
        public float settleSpeed = 1.2f;

        [Tooltip("How much bigger the lantern swells when light is taken back, at the moment it is taken.")]
        public float gainSwell = 0.12f;

        [Tooltip("How far the lantern is jolted, in reference pixels, when light is lost.")]
        public float lossJolt = 14f;

        [Tooltip("How quickly the swell and the jolt die away, per second.")]
        public float reactionDecay = 6f;

        [Header("Running low")]
        [Tooltip("Below this fraction of a full lantern the light starts to gutter.")]
        [Range(0f, 1f)] public float flickerBelow = 0.2f;

        [Tooltip("How much of its brightness a gutter takes away at the darkest.")]
        [Range(0f, 1f)] public float flickerDepth = 0.5f;

        [Tooltip("How fast the gutter moves.")]
        public float flickerSpeed = 9f;

        [Header("Canvas")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Tooltip("Below the weapon wheel, so the wheel is never drawn under the lantern.")]
        public int sortingOrder = 100;
    }
}
