// What PlayerCharacter.prefab must hold once the third-person tool has run.
//
// It is a prefab surgery with references spread over half a dozen components, and the failure
// mode of a missed one is quiet: a stance that still crouches the camera instead of the eye, an
// interaction ray cast from behind the shoulder. These pin the wiring so a later prefab edit
// cannot undo it unnoticed.
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
                            "The eye moved: the stance and the interaction ray assume 1.45 m.");
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

        // Where the body meets the ground.
        //
        // Everything that puts a player down puts the ROOT on a ground point: SpawnPoint resolves
        // one by raycast, a rebuilt world drops the prefab on the terrain, a designer drags it into
        // the scene view. So the root has to BE the contact point — the underside of the capsule
        // and the soles of the feet, both at y = 0 in the body's own space.
        //
        // Neither was. The root sat 0.80 m up the character's chest and the capsule hung a further
        // 0.20 m below the feet, so setting the body down on the terrain buried the capsule a metre
        // deep and left the body floating a fifth of one. That is the "I fall through the floor the
        // moment I press play" this prefab shipped with, and SpawnPoint.groundClearance — a 1.2 m
        // lift applied to every spawn everywhere — was the plaster over it.
        [Test]
        public void TheBodyStandsOnItsOwnPivot()
        {
            var capsule = root.GetComponentInChildren<CapsuleCollider>(true);
            Assert.IsNotNull(capsule, "The player has no capsule to stand on.");
            Assert.AreEqual(0f, UndersideOf(capsule), 0.02f,
                            "The bottom of the capsule must rest on the body's own pivot. Every "
                            + "position the game measures is a point on the ground, and whatever "
                            + "the capsule hangs below the pivot is how deep it is buried.");

            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(skin, "The player has no body.");
            Assert.IsNotNull(skin.rootBone, "The body has no root bone to stand on.");
            Assert.AreEqual(0f, root.transform.InverseTransformPoint(skin.rootBone.position).y, 0.02f,
                            "The feet must meet the same pivot the capsule does, or the body "
                            + "hovers over the ground it is standing on.");
        }

        // The lowest point of the capsule in the body's own space.
        //
        // Measured rather than read off center, because the capsule hangs on a child carrying its
        // own scale: Unity sizes a capsule by that scale — height along Y, radius by the wider of
        // X and Z — and then refuses to make it shorter than its own diameter.
        private float UndersideOf(CapsuleCollider capsule)
        {
            Transform on = capsule.transform;
            float alongY = Mathf.Abs(on.lossyScale.y);
            float across = Mathf.Max(Mathf.Abs(on.lossyScale.x), Mathf.Abs(on.lossyScale.z));
            float height = Mathf.Max(capsule.height * alongY, capsule.radius * across * 2f);
            Vector3 centre = root.transform.InverseTransformPoint(on.TransformPoint(capsule.center));
            return centre.y - height * 0.5f;
        }

        private static Object Reference(Component component, string property)
        {
            Assert.IsNotNull(component, $"Missing component for '{property}'.");
            return new SerializedObject(component).FindProperty(property).objectReferenceValue;
        }
    }
}
