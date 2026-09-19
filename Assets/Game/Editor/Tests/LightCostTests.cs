// What a blow costs in light, and that the player's health really is counted in it.
//
// The tiers are pinned against the damage numbers the game actually ships — a crumpy's 9, the
// weapons' 19 to 38 — because the design is "a light hit is one orb, a heavy one three", and a
// threshold that drifts by a point would quietly turn the sword into a one-orb weapon.
//
// In Editor/ rather than beside the other EditMode tests for the same reason WeaponWheelTests is:
// LightCost and HealthComponent live in Assembly-CSharp, which SpaceGame.Tests.EditMode does not
// reference.
using NUnit.Framework;
using SpaceGame.Gameplay;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class LightCostTests
    {
        private const int LanternOrbs = 30;

        private GameObject body;
        private LightCost cost;

        [SetUp]
        public void SetUp()
        {
            cost = ScriptableObject.CreateInstance<LightCost>();
            body = new GameObject("Player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(cost);
        }

        [Test]
        public void ABlowIsWorthOneTwoOrThreeOrbsByHowHardItHits()
        {
            Assert.AreEqual(1, cost.OrbsFor(12), "a light enemy's swing");
            Assert.AreEqual(1, cost.OrbsFor(19), "the lightest weapon, just under the two-orb line");
            Assert.AreEqual(2, cost.OrbsFor(20), "on the two-orb line");
            Assert.AreEqual(2, cost.OrbsFor(22), "the sword");
            Assert.AreEqual(2, cost.OrbsFor(29), "just under the three-orb line");
            Assert.AreEqual(3, cost.OrbsFor(30), "on the three-orb line");
            Assert.AreEqual(3, cost.OrbsFor(38), "the axe");
            Assert.AreEqual(0, cost.OrbsFor(0), "a blow that does nothing takes nothing");
        }

        [Test]
        public void ThePlayersLightLosesTheOrbsABlowIsWorthWhileEventsStillReportTheBlow()
        {
            HealthComponent light = NewLight(withCost: true);
            int reported = 0;
            light.OnDamage += amount => reported = amount;

            light.Damage(12);
            Assert.AreEqual(LanternOrbs - 1, light.GetHealth);

            light.Damage(38);
            Assert.AreEqual(LanternOrbs - 1 - 3, light.GetHealth);
            Assert.AreEqual(38, reported, "listeners size their reaction to the blow, not to the orbs");
        }

        [Test]
        public void HealthWithoutALightCostStillLosesWhatTheBlowDoes()
        {
            HealthComponent enemy = NewLight(withCost: false);
            enemy.Damage(38);

            Assert.AreEqual(LanternOrbs - 38, enemy.GetHealth);
        }

        private HealthComponent NewLight(bool withCost)
        {
            var health = body.AddComponent<HealthComponent>();

            var serialized = new SerializedObject(health);
            serialized.FindProperty("maxHealth").intValue = LanternOrbs;
            serialized.FindProperty("currentHealth").intValue = LanternOrbs;
            serialized.FindProperty("lightCost").objectReferenceValue = withCost ? cost : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return health;
        }
    }
}
