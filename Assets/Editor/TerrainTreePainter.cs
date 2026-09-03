using UnityEngine;
using UnityEditor;
using RoadArchitect;
using System.Collections.Generic;

/// <summary>
/// Editor tool to paint trees along the road sides on the Level 4 terrain.
/// Uses the RoadArchitect spline to follow the road path and places trees
/// at configured offsets on both sides.
/// 
/// Usage: Open Level 4 scene in Unity Editor, then go to menu:
/// Tools > Road Tools > Paint Trees Along Road
/// </summary>
public class TerrainTreePainter : EditorWindow
{
    private Road targetRoad;
    private Terrain targetTerrain;
    
    // Section of road to paint (spline param 0-1)
    private float startParam = 0.15f; // Skip beginning where trees already exist
    private float endParam = 1.0f;
    
    // Offset from road center to start placing trees
    private float minOffsetFromRoad = 12f; // Just beyond shoulder
    private float maxOffsetFromRoad = 80f; // How far from road to place trees
    
    // Spacing
    private float spacingAlongRoad = 8f; // Distance between tree positions along road
    private float spacingAcrossRoad = 12f; // Distance between trees across the road side
    
    // Tree prototype selection
    private bool[] selectedPrototypes;
    private bool prototypesInitialized = false;
    
    // Randomization
    private float randomOffset = 3f; // Random offset from calculated positions
    private float randomScaleMin = 0.8f;
    private float randomScaleMax = 1.2f;
    
    // Preview
    private bool showPreview = false;
    private List<Vector3> previewPositions = new List<Vector3>();
    
    [MenuItem("Tools/Road Tools/Paint Trees Along Road")]
    static void OpenWindow()
    {
        var window = GetWindow<TerrainTreePainter>("Tree Painter");
        window.minSize = new Vector2(350, 500);
        window.Show();
    }

    void OnEnable()
    {
        // Auto-find road and terrain in scene
        if (targetRoad == null)
        {
            var roads = FindObjectsOfType<Road>();
            if (roads.Length > 0)
            {
                targetRoad = roads[0];
            }
        }
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
        }
    }

    private Vector2 scrollPos;

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        try
        {
        GUILayout.Label("Paint Trees Along Road", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // References
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        targetRoad = (Road)EditorGUILayout.ObjectField("Road", targetRoad, typeof(Road), true);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain", targetTerrain, typeof(Terrain), true);
        
        if (targetRoad == null || targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Please assign both a Road and Terrain reference.", MessageType.Warning);
            return;
        }
        
        if (targetRoad.spline == null)
        {
            EditorGUILayout.HelpBox("Road spline not found. Make sure the road is built.", MessageType.Warning);
            return;
        }
        if (targetRoad.spline.distance <= 0f)
        {
            EditorGUILayout.HelpBox("Spline distance is 0. Make sure the road spline is properly built.", MessageType.Warning);
            return;
        }
        
        if (targetTerrain.terrainData.treePrototypes.Length == 0)
        {
            EditorGUILayout.HelpBox("Terrain has no tree prototypes defined. Add at least one tree prefab to the Terrain Data > Trees section.", MessageType.Error);
            return;
        }
        
        EditorGUILayout.Space();
        
        // Road info
        EditorGUILayout.LabelField("Road Info", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  Road Width: {targetRoad.RoadWidth()}m ({targetRoad.laneWidth} x {targetRoad.laneAmount} lanes)");
        EditorGUILayout.LabelField($"  Shoulder Width: {targetRoad.shoulderWidth}m each side");
        EditorGUILayout.LabelField($"  Total Road Width: {targetRoad.RoadWidth() + targetRoad.shoulderWidth * 2}m");
        EditorGUILayout.LabelField($"  Spline Distance: {targetRoad.spline.distance:F1}m");
        EditorGUILayout.LabelField($"  Node Count: {targetRoad.spline.GetNodeCount()}");
        
        // Terrain info
        var td = targetTerrain.terrainData;
        EditorGUILayout.LabelField($"  Terrain Size: {td.size.x}x{td.size.y}x{td.size.z}");
        
        // Initialize prototype toggles if needed
        if (selectedPrototypes == null || selectedPrototypes.Length != td.treePrototypes.Length)
        {
            selectedPrototypes = new bool[td.treePrototypes.Length];
            for (int i = 0; i < selectedPrototypes.Length; i++)
                selectedPrototypes[i] = true; // All selected by default
            prototypesInitialized = true;
        }
        
        EditorGUILayout.Space();
        
        // Section to paint
        EditorGUILayout.LabelField("Road Section (spline 0-1)", EditorStyles.boldLabel);
        startParam = EditorGUILayout.Slider("Start %", startParam, 0f, 1f);
        endParam = EditorGUILayout.Slider("End %", endParam, 0f, 1f);
        
        EditorGUILayout.Space();
        
        // Tree placement settings
        EditorGUILayout.LabelField("Tree Placement", EditorStyles.boldLabel);
        minOffsetFromRoad = EditorGUILayout.Slider("Min Offset From Road", minOffsetFromRoad, 5f, 50f);
        maxOffsetFromRoad = EditorGUILayout.Slider("Max Offset From Road", maxOffsetFromRoad, minOffsetFromRoad + 1f, 200f);
        
        EditorGUILayout.Space();
        
        // Density presets
        EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sparse")) { spacingAlongRoad = 15f; spacingAcrossRoad = 18f; }
        if (GUILayout.Button("Normal")) { spacingAlongRoad = 8f; spacingAcrossRoad = 12f; }
        if (GUILayout.Button("Dense")) { spacingAlongRoad = 4f; spacingAcrossRoad = 8f; }
        if (GUILayout.Button("Very Dense")) { spacingAlongRoad = 2f; spacingAcrossRoad = 5f; }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField($"Current: ~{EstimateTreeCount():N0} trees will be placed", EditorStyles.miniLabel);
        spacingAlongRoad = EditorGUILayout.Slider("Along Road Spacing", spacingAlongRoad, 1f, 30f);
        spacingAcrossRoad = EditorGUILayout.Slider("Across Road Spacing", spacingAcrossRoad, 2f, 30f);
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
        randomOffset = EditorGUILayout.FloatField("Position Random Offset (m)", randomOffset);
        randomScaleMin = EditorGUILayout.Slider("Scale Min", randomScaleMin, 0.1f, 2f);
        randomScaleMax = EditorGUILayout.Slider("Scale Max", randomScaleMax, 0.1f, 2f);
        
        EditorGUILayout.Space();
        
        // Tree prototype selection
        EditorGUILayout.LabelField("Select Tree Types to Paint", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All", GUILayout.Width(40)))
        {
            for (int i = 0; i < selectedPrototypes.Length; i++)
                selectedPrototypes[i] = true;
        }
        if (GUILayout.Button("None", GUILayout.Width(50)))
        {
            for (int i = 0; i < selectedPrototypes.Length; i++)
                selectedPrototypes[i] = false;
        }
        EditorGUILayout.EndHorizontal();
        for (int i = 0; i < td.treePrototypes.Length; i++)
        {
            string prefabName = td.treePrototypes[i].prefab != null ? td.treePrototypes[i].prefab.name : "null";
            EditorGUILayout.BeginHorizontal();
            selectedPrototypes[i] = EditorGUILayout.ToggleLeft($"  [{i}] {prefabName}", selectedPrototypes[i]);
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.Space();
        
        // Buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Positions", GUILayout.Height(30)))
        {
            GeneratePreview();
            showPreview = true;
        }
        if (GUILayout.Button("Clear Preview", GUILayout.Height(30)))
        {
            previewPositions.Clear();
            showPreview = false;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT TREES", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Paint Trees",
                $"This will add tree instances to the terrain along the road from {startParam * 100:F0}% to {endParam * 100:F0}%.\n\nThis action can be undone (Ctrl+Z).",
                "Paint", "Cancel"))
            {
                PaintTrees();
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space();
        
        // Clear all trees option
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove ALL Trees From Terrain", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Remove All Trees",
                "This will remove ALL tree instances from the terrain. This action can be undone.",
                "Remove", "Cancel"))
            {
                RemoveAllTrees();
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
        if (!showPreview || previewPositions == null) return;
        
        Handles.color = new Color(0f, 1f, 0f, 0.6f);
        foreach (var pos in previewPositions)
        {
            Handles.DrawWireDisc(pos, Vector3.up, 1f);
        }
    }

    void GeneratePreview()
    {
        previewPositions.Clear();
        if (targetRoad == null || targetRoad.spline == null) return;
        if (targetRoad.spline.distance <= 0f || spacingAlongRoad <= 0f || spacingAcrossRoad <= 0f) return;
        
        var spline = targetRoad.spline;
        var td = targetTerrain.terrainData;
        float roadHalfWidth = (targetRoad.RoadWidth() + targetRoad.shoulderWidth * 2) / 2f;
        
        int steps = Mathf.CeilToInt((endParam - startParam) * spline.distance / spacingAlongRoad);
        if (steps <= 0) return;
        
        for (int i = 0; i <= steps; i++)
        {
            float distAlong = (i * spacingAlongRoad);
            float param = startParam + (distAlong / spline.distance);
            if (param > endParam) break;
            
            Vector3 pos, tangent;
            spline.GetSplineValueBoth(param, out pos, out tangent);
            
            if (tangent == Vector3.zero) continue;
            
            // Calculate perpendicular (right vector in XZ plane)
            Vector3 right = new Vector3(tangent.z, 0, -tangent.x).normalized;
            
            // Place trees on both sides
            for (int side = -1; side <= 1; side += 2)
            {
                float offset = roadHalfWidth + minOffsetFromRoad;
                while (offset < roadHalfWidth + maxOffsetFromRoad)
                {
                    Vector3 treePos = pos + right * (side * offset);
                    treePos.y = td.GetInterpolatedHeight(
                        treePos.x / td.size.x,
                        treePos.z / td.size.z
                    );
                    previewPositions.Add(treePos);
                    offset += spacingAcrossRoad;
                }
            }
        }
        
        SceneView.RepaintAll();
        Debug.Log($"Preview: {previewPositions.Count} positions generated");
    }

    void PaintTrees()
    {
        if (targetRoad == null || targetRoad.spline == null || targetTerrain == null)
        {
            Debug.LogError("Road, Spline, or Terrain is null!");
            return;
        }
        if (targetRoad.spline.distance <= 0f || spacingAlongRoad <= 0f || spacingAcrossRoad <= 0f)
        {
            Debug.LogError("Spline distance or spacing values are invalid (<= 0)!");
            return;
        }
        
        var spline = targetRoad.spline;
        var td = targetTerrain.terrainData;
        
        if (td.treePrototypes.Length == 0)
        {
            Debug.LogError("No tree prototypes found on the terrain! Add tree prefabs to the terrain first.");
            return;
        }
        
        // Determine which prototypes to use from toggle states
        List<int> prototypeIndices = new List<int>();
        if (selectedPrototypes != null)
        {
            for (int i = 0; i < td.treePrototypes.Length && i < selectedPrototypes.Length; i++)
            {
                if (selectedPrototypes[i])
                    prototypeIndices.Add(i);
            }
        }
        
        if (prototypeIndices.Count == 0)
        {
            Debug.LogError("No tree prototypes selected! Toggle at least one in the inspector.");
            return;
        }
        
        float roadHalfWidth = (targetRoad.RoadWidth() + targetRoad.shoulderWidth * 2) / 2f;
        int totalTreesAdded = 0;
        
        // Store original tree count for undo
        TreeInstance[] originalTrees = td.treeInstances;
        
        List<TreeInstance> newTrees = new List<TreeInstance>();
        newTrees.AddRange(td.treeInstances);
        
        int steps = Mathf.CeilToInt((endParam - startParam) * spline.distance / spacingAlongRoad);
        if (steps <= 0) { EditorUtility.ClearProgressBar(); Debug.LogWarning("No steps to paint - check start/end params and spacing."); return; }
        System.Random rng = new System.Random();
        
        for (int i = 0; i <= steps; i++)
        {
            float distAlong = (i * spacingAlongRoad);
            float param = startParam + (distAlong / spline.distance);
            if (param > endParam) break;
            
            Vector3 pos, tangent;
            spline.GetSplineValueBoth(param, out pos, out tangent);
            
            if (tangent == Vector3.zero) continue;
            
            // Calculate perpendicular (right vector in XZ plane)
            Vector3 right = new Vector3(tangent.z, 0, -tangent.x).normalized;
            
            // Place trees on both sides
            for (int side = -1; side <= 1; side += 2)
            {
                float offset = roadHalfWidth + minOffsetFromRoad;
                while (offset < roadHalfWidth + maxOffsetFromRoad)
                {
                    Vector3 treePos = pos + right * (side * offset);
                    
                    // Add random offset
                    treePos.x += (float)(rng.NextDouble() * 2 - 1) * randomOffset;
                    treePos.z += (float)(rng.NextDouble() * 2 - 1) * randomOffset;
                    
                    // Get terrain height at this position
                    float normalizedX = treePos.x / td.size.x;
                    float normalizedZ = treePos.z / td.size.z;
                    
                    // Skip if outside terrain bounds
                    if (normalizedX < 0 || normalizedX > 1 || normalizedZ < 0 || normalizedZ > 1)
                    {
                        offset += spacingAcrossRoad;
                        continue;
                    }
                    
                    treePos.y = td.GetInterpolatedHeight(normalizedX, normalizedZ);
                    
                    // Random prototype
                    int protoIdx = prototypeIndices[rng.Next(prototypeIndices.Count)];
                    
                    // Random scale
                    float scale = (float)(rng.NextDouble() * (randomScaleMax - randomScaleMin) + randomScaleMin);
                    
                    TreeInstance tree = new TreeInstance();
                    tree.position = new Vector3(normalizedX, treePos.y / td.size.y, normalizedZ);
                    tree.prototypeIndex = protoIdx;
                    tree.widthScale = scale;
                    tree.heightScale = scale;
                    tree.color = Color.white;
                    tree.lightmapColor = Color.white;
                    
                    newTrees.Add(tree);
                    totalTreesAdded++;
                    
                    offset += spacingAcrossRoad;
                }
            }
            
            // Progress bar
            if (i % 50 == 0)
            {
                float progress = (float)i / steps;
                if (EditorUtility.DisplayCancelableProgressBar("Painting Trees",
                    $"Placing trees... {totalTreesAdded} placed",
                    progress))
                {
                    break;
                }
            }
        }
        
        EditorUtility.ClearProgressBar();
        
        // Apply
        td.treeInstances = newTrees.ToArray();
        
        Debug.Log($"Painted {totalTreesAdded} trees along the road (param {startParam:F2} - {endParam:F2})");
        EditorUtility.SetDirty(td);
    }

    void RemoveAllTrees()
    {
        if (targetTerrain == null) return;
        var td = targetTerrain.terrainData;
        td.treeInstances = new TreeInstance[0];
        EditorUtility.SetDirty(td);
        Debug.Log("All tree instances removed from terrain.");
    }

    void OnDisable()
    {
        EditorUtility.ClearProgressBar();
    }

    private int EstimateTreeCount()
    {
        if (targetRoad == null || targetRoad.spline == null) return 0;
        float splineDist = targetRoad.spline.distance;
        if (splineDist <= 0f || spacingAlongRoad <= 0f || spacingAcrossRoad <= 0f) return 0;
        
        float roadHalfWidth = (targetRoad.RoadWidth() + targetRoad.shoulderWidth * 2) / 2f;
        float sectionLength = (endParam - startParam) * splineDist;
        int stepsAlong = Mathf.CeilToInt(sectionLength / spacingAlongRoad);
        float acrossRange = (maxOffsetFromRoad - minOffsetFromRoad);
        int stepsAcross = Mathf.Max(1, Mathf.CeilToInt(acrossRange / spacingAcrossRoad));
        return stepsAlong * stepsAcross * 2; // x2 for both sides
    }
}
