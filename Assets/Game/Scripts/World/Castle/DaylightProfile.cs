using UnityEngine;

namespace SpaceGame.Castle
{
    /// <summary>
    /// What the world looks like once the lighthouse is lit: the far end of the only atmosphere
    /// change the game makes.
    /// <para>
    /// An asset rather than fields on a component, because this is the payoff of the whole castle
    /// and it will be tuned far more often than it is wired (<c>GDC-L1-ARCH-0001</c>). It also has
    /// to survive a rebuild of the world — <c>NatureWorldBuilder</c> replaces the scene wholesale,
    /// and a look tuned into a scene object would be thrown away with it.
    /// </para>
    /// <para>
    /// The eclipse end is NOT here. It is the values already on <see cref="SpaceGame.World.WorldAtmosphere"/>,
    /// which are the ones that have been tuned to make the dark world read, and copying them into a
    /// second asset would only create somewhere for them to disagree.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Daylight Profile", fileName = "DaylightProfile")]
    public sealed class DaylightProfile : ScriptableObject
    {
        [Header("Fog")]
        [Tooltip("Fog colour under a clear sky. Also written straight into the skybox's haze, so " +
                 "the two cannot drift apart — see WorldAtmosphere.")]
        public Color FogColor = new Color(0.62f, 0.68f, 0.78f);

        [Tooltip("Extinction per metre. The number that actually lifts the dark: the eclipse world " +
                 "runs around 0.0045, which swallows a torch inside 100 m.")]
        public float FogDensity = 0.0009f;

        public float FogBaseHeight = 6f;
        public float FogFalloff = 0.02f;

        [Tooltip("Metres of clear air before fog starts to build. Far out, so distance reads.")]
        public float FogStart = 60f;

        [Header("Ambient")]
        [Tooltip("Sky ambient. This is most of what makes the world look lit rather than merely " +
                 "less foggy — the eclipse world's is near black.")]
        public Color AmbientSky = new Color(0.42f, 0.48f, 0.60f);

        public Color AmbientEquator = new Color(0.34f, 0.34f, 0.33f);
        public Color AmbientGround = new Color(0.20f, 0.18f, 0.16f);

        [Header("Sun")]
        [Tooltip("The sun with nothing in front of it.")]
        public Color SunColour = new Color(1f, 0.96f, 0.88f);

        [Tooltip("Full daylight. The eclipse leaves it at 0.18, which lights nothing on purpose.")]
        public float SunIntensity = 2.1f;

        [Header("Sky")]
        public Color Zenith = new Color(0.23f, 0.40f, 0.72f);
        public Color Horizon = new Color(0.66f, 0.74f, 0.84f);

        [Tooltip("The disc itself, once it is no longer occluded. Bright, because it is now the sun.")]
        public Color Disc = new Color(1f, 0.97f, 0.86f);

        [Tooltip("What is left of the eclipse's ring, corona and red bleed. All zero: the eclipse " +
                 "is over, and leaving any of them up keeps a ring of fire round the sun.")]
        public float RingIntensity;
        public float CoronaStrength;
        public float BloodSky;

        [Header("Cloud")]
        [Tooltip("How much of the overcast pall is left.")]
        [Range(0f, 1f)] public float PallStrength = 0.15f;

        [Range(0f, 1f)] public float CloudCoverage = 0.34f;

        [Tooltip("Cloud body colour. White, where the eclipse world's is ash.")]
        public Color CloudBody = new Color(0.86f, 0.88f, 0.92f);

        [Tooltip("The red the eclipse painted on the clouds' undersides. Gone.")]
        public float CloudUndertoneStrength;
    }
}
