// The streak of black a creature's blade drags behind it — the player's slash of light, read
// backwards.
//
// Mechanically it IS the player's trail: the same SwingRibbonTrail records the same ribbon between
// a hilt anchor and a tip anchor while they are moving fast, and the same SlashRibbon builds the
// same mesh from it. Only two things differ, and they are exactly the two a subclass supplies.
//
// WHO ANNOUNCES THE SWING. The player's ribbon hears it from PlayerMeleeSwing, because a player's
// swing starts at a button. A creature's starts when its brain says so, and the component that
// knows is MeleeStrike — the same one that decides what the swing hits, so there is exactly one
// clock for "this creature is swinging" rather than a second one to fall out of step with it.
//
// WHAT IT IS PAINTED WITH. The DarkSlash material, which multiplies the frame to black where the
// light one adds a spectrum to it. That difference is the read: the player's swing throws light
// across a dark world and the eye tracks the bright edge, a creature's takes the world away and the
// eye tracks the hole. Neither can be mistaken for the other at a glance, which is what a fight
// against several kinds of thing needs (GDC-L1-FEEL-0004).
//
// It sits on the WEAPON, not on the creature, because the two anchors are the hilt and the tip of
// a specific blade and DarkBladeBuilder already knows where those are. That makes the creature
// above it something only found at runtime — see Resolve.
using SpaceGame.Enemies;
using SpaceGame.Presentation;
using UnityEngine;

namespace SpaceGame.Gameplay
{
    [DisallowMultipleComponent]
    public class DarkSwingTrail : SwingRibbonTrail
    {
        [Tooltip("The swing this ribbon draws for. Left empty it finds the one on the creature " +
                 "holding the weapon, which is what a blade seated into a hand by EnemyGear wants.")]
        [SerializeField] private MeleeStrike strike;

        protected override bool Resolve()
        {
            if (strike == null) strike = GetComponentInParent<MeleeStrike>();

            if (strike == null)
            {
                Debug.LogError($"{nameof(DarkSwingTrail)} on '{name}' found no {nameof(MeleeStrike)} " +
                               "above it, so it will draw nothing. A dark blade has to be seated " +
                               "into a creature's hand before it wakes up.", this);
                return false;
            }

            // The creature, not the blade. Measured in the blade's own frame the tip never moves at
            // all; measured in the world, walking is a swing. The swinger's frame is the only one
            // in which "the blade is travelling" means what it says.
            if (BodyFrame == null) BodyFrame = strike.transform;
            return true;
        }

        private void OnEnable()
        {
            if (strike != null) strike.SwingStarted += Arm;
        }

        // Shut the window the moment the blade goes cold, rather than letting it run out on its
        // own. The player can afford a generous window because a player is standing still between
        // swings; a creature spends the second after one running at you, and an arm swinging
        // through a run cycle moves quite fast enough to fool the speed gate into streaking.
        private void Update()
        {
            if (!strike.IsSwinging) Disarm();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (strike != null) strike.SwingStarted -= Arm;
        }
    }
}
