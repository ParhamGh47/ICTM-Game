using UnityEngine;
using UnityEditor;
using RoadArchitect;
using System.Collections.Generic;

/// <summary>
/// Editor tool that paints looping "passing cars" along a RoadArchitect road,
/// replicating the manual passing-car setup used in Level 1 (Core-1).
///
/// How it behaves:
///  - One shared waypoint path per direction ("Path_Forward" / "Path_Reverse"),
///    following the FULL road spline loop (param 0 -> 1). Because the path
///    covers the whole loop, cars wrap around along the road itself instead of
///    cutting a straight line across a gap.
///  - Each car is placed EXACTLY on its own waypoint and gets
///    AICarController.startingWaypoint = that waypoint's index, so it starts
///    by advancing to the NEXT waypoint and drives forward. (Without this,
///    every car targets waypoint[0] first, turns around, and rams the cars
///    behind it - the "kangaroo jump / fly off the map" bug.)
///  - All cars on a path share the SAME speed (random per direction), so they
///    keep their spacing forever and can never catch up and ram each other.
///  - Lane offset is derived from the road's own geometry (center of the
///    rightmost lane), so cars stay on the asphalt for any road width.
///  - Body paint is randomized from the CarColor materials found under
///    Assets/Prefabs/Cars (applied to the same slots the prefab already paints).
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
    private int carsPerDirection = 5;
    private float laneOffset = -1f; // auto-computed from the road when < 0
    private bool laneOffsetAuto = true;
    private float minSpeedKPH = 25f;
    private float maxSpeedKPH = 35f;
    private bool lightsOn = true;

    private GameObject[] carPrefabs = new GameObject[0];
    private Material[] carColors = new Material[0];

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
        List<Material> found = new List<Material>();
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Prefabs/Cars" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.name.StartsWith("CarColor"))
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
            EditorGUILayout.LabelField("Paint Colors Found: " + carColors.Length);
        }
        else
        {
            EditorGUILayout.HelpBox("No AICarController prefabs found under Assets/Prefabs/Cars.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // ---- Amount of cars -------------------------------------------------
        EditorGUILayout.LabelField("Amount of Cars", EditorStyles.boldLabel);
        carsPerDirection = EditorGUILayout.IntSlider("Cars per Direction", carsPerDirection, 1, 20);
        EditorGUILayout.LabelField(
            $"Total: {carsPerDirection * 2} cars ({carsPerDirection} in each lane, " +
            "same speed per lane so they never catch each other)",
            EditorStyles.miniLabel);

        EditorGUILayout.Space();

        // ---- Placement ------------------------------------------------------
        EditorGUILayout.LabelField("Placement (road loop)", EditorStyles.boldLabel);
        startParam = EditorGUILayout.Slider("Start %", startParam, 0f, 1f);
        endParam = EditorGUILayout.Slider("End %", endParam, 0f, 1f);
        EditorGUILayout.LabelField(
            "Cars are spread between Start % and End % of the loop. The waypoint " +
            "path always covers the whole loop so cars never drive off the road.",
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
            "All cars in a lane share one random speed in this range.",
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
            $"(spread from {startParam * 100f:F0}% to {endParam * 100f:F0}% of the loop, " +
            $"{laneOffset:F1} m from the centerline each way).\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        System.Random rng = new System.Random();

        GameObject parent = FindOrCreateParent(ParentName);
        int created = 0;

        // Forward lane: drives around the loop in spline direction on the +right side.
        created += PaintDirection(parent.transform, "Path_Forward", true, +laneOffset, splineDistance, rng);
        // Reverse lane: drives around the loop against spline direction on the -right side.
        created += PaintDirection(parent.transform, "Path_Reverse", false, -laneOffset, splineDistance, rng);

        Debug.Log($"Painted {created} passing cars ({carsPerDirection} per direction)");
    }

    int PaintDirection(Transform parent, string pathName, bool forward, float offset, float splineDistance, System.Random rng)
    {
        // Waypoints span the FULL loop (param 0 -> 1) so the path wraps around
        // along the road. A path that stops at End % would force the last car
        // to cut a straight line back to Start % across the loop.
        int numWaypoints = Mathf.Max(12, Mathf.CeilToInt(splineDistance / WaypointSpacing));

        // Recreate the path so re-painting doesn't stack duplicate paths.
        GameObject pathObj = CreateOrReplacePath(parent, pathName);
        Transform[] waypoints = new Transform[numWaypoints];

        var spline = targetRoad.spline;

        for (int i = 0; i < numWaypoints; i++)
        {
            // "forward" paths run param 0->1, "reverse" paths run param 1->0,
            // so waypoint i+1 is always the NEXT point in driving order.
            float t = (float)i / (numWaypoints - 1);
            float param = forward ? t : (1f - t);

            Vector3 pos, tangent;
            spline.GetSplineValueBoth(param, out pos, out tangent);

            if (tangent == Vector3.zero) continue;

            // Right vector of the car's travel direction, flattened to XZ.
            Vector3 right = new Vector3(tangent.z, 0f, -tangent.x).normalized;

            GameObject wp = new GameObject($"WP_{i:D3}");
            wp.transform.position = pos + right * offset;
            wp.transform.rotation = Quaternion.LookRotation(
                (forward ? tangent.normalized : -tangent.normalized),
                Vector3.up);
            Undo.RegisterCreatedObjectUndo(wp, "Create Passing Car Waypoint");
            Undo.SetTransformParent(wp.transform, pathObj.transform, "Create Passing Car Waypoint");
            waypoints[i] = wp.transform;
        }

        // One speed for the whole direction: cars keep their spacing forever,
        // so they never catch up and ram each other (same as the manual setup).
        float speedKPH = (float)(rng.NextDouble() * (maxSpeedKPH - minSpeedKPH) + minSpeedKPH);

        // Section of the loop the cars are spread across. Waypoint index i on
        // the reverse path sits at param 1 - i/(n-1), so mirror the section.
        float secStart = forward ? startParam : (1f - endParam);
        float secEnd = forward ? endParam : (1f - startParam);

        int firstWp = Mathf.RoundToInt(secStart * (numWaypoints - 1));
        int lastWp = Mathf.Max(firstWp + 1, Mathf.RoundToInt(secEnd * (numWaypoints - 1)));
        int span = lastWp - firstWp;

        int toPlace = Mathf.Min(carsPerDirection, span + 1);

        int created = 0;
        for (int i = 0; i < toPlace; i++)
        {
            // Sit each car exactly on its own waypoint and tell the controller
            // to start there - it advances to the NEXT waypoint and drives
            // forward with the platoon instead of turning back to waypoint[0].
            int wpIndex = firstWp + Mathf.RoundToInt((float)i * span / Mathf.Max(1, toPlace - 1));
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
            Debug.LogWarning($"{pathName}: only {toPlace} waypoints fit in the Start %-End % section " +
                             $"- reduce the car count or widen the section.");
        }

        return created;
    }

    void ApplyRandomColor(GameObject carObj, System.Random rng)
    {
        if (carColors.Length == 0) return;

        var renderers = carObj.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            Material[] mats = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null || !IsPaintable(mat)) continue;

                Material color = carColors[rng.Next(carColors.Length)];
                if (color == mat) continue;

                mats[i] = color;
                changed = true;
            }

            if (changed)
            {
                Undo.RecordObject(renderer, "Recolor Passing Car");
                renderer.sharedMaterials = mats;
            }
        }
    }

    // The 206 prefab paints its body with CarColor.mat; the 911 uses
    // "Material.005 1". Swap those paint slots for a random CarColor material
    // and leave windows/lights/smoke untouched.
    bool IsPaintable(Material mat)
    {
        return mat.name.StartsWith("CarColor") || mat.name.StartsWith("Material.005");
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