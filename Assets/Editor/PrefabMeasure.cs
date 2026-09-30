using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Measures where a prefab's geometry actually is, relative to its own root.
///
/// The props and signs in this project do not all have their pivot at their feet - the Blinder's
/// model, for one, hangs below its prefab root - so a painter that trusts the pivot sinks objects
/// into the road or leaves them floating. Measuring the meshes instead means a painter can sit the
/// lowest point of whatever it just placed on the ground.
///
/// The measurement is taken from the meshes' own bounds transformed into the prefab's local frame, so
/// it does not depend on anything in the scene, and it is cached per prefab.
/// </summary>
public static class PrefabMeasure
{
    public struct Footprint
    {
        /// <summary>Local Y of the lowest point of the prefab's geometry.</summary>
        public float Bottom;

        /// <summary>Local Y of the highest point.</summary>
        public float Top;

        /// <summary>Widest horizontal reach from the pivot, used for spacing objects apart.</summary>
        public float Radius;

        public float Height { get { return Top - Bottom; } }
    }

    private static readonly Dictionary<GameObject, Footprint> Cache = new Dictionary<GameObject, Footprint>();

    public static Footprint Of(GameObject prefab)
    {
        Footprint cached;
        if (prefab != null && Cache.TryGetValue(prefab, out cached))
            return cached;

        Footprint footprint = Measure(prefab);

        if (prefab != null)
            Cache[prefab] = footprint;

        return footprint;
    }

    /// <summary>Forgets a cached measurement, for example after a prefab is re-imported.</summary>
    public static void Clear()
    {
        Cache.Clear();
    }

    /// <summary>
    /// How much of the world the prefab's geometry would cover along one world direction, if it were placed
    /// with the given rotation at the given scale. Nothing has to be instantiated: the same mesh bounds the
    /// footprint is taken from are put back through the root's own scale, the placement's rotation and its
    /// scale, which is exactly what Unity is about to do.
    ///
    /// This is what lets a painter size something to the road rather than to a hand-picked number - a strip
    /// that has to reach from one side of the asphalt to the other is measured and scaled to do it, whatever
    /// size the model inside the prefab happens to be.
    ///
    /// <paramref name="centreOffset"/> comes back with where the middle of that geometry sits along the same
    /// direction, measured from the prefab's pivot. A prefab's geometry is not always centred on its pivot -
    /// the Rumble's strip hangs about 2.6 m along its own length - so a painter that wants a strip centred on
    /// the road has to know this, or it centres an empty point and leaves the strip hanging off one edge.
    /// </summary>
    public static float SpanAlong(GameObject prefab, Quaternion rotation, float scale, Vector3 worldDirection,
                                  out float centreOffset)
    {
        centreOffset = 0f;

        if (prefab == null) return 0f;

        Vector3 direction = worldDirection;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return 0f;

        direction.Normalize();

        Transform root = prefab.transform;
        Vector3 rootScale = root.localScale;

        float lowest = float.MaxValue;
        float highest = float.MinValue;
        bool measured = false;

        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null) continue;

            Bounds bounds = mesh.bounds;
            Vector3 centre = bounds.center;
            Vector3 extents = bounds.extents;
            Transform part = filters[i].transform;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3(
                    ((corner & 1) == 0 ? -extents.x : extents.x),
                    ((corner & 2) == 0 ? -extents.y : extents.y),
                    ((corner & 4) == 0 ? -extents.z : extents.z));

                // In the prefab's own frame, as the footprint is measured - the root's own scale has been
                // taken out of it, so it goes back on here, then the placement's rotation and scale.
                Vector3 local = root.InverseTransformPoint(part.TransformPoint(centre + offset));
                Vector3 placed = rotation * Vector3.Scale(local * scale, rootScale);

                float along = Vector3.Dot(placed, direction);

                lowest = Mathf.Min(lowest, along);
                highest = Mathf.Max(highest, along);
                measured = true;
            }
        }

        if (!measured) return 0f;

        centreOffset = (lowest + highest) * 0.5f;

        return highest - lowest;
    }

    private static Footprint Measure(GameObject prefab)
    {
        Footprint footprint = new Footprint();

        if (prefab == null)
            return footprint;

        Transform root = prefab.transform;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        float radius = 0f;
        bool measured = false;

        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null) continue;

            Bounds bounds = mesh.bounds;
            Vector3 centre = bounds.center;
            Vector3 extents = bounds.extents;
            Transform part = filters[i].transform;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3(
                    ((corner & 1) == 0 ? -extents.x : extents.x),
                    ((corner & 2) == 0 ? -extents.y : extents.y),
                    ((corner & 4) == 0 ? -extents.z : extents.z));

                Vector3 local = root.InverseTransformPoint(part.TransformPoint(centre + offset));

                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
                radius = Mathf.Max(radius, new Vector2(local.x, local.z).magnitude);
                measured = true;
            }
        }

        if (!measured)
            return footprint;

        footprint.Bottom = minY;
        footprint.Top = maxY;
        footprint.Radius = radius;

        return footprint;
    }
}
