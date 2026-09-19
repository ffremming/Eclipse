using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// The carried lantern: a steady light on a handle, switched on and off.
    /// <para>
    /// The torch's counterpart, not a second torch. The torch is a weapon that happens to burn —
    /// swinging it is what it is for, so it has no switch. The lantern has no swing at all, which
    /// leaves its one use free to mean one thing: a press lights it or puts it out. It is what to
    /// carry when the fight is over and the walking begins, and what to put out when something is
    /// watching the light.
    /// </para>
    /// <para>
    /// It hangs by its loop. The prefab puts the carried model under the hanging transform with the
    /// loop at that transform's origin, and every frame that transform is eased back to upright, so
    /// the lantern lags behind the arm and settles rather than being welded to it. A lantern that
    /// tilts with the hand reads as a can held in a fist.
    /// </para>
    /// </summary>
    public class LanternArtifact : ToolItem
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>State key for whether it is lit. Written into save files — never rename.</summary>
        private const string LitKey = "lit";

        [Header("Light")]
        [Tooltip("The point light inside the glass. Its intensity is driven; whatever is authored " +
                 "on the component is overwritten. Its range is left as authored.")]
        [SerializeField] private Light flame;

        [Tooltip("What the light burns at while lit.")]
        [SerializeField] private float litIntensity = 30f;

        [Header("Flicker")]
        [Tooltip("How far the flame wanders from steady, as a fraction of its brightness. 0 is a " +
                 "lamp; the torch's flame is the loud end.")]
        [SerializeField, Range(0f, 1f)] private float flickerDepth = 0.1f;

        [Tooltip("How fast the flame wanders, in cycles per second.")]
        [SerializeField] private float flickerSpeed = 2.5f;

        [Header("Glass")]
        [Tooltip("The glass. Its emission follows the flame, so a lantern that is out is dark and " +
                 "one that flickers is seen to.")]
        [SerializeField] private Renderer glass;

        [Tooltip("The glass's emission at full brightness.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color glowColor = new Color(1f, 0.52f, 0.29f);

        [Header("Hang")]
        [Tooltip("The carried model, hung from its loop at this transform's origin. Its local +Y is " +
                 "the way up the lantern.")]
        [SerializeField] private Transform hanging;

        [Tooltip("How fast it settles upright after the hand tips it, per second. Higher is stiffer.")]
        [SerializeField] private float settleSpeed = 6f;

        private MaterialPropertyBlock glassBlock;
        private bool lit = true;

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);
            glassBlock = new MaterialPropertyBlock();

            // Hung at once. Easing from wherever the hand happens to have put it would show the
            // lantern falling into place on every equip.
            Hang(1f);
        }

        /// <summary>The whole effect: light it, or put it out.</summary>
        protected override void Use() => lit = !lit;

        public override void CaptureItemState(ItemState state)
        {
            base.CaptureItemState(state);

            // Only the exception is written. A lantern is lit when picked up, and a bag on every
            // slot to say so would be a bag with nothing to say.
            if (state != null && !lit) state.Set(LitKey, false);
        }

        public override void RestoreItemState(ItemState state)
        {
            base.RestoreItemState(state);
            lit = state == null || state.GetBool(LitKey, true);
        }

        private void LateUpdate()
        {
            Hang(1f - Mathf.Exp(-settleSpeed * Time.deltaTime));
            Burn();
        }

        /// <summary>
        /// Turns the model toward hanging straight down by <paramref name="blend"/> of the way. The
        /// shortest rotation that gets its up to world up, so the way the hand has turned it round
        /// is kept and only the tilt is taken out.
        /// </summary>
        private void Hang(float blend)
        {
            if (hanging == null) return;

            Quaternion upright = Quaternion.FromToRotation(hanging.up, Vector3.up) * hanging.rotation;
            hanging.rotation = Quaternion.Slerp(hanging.rotation, upright, blend);
        }

        /// <summary>The light and the glass, from the one flicker, so they cannot disagree.</summary>
        private void Burn()
        {
            float wander = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * 2f - 1f;
            float brightness = lit ? 1f + flickerDepth * wander : 0f;

            if (flame != null)
            {
                flame.intensity = litIntensity * brightness;
                flame.enabled = lit;
            }

            if (glass != null && glassBlock != null)
            {
                glass.GetPropertyBlock(glassBlock);
                glassBlock.SetColor(EmissionColorId, glowColor * brightness);
                glass.SetPropertyBlock(glassBlock);
            }
        }
    }
}
