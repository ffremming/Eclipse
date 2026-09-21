// The light the player sees by: a pair of lamps hanging in front of the eyes, and a cone down the
// line of sight.
//
// It lives on the body rather than on anything carried, which is the whole point. A light that
// belongs to the torch is a light the player loses the moment they reach for the chain, and a world
// this dark cannot have a weapon swap turn the screen black. The eyes are always there.
//
// The rig is built from code, like the wheel and the lantern, so there is no prefab to keep in step
// with the numbers here.
using SpaceGame.Gameplay;
using UnityEngine;

namespace SpaceGame.Characters
{
    [DisallowMultipleComponent]
    public sealed class EyeLights : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Read for the head bone the lamps hang off. Left empty, the animator on this body " +
                 "is used; a rig with no head falls back to a fixed height above the player's feet.")]
        [SerializeField] private Animator animator;

        [Tooltip("The player's light. The lamps dim with it, so running low is something the world " +
                 "shows before the lantern does. Left empty, the health on this body is used.")]
        [SerializeField] private HealthComponent health;

        [Header("Placement")]
        [Tooltip("How far above the head bone's origin the eyes sit, in metres.")]
        [SerializeField] private float eyeHeight = 0.09f;

        [Tooltip("How far in front of the eyes the lamps hang, in metres. Far enough that the face " +
                 "is not lit from inside its own skull, close enough to read as the player's own.")]
        [SerializeField] private float eyeForward = 0.12f;

        [Tooltip("Distance between the two lamps, in metres.")]
        [SerializeField] private float eyeSpacing = 0.13f;

        [Tooltip("Height above the player's feet the eyes fall back to when the rig has no head " +
                 "bone, in metres.")]
        [SerializeField] private float fallbackEyeHeight = 1.6f;

        [Header("Lamps")]
        [ColorUsage(false, true)]
        [SerializeField] private Color lampColor = new Color(0.86f, 0.9f, 1f);

        [Tooltip("How far the pair reaches at full light, in metres. Short on purpose: these are " +
                 "what gives the ground near the player shape, while the gaze is what gives distance.")]
        [SerializeField] private float lampRange = 14f;

        [SerializeField] private float lampIntensity = 30f;

        [Tooltip("Whether the lamps put shadows on what is close. Only one of the pair casts: two " +
                 "shadow maps from two points thirteen centimetres apart cost twice as much for a " +
                 "difference nobody can see.")]
        [SerializeField] private bool castShadows = true;

        [Header("Gaze")]
        [Tooltip("The long cone down the line of sight. This is where distance actually comes from " +
                 "— a point light spreads its energy over a whole sphere and is already almost " +
                 "nothing a few metres out, while a cone puts the same energy down one direction.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color gazeColor = new Color(0.78f, 0.82f, 0.95f);

        [SerializeField] private float gazeRange = 170f;
        [SerializeField] private float gazeAngle = 74f;
        [SerializeField] private float gazeInnerAngle = 26f;
        [SerializeField] private float gazeIntensity = 260f;

        [Tooltip("How fast the cone swings round to where the player is looking, in degrees per " +
                 "second. Instant tracking reads as a lamp bolted to the camera; a slight lag reads " +
                 "as a head turning.")]
        [SerializeField] private float gazeTurnSpeed = 520f;

        [Header("Dimming")]
        [Tooltip("What the lamps are worth on the last orb, as a fraction of full. Zero would be a " +
                 "player who cannot see to fight their way out of being nearly dead, so this floor " +
                 "is what stops losing from feeding on itself.")]
        [SerializeField, Range(0f, 1f)] private float dimFloor = 0.4f;

        [Tooltip("How fast the lamps follow the light level, per second. Orbs leave one at a time " +
                 "and an instant step down reads as a flicker rather than as a loss.")]
        [SerializeField] private float dimSpeed = 4f;

        private Transform rig;
        private Light leftLamp;
        private Light rightLamp;
        private Light gaze;
        private Transform head;
        private Camera view;

        /// <summary>How hard the lamps are burning, as a fraction of full, eased toward the level.</summary>
        private float glow = 1f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (health == null) health = GetComponent<HealthComponent>();

            head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;

            view = Camera.main;

            Build();

            // Placed and lit outright on the first frame. Easing in from nowhere would show the
            // lamps swinging into the face and brightening every time the player spawned.
            Place(snap: true);
            glow = Level();
            Burn();
        }

        /// <summary>
        /// The rig: a root the two lamps and the cone hang off, parented to the head so it dies
        /// with the body. Where it ends up is driven outright rather than left to the parenting,
        /// because a head bone's exported axes are arbitrary and "in front of the eyes" has to mean
        /// in front of the eyes on every rig.
        /// </summary>
        private void Build()
        {
            rig = new GameObject("Eye Lights").transform;
            rig.SetParent(head != null ? head : transform, false);

            leftLamp = BuildLamp("Left Eye", -eyeSpacing * 0.5f, shadowed: false);
            rightLamp = BuildLamp("Right Eye", eyeSpacing * 0.5f, shadowed: castShadows);
            gaze = BuildGaze();
        }

        private Light BuildLamp(string name, float offset, bool shadowed)
        {
            GameObject lampObject = new GameObject(name);
            lampObject.transform.SetParent(rig, false);
            lampObject.transform.localPosition = new Vector3(offset, 0f, 0f);

            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = lampColor;
            lamp.range = lampRange;
            lamp.shadows = shadowed ? LightShadows.Soft : LightShadows.None;

            // Pulled off the defaults because a light this close to the body shadow-acnes the
            // player's own shoulders otherwise.
            lamp.shadowBias = 0.1f;
            lamp.shadowNormalBias = 0.6f;
            return lamp;
        }

        /// <summary>
        /// The cone. Shadowless: a spot shadow covering this range needs a map so coarse that what
        /// it draws is blocky rather than informative, and the lamps are what put shadows where the
        /// player can read them.
        /// </summary>
        private Light BuildGaze()
        {
            GameObject gazeObject = new GameObject("Gaze");
            gazeObject.transform.SetParent(rig, false);

            Light light = gazeObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = gazeColor;
            light.range = gazeRange;
            light.spotAngle = gazeAngle;
            light.innerSpotAngle = gazeInnerAngle;
            light.shadows = LightShadows.None;
            return light;
        }

        // After the animation, so the lamps sit on the head the frame is actually drawn with rather
        // than on where it was before the clip moved it.
        private void LateUpdate()
        {
            Place(snap: false);

            glow = Mathf.Lerp(glow, Level(), 1f - Mathf.Exp(-dimSpeed * Time.deltaTime));
            Burn();
        }

        /// <summary>
        /// Puts the rig in front of the eyes and turns it toward the line of sight — the whole way
        /// when <paramref name="snap"/>, otherwise at the gaze's own turn speed.
        /// </summary>
        private void Place(bool snap)
        {
            Vector3 forward = Forward();

            Vector3 eyes = head != null
                ? head.position
                : transform.position + Vector3.up * fallbackEyeHeight;

            rig.position = eyes + Vector3.up * eyeHeight + forward * eyeForward;

            // Turned toward the view rather than carried by the head bone: a cone welded to an
            // animated head sprays the landscape on every step, which reads as a strobe exactly
            // when the player most needs to see.
            Quaternion aim = Quaternion.LookRotation(forward);
            rig.rotation = snap
                ? aim
                : Quaternion.RotateTowards(rig.rotation, aim, gazeTurnSpeed * Time.deltaTime);
        }

        /// <summary>Puts the current glow on all three lights, so they cannot disagree about it.</summary>
        private void Burn()
        {
            leftLamp.intensity = lampIntensity * glow;
            rightLamp.intensity = lampIntensity * glow;
            gaze.intensity = gazeIntensity * glow;
        }

        /// <summary>
        /// What the player's remaining light is worth to the lamps: full at full health, never less
        /// than the floor.
        /// </summary>
        private float Level()
        {
            if (health == null) return 1f;

            float remaining = (float)health.GetHealth / Mathf.Max(health.GetMaxHealth, 1);
            return Mathf.Lerp(dimFloor, 1f, Mathf.Clamp01(remaining));
        }

        /// <summary>
        /// The direction the player is looking. The camera's, not the body's, because on a
        /// third-person boom the body lags the camera through every turn — and a lamp that lags the
        /// turn is a lamp always pointing at what the player has just stopped looking at.
        /// </summary>
        private Vector3 Forward()
        {
            if (view == null) view = Camera.main;
            return view != null ? view.transform.forward : transform.forward;
        }
    }
}
