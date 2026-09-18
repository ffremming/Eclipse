// Everything the brain is allowed to know, gathered once per tick.
//
// The brain takes one of these and returns an EnemyDecision. It never reaches for a Transform, a
// Physics call or Time.time, which is the whole reason the state machine can be tested without
// entering play mode: a test hands it a struct and reads the answer.
using UnityEngine;

namespace SpaceGame.Enemies
{
    public struct EnemySenses
    {
        /// <summary>Where this enemy is standing.</summary>
        public Vector3 Position;

        /// <summary>The middle of its camp — where it wanders around and retreats to.</summary>
        public Vector3 HomePosition;

        /// <summary>False when there is no player, or the player is dead. Nothing to fight.</summary>
        public bool HasTarget;

        public Vector3 TargetPosition;

        /// <summary>Clear line of sight, inside the view cone, this tick.</summary>
        public bool CanSeeTarget;

        /// <summary>An ally within earshot shouted since the last tick.</summary>
        public bool HeardAlert;

        /// <summary>This enemy was hurt since the last tick. Being shot from behind still aggros.</summary>
        public bool TookDamage;

        /// <summary>The target is standing inside the camp. Walking into the camp wakes it.</summary>
        public bool TargetInsideBase;

        /// <summary>Where this enemy is currently strolling, if it picked a spot.</summary>
        public Vector3 WanderTarget;

        public bool HasWanderTarget;

        /// <summary>It got where it was strolling to, and needs somewhere new to go.</summary>
        public bool ReachedWanderTarget;
    }
}
