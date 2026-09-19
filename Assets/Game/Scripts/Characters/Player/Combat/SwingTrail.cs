// The slash of light a player's swing leaves in the air.
//
// One of these rides one limb: a base anchor and a tip anchor, parented to bones so the animation
// carries them. The sword arm gets one, and each leg gets one for the kick. SwingRibbonTrail does
// the recording and the drawing; what is here is the player's half of it — which attacks light the
// limb, and whose colours the ribbon wears.
//
// Whose colours it wears depends on what is in the hand. Every weapon gets the ribbon, but a weapon
// whose own light already has a palette — the chain's orb — asks for that palette instead of the
// four-hue spectrum, so the light of a swing never introduces a colour the weapon does not have.
using SpaceGame.Items;
using SpaceGame.Presentation;
using UnityEngine;

namespace SpaceGame.Characters
{
    public class SwingTrail : SwingRibbonTrail
    {
        [Header("Swing")]
        [SerializeField] private PlayerMeleeSwing swing;

        [Tooltip("Read for the held weapon's swing palette. Left empty, the one on the same body " +
                 "as the swing is used.")]
        [SerializeField] private EquipmentController equipment;

        [Tooltip("Which attacks light this trail. The sword arm takes Slash and JumpAttack; a leg " +
                 "takes Kick.")]
        [SerializeField] private SwingKind armedBy = SwingKind.Slash | SwingKind.JumpAttack;

        private static readonly int OrbPaletteId = Shader.PropertyToID("_OrbPalette");

        protected override bool Resolve()
        {
            if (swing == null) swing = GetComponentInParent<PlayerMeleeSwing>();
            if (equipment == null && swing != null) equipment = swing.GetComponent<EquipmentController>();

            if (swing != null) return true;

            Debug.LogError($"{nameof(SwingTrail)} on '{name}' has no {nameof(PlayerMeleeSwing)} to " +
                           "listen to, so it will draw nothing.", this);
            return false;
        }

        private void OnEnable()
        {
            if (swing != null) swing.Swung += OnSwung;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (swing != null) swing.Swung -= OnSwung;
        }

        private void OnSwung(SwingKind kind)
        {
            if ((kind & armedBy) == 0) return;

            LightWeapon weapon = HeldWeapon();

            // A weapon that throws its own light has no use for the arm's. Disarmed rather than just
            // not armed, so a ribbon still armed from a swing with the last weapon does not draw
            // through this one's throw.
            if (weapon != null && !weapon.LeavesSwingLight)
            {
                Disarm();
                return;
            }

            Arm();
            Properties.SetFloat(OrbPaletteId, (float)(weapon != null ? weapon.SwingPalette : SlashPalette.Spectrum));
        }

        // Asked when a swing starts, not every frame, so swapping weapons on its own does not
        // recolour light that is already in the air.
        private LightWeapon HeldWeapon()
        {
            return equipment != null ? equipment.HeldUsable as LightWeapon : null;
        }
    }
}
