using UnityEngine;
using SpaceGame.Gameplay;

namespace SpaceGame.Castle
{
    /// <summary>
    /// Makes a <see cref="KeyedEntrance"/> look like what it is: a light on the door, breathing red
    /// while it is shut to you, steady and turquoise once you are carrying its key, and flaring
    /// when it turns you away.
    /// <para>
    /// This is the entire user interface of the castle. Eclipse has no HUD, so there is nowhere to
    /// write "Locked — needs the Tower Key"; the door has to say it by looking like it. See
    /// <see cref="LockSignal"/> for what the three states are and why the refusal is the one that
    /// matters.
    /// </para>
    /// <para>
    /// It also answers where the player is LOOKING: a door under the crosshair and inside the
    /// interactor's reach brightens, which is this game's stand-in for the interaction prompt it
    /// does not have (<c>GDC-L1-UX-0004</c>). The hover comes from
    /// <see cref="Interactor.HoveredInteractable"/> rather than from a distance check of its own,
    /// so the door lights up exactly when pressing E would reach it — a highlight that came on a
    /// metre earlier than the key worked would be a promise the door then breaks.
    /// </para>
    /// <para>
    /// Whether the player has the key is polled on an interval rather than every frame. The lookup
    /// itself is cheap, but finding the player by tag to do it is not, and the answer can only
    /// change when they walk over a key — a fifth of a second is far below what anyone notices. The
    /// player's interactor is found by the same poll, which is also what picks up a player who has
    /// died and respawned as a new body.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class EntranceSignal : MonoBehaviour
    {
        [Tooltip("The door this light belongs to. Left empty it looks for one on this object or " +
                 "above it.")]
        [SerializeField] private KeyedEntrance entrance;

        [Tooltip("Renderer whose emission follows the lock, so the door itself glows rather than " +
                 "just being lit. Optional.")]
        [SerializeField] private Renderer glow;

        [SerializeField] private LockSignalTuning tuning = LockSignalTuning.Default;

        [Tooltip("Seconds between checks of whether the player is carrying this door's key, and of " +
                 "which body is currently the player.")]
        [SerializeField] private float keyCheckInterval = 0.2f;

        [Tooltip("Seconds the door takes to come up to full brightness once the player looks at " +
                 "it. Short — this is an answer to the player's own aim, and an answer that " +
                 "arrives late reads as unrelated to what they did.")]
        [SerializeField] private float aimRiseSeconds = 0.12f;

        [Tooltip("Seconds the door takes to dim again once they look away. Longer than the rise, " +
                 "so glancing past a row of doors does not set them strobing.")]
        [SerializeField] private float aimFallSeconds = 0.35f;

        [Tooltip("Seconds the light takes to fade out once the door is open. An open door has " +
                 "nothing left to say.")]
        [SerializeField] private float fadeSeconds = 1.5f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Light lamp;
        private MaterialPropertyBlock block;

        private float refusedAt = float.NegativeInfinity;
        private float nextKeyCheck;
        private bool hasKey;
        private float openedAt = float.NegativeInfinity;

        private Interactor interactor;
        private float aim01;

        private void Awake()
        {
            lamp = GetComponent<Light>();
            if (entrance == null) entrance = GetComponentInParent<KeyedEntrance>();

            if (entrance == null)
            {
                Debug.LogError("[EntranceSignal] " + name + " has no door to speak for.", this);
                enabled = false;
                return;
            }

            // A property block rather than a material instance, so every door in the castle shares
            // one material and lighting them differently does not become a material per door.
            if (glow != null) block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            entrance.Refused += OnRefused;
            entrance.Opened += OnOpened;
        }

        private void OnDisable()
        {
            entrance.Refused -= OnRefused;
            entrance.Opened -= OnOpened;
        }

        private void OnRefused() => refusedAt = Time.time;

        private void OnOpened() => openedAt = Time.time;

        private void Update()
        {
            if (entrance.IsOpen)
            {
                Fade();
                return;
            }

            RefreshPlayer();
            RefreshAim();

            LockSignal signal = LockSignal.Evaluate(hasKey, aim01, Time.time,
                                                    Time.time - refusedAt, tuning);
            Apply(signal.Colour, signal.Intensity);
        }

        /// <summary>
        /// Eases <see cref="aim01"/> towards whether the player's crosshair is on this door.
        /// <para>
        /// Asked of the interactor rather than worked out from angles and distances here, so there
        /// is one answer to "what is the player looking at" and the light cannot disagree with the
        /// key. <see cref="Interactor.HoveredInteractable"/> already excludes anything that would
        /// refuse the press — and a locked door deliberately does NOT refuse it, which is why a
        /// door you have no key for still lights up. See <see cref="KeyedEntrance.CanInteract"/>.
        /// </para>
        /// </summary>
        private void RefreshAim()
        {
            bool aimed = interactor != null
                         && ReferenceEquals(interactor.HoveredInteractable, entrance);

            float seconds = aimed ? aimRiseSeconds : aimFallSeconds;

            // A zero ease is a legitimate setting — it means "snap" — and is the one value the
            // division below cannot take.
            aim01 = seconds <= 0f
                ? (aimed ? 1f : 0f)
                : Mathf.MoveTowards(aim01, aimed ? 1f : 0f, Time.deltaTime / seconds);
        }

        /// <summary>
        /// Dims to nothing after the door opens, then stops driving the light for good — an open
        /// door is not a lock any more and should not keep breathing at the player.
        /// </summary>
        private void Fade()
        {
            float t = Mathf.Clamp01((Time.time - openedAt) / Mathf.Max(fadeSeconds, 0.0001f));
            Apply(tuning.OpenColour, Mathf.Lerp(tuning.RestingIntensity, 0f, t));

            if (t >= 1f) enabled = false;
        }

        /// <summary>
        /// Finds the player, and asks their hotbar whether this door's key is in it. Both on the
        /// same interval because both have the same answer to "when could this have changed" — the
        /// player picked something up, or the player is a different body than they were.
        /// </summary>
        private void RefreshPlayer()
        {
            if (Time.time < nextKeyCheck) return;
            nextKeyCheck = Time.time + Mathf.Max(keyCheckInterval, 0f);

            GameObject player = GameObject.FindGameObjectWithTag(SpawnClearance.PlayerTag);
            interactor = player != null ? player.GetComponentInChildren<Interactor>() : null;

            // An unlocked door is one the player can always open, so it shows the open colour
            // without anyone having to carry anything.
            if (entrance.RequiredKey == null)
            {
                hasKey = true;
                return;
            }

            var ring = player != null ? player.GetComponentInChildren<PlayerKeyRing>() : null;
            hasKey = ring != null && ring.Keys.Holds(entrance.RequiredKey);
        }

        private void Apply(Color colour, float intensity)
        {
            lamp.color = colour;
            lamp.intensity = intensity;

            if (glow == null) return;

            glow.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, colour * intensity);
            glow.SetPropertyBlock(block);
        }
    }
}
