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
