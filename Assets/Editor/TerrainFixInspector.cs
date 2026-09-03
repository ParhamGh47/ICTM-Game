using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for Terrain that prevents DivideByZeroException and Invalid GUILayout state
/// caused by Unity's built-in TerrainInspector.GetAspectRect when treePrototypes.Length == 0.
///
/// Instead of letting DrawDefaultInspector crash mid-draw (which corrupts GUI state and
/// produces BOTH errors), we detect the problematic condition upfront and draw properties
/// safely via SerializedProperty iteration.
/// </summary>
[CustomEditor(typeof(Terrain))]
[CanEditMultipleObjects]
public class TerrainFixInspector : Editor
{
    private bool _useSafeDraw = false;

    void OnEnable()
    {
        // Detect the problematic condition upfront so we never trigger the crash
        Terrain terrain = target as Terrain;
        if (terrain != null && terrain.terrainData != null)
        {
            _useSafeDraw = terrain.terrainData.treePrototypes.Length == 0;
        }
    }

    public override void OnInspectorGUI()
    {
        if (_useSafeDraw)
        {
            DrawSafeInspector();
        }
        else
        {
            // Terrain has prototypes — safe to use default inspector.
            // Still wrap in try/catch as a safety net.
            try
            {
                DrawDefaultInspector();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"TerrainInspector: exception during default draw ({ex.GetType().Name}). Switching to safe mode.");
                _useSafeDraw = true;
                DrawSafeInspector();
            }
        }
    }

    /// <summary>
    /// Draw terrain properties by iterating SerializedObject.
    /// This avoids the broken GetAspectRect / AspectSelectionGridImageAndText code path.
    /// </summary>
    private void DrawSafeInspector()
    {
        serializedObject.Update();

        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == "m_Script") continue;

            // Skip tree prototypes section when empty — this triggers a Unity bug
            if (prop.name == "m_TreePrototypes")
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(prop.displayName, EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Cannot display tree prototypes in this view (0 prototypes causes a Unity bug).\n" +
                    "Use the Terrain Inspector > Trees section on the Terrain Data asset to add tree prefabs.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                continue;
            }

            try
            {
                EditorGUILayout.PropertyField(prop, true);
            }
            catch (System.Exception ex)
            {
                EditorGUILayout.HelpBox(
                    $"Error drawing '{prop.displayName}': {ex.Message}",
                    MessageType.Warning);
            }
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            SceneView.RepaintAll();
        }
    }

    void OnDisable()
    {
        _useSafeDraw = false;
    }
}
