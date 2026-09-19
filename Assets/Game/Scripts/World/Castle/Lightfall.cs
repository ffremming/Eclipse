using System;
using UnityEngine;
using SpaceGame.World;

namespace SpaceGame.Castle
{
    /// <summary>
    /// What the lighthouse does once it is lit: an orb of light above the tower, and the dark
    /// coming off the world.
    /// <para>
    /// Two <see cref="Reach"/>es, because the game uses the castle twice. The keep alone lights
    /// only the ground around itself — a room of daylight in a dark world, and a promise of what
    /// the real one will do. The full castle lights everything.
    /// </para>
    /// <para>
    /// The transition is slow on purpose. <c>GDC-L1-FEEL-0004</c> asks for layered feedback on the
    /// game's biggest moment, and it is right that this one earns the most — but its own exception
    /// covers Eclipse exactly: a restraint-driven look, where the whole world is drained so the
    /// player's small light is the only saturated thing on screen, and where a burst of juice would
    /// read as a bug rather than as dawn. So the layers here are duration, scale and a held beat
    /// before it starts, rather than a flash. Note that this project has no audio at all, so the
    /// redundancy across senses that principle asks for is not available: the moment is carried by
    /// one channel, and will land softer than it should until there is sound to put under it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Lightfall : MonoBehaviour
    {
        /// <summary>How far the light goes.</summary>
        public enum Reach
        {
            /// <summary>Around the castle only: a lit clearing in a dark world.</summary>
            Local,

            /// <summary>The whole island. The eclipse ends.</summary>
            World,
        }

        [Header("What it lights")]
        [SerializeField] private Reach reach = Reach.Local;

        [Tooltip("Metres the local light reaches. The first castle lights a 50 m square, so 25 m " +
                 "from the tower carries to its corners.")]
        [SerializeField] private float localRadius = 35f;

        [Tooltip("Brightness the local light settles at.")]
        [SerializeField] private float localIntensity = 14f;

        [Header("Timing")]
        [Tooltip("Beat between the blow landing and the light starting to come, in seconds. This " +
                 "is what makes the dawn feel caused by the blow rather than coincident with it.")]
        [SerializeField] private float holdSeconds = 1.1f;

        [Tooltip("Seconds the light takes to arrive. Long: this is a sunrise, not a light switch.")]
        [SerializeField] private float riseSeconds = 9f;

        [Header("The orb")]
        [Tooltip("The orb of light that forms over the tower. Left empty, nothing is spawned and " +
                 "only the lighting changes.")]
        [SerializeField] private GameObject orbPrefab;

        [Tooltip("Where the orb forms, usually just above the lantern deck.")]
        [SerializeField] private Transform orbAnchor;

        [Tooltip("How large the orb grows, as a multiple of its prefab's own scale.")]
        [SerializeField] private float orbScale = 6f;

        [Header("World")]
        [Tooltip("The atmosphere this drives, for Reach.World. Left empty it finds the one in the " +
                 "scene — there is only ever one.")]
        [SerializeField] private WorldAtmosphere atmosphere;

        /// <summary>Raised once the light has fully arrived, which is when the castle is finished.</summary>
        public event Action Complete;

        /// <summary>Whether the lighthouse has been lit.</summary>
        public bool Lit => progress != null && progress.Lit;

        private LightfallProgress progress;
        private Light localLamp;
        private Transform orb;
        private Vector3 orbFullScale;
        private bool announced;

        private void Awake()
        {
            progress = new LightfallProgress(holdSeconds, riseSeconds);

            if (reach == Reach.World && atmosphere == null)
                atmosphere = FindFirstObjectByType<WorldAtmosphere>();

            if (reach == Reach.World && atmosphere == null)
                Debug.LogError("[Lightfall] " + name + " is set to light the world, but the scene " +
                               "has no WorldAtmosphere to light. Nothing will change.", this);
        }

        /// <summary>
        /// Light it. Safe to call again — the world does not go back, and a player who keeps
        /// swinging at a lit beacon must not restart the dawn.
        /// </summary>
        public void Light()
        {
            if (!progress.Light()) return;

            SpawnOrb();
            if (reach == Reach.Local) SpawnLocalLamp();
        }

        private void Update()
        {
            if (!progress.Lit) return;

            progress.Tick(Time.deltaTime);
            float blend = progress.Blend;

            if (orb != null) orb.localScale = orbFullScale * blend;
            if (localLamp != null) localLamp.intensity = localIntensity * blend;
            if (atmosphere != null && reach == Reach.World) atmosphere.DaylightBlend = blend;

            if (announced || !progress.Finished) return;

            announced = true;
            Complete?.Invoke();
        }

        private void SpawnOrb()
        {
            if (orbPrefab == null) return;

            Transform anchor = orbAnchor != null ? orbAnchor : transform;
            GameObject spawned = Instantiate(orbPrefab, anchor.position, anchor.rotation);
            orb = spawned.transform;

            // Grown from nothing over the rise rather than popped in at size. Its full size is
            // remembered here because the prefab's own scale is the thing being multiplied, and
            // after the first frame the live scale is no longer that.
            orbFullScale = orb.localScale * orbScale;
            orb.localScale = Vector3.zero;
        }

        /// <summary>
        /// The lamp that lights the ground around the first castle. Built here rather than placed
        /// in the scene so its radius cannot disagree with <see cref="localRadius"/>.
        /// </summary>
        private void SpawnLocalLamp()
        {
            Transform anchor = orbAnchor != null ? orbAnchor : transform;

            var lampObject = new GameObject("Lightfall Lamp");
            lampObject.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

            localLamp = lampObject.AddComponent<Light>();
            localLamp.type = LightType.Point;
            localLamp.range = localRadius;
            localLamp.color = Color.white;
            localLamp.intensity = 0f;

            // Shadows off. A point light with this range over a castle full of brickwork is six
            // shadow maps of several thousand batches each, and the light it casts is meant to
            // read as the dark lifting rather than as a lamp somebody hung on the tower.
            localLamp.shadows = LightShadows.None;
        }
    }
}
