using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// One setting of the torch: how far it reaches, how hard it burns, and how much it wavers.
    /// <para>
    /// A serialized list of these rather than a min and a max with a slider between them, because
    /// the settings are not points on one line. A low torch is not merely a dim bright torch — it
    /// is redder, steadier and much shorter-ranged, and the player is meant to feel those as
    /// different tools rather than as one dial. Discrete settings are also what makes the choice
    /// legible: the player knows which of three states they are in, where a continuous dial leaves
    /// them guessing whether it is worth turning up.
    /// </para>
    /// </summary>
    [System.Serializable]
    public class TorchLevel
    {
        [Tooltip("Shown to the player when the setting changes.")]
        [SerializeField] private string displayName = "Low";

        [Tooltip("How far the light reaches, in metres.")]
        [SerializeField] private float range = 8f;

        [Tooltip("Brightness of the light at this setting.")]
        [SerializeField] private float intensity = 1.6f;

        [Tooltip("Colour of the light. Low settings sit redder, high settings whiter — the same " +
                 "walk from ember to core the light shaders make.")]
        [SerializeField] private Color colour = new Color(1f, 0.52f, 0.20f);

        [Tooltip("How far the brightness wanders, as a fraction of intensity. A steady flame reads " +
                 "as electric; too much wander reads as about to go out.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float flicker = 0.08f;

        [Tooltip("How fast the wander moves, in cycles per second.")]
        [SerializeField] private float flickerSpeed = 7f;

        /// <summary>
        /// Used for the authored defaults on <see cref="TorchArtifact"/>. Those defaults are what a
        /// freshly built prefab serializes, so they are the values the torch actually ships with
        /// until someone retunes it in the Inspector.
        /// </summary>
        public TorchLevel(string displayName, float range, float intensity, Color colour,
                          float flicker, float flickerSpeed)
        {
            this.displayName = displayName;
            this.range = range;
            this.intensity = intensity;
            this.colour = colour;
            this.flicker = flicker;
            this.flickerSpeed = flickerSpeed;
        }

        public string DisplayName => displayName;
        public float Range => range;
        public float Intensity => intensity;
        public Color Colour => colour;
        public float Flicker => flicker;
        public float FlickerSpeed => flickerSpeed;
    }
}
