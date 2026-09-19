// The machinery behind a ribbon drawn along the path of a swing — the part that is the same
// whoever is swinging and whatever the ribbon is made of.
//
// One of these rides one limb or one blade: a base anchor and a tip anchor that the animation
// carries, and for a short while after a swing is announced it records the ribbon between them
// WHILE they are moving fast — see SweepDetector for why speed rather than clip timing.
//
// Drawn with Graphics.DrawMesh in world space instead of from a child renderer: the ribbon has to
// stay where the blade WAS while the bones move on, and a renderer parented to the skeleton would
// drag the whole trail along with the swing it is supposed to be trailing behind.
//
// What a subclass supplies is only ever two things: where the announcement of a swing comes from,
// and what the ribbon is painted with. The player hears it from PlayerMeleeSwing and paints in
// light; a creature hears it from its MeleeStrike and paints in black. Splitting it here rather
// than writing the recording twice is the same argument SwingFlare makes one level down — there is
// one question ("where has this thing been while it was moving"), and two answers to it would
// drift apart the first time either was tuned.
//
// Presentation only. It reads the swing and never feeds back into it.
using UnityEngine;

namespace SpaceGame.Presentation
{
    public abstract class SwingRibbonTrail : MonoBehaviour
    {
        [Header("Anchors")]
        [Tooltip("The hilt end of the ribbon — a transform parented under whatever swings.")]
        [SerializeField] private Transform baseAnchor;

        [Tooltip("The far end of the ribbon.")]
        [SerializeField] private Transform tipAnchor;

        [Tooltip("The body the swing is measured against, so walking or turning does not count as " +
                 "swinging. Left empty, the subclass names one, or this transform is used.")]
        [SerializeField] private Transform bodyFrame;

        [SerializeField] private Material material;

        [Header("Shape")]
        [Tooltip("How far past the tip anchor the ribbon reaches, as a multiple of base-to-tip. " +
                 "Above 1 the ribbon overshoots the blade, which is what makes the arc read as " +
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

        private SlashRibbon ribbon;
        private SweepDetector detector;
        private Mesh mesh;
        private MaterialPropertyBlock properties;
        private float armedUntil = float.NegativeInfinity;
        private bool wasSweeping;

        /// <summary>
        /// Handed to every draw, so a subclass can tell its shader which of its looks to wear.
        /// Null until <see cref="Resolve"/> has passed, and never written to by this class.
        /// </summary>
        protected MaterialPropertyBlock Properties => properties;

        /// <summary>
        /// The frame the sweep is measured in. A subclass sets it from <see cref="Resolve"/> when
        /// the thing doing the swinging is only known at runtime — a weapon seated into a
        /// creature's hand does not know whose hand it is until it is in one.
        /// </summary>
        protected Transform BodyFrame
        {
            get => bodyFrame;
            set => bodyFrame = value;
        }

        /// <summary>
        /// Find whatever announces a swing to this trail, and say whether it was found. Returning
        /// false disables the component, so report what is missing before doing so.
        /// </summary>
        protected abstract bool Resolve();

        private void Awake()
        {
            if (!Resolve())
            {
                enabled = false;
                return;
            }

            if (bodyFrame == null) bodyFrame = transform;

            if (baseAnchor == null || tipAnchor == null || material == null)
            {
                Debug.LogError($"{GetType().Name} on '{name}' is missing an anchor or its material, " +
                               "so it will draw nothing.", this);
                enabled = false;
                return;
            }

            ribbon = new SlashRibbon();
            detector = new SweepDetector(sweepSpeed, holdSpeedRatio);
            mesh = new Mesh { name = GetType().Name };
            mesh.MarkDynamic();
            properties = new MaterialPropertyBlock();
        }

        protected virtual void OnDisable()
        {
            if (ribbon != null) ribbon.Clear();
            wasSweeping = false;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        /// <summary>Open the window in which a fast-moving tip draws. A swing has just started.</summary>
        protected void Arm() => armedUntil = Time.time + armedSeconds;

        /// <summary>Shut the window, whatever is left of it. Nothing more from this swing draws.</summary>
        protected void Disarm() => armedUntil = float.NegativeInfinity;

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

            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, gameObject.layer, null, 0, properties);
        }
    }
}
