// Renders an inventory icon for every item that has none.
//
// The artifact pipeline used to end with "run Generate All Item Icons"; that tool did not
// survive the carve-out, and the two items that came across kept their sprites. Anything
// built since ships with a blank hotbar slot that falls back to the item's initial letter.
// This is the small replacement: each item's own prefab, framed by its renderer bounds,
// drawn from front-above against transparency, written next to the existing sprites and
// handed back to the asset.
//
// Idempotent and cheap: an item with an icon is left alone, so it can run after every build.
//
// Run from: Tools ▸ SpaceGame ▸ Items ▸ Bake Missing Item Icons
using System.IO;
using UnityEditor;
using UnityEngine;
using SpaceGame.Items;

namespace SpaceGame.EditorTools
{
    public static class ItemIconBaker
    {
        private const string ItemsFolder = "Assets/Game/Resources/Items";
        private const string SpriteFolder = "Assets/Game/Art/Sprites/Items";
        private const int Size = 256;

        // Front and above, the way the hotbar has always drawn its two shipped icons.
        private static readonly Vector3 ViewDirection = new Vector3(0.35f, -0.45f, 1f).normalized;

        [MenuItem("Tools/Eclipse/Items/Bake Missing Item Icons")]
        public static void Run()
        {
            int baked = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:InventoryItem", new[] { ItemsFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.icon != null || item.itemPrefab == null) continue;

                Sprite sprite = Bake(item);
                if (sprite == null) continue;

                item.icon = sprite;
                EditorUtility.SetDirty(item);
                baked++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemIconBaker] Baked {baked} icon(s).");
        }

        private static Sprite Bake(InventoryItem item)
        {
            var subject = (GameObject)PrefabUtility.InstantiatePrefab(item.itemPrefab);
            var rig = new GameObject("IconCamera");
            var texture = new RenderTexture(Size, Size, 24);

            try
            {
                Bounds bounds = RenderBounds(subject);
                if (bounds.size == Vector3.zero)
                {
                    Debug.LogError($"[ItemIconBaker] '{item.itemName}' has nothing to render.");
                    return null;
                }

                var camera = rig.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.orthographicSize = bounds.extents.magnitude * 1.05f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = bounds.extents.magnitude * 6f;
                camera.transform.position = bounds.center - ViewDirection * bounds.extents.magnitude * 3f;
                camera.transform.LookAt(bounds.center);
                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;
                var pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                pixels.Apply();
                RenderTexture.active = null;

                Directory.CreateDirectory(SpriteFolder);
                string path = $"{SpriteFolder}/{item.itemName.Replace(" ", string.Empty)}.png";
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Object.DestroyImmediate(pixels);

                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                // Single, explicitly: a texture switched to Sprite by script lands in Multiple
                // mode with no sheet defined, which imports no Sprite at all.
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();

                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            {
                Object.DestroyImmediate(subject);
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(texture);
            }
        }

        private static Bounds RenderBounds(GameObject subject)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }
}
