// Turns the player's rigid first-person eye into a third-person rig, on the prefab.
//
// The player used to be first-person by construction: the camera was a rigid child of the
// body at eye height, PlayerLook pitched it directly, and Interactor, PlayerStance and the
// remote aim pose all treated "the camera" and "the eye" as the same object. This keeps that
// object — renamed to what it always was, the eye pivot — and moves only the rendering camera
// onto a ThirdPersonCameraBoom behind it. Everything that looked from the eye still does;
// only the picture is taken from further back.
//
// A prefab-editing tool rather than a hand edit of the YAML, because the camera is a nested
// prefab instance and the references that must change live on five different components.
// Idempotent: a second run finds the pivot in place and re-asserts the wiring.
//
// Run from: Tools ▸ SpaceGame ▸ Player ▸ Setup Third Person View
using UnityEditor;
using UnityEngine;
using SpaceGame.Characters;
using SpaceGame.Gameplay;

namespace SpaceGame.EditorTools
{
    public static class PlayerThirdPersonSetup
    {
        public const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/Player/PlayerCharacter.prefab";
        public const string PivotName = "CameraPivot";

        // Over the right shoulder, a touch above the eye, a few metres back. Far enough to see
        // the whole body and the wings; close enough that a doorway-sized gap still reads.
        public static readonly Vector3 RestOffset = new(0.45f, 0.35f, -3.6f);

        // Where the first-person eye was authored, for a prefab whose camera has already been
        // moved off the root by something else.
        private static readonly Vector3 AuthoredEye = new(0f, 1.45f, 0.16f);

        [MenuItem("Tools/Eclipse/Player/Setup Third Person View")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[PlayerThirdPersonSetup] Exit Play mode first — prefab edits made " +
                               "during play mode are discarded when play mode ends.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[PlayerThirdPersonSetup] No player prefab at {PlayerPrefabPath}.");
                return;
            }

            try
            {
                var camera = root.GetComponentInChildren<Camera>(true);
                if (camera == null)
                {
                    Debug.LogError("[PlayerThirdPersonSetup] The player prefab has no Camera to put on a boom.");
                    return;
                }

                Transform cameraTransform = camera.transform;
                if (PrefabUtility.IsPartOfPrefabInstance(camera.gameObject) &&
                    !PrefabUtility.IsOutermostPrefabInstanceRoot(camera.gameObject))
                {
                    Debug.LogError("[PlayerThirdPersonSetup] The Camera is inside a nested prefab " +
                                   "instance and cannot be reparented without unpacking it.");
                    return;
                }

                Transform pivot = EnsurePivot(root.transform, cameraTransform);

                if (cameraTransform.parent != pivot) cameraTransform.SetParent(pivot, false);
                cameraTransform.localPosition = RestOffset;
                cameraTransform.localRotation = Quaternion.identity;

                // The camera used to be authored off, and PlayerController.EnablePlayer switched
                // it on for the owner. That switch now flips the pivot — it is what the
                // controller's playerCamera points at — so the pivot takes over the "off until
                // owned" role and the camera beneath it must be live, or nothing ever renders
                // and the loading screen waits for a camera that never comes.
                pivot.gameObject.SetActive(false);
                camera.gameObject.SetActive(true);

                WireBoom(pivot, cameraTransform);
                WireEye(root, pivot);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"[PlayerThirdPersonSetup] '{camera.name}' now rides a boom at {RestOffset} " +
                          $"behind '{PivotName}' (local {pivot.localPosition}); look, stance, " +
                          "interaction and the remote aim pose all read from the pivot.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform EnsurePivot(Transform root, Transform cameraTransform)
        {
            Transform pivot = root.Find(PivotName);
            if (pivot != null) return pivot;

            pivot = new GameObject(PivotName).transform;
            pivot.SetParent(root, false);

            // The eye stays exactly where the first-person camera was, so every system that
            // treated the camera as the eye keeps the same numbers.
            pivot.localPosition = cameraTransform.parent == root ? cameraTransform.localPosition : AuthoredEye;
            pivot.localRotation = Quaternion.identity;
            return pivot;
        }

        private static void WireBoom(Transform pivot, Transform cameraTransform)
        {
            var boom = pivot.GetComponent<ThirdPersonCameraBoom>();
            if (boom == null) boom = pivot.gameObject.AddComponent<ThirdPersonCameraBoom>();

            int playerLayer = LayerMask.NameToLayer("Player");

            var serialized = new SerializedObject(boom);
            serialized.FindProperty("boomCamera").objectReferenceValue = cameraTransform;
            serialized.FindProperty("restOffset").vector3Value = RestOffset;
            serialized.FindProperty("collisionMask").intValue = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireEye(GameObject root, Transform pivot)
        {
            var look = root.GetComponent<PlayerLook>();
            if (SetReference(look, "playerCamera", pivot.gameObject))
            {
                // Head-hiding was a first-person courtesy: from behind, the head is the point.
                var serialized = new SerializedObject(look);
                serialized.FindProperty("firstPersonHidden").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            SetReference(root.GetComponent<PlayerController>(), "playerCamera", pivot.gameObject);
            SetReference(root.GetComponentInChildren<Interactor>(true), "lookTransform", pivot);
            SetReference(root.GetComponentInChildren<PlayerStance>(true), "eye", pivot);
        }

        private static bool SetReference(Component component, string property, Object value)
        {
            if (component == null)
            {
                Debug.LogError($"[PlayerThirdPersonSetup] No component found for '{property}' — " +
                               "that system still looks from wherever it did before.");
                return false;
            }

            var serialized = new SerializedObject(component);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
