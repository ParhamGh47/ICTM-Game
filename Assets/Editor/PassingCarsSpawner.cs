using UnityEngine;
using UnityEditor;
using RoadArchitect;
using System.Collections.Generic;

/// <summary>
/// Editor tool that paints looping "passing cars" along a RoadArchitect road,
/// replicating the manual passing-car setup used in Level 1 (Core-1).
///
/// How it behaves:
///  - One waypoint path per direction ("Path_Forward" / "Path_Reverse") that
///    is a closed shuttle loop: drive out along one side of the road to the
///    turnaround point, cross the road, drive back along the other side, cross
///    again. Cars follow the loop forever, so none ever reaches the end of the
///    path and dies. Forward cars turn at "Turnaround %", reverse cars at
///    100 - Turnaround %.
///  - Painting REPLACES the previous batch: whatever is under "PassingCars" is
///    removed first, then the paths and the cars are built again from the road.
///    Without that, a second run would leave the first run's cars behind - and
///    since the paths they point at are rebuilt (the old objects destroyed),
///    those cars would have no path at all and would stand still forever.
///  - Each car is placed EXACTLY on its own waypoint and gets
///    AICarController.startingWaypoint = that waypoint's index, so it starts
///    by advancing to the NEXT waypoint and drives forward. (Without this,
///    every car targets waypoint[0] first, turns around, and rams the cars
///    behind it - the "kangaroo jump / fly off the map" bug.)
///  - Every car painted in one run shares the SAME speed, both directions. That
///    is what keeps the fleet's spacing: the two paths are shuttle loops that
///    drive out on one side of the road and back on the other, so both of them
///    use both lanes, and a faster direction would slowly overtake the slower
///    one in the same lane and shunt it.
///  - Cars are spaced by DISTANCE around the loop, not by waypoint index. A leg
///    is cut in even steps of the spline's own parameter, so tight bends pack
///    their waypoints closer together and the U-turns are denser still; spacing
///    by index therefore seated cars close together exactly where the waypoints
///    bunch.
///  - Lane offset is derived from the road's own geometry (center of the
///    rightmost lane), so cars stay on the asphalt for any road width.
///  - Body paint is randomized from the palette in <see cref="PassingCarPalette"/> - the materials it
///    writes into Assets/Prefabs/Cars/Colors - plus any CarColor material already in the project. One colour
///    per car, applied to the same slots the prefab already paints. Every car prefab dropped in
///    Assets/Prefabs/Cars joins the fleet on its own; one built from a fresh model carries its body material
///    as "BODY", which is recognised here without any change to this list of prefabs.
///  - "Lights On" decides whether spawned cars drive with their headlights on.
///    Nothing is baked into the scene for it: a car prefab ships its headlights
///    switched off and its lamp colour is per car, so the painter only records
///    the answer and AICarController lights each car when the level starts - the
///    spotlights and the lens, in a lamp colour of its own. See
///    <see cref="AICarController"/>'s ApplyHeadlights for why it cannot be
///    painted in.
///
/// Usage: open a scene with a RoadArchitect road, then:
///   Tools > Road Tools > Paint Passing Cars
/// </summary>
public class PassingCarsSpawner : EditorWindow
{
    private const string ParentName = "PassingCars";

    // What the window was last set to, kept in EditorPrefs so it survives a Unity restart. Painting replaces
    // the whole batch now, so a second run has to be able to reproduce the first one exactly - "how many cars
    // per lane was this?" is not a question anyone should have to answer twice.
    private const string PrefsPrefix = "ICTM.PassingCarsSpawner.";

    // Waypoint spacing along the road, in meters. Dense enough that cars hug
    // the curves, sparse enough to keep object counts low.
    private const float WaypointSpacing = 15f;

    // The closest two cars on a path may be put to each other, in meters. Above one waypoint's worth of road,
    // so no two cars are ever seated on the same waypoint, and far enough apart that a car never starts out
    // looking like it is tailgating the one in front of it.
    private const float MinCarSpacing = 16f;

    private Road targetRoad;
    private float startParam = 0.05f;
    private float endParam = 0.95f;
    private float turnParam = 0.9f; // forward cars turn here; reverse cars at 100-%
    private int carsPerDirection = 5;
    private float laneOffset = -1f; // auto-computed from the road when < 0
    private bool laneOffsetAuto = true;
    private float minSpeedKPH = 25f;
    private float maxSpeedKPH = 35f;
    private bool lightsOn = true;

    private GameObject[] carPrefabs = new GameObject[0];
    private Material[] carColors = new Material[0];

    // The colour palette as a shuffled deck, so a paint run spreads over it instead of repeating.
    private List<Material> colorDeck;
    private int colorDeckIndex;

    // How the colouring went on this run, reported once at the end rather than per car.
    private int shaped;
    private string shapedSample;
    private int unpainted;
    private string unpaintedSample;

    [MenuItem("Tools/Road Tools/Paint Passing Cars")]
    static void OpenWindow()
    {
        var window = GetWindow<PassingCarsSpawner>("Passing Cars Spawner");
        window.minSize = new Vector2(400, 520);
        window.Show();
    }

    void OnEnable()
    {
        LoadSettings();

        if (targetRoad == null)
        {
            var roads = FindObjectsOfType<Road>();
            if (roads.Length > 0)
            {
                targetRoad = roads[0];
            }
        }
        FindCarPrefabs();
        FindCarColors();
        if (targetRoad != null) laneOffset = ComputeLaneOffset(targetRoad);
    }

    void FindCarPrefabs()
    {
        List<GameObject> found = new List<GameObject>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cars" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<AICarController>() != null)
            {
                found.Add(prefab);
            }
        }
        carPrefabs = found.ToArray();
    }

    void FindCarColors()
    {
        // The palette is the pool: it is written into Assets/Prefabs/Cars/Colors the first time it is asked
        // for, and any CarColor material already sitting in the project - the original four, or one made by
        // hand - is picked up alongside it.
        List<Material> found = new List<Material>(PassingCarPalette.Ensure());
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Prefabs/Cars" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.name.StartsWith("CarColor") && !found.Contains(mat))
            {
                found.Add(mat);
            }
        }
        carColors = found.ToArray();
    }

    void OnGUI()
    {
        GUILayout.Label("Paint Passing Cars", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Above the road check on purpose: a car prefab is worth building whether or not the scene in front
        // of you happens to hold a road.
        if (GUILayout.Button("Create A Car Prefab From A Model..."))
        {
            PassingCarPrefabBuilder.OpenWindow();
        }

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        targetRoad = (Road)EditorGUILayout.ObjectField("Road", targetRoad, typeof(Road), true);
        if (EditorGUI.EndChangeCheck() && targetRoad != null)
        {
            laneOffset = ComputeLaneOffset(targetRoad);
        }

        if (targetRoad == null)
        {
            EditorGUILayout.HelpBox("Assign a Road reference.", MessageType.Warning);
            return;
        }

        if (targetRoad.spline == null)
        {
            EditorGUILayout.HelpBox("Road spline not found.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();

        if (carPrefabs.Length > 0)
        {
            EditorGUILayout.LabelField("Car Prefabs Found: " + carPrefabs.Length);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Paint Colors Found: " + carColors.Length,
                                       GUILayout.Width(EditorGUIUtility.labelWidth));
            if (GUILayout.Button("Show Colour Palette", GUILayout.Width(150)))
            {
                FindCarColors();
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(PassingCarPalette.Folder));
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("No AICarController prefabs found under Assets/Prefabs/Cars.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // ---- Amount of cars -------------------------------------------------
        EditorGUILayout.LabelField("Amount of Cars", EditorStyles.boldLabel);
        carsPerDirection = EditorGUILayout.IntSlider("Cars per Direction", carsPerDirection, 1, 30);
        EditorGUILayout.LabelField(
            $"Total: {carsPerDirection * 2} cars ({carsPerDirection} in each lane, " +
            "same speed per lane so they never catch each other)",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // ---- Placement ------------------------------------------------------
        EditorGUILayout.LabelField("Placement (road section)", EditorStyles.boldLabel);
        startParam = EditorGUILayout.Slider("Start %", startParam, 0f, 1f);
        endParam = EditorGUILayout.Slider("End %", endParam, 0f, 1f);
        turnParam = EditorGUILayout.Slider("Forward Turnaround %", turnParam, 0.55f, 0.98f);
        EditorGUILayout.LabelField(                $"Forward cars drive {startParam * 100f:F0}% -> {turnParam * 100f:F0}% on one side, " +
            $"turn around at that end, cross the road, and come back. " +
            $"Reverse cars drive {endParam * 100f:F0}% -> {(1f - turnParam) * 100f:F0}% on the other side, " +
            $"turn around at ITS end, cross back. Each direction turns around at the " +
            $"end of its OWN stretch (not the same middle point), and shuttles forever.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // ---- Lane -----------------------------------------------------------
        EditorGUILayout.LabelField("Lane", EditorStyles.boldLabel);
        float computedOffset = ComputeLaneOffset(targetRoad);
        if (laneOffsetAuto)
        {
            laneOffset = computedOffset;
            EditorGUILayout.LabelField(
                $"Lane Offset: ±{laneOffset:F1} m (auto - center of the " +
                $"rightmost lane of a {targetRoad.laneWidth:F0} m x {targetRoad.laneAmount} lane road)");
            if (GUILayout.Button("Override Lane Offset"))
            {
                laneOffsetAuto = false;
            }
        }
        else
        {
            laneOffset = EditorGUILayout.Slider("Lane Offset", laneOffset, 0.5f, Mathf.Max(1f, computedOffset * 2f));
            if (GUILayout.Button("Auto (road lanes)"))
            {
                laneOffsetAuto = true;
                laneOffset = computedOffset;
            }
        }

        EditorGUILayout.Space();

        // ---- Speed ----------------------------------------------------------
        EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel);
        minSpeedKPH = EditorGUILayout.Slider("Min Speed (KPH)", minSpeedKPH, 10f, 60f);
        maxSpeedKPH = EditorGUILayout.Slider("Max Speed (KPH)", maxSpeedKPH, minSpeedKPH, 60f);
        EditorGUILayout.LabelField(
            "One random speed for the whole run, both directions. The two paths " +
            "share both lanes, so a faster direction would overtake the slower one " +
            "in the same lane and shunt it; at one speed nothing changes relative " +
            "position at all.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // ---- Headlights -----------------------------------------------------
        lightsOn = EditorGUILayout.Toggle(new GUIContent("Lights On",
            "Spawned cars drive with their headlights on."), lightsOn);
        EditorGUILayout.LabelField(
            "Recorded per car here; AICarController switches the LightL/LightR " +
            "spotlights on and gives each car a lamp colour of its own when the " +
            "level starts.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT PASSING CARS", GUILayout.Height(40)))
        {
            PaintCars();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove All Painted Cars", GUILayout.Height(25)))
        {
            RemovePaintedCars();
        }
        GUI.backgroundColor = Color.white;
    }

    // Center of the rightmost driving lane, measured from the road centerline.
    // Right-hand traffic: forward lane on +right, reverse lane on -right.
    float ComputeLaneOffset(Road road)
    {
        if (road == null || road.laneAmount <= 0) return 2.5f;
        float lanesPerDirection = Mathf.Max(1f, road.laneAmount / 2f);
        return (lanesPerDirection - 0.5f) * road.laneWidth;
    }

    void PaintCars()
    {
        // Re-scanned here as well as when the window opens, so a car prefab dropped into the folder while the
        // window was sitting open still joins the fleet on the next paint.
        FindCarPrefabs();

        if (targetRoad == null || targetRoad.spline == null || carPrefabs.Length == 0)
        {
            Debug.LogError("Cannot paint: missing road or car prefabs");
            return;
        }

        if (endParam <= startParam)
        {
            float tmp = startParam;
            startParam = endParam;
            endParam = tmp;
        }

        // Keep the forward turnaround inside the forward stretch so both
        // directions' turnarounds sit on the road (not necessarily in the middle).
        turnParam = Mathf.Clamp(turnParam, startParam + 0.05f, endParam - 0.05f);
        // Reverse turnaround is the mirrored end of the reverse stretch.
        float revTurnParam = 1f - turnParam;

        float splineDistance = targetRoad.spline.distance;
        if (splineDistance <= 0f)
        {
            Debug.LogError("Road spline distance is 0. Make sure the road is built.");
            return;
        }

        if (laneOffsetAuto) laneOffset = ComputeLaneOffset(targetRoad);

        int total = carsPerDirection * 2;
        if (!EditorUtility.DisplayDialog("Paint Passing Cars",
            $"Place {total} passing cars along the road " +
            $"(forward lane: {startParam * 100f:F0}% -> {turnParam * 100f:F0}% then back, " +
            $"reverse lane: {endParam * 100f:F0}% -> {revTurnParam * 100f:F0}% then back, " +
            $"{laneOffset:F1} m from the centerline each way).\n\n" +
            "Each direction turns around at the END of its own stretch (not the same " +
            "point in the middle) and shuttles back and forth forever - no car gets " +
            "stuck at the end of the path.\n\n" +
            $"Whatever an earlier run left under '{ParentName}' is replaced, so painting " +
            "twice gives one batch of cars rather than two.\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        // Remembered as soon as the paint is agreed to, so a run that then fails halfway does not take the
        // settings down with it.
        SaveSettings();

        System.Random rng = new System.Random();

        shaped = 0;
        shapedSample = null;
        unpainted = 0;
        unpaintedSample = null;

        // One speed for the whole run, both directions. The two paths share both lanes - each drives out on
        // one side of the road and back on the other - so a faster direction slowly overtakes the slower one
        // in the same lane, shunts it off line and leaves a wreck behind. At one speed nothing changes
        // relative position at all: the fleet keeps the spacing it was painted with for the whole level.
        float cruiseKPH = (float)(rng.NextDouble() * (maxSpeedKPH - minSpeedKPH) + minSpeedKPH);

        GameObject parent = FindOrCreateParent(ParentName);
        ClearPreviousPaint(parent);

        int created = 0;

        // Forward lane: shuttles out along the spline (+right side) to its turnaround.
        created += PaintDirection(parent.transform, "Path_Forward", true, +laneOffset, splineDistance, rng, turnParam, cruiseKPH);
        // Reverse lane: shuttles out against the spline (-right side) to its own turnaround.
        created += PaintDirection(parent.transform, "Path_Reverse", false, -laneOffset, splineDistance, rng, revTurnParam, cruiseKPH);

        Debug.Log($"Painted {created} passing cars ({carsPerDirection} per direction)");

        // Said out loud because a car whose body cannot be identified looks exactly like a car that was
        // skipped: the number here is the one to look at when some prefab stays its own colour.
        if (shaped > 0)
        {
            Debug.Log($"{shaped} car(s) had no body material this tool knows, so the body was taken to be the " +
                      $"biggest mesh on them - e.g. {shapedSample}");
        }

        if (unpainted > 0)
        {
            Debug.LogWarning($"{unpainted} car(s) had nothing to paint at all - no visible renderer to take a " +
                             $"colour - and kept their own, e.g. {unpaintedSample}.");
        }
    }

    int PaintDirection(Transform parent, string pathName, bool forward, float offset, float splineDistance, System.Random rng, float turnParam, float cruiseKPH)
    {
        // Build a closed "shuttle" loop for THIS direction. The turnaround is at
        // the END of this direction's own stretch (some percent before the end of
        // the real road - not a shared middle point). Cars follow the loop forever,
        // so none ever reaches the end of the path.
        //   Forward:  outbound +side startParam -> turnParam,
        //             U-turn at turnParam (THE END of the forward stretch),
        //             inbound -side turnParam -> startParam.
        //   Reverse:  outbound -side endParam -> turnParam,
        //             U-turn at turnParam (THE END of the reverse stretch, opposite end of the road),
        //             inbound +side turnParam -> endParam.
        float legStart = forward ? startParam : endParam;
        float legEnd = turnParam;

        var spline = targetRoad.spline;

        // Position on the road at param t, shifted `side` meters across the
        // road from the centerline (side is the signed lane offset).
        Vector3 PointOnLane(float t, float side)
        {
            Vector3 pos, tangent;
            spline.GetSplineValueBoth(t, out pos, out tangent);
            Vector3 right = new Vector3(tangent.z, 0f, -tangent.x).normalized;
            return pos + right * side;
        }

        // Waypoints along one leg, driving from param a to param b on side s.
        // The list order IS the driving order (b may be lower than a).
        void AddLeg(List<Vector3> points, float a, float b, float s)
        {
            int n = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(b - a) * splineDistance / WaypointSpacing));
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Lerp(a, b, (float)i / (n - 1));
                points.Add(PointOnLane(t, s));
            }
        }

        // A U-turn at param t: cross the road from one side to the other,
        // bulging a few meters forward so the car sweeps a natural arc.
        void AddUTurn(List<Vector3> points, float t, float fromSide, float toSide)
        {
            float[] sides = { fromSide, fromSide * 0.6f, 0f, toSide * 0.6f, toSide };
            float[] bulge = { 0f, 3.5f, 5f, 3.5f, 0f };
            for (int k = 0; k < sides.Length; k++)
            {
                Vector3 pos, tangent;
                spline.GetSplineValueBoth(t, out pos, out tangent);
                Vector3 right = new Vector3(tangent.z, 0f, -tangent.x).normalized;
                points.Add(pos + right * sides[k] + tangent.normalized * bulge[k]);
            }
        }

        // Recreate the path so re-painting doesn't stack duplicate paths.
        GameObject pathObj = CreateOrReplacePath(parent, pathName);

        // Build the closed loop in driving order. The last U-turn ends exactly
        // where the first leg started, so the car wraps around seamlessly.
        List<Vector3> points = new List<Vector3>();
        AddLeg(points, legStart, legEnd, offset);
        AddUTurn(points, legEnd, offset, -offset);
        AddLeg(points, legEnd, legStart, -offset);
        AddUTurn(points, legStart, -offset, offset);

        Transform[] waypoints = new Transform[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            // Face the next point in the chain so spawned cars point the right way.
            Vector3 here = points[i];
            Vector3 next = points[(i + 1) % points.Count];
            Vector3 dir = next - here;
            dir.y = 0f;
            Quaternion rot = dir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(dir.normalized, Vector3.up)
                : Quaternion.identity;

            GameObject wp = new GameObject($"WP_{i:D3}");
            wp.transform.position = here;
            wp.transform.rotation = rot;
            Undo.RegisterCreatedObjectUndo(wp, "Create Passing Car Waypoint");
            Undo.SetTransformParent(wp.transform, pathObj.transform, "Create Passing Car Waypoint");
            waypoints[i] = wp.transform;
        }

        // One speed for the whole direction: cars keep their spacing forever,
        // so they never catch up and ram each other (same as the manual setup).
        // Cars are seated by DISTANCE around the whole loop, not by waypoint index. Waypoints are only evenly
        // spaced by and large: a leg is cut in even steps of the spline's own parameter, so a tight bend packs
        // them closer together, and the two U-turns are denser still (five waypoints across the road). Spacing
        // by index therefore seated cars close together exactly where the waypoints bunch. Spacing by arc
        // length puts the same distance between them everywhere - and since the whole run shares one speed,
        // that spacing is what the platoon keeps.
        float[] arcTo = new float[points.Count + 1];
        for (int i = 0; i < points.Count; i++)
            arcTo[i + 1] = arcTo[i] + Vector3.Distance(points[i], points[(i + 1) % points.Count]);

        float loopLength = arcTo[points.Count];

        // A car a second or two behind the one in front of it is traffic; two cars in the same place are a
        // pile-up. This is the closest the loop may be asked to seat them, and a run asking for more cars
        // than fit at this spacing places fewer and says so.
        int toPlace = Mathf.Min(carsPerDirection,
                                Mathf.Max(1, Mathf.FloorToInt(loopLength / MinCarSpacing)));

        int created = 0;
        for (int i = 0; i < toPlace; i++)
        {
            // Sit each car exactly on its own waypoint and tell the controller
            // to start there - it advances to the NEXT waypoint and drives
            // forward with the platoon instead of turning back to waypoint[0].
            int wpIndex = WaypointAtArc(arcTo, loopLength * i / toPlace);
            if (waypoints[wpIndex] == null) continue;

            GameObject prefab = carPrefabs[rng.Next(carPrefabs.Length)];
            GameObject carObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(carObj, "Place Passing Car");

            carObj.name = $"{prefab.name}_{(forward ? "Fwd" : "Rev")}_{i}";
            Undo.SetTransformParent(carObj.transform, parent, "Move Passing Car");
            // Spawn ~0.3 m above the road: the 206/911 prefab collider hangs
            // ~0.3 m below the root, so the car starts resting right on the
            // asphalt instead of sinking into the road mesh (which pops it).
            carObj.transform.position = waypoints[wpIndex].position + Vector3.up * 0.3f;
            carObj.transform.rotation = waypoints[wpIndex].rotation;

            AICarController controller = carObj.GetComponent<AICarController>();
            if (controller != null)
            {
                Undo.RecordObject(controller, "Configure Passing Car");
                controller.waypointsRoot = pathObj.transform;
                controller.startingWaypoint = wpIndex;
                controller.speedKPH = cruiseKPH;
                // The lights are the controller's own business: the prefab keeps them off and gives each car a
                // lamp colour of its own when the level starts, so all the painter has to say is whether they
                // are wanted. See AICarController.ApplyHeadlights.
                controller.lightsOn = lightsOn;
                // Same feel as the manual level-1 cars.
                controller.turnSpeed = 3f;
                controller.maxSteerAngle = 150f;
                controller.reachThreshold = 3f;
            }

            ApplyRandomColor(carObj, rng);
            created++;
        }

        if (toPlace < carsPerDirection)
        {
            Debug.LogWarning($"{pathName}: {loopLength:F0} m of loop does not hold {carsPerDirection} cars " +
                             $"{MinCarSpacing:F0} m apart - {toPlace} were placed. Ask for fewer cars or widen " +
                             "the section.");
        }

        return created;
    }

    /// <summary>
    /// The waypoint at or just past a distance along the loop, as an index into the path's children. The loop
    /// is only a few hundred waypoints long and the distances asked for come in order, but a scan from the
    /// front is the simplest thing that is obviously right; the trailing entry of <paramref name="arcTo"/> is
    /// the loop closing, so the index wraps back to the first waypoint there.
    /// </summary>
    static int WaypointAtArc(float[] arcTo, float target)
    {
        for (int i = 1; i < arcTo.Length; i++)
        {
            if (arcTo[i] >= target) return i % (arcTo.Length - 1);
        }

        return 0;
    }

    /// <summary>
    /// The next colour off a shuffled draw of the palette. Drawing from a shuffled deck rather than rolling
    /// each car independently means the whole palette shows up before any colour repeats, so a fleet reads as
    /// deliberately varied instead of lurching between three shades of silver.
    /// </summary>
    Material NextColor(System.Random rng)
    {
        if (carColors.Length == 0) return null;

        if (colorDeck == null || colorDeck.Count != carColors.Length || colorDeckIndex >= colorDeck.Count)
        {
            colorDeck = new List<Material>(carColors);

            for (int i = colorDeck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Material swap = colorDeck[i];
                colorDeck[i] = colorDeck[j];
                colorDeck[j] = swap;
            }

            colorDeckIndex = 0;
        }

        return colorDeck[colorDeckIndex++];
    }

    void ApplyRandomColor(GameObject carObj, System.Random rng)
    {
        // One colour for the whole car. A car body is often several meshes - the model in Objects/Cars splits
        // its own into four - so choosing per slot would hand each panel its own colour and the car would
        // come out patchwork.
        Material color = NextColor(rng);
        if (color == null) return;

        var renderers = carObj.GetComponentsInChildren<MeshRenderer>(true);
        List<MeshRenderer> visible = new List<MeshRenderer>();
        int painted = 0;

        foreach (var renderer in renderers)
        {
            // Only what is drawn counts. A car model ships leftovers that are switched off - the 911 keeps an
            // inactive copy of its own body - and recolouring one of those changes nothing on screen while
            // hiding the fact that the body itself was never touched.
            if (!renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;

            visible.Add(renderer);
            if (PaintBodySlots(renderer, color)) painted++;
        }

        // Then the shape rule, which is what makes the colour land on the CAR rather than on whatever small
        // panel happened to be named. A model can name one little part the way this tool looks for and leave
        // the shell itself named something else - the 911's shell is a Circle with two material slots and
        // nothing in its name to go on - and a name-only pass would quietly recolour the little part and
        // leave the car looking untouched. The biggest visible mesh on a car is its body shell, and its first
        // slot is the paint. On a car the names already painted this finds the body already wearing the
        // colour and does nothing, so nothing previously working changes.
        if (PaintBodyByShape(visible, color, out string shapeNote))
        {
            shapedSample = shapedSample == null ? carObj.name + " -> " + shapeNote : shapedSample;
            shaped++;
            return;
        }

        if (painted > 0) return;

        unpaintedSample = unpaintedSample == null ? carObj.name : unpaintedSample;
        unpainted++;
    }

    /// <summary>Repaints every slot of one renderer that is body paint. Returns whether anything changed.</summary>
    bool PaintBodySlots(MeshRenderer renderer, Material color)
    {
        Material[] mats = renderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < mats.Length; i++)
        {
            Material mat = mats[i];
            if (mat == null || !IsPaintable(mat)) continue;
            if (color == mat) continue;

            mats[i] = color;
            changed = true;
        }

        if (!changed) return false;

        Undo.RecordObject(renderer, "Recolor Passing Car");
        renderer.sharedMaterials = mats;
        return true;
    }

    /// <summary>
    /// Finds the car's body by its shape: the biggest visible renderer whose first slot is not glass, a lens
    /// or lights is taken to be the shell, and its first slot is painted. Only the first slot, because that
    /// is the one the body paint sits in - the rest of the array is the glass, the chrome and whatever else
    /// the model packs onto the same mesh. Returns false when there is no shell or when it is already wearing
    /// the colour, which is the case on a car whose body the material names found.
    /// </summary>
    bool PaintBodyByShape(List<MeshRenderer> visible, Material color, out string note)
    {
        MeshRenderer body = null;
        float biggest = 0f;

        foreach (MeshRenderer renderer in visible)
        {
            Material[] mats = renderer.sharedMaterials;
            if (mats.Length == 0 || mats[0] == null) continue;
            if (IsNeverBody(mats[0])) continue;

            Vector3 size = renderer.bounds.size;
            float volume = size.x * size.y * size.z;
            if (volume <= biggest) continue;

            biggest = volume;
            body = renderer;
        }

        // No shell to speak of, or the body is already wearing the colour because the names found it.
        if (body == null || body.sharedMaterials[0] == color)
        {
            note = null;
            return false;
        }

        Material[] materials = body.sharedMaterials;
        note = $"'{body.name}' slot 0 (was '{materials[0].name}')";

        materials[0] = color;

        Undo.RecordObject(body, "Recolor Passing Car");
        body.sharedMaterials = materials;
        return true;
    }

    // The names a car model gives its body paint, which is the only thing on a car
    // that gets recoloured. The 206 prefab paints its body with CarColor.mat, the
    // 911 uses "Material.005 1", and the car model (Objects/Cars/car.fbx) calls
    // its body material "BODY". Windows, lights, wheels and the engine keep their
    // own materials - they are not paint.
    private static readonly string[] BodyMaterialNames =
    {
        "CarColor",
        "Material.005",
        "BODY",
    };

    // Never the body, however big the mesh turns out to be.
    private static readonly string[] NeverBodyNames =
    {
        "glass",
        "window",
        "windscreen",
        "windshield",
        "light",
        "lamp",
        "siren",
    };

    bool IsPaintable(Material mat)
    {
        for (int i = 0; i < BodyMaterialNames.Length; i++)
        {
            if (mat.name.StartsWith(BodyMaterialNames[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    bool IsNeverBody(Material mat)
    {
        for (int i = 0; i < NeverBodyNames.Length; i++)
        {
            if (mat.name.IndexOf(NeverBodyNames[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // Lights are the one thing this tool deliberately does NOT paint into the scene. A car prefab ships its
    // headlights switched off and its lamp colour is picked per car, so a colour baked into the scene is a
    // colour that is either wrong (every car the same) or invisible (a material instance the scene cannot
    // hold). AICarController lights each car when the level starts instead - see its ApplyHeadlights - and
    // what is painted here is only the answer to "with the lights on or off", which is per car and does
    // persist.

    /// <summary>
    /// Removes whatever the last paint run left under the parent, cars and paths alike.
    ///
    /// Painting rebuilds the waypoint paths every time, which means the objects parked cars were pointing at
    /// no longer exist afterwards - and a car with no path cannot be driven, so it stands where it was put for
    /// the rest of the level. That is what happened to a level that had been painted twice: 120 cars, 60 of
    /// them pointing at nothing and standing still on the road. Clearing the batch first is what makes
    /// painting repeatable - run it ten times and there are still only the cars it was asked for, all moving.
    /// </summary>
    void ClearPreviousPaint(GameObject parent)
    {
        int removed = 0;

        // Backwards: the child list shifts as each one goes.
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(parent.transform.GetChild(i).gameObject);
            removed++;
        }

        if (removed > 0)
        {
            Debug.Log($"Replaced the previous paint run: {removed} object(s) - the paths and the cars " +
                      "built from them - removed before painting the new ones.");
        }
    }

    GameObject FindOrCreateParent(string parentName)
    {
        GameObject existing = GameObject.Find(parentName);
        if (existing != null) return existing;

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create PassingCars Parent");
        return parent;
    }

    GameObject CreateOrReplacePath(Transform parent, string pathName)
    {
        Transform existing = parent.Find(pathName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject pathObj = new GameObject(pathName);
        Undo.RegisterCreatedObjectUndo(pathObj, "Create Passing Car Path");
        Undo.SetTransformParent(pathObj.transform, parent, "Create Passing Car Path");
        return pathObj;
    }

    void RemovePaintedCars()
    {
        GameObject parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            Debug.Log("No painted passing cars to remove.");
            return;
        }

        if (EditorUtility.DisplayDialog("Remove Painted Cars",
            $"Remove the whole '{ParentName}' hierarchy from the scene?", "Remove", "Cancel"))
        {
            Undo.DestroyObjectImmediate(parent);
        }
    }

    void OnDisable()
    {
        SaveSettings();
        EditorUtility.ClearProgressBar();
    }

    void LoadSettings()
    {
        startParam = EditorPrefs.GetFloat(PrefsPrefix + "startParam", startParam);
        endParam = EditorPrefs.GetFloat(PrefsPrefix + "endParam", endParam);
        turnParam = EditorPrefs.GetFloat(PrefsPrefix + "turnParam", turnParam);
        carsPerDirection = EditorPrefs.GetInt(PrefsPrefix + "carsPerDirection", carsPerDirection);
        laneOffsetAuto = EditorPrefs.GetBool(PrefsPrefix + "laneOffsetAuto", laneOffsetAuto);
        laneOffset = EditorPrefs.GetFloat(PrefsPrefix + "laneOffset", laneOffset);
        minSpeedKPH = EditorPrefs.GetFloat(PrefsPrefix + "minSpeedKPH", minSpeedKPH);
        maxSpeedKPH = EditorPrefs.GetFloat(PrefsPrefix + "maxSpeedKPH", maxSpeedKPH);
        lightsOn = EditorPrefs.GetBool(PrefsPrefix + "lightsOn", lightsOn);
    }

    void SaveSettings()
    {
        EditorPrefs.SetFloat(PrefsPrefix + "startParam", startParam);
        EditorPrefs.SetFloat(PrefsPrefix + "endParam", endParam);
        EditorPrefs.SetFloat(PrefsPrefix + "turnParam", turnParam);
        EditorPrefs.SetInt(PrefsPrefix + "carsPerDirection", carsPerDirection);
        EditorPrefs.SetBool(PrefsPrefix + "laneOffsetAuto", laneOffsetAuto);
        EditorPrefs.SetFloat(PrefsPrefix + "laneOffset", laneOffset);
        EditorPrefs.SetFloat(PrefsPrefix + "minSpeedKPH", minSpeedKPH);
        EditorPrefs.SetFloat(PrefsPrefix + "maxSpeedKPH", maxSpeedKPH);
        EditorPrefs.SetBool(PrefsPrefix + "lightsOn", lightsOn);
    }
}