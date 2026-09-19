// The slash of light a swing leaves in the air.
//
// One of these rides one limb: a base anchor and a tip anchor, parented to bones so the animation
// carries them. The sword arm gets one, and each leg gets one for the kick. It listens for the
// swing the player just started, and for a short while afterwards it records the ribbon between
// its anchors WHILE they are moving fast — see SweepDetector for why speed rather than clip timing.
//
// Drawn with Graphics.DrawMesh in world space instead of from a child renderer: the ribbon has to
// stay where the blade WAS while the bones move on, and a renderer parented to the skeleton would
// drag the whole trail along with the swing it is supposed to be trailing behind.
//
// Whose colours it wears depends on what is in the hand. Every weapon gets the ribbon, but a weapon
// whose own light already has a palette — the chain's orb — asks for that palette instead of the
// four-hue spectrum, so the light of a swing never introduces a colour the weapon does not have.
//
// Presentation only. It reads the swing and never feeds back into it.
using SpaceGame.Items;
using SpaceGame.Presentation;
using UnityEngine;

namespace SpaceGame.Characters
{
    public class SwingTrail : MonoBehaviour
    {
        [SerializeField] private PlayerMeleeSwing swing;

        [Tooltip("Read for the held weapon's swing palette. Left empty, the one on the same body " +
                 "as the swing is used.")]
        [SerializeField] private EquipmentController equipment;

        [Tooltip("Which attacks light this trail. The sword arm takes Slash and JumpAttack; a leg " +
                 "takes Kick.")]
        [SerializeField] private SwingKind armedBy = SwingKind.Slash | SwingKind.JumpAttack;

        [Header("Anchors")]
        [Tooltip("The hilt end of the ribbon — a transform parented under the bone that swings.")]
        [SerializeField] private Transform baseAnchor;

        [Tooltip("The far end of the ribbon.")]
        [SerializeField] private Transform tipAnchor;

        [Tooltip("The body the swing is measured against, so walking or turning does not count as " +
                 "swinging. Left empty, this transform is used.")]
        [SerializeField] private Transform bodyFrame;

        [SerializeField] private Material material;

        [Header("Shape")]
        [Tooltip("How far past the tip anchor the ribbon reaches, as a multiple of base-to-tip. " +
                 "Above 1 the light overshoots the blade, which is what makes the arc read as " +
                 "bigger than the weapon that threw it.")]
        [SerializeField] private float lengthScale = 1.6f;

        [Tooltip("Seconds a point of the ribbon lives. Longer is a longer tail and a heavier blow.")]
        [SerializeField] private float lifetime = 0.45f;

        [Tooltip("How sharply the ribbon narrows towards its oldest end. 0 leaves it full width " +
                 "and cut off; higher tapers to a point sooner.")]
        [SerializeField] private float taperPower = 0.6f;

        [Tooltip("Curve segments drawn between each pair of recorded frames.")]
        [SerializeField] private int subdivisions = 4;

        [Header("Emission")]
        [Tooltip("Seconds after a swing starts during which the ribbon may be drawn. Long enough " +
                 "to cover the longest swing clip; the speed gate does the real work inside it.")]
        [SerializeField] private float armedSeconds = 1.1f;

        [Tooltip("Metres a second the tip has to move, relative to the body, to start drawing.")]
        [SerializeField] private float sweepSpeed = 4f;

        [Tooltip("Fraction of the sweep speed the tip may slow to before the ribbon breaks off.")]
        [Range(0f, 1f)]
        [SerializeField] private float holdSpeedRatio = 0.5f;

        private static readonly int OrbPaletteId = Shader.PropertyToID("_OrbPalette");

        private SlashRibbon ribbon;
        private SweepDetector detector;
        private Mesh mesh;
        private MaterialPropertyBlock palette;
        private float armedUntil = float.NegativeInfinity;
        private bool wasSweeping;

        private void Awake()
        {
            if (swing == null) swing = GetComponentInParent<PlayerMeleeSwing>();
            if (bodyFrame == null) bodyFrame = transform;
            if (equipment == null && swing != null) equipment = swing.GetComponent<EquipmentController>();

            if (swing == null || baseAnchor == null || tipAnchor == null || material == null)
            {
                Debug.LogError($"{nameof(SwingTrail)} on '{name}' is missing its swing, an anchor or " +
                               "its material, so it will draw nothing.", this);
                enabled = false;
                return;
            }

            ribbon = new SlashRibbon();
            detector = new SweepDetector(sweepSpeed, holdSpeedRatio);
            mesh = new Mesh { name = "SwingTrail" };
            mesh.MarkDynamic();
            palette = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (swing != null) swing.Swung += OnSwung;
        }

        private void OnDisable()
        {
            if (swing != null) swing.Swung -= OnSwung;
            if (ribbon != null) ribbon.Clear();
            wasSweeping = false;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private void OnSwung(SwingKind kind)
        {
            if ((kind & armedBy) == 0) return;

            armedUntil = Time.time + armedSeconds;
            palette.SetFloat(OrbPaletteId, (float)HeldPalette());
        }

        // Asked when a swing starts, not every frame, so swapping weapons on its own does not
        // recolour light that is already in the air.
        private SlashPalette HeldPalette()
        {
            return equipment != null && equipment.HeldUsable is LightWeapon weapon
                ? weapon.SwingPalette
                : SlashPalette.Spectrum;
        }

        // After the animator has posed the bones for this frame, or the ribbon would trail the
        // pose from the frame before.
        private void LateUpdate()
        {
            float now = Time.time;

            Vector3 tip = tipAnchor.position;
            bool moving = detector.Update(bodyFrame.InverseTransformPoint(tip), Time.deltaTime);
            bool sweeping = moving && now < armedUntil;

            if (sweeping)
            {
                Vector3 bladeBase = baseAnchor.position;
                ribbon.Add(bladeBase, bladeBase + (tip - bladeBase) * lengthScale, now, wasSweeping);
            }

            wasSweeping = sweeping;

            ribbon.Prune(now, lifetime);
            if (!ribbon.HasSamples) return;

            ribbon.WriteTo(mesh, now, lifetime, subdivisions, taperPower);
            if (mesh.vertexCount == 0) return;

            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, gameObject.layer, null, 0, palette);
        }
    }
}
