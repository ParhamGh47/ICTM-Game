using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that puts the road's own lane divider back on the turns: Blinder prefabs standing on
/// the centre line, spaced along every corner sharp enough to matter.
///
/// Why it works from curvature rather than from the road object:
///  - a "turn" is whatever the player actually drives through, so the tool walks the route, measures
///    how sharply it turns every couple of metres and calls anything above the threshold a curve,
///  - a bend that runs across two road objects is still one curve, because the route is built from
///    the painter's road list laid end to end,
///  - short kinks (a spline node join reading as momentarily straight) do not split a curve in two,
///    and curves shorter than 'Min Curve Length' are ignored - those are junctions, not turns.
///
/// Blinders stand on the centre line with their broad face down the road, which is what divides the
/// two directions, and they are rigid bodies, so the player can clip one and send it flying instead of
/// driving through it. The prefab's model sits below its pivot, so each instance is measured and
/// dropped onto the asphalt rather than placed by its pivot.
///
/// Usage: open a level scene (Core-1 ... Core-4), check the road list, then
///   Tools > Road Tools > Paint Blinders
/// </summary>
public class BlinderSpawner : EditorWindow
{
    private const string PrefabPath = "Assets/Prefabs/Signs/Signs/Blinder.prefab";
    private const string DefaultParentName = "Road Blinders";

    /// <summary>How tall a blinder reads in the scene preview. Only a drawing aid.</summary>
    private const float PreviewHeight = 1.3f;

    private readonly List<RoadRouteEntry> roads = new List<RoadRouteEntry>();

    // ------------------------------------------------------------------ settings

    // Curves
    private float minTurnRate = 1.2f;      // degrees per metre
    private float minCurveLength = 12f;    // metres

    // Blinders
    private float spacing = 6f;            // metres between blinders along a curve
    private float endInset = 3f;           // metres kept clear at each end of a curve
    private int maxPerCurve = 12;
    private bool faceAcrossRoad = false;
    private float yawJitter = 2f;
    private float lateralJitter = 0.1f;
    private float lift = 0.02f;

    // Route
    private float routeEndTrim = 15f;      // metres skipped at each end of the whole route
    private string parentName = DefaultParentName;
    private bool showPreview = true;

    // ------------------------------------------------------------------ state

    private Vector2 scroll;
    private List<PlanItem> cachedPlan;
    private bool planDirty = true;
    private int curveCount;
    private int placedCurveCount;

    private class Curve
    {
        public float start;
        public float end;
        public float peak;       // sharpest turn rate in it
        public float signedSum;  // total signed turn rate, for the average direction
        public int samples;

        public float Length { get { return end - start; } }
        public float Average { get { return samples > 0 ? signedSum / samples : 0f; } }
    }

    private class PlanItem
    {
        public Vector3 position;
        public float yaw;
        public float turnRate;
        public float distance;
    }

    [MenuItem("Tools/Road Tools/Paint Blinders")]
    static void OpenWindow()
    {
        BlinderSpawner window = GetWindow<BlinderSpawner>("Blinder Spawner");
        window.minSize = new Vector2(430, 620);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (roads.Count == 0)
            roads.AddRange(RoadRoute.FindRoadsInScene());

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
        roads.AddRange(RoadRoute.FindRoadsInScene());
        cachedPlan = null;
        planDirty = true;
        Repaint();
    }

    // ---------------------------------------------------------------- GUI ---

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Blinders", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Places Blinder prefabs along the centre line of every turn in the route, so the two " +
            "directions of the road are divided where it matters. Curves are found from how sharply " +
            "the road turns, so straights and gentle bends are left alone.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Roads (in driving order)", EditorStyles.boldLabel);
        int usableRoads;
        if (RoadRoute.DrawRoadList(roads, out usableRoads))
            planDirty = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("What counts as a turn", EditorStyles.boldLabel);
        minTurnRate = EditorGUILayout.Slider(
            new GUIContent("Min Turn Rate (deg/m)", "How sharply the road must turn before a curve " +
                                                   "is worth dividing. 1.2 is a comfortable bend, 3 is a hairpin"),
            minTurnRate, 0.2f, 8f);
        minCurveLength = EditorGUILayout.Slider(
            new GUIContent("Min Curve Length (m)", "Curves shorter than this are ignored - those are " +
                                                   "junctions and wobbles rather than turns"),
            minCurveLength, 2f, 80f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Blinders", EditorStyles.boldLabel);
        spacing = EditorGUILayout.Slider(
            new GUIContent("Spacing (m)", "Gap between blinders along a curve"),
            spacing, 1f, 40f);
        maxPerCurve = EditorGUILayout.IntSlider(
            new GUIContent("Max Per Curve", "Cap on blinders in one curve; they are spread evenly over " +
                                            "it when the cap is reached"),
            maxPerCurve, 1, 60);
        endInset = EditorGUILayout.Slider(
            new GUIContent("End Inset (m)", "How far past the start of a curve the first blinder sits " +
                                            "and how far before its end the last one does"),
            endInset, 0f, 30f);
        faceAcrossRoad = EditorGUILayout.Toggle(
            new GUIContent("Face Across Road", "Turn each blinder a quarter turn so its broad face meets " +
                                               "oncoming traffic instead of running down the road"),
            faceAcrossRoad);
        yawJitter = EditorGUILayout.Slider(
            new GUIContent("Yaw Jitter (deg)", "Random lean off the road direction, so a row of them " +
                                               "does not look stamped"),
            yawJitter, 0f, 45f);
        lateralJitter = EditorGUILayout.Slider(
            new GUIContent("Lateral Jitter (m)", "Random offset either side of the centre line"),
            lateralJitter, 0f, 1f);
        lift = EditorGUILayout.Slider(
            new GUIContent("Lift (m)", "How far above the asphalt they are placed. They are rigid " +
                                       "bodies, so a small lift just lets them settle"),
            lift, -0.2f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Route & Options", EditorStyles.boldLabel);
        routeEndTrim = EditorGUILayout.Slider(
            new GUIContent("Trim Route Ends (m)", "Nothing is placed this close to either end of the " +
                                                  "whole route, so the start line stays clear"),
            routeEndTrim, 0f, 200f);
        parentName = EditorGUILayout.TextField("Root Object Name", parentName);
        showPreview = EditorGUILayout.Toggle(
            new GUIContent("Scene Preview", "Draw the curves and the planned blinders in the scene view"),
            showPreview);

        if (EditorGUI.EndChangeCheck())
            planDirty = true;

        EditorGUILayout.Space();
        DrawSummary(usableRoads);

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT BLINDERS", GUILayout.Height(40f))) Paint();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove Painted Blinders", GUILayout.Height(25f))) RemoveAll();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary(int usableRoads)
    {
        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  Roads in the route: " + usableRoads +
                                   "   Route length: " + total.ToString("F1") + " m");

        if (total <= 0f)
        {
            EditorGUILayout.HelpBox(
                "No usable roads. Add a RoadArchitect road with a built spline to the list.",
                MessageType.Warning);
            return;
        }

        List<PlanItem> plan = GetPlan();
        EditorGUILayout.LabelField("  Curves found: " + curveCount +
                                   "   Long enough to divide: " + placedCurveCount);

        if (plan == null || plan.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No blinders to place. Lower 'Min Turn Rate' to include gentler bends, or lower " +
                "'Min Curve Length' if the level's turns are short.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("  Blinders: " + plan.Count + "   ~" + spacing.ToString("F0") + " m apart");
        EditorGUILayout.LabelField("  Root object: '" + parentName + "' (replaced on every paint)");
    }

    // -------------------------------------------------------------- curves ---

    /// <summary>
    /// Scans the route and returns every stretch that turns at least as sharply as 'Min Turn Rate'.
    /// Brief straights inside a curve do not end it, so a bend is never split in two by the way the
    /// spline happens to be built.
    /// </summary>
    private List<Curve> FindCurves(List<RoadRouteSegment> segments)
    {
        List<Curve> curves = new List<Curve>();
        float total = RoadRoute.TotalLength(segments);
        if (total <= 0f) return curves;

        float gapTolerance = RoadRoute.TurnSampleStep * 3f;

        Curve open = null;
        float straightRun = 0f;

        for (float distance = 0f; distance <= total + 0.001f; distance += RoadRoute.TurnSampleStep)
        {
            float turn = RoadRoute.TurnRateAt(segments, distance);

            if (Mathf.Abs(turn) >= minTurnRate)
            {
                if (open == null)
                {
                    open = new Curve();
                    open.start = distance;
                }

                open.end = distance;
                open.peak = Mathf.Max(open.peak, Mathf.Abs(turn));
                open.signedSum += turn;
                open.samples++;
                straightRun = 0f;
            }
            else if (open != null)
            {
                straightRun += RoadRoute.TurnSampleStep;

                if (straightRun > gapTolerance)
                {
                    curves.Add(open);
                    open = null;
                    straightRun = 0f;
                }
            }
        }

        if (open != null)
            curves.Add(open);

        return curves;
    }

    // ---------------------------------------------------------------- plan ---

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
        List<PlanItem> plan = new List<PlanItem>();

        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);

        curveCount = 0;
        placedCurveCount = 0;

        if (total <= 0f) return plan;

        List<Curve> curves = FindCurves(segments);
        curveCount = curves.Count;

        float lastDistance = float.NegativeInfinity;

        for (int i = 0; i < curves.Count; i++)
        {
            Curve curve = curves[i];
            if (curve.Length < minCurveLength) continue;

            placedCurveCount++;

            float inset = Mathf.Min(endInset, curve.Length * 0.25f);
            float first = curve.start + inset;
            float last = curve.end - inset;

            int slots = 1;
            if (spacing > 0.1f && last > first)
                slots = Mathf.Clamp(Mathf.FloorToInt((last - first) / spacing) + 1, 1, Mathf.Max(1, maxPerCurve));
            else
                slots = Mathf.Clamp(1, 1, Mathf.Max(1, maxPerCurve));

            for (int slot = 0; slot < slots; slot++)
            {
                // One blinder goes in the middle of the curve; more than one is spread evenly over it.
                float t = slots > 1 ? slot / (float)(slots - 1) : 0.5f;
                float distance = Mathf.Lerp(first, last, t);

                if (distance < routeEndTrim) continue;
                if (distance > total - routeEndTrim) continue;

                // Curves that follow each other closely must not double up where they meet.
                if (distance - lastDistance < spacing) continue;

                Vector3 position;
                Vector3 tangent;
                if (RoadRoute.SampleAt(segments, distance, out position, out tangent) == null) continue;

                Vector3 across = RoadRoute.Across(tangent);

                float yaw = RoadRoute.YawAlong(tangent)
                            + Random.Range(-yawJitter, yawJitter)
                            + (faceAcrossRoad ? 90f : 0f);

                PlanItem item = new PlanItem();
                item.position = position
                                + across * Random.Range(-lateralJitter, lateralJitter)
                                + Vector3.up * lift;
                item.yaw = yaw;
                item.turnRate = curve.Average;
                item.distance = distance;

                plan.Add(item);
                lastDistance = distance;
            }
        }

        return plan;
    }

    // --------------------------------------------------------------- paint ---

    private void Paint()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Blinder Spawner: could not load '" + PrefabPath + "'.");
            return;
        }

        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            Debug.LogError("Blinder Spawner: nothing to place. Check the road list and Min Turn Rate.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Paint Blinders",
            "Place " + plan.Count + " blinders along " + placedCurveCount + " of the route's " +
            curveCount + " curves?\n\n" +
            "An existing root object named '" + parentName + "' will be replaced.\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        RemoveAllSilently();

        PrefabMeasure.Footprint footprint = PrefabMeasure.Of(prefab);

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Road Blinders Root");

        int placed = 0;
        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Painting Blinders",
                    (i + 1) + " / " + plan.Count, (float)i / plan.Count);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Create Blinder");

                instance.name = "Blinder " + (i + 1);
                instance.transform.rotation = Quaternion.Euler(0f, plan[i].yaw, 0f);
                instance.transform.position = SitOnGround(plan[i].position, footprint);
                placed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        RoadRoute.MarkSceneDirty();
        Debug.Log("Blinder Spawner: placed " + placed + " blinders on " + placedCurveCount +
                  " curves (of " + curveCount + " found).");
    }

    /// <summary>
    /// The blinder prefab's model hangs below its pivot, so the instance is dropped until the lowest
    /// point of its geometry sits on the road instead of trusting the pivot.
    /// </summary>
    private static Vector3 SitOnGround(Vector3 position, PrefabMeasure.Footprint footprint)
    {
        return new Vector3(position.x, position.y - footprint.Bottom, position.z);
    }

    private void RemoveAll()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing == null)
        {
            Debug.Log("Blinder Spawner: no root object named '" + parentName + "'.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Blinders",
            "Remove the whole '" + parentName + "' hierarchy?", "Remove", "Cancel"))
        {
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        RoadRoute.MarkSceneDirty();
        Debug.Log("Blinder Spawner: removed '" + parentName + "'.");
    }

    private void RemoveAllSilently()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);
    }

    // ---------------------------------------------------------- scene view ---

    private void OnSceneGUI(SceneView view)
    {
        if (!showPreview) return;

        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);
        if (total <= 0f) return;

        // The curves themselves, so it is obvious what the tool counts as a turn.
        List<Curve> curves = FindCurves(segments);
        for (int i = 0; i < curves.Count; i++)
        {
            Curve curve = curves[i];
            bool longEnough = curve.Length >= minCurveLength;

            Handles.color = longEnough
                ? (curve.Average >= 0f ? new Color(1f, 0.8f, 0.2f, 0.75f) : new Color(0.4f, 0.85f, 1f, 0.75f))
                : new Color(0.6f, 0.6f, 0.6f, 0.35f);

            Vector3 previous = Vector3.zero;
            bool started = false;

            for (float d = curve.start; d <= curve.end + 0.001f; d += RoadRoute.TurnSampleStep)
            {
                Vector3 position;
                Vector3 tangent;
                if (RoadRoute.SampleAt(segments, d, out position, out tangent) == null) continue;

                position += Vector3.up * 0.5f;

                if (started)
                    Handles.DrawAAPolyLine(4f, previous, position);

                previous = position;
                started = true;
            }
        }

        // The blinders that would be placed, including which way each curve turns.
        List<PlanItem> plan = GetPlan();
        if (plan == null) return;

        for (int i = 0; i < plan.Count; i++)
        {
            PlanItem item = plan[i];

            Handles.color = item.turnRate >= 0f
                ? new Color(1f, 0.8f, 0.2f, 0.95f)
                : new Color(0.4f, 0.85f, 1f, 0.95f);

            float size = HandleUtility.GetHandleSize(item.position) * 0.35f;
            Handles.DrawSolidDisc(item.position + Vector3.up * 0.03f, Vector3.up, size);

            Vector3 top = item.position + Vector3.up * PreviewHeight;
            Handles.DrawAAPolyLine(3f, item.position, top);

            // The broad face runs down the road, so the thin side is what faces the two directions.
            Quaternion yaw = Quaternion.Euler(0f, item.yaw, 0f);
            Handles.DrawAAPolyLine(2f,
                top + yaw * new Vector3(-0.35f, 0f, 0f),
                top + yaw * new Vector3(0.35f, 0f, 0f));
        }
    }
}
