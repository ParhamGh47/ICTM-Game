using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Everything the game saves, in one window.
///
/// There is no save file to open: all of it lives in <see cref="PlayerPrefs"/>, which on Windows is the
/// registry and on macOS a plist - readable, but not something to hand-edit nine paint colours into. This is
/// the editor's way in. It reads and writes the same keys the game does, so what it shows is what the game
/// will load, and the buttons that change a lot at once go through the game's own store
/// (<c>LevelProgress</c>, <c>TruckPaint</c>, <c>GraphicsQuality</c>, <c>GameDifficulty</c>, <c>SoundSettings</c>)
/// so its in-memory copy cannot fall out of step with the saved one.
///
/// The keys themselves are repeated here rather than read from the game: the game scripts live in an assembly
/// this one cannot reference, and the prefixes are a storage contract one file owns (the "storage" block of
/// <c>TruckPaint</c> and <c>LevelProgress</c>). They are listed in one place below, so if a prefix is ever
/// renamed, this is the second place to change.
/// </summary>
public class SaveDataWindow : EditorWindow
{
    // ---------------------------------------------------------------- the keys

    private const string UnlockedKey = "LevelProgress.Unlocked";
    private const string ClearedKey = "LevelProgress.Cleared";

    private const string PaintedPrefix = "TruckPaint.Painted.";
    private const string ColourPrefix = "TruckPaint.Colour.";
    private const string StylePrefix = "TruckPaint.Style.";

    private const string PresetKey = "Graphics.Preset";
    private const string ShadowsKey = "Graphics.Shadows";
    private const string MotionBlurKey = "Graphics.MotionBlur";

    private const string DifficultyKey = "Game.Difficulty";

    private const string CameraMixKey = "Sound.CameraMix";

    /// <summary>
    /// The sound channels' keys, in the order of the game's own <c>SoundChannel</c> enum: master, soundtrack,
    /// engine, effects, environment. The names beside them are what the settings screens label them with, and
    /// the steps are what the game's <c>SoundLevel</c> enum holds.
    /// </summary>
    private static readonly string[] SoundKeys =
    {
        "Sound.Master",
        "Sound.Soundtrack",
        "Sound.Engine",
        "Sound.Effects",
        "Sound.Environment",
    };

    private static readonly string[] ChannelNames =
    {
        "Master", "Soundtrack", "Engine", "Effects", "Environment",
    };

    private static readonly string[] LevelNames = { "Muted", "Low", "Medium", "High" };

    private static readonly string[] PresetNames = { "Low", "Medium", "High" };

    private static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard" };

    // ---------------------------------------------------------------- state

    private int unlocked = 1;
    private int cleared;

    private string[] partNames = new string[0];
    private bool[] painted = new bool[0];
    private Color[] colours = new Color[0];
    private int[] styles = new int[0];
    private string[] styleNames = new string[0];

    private int preset = 2;             // High, which is what the game runs at until it is changed
    private bool shadows = true;
    private bool motionBlur = true;

    private int difficulty = 1;         // Medium, which is the game exactly as the levels were authored

    // Every channel starts at High: the game as it was built to sound, before anyone touches the settings.
    private int[] soundLevels = { 3, 3, 3, 3, 3 };
    private bool cameraMix = true;

    private Vector2 scroll;

    // ---------------------------------------------------------------- menu

    [MenuItem("Tools/Save Data/Open Save Editor", false, 10)]
    private static void Open()
    {
        SaveDataWindow window = GetWindow<SaveDataWindow>("Save Data");
        window.minSize = new Vector2(470f, 420f);
        window.Reload();
    }

    [MenuItem("Tools/Save Data/Print Saved Data", false, 20)]
    private static void PrintSavedData()
    {
        Debug.Log("[Save] " + Report());
    }

    [MenuItem("Tools/Save Data/Where Is It Saved", false, 21)]
    private static void PrintLocation()
    {
        Debug.Log("[Save] " + Location());
    }

    private void OnEnable()
    {
        Reload();
    }

    // ---------------------------------------------------------------- reading

    /// <summary>Reads everything back out of PlayerPrefs, so the window always starts from what is saved.</summary>
    private void Reload()
    {
        unlocked = Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));
        cleared = Mathf.Max(0, PlayerPrefs.GetInt(ClearedKey, 0));

        Type partType = TypeByName("TruckPart");

        if (partType == null)
        {
            partNames = new string[0];
            painted = new bool[0];
            colours = new Color[0];
            styles = new int[0];
        }
        else
        {
            partNames = Enum.GetNames(partType);

            painted = new bool[partNames.Length];
            colours = new Color[partNames.Length];
            styles = new int[partNames.Length];

            for (int i = 0; i < partNames.Length; i++)
            {
                string part = partNames[i];

                painted[i] = PlayerPrefs.GetInt(PaintedPrefix + part, 0) != 0;
                colours[i] = ParseColour(PlayerPrefs.GetString(ColourPrefix + part, ""),
                                         new Color(0.82f, 0.14f, 0.13f));
                styles[i] = PlayerPrefs.GetInt(StylePrefix + part, 0);
            }
        }

        styleNames = StyleLabels();
        if (styleNames.Length == 0) styleNames = new[] { "PAINT" };

        for (int i = 0; i < styles.Length; i++)
            styles[i] = Mathf.Clamp(styles[i], 0, styleNames.Length - 1);

        preset = Mathf.Clamp(PlayerPrefs.GetInt(PresetKey, 2), 0, PresetNames.Length - 1);
        shadows = PlayerPrefs.GetInt(ShadowsKey, 1) != 0;
        motionBlur = PlayerPrefs.GetInt(MotionBlurKey, 1) != 0;

        difficulty = Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 1), 0, DifficultyNames.Length - 1);

        for (int i = 0; i < SoundKeys.Length; i++)
            soundLevels[i] = Mathf.Clamp(PlayerPrefs.GetInt(SoundKeys[i], 3), 0, LevelNames.Length - 1);

        cameraMix = PlayerPrefs.GetInt(CameraMixKey, 1) != 0;
    }

    private static string[] StyleLabels()
    {
        // The finishes are the game's own list, read rather than repeated: PAINT, GLOSS, MATTE, METALLIC,
        // CHROME, GLASS.
        object value = StaticField("TruckPaint", "StyleLabels");
        string[] labels = value as string[];

        return labels ?? new string[0];
    }

    // ---------------------------------------------------------------- the window

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Everything the game saves lives in PlayerPrefs - there is no save file. " +
            "Progress, the truck's paint and every setting the player can change are all one save, shared by " +
            "every level and every profile. Changes here are read the next time the game loads them: stop and " +
            "start play mode, or reload the scene.", MessageType.Info);

        Section("Level progress", DrawProgress);
        Section("Truck paint", DrawPaint);
        Section("Graphics", DrawGraphics);
        Section("Difficulty", DrawDifficulty);
        Section("Sound", DrawSound);
        Section("Everything", DrawDangerZone);

        EditorGUILayout.EndScrollView();
    }

    private void Section(string title, Action body)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        body();
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(2f);
    }

    private void DrawProgress()
    {
        EditorGUI.BeginChangeCheck();

        unlocked = EditorGUILayout.IntSlider(
            new GUIContent("Unlocked up to", "The furthest level that can be opened. 1 is a new player."),
            unlocked, 1, 6);

        cleared = EditorGUILayout.IntSlider(
            new GUIContent("Finished up to", "How far the player has got."), cleared, 0, 6);

        if (EditorGUI.EndChangeCheck())
        {
            PlayerPrefs.SetInt(UnlockedKey, unlocked);
            PlayerPrefs.SetInt(ClearedKey, cleared);
            PlayerPrefs.Save();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Open every level")) CallStatic("LevelProgress", "UnlockAll");
        if (GUILayout.Button("Reset (as a new player)")) CallStatic("LevelProgress", "Reset");

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Pull the saved values back into this window")) Reload();
    }

    private void DrawPaint()
    {
        if (partNames.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "The game scripts have not been built yet, so the parts of the truck cannot be listed.",
                MessageType.Warning);
            return;
        }

        for (int i = 0; i < partNames.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();

            string part = partNames[i];

            EditorGUI.BeginChangeCheck();

            painted[i] = EditorGUILayout.Toggle(painted[i], GUILayout.Width(18f));
            EditorGUILayout.LabelField(part, GUILayout.Width(96f));

            using (new EditorGUI.DisabledScope(!painted[i]))
            {
                colours[i] = EditorGUILayout.ColorField(colours[i], GUILayout.Width(64f));
                styles[i] = EditorGUILayout.Popup(styles[i], styleNames);
            }

            bool changed = EditorGUI.EndChangeCheck();

            if (changed) WritePart(part, i);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Paint every part this colour")) PaintAll();
        if (GUILayout.Button("Clear all paint")) CallStatic("TruckPaint", "ResetAll");

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGraphics()
    {
        EditorGUI.BeginChangeCheck();

        preset = EditorGUILayout.Popup(
            new GUIContent("Preset", "How much of the picture the game spends. High is the default."),
            Mathf.Clamp(preset, 0, PresetNames.Length - 1), PresetNames);

        shadows = EditorGUILayout.Toggle("Shadows", shadows);
        motionBlur = EditorGUILayout.Toggle("Motion blur", motionBlur);

        if (EditorGUI.EndChangeCheck())
        {
            PlayerPrefs.SetInt(PresetKey, preset);
            PlayerPrefs.SetInt(ShadowsKey, shadows ? 1 : 0);
            PlayerPrefs.SetInt(MotionBlurKey, motionBlur ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (GUILayout.Button("Back to what a new player gets")) CallStatic("GraphicsQuality", "ResetToDefaults");
    }

    private void DrawDifficulty()
    {
        EditorGUI.BeginChangeCheck();

        difficulty = EditorGUILayout.Popup(
            new GUIContent("Level", "What a level's time limit and kill requirement are scaled by. Medium is " +
                                  "the game exactly as the levels were authored, and is the default."),
            Mathf.Clamp(difficulty, 0, DifficultyNames.Length - 1), DifficultyNames);

        if (EditorGUI.EndChangeCheck())
        {
            PlayerPrefs.SetInt(DifficultyKey, difficulty);
            PlayerPrefs.Save();
        }

        if (GUILayout.Button("Back to what a new player gets")) CallStatic("GameDifficulty", "ResetToDefaults");
    }

    private void DrawSound()
    {
        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < SoundKeys.Length; i++)
        {
            soundLevels[i] = EditorGUILayout.Popup(
                new GUIContent(ChannelNames[i], ChannelTooltip(i)),
                Mathf.Clamp(soundLevels[i], 0, LevelNames.Length - 1), LevelNames);
        }

        cameraMix = EditorGUILayout.Toggle(
            new GUIContent("Camera mix", "Whether the engine is re-balanced for the camera the player is " +
                                          "driving from, so it keeps its place in the mix from any of them."),
            cameraMix);

        if (EditorGUI.EndChangeCheck())
        {
            for (int i = 0; i < SoundKeys.Length; i++)
                PlayerPrefs.SetInt(SoundKeys[i], soundLevels[i]);

            PlayerPrefs.SetInt(CameraMixKey, cameraMix ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (GUILayout.Button("Back to what a new player gets")) CallStatic("SoundSettings", "ResetToDefaults");
    }

    /// <summary>What each channel is, in the same words the game's own settings screens use.</summary>
    private static string ChannelTooltip(int channel)
    {
        switch (channel)
        {
            case 0: return "Everything at once, the way a volume knob works.";
            case 1: return "The music: the menu song, a level's own track, the pause and game over songs.";
            case 2: return "The player's own truck: the engine, its gear shifts and its exhaust.";
            case 3: return "Everything the truck hits or is told to do: crashes, the horn, the targets.";
            default: return "The world around the road: the rain, sirens, whatever a level puts out there.";
        }
    }

    private void DrawDangerZone()
    {
        if (GUILayout.Button("Print everything to the console")) Debug.Log("[Save] " + Report());

        if (GUILayout.Button("Where is it saved?")) Debug.Log("[Save] " + Location());

        EditorGUILayout.Space(4f);

        if (GUILayout.Button("Delete every saved value"))
        {
            bool go = EditorUtility.DisplayDialog(
                "Delete saved data",
                "Forget the level progress, the truck's paint, the graphics and difficulty settings and the " +
                "sound settings?\n\n" +
                "This is the player's saved data and it cannot be undone.",
                "Delete",
                "Cancel");

            if (!go) return;

            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(ClearedKey);
            PlayerPrefs.DeleteKey(PresetKey);
            PlayerPrefs.DeleteKey(ShadowsKey);
            PlayerPrefs.DeleteKey(MotionBlurKey);
            PlayerPrefs.DeleteKey(DifficultyKey);

            for (int i = 0; i < SoundKeys.Length; i++)
                PlayerPrefs.DeleteKey(SoundKeys[i]);

            PlayerPrefs.DeleteKey(CameraMixKey);

            for (int i = 0; i < partNames.Length; i++)
                ClearPart(partNames[i]);

            PlayerPrefs.Save();

            Debug.Log("[Save] Everything saved has been deleted, so the game starts as it does for a new " +
                      "player.");
        }
    }

    // ---------------------------------------------------------------- writing

    private void WritePart(string part, int index)
    {
        if (painted[index])
        {
            PlayerPrefs.SetInt(PaintedPrefix + part, 1);
            PlayerPrefs.SetString(ColourPrefix + part, ColorUtility.ToHtmlStringRGB(colours[index]));
            PlayerPrefs.SetInt(StylePrefix + part, styles[index]);
        }
        else
        {
            ClearPart(part);
        }

        PlayerPrefs.Save();
    }

    private void PaintAll()
    {
        for (int i = 0; i < partNames.Length; i++)
        {
            painted[i] = true;

            PlayerPrefs.SetInt(PaintedPrefix + partNames[i], 1);
            PlayerPrefs.SetString(ColourPrefix + partNames[i], ColorUtility.ToHtmlStringRGB(colours[i]));
            PlayerPrefs.SetInt(StylePrefix + partNames[i], styles[i]);
        }

        PlayerPrefs.Save();
    }

    private static void ClearPart(string part)
    {
        PlayerPrefs.DeleteKey(PaintedPrefix + part);
        PlayerPrefs.DeleteKey(ColourPrefix + part);
        PlayerPrefs.DeleteKey(StylePrefix + part);
    }

    private static Color ParseColour(string text, Color fallback)
    {
        Color colour;

        if (!string.IsNullOrEmpty(text) && ColorUtility.TryParseHtmlString("#" + text, out colour)) return colour;

        return fallback;
    }

    // ---------------------------------------------------------------- reporting

    /// <summary>Everything saved, as one line of text - what "Print Saved Data" logs.</summary>
    private static string Report()
    {
        int unlocked = PlayerPrefs.GetInt(UnlockedKey, 1);
        int cleared = PlayerPrefs.GetInt(ClearedKey, 0);

        List<string> parts = new List<string>();

        Type partType = TypeByName("TruckPart");

        if (partType != null)
        {
            string[] names = Enum.GetNames(partType);
            string[] styles = StyleLabels();

            for (int i = 0; i < names.Length; i++)
            {
                if (PlayerPrefs.GetInt(PaintedPrefix + names[i], 0) == 0) continue;

                string hex = PlayerPrefs.GetString(ColourPrefix + names[i], "?");
                int style = PlayerPrefs.GetInt(StylePrefix + names[i], 0);

                string finish = styles.Length > 0
                    ? styles[Mathf.Clamp(style, 0, styles.Length - 1)]
                    : style.ToString();

                parts.Add(names[i] + " #" + hex + " " + finish);
            }
        }

        int preset = PlayerPrefs.GetInt(PresetKey, 2);
        string presetName = PresetNames[Mathf.Clamp(preset, 0, PresetNames.Length - 1)];

        int difficulty = PlayerPrefs.GetInt(DifficultyKey, 1);
        string difficultyName = DifficultyNames[Mathf.Clamp(difficulty, 0, DifficultyNames.Length - 1)];

        List<string> sound = new List<string>();

        for (int i = 0; i < SoundKeys.Length; i++)
        {
            int level = PlayerPrefs.GetInt(SoundKeys[i], 3);

            sound.Add(ChannelNames[i] + " " + LevelNames[Mathf.Clamp(level, 0, LevelNames.Length - 1)]);
        }

        return string.Format(
            "\n  {0}\n" +
            "  progress: unlocked up to {1}, finished up to {2}\n" +
            "  paint: {3}\n" +
            "  graphics: {4}, shadows {5}, motion blur {6}\n" +
            "  difficulty: {7}\n" +
            "  sound: {8}, camera mix {9}",
            Location(), unlocked, cleared,
            parts.Count == 0 ? "(nothing repainted)" : string.Join(" | ", parts.ToArray()),
            presetName,
            PlayerPrefs.GetInt(ShadowsKey, 1) != 0 ? "on" : "off",
            PlayerPrefs.GetInt(MotionBlurKey, 1) != 0 ? "on" : "off",
            difficultyName,
            string.Join(", ", sound.ToArray()),
            PlayerPrefs.GetInt(CameraMixKey, 1) != 0 ? "on" : "off");
    }

    /// <summary>
    /// Where PlayerPrefs are kept on this machine.
    ///
    /// Worth printing rather than assuming: it is the registry on Windows and a plist on macOS, and the editor
    /// and a built player use *different* keys on Windows - the editor's sit under Unity\UnityEditor, so a
    /// build's saved data is not the one being edited here.
    /// </summary>
    private static string Location()
    {
        string company = PlayerSettings.companyName;
        string product = PlayerSettings.productName;

#if UNITY_EDITOR_WIN
        return "PlayerPrefs are in the Windows registry. Editor:\n" +
               "  HKEY_CURRENT_USER\\Software\\Unity\\UnityEditor\\" + company + "\\" + product + "\n" +
               "A built player:\n" +
               "  HKEY_CURRENT_USER\\Software\\" + company + "\\" + product;
#elif UNITY_EDITOR_OSX
        return "PlayerPrefs are in a plist in ~/Library/Preferences:\n" +
               "  editor: unity.UnityEditor." + company + "." + product + ".plist\n" +
               "  player: unity." + company + "." + product + ".plist";
#else
        return "PlayerPrefs are in ~/.config/unity3d/" + company + "/" + product + "/prefs";
#endif
    }

    // ---------------------------------------------------------------- reflection glue

    /// <summary>
    /// A type in the game scripts, looked up by name because this assembly cannot reference it. Null before
    /// those scripts have been built.
    /// </summary>
    private static Type TypeByName(string name)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type;

            try
            {
                type = assemblies[i].GetType(name);
            }
            catch (Exception)
            {
                continue;
            }

            if (type != null) return type;
        }

        return null;
    }

    private static object StaticField(string typeName, string fieldName)
    {
        Type type = TypeByName(typeName);
        if (type == null) return null;

        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

        return field != null ? field.GetValue(null) : null;
    }

    /// <summary>
    /// Calls one of the game's own methods - the ones that change both the saved values and its in-memory copy,
    /// which is what keeps a running game in step with an edit made here.
    /// </summary>
    private static void CallStatic(string typeName, string methodName)
    {
        Type type = TypeByName(typeName);

        if (type == null)
        {
            Debug.LogWarning("[Save] " + typeName + " is not loaded yet, so " + methodName + "() could not be " +
                             "called. Build the game scripts and try again.");
            return;
        }

        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

        if (method == null)
        {
            Debug.LogWarning("[Save] " + typeName + "." + methodName + "() was not found.");
            return;
        }

        method.Invoke(null, null);

        Debug.Log("[Save] " + typeName + "." + methodName + "() done. Reload the window to see the result.");
    }
}
