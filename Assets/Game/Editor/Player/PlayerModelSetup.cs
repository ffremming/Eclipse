// Puts the Nomad body on the player, on the prefab.
//
// The astronaut and the Nomad are both Humanoid rigs with mixamo bone names, and the Nomad
// NPC already runs the player's AnimatorController — so the swap is a body swap, not an
// animation project: remove the astronaut FBX instance, add the Nomad FBX instance at the
// same height with its soles on the same floor, and hand the root Animator the Nomad's
// avatar. Every hand socket, backpack mount, aim IK and ragdoll on the player resolves its
// bones through the Humanoid API at runtime, which is why none of them are touched here.
//
// The body is fitted to whatever was there before rather than to a constant, so the capsule,
// the eye height and every offset tuned against the old body stay true. Measured off mesh
// bounds, not renderer bounds: an unanimated SkinnedMeshRenderer reports whatever the
// importer last cached (see NomadPrefabBuilder.AlignSoleToRoot for the same lesson).
//
// Run from: Tools ▸ SpaceGame ▸ Player ▸ Use Nomad Body
using System.Linq;
using UnityEditor;
using UnityEngine;
using SpaceGame.Characters;

namespace SpaceGame.EditorTools
{
    public static class PlayerModelSetup
    {
        public const string NomadFbxPath = "Assets/Game/Art/Models/Characters/Nomad/nomad.fbx";
        private const string AstronautFbxPath = "Assets/Game/Art/Models/Characters/Astronaut/astronaut.fbx";
        private const string ClothMaterialFolder = "Assets/Game/Art/Materials/Characters";

        private static readonly string[] CapeMeshes = { "Cloth_Cape_01", "Cloth_Cape_02" };

        // Used only when there is no previous body to measure against: NomadPrefabBuilder's
        // stature, standing on the capsule's sole a metre below the prefab pivot.
        private const float FallbackHeight = 3.0f;
        private const float FallbackSoleY = -1f;

        [MenuItem("Tools/Eclipse/Player/Use Nomad Body")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[PlayerModelSetup] Exit Play mode first — prefab edits made during " +
                               "play mode are discarded when play mode ends.");
                return;
            }

            var nomadFbx = AssetDatabase.LoadAssetAtPath<GameObject>(NomadFbxPath);
            if (nomadFbx == null)
            {
                Debug.LogError($"[PlayerModelSetup] No Nomad model at {NomadFbxPath}.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerThirdPersonSetup.PlayerPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[PlayerModelSetup] No player prefab at {PlayerThirdPersonSetup.PlayerPrefabPath}.");
                return;
            }

            try
            {
                float soleY = FallbackSoleY;
                float height = FallbackHeight;

                GameObject previous = FindBody(root, out _);
                if (previous != null && Measure(root.transform, previous, out float low, out float high))
                {
                    soleY = low;
                    height = high - low;
                }

                if (previous != null) Object.DestroyImmediate(previous);

                var body = (GameObject)PrefabUtility.InstantiatePrefab(nomadFbx, root.scene);
                body.transform.SetParent(root.transform, false);
                body.transform.localRotation = Quaternion.identity;

                FitBody(root.transform, body, height, soleY);
                ApplyNomadCapeMaterials(body);
                AssignAvatar(root);
                ClearFirstPersonHidden(root);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerThirdPersonSetup.PlayerPrefabPath);
                Debug.Log($"[PlayerModelSetup] The player wears the Nomad body: {height:0.##} m tall, " +
                          $"soles at y={soleY:0.##}, root Animator on the Nomad avatar.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The player's body model — the direct child instanced from a character FBX.</summary>
        public static GameObject FindBody(GameObject root, out string sourcePath)
        {
            foreach (Transform child in root.transform)
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if (path != AstronautFbxPath && path != NomadFbxPath) continue;

                sourcePath = path;
                return child.gameObject;
            }

            sourcePath = null;
            return null;
        }

        /// <summary>
        /// The cloak needs the wind materials NomadPrefabBuilder creates, because the FBX ships
        /// the cape with an ordinary opaque material that neither moves nor takes the suit colour.
        /// Shared with the menu figure, which wears the same body.
        /// </summary>
        public static void ApplyNomadCapeMaterials(GameObject body)
        {
            foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!CapeMeshes.Contains(renderer.name)) continue;

                string leaf = renderer.name.Substring("Cloth_".Length);
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{ClothMaterialFolder}/NomadCloth_{leaf}.mat");
                if (material == null)
                {
                    Debug.LogError($"[PlayerModelSetup] No cape material for '{renderer.name}' — build the " +
                                   "Nomad NPC first (Tools ▸ SpaceGame ▸ Agents ▸ Build Nomad NPC); it " +
                                   "creates the cloth materials with wind anchors measured off this mesh.");
                    continue;
                }

                renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Lowest and highest point of the model's meshes in <paramref name="root"/> space,
        /// walked corner by corner from each mesh's own bounds.
        /// </summary>
        public static bool Measure(Transform root, GameObject model, out float low, out float high)
        {
            low = float.MaxValue;
            high = float.MinValue;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;

                Bounds bounds = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));

                    float y = root.InverseTransformPoint(renderer.transform.TransformPoint(corner)).y;
                    low = Mathf.Min(low, y);
                    high = Mathf.Max(high, y);
                }
            }

            return low != float.MaxValue;
        }

        private static void FitBody(Transform root, GameObject body, float targetHeight, float soleY)
        {
            if (!Measure(root, body, out float low, out float high))
            {
                Debug.LogWarning("[PlayerModelSetup] Nothing to measure on the Nomad body; it may " +
                                 "stand in the ground or at the wrong size.");
                return;
            }

            float rendered = high - low;
            if (rendered > 1e-3f) body.transform.localScale *= targetHeight / rendered;

            // Re-measure: the scale just moved the soles too.
            if (Measure(root, body, out low, out _))
                body.transform.localPosition += Vector3.up * (soleY - low);
        }

        private static void AssignAvatar(GameObject root)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[PlayerModelSetup] The player root has no Animator — the Nomad body " +
                               "will stand in its bind pose.");
                return;
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(NomadFbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
            {
                Debug.LogError($"[PlayerModelSetup] {NomadFbxPath} has no Avatar — is it imported as Humanoid?");
                return;
            }

            animator.avatar = avatar;
        }

        /// <summary>
        /// The head-hiding list pointed at astronaut renderers that no longer exist. Cleared here
        /// as well as by the third-person setup, so the tools are correct in either order.
        /// </summary>
        private static void ClearFirstPersonHidden(GameObject root)
        {
            var look = root.GetComponent<PlayerLook>();
            if (look == null) return;

            var serialized = new SerializedObject(look);
            serialized.FindProperty("firstPersonHidden").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
