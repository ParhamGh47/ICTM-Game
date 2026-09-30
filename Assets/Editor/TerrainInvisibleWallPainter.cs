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
/// Where one road hands over to the next the two walls are joined by running each on along its own line until
/// they meet, rather than by one wall straight across the corner: a straight join is a chord across the inside
/// of the bend, so it comes in towards the asphalt, and that is what put invisible walls in the middle of open
/// ground at every corner.
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

    /// <summary>A sampled point of one verge's wall line, before it is cut into segments.</summary>
    private struct VergePoint
    {
        public Vector3 point;               // where the wall stands at this sample
        public Vector3 centre;              // the road centre line it was measured from
        public Vector3 right;               // flat, away from the road on this verge's side
        public Vector3 tangent;             // flat, the way the road runs here
        public float lateral;               // how far out from the centre line the wall stands here
        public float along;                 // flat distance from the first sample of this verge
        public bool byBend;                 // the bend was tighter than the offset, so the wall came in
        public bool byRoad;                 // another road was in the way, so the wall came in
        public bool placeable;              // false when there is nowhere legal for a wall at all here
    }

    /// <summary>The end of one verge, kept so that the ends of the level can be closed.</summary>
    private struct VergeEnd
    {
        public Road road;
        public int side;
        public float drivableHalf;          // asphalt plus shoulders, which is what a cap has to stay clear of
        public Vector3 point;               // the wall's own stand at this end
        public Vector3 inner;               // the same place pulled in to just off the asphalt
        public Vector3 outward;             // flat, leading off the end of the road
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

    [Tooltip("How far a wall must keep from the asphalt of *any* road in the scene, its own included. A wall " +
             "thirty metres out from one road lands on the next one wherever a level hairpins or a second road " +
             "runs alongside, and a wall in the middle of a road the player drives on is worse than no wall at " +
             "all - it is an invisible crash. Walls come in towards the road until they clear this.")]
    private float keepOffRoads = 1f;

    [Tooltip("Close the ends of the wall: where two roads meet, the two wall lines are joined across the " +
             "corner between them, and where the route simply stops - the start and the end of the level - each " +
             "wall is folded back in towards the road, leaving only the road's own width open. Without this, a " +
             "driver who reaches the end of the last wall can go round the back of it and off the map.")]
    private bool sealEnds = true;

    [Tooltip("How close two road ends have to be to count as one junction - where the wall of one road carries " +
             "on into the wall of the next - rather than as two separate ends of the level.")]
    private float junctionTolerance = 25f;

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
    private readonly List<VergeEnd> vergeEnds = new List<VergeEnd>();

    // Every road in the scene, sampled, so a spot can be measured against all of them at once. Rebuilt with
    // the plan, not per query: the plan asks it a few thousand questions.
    private RoadField field;

    private int pulledInCount;              // came in because of a bend
    private int roadPulledCount;            // came in because of another road
    private int groundMissingCount;
    private int unplaceableCount;           // no legal place for a wall here at all
    private int joinedCount;                // junctions sealed across the corner
    private int cappedCount;                // ends of the level folded shut
    private int unsealedCount;              // ends that could not be sealed at all
    private int skippedCount;               // walls dropped because they would have stood on a road
    private int auditViolations;
    private float auditMinClearance = float.MaxValue;

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
            "the asphalt. At a junction the two walls are joined by carrying each on along its own line until " +
            "they meet, so the join stays as far from the road as the wall itself does.",
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
                                                        "at least 1 m past the asphalt itself, and it comes " +
                                                        "in on its own wherever it would stand on a road"),
            offsetFromRoadEdge, 1f, 60f);
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
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sealing", EditorStyles.boldLabel);
        keepOffRoads = EditorGUILayout.Slider(
            new GUIContent("Keep Clear Of Roads (m)", "How far a wall keeps from the asphalt of any road in " +
                                                      "the scene, its own included. Walls come in towards the " +
                                                      "road, and are dropped, until they clear it"),
            keepOffRoads, 0.25f, 10f);
        sealEnds = EditorGUILayout.ToggleLeft(
            new GUIContent("Seal The Ends", "Join the wall across each junction - each wall carried on along " +
                                             "its own line until the two meet, so the join never comes in " +
                                             "towards the road - and fold it shut at the ends of the route, " +
                                             "so the player cannot go round the back of it"),
            sealEnds);
        GUI.enabled = sealEnds;
        junctionTolerance = EditorGUILayout.Slider(
            new GUIContent("Junction Reach (m)", "How close two road ends have to be to count as one " +
                                                  "junction rather than as two ends of the level"),
            junctionTolerance, 2f, 80f);
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Tight (2m)")) offsetFromRoadEdge = 2f;
        if (GUILayout.Button("Medium (8m)")) offsetFromRoadEdge = 8f;
        if (GUILayout.Button("Wide (15m)")) offsetFromRoadEdge = 15f;
        if (GUILayout.Button("Very Wide (25m)")) offsetFromRoadEdge = 25f;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Far (30m)")) offsetFromRoadEdge = 30f;
        if (GUILayout.Button("Further (35m)")) offsetFromRoadEdge = 35f;
        if (GUILayout.Button("Open (45m)")) offsetFromRoadEdge = 45f;
        if (GUILayout.Button("Everything Else")) offsetFromRoadEdge = 60f;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("  A wall further out meets more of the level: any road it would stand on " +
                                   "pulls it back in, and the summary says how many were pulled.",
                                   EditorStyles.miniLabel);

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

        if (sealEnds)
        {
            EditorGUILayout.LabelField("  " + joinedCount + " junction(s) joined along their own lines, " +
                                       cappedCount + " end(s) of the route folded shut");
        }

        if (pulledInCount > 0)
            EditorGUILayout.LabelField("  " + pulledInCount + " segments pulled in by a tight bend, so the wall " +
                                       "does not fold over the road");

        if (roadPulledCount > 0)
            EditorGUILayout.LabelField("  " + roadPulledCount + " segments pulled in by another road, so the wall " +
                                       "does not stand on one");

        if (groundMissingCount > 0)
            EditorGUILayout.LabelField("  " + groundMissingCount + " segments had no terrain under them, so they " +
                                       "hang off the road height");

        // The audit: what the plan promises, checked against every road in the scene rather than assumed.
        if (skippedCount == 0 && unsealedCount == 0 && auditViolations == 0 && unplaceableCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Checked against every road in the scene: no segment stands within " + keepOffRoads.ToString("F1") +
                " m of any asphalt (the closest is " +
                (auditMinClearance > 9000f ? "nothing near" : auditMinClearance.ToString("F1") + " m") +
                "), every verge is one unbroken chain, and both ends of the route are closed.",
                MessageType.Info);
        }
        else
        {
            string report = "This plan has holes in it:\n";

            if (unplaceableCount > 0)
                report += "\u2022 " + unplaceableCount + " place(s) where no wall fits beside the road at all " +
                          "(another road is too close) - lower the offset or wall that road too.\n";
            if (unsealedCount > 0)
                report += "\u2022 " + unsealedCount + " end(s) could not be sealed: no straight wall from there " +
                          "stays off the roads.\n";
            if (skippedCount > 0)
                report += "\u2022 " + skippedCount + " wall(s) were dropped rather than laid on a road, which " +
                          "leaves a gap there.\n";

            report += "\nPress Preview Walls to see the plan, then PLACE. Every skipped spot is named in the " +
                      "console.";

            EditorGUILayout.HelpBox(report, MessageType.Warning);
        }
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
        vergeEnds.Clear();
        pulledInCount = 0;
        roadPulledCount = 0;
        groundMissingCount = 0;
        unplaceableCount = 0;
        joinedCount = 0;
        cappedCount = 0;
        unsealedCount = 0;
        skippedCount = 0;
        auditViolations = 0;
        auditMinClearance = float.MaxValue;
        planDirty = false;

        float from = Mathf.Clamp01(Mathf.Min(startParam, endParam));
        float to = Mathf.Clamp01(Mathf.Max(startParam, endParam));
        if (to - from < 0.0001f) return;

        List<Road> roads = SelectedRoads();
        if (roads.Count == 0) return;

        // Every road in the scene, not just the chosen ones: a wall has to keep off the roads it is not
        // walling exactly as much as off its own, and it is the ones it is not walling that turn up under it.
        // Sampled every two metres, which is fine enough that a wall cannot be placed on an asphalt edge
        // without the measurement saying so (see RoadField.Slack).
        field = new RoadField(FindObjectsOfType<Road>(), 2f);

        for (int r = 0; r < roads.Count; r++)
        {
            BuildPlanFor(roads[r], from, to, Mathf.Max(0.25f, keepOffRoads));
            plannedRoads.Add(roads[r].name);
        }

        // A wall that runs past the end of the level, or past a junction, is a wall the player can walk
        // round. This is what turns the chains into one closed boundary.
        if (sealEnds) SealEnds(Mathf.Max(0.25f, keepOffRoads));
    }

    /// <summary>The walls down both verges of one road.</summary>
    private void BuildPlanFor(Road road, float from, float to, float margin)
    {
        SplineC spline = road != null ? road.spline : null;
        if (spline == null || spline.distance <= 0.01f) return;

        float total = spline.distance;

        float desired = WallOffset(road);

        // The shoulders count as road: they are wide, they are flat and a truck can drive on them, so a wall
        // that took them for verge is a wall in the road. Everything below measures from this, not from the
        // asphalt, and a wall pulled in stops outside it rather than on it.
        float drivableHalf = RoadHalfWidth(road);

        // Room between the wall and the drivable edge: what a segment's straight chord may stray by, since on
        // the outside of a bend a chord dips towards the road by exactly its stray.
        float tolerance = DeviationTolerance(desired - drivableHalf);

        for (int side = -1; side <= 1; side += 2)
        {
            List<VergePoint> line = SampleVerge(spline, total, from, to, desired, drivableHalf, margin, side);

            if (line.Count < 2) continue;

            // Cut the verge into segments: each one takes as many samples as it can while a straight wall
            // between its two ends still follows the line. Consecutive segments share an end by construction,
            // which is why there is no joint to leave a gap in.
            int start = 0;

            while (start < line.Count - 1)
            {
                int last = FurthestWithin(line, start, tolerance);

                bool byBend = line[start].byBend || line[last].byBend;
                bool byRoad = line[start].byRoad || line[last].byRoad;

                if (line[start].placeable && line[last].placeable)
                {
                    AddWall(line[start].point, line[last].point, wallThickness, byBend, byRoad, false, margin);

                    if (byBend) pulledInCount++;
                    if (byRoad) roadPulledCount++;
                }
                else
                {
                    for (int i = start; i <= last; i++)
                        if (!line[i].placeable) unplaceableCount++;
                }

                start = last;
            }

            // The two ends of this verge, for the sealing pass: a wall that stops in open country is a wall
            // the player can walk round.
            //
            // They are taken from the samples the wall could actually stand at, not from the first and last
            // sample of the verge. At a junction the road's own end sits inside the next road's corridor, so
            // those samples are unplaceable and their offset has collapsed to a couple of metres off the
            // asphalt - sealing from there was dropping a join right beside the road.
            int firstStand = -1;
            int lastStand = -1;

            for (int i = 0; i < line.Count; i++)
            {
                if (!line[i].placeable) continue;

                if (firstStand < 0) firstStand = i;
                lastStand = i;
            }

            if (firstStand < 0) continue;

            vergeEnds.Add(MakeEnd(road, side, drivableHalf, line[firstStand], false, margin));
            vergeEnds.Add(MakeEnd(road, side, drivableHalf, line[lastStand], true, margin));
        }
    }

    /// <summary>Metres between samples along a road while a plan is built. Every wall is measured at these
    /// points, so this is what decides how closely the wall follows a bend.</summary>
    private const float SampleStep = 2f;

    /// <summary>
    /// One verge of one road, sampled every couple of metres: where a wall may stand at each sample, and why
    /// it stands no further out.
    ///
    /// Everything the plan does is worked out here and then only cut into segments afterwards, which keeps the
    /// expensive half - measuring a spot against every road in the scene - to one pass per sample rather than
    /// one per attempt.
    /// </summary>
    private List<VergePoint> SampleVerge(SplineC spline, float total, float from, float to, float desired,
                                         float drivableHalf, float margin, int side)
    {
        List<VergePoint> line = new List<VergePoint>();

        float length = Mathf.Max(0.01f, (to - from) * total);
        int count = Mathf.Max(2, Mathf.CeilToInt(length / SampleStep));

        float travelled = 0f;
        Vector3 previous = Vector3.zero;

        for (int i = 0; i <= count; i++)
        {
            float param = Mathf.Clamp01(Mathf.Lerp(from, to, i / (float)count));

            Vector3 position, tangent;
            spline.GetSplineValueBoth(param, out position, out tangent);

            Vector3 right = new Vector3(tangent.z, 0f, -tangent.x);

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            else
                right.Normalize();

            VergePoint point = new VergePoint();
            point.centre = position;
            point.right = right;

            point.tangent = tangent;
            point.tangent.y = 0f;
            if (point.tangent.sqrMagnitude < 0.0001f) point.tangent = Vector3.forward;
            else point.tangent.Normalize();

            float lateral;
            bool byBend;
            bool byRoad;

            point.placeable = ChooseLateral(position, right, side, desired, drivableHalf,
                                            CurvatureRadius(spline, param, total), margin,
                                            out lateral, out byBend, out byRoad);

            point.lateral = lateral;
            point.byBend = byBend;
            point.byRoad = byRoad;
            point.point = position + right * (lateral * side);
            point.point.y = position.y;

            if (i > 0) travelled += FlatDistance(previous, point.point);
            point.along = travelled;
            previous = point.point;

            line.Add(point);
        }

        return line;
    }

    /// <summary>
    /// How far out the wall may stand at one sample, and whether it had to come in to get there.
    ///
    /// Three things can hold it in. The asphalt and shoulders, which it may never touch. A bend tighter than
    /// the offset, where the point that far to the side does not exist - past the bend's own centre it comes
    /// back out on the other side of the road, which is how a straight wall ends up lying across the asphalt.
    /// And another road: with the offset far out, a wall runs into the carriageway of the next road along,
    /// round the inside of a hairpin, or across a road that passes underneath the one being walled. That last
    /// one is the reason this measures against every road in the scene and not just its own.
    ///
    /// <paramref name="thickness"/> is counted in because a wall is a box: it is the far face, not the line
    /// the wall was measured along, that would stand on the next road.
    ///
    /// Returns whether there is anywhere legal to stand at all.
    /// </summary>
    private bool ChooseLateral(Vector3 position, Vector3 right, int side, float desired, float drivableHalf,
                               float bendRadius, float margin, out float lateral, out bool byBend,
                               out bool byRoad)
    {
        float required = margin + RoadField.Slack;

        // Closest the wall may come: just off the drivable surface, and never further in than the offset the
        // tool was asked for - a wall that has to come in to get round a bend or another road still stands as
        // far out as it legally can, rather than being pushed onto the shoulders.
        float minimum = Mathf.Min(desired, drivableHalf + required);
        float bendLimit = bendRadius * 0.85f;

        byBend = bendLimit < desired - 0.01f;
        byRoad = false;

        float maximum = Mathf.Min(desired, bendLimit);

        // A road narrower than the room the wall needs has nowhere legal to stand beside it: reported, and the
        // wall skipped, rather than a wall laid in a road.
        if (maximum <= minimum)
        {
            lateral = minimum;
            return false;
        }

        // Out from the closest legal stand to the wanted one, keeping the furthest that is clear. Eight steps
        // are enough to place a wall within a few centimetres of the best it could be, and it costs nothing on
        // the overwhelmingly common sample where the wanted offset is already clear.
        const int probes = 8;

        for (int i = probes; i >= 0; i--)
        {
            float candidate = Mathf.Lerp(minimum, maximum, i / (float)probes);

            Vector3 stand = position + right * (candidate * side);
            Vector3 outer = position + right * ((candidate + wallThickness) * side);

            if (field.Clearance(stand) < required) continue;
            if (field.Clearance(outer) < required) continue;

            lateral = candidate;
            byRoad = candidate < maximum - 0.01f;

            return true;
        }

        lateral = minimum;

        return false;
    }

    /// <summary>
    /// How far along the verge one straight segment may run from a given sample: as many samples ahead as the
    /// longest allowed segment reaches, and then back off while a straight wall between the two ends strays
    /// further from the sampled line than it may.
    /// </summary>
    private int FurthestWithin(List<VergePoint> line, int start, float tolerance)
    {
        float longest = Mathf.Max(1f, wallSegmentLength);
        int limit = start + 1;

        while (limit < line.Count - 1 && line[limit + 1].along - line[start].along <= longest) limit++;

        while (limit > start + 1 && !ChordFollowsLine(line, start, limit, tolerance)) limit--;

        return limit;
    }

    /// <summary>
    /// Whether a straight wall between two samples follows the line between them. Every sample in between is
    /// measured against the chord, not just the middle: a stretch of road that bends one way and then the other
    /// bulges between its ends without the middle of the segment showing it.
    /// </summary>
    private static bool ChordFollowsLine(List<VergePoint> line, int start, int end, float tolerance)
    {
        float span = line[end].along - line[start].along;
        if (span <= 0.0001f) return true;

        for (int i = start + 1; i < end; i++)
        {
            float t = (line[i].along - line[start].along) / span;
            Vector3 onChord = Vector3.Lerp(line[start].point, line[end].point, t);

            if (FlatDistance(onChord, line[i].point) > tolerance) return false;
        }

        return true;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;

        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// The end of one verge, in the form the sealing pass wants it: where the wall stops, and the same place
    /// pulled in to just off the asphalt, which is where a cap at the end of the level begins.
    /// <paramref name="outgoing"/> says which end of the road this is, and so which way leads off it.
    /// </summary>
    private static VergeEnd MakeEnd(Road road, int side, float drivableHalf, VergePoint end, bool outgoing,
                                    float margin)
    {
        VergeEnd verge = new VergeEnd();
        verge.road = road;
        verge.side = side;
        verge.drivableHalf = drivableHalf;
        verge.point = end.point;
        verge.inner = end.centre + end.right * (side * (drivableHalf + margin + RoadField.Slack));
        verge.inner.y = end.point.y;
        verge.outward = end.tangent * (outgoing ? 1f : -1f);

        return verge;
    }

    /// <summary>
    /// Adds one wall between two points, if it can stand there legally. Returns whether it was added.
    ///
    /// The two points are the wall's <b>near</b> face, so the box is pushed out from them by half its
    /// thickness - out being whichever side has more room, which on a verge is away from the road it belongs
    /// to. The wall then has to reach the ground rather than float at road height, because a verge higher than
    /// the road is otherwise driven over and one below it driven under.
    ///
    /// The whole box is measured against every road in the scene before it is kept. A wall that would stand on
    /// a road is dropped and counted instead: an invisible wall in the middle of a road the player is meant to
    /// be driving on is worse than a gap they can see.
    /// </summary>
    private bool AddWall(Vector3 a, Vector3 b, float thickness, bool byBend, bool byRoad, bool seal,
                         float margin)
    {
        Vector3 flat = b - a;
        flat.y = 0f;

        float length = flat.magnitude;
        if (length <= 0.01f) return false;

        Vector3 along = flat / length;
        Vector3 perp = new Vector3(along.z, 0f, -along.x);
        Vector3 middle = (a + b) * 0.5f;

        if (field.Clearance(middle + perp) < field.Clearance(middle - perp)) perp = -perp;

        Vector3 push = perp * (thickness * 0.5f);

        float required = margin + RoadField.Slack;
        float clearance = BoxClearance(a, b, push);

        if (clearance < required)
        {
            skippedCount++;
            auditViolations++;

            if (seal) unsealedCount++;

            Debug.LogWarning("Invisible Walls: a wall was dropped rather than laid " + clearance.ToString("F1") +
                             " m from a road at " + middle + ", which is inside the " + required.ToString("F1") +
                             " m it keeps clear of.");

            return false;
        }

        if (clearance < auditMinClearance) auditMinClearance = clearance;

        Wall wall = new Wall();
        wall.size = new Vector3(thickness, 1f, length + Mathf.Max(0f, segmentOverlap));
        wall.pulledIn = byBend || byRoad;
        wall.rotation = Quaternion.LookRotation(along, Vector3.up);

        float roadLow = Mathf.Min(a.y, b.y);
        float roadHigh = Mathf.Max(a.y, b.y);
        float groundA = GroundAt(a, a.y, out bool foundA);
        float groundB = GroundAt(b, b.y, out bool foundB);

        if (!foundA || !foundB)
        {
            groundMissingCount++;
            wall.groundMissing = true;
        }

        // A wall reaching down as far as it reaches up is already more than anything can dig through, so a
        // verge that falls away into a valley does not make a 200 m collider.
        float low = Mathf.Max(Mathf.Min(roadLow, Mathf.Min(groundA, groundB)) - 3f, roadLow - wallHeight);
        float high = Mathf.Max(roadHigh, Mathf.Max(groundA, groundB)) + wallHeight;

        Vector3 centre = middle + push;
        centre.y = (low + high) * 0.5f;

        wall.size.y = Mathf.Max(1f, high - low);
        wall.position = centre;

        plan.Add(wall);

        return true;
    }

    /// <summary>
    /// How clear a wall box is of every road: the two ends of the line it was measured along, the two ends of
    /// its far face, and the far face's own length. The far face is the one that matters - a wall whose near
    /// face is a metre off the asphalt can still have its back half standing on the road behind it.
    /// </summary>
    private float BoxClearance(Vector3 a, Vector3 b, Vector3 push)
    {
        Vector3 outerA = a + push;
        Vector3 outerB = b + push;

        float best = Mathf.Min(field.Clearance(a), field.Clearance(b));
        best = Mathf.Min(best, Mathf.Min(field.Clearance(outerA), field.Clearance(outerB)));

        for (int i = 1; i < 4; i++)
            best = Mathf.Min(best, field.Clearance(Vector3.Lerp(outerA, outerB, i * 0.25f)));

        return best;
    }

    /// <summary>
    /// Joins the two walls of a junction by running each one on along its own line until the two meet, instead
    /// of laying a single wall straight across the corner between them.
    ///
    /// The straight join is what used to come in towards the road at every corner. A wall from one verge end to
    /// the other is a chord across the inside of the bend, and the sharper the corner the closer that chord
    /// passes to the asphalt - at a right angle it is only about two thirds of the offset out - so a player who
    /// cut the corner met an invisible wall in the middle of what looked like open ground.
    ///
    /// Every wall is already the right distance from its own road and runs in a known direction - the road's own
    /// heading at that end - so the two lines only have to be followed until they cross. The meeting point is
    /// the corner of the offset boundary, and it is further out than the chord ever was, never nearer the road.
    /// Where the two roads run on together there is no corner at all: their lines are parallel, and the short
    /// wall between the two ends is then already the wall that belongs in the gap.
    /// </summary>
    private bool JoinEnds(VergeEnd a, VergeEnd b, float margin)
    {
        Vector3 from = a.point;
        Vector3 to = b.point;

        Vector3 corner;

        if (!TryOffsetCorner(a, b, out corner))
            return AddWallRun(from, to, margin);

        // Going round the corner is only worth it if both legs actually get there. Where a junction brings the
        // two roads close together, the lines meet on the far side of one of them, and cutting across its
        // asphalt to reach the corner is the very thing this is trying to avoid. There the short wall between
        // the two ends is both the better wall and the only one that can be laid.
        float required = margin + RoadField.Slack;

        if (!RunIsClear(from, corner, required) || !RunIsClear(corner, to, required))
            return AddWallRun(from, to, margin);

        bool laid = AddWallRun(from, corner, margin);

        if (AddWallRun(corner, to, margin)) laid = true;

        return laid;
    }

    /// <summary>Whether a straight run between two points stays clear of every road in the scene.</summary>
    private bool RunIsClear(Vector3 from, Vector3 to, float required)
    {
        float span = FlatDistance(from, to);
        int samples = Mathf.Clamp(Mathf.CeilToInt(span / Mathf.Max(2f, wallSegmentLength)), 2, 24);

        return field.ClearanceAlong(from, to, null, samples) >= required;
    }

    /// <summary>
    /// Where the two walls of a junction meet when each is followed on along its own line. False when the two
    /// roads run on together, so there is no corner to go round, and when the lines only meet so far away that
    /// following them would swing the wall across the level rather than round a corner.
    /// </summary>
    private bool TryOffsetCorner(VergeEnd a, VergeEnd b, out Vector3 corner)
    {
        corner = Vector3.zero;

        Vector3 da = a.outward;
        Vector3 db = b.outward;
        da.y = 0f;
        db.y = 0f;

        float cross = da.x * db.z - da.z * db.x;

        // Parallel, which includes a road that simply carries on as the next one.
        if (Mathf.Abs(cross) < 0.05f) return false;

        Vector3 delta = b.point - a.point;
        delta.y = 0f;

        float along = (delta.x * db.z - delta.z * db.x) / cross;

        Vector3 meeting = a.point + da * along;

        // A gentle bend puts the corner a long way off. There the two ends are close together and the straight
        // join between them is within a metre or so of both lines, so it is the better wall.
        float desired = Mathf.Max(WallOffset(a.road), WallOffset(b.road));
        float reach = Mathf.Max(FlatDistance(a.point, b.point), desired) * 1.5f;

        if (Mathf.Abs(along) > reach) return false;
        if (FlatDistance(b.point, meeting) > reach) return false;

        meeting.y = (a.point.y + b.point.y) * 0.5f;
        corner = meeting;
        return true;
    }

    /// <summary>
    /// Lays one straight run of a join as a chain of segments no longer than the tool's own segment length, so
    /// a join follows the ground it crosses exactly as the verge walls do. Returns whether any of it was laid;
    /// a run with a piece dropped in it is counted as a hole, because that is a gap in the boundary.
    /// </summary>
    private bool AddWallRun(Vector3 from, Vector3 to, float margin)
    {
        float span = FlatDistance(from, to);
        if (span <= 0.01f) return false;

        int pieces = Mathf.Clamp(Mathf.CeilToInt(span / Mathf.Max(2f, wallSegmentLength)), 1, 24);

        bool laid = false;
        bool dropped = false;

        for (int i = 0; i < pieces; i++)
        {
            Vector3 start = Vector3.Lerp(from, to, i / (float)pieces);
            Vector3 finish = Vector3.Lerp(from, to, (i + 1) / (float)pieces);

            if (AddWall(start, finish, wallThickness, false, false, false, margin)) laid = true;
            else dropped = true;
        }

        if (dropped && laid) unsealedCount++;

        return laid;
    }

    /// <summary>
    /// Closes the wall where the level turns a corner or simply stops.
    ///
    /// A chain of walls down two verges is an open tube: the player who reaches its end can drive round the
    /// back of it and away across the map. Two things fix that. Where two roads meet - which is most of a
    /// level's road ends, the roads being laid end to end - the verge of one is joined to the verge of the
    /// other across the corner between them. And where the route really does stop, the start and the end of
    /// the level, the wall is folded back in towards the road, stopping just short of the asphalt, so the only
    /// way past it is along the road itself.
    /// </summary>
    private void SealEnds(float margin)
    {
        bool[] used = new bool[vergeEnds.Count];
        float required = margin + RoadField.Slack;

        // How far apart two road ends may be and still be one junction. The user's own reach is the floor, but
        // it is raised to match the offset: a wall thirty metres out meets thirty metres back from the corner,
        // so at a wide offset the two ends of a junction can be fifty metres apart and still be the same
        // corner. Without this, a wide wall would be capped at each junction instead of joined, and the pocket
        // beside the corner is a way off the map.
        float reach = Mathf.Max(2f, junctionTolerance);

        List<Road> chosen = SelectedRoads();
        for (int i = 0; i < chosen.Count; i++) reach = Mathf.Max(reach, WallOffset(chosen[i]) * 1.5f);

        for (int i = 0; i < vergeEnds.Count; i++)
        {
            if (used[i]) continue;

            int best = -1;
            float bestDistance = reach;

            for (int j = 0; j < vergeEnds.Count; j++)
            {
                if (j == i || used[j]) continue;

                // A road's own two ends are never a junction with each other, however close a hairpin brings
                // them: what is between them is the road itself.
                if (vergeEnds[j].road == vergeEnds[i].road) continue;

                float distance = FlatDistance(vergeEnds[i].point, vergeEnds[j].point);
                if (distance >= bestDistance) continue;

                // The join is a wall across the land between two road ends, so it has to be checked for roads
                // on the way and not only at its ends.
                if (field.ClearanceAlong(vergeEnds[i].point, vergeEnds[j].point, null, 8) < required) continue;

                bestDistance = distance;
                best = j;
            }

            if (best < 0) continue;

            if (JoinEnds(vergeEnds[i], vergeEnds[best], margin))
            {
                joinedCount++;
                used[i] = true;
                used[best] = true;
            }

            // An end whose join could not be laid at all is left for the cap pass below rather than booked as
            // joined: a cap is a worse wall than a corner, but an end with no wall at all is a hole.
        }

        for (int i = 0; i < vergeEnds.Count; i++)
        {
            if (used[i]) continue;

            VergeEnd end = vergeEnds[i];

            // Off the end of the road by half the wall's thickness, so none of it overlaps the last metres of
            // asphalt: the cap is what closes the mouth of the corridor, not what blocks the road.
            Vector3 outward = end.outward * (wallThickness * 0.5f + 0.5f);

            if (AddWall(end.inner + outward, end.point + outward, wallThickness, false, false, true, margin))
                cappedCount++;
            else
                unsealedCount++;
        }
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
