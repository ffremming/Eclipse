using System;
using UnityEngine;
using SpaceGame.Presentation;

namespace SpaceGame.Items
{
    /// <summary>
    /// Base for everything the player can hold and trigger.
    ///
    /// One use splits into two jobs:
    ///
    ///   • <see cref="Use"/> — what the use DOES. Spawning, damage, consuming a charge.
    ///   • <see cref="Present"/> — what the use LOOKS AND SOUNDS LIKE. Sound, muzzle flash, VFX.
    ///
    /// The split is here rather than per item so an item can be presented without being run —
    /// which is what a press on a spent artifact does, so it clicks rather than passing in
    /// silence.
    /// </summary>
    public abstract class UsableItem : MonoBehaviour, IItemStateCarrier
    {
        [SerializeField] private int maxUses = -1; // -1 means unlimited uses

        private int currentUses = 0;

        protected GameObject owner;

        /// <summary>
        /// What the holder reported about this use — chiefly where they were aiming.
        ///
        /// The item cannot work that out for itself: an NPC has no camera, and a weapon in a hand
        /// is not where the eyes are. Whatever <see cref="OnRequestUse"/> put in here is what
        /// <see cref="Use"/> and <see cref="Present"/> work from.
        /// </summary>
        protected UseContext UseRequest { get; private set; }

        public event Action<UsableItem> OnItemDepleted;

        /// <summary>
        /// Does this item keep acting for as long as the button is held?
        ///
        /// False for everything that existed before the laser staff, and that is the point: a
        /// press-and-forget item is untouched by the held-use path, which never runs for it. An
        /// item that answers true additionally gets <see cref="OnRequestHold"/>,
        /// <see cref="Hold"/> and <see cref="PresentHold"/> called at the rate
        /// EquipmentController ticks at, until the button comes up.
        ///
        /// A continuous item still gets the ordinary press first. That is deliberate rather than
        /// incidental: the press is what plays the ignition sound and what counts against
        /// <c>maxUses</c>, so a held item is a normal item that also happens to keep going.
        /// </summary>
        public virtual bool IsContinuous => false;

        /// <summary>
        /// Does this item want the hold to continue even though the button is up?
        ///
        /// <para>
        /// False for a beam you steer by holding the trigger — that one ends when you let go, and
        /// nothing here changes for it. True for an item that runs for a fixed time once triggered:
        /// the laser staff burns for three seconds whether or not the finger stays down.
        /// </para>
        /// <para>
        /// It exists because a self-timed burst still needs the aim to keep flowing. Hold ticks are
        /// the only thing that carries an aim, and they stop the instant the button comes up — so
        /// without this, tapping the button leaves the beam burning along an aim frozen at the
        /// moment of the press while the player's own view sweeps freely. Ending the burst is then
        /// the item's job rather than the button's: EquipmentController keeps ticking until this
        /// goes false again.
        /// </para>
        /// </summary>
        public virtual bool WantsHold => false;

        /// <summary>
        /// Before the use runs: describe it. Aim origins go in <see cref="UseContext.Origin"/>,
        /// orientations in <see cref="UseContext.Aim"/>.
        /// </summary>
        public virtual void OnRequestUse(ref UseContext context) { }

        /// <summary>Actually use the item: the effect, the charge, the damage.</summary>
        public void TryUse(GameObject useOwner, UseContext context = default)
        {
            owner = useOwner;
            UseRequest = context;

            if (!CanUse()) return;

            Use();
            currentUses++;

            // Check if we've reached max uses
            if (maxUses >= 0 && currentUses >= maxUses)
            {
                OnMaxUsesReached();
            }
        }

        /// <summary>
        /// Play the use: whatever <see cref="Present"/> draws.
        ///
        /// Deliberately not gated on <see cref="CanUse"/>, so a press on a spent artifact still
        /// shows something rather than passing in silence.
        /// </summary>
        public void PlayUse(GameObject useOwner, UseContext context = default)
        {
            owner = useOwner;
            UseRequest = context;

            Present();
        }

        // ── Per-instance state ─────────────────────────────────────────────────
        //
        // Every held object in this game is a fresh Instantiate of the item prefab, destroyed on
        // unequip — see ItemState. So anything an item has BECOME lives in the hotbar slot rather
        // than on the instance, and these two methods are how it gets there and back.
        //
        // The charge count is here because it belongs to every item: a limited-use artifact whose
        // charges refilled every time the player scrolled past it was not a save bug, it was a
        // gameplay bug that a save merely made visible.

        /// <summary>State key for the charge count. Written into save files — never rename.</summary>
        private const string UsesKey = "uses";

        /// <summary>
        /// Write what this instance would otherwise lose. Subclasses override and call base.
        /// </summary>
        public virtual void CaptureItemState(ItemState state)
        {
            if (state == null) return;

            // An unlimited item has no count worth storing, and storing a zero for every artifact in
            // the game would put a bag on every slot that has nothing in it.
            if (maxUses >= 0 && currentUses > 0) state.Set(UsesKey, currentUses);
        }

        /// <summary>
        /// Apply a captured bag. Runs after <see cref="OnEquipped"/>, so it is free to overwrite
        /// whatever equipping set up.
        /// </summary>
        public virtual void RestoreItemState(ItemState state)
        {
            // A null bag is "this item is at its defaults", which for a fresh instance is already
            // true — but the reset is written out rather than assumed, because the same instance can
            // be handed a bag and then handed none.
            currentUses = state == null ? 0 : state.GetInt(UsesKey, 0);
        }

        /// <summary>How many uses are left, or -1 when this item is unlimited.</summary>
        protected int ChargesLeft => maxUses < 0 ? -1 : Mathf.Max(0, maxUses - currentUses);

        /// <summary>
        /// The authored charge limit, or -1 when unlimited.
        ///
        /// Exposed so a refilling item can tell when it is full without carrying a second copy of
        /// the number. A subclass that hardcoded its own maximum would be a magic number that
        /// silently disagrees with the prefab the moment a designer changes one of them.
        /// </summary>
        protected int MaxCharges => maxUses;

        /// <summary>
        /// Give one charge back.
        ///
        /// <para>
        /// The count is otherwise monotonic, which was right while every limited item in the game
        /// was strictly consumable. An item that REFILLS — the net gun is the first — has no way to
        /// express that without this, and the alternative is a second ammo counter running beside
        /// this one, which would then be the one that persists incorrectly.
        /// </para>
        /// <para>
        /// Note that an item which refills must also override <see cref="OnMaxUsesReached"/> to
        /// stay silent: the default raises <c>OnItemDepleted</c>, and
        /// <c>EquipmentController.ItemDepleted</c> answers that by removing the item from the
        /// inventory altogether.
        /// </para>
        /// </summary>
        protected void RefundUse()
        {
            if (currentUses > 0) currentUses--;
        }

        protected virtual bool CanUse()
        {
            // Prevent use if max uses reached
            if (maxUses >= 0 && currentUses >= maxUses)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Called when the item reaches its maximum number of uses.
        /// Override in subclasses for custom behavior.
        /// </summary>
        protected virtual void OnMaxUsesReached()
        {
            OnItemDepleted?.Invoke(this);
        }

        // ── Held use ───────────────────────────────────────────────────────────
        //
        // The same describe/do/present split as a press, because a hold has the same three jobs.
        // `active` is false on the final tick, which is the release — every override must treat
        // that as "stop", including the case where it never saw the ticks that came before it.

        /// <summary>Before each hold tick: describe the aim, which the item cannot work out.</summary>
        public virtual void OnRequestHold(ref UseContext context, bool active) { }

        /// <summary>Keep doing the thing. See <see cref="TryUse"/>.</summary>
        public void TryHold(GameObject useOwner, UseContext context, bool active)
        {
            owner = useOwner;
            UseRequest = context;
            Hold(context, active);
        }

        /// <summary>Keep showing the thing. See <see cref="PlayUse"/>.</summary>
        public void PlayHold(GameObject useOwner, UseContext context, bool active)
        {
            owner = useOwner;
            UseRequest = context;
            PresentHold(context, active);
        }

        /// <summary>The effect half of a hold tick. Empty unless the item is continuous.</summary>
        protected virtual void Hold(UseContext context, bool active) { }

        /// <summary>The cosmetic half of a hold tick.</summary>
        protected virtual void PresentHold(UseContext context, bool active) { }

        /// <summary>
        /// Lifecycle hook fired by EquipmentController right after the item prefab is
        /// instantiated and parented to the player's hand. Use for "while held"
        /// effects (animation flags, audio loops, glow, etc). Subclasses overriding
        /// this should call base.OnEquipped() so the shared HoldAnimator wiring
        /// still fires.
        /// </summary>
        public virtual void OnEquipped(GameObject holder)
        {
            // Set here and not only in TryUse/PlayUse, because OnRequestUse runs BEFORE either of
            // those on the very first use of a freshly equipped item. Anything that reads the
            // holder to describe a use — an aim ray, a muzzle, a velocity — would otherwise find
            // null exactly once per equip.
            owner = holder;

            // Give the item a hold pose whether or not anyone remembered to author one.
            //
            // This used to read the component and do nothing when it was absent, which made the
            // pose opt-in and silently so — an artifact without a HoldAnimator does not fail or
            // warn, it just stands in the idle tree holding a gun. Four of eleven equippable
            // artifacts had the component; the other seven were the bug.
            //
            // An authored component is left exactly as it is, because it carries per-prefab
            // tuning. This only fills the gap.
            var hold = GetComponent<HoldAnimator>();
            if (hold == null && UsesHoldPose) hold = gameObject.AddComponent<HoldAnimator>();
            if (hold != null) hold.SetHeld(holder, true);
        }

        /// <summary>
        /// Whether holding this item should pose the holder's body.
        ///
        /// <para>
        /// True for anything gripped. Override to false for something worn rather than held — a
        /// pack, a suit module — where posing the arms as though gripping it is wrong.
        /// </para>
        /// </summary>
        protected virtual bool UsesHoldPose => true;

        /// <summary>
        /// Lifecycle hook fired by EquipmentController right before the item prefab
        /// is unparented/destroyed. Mirror of OnEquipped — clean up here.
        /// </summary>
        public virtual void OnUnequipped(GameObject holder)
        {
            var hold = GetComponent<HoldAnimator>();
            if (hold != null) hold.SetHeld(holder, false);
        }

        /// <summary>The effect. See the class summary.</summary>
        protected abstract void Use();

        /// <summary>
        /// The cosmetic half. Empty by default: most items show themselves through whatever
        /// <see cref="Use"/> spawns. Override it for a VFX burst, a beam, a muzzle flash.
        /// </summary>
        protected virtual void Present() { }
    }
}
