// The player's bare-handed swing: the fighting game's core verb, on the Use button.
//
// Bound to Use rather than to a new action because Use is already the "do the thing in front of
// you" button, and the goblin's sword is part of its mesh rather than an inventory item — so with
// nothing equipped the button has nothing else to do. An equipped item still wins: the hotbar item
// is what the player deliberately put in their hand.
//
// This decides WHEN the player swings. What a swing connects with belongs to MeleeStrike, which the
// enemies drive from their own brains off the very same component — so the player and the goblins
// fighting them resolve a hit through one piece of code rather than two that drift apart.
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

        [SerializeField] private Animator animator;
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private HealthComponent health;

        [Tooltip("What the swing hits. Leave empty and the player swings for show, as it did before " +
                 "there was a hitbox — which is worth knowing if melee suddenly stops hurting.")]
        [SerializeField] private MeleeStrike strike;

        [Tooltip("How many swing clips the Attack layer cycles through. Must match the number of " +
                 "swing states in the controller, which the AttackIndex parameter selects between.")]
        [SerializeField] private int swingVariations = 3;

        [Tooltip("Shortest gap between swings, in seconds. Shorter than the clip on purpose: the " +
                 "next swing cuts into the follow-through of the last one rather than waiting " +
                 "for it, which is what keeps a held button from feeling sluggish.")]
        [SerializeField] private float swingCooldown = 0.35f;

        private PlayerInputManager input;
        private MeleeSwingSequence sequence;

        private void Awake()
        {
            input = GetComponent<PlayerInputManager>();
            if (animator == null) animator = GetComponent<Animator>();
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (health == null) health = GetComponent<HealthComponent>();
            if (strike == null) strike = GetComponent<MeleeStrike>();

            sequence = new MeleeSwingSequence(swingVariations, swingCooldown);
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
            // over a thrown grenade.
            if (equipment != null && equipment.HeldUsable != null) return;
            if (health != null && !health.Alive) return;
            if (animator == null || animator.runtimeAnimatorController == null) return;

            if (!sequence.TrySwing(Time.time, out int index)) return;

            animator.SetInteger(AttackIndex, index);
            animator.SetTrigger(AttackTrigger);

            // After the animator, so that a swing always looks like it happened even if the hitbox
            // is missing from the prefab.
            if (strike != null) strike.Swing();
        }
    }
}
