// Turns a glTF point cloud into a Unity mesh asset.
//
// The supernova remnant is a scan: 2.1 million POINTS primitives with positions and vertex
// colours and nothing else. Unity has no glTF importer, and a converted FBX loses the point
// topology (it arrives as loose vertices with no primitives, so nothing draws). So the .glb
// stays in _Source~ — outside the asset database — and this reads it directly into a mesh
// asset with MeshTopology.Points, which the Eclipse/Effects/Point Cloud Unlit shader draws.
//
// Only the narrow case is handled: float POSITION and float COLOR_0 accessors in a binary
// .glb. Anything else in the file throws rather than importing something half-right.
//
// Run from: Tools ▸ Eclipse ▸ Art ▸ Build Supernova Remnant Mesh
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public static class PointCloudMeshBuilder
    {
        private const string SourceGlb =
            "Assets/Game/Art/Models/Environment/SupernovaRemnant/_Source~/SupernovaRemnant.glb";
        private const string MeshAsset =
            "Assets/Game/Art/Models/Environment/SupernovaRemnant/SupernovaRemnant.asset";

        // The scan holds 2.17 million points, which serialises to a 139 MB mesh asset. Every
        // nth point is kept instead: the cloud reads the same at the distance it is seen from,
        // and the asset stays in the tens of megabytes. Raise it if the thinning ever shows.
        private const int MaxPoints = 500_000;

        private const uint GlbMagic = 0x46546C67;   // "glTF"
        private const uint ChunkJson = 0x4E4F534A;  // "JSON"
        private const uint ChunkBinary = 0x004E4942; // "BIN"
        private const int ComponentTypeFloat = 5126;
        private const int PrimitiveModePoints = 0;

        [MenuItem("Tools/Eclipse/Art/Build Supernova Remnant Mesh")]
        public static void Run()
        {
            Mesh mesh = BuildMesh(SourceGlb);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAsset);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshAsset);
            }
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PointCloudMeshBuilder] {MeshAsset}: {mesh.vertexCount} points, bounds {mesh.bounds.size}.");
        }

        private static Mesh BuildMesh(string glbPath)
        {
            byte[] bytes = File.ReadAllBytes(glbPath);
            ReadHeader(bytes, out GlbJson json, out int binaryStart);

            var positions = new List<Vector3>();
            var colors = new List<Color32>();

            foreach (GlbJson.Mesh sourceMesh in json.meshes)
            {
                foreach (GlbJson.Primitive primitive in sourceMesh.primitives)
                {
                    if (primitive.mode != PrimitiveModePoints)
                        throw new InvalidDataException(
                            $"{glbPath}: primitive mode {primitive.mode} is not POINTS.");

                    ReadVector3(json, bytes, binaryStart, primitive.attributes.POSITION, positions);
                    ReadColor(json, bytes, binaryStart, primitive.attributes.COLOR_0, colors);
                }
            }

            if (positions.Count != colors.Count)
                throw new InvalidDataException(
                    $"{glbPath}: {positions.Count} positions but {colors.Count} colours.");

            int step = Mathf.Max(1, Mathf.CeilToInt(positions.Count / (float)MaxPoints));
            var keptPositions = new List<Vector3>(positions.Count / step + 1);
            var keptColors = new List<Color32>(keptPositions.Capacity);
            for (int i = 0; i < positions.Count; i += step)
            {
                // glTF is right-handed with +Z forward; Unity is left-handed. The cloud keeps
                // its source units (a radius of 2); the prefab scales it to the size it is used at.
                Vector3 p = positions[i];
                keptPositions.Add(new Vector3(-p.x, p.y, p.z));
                keptColors.Add(colors[i]);
            }
            positions = keptPositions;
            colors = keptColors;

            var indices = new int[positions.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            var mesh = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(MeshAsset),
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void ReadHeader(byte[] bytes, out GlbJson json, out int binaryStart)
        {
            if (BitConverter.ToUInt32(bytes, 0) != GlbMagic)
                throw new InvalidDataException("Not a binary glTF file.");

            int jsonLength = (int)BitConverter.ToUInt32(bytes, 12);
            if (BitConverter.ToUInt32(bytes, 16) != ChunkJson)
                throw new InvalidDataException("First glTF chunk is not JSON.");

            json = JsonUtility.FromJson<GlbJson>(Encoding.UTF8.GetString(bytes, 20, jsonLength));

            int binaryHeader = 20 + jsonLength;
            if (BitConverter.ToUInt32(bytes, binaryHeader + 4) != ChunkBinary)
                throw new InvalidDataException("Second glTF chunk is not BIN.");
            binaryStart = binaryHeader + 8;
        }

        private static void ReadVector3(GlbJson json, byte[] bytes, int binaryStart, int accessorIndex,
            List<Vector3> into)
        {
            ResolveAccessor(json, binaryStart, accessorIndex, 12, out int offset, out int stride, out int count);
            for (int i = 0; i < count; i++)
            {
                int at = offset + i * stride;
                into.Add(new Vector3(
                    BitConverter.ToSingle(bytes, at),
                    BitConverter.ToSingle(bytes, at + 4),
                    BitConverter.ToSingle(bytes, at + 8)));
            }
        }

        private static void ReadColor(GlbJson json, byte[] bytes, int binaryStart, int accessorIndex,
            List<Color32> into)
        {
            ResolveAccessor(json, binaryStart, accessorIndex, 16, out int offset, out int stride, out int count);
            for (int i = 0; i < count; i++)
            {
                int at = offset + i * stride;
                into.Add((Color32)new Color(
                    BitConverter.ToSingle(bytes, at),
                    BitConverter.ToSingle(bytes, at + 4),
                    BitConverter.ToSingle(bytes, at + 8),
                    BitConverter.ToSingle(bytes, at + 12)));
            }
        }

        private static void ResolveAccessor(GlbJson json, int binaryStart, int accessorIndex, int elementSize,
            out int offset, out int stride, out int count)
        {
            GlbJson.Accessor accessor = json.accessors[accessorIndex];
            if (accessor.componentType != ComponentTypeFloat)
                throw new InvalidDataException(
                    $"Accessor {accessorIndex} has component type {accessor.componentType}; only float is handled.");

            GlbJson.BufferView view = json.bufferViews[accessor.bufferView];
            offset = binaryStart + view.byteOffset + accessor.byteOffset;
            stride = view.byteStride > 0 ? view.byteStride : elementSize;
            count = accessor.count;
        }

        // The slice of the glTF schema this importer needs; JsonUtility ignores the rest.
        [Serializable]
        private class GlbJson
        {
            public Accessor[] accessors;
            public BufferView[] bufferViews;
            public Mesh[] meshes;

            [Serializable]
            public class Accessor
            {
                public int bufferView;
                public int byteOffset;
                public int componentType;
                public int count;
            }

            [Serializable]
            public class BufferView
            {
                public int byteOffset;
                public int byteStride;
            }

            [Serializable]
            public class Mesh
            {
                public Primitive[] primitives;
            }

            [Serializable]
            public class Primitive
            {
                public Attributes attributes;
                public int mode;
            }

            [Serializable]
            public class Attributes
            {
                public int POSITION;
                public int COLOR_0;
            }
        }
    }
}
