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
    /// measures the mesh and derives the transform from it.
    /// </para>
    /// <para>
    /// Shared by every item builder, because the alternative is each builder carrying its own copy
    /// of the same measurement and the copies quietly disagreeing about which end is the handle.
    /// </para>
    /// </summary>
    public static class ModelMount
    {
        /// <summary>
        /// How far either side of the grip height, in metres, a vertex still counts as part of the
        /// handle when working out where its axis runs.
        /// </summary>
        private const float GripSampleHalfHeight = 0.03f;

        /// <summary>
        /// Instances <paramref name="modelPath"/> under <paramref name="parent"/>, stands it along
        /// +Y at <paramref name="targetLength"/> metres with the handle end at the origin and its
        /// axis through the origin, and returns the far end and the point the hand closes on.
        /// </summary>
        /// <param name="handle">Which end of the model, as authored, the handle is on.</param>
        /// <param name="gripAlong">How far up the model from the handle end the hand closes, as a
        /// fraction of its length: 0 is the pommel, 1 the tip.</param>
        public static MountedModel Mount(Transform parent, string modelPath, float targetLength,
                                         HandleEnd handle, float gripAlong)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogError($"[ModelMount] No model at {modelPath}.");
            }

            GameObject holder = new GameObject("Model");
            holder.transform.SetParent(parent, false);

            Vector3 grip = new Vector3(0f, targetLength * gripAlong, 0f);

            if (source != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, holder.transform);
                instance.transform.localPosition = Vector3.zero;

                // Measured in the holder's space, not the instance's own: an imported root can
                // carry a rotation of its own (a GLB's Y-up-to-Z-up), and its local axes are then
                // not the ones the holder's rotation below acts on.
                if (TryGetLocalBounds(instance, holder.transform, out Bounds bounds))
                {
                    Vector3 size = bounds.size;
                    int longest = size.x > size.y && size.x > size.z ? 0 : (size.y >= size.z ? 1 : 2);

                    holder.transform.localRotation = StandUp(longest, handle);

                    float length = size[longest];
                    if (length > 1e-4f)
                    {
                        instance.transform.localScale *= targetLength / length;

                        // Standing and sized. Put the handle end on the origin and the model's axis
                        // through it, so the hand closes on the handle rather than in the middle of
                        // the blade and the item is not offset sideways from its own pivot.
                        TryGetLocalBounds(instance, parent, out Bounds stood);
                        holder.transform.localPosition = new Vector3(-stood.center.x, -stood.min.y, -stood.center.z);

                        grip = AxisAt(instance, parent, grip.y);
                    }
                }
            }

            GameObject tip = new GameObject("Tip");
            tip.transform.SetParent(parent, false);
            tip.transform.localPosition = new Vector3(0f, targetLength, 0f);
            return new MountedModel(tip.transform, grip);
        }

        /// <summary>
        /// The rotation that puts the longest axis on +Y with the handle at the bottom. An
        /// already-upright model with its handle low takes the identity, so its authored
        /// orientation is left exactly as the artist had it.
        /// </summary>
        private static Quaternion StandUp(int longest, HandleEnd handle)
        {
            // Each of these carries the authored high end of the axis to the top.
            Quaternion stand = longest == 0 ? Quaternion.Euler(0f, 0f, 90f)
                             : longest == 2 ? Quaternion.Euler(-90f, 0f, 0f)
                             : Quaternion.identity;

            return handle == HandleEnd.HighEnd ? Quaternion.Euler(180f, 0f, 0f) * stand : stand;
        }

        /// <summary>
        /// Where the model's own axis runs at <paramref name="height"/>: the middle of the vertices
        /// around that height, in <paramref name="space"/>. A handle is not on the middle of the
        /// bounding box — an axe's is off to one side of its head — and this is what finds it.
        /// </summary>
        private static Vector3 AxisAt(GameObject instance, Transform space, float height)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = toSpace.MultiplyPoint3x4(vertex);
                    if (Mathf.Abs(point.y - height) > GripSampleHalfHeight) continue;

                    sum += point;
                    count++;
                }
            }

            return count == 0 ? new Vector3(0f, height, 0f) : new Vector3(sum.x / count, height, sum.z / count);
        }

        /// <summary>Combined mesh bounds of <paramref name="instance"/>, in <paramref name="space"/>.</summary>
        private static bool TryGetLocalBounds(GameObject instance, Transform space, out Bounds bounds)
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
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = GeometryUtility.CalculateBounds(Corners(meshBounds), toSpace);

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
