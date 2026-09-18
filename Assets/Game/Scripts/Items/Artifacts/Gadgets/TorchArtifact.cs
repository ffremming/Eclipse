using UnityEngine;
using SpaceGame.World;

namespace SpaceGame.Items
{
    /// <summary>
    /// The carried torch: the player's own light, and in a world this dark the only light that
    /// falls on anything.
    /// <para>
    /// One button cycles it, off through every setting and back to off, rather than one button for
    /// on and another for brighter. The hotbar gives an item a single Use action, and a torch whose
    /// second control lived on a key nobody could find would be a setting most players never saw.
    /// Cycling costs one press to reach any state and is learnable in the first three presses.
    /// </para>
    /// <para>
    /// The light is a real <see cref="Light"/>, not a shader effect. Everything the light shaders
    /// draw is additive self-glow that leaves the ground black; the illumination the player
    /// navigates by has to come from the renderer's own lighting or it does not exist.
    /// </para>
    /// </summary>
    public class TorchArtifact : ToolItem
    {
        /// <summary>State key for the current setting. Written into save files — never rename.</summary>
        private const string LevelKey = "torchLevel";

        [Header("Light")]
        [Tooltip("The light on this prefab. Its range, colour and intensity are driven from the " +
                 "settings below, so whatever is authored on the component itself is overwritten.")]
        [SerializeField] private Light torchLight;

        [Tooltip("Off is setting 0 and is not listed. These are what the cycle steps through.")]
        [SerializeField] private TorchLevel[] levels =
        {
            // Ember: barely a light. Short, red and restless — enough to find your feet and not
            // enough to find anything else, which is what makes turning it up feel like a decision.
            new TorchLevel("Ember", 5f, 1.1f, new Color(1f, 0.38f, 0.13f), 0.16f, 9f),

            // Burning: the working setting. Reaches far enough to read a space, and steady enough
            // that the shadows it throws stay legible rather than swimming.
            new TorchLevel("Burning", 11f, 2.4f, new Color(1f, 0.55f, 0.24f), 0.08f, 7f),

            // Blazing: the whole room, and paler for it. The walk from ember to blazing is the same
            // walk the light shaders make from fringe to core, so the torch and the attacks read as
            // the same light at different strengths.
            new TorchLevel("Blazing", 19f, 4.2f, new Color(1f, 0.74f, 0.48f), 0.04f, 5f),
        };

        [Header("Feel")]
        [Tooltip("How long the light takes to reach a new setting, in seconds. A torch that snaps " +
                 "between settings reads as a switch rather than as a flame catching.")]
        [SerializeField] private float settleTime = 0.18f;

        [Tooltip("Metres beyond the light's range that the ash motes still react. Slightly wider " +
                 "than the light itself, so the air ahead hints at the reach before the ground does.")]
        [SerializeField] private float ashReachBonus = 3f;

        /// <summary>0 is off; 1..levels.Length are the settings.</summary>
        private int level;

        private float shownIntensity;
        private float shownRange;
        private WorldAtmosphere atmosphere;

        /// <summary>The setting the torch is on, or null when it is out.</summary>
        private TorchLevel Current =>
            level >= 1 && level <= levels.Length ? levels[level - 1] : null;

        /// <summary>How many states the cycle has, counting off.</summary>
        private int StateCount => levels.Length + 1;

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);

            // Found on equip rather than per frame. Equipping is rare, and a torch that searched
            // the scene every frame for the atmosphere would be paying for something that cannot
            // change while it is in a hand.
            atmosphere = FindFirstObjectByType<WorldAtmosphere>();

            // A torch handed over while lit should already be lit, with no fade from dark, because
            // the fade is meant to read as the flame catching and nothing is catching here.
            ApplyLevel(instant: true);
        }

        public override void OnUnequipped(GameObject holder)
        {
            // Stop the motes reacting to a torch that is no longer in the world. Without this they
            // keep glowing around the last place it was held.
            if (atmosphere != null) atmosphere.TrackLight(null, 0f);
            atmosphere = null;

            base.OnUnequipped(holder);
        }

        /// <summary>Advances the cycle. Off is part of the cycle, so this also puts it out.</summary>
        protected override void Use()
        {
            level = (level + 1) % StateCount;
            ApplyLevel(instant: false);
        }

        private void LateUpdate()
        {
            if (torchLight == null) return;

            TorchLevel current = Current;
            float targetIntensity = current != null ? current.Intensity : 0f;
            float targetRange = current != null ? current.Range : 0f;

            // MoveTowards rather than an exponential ease, for the same reason the aim rig uses it:
            // an exponential approaches the target and never arrives, and a torch that settles at
            // 99.7% of its setting never quite matches the one the player last saw.
            float step = settleTime <= 0f ? 1f : Time.deltaTime / settleTime;
            shownIntensity = Mathf.MoveTowards(shownIntensity, targetIntensity, step * Mathf.Max(targetIntensity, 1f));
            shownRange = Mathf.MoveTowards(shownRange, targetRange, step * Mathf.Max(targetRange, 1f));

            float wander = 1f;
            if (current != null && current.Flicker > 0f)
            {
                // Perlin rather than a sine: a sine is a pulse, and a pulse reads as a machine.
                // Sampled against this instance's id so two torches in one scene never flicker in
                // step, which is what would give away that they are the same object.
                float seed = GetInstanceID() * 0.017f;
                float n = Mathf.PerlinNoise(Time.time * current.FlickerSpeed, seed);
                wander = 1f + (n - 0.5f) * 2f * current.Flicker;
            }

            torchLight.enabled = shownIntensity > 0.001f;
            torchLight.intensity = shownIntensity * wander;
            torchLight.range = shownRange;
            if (current != null) torchLight.color = current.Colour;

            // Tell the ash where the light is, every frame, because the torch moves with the hand.
            if (atmosphere != null)
            {
                float reach = shownRange > 0f ? shownRange + ashReachBonus : 0f;
                atmosphere.TrackLight(torchLight.transform, reach);
            }
        }

        /// <summary>Points the light at the current setting, optionally without the settle.</summary>
        private void ApplyLevel(bool instant)
        {
            if (torchLight == null) return;

            TorchLevel current = Current;
            if (instant)
            {
                shownIntensity = current != null ? current.Intensity : 0f;
                shownRange = current != null ? current.Range : 0f;
            }

            torchLight.enabled = shownIntensity > 0.001f;
        }

        public override void CaptureItemState(ItemState state)
        {
            base.CaptureItemState(state);

            // Only written when the torch is actually lit. Storing a 0 for every torch in every
            // slot would put a bag on slots that hold nothing worth remembering.
            if (state != null && level > 0) state.Set(LevelKey, level);
        }

        public override void RestoreItemState(ItemState state)
        {
            base.RestoreItemState(state);

            // Clamped because the level list can be retuned between the save and the load, and a
            // stored index past the end of a shortened list would otherwise throw on the first
            // frame after loading rather than quietly falling back to the brightest setting.
            level = state == null ? 0 : Mathf.Clamp(state.GetInt(LevelKey, 0), 0, levels.Length);
            ApplyLevel(instant: true);
        }
    }
}
