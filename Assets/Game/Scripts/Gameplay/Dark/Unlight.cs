// A light that takes light away.
//
// The world is lit by the player's own light and almost nothing else, so the strongest thing an
// enemy's weapon can do to it is subtract. Where a LightBlade's tip light adds to what falls on the
// ground, this removes it: walk into the reach of one and the grass, the mushrooms and your own
// hands go out.
//
// The trick, and why it is behind a component instead of typed into a prefab:
//
// Unity clamps `Light.intensity` to zero — a negative intensity is silently dropped, which is the
// obvious way to try this and it does nothing. A negative COLOUR survives, all the way through
// `Color.linear` and into URP's additive light loop, where it subtracts from the light already
// accumulated on a surface. So the colour on the Light is the negation of `absorbs` below, written
// from here, and the Light's own colour swatch must not be tuned in the Inspector — it is an output.
//
// One more consequence of going through the colour: `Color.linear` takes the linear segment of the
// sRGB curve for anything at or below 0, so a component of -1 arrives as -1/12.92. Strengths here
// are therefore about thirteen times what the same brightness would be on an ordinary light, which
// is why they look absurd next to LightBladeBuilder's numbers and are not.
//
// Nothing can be darker than black: the subtraction stops when the surface runs out of light, so an
// unlight cannot drive the picture negative however hard it is driven.
using UnityEngine;

namespace SpaceGame.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class Unlight : MonoBehaviour
    {
        [Tooltip("The light this eats, as a positive colour — what would be REMOVED from a white " +
                 "surface. Warm here eats the torch and the light weapons and leaves the world " +
                 "cold, which is what makes the dark read as theirs rather than as night falling.")]
        [SerializeField] private Color absorbs = new Color(1f, 0.82f, 0.55f);

        [Tooltip("Strength while nothing is driving it. Set by whatever owns this, every frame, " +
                 "so this is only what it sits at before anything does.")]
        [SerializeField] private float strength = 8f;

        [Tooltip("How far the dark reaches, in metres.")]
        [SerializeField] private float range = 5f;

        private Light lamp;

        private void Awake()
        {
            lamp = GetComponent<Light>();
            lamp.type = LightType.Point;

            // A shadow cast by a light that subtracts is a patch that is LIGHTER than around it,
            // which reads as a hole in the effect rather than as a shadow.
            lamp.shadows = LightShadows.None;

            Apply();
        }

        /// <summary>How hard it is eating, in the units described at the top of this file.</summary>
        public float Strength
        {
            get => strength;
            set
            {
                strength = Mathf.Max(0f, value);
                Apply();
            }
        }

        /// <summary>How far the dark reaches, in metres.</summary>
        public float Range
        {
            get => range;
            set
            {
                range = Mathf.Max(0f, value);
                Apply();
            }
        }

        // Kept live while the Inspector is being dragged, so tuning the dark shows the dark.
        private void OnValidate()
        {
            if (lamp == null) lamp = GetComponent<Light>();
            Apply();
        }

        private void Apply()
        {
            if (lamp == null) return;

            lamp.color = new Color(-absorbs.r, -absorbs.g, -absorbs.b, 1f);
            lamp.intensity = strength;
            lamp.range = range;
            lamp.enabled = strength > 0.001f;
        }
    }
}
