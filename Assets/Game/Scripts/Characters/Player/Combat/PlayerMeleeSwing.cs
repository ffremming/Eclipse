// The player's bare-handed swing: the fighting game's core verb, on the Use button.
//
// Bound to Use rather than to a new action because Use is already the "do the thing in front of
// you" button, and an enemy's blade is put in its hand by EnemyGear rather than equipped — so with
// nothing equipped the button has nothing else to do. An equipped item still wins: the hotbar item
// is what the player deliberately put in their hand.
//
// This decides WHEN the player swings. What a swing connects with belongs to MeleeStrike, which the
// enemies drive from their own brains off the very same component — so the player and the enemies
// fighting them resolve a hit through one piece of code rather than two that drift apart.
using System;
using SpaceGame.Core;
using SpaceGame.Enemies;
using SpaceGame.Gameplay;
using SpaceGame.Items;
using UnityEngine;

namespace SpaceGame.Characters
{
    [DisallowMultipleComponent]
    public class PlayerMeleeSwing : MonoBehaviour
    {
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int AttackIndex = Animator.StringToHash("AttackIndex");
        private static readonly int KickTrigger = Animator.StringToHash("Kick");
        private static readonly int JumpAttackTrigger = Animator.StringToHash("JumpAttack");

        [SerializeField] private Animator animator;
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private HealthComponent health;

        [Tooltip("Read for the two contextual attacks: a swing in the air becomes the jump " +
                 "attack, a swing from a crouch becomes the kick. Left empty, the player only " +
                 "ever gets the standing swings.")]
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerStance stance;

        [Tooltip("What the swing hits. Leave empty and the player swings for show, as it did before " +
                 "there was a hitbox — which is worth knowing if melee suddenly stops hurting.")]
        [SerializeField] private MeleeStrike strike;

        [Tooltip("How many swing clips the Attack layer cycles through. Must match the number of " +
                 "swing states in the controller, which the AttackIndex parameter selects between.")]
        [SerializeField] private int swingVariations = 5;

        [Tooltip("Shortest gap between swings, in seconds. Shorter than the clip on purpose: the " +
                 "next swing cuts into the follow-through of the last one rather than waiting " +
                 "for it, which is what keeps a held button from feeling sluggish.")]
        [SerializeField] private float swingCooldown = 0.35f;

        [Tooltip("Longest gap that still continues the combo. Press again inside this and the " +
                 "chain escalates towards the finisher; let it lapse and the next swing opens a " +
                 "fresh chain. Must be longer than the cooldown or the chain can never advance.")]
        [SerializeField] private float chainWindow = 1.1f;

        [Tooltip("Orbs of light one swing burns. Swinging is the player's only way to spend light " +
                 "by choice, which is what makes a miss cost something and a fight worth leaving. " +
                 "Zero gives the swings away free, as they were before the lantern was a resource.")]
        [SerializeField] private int swingLightCost = 1;

        private PlayerInputManager input;
        private MeleeSwingSequence sequence;

        /// <summary>
        /// Raised the moment a swing's animation is started, with the attack the body's context
        /// turned the press into. For anything that should look like the swing — the light it
        /// throws off — rather than decide it.
        /// </summary>
        public event Action<SwingKind> Swung;

        private void Awake()
        {
            input = GetComponent<PlayerInputManager>();
            if (animator == null) animator = GetComponent<Animator>();
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (health == null) health = GetComponent<HealthComponent>();
            if (strike == null) strike = GetComponent<MeleeStrike>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (stance == null) stance = GetComponent<PlayerStance>();

            sequence = new MeleeSwingSequence(swingVariations, swingCooldown, chainWindow);
        }

        private void OnEnable()
        {
            if (input != null) input.OnUsePressed += OnUsePressed;
        }

        private void OnDisable()
        {
            if (input != null) input.OnUsePressed -= OnUsePressed;
        }

        private void OnUsePressed()
        {
            // The held item's own use already ran; swinging as well would play a sword animation
            // over a thrown grenade. A melee weapon is the exception and asks for the swing itself,
            // through TryPlaySwing — see LightWeapon.
            if (equipment != null && equipment.HeldUsable != null) return;
            if (!TryPlaySwing()) return;

            // After the animator, so that a swing always looks like it happened even if the hitbox
            // is missing from the prefab.
            if (strike != null) strike.Swing();
        }

        /// <summary>
        /// Play a swing clip, if the sequence allows one right now.
        /// <para>
        /// Public because a held melee weapon has to swing the same body this does, and it must do
        /// it through the same <see cref="MeleeSwingSequence"/> — otherwise the weapon and the bare
        /// hand keep separate cooldowns and separate variation counters, and a player swapping
        /// between them sees the same clip twice in a row for no reason they can see.
        /// </para>
        /// <para>
        /// Only the animation. The weapon resolves its own hit through its own sweep, so this does
        /// NOT fire <see cref="MeleeStrike"/> — calling both would hurt everything in front of the
        /// player twice per press.
        /// </para>
        /// <para>
        /// Costs light, so a caller that gets false back may have been refused for want of it
        /// rather than for the cooldown.
        /// </para>
        /// </summary>
        public bool TryPlaySwing()
        {
            if (health != null && !health.Alive) return false;
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            // Asked before the sequence, because TrySwing consumes a step of the chain the moment
            // it says yes and a swing refused for want of light must not burn one.
            if (!CanAffordSwing()) return false;

            // Gated through the sequence even when the swing that comes out is contextual, so the
            // kick and the jump attack share one cooldown with the sword rather than giving the
            // player a second attack button by accident.
            if (!sequence.TrySwing(Time.time, out int index)) return false;

            if (health != null) health.Spend(swingLightCost);

            Swung?.Invoke(PlayForContext(index));
            return true;
        }

        // The last orb is never spendable, so the player cannot put their own lantern out by
        // swinging at nothing — dying has to be something an enemy did.
        private bool CanAffordSwing() =>
            swingLightCost <= 0 || health == null || health.CanSpend(swingLightCost);

        private SwingKind PlayForContext(int index)
        {
            if (movement != null && !movement.IsOnGround)
            {
                animator.SetTrigger(JumpAttackTrigger);
                return SwingKind.JumpAttack;
            }

            if (stance != null && stance.IsCrouching)
            {
                animator.SetTrigger(KickTrigger);
                return SwingKind.Kick;
            }

            animator.SetInteger(AttackIndex, index);
            animator.SetTrigger(AttackTrigger);
            return SwingKind.Slash;
        }
    }
}
