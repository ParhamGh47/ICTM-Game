using UnityEngine;
using UnityEditor;
using RoadArchitect;
using System.Collections.Generic;

/// <summary>
/// Editor tool to create invisible BoxCollider walls along the road sides.
/// Uses the RoadArchitect spline to follow the road path and places wall segments
/// at configured offsets on both sides, preventing the player from going offroad.
/// 
/// Usage: Open a level scene in Unity Editor, then go to menu:
/// Tools > Road Tools > Place Invisible Walls Along Road
/// </summary>
public class TerrainInvisibleWallPainter : EditorWindow
{
    private Road targetRoad;

    // Section of road to place walls (spline param 0-1)
    private float startParam = 0f;
    private float endParam = 1.0f;

    // Offset from road edge (shoulder) to place walls
    // This is the distance from the edge of the road+shoulder to where the wall starts
    private float offsetFromRoadEdge = 8f;

    // Wall dimensions
    private float wallHeight = 50f;       // Tall enough that vehicles can't go over
    private float wallSegmentLength = 15f; // Length of each wall segment along the road
    private float wallThickness = 1f;      // How thick the wall collider is

    // Spacing between segments (slight overlap prevents gaps)
    private float segmentOverlap = 0.5f;

    // Preview
    private bool showPreview = false;
    private List<WallPreviewData> previewData = new List<WallPreviewData>();

    private struct WallPreviewData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    [MenuItem("Tools/Road Tools/Place Invisible Walls Along Road")]
    static void OpenWindow()
    {
        var window = GetWindow<TerrainInvisibleWallPainter>("Invisible Wall Painter");
        window.minSize = new Vector2(380, 520);
        window.Show();
    }

    void OnEnable()
    {
        // Auto-find road in scene
        if (targetRoad == null)
        {
            var roads = FindObjectsOfType<Road>();
            if (roads.Length > 0)
            {
                targetRoad = roads[0];
            }
        }
    }

    private Vector2 scrollPos;

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        try
        {
            GUILayout.Label("Place Invisible Walls Along Road", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // References
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            targetRoad = (Road)EditorGUILayout.ObjectField("Road", targetRoad, typeof(Road), true);

            if (targetRoad == null)
            {
                EditorGUILayout.HelpBox("Please assign a Road reference.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (targetRoad.spline == null)
            {
                EditorGUILayout.HelpBox("Road spline not found. Make sure the road is built.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }
            if (targetRoad.spline.distance <= 0f)
            {
                EditorGUILayout.HelpBox("Spline distance is 0. Make sure the road spline is properly built.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();

            // Road info
            EditorGUILayout.LabelField("Road Info", EditorStyles.boldLabel);
            float totalRoadWidth = targetRoad.RoadWidth() + targetRoad.shoulderWidth * 2;
            EditorGUILayout.LabelField($"  Road Width: {targetRoad.RoadWidth()}m ({targetRoad.laneWidth} x {targetRoad.laneAmount} lanes)");
            EditorGUILayout.LabelField($"  Shoulder Width: {targetRoad.shoulderWidth}m each side");
            EditorGUILayout.LabelField($"  Total Road Width: {totalRoadWidth}m");
            EditorGUILayout.LabelField($"  Spline Distance: {targetRoad.spline.distance:F1}m");
            EditorGUILayout.LabelField($"  Node Count: {targetRoad.spline.GetNodeCount()}");

            EditorGUILayout.Space();

            // Section to place walls
            EditorGUILayout.LabelField("Road Section (spline 0-1)", EditorStyles.boldLabel);
            startParam = EditorGUILayout.Slider("Start %", startParam, 0f, 1f);
            endParam = EditorGUILayout.Slider("End %", endParam, 0f, 1f);

            EditorGUILayout.Space();

            // Wall placement settings
            EditorGUILayout.LabelField("Wall Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Offset From Road Edge: Distance from the shoulder edge to where the wall is placed.\n" +
                "Higher values = more offroad space before the wall stops the player.",
                MessageType.Info);
            offsetFromRoadEdge = EditorGUILayout.Slider("Offset From Road Edge", offsetFromRoadEdge, 1f, 50f);

            EditorGUILayout.Space();

            // Wall dimensions
            EditorGUILayout.LabelField("Wall Dimensions", EditorStyles.boldLabel);
            wallHeight = EditorGUILayout.Slider("Wall Height", wallHeight, 5f, 200f);
            wallSegmentLength = EditorGUILayout.Slider("Segment Length", wallSegmentLength, 5f, 50f);
            wallThickness = EditorGUILayout.Slider("Wall Thickness", wallThickness, 0.5f, 5f);
            segmentOverlap = EditorGUILayout.Slider("Segment Overlap", segmentOverlap, 0f, 5f);

            EditorGUILayout.Space();

            // Preview info
            int estimatedSegments = EstimateSegmentCount();
            EditorGUILayout.LabelField($"Estimated segments: ~{estimatedSegments} ({estimatedSegments * 2} walls total, both sides)", EditorStyles.miniLabel);

            EditorGUILayout.Space();

            // Presets
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tight (2m)"))
            {
                offsetFromRoadEdge = 2f;
                wallHeight = 50f;
            }
            if (GUILayout.Button("Medium (8m)"))
            {
                offsetFromRoadEdge = 8f;
                wallHeight = 50f;
            }
            if (GUILayout.Button("Wide (15m)"))
            {
                offsetFromRoadEdge = 15f;
                wallHeight = 50f;
            }
            if (GUILayout.Button("Very Wide (25m)"))
            {
                offsetFromRoadEdge = 25f;
                wallHeight = 50f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Walls", GUILayout.Height(30)))
            {
                GeneratePreview();
                showPreview = true;
            }
            if (GUILayout.Button("Clear Preview", GUILayout.Height(30)))
            {
                previewData.Clear();
                showPreview = false;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("PLACE INVISIBLE WALLS", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Place Invisible Walls",
                    $"This will create invisible wall colliders along both sides of the road from {startParam * 100:F0}% to {endParam * 100:F0}%.\n\n" +
                    $"Offset from road edge: {offsetFromRoadEdge}m\n" +
                    $"Estimated ~{estimatedSegments * 2} wall segments total.\n\n" +
                    "This action can be undone (Ctrl+Z).",
                    "Place", "Cancel"))
                {
                    PlaceWalls();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Remove existing walls option
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Remove ALL Invisible Walls From Scene", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Remove All Invisible Walls",
                    "This will find and remove all GameObjects named 'InvisibleWall' from the scene hierarchy.\n\nThis action can be undone.",
                    "Remove", "Cancel"))
                {
                    RemoveAllWalls();
                }
            }
            GUI.backgroundColor = Color.white;
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!showPreview || previewData == null) return;

        foreach (var wall in previewData)
        {
            Handles.matrix = Matrix4x4.TRS(wall.position, wall.rotation, wall.scale);
            Handles.color = new Color(0f, 0.8f, 1f, 0.4f);
            Handles.DrawWireCube(Vector3.zero, Vector3.one);
        }
        Handles.matrix = Matrix4x4.identity;
    }

    void GeneratePreview()
    {
        previewData.Clear();
        if (targetRoad == null || targetRoad.spline == null) return;
        if (targetRoad.spline.distance <= 0f || wallSegmentLength <= 0f) return;

        var spline = targetRoad.spline;
        float roadHalfWidth = (targetRoad.RoadWidth() + targetRoad.shoulderWidth * 2) / 2f;
        float wallOffset = roadHalfWidth + offsetFromRoadEdge;

        int steps = Mathf.CeilToInt((endParam - startParam) * spline.distance / wallSegmentLength);
        if (steps <= 0) return;

        for (int i = 0; i <= steps; i++)
        {
            float distAlong = i * wallSegmentLength;
            float param = startParam + (distAlong / spline.distance);
            if (param > endParam) break;

            Vector3 pos, tangent;
            spline.GetSplineValueBoth(param, out pos, out tangent);

            if (tangent == Vector3.zero) continue;

            // Calculate perpendicular (right vector in XZ plane)
            Vector3 right = new Vector3(tangent.z, 0, -tangent.x).normalized;

            // Rotation to align with road direction
            Quaternion rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);

            // Place walls on both sides
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 wallPos = pos + right * (side * wallOffset);
                wallPos.y = pos.y; // Keep at road level

                WallPreviewData data = new WallPreviewData();
                data.position = wallPos;
                data.rotation = rotation;
                data.scale = new Vector3(wallThickness, wallHeight, wallSegmentLength + segmentOverlap);
                previewData.Add(data);
            }
        }

        SceneView.RepaintAll();
        Debug.Log($"Preview: {previewData.Count} wall positions generated");
    }

    void PlaceWalls()
    {
        if (targetRoad == null || targetRoad.spline == null)
        {
            Debug.LogError("Road or Spline is null!");
            return;
        }
        if (targetRoad.spline.distance <= 0f || wallSegmentLength <= 0f)
        {
            Debug.LogError("Spline distance or segment length values are invalid (<= 0)!");
            return;
        }

        var spline = targetRoad.spline;
        float roadHalfWidth = (targetRoad.RoadWidth() + targetRoad.shoulderWidth * 2) / 2f;
        float wallOffset = roadHalfWidth + offsetFromRoadEdge;

        // Find or create parent object
        GameObject parentObj = FindOrCreateParent();

        int totalWallsCreated = 0;

        int steps = Mathf.CeilToInt((endParam - startParam) * spline.distance / wallSegmentLength);
        if (steps <= 0)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogWarning("No steps to place walls - check start/end params and segment length.");
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float distAlong = i * wallSegmentLength;
            float param = startParam + (distAlong / spline.distance);
            if (param > endParam) break;

            Vector3 pos, tangent;
            spline.GetSplineValueBoth(param, out pos, out tangent);

            if (tangent == Vector3.zero) continue;

            // Calculate perpendicular (right vector in XZ plane)
            Vector3 right = new Vector3(tangent.z, 0, -tangent.x).normalized;

            // Rotation to align with road direction
            Quaternion rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);

            // Place walls on both sides
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 wallPos = pos + right * (side * wallOffset);
                wallPos.y = pos.y; // Keep at road level

                GameObject wallObj = new GameObject($"InvisibleWall");
                wallObj.transform.SetParent(parentObj.transform);
                wallObj.transform.position = wallPos;
                wallObj.transform.rotation = rotation;
                wallObj.transform.localScale = new Vector3(wallThickness, wallHeight, wallSegmentLength + segmentOverlap);

                BoxCollider col = wallObj.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.size = Vector3.one;
                col.center = Vector3.zero;

                totalWallsCreated++;
            }

            // Progress bar
            if (i % 20 == 0)
            {
                float progress = (float)i / steps;
                if (EditorUtility.DisplayCancelableProgressBar("Placing Invisible Walls",
                    $"Creating wall segments... {totalWallsCreated} placed",
                    progress))
                {
                    break;
                }
            }
        }

        EditorUtility.ClearProgressBar();

        Debug.Log($"Created {totalWallsCreated} invisible wall segments along the road (param {startParam:F2} - {endParam:F2})");
    }

    GameObject FindOrCreateParent()
    {
        // Look for existing "InvisibleWalls" parent in scene
        GameObject existing = GameObject.Find("InvisibleWalls");
        if (existing != null) return existing;

        // Create new parent
        GameObject parent = new GameObject("InvisibleWalls");
        Undo.RegisterCreatedObjectUndo(parent, "Create InvisibleWalls Parent");
        return parent;
    }

    void RemoveAllWalls()
    {
        // Find all InvisibleWall objects and delete them
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int removed = 0;

        foreach (var obj in allObjects)
        {
            if (obj.name.StartsWith("InvisibleWall"))
            {
                Undo.DestroyObjectImmediate(obj);
                removed++;
            }
        }

        // Also try to remove the parent if it's empty
        GameObject parent = GameObject.Find("InvisibleWalls");
        if (parent != null && parent.transform.childCount == 0)
        {
            Undo.DestroyObjectImmediate(parent);
        }

        Debug.Log($"Removed {removed} invisible wall objects from scene.");
    }

    void OnDisable()
    {
        EditorUtility.ClearProgressBar();
    }

    private int EstimateSegmentCount()
    {
        if (targetRoad == null || targetRoad.spline == null) return 0;
        float splineDist = targetRoad.spline.distance;
        if (splineDist <= 0f || wallSegmentLength <= 0f) return 0;

        float sectionLength = (endParam - startParam) * splineDist;
        return Mathf.CeilToInt(sectionLength / wallSegmentLength);
    }
}
