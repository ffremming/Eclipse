using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the boomerang prefab and its inventory asset from the imported boomerang model.
    /// <para>
    /// Re-runnable, and it replaces the prefab wholesale — so every number that matters is a
    /// constant here rather than something tuned in the Inspector and lost on the next run.
    /// </para>
    /// <para>
    /// The prefab is a root, a <c>Flying</c> transform that is the boomerang, and the model under
    /// that. <c>Flying</c> is what leaves the hand: the light and the trail ride on it, and the
    /// model beneath is turned so the blade's face lies along <c>Flying</c>'s +Z, which is the
    /// contract <see cref="LightBoomerang"/> lays the throw out by.
    /// </para>
    /// </summary>
    public static class BoomerangBuilder
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Items/Artifacts/Weapons";
        private const string ItemName = "LightBoomerang";

        private const string ModelPath = "Assets/Game/Art/Models/Weapons/Boomerang/boomerang.fbx";
        private const string GlowMaterialPath = "Assets/Game/Art/Materials/Items/BoomerangGlow.mat";
        private const string ArcMaterialPath = "Assets/Game/Art/Materials/Light/LightArc.mat";

        /// <summary>Tip to tip, in metres. The source is three metres across; this is readable, not realistic.</summary>
        private const float Span = 0.55f;

        // From boomerang.blend: a red body with an orange glow. Blender's emission strength for it
        // (16.4) is a Blender number, not a Unity one, so the strength here is a starting point to
        // be judged by eye against the other glowing weapons.
        private static readonly Color BodyColor = new Color(0.8f, 0.13f, 0.11f);
        private static readonly Color GlowColor = new Color(1f, 0.41f, 0.01f);
        private const float GlowStrength = 1.2f;

        private static readonly Color LightColor = new Color(1f, 0.55f, 0.2f);

        // Below the blades while carried — it is not the one in the hand that does the work — and
        // brighter than them at the peak, because the peak is when it is away from you and the
        // light is what lets you see where it is.
        private const float IdleIntensity = 4f;
        private const float SwingIntensity = 30f;
        private const float IdleRange = 5f;
        private const float SwingRange = 15f;

        // The trade. Long reach and two chances at everything, paid for with a slow flight during
        // which the hand is empty; hits lighter than the sword's because each target can take two.
        private const int Damage = 20;
        private const float FlightSeconds = 1.5f;
        private const float Range = 12f;
        private const float Bow = 3f;
        private const float SpinDegreesPerSecond = 1080f;
        private const float StrikeRadius = 0.6f;

        private const float TrailWidth = 0.3f;
        private const float TrailSeconds = 0.3f;

        [MenuItem("Tools/Eclipse/Items/Build Boomerang")]
        private static void Build()
        {
            Material arc = AssetDatabase.LoadAssetAtPath<Material>(ArcMaterialPath);
            Material glow = ItemBuilderKit.EnsureLitMaterial(GlowMaterialPath, BodyColor, GlowColor * GlowStrength);
            if (arc == null || glow == null)
            {
                Debug.LogError($"[Boomerang] Missing {ArcMaterialPath} or the glow material.");
                return;
            }

            GameObject root = new GameObject(ItemName);

            Transform flying = new GameObject("Flying").transform;
            flying.SetParent(root.transform, false);

            // The model lies flat with its thin axis up, which is its Y once imported, and its open
            // side toward -Z. A quarter turn about X puts that thin axis on +Z, the face, and turns
            // the open side up — so it is held like everything else in the project: pointing up
            // from the hand.
            Transform model = ModelMount.MountAt(flying, ModelPath, Span, ModelMount.Anchor.Centre);
            model.localRotation = Quaternion.Euler(90f, 0f, 0f);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = glow;
            }

            ItemBuilderKit.StopCastingShadows(root);

            // Held by the outside of the bend, with the arms reaching up from the hand. The bounds
            // are measured rather than assumed so the hand lands on the bend whatever the model's
            // proportions are after a re-export.
            float halfLength = model.GetComponentInChildren<Renderer>().bounds.extents.y;
            flying.localPosition = new Vector3(0f, halfLength, 0f);

            Light tipLight = ItemBuilderKit.AddPointLight(flying, "TipLight", LightColor, IdleRange,
                                                         IdleIntensity, LightShadows.None);
            TrailRenderer trail = ItemBuilderKit.AddTrail(flying, arc, TrailWidth, TrailSeconds);

            LightBoomerang boomerang = root.AddComponent<LightBoomerang>();
            ItemBuilderKit.Wire(boomerang, "flying", flying);
            ItemBuilderKit.Wire(boomerang, "tipLight", tipLight);
            ItemBuilderKit.Wire(boomerang, "trail", trail);
            ItemBuilderKit.WireFloat(boomerang, "idleIntensity", IdleIntensity);
            ItemBuilderKit.WireFloat(boomerang, "swingIntensity", SwingIntensity);
            ItemBuilderKit.WireFloat(boomerang, "idleRange", IdleRange);
            ItemBuilderKit.WireFloat(boomerang, "swingRange", SwingRange);
            ItemBuilderKit.WireFloat(boomerang, "swingDuration", FlightSeconds);
            ItemBuilderKit.WireBool(boomerang, "leavesSwingLight", false);
            ItemBuilderKit.WireInt(boomerang, "damage", Damage);
            ItemBuilderKit.WireFloat(boomerang, "range", Range);
            ItemBuilderKit.WireFloat(boomerang, "bow", Bow);
            ItemBuilderKit.WireFloat(boomerang, "spinSpeed", SpinDegreesPerSecond);
            ItemBuilderKit.WireFloat(boomerang, "strikeRadius", StrikeRadius);
            ItemBuilderKit.WireInt(boomerang, "groundMask", ItemBuilderKit.GroundLayerMask);

            ItemBuilderKit.SavePickupable(root, PrefabFolder, ItemName, Span, flying.localPosition);
            Debug.Log($"[Boomerang] Built {PrefabFolder}/{ItemName}.prefab.");
        }
    }
}
