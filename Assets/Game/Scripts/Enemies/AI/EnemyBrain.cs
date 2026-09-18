// The whole of an enemy's decision-making, with no Unity in it.
//
// This replaces a stack of prioritised behaviour modules with a four-state machine, because the
// behaviour asked for is four states wide: stroll around camp, notice you, close, swing. A module
// system earns its indirection when behaviours combine in ways a switch cannot express; this one
// does not, and the cost of it was 12,000 lines nothing could test.
//
// The rule that shapes the rest: aggro is a DEADLINE, not a flag. Everything that notices the
// target — seeing them, hearing an ally, being hit, them walking into camp — pushes the deadline
// forward. Losing sight does not clear it, so ducking behind a rock buys you a few seconds of being
// chased rather than an instant reset.
using UnityEngine;

namespace SpaceGame.Enemies
{
    public sealed class EnemyBrain
    {
        private readonly EnemySettings settings;

        private EnemyState state = EnemyState.Wander;
        private float aggroUntil = float.NegativeInfinity;
        private float nextAttackTime = float.NegativeInfinity;
        private float wanderResumeTime;
        private bool pausing;

        public EnemyBrain(EnemySettings settings) => this.settings = settings;

        public EnemyState State => state;

        /// <summary>True while it is angry at something and has not yet forgotten about it.</summary>
        public bool IsAggravated(float time) => time < aggroUntil;

        /// <summary>
        /// Decide what to do at <paramref name="time"/>, given what is currently sensed.
        /// Call once per tick: a swing and a shout are consumed by being returned.
        /// </summary>
        public EnemyDecision Tick(in EnemySenses senses, float time)
        {
            bool wasAggravated = time < aggroUntil;
            bool sawTarget = senses.HasTarget && senses.CanSeeTarget;
            bool provoked = senses.HasTarget &&
                            (senses.TookDamage || senses.HeardAlert || senses.TargetInsideBase);

            if (sawTarget || provoked) aggroUntil = time + settings.AggroMemory;

            // Past the leash it gives up completely, rather than merely walking home still angry —
            // otherwise it would turn around and set off again the moment it was back inside.
            bool leashed = Vector3.Distance(senses.Position, senses.HomePosition) > settings.LeashRadius;
            if (leashed) aggroUntil = float.NegativeInfinity;

            bool aggravated = senses.HasTarget && time < aggroUntil;

            // Only ever announced by an enemy that worked it out for itself. A goblin that was told
            // does not pass it on, which is what keeps one shout from echoing around a camp forever.
            bool shout = !wasAggravated && aggravated && (sawTarget || senses.TookDamage);

            EnemyDecision decision = default;
            decision.ShoutAlert = shout;

            if (aggravated)
                Fight(ref decision, senses, time);
            else
                GoHomeOrWander(ref decision, senses, time);

            decision.State = state;
            return decision;
        }

        /// <summary>Close the distance, then stand and swing once inside reach.</summary>
        private void Fight(ref EnemyDecision decision, in EnemySenses senses, float time)
        {
            pausing = false;

            if (Vector3.Distance(senses.Position, senses.TargetPosition) > settings.AttackRange)
            {
                state = EnemyState.Chase;
                decision.Destination = senses.TargetPosition;
                decision.HasDestination = true;
                return;
            }

            state = EnemyState.Attack;

            // Standing still, and turning by hand. Navigation steers by facing where it is going,
            // and an enemy that has arrived is no longer going anywhere — left to it, it would
            // swing at whichever way it happened to stop.
            decision.HasDestination = false;
            decision.FaceTarget = true;

            if (time < nextAttackTime) return;

            nextAttackTime = time + settings.AttackCooldown;
            decision.Attack = true;
        }

        /// <summary>Calm: walk back if it has strayed, otherwise stroll the camp.</summary>
        private void GoHomeOrWander(ref EnemyDecision decision, in EnemySenses senses, float time)
        {
            float homeDistance = Vector3.Distance(senses.Position, senses.HomePosition);

            // Hysteresis on purpose: it leaves Wander at the wander radius but only re-enters it
            // once it is well inside, at the arrival radius. Matching the two would leave an enemy
            // that stopped exactly on the line flipping between states every tick.
            if (state == EnemyState.ReturnHome
                ? homeDistance > settings.ArrivalRadius
                : homeDistance > settings.WanderRadius)
            {
                state = EnemyState.ReturnHome;
                pausing = false;
                decision.Destination = senses.HomePosition;
                decision.HasDestination = true;
                return;
            }

            state = EnemyState.Wander;

            if (senses.HasWanderTarget && !senses.ReachedWanderTarget)
            {
                pausing = false;
                decision.Destination = senses.WanderTarget;
                decision.HasDestination = true;
                return;
            }

            // Arrived, or never had anywhere to go: stand about for a moment before choosing the
            // next spot. Without the pause a camp reads as a crowd of people who never stop moving.
            if (!pausing)
            {
                pausing = true;
                wanderResumeTime = time + settings.WanderPause;
                return;
            }

            if (time < wanderResumeTime) return;

            pausing = false;
            decision.WantsNewWanderTarget = true;
        }
    }
}
