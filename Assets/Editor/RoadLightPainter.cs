using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using RoadArchitect;

/// <summary>
/// Editor tool that automatically places road lights along the sides of the road,
/// matching how Core-1 lays them out by hand.
///
/// Why it fixes the prefabs instead of just using them:
///  - 'Light Off.prefab' carries a stale world position in its root transform,
///    which every hand placement in Core-1 inherits. 'Light.prefab' has its
///    point light sitting at the pole's BASE instead of at the lamp head.
///  - The root's -90 deg X rotation is CORRECT (that is what stands the pole
///    up), so it is never touched. The tool only zeroes stale root positions
///    and restores the light child to the lamp head, then places instances
///    with a clean pivot: the pole's ground position.
///
/// Placement model:
///  - lights are placed in stations along the route; every station puts one light
///    on each side, shoulder + pad metres past the asphalt edge
///  - the right row is shifted along the road by 'stagger' of the spacing
///    (0.5 makes the two rows zig-zag, like a typical street layout)
///  - the lamp arm reaches across the road, so a station reads as one gate
///    (Core-1's 'Double' pair)
///  - on curves the pole is pulled slightly toward the road on the outside of the
///    curve so the lamp still reaches over the asphalt
///
/// Usage: open a level scene (Core-1 ... Core-4) and then
///   Tools > Road Tools > Paint Road Lights
/// </summary>
public class RoadLightPainter : EditorWindow
{
    private const string OnPrefabPath = "Assets/Prefabs/Signs/Lights/Light.prefab";
    private const string OffPrefabPath = "Assets/Prefabs/Signs/Lights/Light Off.prefab";
    private const string DefaultParentName = "Road Lights";

    // Measured on the TrafficLight.fbx the prefabs use. The prefab root carries a
    // -90 deg X rotation (that is what stands the pole up), so in the root's local
    // frame the pole runs along +Z: 9.55 m tall, arm reaching 2.31 m sideways.
    // These match the authored 'Light Off' light child position.
    private const float PoleHeight = 9.55f;
    private const float ArmReach = 2.31f;
    private static readonly Vector3 LightChildLocalPosition =
        new Vector3(0f, 2.3137102f, 9.553313f);

    // The model's long axis is its local +Z (the colliders wrap z 6..10), so the
    // root's -90 deg X rotation is what stands the pole up. Canonical upright pose.
    private static readonly Quaternion UprightRootRotation =
        Quaternion.Euler(-90f, 0f, 0f);

    [Serializable]
    private class RoadEntry
    {
        public Road road;
        public bool reverse;
    }

    private class PlanItem
    {
        public Vector3 position;   // ground position of the pole
        public float yaw;          // world yaw the instance should face
        public bool rightSide;
    }

    private class PathSegment
    {
        public Road road;
        public bool reverse;
        public float start;
        public float length;
        public float End { get { return start + length; } }
    }

    private readonly List<RoadEntry> roads = new List<RoadEntry>();

    private string parentName = DefaultParentName;
    private float spacing = 40f;
    private float shoulderPad = 0.65f;
    private float stagger = 0.5f;
    private bool lightsOn = false;
    private bool trimEnds = true;
    private bool fixPrefabs = true;
    private bool armsFaceRoad = true;
    private bool showPreview = true;
    private float leanAtCurves = 0.85f;

    private Vector2 scroll;
    private List<PlanItem> cachedPlan;
    private bool planDirty = true;

    [MenuItem("Tools/Road Tools/Paint Road Lights")]
    static void OpenWindow()
    {
        RoadLightPainter window = GetWindow<RoadLightPainter>("Road Light Painter");
        window.minSize = new Vector2(430, 620);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;
        if (roads.Count == 0) FindRoadsInScene();
        planDirty = true;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged -= OnActiveSceneChanged;
        EditorUtility.ClearProgressBar();
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        roads.Clear();
        FindRoadsInScene();
        cachedPlan = null;
        planDirty = true;
        Repaint();
    }

    // ---------------------------------------------------------------- GUI ---

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Road Lights", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Places 'Light' / 'Light Off' prefab instances along both sides of every road " +
            "in the list. Stale prefab root offsets are zeroed and the 'Light' variant's " +
            "base-sitting point light is restored to the lamp head first, so instances " +
            "sit pole-base-down on the shoulder with the arm reaching over the road.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Roads", EditorStyles.boldLabel);
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
            if (GUILayout.Button("^", GUILayout.Width(24f))) MoveRoad(i, -1);
            GUI.enabled = i < roads.Count - 1;
            if (GUILayout.Button("v", GUILayout.Width(24f))) MoveRoad(i, 1);
            GUI.enabled = true;
            if (GUILayout.Button("x", GUILayout.Width(24f))) removeIndex = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeIndex >= 0) roads.RemoveAt(removeIndex);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Road Slot")) roads.Add(new RoadEntry());
        if (GUILayout.Button("Find Roads in Scene")) FindRoadsInScene();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        spacing = EditorGUILayout.Slider(
            new GUIContent("Spacing (m)", "Distance between light stations along the road"),
            spacing, 5f, 200f);
        shoulderPad = EditorGUILayout.Slider(
            new GUIContent("Past Road Edge (m)", "How far past the asphalt edge the poles sit"),
            shoulderPad, 0f, 5f);
        leanAtCurves = EditorGUILayout.Slider(
            new GUIContent("Curve Lean (m)", "How far a pole on the outside of a curve is pulled toward the road"),
            leanAtCurves, 0f, 3f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stagger & Facing", EditorStyles.boldLabel);
        stagger = EditorGUILayout.Slider(
            new GUIContent("Stagger (of spacing)", "How far the right row is shifted along the road (0.5 = half a spacing, zig-zag)"),
            stagger, 0f, 1f);
        EditorGUILayout.LabelField(
            "Unity composes the root as Ry(yaw) * Rx(-90), so the pole stands up for any " +
            "yaw and the arm offset points (-sin yaw, 0, -cos yaw).",
            EditorStyles.miniLabel);
        armsFaceRoad = EditorGUILayout.Toggle(
            new GUIContent("Arms Face Road", "Point the 2.31 m lamp offset toward the road (untick to flip both rows outward)"),
            armsFaceRoad);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lights", EditorStyles.boldLabel);
        lightsOn = EditorGUILayout.Toggle(
            new GUIContent("Lights On", "Use the lit 'Light' variant instead of the dark 'Light Off'"),
            lightsOn);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        parentName = EditorGUILayout.TextField("Root Object Name", parentName);
        trimEnds = EditorGUILayout.Toggle(
            new GUIContent("Trim First & Last", "Skip the first and last stations so lights do not sit right at the route ends"),
            trimEnds);
        fixPrefabs = EditorGUILayout.Toggle(
            new GUIContent("Fix Prefab Offsets", "One-time fix of the two light prefabs (zero stale root position, restore the upright root rotation, light back at the lamp head)"),
            fixPrefabs);
        showPreview = EditorGUILayout.Toggle(
            new GUIContent("Scene Preview", "Draw the planned lights in the scene view"),
            showPreview);

        if (EditorGUI.EndChangeCheck()) planDirty = true;

        EditorGUILayout.Space();
        DrawSummary();

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT ROAD LIGHTS", GUILayout.Height(40f))) Paint();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove Painted Lights", GUILayout.Height(25f))) RemoveAll();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void MoveRoad(int index, int delta)
    {
        int target = index + delta;
        if (target < 0 || target >= roads.Count) return;
        RoadEntry tmp = roads[target];
        roads[target] = roads[index];
        roads[index] = tmp;
    }

    private void DrawSummary()
    {
        List<PathSegment> segments = BuildSegments();
        float total = TotalLength(segments);

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  Roads: " + segments.Count + "   Route length: " + total.ToString("F1") + " m");

        if (total <= 0f)
        {
            EditorGUILayout.HelpBox(
                "No usable roads. Add at least one RoadArchitect road (with a built spline).",
                MessageType.Warning);
            return;
        }

        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing to place - lower the spacing.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField(
            "  Lights: " + plan.Count + "   Station spacing ~" + spacing.ToString("F0") + " m");
        EditorGUILayout.LabelField(
            "  Root object: '" + parentName + "' (replaced on every paint)");
    }

    // ------------------------------------------------------------- route ---

    private List<PathSegment> BuildSegments()
    {
        List<PathSegment> segments = new List<PathSegment>();
        float acc = 0f;
        foreach (RoadEntry entry in roads)
        {
            if (entry == null || entry.road == null || entry.road.spline == null) continue;
            float length = entry.road.spline.distance;
            if (length <= 0.01f) continue;
            PathSegment segment = new PathSegment();
            segment.road = entry.road;
            segment.reverse = entry.reverse;
            segment.start = acc;
            segment.length = length;
            segments.Add(segment);
            acc += length;
        }
        return segments;
    }

    private static float TotalLength(List<PathSegment> segments)
    {
        if (segments.Count == 0) return 0f;
        return segments[segments.Count - 1].End;
    }

    /// <summary>
    /// World position + driving tangent at a cumulative distance along the route.
    /// </summary>
    private static PathSegment SampleAt(List<PathSegment> segments, float distance,
                                        out Vector3 position, out Vector3 tangent)
    {
        position = Vector3.zero;
        tangent = Vector3.forward;
        if (segments.Count == 0) return null;

        PathSegment chosen = segments[segments.Count - 1];
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
        {
            localParam = Mathf.Clamp01(spline.TranslateDistBasedToParam(localDist));
        }

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

    /// <summary>Signed turn rate (deg per metre) around a route distance.</summary>
    private static float TurnRateAt(List<PathSegment> segments, float distance)
    {
        Vector3 pos; Vector3 tan;
        PathSegment segment = SampleAt(segments, distance - 2f, out pos, out tan);
        if (segment == null) return 0f;

        Vector3 beforePos; Vector3 beforeTan;
        SampleAt(segments, Mathf.Max(0f, distance - 2f), out beforePos, out beforeTan);
        SampleAt(segments, Mathf.Min(TotalLength(segments), distance + 2f), out pos, out tan);

        beforeTan.y = 0f; tan.y = 0f;
        if (beforeTan.sqrMagnitude < 0.0001f || tan.sqrMagnitude < 0.0001f) return 0f;
        beforeTan.Normalize(); tan.Normalize();
        return Vector3.SignedAngle(beforeTan, tan, Vector3.up) / 4f;
    }

    // -------------------------------------------------------------- plan ---

    private List<PlanItem> GetPlan()
    {
        if (planDirty || cachedPlan == null)
        {
            cachedPlan = BuildPlan();
            planDirty = false;
        }
        return cachedPlan;
    }

    private List<PlanItem> BuildPlan()
    {
        List<PathSegment> segments = BuildSegments();
        float total = TotalLength(segments);
        if (total <= 0f) return null;

        float step = Mathf.Max(1f, spacing);
        int count = Mathf.Max(1, Mathf.RoundToInt(total / step));
        float stationSpacing = total / count;

        List<PlanItem> plan = new List<PlanItem>();

        for (int i = 0; i <= count; i++)
        {
            if (trimEnds && (i == 0 || i == count)) continue;

            float station = i * stationSpacing;

            // Right row shifted along the road, left row on the station.
            AddSideLight(plan, segments, total, station, false);
            AddSideLight(plan, segments, total, station + stagger * stationSpacing, true);
        }

        return plan;
    }

    private void AddSideLight(List<PlanItem> plan, List<PathSegment> segments,
                              float total, float distance, bool rightSide)
    {
        if (distance < 0f || distance > total) return;

        Vector3 position;
        Vector3 tangent;
        PathSegment segment = SampleAt(segments, distance, out position, out tangent);
        if (segment == null) return;

        // Asphalt half width + shoulder + pad. RoadWidth() is the FULL asphalt
        // width (CheckpointPainter spans the whole road with it).
        float lateral = segment.road.RoadWidth() * 0.5f
                        + segment.road.shoulderWidth
                        + shoulderPad;

        // Pull the pole toward the road when it stands on the outside of a curve.
        float turn = TurnRateAt(segments, distance); // + = turning right
        bool outside = rightSide ? turn < -0.5f : turn > 0.5f;
        if (outside)
        {
            float pull = Mathf.Min(leanAtCurves, Mathf.Abs(turn) * leanAtCurves / 6f);
            lateral -= pull;
        }

        // Right of the driving direction (left when !rightSide); points AWAY from
        // the road, toward the pole.
        Vector3 across = new Vector3(tangent.z, 0f, -tangent.x);
        if (!rightSide) across = -across;

        // Instances keep the prefab's -90 deg X (that stands the pole up) and get
        // a yaw so the lamp offset (2.31 m along the model's local +Y) points back
        // ACROSS the road. Unity composes Euler(-90, yaw, 0) as Ry(yaw) * Rx(-90),
        // so the arm's horizontal reach is (-sin yaw, -cos yaw); yaw = Atan2(across)
        // makes it exactly -across: back toward the road. A road along Z gives
        // +90 deg (pole on the +X side) / -90 deg (pole on the -X side) - the set
        // of yaws Core-1's 'Double' pair uses.
        float yawDeg = armsFaceRoad
            ? Mathf.Atan2(across.x, across.z) * Mathf.Rad2Deg
            : Mathf.Atan2(-across.x, -across.z) * Mathf.Rad2Deg;

        PlanItem item = new PlanItem();
        item.position = position + across * lateral;
        item.yaw = yawDeg;
        item.rightSide = rightSide;
        plan.Add(item);
    }

    // ------------------------------------------------------------- paint ---

    private void Paint()
    {
        string prefabPath = lightsOn ? OnPrefabPath : OffPrefabPath;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("Road Light Painter: could not load '" + prefabPath + "'.");
            return;
        }

        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            Debug.LogError("Road Light Painter: nothing to place (no roads or zero spacing).");
            return;
        }

        if (fixPrefabs) FixPrefabPivots();

        if (!EditorUtility.DisplayDialog("Paint Road Lights",
            "Place " + plan.Count + " road lights (stations every ~" + spacing.ToString("F0") +
            " m, " + (lightsOn ? "lit" : "dark") + " variant)?\n\n" +
            "An existing root object named '" + parentName + "' will be replaced.\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        RemoveAllSilently();

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Road Lights Root");

        int placed = 0;
        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                PlanItem item = plan[i];
                EditorUtility.DisplayProgressBar("Painting Road Lights",
                    (i + 1) + " / " + plan.Count, (float)i / plan.Count);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Create Road Light");

                instance.name = "Light " + (i + 1) + (item.rightSide ? " (R)" : " (L)");
                instance.transform.position = item.position;
                instance.transform.rotation = Quaternion.Euler(-90f, item.yaw, 0f);
                placed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        RoadLightEditorSceneBridge.MarkSceneDirty();
        Debug.Log("Road Light Painter: placed " + placed + " road lights (" +
                  (lightsOn ? "lit" : "dark") + " variant).");
    }

    private void RemoveAll()
    {
        GameObject existing = FindRootByName(parentName);
        if (existing == null)
        {
            Debug.Log("Road Light Painter: no root object named '" + parentName + "'.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Road Lights",
            "Remove the whole '" + parentName + "' hierarchy?", "Remove", "Cancel"))
        {
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        RoadLightEditorSceneBridge.MarkSceneDirty();
        Debug.Log("Road Light Painter: removed '" + parentName + "'.");
    }

    private void RemoveAllSilently()
    {
        GameObject existing = FindRootByName(parentName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
    }

    /// <summary>
    /// Only matches ROOT objects, never children - so the artists' own
    /// 'Environments/Lights' hierarchy can never be touched by accident.
    /// </summary>
    private static GameObject FindRootByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == name) return root;
        }
        return null;
    }

    // ----------------------------------------------------- prefab repair ---

    private void FixPrefabPivots()
    {
        FixOnePrefab(OffPrefabPath, false);
        FixOnePrefab(OnPrefabPath, true);
    }

    private void FixOnePrefab(string path, bool isOnVariant)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (root == null)
        {
            Debug.LogWarning("Road Light Painter: missing prefab '" + path + "'.");
            return;
        }

        // The 'Light Off' light child position is the authored, correct one: the
        // lamp head at the arm tip. The 'Light' (on) variant has it sitting at
        // the pole base instead - restore it. The root's -90 deg X rotation is
        // what stands the pole up (the model's long axis is its local +Z), so it
        // is normalized to the canonical upright pose; only the stale root
        // position is junk.
        Transform lightChild = root.transform.Find("Light");
        if (lightChild != null)
        {
            lightChild.localPosition = LightChildLocalPosition;

            UnityEngine.Light lightComponent = lightChild.GetComponent<UnityEngine.Light>();
            if (lightComponent != null) lightComponent.enabled = isOnVariant;
        }

        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = UprightRootRotation;

        PrefabUtility.SavePrefabAsset(root);
        Debug.Log("Road Light Painter: fixed pivot + light position in '" + path + "'.");
    }

    private void FindRoadsInScene()
    {
        Road[] found = FindObjectsOfType<Road>();
        List<RoadEntry> entries = new List<RoadEntry>();
        foreach (Road road in found)
        {
            if (road == null || road.spline == null) continue;
            RoadEntry entry = new RoadEntry();
            entry.road = road;
            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            Debug.LogWarning("Road Light Painter: no RoadArchitect roads found in this scene.");
            return;
        }

        roads.Clear();
        roads.AddRange(entries);
        cachedPlan = null;
        planDirty = true;
    }

    // -------------------------------------------------------- scene view ---

    private void OnSceneGUI(SceneView view)
    {
        if (!showPreview) return;

        List<PathSegment> segments = BuildSegments();
        float total = TotalLength(segments);
        if (total <= 0f) return;

        List<PlanItem> plan = GetPlan();
        if (plan == null) return;

        for (int i = 0; i < plan.Count; i++)
        {
            PlanItem item = plan[i];
            Handles.color = item.rightSide
                ? new Color(1f, 0.85f, 0.25f, 0.9f)
                : new Color(0.4f, 0.8f, 1f, 0.9f);

            float size = HandleUtility.GetHandleSize(item.position) * 0.6f;
            Handles.DrawSolidDisc(item.position + Vector3.up * 0.05f, Vector3.up, size * 0.35f);

            // Pole stick: the root's -90 deg X makes the model's local +Z world
            // up, so the pole stands PoleHeight tall.
            Vector3 top = item.position + Vector3.up * PoleHeight;
            Handles.DrawAAPolyLine(3f, item.position, top);

            // Lamp offset reaching back over the road (model local +Y -> world
            // (-sin yaw, 0, -cos yaw) under Ry(yaw) * Rx(-90)).
            float yawRad = item.yaw * Mathf.Deg2Rad;
            Vector3 reach = new Vector3(-Mathf.Sin(yawRad), 0f, -Mathf.Cos(yawRad));
            Vector3 head = item.position + reach * ArmReach + Vector3.up * PoleHeight;
            Handles.DrawAAPolyLine(2f, top, head);
            Handles.SphereHandleCap(0, head, Quaternion.identity, size * 0.25f, EventType.Repaint);
        }
    }
}

/// <summary>Small wrapper so the tool does not need a GameView dependency.</summary>
internal static class RoadLightEditorSceneBridge
{
    public static void MarkSceneDirty()
    {
        if (SceneManager.GetActiveScene().IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
