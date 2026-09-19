using System;
using UnityEngine;

namespace SpaceGame.Castle
{
    /// <summary>
    /// The numbers behind how a locked door looks: what colour it glows and how brightly, given
    /// whether the player can open it and whether it has just refused them.
    /// <para>
    /// This exists because Eclipse has no HUD — no crosshair, no interaction prompt, no message
    /// line. The convention <c>GDC-L1-UX-0004</c> leans on, "a button prompt tells you this is
    /// usable", is simply not available here, so the door itself has to be the signifier. It
    /// carries three states the player has to be able to tell apart without any text:
    /// </para>
    /// <list type="bullet">
    /// <item>locked, and you do not have the key — a slow breath in the eclipse's red;</item>
    /// <item>locked, and you DO — steady and bright in the colour of the player's own light, which
    /// is the only saturated colour in the world and so the one the eye already goes to;</item>
    /// <item>just refused you — a hard flash that decays.</item>
    /// </list>
    /// <para>
    /// On top of those three sits a fourth thing, which is not a state but an answer to where the
    /// player is looking: a door the crosshair is on brightens. That is the closest this game gets
    /// to an interaction prompt, and it is the signifier <c>GDC-L1-UX-0004</c> asks for — "this is
    /// a thing you can work, from here, now". It rides on BRIGHTNESS alone and deliberately leaves
    /// colour and breathing untouched, because those two already carry a different sentence. A
    /// highlight that also steadied the breath would make a locked door you are looking at read
    /// exactly like a door you have the key for, which is one channel saying two things
    /// (<c>GDC-L1-UX-0003</c>).
    /// </para>
    /// <para>
    /// The refusal is the part that is easy to leave out and the part that matters most. A door
    /// that silently does nothing when the player presses Use is indistinguishable from a door
    /// that is not interactive, from a broken key check, and from an input that never arrived —
    /// <c>GDC-L1-UX-0004</c>'s own note that a signified action must also RESPOND
    /// (<c>GDC-L1-FEEL-0002</c>). So a refusal is louder than either resting state, which is what
    /// <c>LockSignalTests</c> pins down.
    /// </para>
    /// </summary>
    public readonly struct LockSignal
    {
        public readonly Color Colour;
        public readonly float Intensity;

        public LockSignal(Color colour, float intensity)
        {
            Colour = colour;
            Intensity = intensity;
        }

        /// <summary>
        /// What the lock should look like this frame.
        /// </summary>
        /// <param name="hasKey">Whether the player is carrying what this door wants.</param>
        /// <param name="aim01">
        /// How far the door is under the player's crosshair, 0 to 1. Eased by the caller rather
        /// than a bare bool, so the door comes up and dies away instead of snapping — a hard cut
        /// at the edge of the interaction range makes the light flicker as the player turns.
        /// </param>
        /// <param name="time">Scene time, for the breathing.</param>
        /// <param name="sinceRefusal">
        /// Seconds since the door last turned the player away, or <see cref="float.PositiveInfinity"/>
        /// if it never has.
        /// </param>
        public static LockSignal Evaluate(bool hasKey, float aim01, float time, float sinceRefusal,
                                          in LockSignalTuning tuning)
        {
            Color resting = hasKey ? tuning.OpenColour : tuning.LockedColour;

            // An unlocked door sits still and an unopenable one breathes. The stillness is the
            // signal: among a world of slowly pulsing red, the one steady turquoise thing is
            // legible as "this one is different" before the player works out why.
            float breath = hasKey
                ? 1f
                : Mathf.Lerp(1f - tuning.BreathDepth, 1f,
                             Mathf.Cos(time * Mathf.PI * 2f / Mathf.Max(tuning.BreathPeriod, 0.0001f))
                             * 0.5f + 0.5f);

            // Multiplied through the breath rather than added to it, so a locked door under the
            // crosshair still breathes — it is the same door, said louder. Adding a constant would
            // flatten the breath as the player closed in, which is the one thing that tells them
            // they still cannot open it.
            float attention = Mathf.Lerp(1f, Mathf.Max(tuning.AimIntensityScale, 1f),
                                         Mathf.Clamp01(aim01));

            float intensity = tuning.RestingIntensity * breath * attention;
            Color colour = resting;

            float flash = Flash(sinceRefusal, tuning.RefusalSeconds);
            if (flash > 0f)
            {
                // Towards the refusal colour and well past the resting brightness, so the answer
                // reads as an answer rather than as a flicker in the breathing.
                colour = Color.Lerp(resting, tuning.RefusalColour, flash);
                intensity = Mathf.Lerp(intensity, tuning.RefusalIntensity, flash);
            }

            return new LockSignal(colour, intensity);
        }

        /// <summary>
        /// How much of the refusal flash is left, 1 at the moment of refusal down to 0.
        /// <para>
        /// Squared rather than linear so it snaps: a refusal wants a hard front edge and a quick
        /// tail, which is what makes it read as the door answering back. A linear decay over the
        /// same duration reads as a slow glow, which is a different sentence.
        /// </para>
        /// </summary>
        private static float Flash(float sinceRefusal, float refusalSeconds)
        {
            if (float.IsNaN(sinceRefusal) || sinceRefusal < 0f) return 0f;

            float window = Mathf.Max(refusalSeconds, 0.0001f);
            if (sinceRefusal >= window) return 0f;

            float remaining = 1f - sinceRefusal / window;
            return remaining * remaining;
        }
    }

    /// <summary>
    /// The look of a lock, as Inspector fields. Serialized on the component rather than baked into
    /// <see cref="LockSignal"/> so the two doors in the game can read differently without a second
    /// copy of the maths.
    /// </summary>
    [Serializable]
    public struct LockSignalTuning
    {
        [Tooltip("The colour of a door you cannot open. The eclipse's red: the world's own " +
                 "'no', already used by the ring in the sky.")]
        public Color LockedColour;

        [Tooltip("The colour of a door you are carrying the key for. The player's own light, " +
                 "which is the only saturated colour in the world that means 'yours'.")]
        public Color OpenColour;

        [Tooltip("The colour of the flash a door gives when it turns the player away.")]
        public Color RefusalColour;

        [Tooltip("How bright the lock sits when nothing is happening.")]
        public float RestingIntensity;

        [Tooltip("How bright the refusal flash peaks. Must be well above the resting intensity or " +
                 "the refusal is invisible, which in a game with no HUD means the door said nothing.")]
        public float RefusalIntensity;

        [Tooltip("How much brighter a door gets while the player is looking straight at it, as a " +
                 "multiple of its resting brightness. Below 1 is treated as 1 — a highlight that " +
                 "dimmed the door would be telling the player the opposite of what it means. Must " +
                 "also stay well under the refusal, or an answer looks like a glance.")]
        public float AimIntensityScale;

        [Tooltip("Seconds the refusal flash takes to die away.")]
        public float RefusalSeconds;

        [Tooltip("Seconds for one breath of a locked door.")]
        public float BreathPeriod;

        [Tooltip("How far the breath dips, 0 to 1. At 0 a locked door is as steady as an open one, " +
                 "which throws away the difference between them.")]
        [Range(0f, 1f)] public float BreathDepth;

        /// <summary>
        /// Values that read correctly against the corrupt sky, for a component that has not been
        /// tuned yet. Not a substitute for tuning — a lock is lit against whatever it is standing
        /// on, and the castle's stone is nearly black.
        /// </summary>
        public static LockSignalTuning Default => new LockSignalTuning
        {
            LockedColour = new Color(1f, 0.13f, 0.06f),
            OpenColour = new Color(0.45f, 0.95f, 0.9f),
            RefusalColour = new Color(1f, 0.25f, 0.12f),
            RestingIntensity = 1.6f,
            RefusalIntensity = 9f,
            AimIntensityScale = 2.4f,
            RefusalSeconds = 0.55f,
            BreathPeriod = 3.2f,
            BreathDepth = 0.45f,
        };
    }
}
