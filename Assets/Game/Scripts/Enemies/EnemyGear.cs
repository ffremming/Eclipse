// Puts the weapons a creature carries into its hands.
//
// Written for the goblin, whose sword and shield were part of its FBX: loose objects standing
// beside the body, parented to the model root rather than to any bone. They held their place in
// the bind pose while the arms moved, so it looked to be followed around by a floating blade.
// The goblin is gone; what is left is the seating, which is how the alien and the crumpy are
// handed their dark blades.
//
// Each weapon is handed to the hand its ItemGrip names through the same seating the player's
// equipment uses — a grip frame derived from the finger bones, then the prop's own grip point and
// offsets. One answer to how a hand holds a thing, so a weapon is tuned by the rules everything
// else in the game is tuned by, and a creature's arm animations carry it without any per-frame work.
//
// The weapons are PREFABS, spawned here, rather than objects sitting in the creature's own prefab.
// That is not a preference: seating one means reparenting it onto a hand bone, and Unity refuses to
// reparent anything that is part of a prefab instance — which is exactly what a weapon inside the
// creature's prefab is the moment that creature is placed in a scene. It fails only in the Editor,
// where prefab links survive into play mode, so a build would have looked fine while every creature
// in the editor fought bare-handed with an error in the console.
using System.Collections.Generic;
using SpaceGame.Items;
using UnityEngine;

namespace SpaceGame.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyGear : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("The weapon prefabs this creature carries. One of each is spawned and put in the " +
                 "hand its ItemGrip names, at the grip and angle the same component says.")]
        [SerializeField] private ItemGrip[] props;

        /// <summary>What was spawned, so seating twice does not arm the creature twice.</summary>
        private readonly List<ItemGrip> carried = new List<ItemGrip>();

        // Only when the game is running. The Editor calls Awake too — when the component is added,
        // and when a tool instantiates the creature's prefab into a scene. Arming it there hangs a
        // blade on the instance as an override, which is then saved into the scene; at play time
        // Awake spawns a second one beside it, and the creature is carrying two.
        private void Awake()
        {
            if (Application.isPlaying) SeatProps();
        }

        /// <summary>
        /// Spawn every weapon and hand it to its hand. Awake calls it when the game starts; it is
        /// public because the EditMode test checks the seating against the rig without one. Calling
        /// it again re-seats what it already spawned, to the same place.
        /// </summary>
        public void SeatProps()
        {
            // A creature with nothing to hold is not a mistake.
            if (props == null || props.Length == 0) return;

            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("EnemyGear needs a humanoid Animator to find the hands.", this);
                return;
            }

            if (carried.Count == 0)
            {
                foreach (ItemGrip prop in props)
                {
                    if (prop == null) continue;

                    Transform hand = HandFor(prop);
                    if (hand == null) continue;

                    // Spawned INTO the hand rather than at the scene root and moved there a line
                    // later. A weapon's own components wake up during Instantiate, and the dark
                    // ones look upwards for the creature whose swing they ride — DarkWeapon for its
                    // MeleeStrike, DarkSwingTrail for the same. A prop standing on its own has
                    // nothing above it, so they found nothing and listened to nothing, and every
                    // dark effect on every enemy sat at its idle value for the whole game. The
                    // player's equipment has always spawned this way; see EquipItemSocket.Equip.
                    carried.Add(Instantiate(prop, hand));
                }
            }

            foreach (ItemGrip prop in carried)
                Hold(prop);
        }

        private void Hold(ItemGrip prop)
        {
            Transform hand = HandFor(prop);
            if (hand == null) return;

            HandGripFrame frame = HandGripFrame.Derive(animator, hand,
                                                       prop.HeldIn == ItemGrip.Hand.Right);
            new EquipItemSocket(hand, frame).Hold(prop.gameObject);
        }

        private Transform HandFor(ItemGrip prop)
        {
            bool rightHand = prop.HeldIn == ItemGrip.Hand.Right;
            Transform hand = animator.GetBoneTransform(rightHand ? HumanBodyBones.RightHand
                                                                 : HumanBodyBones.LeftHand);
            if (hand == null)
                Debug.LogError($"EnemyGear: the rig has no {prop.HeldIn} hand bone for '{prop.name}'.", this);

            return hand;
        }
    }
}
