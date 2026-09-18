// What PlayerCharacter.prefab must hold once the third-person and Nomad-body tools have run.
//
// Both are prefab surgeries with references spread over half a dozen components, and the
// failure mode of a missed one is quiet: a stance that still crouches the camera instead of
// the eye, an interaction ray cast from behind the shoulder, a body whose soles float a hand's
// breadth above the ground. These pin the wiring so a later prefab edit cannot undo it
// unnoticed.
//
// In Editor/ rather than beside the other EditMode tests because these touch the editor
// tools' constants and Assembly-CSharp types.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SpaceGame.Characters;
using SpaceGame.Gameplay;

namespace SpaceGame.EditorTools
{
    public class PlayerBodyAndViewTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = PrefabUtility.LoadPrefabContents(PlayerThirdPersonSetup.PlayerPrefabPath);
            Assert.IsNotNull(root, $"No player prefab at {PlayerThirdPersonSetup.PlayerPrefabPath}.");
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }

        [Test]
        public void TheEyeIsAPivotAndTheCameraRidesABoomBehindIt()
        {
            var camera = root.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(camera, "The player has no camera at all.");

            Transform pivot = camera.transform.parent;
            Assert.AreEqual(PlayerThirdPersonSetup.PivotName, pivot.name);
            Assert.AreSame(root.transform, pivot.parent, "The pivot must hang directly off the body.");
            Assert.AreEqual(1.45f, pivot.localPosition.y, 0.05f,
                            "The eye moved: AimPose.Eye, the stance and the remote aim pose assume 1.45 m.");
            Assert.Less(camera.transform.localPosition.z, -1f, "The camera is not behind the pivot.");

            var boom = pivot.GetComponent<ThirdPersonCameraBoom>();
            Assert.IsNotNull(boom, "No boom on the pivot — the camera would clip into every cliff.");
            Assert.AreSame(camera.transform, Reference(boom, "boomCamera"));

            var look = root.GetComponent<PlayerLook>();
            Assert.AreSame(pivot.gameObject, look.playerCamera, "PlayerLook must pitch the pivot, not the camera.");
            Assert.AreEqual(0, new SerializedObject(look).FindProperty("firstPersonHidden").arraySize,
                            "Nothing should be hidden from a third-person view.");

            Assert.AreSame(pivot.gameObject, Reference(root.GetComponent<PlayerController>(), "playerCamera"));
            Assert.AreSame(pivot, Reference(root.GetComponentInChildren<Interactor>(true), "lookTransform"),
                           "Interaction must reach from the eye, not from behind the shoulder.");
            Assert.AreSame(pivot, Reference(root.GetComponentInChildren<PlayerStance>(true), "eye"),
                           "Crouching must lower the eye, which carries the camera with it.");
        }

        // PlayerController.EnablePlayer activates playerCamera for the owner — the pivot, now.
        // A camera authored off beneath an active pivot never wakes: nothing renders and the
        // loading screen holds forever waiting for it. That is exactly how the rig first shipped.
        [Test]
        public void WakingThePivotWakesTheCamera()
        {
            var camera = root.GetComponentInChildren<Camera>(true);
            Transform pivot = camera.transform.parent;

            Assert.IsFalse(pivot.gameObject.activeSelf, "The pivot must be off until the owner enables it.");
            Assert.IsTrue(camera.gameObject.activeSelf, "The camera must be live beneath the pivot.");
            Assert.IsTrue(camera.enabled);

            pivot.gameObject.SetActive(true);
            Assert.IsNotNull(root.GetComponentInChildren<Camera>(false),
                             "Enabling the pivot must yield an active camera.");
        }

        [Test]
        public void ThePlayerWearsTheNomadWhereTheAstronautStood()
        {
            GameObject body = PlayerModelSetup.FindBody(root, out string source);
            Assert.IsNotNull(body, "No character FBX instance under the player.");
            Assert.AreEqual(PlayerModelSetup.NomadFbxPath, source);

            Assert.IsTrue(PlayerModelSetup.Measure(root.transform, body, out float low, out float high));
            Assert.AreEqual(-1f, low, 0.1f, "The soles must meet the capsule's sole a metre under the pivot.");
            Assert.That(high - low, Is.InRange(2.5f, 3.5f), "The Nomad is not the height the player was.");

            var animator = root.GetComponent<Animator>();
            Assert.IsNotNull(animator.avatar);
            Assert.IsTrue(animator.avatar.isHuman, "The root Animator needs a Humanoid avatar for the hand sockets.");
            StringAssert.Contains("nomad", animator.avatar.name.ToLowerInvariant());

            int capes = 0;
            foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.name.StartsWith("Cloth_Cape_")) continue;
                capes++;
                StringAssert.StartsWith("NomadCloth_", renderer.sharedMaterial.name,
                                        $"'{renderer.name}' wears the FBX's dead material — no wind, no suit colour.");
            }

            Assert.AreEqual(2, capes, "The cloak and its shoulder flap should both be present.");
        }

        private static Object Reference(Component component, string property)
        {
            Assert.IsNotNull(component, $"Missing component for '{property}'.");
            return new SerializedObject(component).FindProperty(property).objectReferenceValue;
        }
    }
}
