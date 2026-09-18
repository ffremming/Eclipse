using System.IO;
using SpaceGame.Vegetation;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// The three grounds the island is painted with — grass, rock and sand — as terrain layers
    /// backed by generated textures.
    /// <para>
    /// Generated rather than imported so the world builds from nothing but this repository: the
    /// textures are flat colours with a little noise in them, enough that a slope does not read as
    /// one solid sheet, and they are ordinary assets that can be replaced by painted ones later.
    /// </para>
    /// </summary>
    public static class TerrainGroundLayers
    {
        private const string Folder = "Assets/Game/Art/Terrain";
        private const int TextureSize = 128;

        /// <summary>Metres of ground one tile of the texture covers.</summary>
        private const float TileSize = 8f;

        /// <summary>How far the noise pushes a texel's colour either side of the base colour.</summary>
        private const float Grain = 0.06f;

        /// <summary>Grass, rock and sand, in the order the splat map weights them.</summary>
        public static TerrainLayer[] Load()
        {
            Directory.CreateDirectory(Folder);

            return new[]
            {
                Layer("Grass", new Color(0.33f, 0.45f, 0.20f), seed: 11),
                Layer("Rock", new Color(0.42f, 0.41f, 0.39f), seed: 22),
                Layer("Sand", new Color(0.74f, 0.68f, 0.50f), seed: 33),
            };
        }

        private static TerrainLayer Layer(string name, Color colour, int seed)
        {
            string path = Folder + "/" + name + ".terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = Texture(name, colour, seed);
            layer.tileSize = new Vector2(TileSize, TileSize);
            layer.specular = Color.black;
            layer.smoothness = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Texture2D Texture(string name, Color colour, int seed)
        {
            string path = Folder + "/" + name + ".png";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false);
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float grain = (ValueNoise.Fractal(x * 0.25f, y * 0.25f, seed, 3, 0.5f) - 0.5f) * 2f * Grain;
                    texture.SetPixel(x, y, new Color(colour.r + grain, colour.g + grain, colour.b + grain));
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
