using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Chain
{
    /// <summary>
    /// A rope of point masses: Verlet integration for the motion, distance constraints to hold the
    /// segments to length, a sphere sweep per link for the world.
    /// <para>
    /// Nothing about the shape is authored. Where the chain hangs, how it swings when the holder
    /// turns, how it piles on the ground and how it snaps taut at the end of a lash all fall out of
    /// gravity, momentum and the fact that a segment cannot stretch. That is the difference between
    /// a chain and a mesh that plays a chain-shaped animation.
    /// </para>
    /// <para>
    /// A plain class, so the physics can be tested without the Editor. It must be stepped at a
    /// constant <c>dt</c>: Verlet keeps velocity as the gap between two positions, so a changing step
    /// silently changes every link's speed. The caller runs fixed sub-steps for that reason.
    /// </para>
    /// </summary>
    public sealed class ChainRope
    {
        private readonly ChainTuning tuning;
        private readonly Vector3[] position;
        private readonly Vector3[] previous;
        private readonly float[] inverseMass;
        private float length;

        /// <summary>A rope hanging straight down from <paramref name="anchor"/>.</summary>
        public ChainRope(ChainTuning tuning, Vector3 anchor, float length)
        {
            this.tuning = tuning;
            this.length = Mathf.Max(0.01f, length);

            int count = tuning.NodeCount;
            position = new Vector3[count];
            previous = new Vector3[count];
            inverseMass = new float[count];

            for (int i = 1; i < count; i++) inverseMass[i] = 1f;
            inverseMass[count - 1] = tuning.TipInverseMass;

            Reset(anchor);
        }

        /// <summary>The links from the anchor to the orb, in world space.</summary>
        public IReadOnlyList<Vector3> Points => position;

        public Vector3 Tip => position[position.Length - 1];

        /// <summary>
        /// How much rope is paid out, in metres. Changing it is how the chain extends: every segment
        /// is held to this length over the link count, so raising it fast throws the orb outwards.
        /// </summary>
        public float Length
        {
            get => length;
            set => length = Mathf.Max(0.01f, value);
        }

        /// <summary>How fast the orb moved in the last step, in metres per second.</summary>
        public Vector3 TipVelocity(float dt)
        {
            int tip = position.Length - 1;
            return (position[tip] - previous[tip]) / dt;
        }

        /// <summary>Lay the rope straight down from <paramref name="anchor"/>, at rest.</summary>
        public void Reset(Vector3 anchor)
        {
            float segment = length / (position.Length - 1);
            for (int i = 0; i < position.Length; i++)
            {
                position[i] = anchor + Vector3.down * (segment * i);
                previous[i] = position[i];
            }
        }

        /// <summary>Move the handle end. It is held by the hand, so nothing pushes it back.</summary>
        public void Pin(Vector3 anchor)
        {
            position[0] = anchor;
            previous[0] = anchor;
        }

        public void Step(float dt, Vector3 gravity, TipPull pull, IChainWorld world)
        {
            Integrate(dt, gravity);
            ApplyPull(pull);
            SatisfyLengths();
            CollideWithWorld(world);
        }

        private void Integrate(float dt, Vector3 gravity)
        {
            float keep = Mathf.Max(0f, 1f - tuning.Drag * dt);
            for (int i = 1; i < position.Length; i++)
            {
                Vector3 current = position[i];
                Vector3 velocity = (current - previous[i]) * keep;
                previous[i] = current;
                position[i] = current + velocity + gravity * (dt * dt);
            }
        }

        /// <summary>
        /// The hand on the orb. Not a teleport: the orb is pulled a fraction of the way each step, so
        /// it leads the chain out and the links follow it round the curve it drew.
        /// </summary>
        private void ApplyPull(TipPull pull)
        {
            if (pull.Fraction <= 0f) return;

            int tip = position.Length - 1;
            position[tip] = Vector3.Lerp(position[tip], pull.Target, pull.Fraction);
        }

        private void SatisfyLengths()
        {
            float segment = length / (position.Length - 1);
            int last = position.Length - 1;

            // Alternating direction, so the correction is not always pushed the same way along the
            // rope. A one-way sweep leaves the far end visibly stretched under a hard pull.
            for (int pass = 0; pass < tuning.Iterations; pass++)
            {
                if (pass % 2 == 0)
                    for (int i = 0; i < last; i++) SatisfyLength(i, i + 1, segment);
                else
                    for (int i = last - 1; i >= 0; i--) SatisfyLength(i, i + 1, segment);
            }
        }

        private void SatisfyLength(int a, int b, float segment)
        {
            Vector3 delta = position[b] - position[a];
            float distance = delta.magnitude;
            float weight = inverseMass[a] + inverseMass[b];

            // Two links on the same point have no direction to be separated along; gravity parts
            // them next step, and normalising a zero vector here would put a NaN into the rope.
            if (distance < 1e-6f || weight <= 0f) return;

            Vector3 correction = delta * ((distance - segment) / (distance * weight));
            position[a] += correction * inverseMass[a];
            position[b] -= correction * inverseMass[b];
        }

        /// <summary>
        /// Sweep each link from where it was last step to where it has ended up, and stop it at the
        /// first surface. A sweep rather than a push-out, so a lash moving several metres in one step
        /// cannot tunnel through a wall or an enemy.
        /// </summary>
        private void CollideWithWorld(IChainWorld world)
        {
            for (int i = 1; i < position.Length; i++)
            {
                Vector3 move = position[i] - previous[i];
                if (move.sqrMagnitude < 1e-10f) continue;

                if (!world.Sweep(previous[i], position[i], tuning.NodeRadius, out Vector3 centre, out Vector3 normal))
                    continue;

                // Slides rather than bounces: the part of the motion into the surface is dropped and
                // the rest is kept, less friction. A chain dropped on the ground lies there.
                Vector3 slide = move - normal * Vector3.Dot(move, normal);
                position[i] = centre + normal * tuning.Skin;
                previous[i] = position[i] - slide * (1f - tuning.Friction);
            }
        }
    }
}
