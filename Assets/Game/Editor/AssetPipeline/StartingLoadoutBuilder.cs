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
        /// Hotbar order, slot 1 upwards. Three weapons, one per way of reaching a thing: the chain
        /// at range and around cover, the boomerang thrown and come back, the torch in the fist.
        /// <para>
        /// The blades — sword, khopesh and axe — are off the bar rather than deleted. Three weapons
        /// that differ only in reach and damage are one weapon with three models, and a player who
        /// has to choose between three number keys is choosing nothing. Their assets and builders
        /// are left where they are, so putting one back is a line here.
        /// </para>
        /// <para>
        /// The lantern is off it for a different reason: <c>EyeLights</c> makes the light the
        /// player's own, carried in front of the eyes whatever is in the hand, so a hand-held lamp
        /// with a switch has nothing left to do that the eyes do not do better.
        /// </para>
        /// </summary>
        private static readonly string[] Loadout =
        {
            "LightChain",
            "LightBoomerang",
            "Torch",
        };

        /// <summary>
        /// Slots left free for what the player finds in the world. Two castle keys need two; the
        /// third is headroom for the next thing that is picked up rather than started with.
        /// </summary>
        private const int SpareSlots = 3;

        /// <summary>
        /// How long the hotbar can usefully get: <c>PlayerInputManager</c> binds Hotbar1..10, and a
        /// slot past the last bound key is one the player has no way to select.
        /// </summary>
        private const int BoundHotbarKeys = 10;

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
            // dropped on the floor by PlayerInventory with nothing said about it — and longer than
            // it by enough to hold what the player picks up.
            //
            // Sized exactly to the loadout, the hotbar starts full, and a full hotbar makes
            // TryAddItem return false for everything: the castle keys could not be picked up at
            // all, so the doors they open could never be opened. Nothing reports that. The pickup
            // simply does not respond, which in a game with no HUD is indistinguishable from a key
            // that is not interactive.
            serialized.FindProperty("inventorySize").intValue = Mathf.Clamp(
                items.Count + SpareSlots,
                serialized.FindProperty("inventorySize").intValue,
                BoundHotbarKeys);

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
