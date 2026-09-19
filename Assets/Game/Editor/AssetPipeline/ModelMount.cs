using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Stands an imported model upright at a known size, whatever the artist exported.
    /// <para>
    /// Every model in this project arrives from a different tool with a different idea of scale, up
    /// axis and where the origin sits — a Blender OBJ in metres, an FBX in centimetres lying on its
    /// side, a GLB with its pivot at the world origin rather than at the grip. Rather than hand-tune
    /// a set of magic transforms per asset and re-tune them whenever one is re-exported, this
    /// measures the renderer bounds and derives the transform from them.
    /// </para>
    /// <para>
    /// Shared by every item builder, because the alternative is each builder carrying its own copy
    /// of the same measurement and the copies quietly disagreeing about which end is the handle.
    /// </para>
    /// </summary>
    public static class ModelMount
    {
        /// <summary>
        /// Instances <paramref name="modelPath"/> under <paramref name="parent"/>, stands it along
        /// +Y at <paramref name="targetLength"/> metres with its butt at the origin, and returns an
        /// empty transform at its far end.
        /// </summary>
        public static Transform Mount(Transform parent, string modelPath, float targetLength)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogError($"[ModelMount] No model at {modelPath}.");
            }

            GameObject holder = new GameObject("Model");
            holder.transform.SetParent(parent, false);

            if (source != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, holder.transform);
                instance.transform.localPosition = Vector3.zero;

                if (TryGetLocalBounds(instance, out Bounds bounds))
                {
                    Vector3 size = bounds.size;
                    int longest = size.x > size.y && size.x > size.z ? 0 : (size.y >= size.z ? 1 : 2);

                    // Rotate the long axis onto +Y. An already-upright model takes the identity
                    // branch rather than a zero-degree rotation, so its authored orientation is left
                    // exactly as the artist had it.
                    if (longest == 0) holder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    else if (longest == 2) holder.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                    float length = size[longest];
                    if (length > 1e-4f)
                    {
                        float scale = targetLength / length;
                        instance.transform.localScale *= scale;

                        // Slide the butt to the origin along the axis that is now up, so the hand
                        // closes on the handle rather than in the middle of the blade.
                        float butt = (bounds.center[longest] - size[longest] * 0.5f) * scale;
                        Vector3 local = instance.transform.localPosition;
                        local[longest] -= butt;
                        instance.transform.localPosition = local;
                    }
                }
            }

            GameObject tip = new GameObject("Tip");
            tip.transform.SetParent(parent, false);
            tip.transform.localPosition = new Vector3(0f, targetLength, 0f);
            return tip.transform;
        }

        /// <summary>Combined renderer bounds in the instance's own local space.</summary>
        private static bool TryGetLocalBounds(GameObject instance, out Bounds bounds)
        {
            bounds = new Bounds();

            MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>();
            bool any = false;

            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null) continue;

                Bounds meshBounds = filter.sharedMesh.bounds;

                // Meshes on child transforms carry their own offset and scale, which a naive
                // encapsulate of the raw mesh bounds would throw away — and a weapon whose blade is
                // a child would then be measured as if the blade sat at the handle.
                Matrix4x4 toInstance = instance.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = GeometryUtility.CalculateBounds(Corners(meshBounds), toInstance);

                if (any) bounds.Encapsulate(local);
                else { bounds = local; any = true; }
            }

            return any;
        }

        private static Vector3[] Corners(Bounds b) => new[]
        {
            b.min,
            b.max,
            new Vector3(b.min.x, b.min.y, b.max.z),
            new Vector3(b.min.x, b.max.y, b.min.z),
            new Vector3(b.max.x, b.min.y, b.min.z),
            new Vector3(b.min.x, b.max.y, b.max.z),
            new Vector3(b.max.x, b.min.y, b.max.z),
            new Vector3(b.max.x, b.max.y, b.min.z),
        };
    }
}
