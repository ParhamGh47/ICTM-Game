using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool that drops invisible collision walls along both verges of a RoadArchitect road, so the player
/// cannot leave the level sideways.
///
/// The old version spaced fixed-length straight boxes along the road by distance, which cannot describe a
/// bend: on a curve the boxes cut the corner - leaving wedge-shaped gaps on the outside to drive through,
/// and biting into the asphalt on the inside. This one builds the wall as a chain whose segments <b>share
/// their ends</b>, each one spanning the offset points at its start and its finish, so the chain cannot come
/// apart however the road twists; it subdivides the segments where the road bends until a straight segment
/// strays no further than the tolerance from the true curve; it pulls the offset in on a bend tighter than
/// the offset - where a straight wall placed that far out would fold over onto the road itself - and it gives
/// each segment a body that reaches the ground, so a verge higher than the road cannot be driven over.
///
/// Usage: open a level scene (Core-1 ... Core-4), pick the road, then
///   Tools > Road Tools > Place Invisible Walls Along Road
/// </summary>
public class TerrainInvisibleWallPainter : EditorWindow
{
    /// <summary>One wall segment, ready to be turned into a BoxCollider.</summary>
    private struct Wall
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 size;
        public bool pulledIn;               // the offset had to come in because the road bends here
        public bool groundMissing;          // nothing under it, so it hangs off the road height instead
    }

    // ------------------------------------------------------------------ settings

    private Road targetRoad;

    // A level can be several RoadArchitect roads (Core-4 is five splines), so the tool can wall them all: a
    // wall down one road leaves every place the player can reach from the others wide open.
    private bool allRoads = true;

    // Section of road to place walls (spline param 0-1)
    private float startParam = 0f;
    private float endParam = 1f;

    // Distance from the asphalt + shoulder edge to the wall, and the longest straight a segment may be
    private float offsetFromRoadEdge = 8f;
    private float wallSegmentLength = 15f;
    private float wallThickness = 4f;       // thick enough that a fast truck cannot step through it
    private float wallHeight = 50f;         // how far above the highest ground nearby the wall reaches
    private float segmentOverlap = 1f;      // each segment is lengthened by this, so the joints are filled

    [Tooltip("How far a straight segment may stray from the true curve before the segment is cut in two. " +
             "This is what keeps the chain of boxes following the road instead of cutting the corner.")]
    private float maxDeviation = 0.25f;

    [Tooltip("How far above and below a wall the tool looks for ground, to give the wall a body that " +
             "reaches the terrain rather than floating at road height.")]
    private float groundSearch = 150f;

    // Preview
    private bool showPreview;
    private bool planDirty = true;
    private readonly List<Wall> plan = new List<Wall>();
    private readonly List<string> plannedRoads = new List<string>();
    private int pulledInCount;
    private int groundMissingCount;

    private Vector2 scrollPos;

    [MenuItem("Tools/Road Tools/Place Invisible Walls Along Road")]
    static void OpenWindow()
    {
        TerrainInvisibleWallPainter window = GetWindow<TerrainInvisibleWallPainter>("Invisible Wall Painter");
        window.minSize = new Vector2(400, 620);
        window.Show();
    }

    void OnEnable()
    {
        if (targetRoad == null)
        {
            Road[] roads = FindObjectsOfType<Road>();
            if (roads.Length > 0) targetRoad = roads[0];
        }

        SceneView.duringSceneGui += OnSceneGUI;
        planDirty = true;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorUtility.ClearProgressBar();
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Place Invisible Walls Along Road", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds a wall of invisible colliders down each verge. The segments share their ends and are " +
            "cut shorter where the road bends, so the wall follows the curve without gaps and never crosses " +
            "the asphalt.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        // References
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        allRoads = EditorGUILayout.ToggleLeft(
            new GUIContent("Every Road In The Scene", "Wall every built RoadArchitect road, not just one. " +
                                                       "A level is often several roads, and one walled road " +
                                                       "leaves the rest of the level open"),
            allRoads);

        Road[] roadsInScene = FindObjectsOfType<Road>();
        EditorGUILayout.LabelField("  " + UsableRoadCount(roadsInScene) + " built road(s) in this scene",
                                   EditorStyles.miniLabel);

        GUI.enabled = !allRoads;
        targetRoad = (Road)EditorGUILayout.ObjectField("Road", targetRoad, typeof(Road), true);
        GUI.enabled = true;

        List<Road> chosen = SelectedRoads();

        if (chosen.Count == 0)
        {
            EditorGUILayout.HelpBox(allRoads
                ? "No built RoadArchitect roads found in this scene."
                : "Please assign a Road reference, or tick Every Road In The Scene.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Road Info", EditorStyles.boldLabel);

        Road info = chosen[0];
        EditorGUILayout.LabelField("  Asphalt: " + info.RoadWidth() + "m (" + info.laneWidth +
                                   " x " + info.laneAmount + " lanes)   Shoulders: " +
                                   info.shoulderWidth + "m each");
        EditorGUILayout.LabelField("  Asphalt + shoulders: " + (info.RoadWidth() + info.shoulderWidth * 2f) +
                                   "m");
        EditorGUILayout.LabelField("  Walls will run along " + chosen.Count + " road(s), " + RoadLength(chosen).ToString("F0") + "m in total");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Road Section (spline 0-1)", EditorStyles.boldLabel);
        startParam = EditorGUILayout.Slider("Start %", startParam, 0f, 1f);
        endParam = EditorGUILayout.Slider("End %", endParam, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Wall Placement", EditorStyles.boldLabel);
        offsetFromRoadEdge = EditorGUILayout.Slider(
            new GUIContent("Offset From Road Edge (m)", "How far past the asphalt and shoulder the wall " +
                                                        "stands. This is the room the player has to go " +
                                                        "offroad before the wall stops them. It is always " +
                                                        "at least 1 m past the asphalt itself"),
            offsetFromRoadEdge, 1f, 50f);
        maxDeviation = EditorGUILayout.Slider(
            new GUIContent("Max Bend Deviation (m)", "How far a straight segment may stray from the curve. " +
                                                     "Smaller makes the wall follow the road more closely " +
                                                     "by cutting segments up where it bends. It is capped " +
                                                     "by the room between the wall and the asphalt (the " +
                                                     "shoulders plus the offset): a segment that strayed " +
                                                     "further would reach the road"),
            maxDeviation, 0.05f, 2f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Wall Dimensions", EditorStyles.boldLabel);
        wallSegmentLength = EditorGUILayout.Slider(
            new GUIContent("Longest Segment (m)", "The straightest a segment is allowed to be, on a straight " +
                                                  "road. Bends are cut shorter than this automatically"),
            wallSegmentLength, 5f, 50f);
        wallThickness = EditorGUILayout.Slider(
            new GUIContent("Wall Thickness (m)", "A thin wall can be tunnelled through by a truck at speed: " +
                                                 "a physics step at 120 km/h covers about 0.7 m"),
            wallThickness, 0.5f, 20f);
        wallHeight = EditorGUILayout.Slider(
            new GUIContent("Wall Height (m)", "How far above the highest ground at the wall it reaches"),
            wallHeight, 5f, 200f);
        segmentOverlap = EditorGUILayout.Slider(
            new GUIContent("Joint Overlap (m)", "Each segment is lengthened by this so neighbouring segments " +
                                                "overlap at the joint rather than meeting exactly"),
            segmentOverlap, 0f, 5f);
        groundSearch = EditorGUILayout.Slider(
            new GUIContent("Ground Search (m)", "How far above and below a wall the tool looks for terrain, " +
                                                "so the wall reaches the ground instead of stopping at road " +
                                                "height"),
            groundSearch, 10f, 400f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Tight (2m)")) offsetFromRoadEdge = 2f;
        if (GUILayout.Button("Medium (8m)")) offsetFromRoadEdge = 8f;
        if (GUILayout.Button("Wide (15m)")) offsetFromRoadEdge = 15f;
        if (GUILayout.Button("Very Wide (25m)")) offsetFromRoadEdge = 25f;
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck()) planDirty = true;

        EditorGUILayout.Space();
        DrawSummary();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Walls", GUILayout.Height(30f)))
        {
            RebuildPlan();
            showPreview = true;
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Clear Preview", GUILayout.Height(30f)))
        {
            showPreview = false;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("PLACE INVISIBLE WALLS", GUILayout.Height(40f)))
        {
            RebuildPlan();

            if (plan.Count == 0)
            {
                Debug.LogError("Invisible Walls: nothing to place. Check the road section and the offsets.");
            }
            else if (EditorUtility.DisplayDialog("Place Invisible Walls",
                "Place " + plan.Count + " invisible wall segments along both sides of " + plannedRoads.Count +
                " road(s), from " + (Mathf.Min(startParam, endParam) * 100f).ToString("F0") + "% to " +
                (Mathf.Max(startParam, endParam) * 100f).ToString("F0") + "%?\n\n" +
                "An existing 'InvisibleWalls' object is reused and added to.\n\n" +
                "This action can be undone (Ctrl+Z).",
                "Place", "Cancel"))
            {
                PlaceWalls();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove ALL Invisible Walls From Scene", GUILayout.Height(25f)))
        {
            if (EditorUtility.DisplayDialog("Remove All Invisible Walls",
                "Remove every object named 'InvisibleWall' from the scene?\n\nThis action can be undone.",
                "Remove", "Cancel"))
            {
                RemoveAllWalls();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary()
    {
        RebuildPlan();

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);

        if (plan.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing to place. Check the section, the offset and the segment length.",
                                    MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("  Segments: " + plan.Count + "   (about " + (plan.Count / 2) +
                                   " along each verge)");
        EditorGUILayout.LabelField("  Roads: " + plannedRoads.Count + "   Wall offset: " +
                                   offsetFromRoadEdge.ToString("F1") + "m past the asphalt and shoulder");

        if (pulledInCount > 0)
            EditorGUILayout.LabelField("  " + pulledInCount + " segments pulled in by a tight bend, so the wall " +
                                       "does not fold over the road");

        if (groundMissingCount > 0)
            EditorGUILayout.LabelField("  " + groundMissingCount + " segments had no terrain under them, so they " +
                                       "hang off the road height");
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!showPreview || plan.Count == 0) return;

        Handles.color = new Color(0f, 0.85f, 1f, 0.35f);

        for (int i = 0; i < plan.Count; i++)
        {
            Wall wall = plan[i];

            Handles.matrix = Matrix4x4.TRS(wall.position, wall.rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, wall.size);

            if (wall.pulledIn)
            {
                Handles.color = new Color(1f, 0.6f, 0f, 0.5f);
                Handles.DrawWireCube(Vector3.zero, wall.size * 1.02f);
                Handles.color = new Color(0f, 0.85f, 1f, 0.35f);
            }
        }

        Handles.matrix = Matrix4x4.identity;
    }

    // ------------------------------------------------------------------ plan ---

    /// <summary>The roads this paint covers: every built one in the scene, or just the chosen one.</summary>
    private List<Road> SelectedRoads()
    {
        List<Road> roads = new List<Road>();

        if (allRoads)
        {
            Road[] found = FindObjectsOfType<Road>();

            for (int i = 0; i < found.Length; i++)
            {
                Road road = found[i];
                if (road != null && road.spline != null && road.spline.distance > 0.01f) roads.Add(road);
            }
        }
        else if (targetRoad != null)
        {
            roads.Add(targetRoad);
        }

        return roads;
    }

    private static int UsableRoadCount(Road[] roads)
    {
        int count = 0;
        for (int i = 0; i < roads.Length; i++)
            if (roads[i] != null && roads[i].spline != null && roads[i].spline.distance > 0.01f) count++;

        return count;
    }

    private static float RoadLength(List<Road> roads)
    {
        float total = 0f;
        for (int i = 0; i < roads.Count; i++) total += roads[i].spline.distance;

        return total;
    }

    /// <summary>How far the wall stands from the road's centre line.</summary>
    private float WallOffset(Road road)
    {
        return RoadHalfWidth(road) + Mathf.Max(1f, offsetFromRoadEdge);
    }

    private static float RoadHalfWidth(Road road)
    {
        return road != null ? road.RoadWidth() * 0.5f + road.shoulderWidth : 0f;
    }

    /// <summary>
    /// How far a straight segment may stray from the true offset curve, given how much room there is between
    /// the wall and the asphalt (<paramref name="clearance"/>).
    ///
    /// This is what makes "never on the asphalt" a guarantee rather than a hope: on the outside of a bend a
    /// segment's straight chord dips towards the road by exactly its stray, so the stray is held under the
    /// room the wall has. With the default 3 m shoulders and an 8 m offset that room is 11 m, far more than
    /// any sensible deviation - the cap only ever bites when both are set tiny.
    /// </summary>
    private float DeviationTolerance(float clearance)
    {
        return Mathf.Min(maxDeviation, Mathf.Max(0.05f, clearance));
    }

    private void RebuildPlan()
    {
        if (!planDirty) return;

        plan.Clear();
        plannedRoads.Clear();
        pulledInCount = 0;
        groundMissingCount = 0;
        planDirty = false;

        float from = Mathf.Clamp01(Mathf.Min(startParam, endParam));
        float to = Mathf.Clamp01(Mathf.Max(startParam, endParam));
        if (to - from < 0.0001f) return;

        List<Road> roads = SelectedRoads();

        for (int r = 0; r < roads.Count; r++)
        {
            BuildPlanFor(roads[r], from, to);
            plannedRoads.Add(roads[r].name);
        }
    }

    /// <summary>The walls down both verges of one road.</summary>
    private void BuildPlanFor(Road road, float from, float to)
    {
        SplineC spline = road != null ? road.spline : null;
        if (spline == null || spline.distance <= 0.01f) return;

        float total = spline.distance;

        float desired = WallOffset(road);
        float halfRoad = RoadHalfWidth(road);
        float longest = Mathf.Max(1f, wallSegmentLength);

        // Room between the wall and the asphalt: the shoulders, plus the offset. A segment's straight chord
        // is never allowed to stray by more than this, which is what keeps the wall off the road surface.
        float asphaltHalf = halfRoad - road.shoulderWidth;
        float clearance = desired - asphaltHalf;

        for (int side = -1; side <= 1; side += 2)
        {
            float param = from;

            while (param < to - 0.00005f)
            {
                float step = NextStep(spline, param, to, desired, clearance, side, total, longest);
                if (step <= 0.00005f) break;

                Vector3 a = OffsetPoint(spline, param, desired, side, total, out bool pulledA);
                Vector3 b = OffsetPoint(spline, param + step, desired, side, total, out bool pulledB);

                float length = Vector3.Distance(a, b);

                // The two ends are the same point a neighbouring segment ends on - unless the road doubles
                // back so far that they land on top of each other, which is a segment not worth having.
                if (length > 0.01f)
                {
                    Wall wall = new Wall();
                    wall.size = new Vector3(wallThickness, 1f, length + Mathf.Max(0f, segmentOverlap));
                    wall.pulledIn = pulledA || pulledB;

                    Vector3 middle = (a + b) * 0.5f;
                    Vector3 flat = b - a;
                    flat.y = 0f;

                    if (flat.sqrMagnitude > 0.0001f)
                    {
                        wall.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

                        // The offset point is the wall's *near* face, so the box is pushed out by half its
                        // thickness: thickening the wall then moves it away from the road instead of making
                        // its inner face eat into the asphalt.
                        Vector3 outward = new Vector3(flat.z, 0f, -flat.x).normalized * side;
                        middle += outward * (wallThickness * 0.5f);

                        // The wall has to reach the ground rather than float at road height: a verge higher
                        // than the road would otherwise be driven over, and one below it driven under.
                        float roadLow = Mathf.Min(a.y, b.y);
                        float roadHigh = Mathf.Max(a.y, b.y);
                        float groundA = GroundAt(a, a.y, out bool foundA);
                        float groundB = GroundAt(b, b.y, out bool foundB);

                        if (!foundA || !foundB) groundMissingCount++;

                        // A wall reaching down as far as it reaches up is already more than anything can dig
                        // through, so a verge that falls away into a valley does not make a 200 m collider.
                        float floor = Mathf.Min(roadLow, roadHigh) - wallHeight;
                        float low = Mathf.Max(Mathf.Min(roadLow, Mathf.Min(groundA, groundB)) - 3f, floor);
                        float high = Mathf.Max(roadHigh, Mathf.Max(groundA, groundB)) + wallHeight;

                        middle.y = (low + high) * 0.5f;
                        wall.size.y = Mathf.Max(1f, high - low);
                        wall.position = middle;

                        plan.Add(wall);
                    }
                }

                if (pulledA || pulledB) pulledInCount++;
                param += step;
            }
        }
    }

    /// <summary>
    /// How far the next segment runs, in param. It starts at the longest a segment may be and halves until a
    /// straight line between the two offset points - and the middle of it - all stay within the deviation
    /// tolerance, so a bend is described by as many short segments as it takes rather than by one chord that
    /// cuts the corner.
    /// </summary>
    private float NextStep(SplineC spline, float from, float limit, float desired, float clearance, int side,
                           float total, float longest)
    {
        float step = Mathf.Min(longest / total, limit - from);
        float tolerance = DeviationTolerance(clearance);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (step <= 0.00005f) return 0f;

            bool ignored;

            Vector3 a = OffsetPoint(spline, from, desired, side, total, out ignored);
            Vector3 b = OffsetPoint(spline, from + step, desired, side, total, out ignored);

            // The chord is tested at its quarters as well as its middle: a stretch of road that bends one way
            // and then the other can bulge between the ends without the middle of the segment showing it.
            float stray = 0f;

            for (int sample = 1; sample <= 3; sample++)
            {
                float t = sample * 0.25f;

                Vector3 onCurve = OffsetPoint(spline, from + step * t, desired, side, total, out ignored);
                Vector3 onChord = Vector3.Lerp(a, b, t);

                float offsetHere = Vector3.Distance(onChord, onCurve);
                if (offsetHere > stray) stray = offsetHere;
            }

            if (stray <= tolerance) break;

            step *= 0.5f;
        }

        return step;
    }

    /// <summary>
    /// A point the given distance to one side of the road - the verge the wall runs down.
    ///
    /// Where the road bends tighter than that distance the offset would fold back over the road itself (a
    /// point that far out simply does not exist on that side of a tight bend), so it is pulled in to most of
    /// the bend's radius, never inside the asphalt edge. Which segments were pulled in is reported.
    /// </summary>
    private Vector3 OffsetPoint(SplineC spline, float param, float desired, int side, float total,
                                out bool pulledIn)
    {
        Vector3 position, tangent;
        spline.GetSplineValueBoth(Mathf.Clamp01(param), out position, out tangent);

        Vector3 right = new Vector3(tangent.z, 0f, -tangent.x);

        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        // On a bend tighter than the wall stands out, the point that far to the side simply does not exist:
        // past the bend's own centre it comes back out on the *other* side, which is how a straight wall ends
        // up lying across the road. So the wall is pulled in to 85% of the bend's radius instead - still
        // outside the asphalt, because 0.85 of a radius a road can actually be built with is wider than the
        // asphalt's own half, and a bend tighter than that is a road folded through itself.
        float radius = CurvatureRadius(spline, param, total);
        float lateral = Mathf.Min(desired, radius * 0.85f);

        pulledIn = lateral < desired - 0.01f;

        Vector3 point = position + right * (lateral * side);
        point.y = position.y;

        return point;
    }

    /// <summary>
    /// Radius of the road's bend at a point, in metres, or a huge number where it is straight. The heading is
    /// measured over a couple of metres of road, so the answer is the bend the driver actually takes rather
    /// than the curvature at one instant.
    /// </summary>
    private static float CurvatureRadius(SplineC spline, float param, float total)
    {
        const float span = 2f;

        float half = 0.5f * span / Mathf.Max(1f, total);
        float a = Mathf.Clamp01(param - half);
        float b = Mathf.Clamp01(param + half);

        if (b - a < 0.00005f) return float.MaxValue;

        Vector3 position, tangentA, tangentB;
        spline.GetSplineValueBoth(a, out position, out tangentA);
        spline.GetSplineValueBoth(b, out position, out tangentB);

        tangentA.y = 0f;
        tangentB.y = 0f;

        if (tangentA.sqrMagnitude < 0.0001f || tangentB.sqrMagnitude < 0.0001f) return float.MaxValue;

        float sweep = Vector3.Angle(tangentA, tangentB) * Mathf.Deg2Rad;
        if (sweep < 0.0005f) return float.MaxValue;

        return (b - a) * total / sweep;
    }

    /// <summary>The ground under a point, falling back to the road's own height where there is none.</summary>
    private float GroundAt(Vector3 point, float fallback, out bool found)
    {
        RaycastHit hit;

        if (Physics.Raycast(point + Vector3.up * groundSearch, Vector3.down, out hit,
                            groundSearch * 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            found = true;
            return hit.point.y;
        }

        found = false;
        return fallback;
    }

    // ----------------------------------------------------------------- paint ---

    private void PlaceWalls()
    {
        RebuildPlan();

        if (plan.Count == 0) return;

        GameObject parent = FindOrCreateParent();

        int placed = 0;

        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (i % 25 == 0 &&
                    EditorUtility.DisplayCancelableProgressBar("Placing Invisible Walls",
                        "Creating wall segments... " + placed + " placed", (float)i / plan.Count))
                {
                    break;
                }

                Wall wall = plan[i];

                GameObject wallObj = new GameObject("InvisibleWall " + (i + 1));
                Undo.RegisterCreatedObjectUndo(wallObj, "Create Invisible Wall");
                Undo.SetTransformParent(wallObj.transform, parent.transform, "Create Invisible Wall");

                wallObj.transform.position = wall.position;
                wallObj.transform.rotation = wall.rotation;
                wallObj.transform.localScale = Vector3.one;

                BoxCollider collider = wallObj.AddComponent<BoxCollider>();
                collider.isTrigger = false;
                collider.center = Vector3.zero;
                collider.size = wall.size;

                placed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log("Invisible Walls: placed " + placed + " segments along " + plannedRoads.Count +
                  " road(s) (params " +
                  Mathf.Min(startParam, endParam).ToString("F2") + " - " +
                  Mathf.Max(startParam, endParam).ToString("F2") + ", " +
                  offsetFromRoadEdge.ToString("F1") + "m past the asphalt and shoulder)." +
                  (pulledInCount > 0 ? " " + pulledInCount + " were pulled in by a tight bend." : "") +
                  (groundMissingCount > 0 ? " " + groundMissingCount + " had no terrain under them." : ""));
    }

    GameObject FindOrCreateParent()
    {
        GameObject existing = GameObject.Find("InvisibleWalls");
        if (existing != null) return existing;

        GameObject parent = new GameObject("InvisibleWalls");
        Undo.RegisterCreatedObjectUndo(parent, "Create InvisibleWalls Parent");
        return parent;
    }

    void RemoveAllWalls()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int removed = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("InvisibleWall"))
            {
                Undo.DestroyObjectImmediate(obj);
                removed++;
            }
        }

        GameObject parent = GameObject.Find("InvisibleWalls");
        if (parent != null && parent.transform.childCount == 0)
            Undo.DestroyObjectImmediate(parent);

        Debug.Log("Invisible Walls: removed " + removed + " objects from the scene.");
    }
}
