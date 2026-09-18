using UnityEngine;
using SpaceGame.Gameplay;

namespace SpaceGame.Items
{
    /// <summary>
    /// What every light weapon has in common: it glows while carried, it flares when swung, and it
    /// drags a trail of light behind the part of it that does the damage.
    /// <para>
    /// The base exists so the weapons cannot drift apart. Three weapons each owning their own copy
    /// of "ramp a light, drive a trail, sweep for hits" is three chances for one of them to settle
    /// at a different brightness or a different trail length, and a family that no longer looks like
    /// a family. The same argument as the shared light palette, one level up: subclasses choose
    /// timings and reach, never how the light behaves.
    /// </para>
    /// <para>
    /// The idle glow is not decoration. In a world this dark the weapon in your hand would otherwise
    /// be invisible, and a melee weapon you cannot see the reach of is a melee weapon you cannot
    /// aim. The glow is the reticle.
    /// </para>
    /// </summary>
    public abstract class LightWeapon : ToolItem
    {
        /// <summary>Material property the trail shader reads to know how far the swing has come.</summary>
        private static readonly int SweepProgressId = Shader.PropertyToID("_SweepProgress");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Light")]
        [Tooltip("Sits at the business end — the tip of the blade, the head of the axe, the orb. " +
                 "Its intensity is driven; whatever is authored on the component is overwritten.")]
        [SerializeField] private Light tipLight;

        [Tooltip("What the weapon glows at while it is simply being carried.")]
        [SerializeField] private float idleIntensity = 0.9f;

        [Tooltip("What it flares to at the peak of a swing.")]
        [SerializeField] private float swingIntensity = 4.5f;

        [Tooltip("How far the light reaches while idle, in metres.")]
        [SerializeField] private float idleRange = 4f;

        [Tooltip("How far it reaches at the peak of a swing.")]
        [SerializeField] private float swingRange = 9f;

        [Header("Trail")]
        [Tooltip("Uses the SpaceGame/Light/LightArc material. Emitting is driven by the swing.")]
        [SerializeField] private TrailRenderer trail;

        [Tooltip("Brightness handed to the arc shader at the peak of the swing.")]
        [SerializeField] private float trailIntensity = 3.0f;

        [Header("Swing")]
        [Tooltip("How long one swing takes, in seconds. Also how long the trail is drawn for.")]
        [SerializeField] private float swingDuration = 0.42f;

        [Tooltip("Shape of the flare across the swing. Peaks early — a swing is brightest as it " +
                 "commits, not as it finishes.")]
        [SerializeField] private AnimationCurve swingCurve =
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f));

        [Header("Damage")]
        [SerializeField] private int damage = 24;

        [Tooltip("Layers the sweep can hit.")]
        [SerializeField] private LayerMask hitMask = ~0;

        private float swingT = -1f;
        private MaterialPropertyBlock block;

        /// <summary>0 while idle, 0..1 through a swing. Subclasses read it to shape their own motion.</summary>
        protected float SwingProgress => swingT < 0f ? 0f : Mathf.Clamp01(swingT / Mathf.Max(swingDuration, 1e-4f));

        /// <summary>True between the start of a swing and the end of it.</summary>
        protected bool Swinging => swingT >= 0f;

        /// <summary>Where the damage sweep is centred and how far it reaches.</summary>
        protected abstract void GetSweep(out Vector3 centre, out float radius);

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);
            block = new MaterialPropertyBlock();
            ApplyLight(0f);
            if (trail != null) trail.emitting = false;
        }

        /// <summary>The hit. One sweep per press, at the moment of the press.</summary>
        protected override void Use()
        {
            GetSweep(out Vector3 centre, out float radius);

            // OverlapSphere rather than a swept cast: the swing is an arc, and approximating an arc
            // with a line misses everything at the sides, which is exactly where a wide weapon is
            // supposed to connect.
            Collider[] hits = Physics.OverlapSphere(centre, radius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;
                Damage.Apply(hit.gameObject, damage, transform);
            }
        }

        /// <summary>The look. Runs on every machine, so the swing is never waiting on anything.</summary>
        protected override void Present()
        {
            swingT = 0f;
            if (trail != null)
            {
                // Cleared rather than left to fade, so a second swing starts its own arc instead of
                // joining the tail of the last one into a continuous ribbon.
                trail.Clear();
                trail.emitting = true;
            }
        }

        private void LateUpdate()
        {
            if (swingT >= 0f)
            {
                swingT += Time.deltaTime;
                if (swingT >= swingDuration)
                {
                    swingT = -1f;
                    if (trail != null) trail.emitting = false;
                }
            }

            float flare = swingT < 0f ? 0f : swingCurve.Evaluate(SwingProgress);
            ApplyLight(flare);
            ApplyTrail(flare);
        }

        private void ApplyLight(float flare)
        {
            if (tipLight == null) return;

            tipLight.intensity = Mathf.Lerp(idleIntensity, swingIntensity, flare);
            tipLight.range = Mathf.Lerp(idleRange, swingRange, flare);
            tipLight.enabled = tipLight.intensity > 0.001f;
        }

        private void ApplyTrail(float flare)
        {
            if (trail == null || block == null) return;

            trail.GetPropertyBlock(block);

            // The arc shader draws up to _SweepProgress and leaves the rest of the ribbon unlit, so
            // handing it the swing's own progress is what makes the blade of light grow with the
            // swing instead of existing whole for its duration.
            block.SetFloat(SweepProgressId, SwingProgress);
            block.SetFloat(IntensityId, trailIntensity * Mathf.Max(flare, 0.15f));
            trail.SetPropertyBlock(block);
        }
    }
}
