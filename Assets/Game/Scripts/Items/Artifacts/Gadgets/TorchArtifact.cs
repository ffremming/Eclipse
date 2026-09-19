using UnityEngine;
using SpaceGame.World;

namespace SpaceGame.Items
{
    /// <summary>
    /// The carried torch: the player's own light, and a club you can swing.
    /// <para>
    /// Built on <see cref="LightWeapon"/> rather than beside it. A torch that flares when swung and
    /// drags a trail of light is describing exactly what that base already does — the only things
    /// that make it a torch rather than a sword are that it burns while it is merely being carried,
    /// and that it has a flame on the end. Sharing the base is also what stops the torch's swing
    /// drifting out of step with the sword's over time.
    /// </para>
    /// <para>
    /// It has no on/off any more. One press is one swing, and a single Use action cannot mean both
    /// "hit them" and "change setting" without one of the two being wrong half the time. The torch
    /// is always lit; swinging stokes it, and the stoke decays on the base's own flare curve.
    /// </para>
    /// </summary>
    public class TorchArtifact : LightWeapon
    {
        /// <summary>Material property the flame shader reads to know how hard it is burning.</summary>
        private static readonly int StokeId = Shader.PropertyToID("_Stoke");

        [Header("Flame")]
        [Tooltip("The mesh drawn with SpaceGame/Light/LightFlame. Its _Stoke is driven by the swing.")]
        [SerializeField] private Renderer flame;

        [Tooltip("The ball of light thrown off at the moment of a swing. Scaled and hidden by the " +
                 "flare, so it exists only for the length of the burst.")]
        [SerializeField] private Renderer burst;

        [Tooltip("How wide the burst grows at the peak of a swing, in metres.")]
        [SerializeField] private float burstSize = 1.4f;

        [Header("Fill light")]
        [Tooltip("The wide, weak, shadowless light that lifts the area around the player off black.")]
        [SerializeField] private Light fill;

        [SerializeField] private float fillIdleIntensity = 15f;
        [SerializeField] private float fillSwingIntensity = 48f;

        [Header("Throw light")]
        [Tooltip("The long spot that aims where the player looks. This is what actually gives the " +
                 "torch distance: a point light spreads its energy over a whole sphere, so it dies " +
                 "within a few metres no matter how far its range is set, while a cone puts the " +
                 "same energy down one direction and carries it many times further.")]
        [SerializeField] private Light throwLight;

        [SerializeField] private float throwIdleIntensity = 260f;
        [SerializeField] private float throwSwingIntensity = 700f;

        [Tooltip("How fast the throw swings round to where the player is looking, in degrees per " +
                 "second. Instant tracking reads as a head-mounted lamp; a slight lag reads as an " +
                 "arm holding something heavy.")]
        [SerializeField] private float throwTurnSpeed = 520f;

        [Header("Swing")]
        [Tooltip("How far ahead of the holder the swing connects, in metres.")]
        [SerializeField] private float reach = 1.5f;

        [Tooltip("Radius of the swing, in metres.")]
        [SerializeField] private float arcRadius = 1.3f;

        [Tooltip("How high off the holder's feet the swing sits, in metres.")]
        [SerializeField] private float sweepHeight = 1.1f;

        [Header("Ash")]
        [Tooltip("Metres beyond the light's range that the ash motes still react, so the air ahead " +
                 "hints at the reach before the ground does.")]
        [SerializeField] private float ashReachBonus = 4f;

        private WorldAtmosphere atmosphere;
        private Camera view;
        private MaterialPropertyBlock flameBlock;

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);

            // Found on equip rather than per frame. Equipping is rare, and the atmosphere cannot
            // change while the torch is in a hand.
            atmosphere = FindFirstObjectByType<WorldAtmosphere>();
            view = Camera.main;
            flameBlock = new MaterialPropertyBlock();
        }

        public override void OnUnequipped(GameObject holder)
        {
            // Stop the motes reacting to a torch that is no longer being carried. Without this they
            // keep glowing around the last place it was held.
            if (atmosphere != null) atmosphere.TrackLight(null, 0f);
            atmosphere = null;

            base.OnUnequipped(holder);
        }

        /// <summary>
        /// Where the swing connects. Flattened to the horizontal, so looking at the sky does not
        /// lift the swing over the head of whatever is standing in front of the player.
        /// </summary>
        protected override void GetSweep(out Vector3 centre, out float radius)
        {
            radius = arcRadius;

            if (owner == null)
            {
                centre = transform.position;
                return;
            }

            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : owner.transform.forward;

            centre = owner.transform.position + forward * reach + Vector3.up * sweepHeight;
        }

        /// <summary>
        /// Drives the flame and the ash from the same flare the base drives the light with, so all
        /// three peak on the same frame instead of on three timers that slowly disagree.
        /// </summary>
        protected override void OnFlareChanged(float flare)
        {
            if (flame != null && flameBlock != null)
            {
                flame.GetPropertyBlock(flameBlock);
                flameBlock.SetFloat(StokeId, flare);
                flame.SetPropertyBlock(flameBlock);
            }

            if (burst != null)
            {
                // Grown from nothing rather than faded from full: a burst that starts at full size
                // and dims reads as a light being switched off, where one that expands reads as
                // something being thrown outwards.
                bool visible = flare > 0.01f;
                if (burst.enabled != visible) burst.enabled = visible;
                if (visible) burst.transform.localScale = Vector3.one * (burstSize * flare);
            }

            if (fill != null)
            {
                fill.intensity = Mathf.Lerp(fillIdleIntensity, fillSwingIntensity, flare);
            }

            AimThrow(flare);

            if (atmosphere != null && TipLight != null)
            {
                // The ash reacts to the THROW's range, not the flame's, because the throw is what
                // the player perceives as how far their light goes.
                float reach = throwLight != null ? throwLight.range : TipLight.range;
                atmosphere.TrackLight(TipLight.transform, reach + ashReachBonus);
            }
        }

        /// <summary>
        /// Points the throw down the holder's aim.
        /// <para>
        /// Deliberately not parented to the hand. A cone welded to a swinging torch sprays its light
        /// across the sky every time the arm moves, which reads as a strobe rather than as
        /// illumination — and during an attack that is exactly when the player most needs to see.
        /// Keeping it on the aim instead means the swing changes how BRIGHT the world is without
        /// changing WHERE the player can see.
        /// </para>
        /// </summary>
        private void AimThrow(float flare)
        {
            if (throwLight == null) return;

            throwLight.intensity = Mathf.Lerp(throwIdleIntensity, throwSwingIntensity, flare);

            // The view direction, because the throw exists so the player can see where they are
            // looking. On a third-person boom that is the camera's forward, not the body's — the
            // body lags the camera through every turn, and a lamp that lags the turn is a lamp that
            // is always pointing at what the player has just stopped looking at.
            Vector3 forward = view != null ? view.transform.forward
                                           : (owner != null ? owner.transform.forward : transform.forward);

            throwLight.transform.rotation = Quaternion.RotateTowards(
                throwLight.transform.rotation,
                Quaternion.LookRotation(forward),
                throwTurnSpeed * Time.deltaTime);
        }
    }
}
