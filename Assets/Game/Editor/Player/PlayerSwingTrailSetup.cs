// Puts the light trails on the player's swings, on the prefab.
//
// Three trails: the sword arm, which lights for the slashes and the jump attack, and each leg,
// which lights for the kick. The legs need no anchors of their own — the knee and the toe already
// are the two ends of the swing — but the sword does. The goblin's blade is part of its mesh, so
// there is no bone at its tip; the anchors here are children of the wrist, placed from the sword
// object that is disabled on the prefab. That object never renders, but its transform still rides
// the hand and it is the one thing in the rig that knows which way the blade points.
//
// Idempotent: a second run finds the anchors and components in place and re-asserts the wiring,
// so it is also how a tuned value in here reaches the prefab.
//
// Run from: Tools ▸ Eclipse ▸ Player ▸ Setup Swing Trails
using System.Linq;
using UnityEditor;
using UnityEngine;
using SpaceGame.Characters;

namespace SpaceGame.EditorTools
{
    public static class PlayerSwingTrailSetup
    {
        private const string MaterialPath = "Assets/Game/Art/Materials/Light/LightSlash.mat";
        private const string ShaderName = "SpaceGame/Light/LightSlash";

        private const string SwordObject = "Goblins_Sword";
        private const string WristBone = "R_Wrist_Jnt";
        private const string SwordBaseAnchor = "SwordTrailBase";
        private const string SwordTipAnchor = "SwordTrailTip";

        // In the sword mesh's own space, along its long axis: where the hand holds it, and the far
        // end of the blade. The mesh runs y -0.5 to 0.5 with the grip near the low end.
        private static readonly Vector3 SwordGrip = new(0.03f, -0.30f, 0f);
        private static readonly Vector3 SwordTip = new(0.05f, 0.50f, 0f);

        [MenuItem("Tools/Eclipse/Player/Setup Swing Trails")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[PlayerSwingTrailSetup] Exit Play mode first — prefab edits made " +
                               "during play mode are discarded when play mode ends.");
                return;
            }

            Material material = EnsureMaterial();
            if (material == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerThirdPersonSetup.PlayerPrefabPath);
            try
            {
                var swing = root.GetComponent<PlayerMeleeSwing>();
                Transform sword = Find(root, SwordObject);
                Transform wrist = Find(root, WristBone);
                Transform rightKnee = Find(root, "R_Knee_Jnt");
                Transform rightToe = Find(root, "R_Toe_Jnt");
                Transform leftKnee = Find(root, "L_Knee_Jnt");
                Transform leftToe = Find(root, "L_Toe_Jnt");

                if (swing == null || sword == null || wrist == null ||
                    rightKnee == null || rightToe == null || leftKnee == null || leftToe == null)
                {
                    Debug.LogError("[PlayerSwingTrailSetup] The player prefab is missing PlayerMeleeSwing, " +
                                   $"'{SwordObject}' or a knee/toe/wrist bone it needs; nothing was changed.");
                    return;
                }

                Transform swordBase = EnsureAnchor(wrist, SwordBaseAnchor, sword.TransformPoint(SwordGrip));
                Transform swordTip = EnsureAnchor(wrist, SwordTipAnchor, sword.TransformPoint(SwordTip));

                WireTrail(root, swing, material, swordBase, swordTip, SwingKind.Slash | SwingKind.JumpAttack);
                WireTrail(root, swing, material, rightKnee, rightToe, SwingKind.Kick);
                WireTrail(root, swing, material, leftKnee, leftToe, SwingKind.Kick);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerThirdPersonSetup.PlayerPrefabPath);
                Debug.Log("[PlayerSwingTrailSetup] Swing trails wired: sword arm for slashes and the " +
                          "jump attack, both legs for the kick.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material EnsureMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[PlayerSwingTrailSetup] Shader '{ShaderName}' is not compiled yet or " +
                               "has an error; the material was not made.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "LightSlash" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            return material;
        }

        private static Transform Find(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        private static Transform EnsureAnchor(Transform parent, string name, Vector3 worldPosition)
        {
            Transform anchor = parent.Find(name);
            if (anchor == null)
            {
                anchor = new GameObject(name).transform;
                anchor.SetParent(parent, false);
            }

            anchor.position = worldPosition;
            return anchor;
        }

        private static void WireTrail(GameObject root, PlayerMeleeSwing swing, Material material,
            Transform baseAnchor, Transform tipAnchor, SwingKind armedBy)
        {
            // A trail is identified by the anchor it starts from, so a re-run finds the component
            // it made last time instead of stacking a second one on the same limb.
            SwingTrail trail = root.GetComponents<SwingTrail>().FirstOrDefault(t =>
                new SerializedObject(t).FindProperty("baseAnchor").objectReferenceValue == baseAnchor);
            if (trail == null) trail = root.AddComponent<SwingTrail>();

            var serialized = new SerializedObject(trail);
            SerializedFields.Set(serialized, "swing", swing);
            SerializedFields.Set(serialized, "baseAnchor", baseAnchor);
            SerializedFields.Set(serialized, "tipAnchor", tipAnchor);
            SerializedFields.Set(serialized, "bodyFrame", root.transform);
            SerializedFields.Set(serialized, "material", material);
            SerializedFields.SetInt(serialized, "armedBy", (int)armedBy);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
