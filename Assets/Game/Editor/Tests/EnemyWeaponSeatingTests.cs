// That the alien and the crumpy actually end up holding their blades.
//
// This is the one thing about these two creatures that cannot be seen anywhere but in play: the
// weapon does not exist until EnemyGear spawns it and seats it at Awake. Every way it can go wrong
// is silent — a rig that imported as Generic has no hand bone to find, a weapon prefab with no
// ItemGrip is never asked for, a renamed serialized field quietly wires nothing, and a weapon that
// is part of a prefab instance cannot be reparented onto a bone at all — and all of them look the
// same from the outside: a creature fighting bare-handed.
//
// EnemyGear.SeatProps is public for exactly this reason; Awake does not run here.
//
// In Editor/ rather than beside the other EditMode tests: these load prefabs and touch
// Assembly-CSharp types, which SpaceGame.Tests.EditMode cannot reference.
using NUnit.Framework;
using SpaceGame.Enemies;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class EnemyWeaponSeatingTests
    {
        private static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Game/Prefabs/Enemies/{name}.prefab");
            Assert.IsNotNull(prefab, $"{name} is missing — run Tools/Eclipse/Enemies/Build Sculpt Enemies");
            return prefab;
        }

        [TestCase("AlienEnemy", "DarkAxe")]
        [TestCase("CrumpyEnemy", "DarkKhopesh")]
        public void TheCreatureEndsUpHoldingItsBladeInItsHand(string creature, string weapon)
        {
            var body = Object.Instantiate(Load(creature));
            try
            {
                var gear = body.GetComponent<EnemyGear>();
                Assert.IsNotNull(gear, $"{creature} has no EnemyGear, so it carries nothing");
                gear.SeatProps();

                Animator animator = body.GetComponentInChildren<Animator>();
                Assert.IsTrue(animator.isHuman,
                    $"{creature}'s rig did not import as Humanoid, so there is no hand to seat into");

                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform held = FindGrip(body, weapon);

                Assert.IsTrue(held.IsChildOf(hand),
                    $"{creature}'s {weapon} is parented to {held.parent.name}, not to its hand");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        /// <summary>
        /// The spawned weapon, which Unity names "&lt;prefab&gt;(Clone)" — matched by prefix rather
        /// than exactly, so the test pins WHICH blade was spawned without pinning Unity's suffix.
        /// </summary>
        private static Transform FindGrip(GameObject body, string weapon)
        {
            foreach (ItemGrip grip in body.GetComponentsInChildren<ItemGrip>(true))
                if (grip.name.StartsWith(weapon)) return grip.transform;

            Assert.Fail($"no {weapon} with an ItemGrip under {body.name} — nothing was spawned");
            return null;
        }
    }
}
