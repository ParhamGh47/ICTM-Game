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
///  - Each car is placed EXACTLY on its own waypoint and gets
///    AICarController.startingWaypoint = that waypoint's index, so it starts
///    by advancing to the NEXT waypoint and drives forward. (Without this,
///    every car targets waypoint[0] first, turns around, and rams the cars
///    behind it - the "kangaroo jump / fly off the map" bug.)
///  - All cars on a path share the SAME speed (random per direction), so they
///    keep their spacing forever and can never catch up and ram each other.
///  - Lane offset is derived from the road's own geometry (center of the
///    rightmost lane), so cars stay on the asphalt for any road width.
///  - Body paint is randomized from the palette in <see cref="PassingCarPalette"/> - the materials it
///    writes into Assets/Prefabs/Cars/Colors - plus any CarColor material already in the project. One colour
///    per car, applied to the same slots the prefab already paints. Every car prefab dropped in
///    Assets/Prefabs/Cars joins the fleet on its own; one built from a fresh model carries its body material
///    as "BODY", which is recognised here without any change to this list of prefabs.
///  - "Lights On" decides whether spawned cars drive with their headlights on:
///    the LightL/LightR spotlights are enabled and the lens material is swapped
///    to its emissive "lit" variant (206: LightOff206 -> Light206).
///
/// Usage: open a scene with a RoadArchitect road, then:
///   Tools > Road Tools > Paint Passing Cars
/// </summary>
public class PassingCarsSpawner : EditorWindow
{
    private const string ParentName = "PassingCars";

    // Waypoint spacing along the road, in meters. Dense enough that cars hug
    // the curves, sparse enough to keep object counts low.
    private const float WaypointSpacing = 15f;

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
            "All cars in a lane share one random speed; forward cars use the " +
            "lower half of the range, reverse cars the upper half.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // ---- Headlights -----------------------------------------------------
        lightsOn = EditorGUILayout.Toggle(new GUIContent("Lights On",
            "Spawned cars drive with headlights on: LightL/LightR spotlights " +
            "enabled and the lens material swapped to its emissive variant."), lightsOn);
        EditorGUILayout.LabelField(
            "Enables the LightL/LightR spotlights and the emissive lens material " +
            "(206: Light206). The 911 has no separate off-material, so it only " +
            "gets the spotlights.",
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
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        System.Random rng = new System.Random();

        shaped = 0;
        shapedSample = null;
        unpainted = 0;
        unpaintedSample = null;

        GameObject parent = FindOrCreateParent(ParentName);
        int created = 0;

        // Forward lane: shuttles out along the spline (+right side) to its turnaround.
        created += PaintDirection(parent.transform, "Path_Forward", true, +laneOffset, splineDistance, rng, turnParam);
        // Reverse lane: shuttles out against the spline (-right side) to its own turnaround.
        created += PaintDirection(parent.transform, "Path_Reverse", false, -laneOffset, splineDistance, rng, revTurnParam);

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

    int PaintDirection(Transform parent, string pathName, bool forward, float offset, float splineDistance, System.Random rng, float turnParam)
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
        // Forward cars get the lower half of the speed range, reverse cars the
        // upper half, so the oncoming lane is always at least as fast.
        float speedLo = minSpeedKPH;
        float speedHi = forward ? (minSpeedKPH + maxSpeedKPH) * 0.5f : maxSpeedKPH;
        float speedKPH = (float)(rng.NextDouble() * (speedHi - speedLo) + speedLo);

        // Spread the cars along the outbound leg (waypoints 0 .. perLeg-1).
        int perLeg = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(legEnd - legStart) * splineDistance / WaypointSpacing));
        int toPlace = Mathf.Min(carsPerDirection, perLeg);

        int created = 0;
        for (int i = 0; i < toPlace; i++)
        {
            // Sit each car exactly on its own waypoint and tell the controller
            // to start there - it advances to the NEXT waypoint and drives
            // forward with the platoon instead of turning back to waypoint[0].
            int wpIndex = Mathf.RoundToInt((float)i * (perLeg - 1) / Mathf.Max(1, toPlace - 1));
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
                controller.speedKPH = speedKPH;
                // Same feel as the manual level-1 cars.
                controller.turnSpeed = 3f;
                controller.maxSteerAngle = 150f;
                controller.reachThreshold = 3f;
            }

            ApplyRandomColor(carObj, rng);
            ApplyLightState(carObj, lightsOn);
            created++;
        }

        if (toPlace < carsPerDirection)
        {
            Debug.LogWarning($"{pathName}: only {perLeg} waypoints fit between the section start and the turnaround " +
                             $"- reduce the car count or widen the section.");
        }

        return created;
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

    // Turns the car's headlights on/off after painting. Matches the manual
    // level-1 setup: LightL/LightR spotlights plus an emissive lens material
    // when available (206 ships Light206 / LightOff206).
    void ApplyLightState(GameObject carObj, bool on)
    {
        // 1) Real spotlight components (LightL / LightR).
        var lights = carObj.GetComponentsInChildren<Light>(true);
        foreach (var l in lights)
        {
            if (l == null) continue;
            Undo.RecordObject(l, "Set Passing Car Lights");
            l.enabled = on;
        }

        // 2) Lens material with an "off" twin (206: Light206 <-> LightOff206).
        // The twin is a separate asset sitting next to the lit one, so it is
        // derived by name through the AssetDatabase instead of being searched
        // on the car (the prefab only ever carries one of the two).
        Material lens = FindLensMaterial(carObj);
        if (lens == null) return; // e.g. the 911 has no separate lens material

        string litName = lens.name.StartsWith("LightOff")
            ? "Light" + lens.name.Substring("LightOff".Length)
            : lens.name;
        string offName = "LightOff" + litName.Substring("Light".Length);
        string targetName = on ? litName : offName;

        if (lens.name == targetName) return; // already in the requested state

        Material target = null;
        string lensPath = AssetDatabase.GetAssetPath(lens);
        if (!string.IsNullOrEmpty(lensPath))
        {
            string folder = System.IO.Path.GetDirectoryName(lensPath);
            target = AssetDatabase.LoadAssetAtPath<Material>(
                System.IO.Path.Combine(folder, targetName + ".mat"));
        }
        if (target == null)
        {
            Debug.LogWarning($"No '{targetName}' material found next to '{lens.name}' - lens stays as-is.");
            return;
        }

        var renderers = carObj.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            Material[] mats = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (mats[i].name != litName && mats[i].name != offName) continue;
                if (mats[i].name == targetName) continue;

                mats[i] = target;
                changed = true;
            }

            if (changed)
            {
                Undo.RecordObject(renderer, "Set Passing Car Lights");
                renderer.sharedMaterials = mats;
            }
        }
    }

    // First headlight-lens material found on the car (206: "Light206" or
    // "LightOff206"). Null when the car has none - then only the spotlights
    // get toggled.
    Material FindLensMaterial(GameObject carObj)
    {
        var renderers = carObj.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.name.StartsWith("Light"))
                    return mat;
            }
        }
        return null;
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
        EditorUtility.ClearProgressBar();
    }
}