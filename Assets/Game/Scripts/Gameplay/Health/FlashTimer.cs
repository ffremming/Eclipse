// How strongly a hit flash is showing, moment to moment.
//
// Split from DamageFlash so the one decision worth pinning — a second hit restarts the flash rather
// than being swallowed by the one already running — is a plain class a test can drive without a
// scene, a renderer or a frame.
using UnityEngine;

namespace SpaceGame.Gameplay
{
    public class FlashTimer
    {
        private readonly float duration;
        private float elapsed;

        public FlashTimer(float duration)
        {
            this.duration = Mathf.Max(duration, 0.0001f);
            elapsed = this.duration;
        }

        /// <summary>True from a hit until the flash has faded out.</summary>
        public bool Active => elapsed < duration;

        /// <summary>1 at the moment of a hit, fading linearly to 0 as the flash runs out.</summary>
        public float Strength => Active ? 1f - elapsed / duration : 0f;

        /// <summary>A hit. Restarts the flash at full strength, whatever was left of the last one.</summary>
        public void Trigger() => elapsed = 0f;

        public void Advance(float deltaTime)
        {
            if (Active) elapsed += deltaTime;
        }
    }
}
