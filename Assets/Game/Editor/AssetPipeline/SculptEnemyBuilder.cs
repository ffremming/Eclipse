using System.IO;
using SpaceGame.Characters;
using SpaceGame.Enemies;
using SpaceGame.Gameplay;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the alien and the crumpy into enemies: imports their rigs as Humanoid, gives them
    /// skin, and stands them up on the shared enemy chassis.
    /// <para>
    /// Built FROM <c>EnemyChassis.prefab</c> rather than from an empty object, and that is the
    /// load-bearing decision here. "Every enemy fights the same way" is not a list of components to
    /// keep in step by hand — it is one prefab, unpacked, with a model dropped into it and its
    /// numbers retuned. The chassis is what the goblin left behind when it was cut: its body,
    /// without its species. Anything added to the chassis is one re-run away from being on every
    /// creature, and nothing can quietly go missing from one of them.
    /// </para>
    /// <para>
    /// Safe to re-run: it replaces the prefabs it made, and it leaves the materials alone once they
    /// exist so skin tuned in the Inspector survives.
    /// </para>
    /// </summary>
    public static class SculptEnemyBuilder
    {
        private const string ChassisPath = "Assets/Game/Prefabs/Enemies/EnemyChassis.prefab";
        private const string ControllerPath = "Assets/Game/Art/Animations/Creature/Creature.controller";
        private const string WeaponFolder = "Assets/Game/Prefabs/Enemies/Weapons";
        private const string MaterialFolder = "Assets/Game/Art/Materials/Characters";
        private const string LitShader = "Universal Render Pipeline/Lit";

        /// <summary>Where the collider sits relative to the body's height — a capsule stands on the feet.</summary>
        private const float ColliderHeightFraction = 0.8f;

        [MenuItem("Tools/Eclipse/Enemies/Build Sculpt Enemies")]
        private static void BuildAll()
        {
            GameObject chassis = AssetDatabase.LoadAssetAtPath<GameObject>(ChassisPath);
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            if (chassis == null || controller == null)
            {
                Debug.LogError($"[SculptEnemies] Need both {ChassisPath} and {ControllerPath}.");
                return;
            }

            foreach (SculptEnemy species in SculptEnemies.All)
                Build(species, chassis, controller);

            AssetDatabase.SaveAssets();
        }

        private static void Build(SculptEnemy species, GameObject chassis,
                                  RuntimeAnimatorController controller)
        {
            if (!EnsureHumanoid(species)) return;

            GameObject weapon = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{WeaponFolder}/{species.WeaponName}.prefab");
            if (weapon == null)
            {
                Debug.LogError($"[SculptEnemies] No {species.WeaponName} at {WeaponFolder}. " +
                               "Run Tools/Eclipse/Items/Build Dark Blades first.");
                return;
            }

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(chassis);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                                               InteractionMode.AutomatedAction);
            root.name = $"{species.Name}Enemy";

            Animator animator = SwapModel(root, species, controller);
            ShapeBody(root, species);
            TuneFight(root, species);
            Arm(root, weapon, animator);

            Directory.CreateDirectory(EnemyPrefabs.Folder);
            PrefabUtility.SaveAsPrefabAsset(root, $"{EnemyPrefabs.Folder}/{root.name}.prefab");
            Object.DestroyImmediate(root);

            Debug.Log($"[SculptEnemies] Built {species.Name}Enemy: {species.Health} health, " +
                      $"{species.Damage} a swing, carrying the {species.WeaponName}.");
        }

        /// <summary>
        /// Makes sure the model imports as a Humanoid, which is the whole reason these two can wear
        /// the shared clips: they are retargeted through the avatar, and a Generic rig
        /// has no avatar to retarget through — it imports without complaint and stands in T-pose
        /// forever.
        /// </summary>
        private static bool EnsureHumanoid(SculptEnemy species)
        {
            var importer = AssetImporter.GetAtPath(species.ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SculptEnemies] No model at {species.ModelPath}. Export it from " +
                               "Blender first — see sculpt_character_export.py.");
                return false;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

                // The FBX's own materials are Blender's Principled BSDF, which arrives as something
                // that is not the project's Lit shader. The skin is built here instead.
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(species.ModelPath);
            Avatar avatar = model != null ? model.GetComponent<Animator>()?.avatar : null;

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"[SculptEnemies] {species.Name}'s avatar did not come out humanoid. " +
                               "Unity maps it from the bone names, so check the rig still names " +
                               "them after HumanBodyBones.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Puts this creature's body on the chassis, driven by the shared clip library.
        /// Returns the animator, which is what everything else on the prefab drives.
        /// </summary>
        private static Animator SwapModel(GameObject root, SculptEnemy species,
                                          RuntimeAnimatorController controller)
        {
            Transform old = root.transform.Find("Model");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(species.ModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            Animator animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // The clips are authored in place and the agent does the walking. Root motion on top of
            // that is two things moving one creature, and they disagree.
            animator.applyRootMotion = false;

            Skin(model, species);

            // The masked upper-body layer the swings play on. It lives with the model, because the
            // model is what carries the animator.
            var layer = model.AddComponent<AttackLayerWeight>();
            ItemBuilderKit.Wire(layer, "animator", animator);
            return animator;
        }

        /// <summary>
        /// The skin and the eyes. The body is the one skinned mesh and the eyes are the two that
        /// are not — a rule about what the parts ARE, rather than about what Blender called them,
        /// so a re-export that renames an object does not silently paint the eyes as skin.
        /// </summary>
        private static void Skin(GameObject model, SculptEnemy species)
        {
            Material body = ItemBuilderKit.EnsureMaterial(
                $"{MaterialFolder}/{species.Name}_Body.mat", LitShader, material =>
                {
                    material.SetTexture("_BaseMap", BaseColour(species));
                    material.SetColor("_BaseColor", species.SkinTint);
                    material.SetFloat("_Smoothness", species.SkinSmoothness);
                });

            Material eyes = ItemBuilderKit.EnsureLitMaterial(
                $"{MaterialFolder}/{species.Name}_Eyes.mat", Color.black, species.EyeGlow);

            if (body == null || eyes == null) return;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = renderer is SkinnedMeshRenderer ? body : eyes;
        }

        private static Texture BaseColour(SculptEnemy species)
        {
            string folder = Path.GetDirectoryName(species.ModelPath);
            return AssetDatabase.LoadAssetAtPath<Texture>(
                $"{folder}/Textures/{species.Name}_BaseColor.png");
        }

        /// <summary>
        /// How much room the creature takes up. The chassis carries a placeholder capsule, and left
        /// as it is these two would be swung at around the knees and would walk through doorways
        /// their heads do not fit through.
        /// </summary>
        private static void ShapeBody(GameObject root, SculptEnemy species)
        {
            float colliderHeight = species.BodyHeight * ColliderHeightFraction;

            var capsule = root.GetComponent<CapsuleCollider>();
            capsule.radius = species.BodyRadius;
            capsule.height = colliderHeight;
            capsule.center = new Vector3(0f, colliderHeight * 0.5f, 0f);

            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = species.BodyRadius;
            agent.height = species.BodyHeight;

            // Its walking speed is driven per-tick by EnemyAgent's decision, so what is set here is
            // only what it would stand at with nothing driving it.
            agent.speed = species.WalkSpeed;
        }

        private static void TuneFight(GameObject root, SculptEnemy species)
        {
            var health = root.GetComponent<HealthComponent>();
            ItemBuilderKit.WireInt(health, "maxHealth", species.Health);
            ItemBuilderKit.WireInt(health, "currentHealth", species.Health);

            var strike = root.GetComponent<MeleeStrike>();
            ItemBuilderKit.WireInt(strike, "damage", species.Damage);
            ItemBuilderKit.WireFloat(strike, "range", species.Reach);
            ItemBuilderKit.WireFloat(strike, "windup", species.Windup);
            ItemBuilderKit.WireFloat(strike, "hitWindow", species.HitWindow);
            ItemBuilderKit.WireFloat(strike, "originHeight", species.ChestHeight);

            var agent = root.GetComponent<EnemyAgent>();
            ItemBuilderKit.WireFloat(agent, "sightRange", species.SightRange);
            ItemBuilderKit.WireFloat(agent, "eyeHeight", species.EyeHeight);
            ItemBuilderKit.WireFloat(agent, "walkSpeed", species.WalkSpeed);
            ItemBuilderKit.WireFloat(agent, "chaseSpeed", species.ChaseSpeed);
            ItemBuilderKit.WireFloat(agent, "attackRange", species.AttackRange);
            ItemBuilderKit.WireFloat(agent, "attackCooldown", species.AttackCooldown);
        }

        /// <summary>
        /// Gives it the blade to carry — the prefab itself, not a copy parked inside this one.
        /// <para>
        /// EnemyGear spawns it at Awake and seats it, which is the only way the seating can work:
        /// a weapon living inside the creature's prefab is part of a prefab instance once placed,
        /// and Unity refuses to reparent one of those onto a hand bone. Referencing the prefab also
        /// means retuning the dark blades reaches every creature carrying one without rebuilding
        /// the creatures.
        /// </para>
        /// </summary>
        private static void Arm(GameObject root, GameObject weapon, Animator animator)
        {
            var gear = root.AddComponent<EnemyGear>();
            ItemBuilderKit.Wire(gear, "animator", animator);
            ItemBuilderKit.WireArray(gear, "props", weapon.GetComponent<ItemGrip>());
        }
    }
}
