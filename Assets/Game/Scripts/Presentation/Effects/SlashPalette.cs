namespace SpaceGame.Presentation
{
    /// <summary>
    /// Which colours a slash of light is drawn in. Read by the LightSlash shader as a 0/1 toggle,
    /// so the numbers are part of that contract.
    /// </summary>
    public enum SlashPalette
    {
        /// <summary>The four-hue slash: blue, white, orange and red.</summary>
        Spectrum = 0,

        /// <summary>The orb's own ramp — turquoise fringe, orange body, white core — and nothing else.</summary>
        Orb = 1,
    }
}
