// Puts the props modelled into an enemy's mesh into its hands.
//
// The goblin's sword and shield are part of its FBX: loose objects standing beside the body,
// parented to the model root rather than to any bone. They held their place in the bind pose while
// the arms moved, so a goblin looked to be followed around by a floating blade.
//
// Each prop is handed to the hand its ItemGrip names through the same seating the player's
// equipment uses — a grip frame derived from the finger bones, then the prop's own grip point and
// offsets. One answer to how a hand holds a thing, so a prop is tuned by the rules everything
// else in the game is tuned by, and the goblin's arm animations carry it without any per-frame work.
using SpaceGame.Items;
using UnityEngine;

namespace SpaceGame.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyGear : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("The props to put in hands. Each one's ItemGrip says which hand, where it is " +
                 "gripped and which way it points.")]
        [SerializeField] private ItemGrip[] props;

        private void Awake() => SeatProps();

        /// <summary>
        /// Hand every prop to its hand. Awake calls it once; it is public because Awake does not run
        /// in EditMode, where the seating is checked against the rig. Calling it again re-seats the
        /// same props to the same place.
        /// </summary>
        public void SeatProps()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("EnemyGear needs a humanoid Animator to find the hands.", this);
                return;
            }

            foreach (ItemGrip prop in props)
                Hold(prop);
        }

        private void Hold(ItemGrip prop)
        {
            bool rightHand = prop.HeldIn == ItemGrip.Hand.Right;
            Transform hand = animator.GetBoneTransform(rightHand ? HumanBodyBones.RightHand
                                                                 : HumanBodyBones.LeftHand);
            if (hand == null)
            {
                Debug.LogError($"EnemyGear: the rig has no {prop.HeldIn} hand bone for '{prop.name}'.", this);
                return;
            }

            HandGripFrame frame = HandGripFrame.Derive(animator, hand, rightHand);
            new EquipItemSocket(hand, frame).Hold(prop.gameObject);
        }
    }
}
