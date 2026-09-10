using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using RoadArchitect;

/// <summary>
/// Editor tool that automatically paints the invisible progress checkpoints used
/// by the levels (the same setup that was hand-placed in levels 1-3).
///
/// What it places:
///  - One instance of Assets/Prefabs/Triggers/Checkpoint.prefab every N percent
///    (default 5%) of the drivable route, from the start of the route up to the
///    finish.
///  - Each checkpoint counts its own CUMULATIVE progress (5, 10, 15 ... 100)
///    through its ProgressTracker, exactly like the hand-placed ones, so the
///    on-screen percentage only ever moves forwards.
///  - The scene's CheckpointIndicator ("compass") is wired up with the freshly
///    placed checkpoints IN ORDER, so its arrow always points at the next one.
///  - The collider is invisible (the prefab's MeshRenderer is disabled) and
///    deliberately huge: it spans the full road width plus a wide margin on both
///    sides and is very tall, so the player can never slip past it. Its
///    thickness along the road is clamped below the distance between
///    neighbouring checkpoints so two of them never trigger at the same time.
///
/// Multiple roads:
///  Some levels are built from more than one RoadArchitect road, where the end
///  of one road is the beginning of the next one (level 4 uses several). Put
///  every road in the list below IN DRIVING ORDER and tick "Reverse" for a road
///  that is driven from its end back to its start. The whole list is then
///  treated as ONE continuous route and the percentages are measured along its
///  total length. "Auto-Order" tries to chain the list automatically by
///  matching the end of one road to the start of the next (and picks the
///  orientation that gets closest).
///
/// Usage: open a level scene (Core-1 ... Core-4) and then
///   Tools > Road Tools > Paint Progress Checkpoints
/// </summary>
public class CheckpointPainter : EditorWindow
{
    private const string PrefabPath = "Assets/Prefabs/Triggers/Checkpoint.prefab";
    private const string DefaultParentName = "Checkpoints";

    /// <summary>One road in the route, plus which way it is driven.</summary>
    [Serializable]
    private class RoadEntry
    {
        public Road road;
        public bool reverse; // drive the spline from t=1 back to t=0
    }

    /// <summary>A checkpoint that is about to be (or was) placed.</summary>
    private class PlanItem
    {
        public float percent;
        public Vector3 position;
        public Quaternion rotation;
        public float width;
        public float height;
        public float thickness;
        public Road road;
    }

    /// <summary>A road's slice of the combined route.</summary>
    private class PathSegment
    {
        public Road road;
        public bool reverse;
        public float start; // cumulative distance at the start of this road
        public float length;

        public float End { get { return start + length; } }
    }

    private readonly List<RoadEntry> roads = new List<RoadEntry>();

    private string parentName = DefaultParentName;
    private float stepPercent = 5f;
    private float startPercent = 0f;
    private float finishPercent = 1f;
    private float extraWidthPerSide = 20f;
    private float checkpointHeight = 150f;
    private float thicknessAlongRoad = 15f;
    private bool showPreview = true;
    private bool wireCompass = true;

    private Vector2 scroll;
    private List<PlanItem> cachedPlan;
    private bool planDirty = true;

    // Scene wiring is cached: finding those components walks the whole scene
    // hierarchy, which is far too slow to repeat on every OnGUI repaint.
    private Component sceneDisplay;
    private Component sceneCompass;

    [MenuItem("Tools/Road Tools/Paint Progress Checkpoints")]
    static void OpenWindow()
    {
        CheckpointPainter window = GetWindow<CheckpointPainter>("Checkpoint Painter");
        window.minSize = new Vector2(430, 640);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;
        if (roads.Count == 0) FindRoadsInScene();
        RefreshSceneRefs();
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
        RefreshSceneRefs();
        cachedPlan = null;
        planDirty = true;
        Repaint();
    }

    private void RefreshSceneRefs()
    {
        sceneDisplay = FindSceneComponent("ProgressDisplay");
        sceneCompass = FindSceneComponent("CheckpointIndicator");
    }

    // ---------------------------------------------------------------- GUI ---

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Progress Checkpoints", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Places the invisible checkpoint triggers every few percent along the " +
            "route, wires the progress display and the compass, and points the " +
            "compass at the next checkpoint - exactly like levels 1-3.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();

        // ---- Roads ----------------------------------------------------------
        EditorGUILayout.LabelField("Route (in driving order)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "More than one road? Add them here in the order the player drives " +
            "them, and tick Reverse for a road driven from its end to its start.",
            EditorStyles.miniLabel);

        int removeIndex = -1;
        int moveUp = -1;
        int moveDown = -1;

        for (int i = 0; i < roads.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(22f));
            roads[i].road = (Road)EditorGUILayout.ObjectField(roads[i].road, typeof(Road), true);
            roads[i].reverse = EditorGUILayout.ToggleLeft(
                new GUIContent("Rev", "Drive this road from its end back to its start"),
                roads[i].reverse, GUILayout.Width(48f));
            GUI.enabled = i > 0;
            if (GUILayout.Button("^", GUILayout.Width(24f))) moveUp = i;
            GUI.enabled = i < roads.Count - 1;
            if (GUILayout.Button("v", GUILayout.Width(24f))) moveDown = i;
            GUI.enabled = true;
            if (GUILayout.Button("x", GUILayout.Width(24f))) removeIndex = i;
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0) roads.RemoveAt(removeIndex);
        if (moveUp > 0)
        {
            RoadEntry tmp = roads[moveUp];
            roads[moveUp] = roads[moveUp - 1];
            roads[moveUp - 1] = tmp;
        }
        if (moveDown >= 0 && moveDown < roads.Count - 1)
        {
            RoadEntry tmp = roads[moveDown];
            roads[moveDown] = roads[moveDown + 1];
            roads[moveDown + 1] = tmp;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Road Slot")) roads.Add(new RoadEntry());
        if (GUILayout.Button("Find Roads in Scene")) FindRoadsInScene();
        if (GUILayout.Button("Auto-Order (chain)")) AutoOrder();
        if (GUILayout.Button("Refresh Scene")) RefreshSceneRefs();
        EditorGUILayout.EndHorizontal();

        // ---- Progress range -------------------------------------------------
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Progress Range", EditorStyles.boldLabel);
        stepPercent = EditorGUILayout.Slider(
            new GUIContent("Checkpoint Step (%)", "How much progress each checkpoint is worth"),
            stepPercent, 1f, 25f);
        startPercent = EditorGUILayout.Slider(
            new GUIContent("Start (%)", "0% of the route - normally where the player starts"),
            startPercent, 0f, 1f);
        finishPercent = EditorGUILayout.Slider(
            new GUIContent("Finish (%)", "100% of the route - normally the finish line"),
            finishPercent, 0f, 1f);

        if (GUILayout.Button("Start at the player's position"))
        {
            SetStartToPlayer();
        }

        // ---- Collider size --------------------------------------------------
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Trigger Size", EditorStyles.boldLabel);
        extraWidthPerSide = EditorGUILayout.Slider(
            new GUIContent("Extra Width / Side (m)", "How far past the road edge the trigger reaches"),
            extraWidthPerSide, 0f, 100f);
        checkpointHeight = EditorGUILayout.Slider(
            new GUIContent("Height (m)", "Tall enough to cover hills and jumps"),
            checkpointHeight, 20f, 500f);
        thicknessAlongRoad = EditorGUILayout.Slider(
            new GUIContent("Thickness Along Road (m)", "Clamped so neighbours cannot overlap"),
            thicknessAlongRoad, 1f, 60f);
        EditorGUILayout.LabelField(
            "Width is road width + shoulders + the extra margin, so the trigger " +
            "covers the road and then some on both sides.",
            EditorStyles.miniLabel);

        // ---- Misc -----------------------------------------------------------
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        parentName = EditorGUILayout.TextField("Root Object Name", parentName);
        wireCompass = EditorGUILayout.Toggle(
            new GUIContent("Wire Compass", "Point the CheckpointIndicator at the new checkpoints"),
            wireCompass);
        showPreview = EditorGUILayout.Toggle(
            new GUIContent("Scene Preview", "Draw the route and the planned triggers in the scene view"),
            showPreview);

        if (EditorGUI.EndChangeCheck()) planDirty = true;

        // ---- Summary --------------------------------------------------------
        EditorGUILayout.Space();
        DrawSummary();

        // ---- Actions --------------------------------------------------------
        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT CHECKPOINTS", GUILayout.Height(40f))) Paint();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove All Checkpoints", GUILayout.Height(25f))) RemoveAll();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary()
    {
        List<PathSegment> segments = BuildSegments();
        float total = TotalLength(segments);

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  Roads: {segments.Count}   Route length: {total:F1} m");
        EditorGUILayout.LabelField($"  Progress display: {(sceneDisplay != null ? sceneDisplay.name : "NOT FOUND")}");
        EditorGUILayout.LabelField($"  Compass: {(sceneCompass != null ? sceneCompass.name : "NOT FOUND")}");

        if (total <= 0f)
        {
            EditorGUILayout.HelpBox(
                "No usable roads. Add at least one RoadArchitect road (with a built spline) " +
                "to the list above.",
                MessageType.Warning);
            return;
        }

        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "The progress range is empty - raise Start or lower Finish.",
                MessageType.Warning);
            return;
        }

        float spacing = plan.Count > 1
            ? Vector3.Distance(plan[0].position, plan[1].position)
            : 0f;
        EditorGUILayout.LabelField(
            $"  Checkpoints: {plan.Count}   First at {plan[0].percent:F0}%   " +
            $"Last at {plan[plan.Count - 1].percent:F0}%");
        if (spacing > 0f)
            EditorGUILayout.LabelField($"  Spacing: ~{spacing:F1} m");
    }

    // ------------------------------------------------------------ route ---

    private List<PathSegment> BuildSegments()
    {
        List<PathSegment> segments = new List<PathSegment>();
        float acc = 0f;

        foreach (RoadEntry entry in roads)
        {
            if (entry == null || entry.road == null || entry.road.spline == null) continue;
            float length = entry.road.spline.distance;
            if (length <= 0.01f) continue;

            segments.Add(new PathSegment
            {
                road = entry.road,
                reverse = entry.reverse,
                start = acc,
                length = length
            });
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
    /// World position + forward tangent at a cumulative distance along the route.
    /// Returns the segment that contains the distance (null when the route is empty).
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

        // A spline param is not proportional to distance (curves bunch up), so
        // convert the real distance into a param. That is what makes "5%" mean
        // 5% of the actual drivable route instead of 5% of the node count.
        SplineC spline = chosen.road.spline;
        if (spline.RoadDefKeysArray != null && spline.RoadDefKeysArray.Length > 0)
        {
            localParam = Mathf.Clamp01(spline.TranslateDistBasedToParam(localDist));
        }

        float t = chosen.reverse ? 1f - localParam : localParam;

        Vector3 rawPos, rawTangent;
        spline.GetSplineValueBoth(Mathf.Clamp01(t), out rawPos, out rawTangent);

        position = rawPos;
        tangent = chosen.reverse ? -rawTangent : rawTangent;
        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;
        else tangent.Normalize();

        return chosen;
    }

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

        float from = Mathf.Clamp01(startPercent) * total;
        float to = Mathf.Clamp01(finishPercent) * total;
        if (to < from)
        {
            float swap = from;
            from = to;
            to = swap;
        }

        float route = to - from;
        if (route <= 0.1f) return null;

        // How many checkpoints fit in the range (5% step -> 20).
        int count = Mathf.FloorToInt(100f / Mathf.Max(0.01f, stepPercent) + 0.0001f);
        count = Mathf.Max(1, count);
        float spacing = route / count;

        // Never let two neighbouring triggers overlap: keep the thickness well
        // below the spacing so the compass can never skip or double-advance.
        float thickness = Mathf.Max(1f, Mathf.Min(thicknessAlongRoad, spacing * 0.8f));

        List<PlanItem> plan = new List<PlanItem>();

        for (int i = 1; i <= count; i++)
        {
            float percent = i * stepPercent;
            if (percent > 100f + 0.0001f) break;

            float distance = Mathf.Lerp(from, to, percent / 100f);
            Vector3 position, tangent;
            PathSegment segment = SampleAt(segments, distance, out position, out tangent);
            if (segment == null) continue;

            float width = segment.road.RoadWidth()
                          + segment.road.shoulderWidth * 2f
                          + extraWidthPerSide * 2f;

            plan.Add(new PlanItem
            {
                percent = percent,
                position = position,
                rotation = Quaternion.LookRotation(tangent, Vector3.up),
                width = Mathf.Max(1f, width),
                height = checkpointHeight,
                thickness = thickness,
                road = segment.road
            });
        }

        return plan;
    }

    private void SetStartToPlayer()
    {
        List<PathSegment> segments = BuildSegments();
        float total = TotalLength(segments);
        if (total <= 0f) return;

        Transform player = FindPlayerTransform();
        if (player == null)
        {
            Debug.LogWarning("Checkpoint Painter: no player found in the scene (looked for CarController / Player tag).");
            return;
        }

        int steps = 400;
        float bestDistance = 0f;
        float bestSqr = float.MaxValue;

        for (int i = 0; i <= steps; i++)
        {
            float distance = total * i / steps;
            Vector3 position, tangent;
            SampleAt(segments, distance, out position, out tangent);

            Vector3 flat = position - player.position;
            flat.y = 0f;
            float sqr = flat.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestDistance = distance;
            }
        }

        startPercent = Mathf.Clamp01(bestDistance / total);
        cachedPlan = null;
        planDirty = true;
    }

    // --------------------------------------------------------- paint ---

    private void Paint()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Checkpoint Painter: could not load '{PrefabPath}'.");
            return;
        }

        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            Debug.LogError("Checkpoint Painter: nothing to place (no roads or empty progress range).");
            return;
        }

        RefreshSceneRefs();
        Component display = sceneDisplay;
        Component compass = wireCompass ? sceneCompass : null;

        if (display == null)
            Debug.LogWarning("Checkpoint Painter: no ProgressDisplay found in the scene - the checkpoints will count nothing.");
        if (wireCompass && compass == null)
            Debug.LogWarning("Checkpoint Painter: no CheckpointIndicator found in the scene - the compass will not be wired.");

        if (!EditorUtility.DisplayDialog("Paint Progress Checkpoints",
            $"Place {plan.Count} checkpoints every {stepPercent:F0}% of the route?\n\n" +
            $"Progress goes from {plan[0].percent:F0}% at the first trigger to " +
            $"{plan[plan.Count - 1].percent:F0}% at the last one.\n\n" +
            $"An existing '{parentName}' hierarchy (and the compass wiring) will be replaced.\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        // Replace any previous run so repeated painting never stacks triggers.
        RemoveAllSilently();

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Checkpoints Root");

        List<Transform> placed = new List<Transform>();

        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                PlanItem item = plan[i];
                EditorUtility.DisplayProgressBar("Painting Checkpoints",
                    $"{i + 1} / {plan.Count}", (float)i / plan.Count);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Create Checkpoint");

                instance.name = $"Checkpoint ({i + 1})";
                instance.transform.position = item.position;
                instance.transform.rotation = item.rotation;
                instance.transform.localScale = new Vector3(item.width, item.height, item.thickness);

                // The prefab's trigger lives on a child that carries its own
                // scale; flatten it so the world collider is exactly the size
                // we asked for and centred on the road.
                Transform trigger = instance.transform.Find("Trigger");
                if (trigger != null)
                {
                    trigger.localPosition = Vector3.zero;
                    trigger.localRotation = Quaternion.identity;
                    trigger.localScale = Vector3.one;
                }

                Component tracker = FindComponentNamed(instance, "ProgressTracker");
                if (tracker != null)
                {
                    SerializedObject so = new SerializedObject(tracker);
                    SetFloat(so, "progressAmount", item.percent);
                    SetReference(so, "progressDisplay", display);
                    SetReference(so, "checkpointCompass", compass);
                    so.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tracker);
                }

                placed.Add(instance.transform);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (compass != null)
        {
            SerializedObject so = new SerializedObject(compass);
            SerializedProperty array = so.FindProperty("checkpoints");
            if (array != null && array.isArray)
            {
                array.arraySize = placed.Count;
                for (int i = 0; i < placed.Count; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = placed[i];
                }
            }

            SerializedProperty playerProperty = so.FindProperty("player");
            if (playerProperty != null && playerProperty.objectReferenceValue == null)
            {
                Transform player = FindPlayerTransform();
                if (player != null) playerProperty.objectReferenceValue = player;
            }

            so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(compass);
        }

        EditorSceneBridge.MarkSceneDirty();
        Debug.Log($"Checkpoint Painter: placed {placed.Count} checkpoints " +
                  $"({plan[0].percent:F0}% -> {plan[plan.Count - 1].percent:F0}%, " +
                  $"{(wireCompass && compass != null ? "compass wired" : "compass NOT wired")}).");
    }

    private void RemoveAll()
    {
        GameObject existing = FindRootByName(parentName);
        if (existing == null)
        {
            Debug.Log($"Checkpoint Painter: no '{parentName}' hierarchy to remove.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Checkpoints",
            $"Remove the whole '{parentName}' hierarchy and clear the compass?", "Remove", "Cancel"))
        {
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        ClearCompass();
        EditorSceneBridge.MarkSceneDirty();
        Debug.Log($"Checkpoint Painter: removed '{parentName}'.");
    }

    private void RemoveAllSilently()
    {
        GameObject existing = FindRootByName(parentName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        ClearCompass();
    }

    private void ClearCompass()
    {
        Component compass = sceneCompass;
        if (compass == null) compass = FindSceneComponent("CheckpointIndicator");
        if (compass == null) return;

        Undo.RecordObject(compass, "Clear Checkpoint Compass");
        SerializedObject so = new SerializedObject(compass);
        SerializedProperty array = so.FindProperty("checkpoints");
        if (array != null && array.isArray) array.arraySize = 0;
        so.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(compass);
    }

    // ------------------------------------------------------ auto-order ---

    /// <summary>
    /// Reorders the road list so each road's END is as close as possible to the
    /// next road's START, trying both directions for every road. Level 4 is made
    /// of several roads where the end of one is the start of the next, so this
    /// turns a hand-maintained list into one continuous route.
    /// </summary>
    private void AutoOrder()
    {
        List<RoadEntry> valid = new List<RoadEntry>();
        foreach (RoadEntry entry in roads)
        {
            if (entry == null || entry.road == null || entry.road.spline == null) continue;
            if (entry.road.spline.distance <= 0.01f) continue;
            valid.Add(entry);
        }

        if (valid.Count < 2)
        {
            Debug.Log("Checkpoint Painter: need at least two roads to auto-order.");
            return;
        }

        int n = valid.Count;

        // Endpoints of every road in both orientations.
        Vector3[] startF = new Vector3[n];
        Vector3[] endF = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            startF[i] = SplinePoint(valid[i].road, 0f);
            endF[i] = SplinePoint(valid[i].road, 1f);
        }

        Transform player = FindPlayerTransform();
        Vector3 playerPos = player != null ? player.position : Vector3.zero;
        bool hasPlayer = player != null;

        if (n <= 6)
        {
            int[] perm = new int[n];
            for (int i = 0; i < n; i++) perm[i] = i;

            int[] bestPerm = (int[])perm.Clone();
            int bestMask = 0;
            float bestCost = float.MaxValue;

            Action<int[]> evaluate = delegate (int[] order)
            {
                int masks = 1 << n;
                for (int mask = 0; mask < masks; mask++)
                {
                    float cost = 0f;
                    Vector3 cursor = Vector3.zero;
                    for (int k = 0; k < n; k++)
                    {
                        int road = order[k];
                        bool reverse = ((mask >> k) & 1) == 1;
                        Vector3 segStart = reverse ? endF[road] : startF[road];
                        Vector3 segEnd = reverse ? startF[road] : endF[road];

                        if (k > 0) cost += Vector3.Distance(cursor, segStart);
                        else if (hasPlayer) cost += Vector3.Distance(playerPos, segStart) * 0.1f;

                        cursor = segEnd;
                    }

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestMask = mask;
                        bestPerm = (int[])order.Clone();
                    }
                }
            };

            Permute(perm, 0, evaluate);

            List<RoadEntry> ordered = new List<RoadEntry>();
            for (int k = 0; k < n; k++)
            {
                int road = bestPerm[k];
                ordered.Add(new RoadEntry
                {
                    road = valid[road].road,
                    reverse = ((bestMask >> k) & 1) == 1
                });
            }

            roads.Clear();
            roads.AddRange(ordered);
            cachedPlan = null;
            planDirty = true;

            Debug.Log($"Checkpoint Painter: auto-ordered {n} roads into one chain " +
                      $"(total gap {bestCost:F1} m).");
            return;
        }

        // Too many roads to brute force: walk the nearest start / nearest end.
        List<RoadEntry> greedy = new List<RoadEntry>();
        bool[] used = new bool[n];

        int firstIndex = 0;
        float firstBest = float.MaxValue;
        bool firstReverse = false;

        for (int i = 0; i < n; i++)
        {
            float df = Vector3.Distance(playerPos, startF[i]);
            float dr = Vector3.Distance(playerPos, endF[i]);
            if (!hasPlayer) df = dr = 0f;
            if (df < firstBest) { firstBest = df; firstIndex = i; firstReverse = false; }
            if (dr < firstBest) { firstBest = dr; firstIndex = i; firstReverse = true; }
        }

        if (!hasPlayer)
        {
            firstReverse = false;
        }

        used[firstIndex] = true;
        greedy.Add(new RoadEntry { road = valid[firstIndex].road, reverse = firstReverse });
        Vector3 cursor = firstReverse ? startF[firstIndex] : endF[firstIndex];

        for (int placed = 1; placed < n; placed++)
        {
            int bestIndex = -1;
            bool bestRev = false;
            float bestGap = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                float df = Vector3.Distance(cursor, startF[i]);
                float dr = Vector3.Distance(cursor, endF[i]);
                if (df < bestGap) { bestGap = df; bestIndex = i; bestRev = false; }
                if (dr < bestGap) { bestGap = dr; bestIndex = i; bestRev = true; }
            }

            if (bestIndex < 0) break;
            used[bestIndex] = true;
            greedy.Add(new RoadEntry { road = valid[bestIndex].road, reverse = bestRev });
            cursor = bestRev ? startF[bestIndex] : endF[bestIndex];
        }

        roads.Clear();
        roads.AddRange(greedy);
        cachedPlan = null;
        planDirty = true;
        Debug.Log($"Checkpoint Painter: greedily chained {greedy.Count} roads.");
    }

    private static void Permute(int[] array, int k, Action<int[]> visit)
    {
        if (k == array.Length)
        {
            visit(array);
            return;
        }

        for (int i = k; i < array.Length; i++)
        {
            int swap = array[k];
            array[k] = array[i];
            array[i] = swap;

            Permute(array, k + 1, visit);

            swap = array[k];
            array[k] = array[i];
            array[i] = swap;
        }
    }

    private static Vector3 SplinePoint(Road road, float t)
    {
        Vector3 position, tangent;
        road.spline.GetSplineValueBoth(Mathf.Clamp01(t), out position, out tangent);
        return position;
    }

    // -------------------------------------------------------- scene view ---

    private void OnSceneGUI(SceneView view)
    {
        if (!showPreview) return;

        List<PathSegment> segments = BuildSegments();
        float total = TotalLength(segments);
        if (total <= 0f) return;

        float from = Mathf.Clamp01(startPercent) * total;
        float to = Mathf.Clamp01(finishPercent) * total;
        if (to < from)
        {
            float swap = from;
            from = to;
            to = swap;
        }

        // Route centreline.
        int steps = Mathf.Clamp(Mathf.RoundToInt((to - from) / 5f), 8, 800);
        Vector3[] line = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            Vector3 position, tangent;
            SampleAt(segments, Mathf.Lerp(from, to, (float)i / steps), out position, out tangent);
            position.y += 0.5f;
            line[i] = position;
        }

        Handles.color = new Color(0.1f, 0.8f, 1f, 0.9f);
        Handles.DrawAAPolyLine(4f, line);

        // Planned triggers.
        List<PlanItem> plan = GetPlan();
        if (plan == null) return;

        for (int i = 0; i < plan.Count; i++)
        {
            PlanItem item = plan[i];
            Handles.color = i == plan.Count - 1
                ? new Color(1f, 0.35f, 0.1f, 0.85f)
                : new Color(0.2f, 1f, 0.3f, 0.65f);

            Handles.matrix = Matrix4x4.TRS(item.position,
                item.rotation, new Vector3(item.width, item.height, item.thickness));
            Handles.DrawWireCube(Vector3.zero, Vector3.one);
            Handles.matrix = Matrix4x4.identity;

            Handles.Label(item.position + Vector3.up * 4f, $"{item.percent:F0}%");
        }
    }

    // --------------------------------------------------------- helpers ---

    private void FindRoadsInScene()
    {
        Road[] found = FindObjectsOfType<Road>();
        List<RoadEntry> entries = new List<RoadEntry>();

        foreach (Road road in found)
        {
            if (road == null || road.spline == null) continue;
            entries.Add(new RoadEntry { road = road });
        }

        if (entries.Count == 0)
        {
            Debug.LogWarning("Checkpoint Painter: no RoadArchitect roads found in this scene.");
            return;
        }

        roads.Clear();
        roads.AddRange(entries);
        cachedPlan = null;
        planDirty = true;
    }

    private static GameObject FindRootByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == name) return root;
            }
        }

        return GameObject.Find(name);
    }

    private static Component FindSceneComponent(string typeName)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == typeName) return behaviour;
            }
        }

        return null;
    }

    private static Component FindComponentNamed(GameObject go, string typeName)
    {
        foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.GetType().Name == typeName) return behaviour;
        }
        return null;
    }

    private static Transform FindPlayerTransform()
    {
        Component controller = FindSceneComponent("CarController");
        if (controller != null) return controller.transform;

        GameObject tagged = GameObject.FindWithTag("Player");
        if (tagged != null) return tagged.transform;

        GameObject named = GameObject.Find("Player");
        return named != null ? named.transform : null;
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static void SetReference(SerializedObject so, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null && value != null) property.objectReferenceValue = value;
    }
}

/// <summary>Small wrapper so the tool does not need a GameView dependency.</summary>
internal static class EditorSceneBridge
{
    public static void MarkSceneDirty()
    {
        if (SceneManager.GetActiveScene().IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
