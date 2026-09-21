using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The level progress, from the editor.
///
/// Progress is saved in PlayerPrefs, which in the editor belongs to the project - so a session of testing can
/// leave levels unlocked, and the only way back to how a fresh player starts would be to hunt the saved keys
/// down by hand. These are the two shortcuts for that: reset to the state a new player begins in, or open
/// everything to get straight into the level being worked on. Show Progress prints what is actually saved,
/// which is the quickest way to find out why a level is behaving as locked.
///
/// Both of them go through the same store the game uses, so nothing here can disagree with what the level list
/// shows. <see cref="LevelProgress"/> lives in the game scripts, which this editor assembly cannot reference,
/// so it is looked up by name and driven through reflection - the same way the rain tool reaches RainSystem.
/// </summary>
public static class LevelProgressTools
{
    private const string ProgressTypeName = "LevelProgress";

    [MenuItem("Tools/Progress/Show Progress", false, 10)]
    private static void ShowProgress()
    {
        Type type = FindProgressType();

        if (type == null)
        {
            Debug.LogWarning("[LevelProgress] The game scripts are not built yet, so there is no progress to " +
                             "show.");
            return;
        }

        Debug.Log(string.Format(
            "[LevelProgress] {0} levels. Unlocked up to level {1}, finished up to level {2}. A new player has " +
            "level 1 unlocked and nothing finished.",
            Number(type, "LevelCount"),
            Number(type, "HighestUnlocked"),
            Number(type, "HighestCleared")));
    }

    [MenuItem("Tools/Progress/Reset Level Progress", false, 20)]
    private static void ResetProgress()
    {
        Type type = FindProgressType();

        if (type == null)
        {
            Debug.LogWarning("[LevelProgress] The game scripts are not built yet, so there is nothing to reset.");
            return;
        }

        bool go = EditorUtility.DisplayDialog(
            "Reset level progress",
            "Lock every level except the first, and forget which ones were finished?\n\n" +
            "This is the player's saved progress, and it cannot be undone.",
            "Reset",
            "Cancel");

        if (!go) return;

        Call(type, "Reset");

        Debug.Log("[LevelProgress] Reset: level 1 is unlocked and nothing is finished.");
    }

    [MenuItem("Tools/Progress/Unlock Every Level", false, 21)]
    private static void UnlockEverything()
    {
        Type type = FindProgressType();

        if (type == null)
        {
            Debug.LogWarning("[LevelProgress] The game scripts are not built yet, so there is nothing to unlock.");
            return;
        }

        Call(type, "UnlockAll");

        Debug.Log("[LevelProgress] Every level is unlocked, and none is marked finished. Reset Level Progress " +
                  "puts it back to the state a new player sees.");
    }

    // ---------------------------------------------------- reflection glue ---

    /// <summary>
    /// The progress store, looked up by name across the loaded assemblies because it lives in the game
    /// scripts. Returns null before those scripts have been built.
    /// </summary>
    private static Type FindProgressType()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type;

            try
            {
                type = assemblies[i].GetType(ProgressTypeName);
            }
            catch (Exception)
            {
                continue;
            }

            // A static class: nothing to instantiate, everything read straight off the type.
            if (type != null && type.IsAbstract && type.IsSealed)
                return type;
        }

        return null;
    }

    /// <summary>A number on the store - a property such as HighestUnlocked, or the LevelCount constant.</summary>
    private static int Number(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);

        if (property != null && property.PropertyType == typeof(int))
            return (int)property.GetValue(null, null);

        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);

        if (field != null && field.FieldType == typeof(int))
            return (int)field.GetValue(null);

        return -1;
    }

    private static void Call(Type type, string name)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);

        if (method != null)
            method.Invoke(null, null);
    }
}
