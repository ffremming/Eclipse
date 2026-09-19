using UnityEngine;
using SpaceGame.Castle;
using SpaceGame.Items;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Names the key the player has just picked up, once, across the middle of the screen.
    ///
    /// <para>
    /// This is the only text the game puts in front of the player, and it exists because of what a
    /// key is here: picking one up IS unlocking its doors (see <see cref="KeyRing"/>), so the most
    /// consequential thing the player can do is walk over a small object and have nothing visibly
    /// change. A door somewhere else in the world stopped being locked. Nothing on the screen said
    /// so. <c>GDC-L1-UX-0003</c>'s exception for no-HUD design is explicit that minimalism does not
    /// exempt a game from communicating — it only moves the communication — and there is nowhere in
    /// the world to move this one to, because the thing that changed is not where the player is.
    /// </para>
    /// <para>
    /// It is also the redundancy <c>GDC-L1-FEEL-0004</c> asks for on the game's biggest payouts,
    /// standing in for a channel this project does not have: with no audio there is no chime on a
    /// pickup, so the visual has to carry it alone.
    /// </para>
    /// <para>
    /// It says the key's own <see cref="InventoryItem.itemName"/> rather than a sentence, so the
    /// line is what the key IS. Naming it "Key to the Fortress" is what tells the player the castle
    /// on the far hill is now open, without a quest log to write it in.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyBanner : MonoBehaviour
    {
        [Tooltip("The ring this watches. Left empty it finds the one on this object, which is " +
                 "where the player's is.")]
        [SerializeField] private PlayerKeyRing ring;

        [SerializeField] private KeyBannerStyle style = new();

        private KeyBannerView view;
        private Announcement announcement;
        private string saying = string.Empty;

        private void Awake()
        {
            if (ring == null) ring = GetComponent<PlayerKeyRing>();

            announcement = new Announcement(style.riseSeconds, style.holdSeconds, style.fallSeconds);

            view = new GameObject("Key Banner HUD").AddComponent<KeyBannerView>();
            view.transform.SetParent(transform, false);
            view.Build(style);

            if (ring == null)
                Debug.LogError("[KeyBanner] " + name + " has no key ring to watch, so finding a " +
                               "key will say nothing.", this);
        }

        private void OnEnable()
        {
            if (ring != null) ring.Keys.Taken += OnKeyTaken;
        }

        private void OnDisable()
        {
            if (ring != null) ring.Keys.Taken -= OnKeyTaken;
        }

        /// <summary>
        /// Unscaled time, so the line runs at its own pace while the weapon wheel is slowing the
        /// game — the same reason the lantern does.
        /// </summary>
        private void Update()
        {
            announcement.Tick(Time.unscaledDeltaTime);
            view.Draw(saying, announcement.Strength);
        }

        private void OnKeyTaken(InventoryItem key)
        {
            // Upper case here rather than in the asset, so the key is still called what it is
            // called everywhere else it is read.
            saying = key.itemName.ToUpperInvariant();
            announcement.Show();
        }
    }
}
