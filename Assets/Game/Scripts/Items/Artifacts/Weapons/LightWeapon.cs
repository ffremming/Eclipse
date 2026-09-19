using System.Collections.Generic;
using UnityEngine;
using SpaceGame.Gameplay;
using SpaceGame.Presentation;

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
        [Tooltip("The colours of the light the player's swing throws while this weapon is held. " +
                 "Spectrum is the four-hue slash; Orb keeps to the palette of the weapon's own ball " +
                 "of light, for a weapon that already has one and should not gain any other colour.")]
        [SerializeField] private SlashPalette swingPalette = SlashPalette.Spectrum;

        [Tooltip("Whether the player's swing draws its slash of light while this is held. Off for a " +
                 "weapon that throws its own light, so the only light in a use is the weapon's.")]
        [SerializeField] private bool leavesSwingLight = true;

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

        /// <summary>The most colliders one sweep can hurt. A crowd larger than this is not one blow.</summary>
        private const int MaxHitsPerSweep = 32;

        private readonly Collider[] hitBuffer = new Collider[MaxHitsPerSweep];
        private SpaceGame.Characters.SwingFlare swing;
        private MaterialPropertyBlock block;
        private SpaceGame.Characters.PlayerMeleeSwing holderSwing;

        /// <summary>
        /// Whether the holder's body actually swung on this press. Written by <see cref="Present"/>
        /// and read by <see cref="CanUse"/>, which is sound because one press runs the two in that
        /// order and nothing else calls either.
        /// </summary>
        private bool bodySwung;

        /// <summary>Which colours the light of the player's swing takes while this is held.</summary>
        public SlashPalette SwingPalette => swingPalette;

        /// <summary>Whether the player's swing draws its own slash of light while this is held.</summary>
        public bool LeavesSwingLight => leavesSwingLight;

        /// <summary>How long one swing takes, in seconds. A weapon with its own motion fits it to this.</summary>
        protected float SwingDuration => swingDuration;

        /// <summary>0 while idle, 0..1 through a swing. Subclasses read it to shape their own motion.</summary>
        protected float SwingProgress => Swing.Progress;

        /// <summary>True between the start of a swing and the end of it.</summary>
        protected bool Swinging => Swing.Swinging;

        /// <summary>The light at the business end, for subclasses that have to report where it is.</summary>
        protected Light TipLight => tipLight;

        /// <summary>
        /// How hard the weapon is burning right now, 0 idle to 1 at the peak of a swing.
        /// <para>
        /// The same curve that drives the light, exposed so a subclass can drive its own effects
        /// from it instead of timing a second one that would drift out of step with the first.
        /// </para>
        /// </summary>
        protected float Flare => Swing.Flare;

        /// <summary>
        /// The swing's own clock, made on first use rather than in a field initialiser so the
        /// serialized duration and curve are the ones the Inspector shows.
        /// </summary>
        private SpaceGame.Characters.SwingFlare Swing =>
            swing ??= new SpaceGame.Characters.SwingFlare(swingDuration, swingCurve);

        /// <summary>Where the damage sweep is centred and how far it reaches.</summary>
        protected abstract void GetSweep(out Vector3 centre, out float radius);

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);
            block = new MaterialPropertyBlock();

            // The holder's swing, not the item's own animator. Resolved on equip because it cannot
            // change while the weapon is in a hand, and searching for it per swing would pay for
            // that on every press.
            holderSwing = holder != null ? holder.GetComponent<SpaceGame.Characters.PlayerMeleeSwing>() : null;

            ApplyLight(0f);
            if (trail != null) trail.emitting = false;
        }

        /// <summary>The hit. One sweep per press, at the moment of the press.</summary>
        protected override void Use() => SweepForHits();

        /// <summary>
        /// No swing, no sweep.
        /// <para>
        /// The body refuses a swing that is still inside the cooldown or that there is no light
        /// left to pay for, and the sweep has to refuse with it: a weapon that hurt things anyway
        /// would be a blow with no swing behind it, and — since the light is spent inside the
        /// swing — the one attack in the game that cost nothing.
        /// </para>
        /// </summary>
        protected override bool CanUse() => base.CanUse() && bodySwung;

        /// <summary>
        /// Hurt everything inside the sweep <see cref="GetSweep"/> reports right now.
        /// <para>
        /// A weapon whose damage lands at the press calls it once, from <see cref="Use"/>. One whose
        /// damage travels — the whip's orb, over the length of a lash — calls it every step and
        /// passes <paramref name="alreadyHit"/>, so a target the orb stays inside for several steps
        /// is hurt once.
        /// </para>
        /// </summary>
        protected void SweepForHits(HashSet<Component> alreadyHit = null)
        {
            GetSweep(out Vector3 centre, out float radius);

            // OverlapSphere rather than a swept cast: the swing is an arc, and approximating an arc
            // with a line misses everything at the sides, which is exactly where a wide weapon is
            // supposed to connect.
            int count = Physics.OverlapSphereNonAlloc(centre, radius, hitBuffer, hitMask,
                                                      QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hitBuffer[i];
                if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;

                // A target is whatever owns the health, and a creature is many colliders. Keyed on
                // the collider instead, a limb and then a torso would be two hits on one creature.
                Component victim = hit.GetComponentInParent<HealthComponent>();
                if (alreadyHit != null && !alreadyHit.Add(victim != null ? victim : hit)) continue;

                Damage.Apply(hit.gameObject, damage, transform);
            }
        }

        /// <summary>The look, and the swing the look belongs to.</summary>
        protected override void Present()
        {
            // The holder swings its own body. Going through PlayerMeleeSwing rather than writing
            // to the Animator here means the weapon shares the bare hand's cooldown, its variation
            // counter and its cost in light — so swapping between them never repeats a clip for no
            // visible reason, and never makes an attack cheaper. A weapon in no player's hand has
            // no body to ask and swings freely, which is what lets a creature carry one.
            bodySwung = holderSwing == null || holderSwing.TryPlaySwing();

            // Nothing happened, so nothing is shown. Flaring a weapon whose swing was refused
            // would read as a hit that the sweep is about to decline to make.
            if (!bodySwung) return;

            Swing.Begin();

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
            if (Swing.Tick(Time.deltaTime) && trail != null) trail.emitting = false;

            ApplyLight(Flare);
            ApplyTrail(Flare);
            OnFlareChanged(Flare);
        }

        /// <summary>Hook for subclasses with effects of their own to drive. Default does nothing.</summary>
        protected virtual void OnFlareChanged(float flare) { }

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
