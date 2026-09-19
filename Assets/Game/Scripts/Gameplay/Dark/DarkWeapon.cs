// The enemy's answer to a LightWeapon: a blade that emits darkness.
//
// It is the same weapon shape read backwards. A light weapon glows while carried and flares when
// swung, throwing light onto what it is about to hit; this drinks while carried and drinks hard
// when swung, taking light off what it is about to hit.
//
// This drives the LIGHTING half of that — the Unlight at the business end, which is the half that
// touches the world rather than the frame. The streak the blade leaves is DarkSwingTrail's, the
// mirror of the player's own slash of light, and the two are separate components for the same
// reason LightWeapon and SwingTrail are: one changes what the world is lit by, the other draws a
// ribbon through the air, and neither needs to know the other exists.
//
// It is NOT a UsableItem, and that is the whole difference in how it is driven. The player's
// weapons are equipped and used, so their swing arrives through the item's own Use/Present. An
// enemy's weapon is a prop seated in its hand by EnemyGear, and the swing belongs to the enemy's
// MeleeStrike — the same component that decides what the swing hits. So the weapon listens to the
// strike rather than owning one, and there is exactly one clock for "this creature is swinging".
//
// It deals no damage of its own for the same reason: MeleeStrike already does, and a weapon that
// also swept for hits would hit everything twice.
using SpaceGame.Characters;
using SpaceGame.Enemies;
using UnityEngine;

namespace SpaceGame.Gameplay
{
    [DisallowMultipleComponent]
    public class DarkWeapon : MonoBehaviour
    {
        [Header("Swing")]
        [Tooltip("The swing this rides. Left empty it finds the one on the creature holding it, " +
                 "which is what a weapon seated into a hand by EnemyGear wants.")]
        [SerializeField] private MeleeStrike strike;

        [Tooltip("Shape of the flare across the swing. Peaks early, like the light weapons': a " +
                 "swing is at its darkest as it commits, not as it finishes.")]
        [SerializeField] private AnimationCurve swingCurve =
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f));

        [Header("Unlight")]
        [Tooltip("Sits at the business end and takes light off everything near it.")]
        [SerializeField] private Unlight tipUnlight;

        [Tooltip("What it eats while the weapon is simply being carried.")]
        [SerializeField] private float idleStrength = 8f;

        [Tooltip("What it eats at the peak of a swing.")]
        [SerializeField] private float swingStrength = 34f;

        [Tooltip("How far the dark reaches while carried, in metres.")]
        [SerializeField] private float idleRange = 4f;

        [Tooltip("How far it reaches at the peak of a swing.")]
        [SerializeField] private float swingRange = 8f;

        private SwingFlare swing;

        private void Awake()
        {
            if (strike == null) strike = GetComponentInParent<MeleeStrike>();

            // Paced by the creature's own swing rather than by a duration of this weapon's, so a
            // slow heavy enemy's blade stays dark for as long as its arm takes. A weapon lying on
            // the ground with no swinger still has to tick, so it falls back to its curve's length.
            swing = new SwingFlare(strike != null ? strike.SwingDuration : CurveLength(swingCurve),
                                   swingCurve);

            Apply(0f);
        }

        private void OnEnable()
        {
            if (strike != null) strike.SwingStarted += swing.Begin;
        }

        private void OnDisable()
        {
            if (strike != null) strike.SwingStarted -= swing.Begin;

            // A weapon dropped or switched off mid-swing would otherwise stay at whatever it was
            // eating when the arm stopped.
            swing.Cancel();
            Apply(0f);
        }

        private void LateUpdate()
        {
            swing.Tick(Time.deltaTime);
            Apply(swing.Flare);
        }

        private void Apply(float flare)
        {
            if (tipUnlight == null) return;

            tipUnlight.Strength = Mathf.Lerp(idleStrength, swingStrength, flare);
            tipUnlight.Range = Mathf.Lerp(idleRange, swingRange, flare);
        }

        /// <summary>How long the curve itself runs for. SwingFlare floors an empty one.</summary>
        private static float CurveLength(AnimationCurve curve)
            => curve == null || curve.length == 0 ? 0f : curve.keys[curve.length - 1].time;
    }
}
