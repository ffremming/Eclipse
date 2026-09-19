// The enemy state machine, exercised without an Editor.
//
// This is the whole reason EnemyBrain is a plain class rather than logic inside EnemyAgent's Update:
// every transition below is one a playtest would take minutes to reproduce and seconds to miss.
using NUnit.Framework;
using SpaceGame.Enemies;
using UnityEngine;

namespace SpaceGame.Tests
{
    public class EnemyBrainTests
    {
        private static EnemySettings Settings => new EnemySettings
        {
            AttackRange = 2f,
            AttackCooldown = 1f,
            LeashRadius = 20f,
            AggroMemory = 5f,
            WanderRadius = 10f,
            WanderPause = 2f,
            ArrivalRadius = 1f,
            WalkSpeed = 2f,
            ChaseSpeed = 5f,
        };

        private static EnemySenses Calm(Vector3 position) => new EnemySenses
        {
            Position = position,
            HomePosition = Vector3.zero,
        };

        private static EnemySenses Seeing(Vector3 position, Vector3 targetPosition)
        {
            EnemySenses senses = Calm(position);
            senses.HasTarget = true;
            senses.TargetPosition = targetPosition;
            senses.CanSeeTarget = true;
            return senses;
        }

        [Test]
        public void SeeingTheTargetChasesAndShoutsExactlyOnce()
        {
            var brain = new EnemyBrain(Settings);
            EnemySenses senses = Seeing(Vector3.zero, new Vector3(0f, 0f, 8f));

            EnemyDecision first = brain.Tick(senses, 0f);
            Assert.AreEqual(EnemyState.Chase, first.State);
            Assert.IsTrue(first.HasDestination);
            Assert.IsTrue(first.ShoutAlert, "noticing the target should alert the camp");

            // Still staring at them a tick later. A shout per tick would have the whole camp
            // re-alerting each other for as long as the player stayed visible.
            EnemyDecision second = brain.Tick(senses, 0.1f);
            Assert.IsFalse(second.ShoutAlert, "the shout must not repeat while it stays aggravated");
        }

        [Test]
        public void HearingAnAllyAggrosButDoesNotShoutBack()
        {
            var brain = new EnemyBrain(Settings);

            EnemySenses senses = Calm(Vector3.zero);
            senses.HasTarget = true;
            senses.TargetPosition = new Vector3(0f, 0f, 9f);
            senses.HeardAlert = true;

            EnemyDecision decision = brain.Tick(senses, 0f);

            Assert.AreEqual(EnemyState.Chase, decision.State);
            Assert.IsFalse(decision.ShoutAlert,
                "an enemy that was told must not pass it on, or a camp shouts itself awake forever");
        }

        [Test]
        public void InsideAttackRangeItStopsSwingsAndRespectsTheCooldown()
        {
            var brain = new EnemyBrain(Settings);
            EnemySenses senses = Seeing(Vector3.zero, new Vector3(0f, 0f, 1.5f));

            EnemyDecision first = brain.Tick(senses, 0f);
            Assert.AreEqual(EnemyState.Attack, first.State);
            Assert.IsTrue(first.Attack);
            Assert.IsFalse(first.HasDestination, "it should stand its ground to swing");
            Assert.IsTrue(first.FaceTarget);

            Assert.IsFalse(brain.Tick(senses, 0.5f).Attack, "inside the cooldown");
            Assert.IsTrue(brain.Tick(senses, 1.1f).Attack, "past the cooldown");
        }

        [Test]
        public void LosingSightKeepsItComingUntilAggroMemoryRunsOut()
        {
            var brain = new EnemyBrain(Settings);
            brain.Tick(Seeing(Vector3.zero, new Vector3(0f, 0f, 8f)), 0f);

            EnemySenses blind = Calm(Vector3.zero);
            blind.HasTarget = true;
            blind.TargetPosition = new Vector3(0f, 0f, 8f);

            Assert.AreEqual(EnemyState.Chase, brain.Tick(blind, 3f).State,
                "ducking behind cover must not switch it off instantly");
            Assert.AreEqual(EnemyState.Wander, brain.Tick(blind, 6f).State,
                "and it should give up once the memory expires");
        }

        [Test]
        public void PastTheLeashItGivesUpEvenWithTheTargetInPlainSight()
        {
            var brain = new EnemyBrain(Settings);
            var farFromHome = new Vector3(0f, 0f, 25f);

            EnemyDecision decision = brain.Tick(Seeing(farFromHome, new Vector3(0f, 0f, 26f)), 0f);

            Assert.AreEqual(EnemyState.ReturnHome, decision.State);
            Assert.AreEqual(Vector3.zero, decision.Destination, "it should head back to camp");
            Assert.IsFalse(brain.IsAggravated(0f), "and stop being angry, or it turns straight round");
        }

        [Test]
        public void ItStrollsAtAWalkAndHuntsAtARun()
        {
            var brain = new EnemyBrain(Settings);

            EnemySenses strolling = Calm(new Vector3(0f, 0f, 3f));
            strolling.HasWanderTarget = true;
            strolling.WanderTarget = new Vector3(4f, 0f, 3f);

            EnemyDecision walk = brain.Tick(strolling, 0f);
            Assert.AreEqual(EnemyState.Wander, walk.State);
            Assert.AreEqual(2f, walk.Speed, "a calm enemy should not cross camp at full tilt");

            EnemyDecision run = brain.Tick(Seeing(new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, 20f)), 1f);
            Assert.AreEqual(EnemyState.Chase, run.State);
            Assert.AreEqual(5f, run.Speed);
        }

        [Test]
        public void ItPausesBetweenStrollsBeforeAskingForSomewhereNew()
        {
            var brain = new EnemyBrain(Settings);

            EnemySenses arrived = Calm(new Vector3(0f, 0f, 3f));
            arrived.HasWanderTarget = true;
            arrived.WanderTarget = new Vector3(0f, 0f, 3f);
            arrived.ReachedWanderTarget = true;

            EnemyDecision start = brain.Tick(arrived, 0f);
            Assert.AreEqual(EnemyState.Wander, start.State);
            Assert.IsFalse(start.WantsNewWanderTarget, "it should stand about for a moment first");
            Assert.IsFalse(start.HasDestination);

            Assert.IsFalse(brain.Tick(arrived, 1f).WantsNewWanderTarget, "still inside the pause");
            Assert.IsTrue(brain.Tick(arrived, 2.5f).WantsNewWanderTarget, "pause is over");
        }
    }
}
