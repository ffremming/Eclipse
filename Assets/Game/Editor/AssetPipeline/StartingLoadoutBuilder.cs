using System.Collections.Generic;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Fills the player's starting hotbar so the weapons are on the number keys.
    /// <para>
    /// Separate from the weapon and torch builders on purpose. Those own what an item IS; this owns
    /// what the player happens to start with, which is a balance decision that changes far more
    /// often and has nothing to do with how a weapon is built. Folding it into the item builders
    /// would mean every retune of the loadout rebuilt four prefabs.
    /// </para>
    /// <para>
    /// The input side already works and needs nothing: <c>PlayerInputManager</c> binds Hotbar1..10
    /// to the number keys and raises <c>OnHotbarPressed</c>, which <c>PlayerInventoryComponent</c>
    /// turns into <c>SelectSlot</c>. The only reason pressing 1 did nothing before was that the
    /// slots were empty.
    /// </para>
    /// </summary>
    public static class StartingLoadoutBuilder
    {
        private const string PlayerPrefabPath =
            "Assets/Game/Prefabs/Characters/Player/PlayerCharacter.prefab";

        private const string ItemFolder = "Assets/Game/Resources/Items/Artifacts";

        /// <summary>
        /// Hotbar order, slot 1 upwards. The four weapons take the keys the player will actually
        /// reach for mid-fight; the torch sits past them because it is switched on once and then
        /// left alone, not swapped to under pressure.
        /// </summary>
        private static readonly string[] Loadout =
        {
            "LightSword",
            "LightKhopesh",
            "LightAxe",
            "LightChain",
            "Torch",
        };

        [MenuItem("Tools/Eclipse/Items/Equip Starting Loadout")]
        private static void Equip()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player == null)
            {
                Debug.LogError($"[Loadout] No player prefab at {PlayerPrefabPath}.");
                return;
            }

            PlayerInventoryComponent inventory = player.GetComponent<PlayerInventoryComponent>();
            if (inventory == null)
            {
                Debug.LogError("[Loadout] The player prefab has no PlayerInventoryComponent.");
                return;
            }

            List<InventoryItem> items = new List<InventoryItem>();
            foreach (string name in Loadout)
            {
                string path = $"{ItemFolder}/{name}.asset";
                InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);

                if (item == null)
                {
                    // Named rather than counted, because the usual cause is that one builder has not
                    // been run yet and the fix is to run that one.
                    Debug.LogError($"[Loadout] Missing {path}. Run its builder, then this again.");
                    continue;
                }

                items.Add(item);
            }

            if (items.Count == 0) return;

            SerializedObject serialized = new SerializedObject(inventory);

            // The hotbar has to be at least as long as the loadout, or the items past the end are
            // dropped on the floor by PlayerInventory with nothing said about it.
            serialized.FindProperty("inventorySize").intValue =
                Mathf.Max(serialized.FindProperty("inventorySize").intValue, items.Count);

            SerializedProperty starting = serialized.FindProperty("startingItems");
            starting.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                starting.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(player);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Loadout] {items.Count} items on the hotbar: {string.Join(", ", Loadout)}");
        }
    }
}
