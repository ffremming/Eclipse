using System;
using UnityEngine;
using SpaceGame.Gameplay;

namespace SpaceGame.Castle
{
    /// <summary>
    /// The core at the top of the tower. Hit it and the lighthouse lights.
    /// <para>
    /// An <see cref="IDamageable"/> rather than an <see cref="IInteractable"/>, and that is the
    /// design rather than an implementation detail. Eclipse is a fighting game whose one verb is
    /// the swing; making the biggest moment in it a swing means the player already knows how to do
    /// it and does not have to be taught a second kind of "use this" for a single object
    /// (<c>GDC-L1-DESIGN-0007</c> — maximise what each rule buys). It also costs nothing to wire:
    /// <c>MeleeStrike</c> finds targets with <c>GetComponentInParent&lt;IDamageable&gt;</c>, so the
    /// player's existing swing reaches this with no special case anywhere.
    /// </para>
    /// <para>
    /// It takes several blows rather than one. A single hit would fire on the incidental swing a
    /// player makes on arriving at the top, and the moment would happen before they knew they had
    /// caused it — which is the one thing that would waste it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerBeacon : MonoBehaviour, IDamageable
    {
        [Tooltip("Blows needed to light it. More than one so the player knows it was them.")]
        [SerializeField] private int blowsToLight = 3;

        [Tooltip("How brightly the core glows once lit. Below that it brightens with each blow, " +
                 "which is what tells the player that hitting it is working.")]
        [SerializeField] private float litIntensity = 20f;

        [Tooltip("The light in the core. Optional, but without it a struck beacon looks the same " +
                 "as an unstruck one until the world starts to change.")]
        [SerializeField] private Light core;

        [Tooltip("Colour the core burns with. The player's own light, not the eclipse's red — " +
                 "this is the one thing in the world that is going to undo the dark.")]
        [SerializeField] private Color coreColour = new Color(1f, 0.93f, 0.72f);

        /// <summary>Raised once, on the blow that lights it.</summary>
        public event Action Lit;

        /// <summary>Raised on every blow that lands before that one, with 0..1 of the way there.</summary>
        public event Action<float> Struck;

        /// <summary>Whether the beacon has been lit.</summary>
        public bool IsLit { get; private set; }

        private int blows;

        /// <summary>
        /// Always alive. <c>MeleeStrike</c> skips anything that reports itself dead, and a lit
        /// beacon that called itself dead would stop being hittable — which is fine, except that it
        /// would also stop the player's swing registering on it at all, so a player who arrives
        /// after the light is up would swing at a solid object and get nothing back.
        /// </summary>
        public bool Alive => true;

        private void Awake() => Show(0f);

        public void Damage(int amount)
        {
            // The number does not matter, only that a blow landed. A beacon that counted damage
            // would light faster for a player carrying a better weapon, which makes the biggest
            // moment in the game depend on loadout for no reason anyone would enjoy.
            if (IsLit || amount <= 0) return;

            blows++;
            float progress = Mathf.Clamp01(blows / (float)Mathf.Max(blowsToLight, 1));
            Show(progress);

            if (blows < blowsToLight)
            {
                Struck?.Invoke(progress);
                return;
            }

            IsLit = true;
            Lit?.Invoke();
        }

        /// <summary>How bright the core is, as the blows land and once it is lit.</summary>
        private void Show(float progress)
        {
            if (core == null) return;

            core.color = coreColour;

            // Squared, so the first blows barely show and the last one jumps. A linear ramp makes
            // the third blow look like more of the same rather than like the thing that did it.
            core.intensity = litIntensity * progress * progress;
        }
    }
}
