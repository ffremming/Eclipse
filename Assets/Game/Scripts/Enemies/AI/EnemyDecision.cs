// What the brain decided, for EnemyAgent to carry out.
//
// Deliberately all "what", no "how": a destination rather than a NavMeshAgent call, a flag saying a
// swing should happen rather than an Animator trigger. Everything that would need an Editor to run
// stays on the far side of this struct.
using UnityEngine;

namespace SpaceGame.Enemies
{
    public struct EnemyDecision
    {
        public EnemyState State;

        /// <summary>Where to walk. Ignore when <see cref="HasDestination"/> is false.</summary>
        public Vector3 Destination;

        /// <summary>False means stand still — mid-swing, or idling between strolls.</summary>
        public bool HasDestination;

        /// <summary>How fast to get there, in metres per second. Meaningless without a destination.</summary>
        public float Speed;

        /// <summary>Turn to look at the target. Used while attacking, when navigation is not steering.</summary>
        public bool FaceTarget;

        /// <summary>Swing now. True for exactly one tick per swing.</summary>
        public bool Attack;

        /// <summary>The stroll is over; the agent should pick a fresh spot inside the camp.</summary>
        public bool WantsNewWanderTarget;

        /// <summary>
        /// It just noticed the target itself, so it should tell the neighbours. True for exactly one
        /// tick, and never as a result of having heard someone else — otherwise a camp of goblins
        /// would shout each other awake in a loop that never settles.
        /// </summary>
        public bool ShoutAlert;
    }
}
