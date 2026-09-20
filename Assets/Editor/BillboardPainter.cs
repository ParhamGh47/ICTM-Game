using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool that scatters billboards along both sides of a RoadArchitect route, each one facing the road,
/// with a poster picked at random from the project's own pictures.
///
/// The billboards come in two flavours and the tool picks between them: 'Billboard' is the plain one and
/// 'Billboard Variant' is the same thing with the pair of spotlights and the lit face, so the 'Lights On'
/// switch decides which way round the level is lit.
///
/// The posters are the sprites in Assets/Sprites/BilboardPosters. They are drawn from a shuffled deck per
/// side of the road, so the same picture does not turn up twice on one side until that side has used them
/// all - a drive down the road shows you a run of different adverts rather than the same one again and
/// again. The folder called Special is left out on purpose.
///
/// The billboard model is built with its board running along its +-Z and its picture facing its -X, so a
/// billboard turned a quarter turn off the road direction looks back down the road - which is how the traffic
/// on the route sees it, head on, with the board standing across the verge. 'Flip Facing' is there for a
/// prefab whose face turns out to be the other side, and 'Facing Offset' angles them a little if you want
/// them turned towards the road rather than square to it.
///
/// Usage: open a level scene (Core-1 ... Core-4), check the road list, then
///   Tools > Road Tools > Paint Billboards
/// </summary>
public class BillboardPainter : EditorWindow
{
    private const string PosterFolder = "Assets/Sprites/BilboardPosters";
    private const string PlainPrefabPath = "Assets/Prefabs/Signs/Bilboards/Billboard.prefab";
    private const string LitPrefabPath = "Assets/Prefabs/Signs/Bilboards/Billboard Variant.prefab";
    private const string DefaultParentName = "Billboards";

    /// <summary>One of the project's posters, with its own switch.</summary>
    private class Poster
    {
        public Sprite sprite;
        public bool enabled = true;

        public string name { get { return sprite != null ? sprite.name : "?"; } }
    }

    private class PlanItem
    {
        public Vector3 position;
        public float yaw;
        public float scale;
        public Sprite poster;
        public bool right;
    }

    // ------------------------------------------------------------------ settings

    private readonly List<RoadRouteEntry> roads = new List<RoadRouteEntry>();
    private readonly List<Poster> posters = new List<Poster>();

    private bool lightsOn;
    private float spacing = 150f;
    private float spacingJitter = 0.4f;
    private float minPad = 5f;
    private float maxPad = 20f;
    private float lift = 0.05f;
    private float minSeparation = 12f;
    private int maxBillboards = 40;

    private int sideMode;                               // 0 both, 1 right, 2 left
    private static readonly string[] SideNames = { "Both sides", "Right of travel", "Left of travel" };

    private Vector2 scaleRange = new Vector2(1f, 1.35f);
    private bool flipFacing;
    private float facingOffset;
    private float groundProbeHeight = 5f;
    private float groundMaxDrop = 20f;
    private float minGroundSlope = 0.75f;

    private float routeEndTrim = 40f;
    private string parentName = DefaultParentName;
    private bool showPreview = true;

    // ------------------------------------------------------------------ state

    private Vector2 scroll;
    private List<PlanItem> cachedPlan;
    private bool planDirty = true;
    private int rejectedSpots;

    // A deck of posters per side of the road, so a side works through them without repeating, and the last
    // one each side put up, so the two verges do not show the same picture opposite each other.
    private List<Sprite>[] decks;
    private readonly int[] deckIndex = new int[2];
    private readonly Sprite[] lastPoster = new Sprite[2];

    [MenuItem("Tools/Road Tools/Paint Billboards")]
    static void OpenWindow()
    {
        BillboardPainter window = GetWindow<BillboardPainter>("Billboards");
        window.minSize = new Vector2(480, 660);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (roads.Count == 0)
            roads.AddRange(RoadRoute.FindRoadsInScene());

        if (posters.Count == 0)
            FindPosters();

        planDirty = true;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged -= OnActiveSceneChanged;
        EditorUtility.ClearProgressBar();
    }

    private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previous,
                                      UnityEngine.SceneManagement.Scene current)
    {
        roads.Clear();
        roads.AddRange(RoadRoute.FindRoadsInScene());
        cachedPlan = null;
        planDirty = true;
        Repaint();
    }

    /// <summary>
    /// The pictures on offer: the sprites sitting in the poster folder itself. Anything nested in a folder
    /// under it - the Collection called Special - is deliberately not picked up, and a poster that was
    /// switched off keeps its switch across a rescan.
    /// </summary>
    private void FindPosters()
    {
        List<Poster> previous = new List<Poster>(posters);
        posters.Clear();

        if (!AssetDatabase.IsValidFolder(PosterFolder))
        {
            Debug.LogWarning("Billboards: no folder at " + PosterFolder + ".");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { PosterFolder });
        if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PosterFolder });

        List<Sprite> found = new List<Sprite>();
        string folder = PosterFolder.TrimEnd('/');

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');

            // Only the folder itself: the subfolders hold the pictures this tool is told to leave alone.
            // Done by hand rather than with Path.GetDirectoryName, which returns the platform's own
            // separator - backslashes on Windows - and so would never match the folder above.
            int slash = path.LastIndexOf('/');
            if (slash <= 0 || path.Substring(0, slash) != folder) continue;

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null && !found.Contains(sprite)) found.Add(sprite);
        }

        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < found.Count; i++)
        {
            Poster poster = new Poster();
            poster.sprite = found[i];

            for (int p = 0; p < previous.Count; p++)
                if (previous[p].sprite == poster.sprite) poster.enabled = previous[p].enabled;

            posters.Add(poster);
        }

        if (posters.Count == 0)
            Debug.LogWarning("Billboards: no sprites directly inside " + PosterFolder + ".");
    }

    private GameObject PrefabForState()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(lightsOn ? LitPrefabPath : PlainPrefabPath);
    }

    // ---------------------------------------------------------------- GUI ---

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Billboards", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scatters billboards along both verges, each turned to face the road, each with a different " +
            "poster. The posters are drawn from a shuffled deck per side of the road, so one side works " +
            "through the whole set before a picture comes round again.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Roads (in driving order)", EditorStyles.boldLabel);
        int usableRoads;
        if (RoadRoute.DrawRoadList(roads, out usableRoads))
            planDirty = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Billboard", EditorStyles.boldLabel);
        lightsOn = EditorGUILayout.ToggleLeft(
            new GUIContent("Lights On", "Use the lit billboard: the pair of spotlights and the illuminated face"),
            lightsOn);

        GameObject prefab = PrefabForState();
        EditorGUILayout.LabelField("  " + (lightsOn ? "Billboard Variant" : "Billboard") +
                                   (prefab == null ? "  (MISSING at the expected path)" : ""),
                                   EditorStyles.miniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pictures (" + EnabledPosters() + " of " + posters.Count + " in use)",
                                   EditorStyles.boldLabel);

        for (int i = 0; i < posters.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            posters[i].enabled = EditorGUILayout.ToggleLeft(posters[i].name, posters[i].enabled);
            EditorGUILayout.ObjectField(posters[i].sprite, typeof(Sprite), false, GUILayout.Width(60f));
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Rescan Poster Folder")) { FindPosters(); planDirty = true; }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Where they go", EditorStyles.boldLabel);
        spacing = EditorGUILayout.Slider(
            new GUIContent("Spacing (m)", "Average distance along the road between two billboards"),
            spacing, 20f, 800f);
        spacingJitter = EditorGUILayout.Slider(
            new GUIContent("Spacing Jitter", "How uneven the gaps are (0.5 = halfway to double)"),
            spacingJitter, 0f, 0.9f);
        minPad = EditorGUILayout.Slider(
            new GUIContent("Min Past Road Edge (m)", "Closest a billboard may stand to the asphalt edge"),
            minPad, 0f, 40f);
        maxPad = EditorGUILayout.Slider(
            new GUIContent("Max Past Road Edge (m)", "Furthest it may stand from the asphalt edge"),
            maxPad, 0f, 80f);
        sideMode = EditorGUILayout.Popup(new GUIContent("Side", "Which verge to use"), sideMode, SideNames);
        minSeparation = EditorGUILayout.Slider(
            new GUIContent("Min Separation (m)", "Smallest gap between two billboards"),
            minSeparation, 2f, 80f);
        maxBillboards = EditorGUILayout.IntSlider(
            new GUIContent("Max Billboards", "Upper limit on how many are placed in one paint"),
            maxBillboards, 1, 300);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("How they sit", EditorStyles.boldLabel);
        scaleRange = EditorGUILayout.Vector2Field(
            new GUIContent("Scale", "Smallest and largest size a billboard is placed at"), scaleRange);
        groundMaxDrop = EditorGUILayout.Slider(
            new GUIContent("Ground Search (m)", "How far below a spot the tool looks for ground before giving up"),
            groundMaxDrop, 1f, 60f);
        minGroundSlope = EditorGUILayout.Slider(
            new GUIContent("Min Ground Flatness", "Steepest ground a billboard may stand on (1 is flat)"),
            minGroundSlope, 0.3f, 1f);
        lift = EditorGUILayout.Slider(
            new GUIContent("Lift (m)", "How far above the ground each billboard is placed"),
            lift, 0f, 0.5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Facing", EditorStyles.boldLabel);
        facingOffset = EditorGUILayout.Slider(
            new GUIContent("Facing Offset (deg)", "Nudge the facing if the billboards do not sit square to " +
                                                  "the road"), facingOffset, -180f, 180f);
        flipFacing = EditorGUILayout.Toggle(
            new GUIContent("Flip Facing", "Turn every billboard the other way round, for a prefab whose face " +
                                          "is the other side"), flipFacing);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        routeEndTrim = EditorGUILayout.Slider(
            new GUIContent("Trim Route Ends (m)", "Nothing is placed this close to either end of the route"),
            routeEndTrim, 0f, 400f);
        parentName = EditorGUILayout.TextField("Root Object Name", parentName);
        showPreview = EditorGUILayout.Toggle(
            new GUIContent("Scene Preview", "Draw every planned billboard in the scene view"), showPreview);

        if (EditorGUI.EndChangeCheck())
            planDirty = true;

        EditorGUILayout.Space();
        DrawSummary(usableRoads);

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT BILLBOARDS", GUILayout.Height(40f))) Paint();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove Painted Billboards", GUILayout.Height(25f))) RemoveAll();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private int EnabledPosters()
    {
        int count = 0;
        for (int i = 0; i < posters.Count; i++)
            if (posters[i].enabled && posters[i].sprite != null) count++;

        return count;
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
            EditorGUILayout.HelpBox("No usable roads. Add a RoadArchitect road with a built spline to the list.",
                                    MessageType.Warning);
            return;
        }

        if (EnabledPosters() == 0)
        {
            EditorGUILayout.HelpBox("No pictures are switched on.", MessageType.Warning);
            return;
        }

        if (PrefabForState() == null)
        {
            EditorGUILayout.HelpBox("The billboard prefab is missing from " +
                                    (lightsOn ? LitPrefabPath : PlainPrefabPath) + ".", MessageType.Warning);
            return;
        }

        List<PlanItem> plan = GetPlan();

        if (plan == null || plan.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Nothing to place. Widen the pads, lower the ground flatness, or shorten the spacing.",
                MessageType.Warning);
            return;
        }

        int right = 0;
        for (int i = 0; i < plan.Count; i++)
            if (plan[i].right) right++;

        EditorGUILayout.LabelField("  Billboards: " + plan.Count + "   (" + right + " right, " +
                                   (plan.Count - right) + " left)");
        EditorGUILayout.LabelField("  Spots rejected (no ground / no room): " + rejectedSpots);
        EditorGUILayout.LabelField("  Root object: '" + parentName + "' (replaced on every paint)");
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
        rejectedSpots = 0;

        // The decks are rebuilt with the plan, so what the scene view shows is what a paint would produce.
        decks = new List<Sprite>[2];
        for (int i = 0; i < decks.Length; i++)
        {
            decks[i] = ShuffledDeck();
            deckIndex[i] = 0;
            lastPoster[i] = null;
        }

        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);
        if (total <= 0f) return plan;

        if (PrefabForState() == null || EnabledPosters() == 0) return plan;

        float from = routeEndTrim;
        float to = total - routeEndTrim;
        if (to <= from) return plan;

        bool preferRight = Random.value < 0.5f;
        float distance = from;

        while (distance < to && plan.Count < maxBillboards)
        {
            distance += Mathf.Max(10f, spacing * Random.Range(1f - spacingJitter, 1f + spacingJitter));

            if (TrySpot(plan, segments, distance, ref preferRight)) continue;
            rejectedSpots++;
        }

        return plan;
    }

    /// <summary>Picks a side, finds the ground and a poster, checks there is room, and places it.</summary>
    private bool TrySpot(List<PlanItem> plan, List<RoadRouteSegment> segments, float distance,
                         ref bool preferRight)
    {
        Vector3 position;
        Vector3 tangent;
        RoadRouteSegment segment = RoadRoute.SampleAt(segments, distance, out position, out tangent);
        if (segment == null) return false;

        bool firstRight;
        if (sideMode == 1) firstRight = true;
        else if (sideMode == 2) firstRight = false;
        else firstRight = Random.value < 0.75f ? !preferRight : preferRight;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool right = attempt == 0 ? firstRight : !firstRight;

            float pad = Random.Range(Mathf.Min(minPad, maxPad), Mathf.Max(minPad, maxPad));
            Vector3 across = RoadRoute.Across(tangent);
            if (!right) across = -across;

            Vector3 spot = position + across * (RoadRoute.RoadEdge(segment.road) + pad);

            Vector3 ground;
            if (!RoadRoute.TryFindGround(spot, groundProbeHeight, groundMaxDrop, minGroundSlope, out ground))
                continue;

            if (!IsClear(ground)) return false;

            float scale = Random.Range(Mathf.Min(scaleRange.x, scaleRange.y), Mathf.Max(scaleRange.x, scaleRange.y));
            if (!HasRoom(plan, ground, scale)) return false;

            PlanItem item = new PlanItem();
            item.position = ground + Vector3.up * lift;
            item.yaw = FacingFor(tangent);
            item.scale = scale;
            item.right = right;
            item.poster = NextPoster(right);

            plan.Add(item);
            preferRight = right;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Which way a billboard is turned: back down the road, so the traffic on the route reads it head on.
    ///
    /// The board is built along its own +Z with its picture on its -X. Turning the object to face down the
    /// road is therefore a quarter turn off the road direction, not a half turn, and it lands the board across
    /// the verge - which is how a billboard actually stands.
    /// </summary>
    private float FacingFor(Vector3 tangent)
    {
        float yaw = RoadRoute.YawAlong(tangent) - 90f;

        if (flipFacing) yaw += 180f;

        return yaw + facingOffset;
    }

    /// <summary>
    /// The next poster for one side of the road: dealt off that side's deck, which is reshuffled once it runs
    /// out. So one side works its way through the whole set before a picture comes round again, and - because
    /// the card facing the one the other side just put up is swapped for the next along - the two verges
    /// rarely show the same advert opposite each other.
    /// </summary>
    private Sprite NextPoster(bool right)
    {
        int side = right ? 0 : 1;
        List<Sprite> deck = decks != null ? decks[side] : null;
        if (deck == null || deck.Count == 0) return null;

        if (deckIndex[side] >= deck.Count)
        {
            Shuffle(deck);
            deckIndex[side] = 0;
        }

        int index = deckIndex[side];

        // The one the far side is showing right now: rather than a matching pair facing each other, take the
        // next card along. Nothing here matters if there is only one picture in the folder.
        if (deck.Count > 1 && deck[index] == lastPoster[1 - side])
        {
            int next = index + 1 < deck.Count ? index + 1 : 0;
            Sprite swap = deck[index];
            deck[index] = deck[next];
            deck[next] = swap;
        }

        Sprite poster = deck[index];
        deckIndex[side]++;
        lastPoster[side] = poster;
        return poster;
    }

    private List<Sprite> ShuffledDeck()
    {
        List<Sprite> deck = new List<Sprite>();

        for (int i = 0; i < posters.Count; i++)
            if (posters[i].enabled && posters[i].sprite != null) deck.Add(posters[i].sprite);

        Shuffle(deck);
        return deck;
    }

    private static void Shuffle(List<Sprite> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Sprite swap = deck[i];
            deck[i] = deck[j];
            deck[j] = swap;
        }
    }

    /// <summary>
    /// True when nothing else is already standing there. Unlike the sign painter, the road here is an
    /// obstacle rather than ground: a billboard stands on the verge, never on the asphalt, so a spot whose
    /// clearance touches the road is thrown away - which is what the pads are for.
    /// </summary>
    private static bool IsClear(Vector3 ground)
    {
        Collider[] hits = Physics.OverlapSphere(ground + Vector3.up * 1f, 1f, ~0, QueryTriggerInteraction.Ignore);
        GameObject[] painterRoots = RoadRoute.PainterRoots();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            if (hit.GetComponentInParent<Terrain>() != null) continue;
            if (hit.GetComponentInParent<Road>() != null) return false;

            bool ours = false;
            for (int r = 0; r < painterRoots.Length; r++)
                if (painterRoots[r] != null && hit.transform.IsChildOf(painterRoots[r].transform)) ours = true;

            if (ours) continue;

            return false;
        }

        return true;
    }

    private bool HasRoom(List<PlanItem> plan, Vector3 ground, float scale)
    {
        for (int i = 0; i < plan.Count; i++)
        {
            Vector3 delta = plan[i].position - ground;
            delta.y = 0f;

            float needed = minSeparation + (plan[i].scale + scale) * 2f;
            if (delta.sqrMagnitude < needed * needed) return false;
        }

        return true;
    }

    // --------------------------------------------------------------- paint ---

    private void Paint()
    {
        List<PlanItem> plan = GetPlan();
        GameObject prefab = PrefabForState();

        if (plan == null || plan.Count == 0 || prefab == null)
        {
            Debug.LogError("Billboards: nothing to place. Check the road list, the pads and that the poster " +
                           "folder has pictures switched on.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Paint Billboards",
            "Place " + plan.Count + " billboard(s)" + (lightsOn ? " with their lights on" : "") + "?\n\n" +
            "An existing root object named '" + parentName + "' will be replaced.\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        RemoveAllSilently();

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Billboards Root");

        int placed = 0;
        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                PlanItem item = plan[i];
                EditorUtility.DisplayProgressBar("Painting Billboards",
                    (i + 1) + " / " + plan.Count, (float)i / plan.Count);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Create Billboard");

                PrefabMeasure.Footprint footprint = PrefabMeasure.Of(prefab);

                instance.name = "Billboard " + (i + 1) + (item.poster != null ? " (" + item.poster.name + ")" : "");
                if (!Mathf.Approximately(item.scale, 1f)) instance.transform.localScale *= item.scale;
                instance.transform.rotation = Quaternion.Euler(0f, item.yaw, 0f);

                // The prefab's own root carries an offset from wherever it was made, and its posts do not have
                // their pivot at their feet, so it is dropped until its lowest geometry rests on the ground.
                instance.transform.position = new Vector3(
                    item.position.x,
                    item.position.y - footprint.Bottom * item.scale,
                    item.position.z);

                SetPoster(instance, item.poster);
                placed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        RoadRoute.MarkSceneDirty();

        Debug.Log("Billboards: placed " + placed + " billboard(s) along the road (" + rejectedSpots +
                  " spots rejected).");
    }

    /// <summary>The poster is the sprite on the billboard's board, so that is what gets swapped.</summary>
    private static void SetPoster(GameObject instance, Sprite poster)
    {
        if (poster == null) return;

        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null) return;

        Undo.RecordObject(renderer, "Set Billboard Poster");
        renderer.sprite = poster;
    }

    private void RemoveAll()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing == null)
        {
            Debug.Log("Billboards: no root object named '" + parentName + "'.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Billboards",
            "Remove the whole '" + parentName + "' hierarchy?", "Remove", "Cancel"))
        {
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        RoadRoute.MarkSceneDirty();
        Debug.Log("Billboards: removed '" + parentName + "'.");
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

        List<PlanItem> plan = GetPlan();
        if (plan == null) return;

        for (int i = 0; i < plan.Count; i++)
        {
            PlanItem item = plan[i];

            Handles.color = new Color(0.3f, 0.75f, 1f);
            Handles.DrawWireDisc(item.position + Vector3.up * 0.05f, Vector3.up, 1.6f * item.scale);

            // The board runs along the object's Z and looks along its -X, so both are drawn: one line along
            // the board, one showing which way the picture faces.
            Quaternion turn = Quaternion.Euler(0f, item.yaw, 0f);
            Vector3 along = turn * Vector3.forward * 2.2f * item.scale;
            Vector3 facing = turn * Vector3.left * 6f;

            Handles.DrawAAPolyLine(4f, item.position - along, item.position + along);
            Handles.DrawAAPolyLine(2f, item.position + Vector3.up * 1.5f,
                                   item.position + Vector3.up * 1.5f + facing);

            Handles.Label(item.position + Vector3.up * 3f,
                          (item.poster != null ? item.poster.name : "no poster") +
                          (lightsOn ? " (lit)" : "") + (item.right ? "  R" : "  L"));
        }
    }
}
