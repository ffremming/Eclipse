using System;
using UnityEngine;

namespace SpaceGame.Chain
{
    /// <summary>How the rope behaves. Serialized on the weapon so it is tuned in the Inspector.</summary>
    [Serializable]
    public sealed class ChainTuning
    {
        [Tooltip("Point masses in the rope. More is smoother and costs a sphere cast each per step.")]
        [SerializeField] private int nodeCount = 28;

        [Tooltip("How many times per step the segment lengths are enforced. Low is stretchy and " +
                 "rubbery, high is a rigid cable. A chain wants it high: at 10 a lash stretched the " +
                 "rope to more than twice its length as it was reeled in.")]
        [SerializeField] private int iterations = 50;

        [Tooltip("Air resistance, per second. Higher and the chain settles quickly; lower and it " +
                 "keeps swinging.")]
        [SerializeField] private float drag = 0.8f;

        [Tooltip("How much sideways speed a link loses when it drags along a surface, 0 to 1.")]
        [SerializeField] private float friction = 0.35f;

        [Tooltip("Radius of a link against the world, in metres.")]
        [SerializeField] private float nodeRadius = 0.03f;

        [Tooltip("How far a link is held off a surface it landed on, in metres. Keeps the next " +
                 "sweep from starting inside it.")]
        [SerializeField] private float skin = 0.005f;

        [Tooltip("1 over the orb's mass, relative to a link's 1. Lower is heavier: a heavy orb " +
                 "carries the chain behind it instead of being dragged by it, which is what makes " +
                 "the tip crack.")]
        [SerializeField] private float tipInverseMass = 0.25f;

        public int NodeCount => Mathf.Max(3, nodeCount);
        public int Iterations => Mathf.Max(1, iterations);
        public float Drag => Mathf.Max(0f, drag);
        public float Friction => Mathf.Clamp01(friction);
        public float NodeRadius => Mathf.Max(0f, nodeRadius);
        public float Skin => Mathf.Max(0f, skin);
        public float TipInverseMass => Mathf.Max(1e-3f, tipInverseMass);
    }
}
