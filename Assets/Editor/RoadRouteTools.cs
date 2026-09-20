using System;
using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One road in a painter's list, in driving order.
/// </summary>
[Serializable]
public class RoadRouteEntry
{
    public Road road;

    [Tooltip("Traffic on this road flows from its end back to its start.")]
    public bool reverse;
}

/// <summary>
/// One road's slice of the route: where it starts and ends along the cumulative distance.
/// </summary>
public class RoadRouteSegment
{
    public Road road;
    public bool reverse;
    public float start;
    public float length;

    public float End { get { return start + length; } }
}

/// <summary>
/// The road bookkeeping the roadside painters share: turning the scene's roads into one drivable
/// route, sampling that route, and asking how sharply it turns at a given distance.
///
/// A route is a list of <see cref="RoadRouteEntry"/> - one per road, in the order the player drives
/// them, each optionally reversed - which becomes a list of <see cref="RoadRouteSegment"/> laid end to
/// end. Distances are then cumulative metres along the whole route, so a painter can walk "the road
/// the player drives" as a single line instead of road by road, and a curve that runs across two
/// roads is still seen as one curve.
///
/// RoadLightPainter carries its own copy of this logic from before this file existed; anything new
/// should go through here.
/// </summary>
public static class RoadRoute
{
    /// <summary>Metres between samples when scanning the route for curves.</summary>
    public const float TurnSampleStep = 2f;

    // ------------------------------------------------------------------ route

    /// <summary>Lays the painter's roads end to end, skipping any without a built spline.</summary>
    public static List<RoadRouteSegment> BuildSegments(List<RoadRouteEntry> roads)
    {
        List<RoadRouteSegment> segments = new List<RoadRouteSegment>();
        float acc = 0f;

        for (int i = 0; i < roads.Count; i++)
        {
            RoadRouteEntry entry = roads[i];
            if (entry == null || entry.road == null || entry.road.spline == null) continue;

            float length = entry.road.spline.distance;
            if (length <= 0.01f) continue;

            RoadRouteSegment segment = new RoadRouteSegment();
            segment.road = entry.road;
            segment.reverse = entry.reverse;
            segment.start = acc;
            segment.length = length;

            segments.Add(segment);
            acc += length;
        }

        return segments;
    }

    public static float TotalLength(List<RoadRouteSegment> segments)
    {
        if (segments == null || segments.Count == 0) return 0f;
        return segments[segments.Count - 1].End;
    }

    /// <summary>
    /// World position and driving tangent at a cumulative distance along the route.
    ///
    /// <c>road.spline.distance</c> and <c>TranslateDistBasedToParam</c> both measure along the spline
    /// itself, so the parameter is looked up in distance rather than assumed to be linear in it - on a
    /// road with long straights and tight corners those are not the same thing.
    /// </summary>
    public static RoadRouteSegment SampleAt(List<RoadRouteSegment> segments, float distance,
                                            out Vector3 position, out Vector3 tangent)
    {
        position = Vector3.zero;
        tangent = Vector3.forward;
        if (segments == null || segments.Count == 0) return null;

        RoadRouteSegment chosen = segments[segments.Count - 1];
        for (int i = 0; i < segments.Count; i++)
        {
            if (distance <= segments[i].End + 0.001f)
            {
                chosen = segments[i];
                break;
            }
        }

        float localDist = Mathf.Clamp(distance - chosen.start, 0f, chosen.length);
        float localParam = chosen.length > 0.001f ? localDist / chosen.length : 0f;

        SplineC spline = chosen.road.spline;
        if (spline.RoadDefKeysArray != null && spline.RoadDefKeysArray.Length > 0)
            localParam = Mathf.Clamp01(spline.TranslateDistBasedToParam(localDist));

        float t = chosen.reverse ? 1f - localParam : localParam;

        Vector3 rawPos;
        Vector3 rawTangent;
        spline.GetSplineValueBoth(Mathf.Clamp01(t), out rawPos, out rawTangent);

        position = rawPos;
        tangent = chosen.reverse ? -rawTangent : rawTangent;

        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;
        else tangent.Normalize();

        return chosen;
    }

    /// <summary>Signed turn rate in degrees per metre: positive is turning right.</summary>
    public static float TurnRateAt(List<RoadRouteSegment> segments, float distance)
    {
        Vector3 beforePos;
        Vector3 beforeTan;
        if (SampleAt(segments, Mathf.Max(0f, distance - TurnSampleStep), out beforePos, out beforeTan) == null)
            return 0f;

        Vector3 afterPos;
        Vector3 afterTan;
        SampleAt(segments, Mathf.Min(TotalLength(segments), distance + TurnSampleStep), out afterPos, out afterTan);

        beforeTan.y = 0f;
        afterTan.y = 0f;
        if (beforeTan.sqrMagnitude < 0.0001f || afterTan.sqrMagnitude < 0.0001f)
            return 0f;

        beforeTan.Normalize();
        afterTan.Normalize();

        return Vector3.SignedAngle(beforeTan, afterTan, Vector3.up) / (TurnSampleStep * 2f);
    }

    /// <summary>Right of the direction of travel, flat: points away from the road on the driver's right.</summary>
    public static Vector3 Across(Vector3 tangent)
    {
        return new Vector3(tangent.z, 0f, -tangent.x);
    }

    /// <summary>Yaw that points an instance's local +Z down the tangent.</summary>
    public static float YawAlong(Vector3 tangent)
    {
        return Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
    }

    /// <summary>How far the asphalt reaches to one side of the centre line, shoulder included.</summary>
    public static float RoadEdge(Road road)
    {
        if (road == null) return 0f;
        return road.RoadWidth() * 0.5f + road.shoulderWidth;
    }

    /// <summary>
    /// Drops a point onto whatever is actually under it - the verge, not the spline's own height, so
    /// props sit on the ground rather than floating over a bank or sinking into a slope.
    /// </summary>
    public static bool TryFindGround(Vector3 point, float probeHeight, float maxDrop, float minNormalY,
                                     out Vector3 ground)
    {
        ground = point;

        RaycastHit hit;
        if (!Physics.Raycast(point + Vector3.up * probeHeight, Vector3.down, out hit,
                             probeHeight + maxDrop, ~0, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.normal.y < minNormalY)
            return false;

        ground = hit.point;
        return true;
    }

    // ----------------------------------------------------------- surroundings

    /// <summary>
    /// World XZ of every tree the terrain paints. Trees are instances on the terrain data rather than
    /// objects in the scene, so this is the only way to know where the woods really are.
    /// </summary>
    public static List<Vector2> TreePoints()
    {
        List<Vector2> points = new List<Vector2>();
        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();

        for (int i = 0; i < terrains.Length; i++)
        {
            TerrainData data = terrains[i].terrainData;
            if (data == null) continue;

            Vector3 origin = terrains[i].transform.position;
            TreeInstance[] trees = data.treeInstances;

            for (int t = 0; t < trees.Length; t++)
            {
                Vector3 world = origin + Vector3.Scale(trees[t].position, data.size);
                points.Add(new Vector2(world.x, world.z));
            }
        }

        return points;
    }

    /// <summary>
    /// World XZ of the level's real structures: renderers that are not the road, not the terrain and not
    /// anything the given roots own. Density of these is what "built up" means to the painters, so a tool
    /// does not have to know how any particular building is named.
    /// </summary>
    public static List<Vector2> StructurePoints(params GameObject[] excludeRoots)
    {
        List<Vector2> points = new List<Vector2>();
        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null) continue;

            if (renderer.GetComponentInParent<Road>() != null) continue;
            if (renderer.GetComponentInParent<Terrain>() != null) continue;

            bool excluded = false;
            for (int r = 0; r < excludeRoots.Length; r++)
            {
                if (excludeRoots[r] != null && renderer.transform.IsChildOf(excludeRoots[r].transform))
                    excluded = true;
            }
            if (excluded) continue;

            // Sky domes, water sheets and other one-mesh backdrops are wider than a road is; they are
            // scenery, not buildings.
            Vector3 size = renderer.bounds.size;
            if (size.x > 60f || size.z > 60f || size.y > 60f) continue;

            points.Add(new Vector2(renderer.bounds.center.x, renderer.bounds.center.z));
        }

        return points;
    }

    /// <summary>
    /// The root objects the roadside tools create. Every tool excludes all of them when it reads the
    /// surroundings, so a level painted once already cannot make the next pass think a town has grown up
    /// where the tool itself put the signs and bins.
    /// </summary>
    public static GameObject[] PainterRoots()
    {
        string[] names = { "Roadside Props", "Roadside Signs", "Billboards", "PassingCars" };
        List<GameObject> roots = new List<GameObject>();

        for (int i = 0; i < names.Length; i++)
        {
            GameObject root = FindRootByName(names[i]);
            if (root != null) roots.Add(root);
        }

        return roots.ToArray();
    }

    public static int CountWithin(List<Vector2> points, Vector2 origin, float radius)
    {
        if (points == null || points.Count == 0) return 0;

        float squared = radius * radius;
        int count = 0;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 delta = points[i] - origin;
            if (delta.sqrMagnitude <= squared) count++;
        }

        return count;
    }

    /// <summary>How much of that kind of surroundings a spot has, from 0 to 1.</summary>
    public static float Density(List<Vector2> points, Vector2 origin, float radius, float fullCount)
    {
        return Mathf.Clamp01(CountWithin(points, origin, radius) / Mathf.Max(1f, fullCount));
    }

    // ------------------------------------------------------------------ scene

    /// <summary>Every road in the scene that has a built spline, in scene order.</summary>
    public static List<RoadRouteEntry> FindRoadsInScene()
    {
        List<RoadRouteEntry> entries = new List<RoadRouteEntry>();
        Road[] found = UnityEngine.Object.FindObjectsOfType<Road>();

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null || found[i].spline == null) continue;
            if (found[i].spline.distance <= 0.01f) continue;

            RoadRouteEntry entry = new RoadRouteEntry();
            entry.road = found[i];
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// The shared roads list every painter shows: number, road, reverse toggle, reorder and remove.
    /// Returns true when the list changed, which is the caller's cue to rebuild its plan.
    /// </summary>
    public static bool DrawRoadList(List<RoadRouteEntry> roads, out int usableCount)
    {
        bool changed = false;
        int removeIndex = -1;

        for (int i = 0; i < roads.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((i + 1) + ".", GUILayout.Width(22f));
            roads[i].road = (Road)EditorGUILayout.ObjectField(roads[i].road, typeof(Road), true);
            roads[i].reverse = EditorGUILayout.ToggleLeft(
                new GUIContent("Rev", "Traffic on this road flows from its end back to its start"),
                roads[i].reverse, GUILayout.Width(48f));

            GUI.enabled = i > 0;
            if (GUILayout.Button("^", GUILayout.Width(24f))) { Move(roads, i, -1); changed = true; }
            GUI.enabled = i < roads.Count - 1;
            if (GUILayout.Button("v", GUILayout.Width(24f))) { Move(roads, i, 1); changed = true; }
            GUI.enabled = true;

            if (GUILayout.Button("x", GUILayout.Width(24f))) removeIndex = i;
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
        {
            roads.RemoveAt(removeIndex);
            changed = true;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Road Slot")) { roads.Add(new RoadRouteEntry()); changed = true; }

        if (GUILayout.Button("Find Roads in Scene"))
        {
            List<RoadRouteEntry> found = FindRoadsInScene();
            if (found.Count == 0)
            {
                Debug.LogWarning("No RoadArchitect roads with a built spline in this scene.");
            }
            else
            {
                roads.Clear();
                roads.AddRange(found);
                changed = true;
            }
        }
        EditorGUILayout.EndHorizontal();

        usableCount = 0;
        for (int i = 0; i < roads.Count; i++)
            if (roads[i] != null && roads[i].road != null && roads[i].road.spline != null) usableCount++;

        return changed;
    }

    public static void Move(List<RoadRouteEntry> roads, int index, int delta)
    {
        int target = index + delta;
        if (target < 0 || target >= roads.Count) return;

        RoadRouteEntry tmp = roads[target];
        roads[target] = roads[index];
        roads[index] = tmp;
    }

    /// <summary>
    /// Only matches ROOT objects, never children, so a painter can never reach into an artist's own
    /// hierarchy by accident.
    /// </summary>
    public static GameObject FindRootByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i] != null && roots[i].name == name)
                return roots[i];

        return null;
    }

    public static void MarkSceneDirty()
    {
        if (SceneManager.GetActiveScene().IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
