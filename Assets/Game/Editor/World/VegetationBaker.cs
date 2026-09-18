using System.Collections.Generic;
using SpaceGame.Vegetation;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Puts a <see cref="VegetationField"/>'s plan into the scene, under one holder child the bake
    /// owns and clears.
    /// <para>
    /// Baked rather than spawned at runtime: what ships is ordinary scene geometry, which no save
    /// file has to remember. The holder is emptied first, so baking twice replaces the field
    /// instead of doubling it.
    /// </para>
    /// <para>
    /// Only the holder is registered with <see cref="Undo"/>, not the thousands of plants under it:
    /// undoing its creation takes them with it, and registering each one overflows the undo stack
    /// long before a field this size is finished.
    /// </para>
    /// </summary>
    public static class VegetationBaker
    {
        private const string HolderName = "Vegetation";

        [MenuItem("Tools/Eclipse/World/Bake Vegetation")]
        private static void BakeSelected()
        {
            VegetationField[] fields = Object.FindObjectsByType<VegetationField>(FindObjectsSortMode.None);
            if (fields.Length == 0)
            {
                Debug.LogWarning("[Vegetation] No VegetationField in the open scene.");
                return;
            }

            foreach (VegetationField field in fields) Bake(field);
        }

        /// <summary>Clears the field's holder and plants what it plans. Undoable as one step.</summary>
        public static void Bake(VegetationField field)
        {
            foreach (VegetationLayerAsset layer in field.Layers)
            {
                string problem = layer == null ? field.name + " has an empty layer slot." : layer.Problem();
                if (problem != null) Debug.LogWarning("[Vegetation] Skipped: " + problem, field);
            }

            Transform holder = Holder(field);
            List<VegetationField.PlannedPlant> plan = field.Plan();

            foreach (VegetationField.PlannedPlant plant in plan)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(plant.Prefab, holder);
                instance.transform.SetPositionAndRotation(plant.Position, plant.Rotation);
                instance.transform.localScale = plant.Prefab.transform.localScale * plant.Scale;
                GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic |
                                                                 StaticEditorFlags.OccludeeStatic);
            }

            EditorUtility.SetDirty(field);
            Debug.Log("[Vegetation] " + field.name + ": planted " + plan.Count + " objects.", field);
        }

        /// <summary>Removes everything a previous bake of this field put in the scene.</summary>
        public static void Clear(VegetationField field)
        {
            Transform holder = field.transform.Find(HolderName);
            if (holder != null) Undo.DestroyObjectImmediate(holder.gameObject);
        }

        private static Transform Holder(VegetationField field)
        {
            Clear(field);

            GameObject holder = new GameObject(HolderName);
            Undo.RegisterCreatedObjectUndo(holder, "Bake Vegetation");
            holder.transform.SetParent(field.transform, false);
            return holder.transform;
        }
    }
}
