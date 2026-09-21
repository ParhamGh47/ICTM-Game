using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// How far the player has got: which levels are open and which are finished, kept in PlayerPrefs so it
/// survives closing the game.
///
/// The rule is one line long - level 1 is open from the start, and finishing a level opens the one after it -
/// but it lives in one place on purpose: the level list, the buttons that open a level and the finish line all
/// ask the same question here rather than each keeping their own idea of progress. Opens are never taken back,
/// so replaying an earlier level, or coming back to the game a week later, keeps everything already reached.
///
/// Nothing here knows about scenes beyond their names. A level is one number, and every scene that makes it up
/// carries that number after a fixed prefix - TW-Start-3, Core-3 and TW-End-3 are all level 3 - which is what
/// <see cref="LevelFromScene"/> reads. A scene that is not part of a level (a menu, the playground) simply
/// belongs to no level, and can neither be locked nor finish anything.
/// </summary>
public static class LevelProgress
{
    /// <summary>
    /// The playable levels: TW-Start-n through TW-End-n for each n from 1 to this. Bump it when a level is
    /// added - it is the range the level list marks and the ceiling nothing can unlock past.
    /// </summary>
    public const int LevelCount = 4;

    /// <summary>The scenes a level is made of, each followed by the level number.</summary>
    private static readonly string[] LevelScenePrefixes = { "TW-Start-", "Core-", "TW-End-" };

    private const string UnlockedKey = "LevelProgress.Unlocked";
    private const string ClearedKey = "LevelProgress.Cleared";

    /// <summary>Raised when the progress changes, so an open level list can redraw itself.</summary>
    public static event System.Action Changed;

    private static bool loaded;
    private static int unlocked;    // the highest level the player may enter
    private static int cleared;     // the highest level the player has finished

    /// <summary>The highest level the player may enter. At least 1, so there is always somewhere to start.</summary>
    public static int HighestUnlocked
    {
        get
        {
            Load();
            return unlocked;
        }
    }

    /// <summary>The highest level the player has finished, or 0 if none has been.</summary>
    public static int HighestCleared
    {
        get
        {
            Load();
            return cleared;
        }
    }

    /// <summary>Whether a level may be entered.</summary>
    public static bool IsUnlocked(int level)
    {
        Load();
        return level >= 1 && level <= unlocked;
    }

    /// <summary>Whether a level has been finished.</summary>
    public static bool IsCleared(int level)
    {
        Load();
        return level >= 1 && level <= cleared;
    }

    /// <summary>
    /// Finishes a level: it counts as cleared and the level after it opens. Replaying a level that has already
    /// been cleared changes nothing, so nothing is saved and nothing is raised for it.
    /// </summary>
    public static void Complete(int level)
    {
        Load();

        if (level < 1) return;

        bool changed = false;

        if (level > cleared)
        {
            cleared = Mathf.Min(level, LevelCount);
            changed = true;
        }

        // The last level has nothing after it, so its own completion opens nothing.
        if (level + 1 > unlocked)
        {
            unlocked = Mathf.Clamp(level + 1, 1, LevelCount);
            changed = true;
        }

        if (!changed) return;

        Save();

        if (Changed != null) Changed();
    }

    /// <summary>
    /// Finishes whichever level the active scene belongs to. Called when the finish line is crossed; a scene
    /// that belongs to no level does nothing here.
    /// </summary>
    public static void CompleteCurrentScene()
    {
        int level = LevelFromScene(SceneManager.GetActiveScene().name);

        if (level >= 1) Complete(level);
    }

    /// <summary>
    /// The level a scene belongs to, from its name - TW-Start-2, Core-2 and TW-End-2 are all level 2. Anything
    /// else is level 0, which is not a level, so menus and the playground can never be mistaken for progress.
    /// </summary>
    public static int LevelFromScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return 0;

        for (int i = 0; i < LevelScenePrefixes.Length; i++)
        {
            if (!sceneName.StartsWith(LevelScenePrefixes[i], System.StringComparison.OrdinalIgnoreCase)) continue;

            int number;

            return int.TryParse(sceneName.Substring(LevelScenePrefixes[i].Length), out number) ? number : 0;
        }

        return 0;
    }

    /// <summary>Locks everything but the first level and forgets what was finished.</summary>
    public static void Reset()
    {
        loaded = true;
        unlocked = 1;
        cleared = 0;

        Save();

        if (Changed != null) Changed();
    }

    /// <summary>Opens every level but clears none - a shortcut for testing, not a cheat the game offers.</summary>
    public static void UnlockAll()
    {
        loaded = true;
        unlocked = LevelCount;
        cleared = 0;

        Save();

        if (Changed != null) Changed();
    }

    // ---------------------------------------------------------------- storage

    private static void Load()
    {
        if (loaded) return;

        loaded = true;

        unlocked = Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, LevelCount);
        cleared = Mathf.Clamp(PlayerPrefs.GetInt(ClearedKey, 0), 0, LevelCount);
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(UnlockedKey, unlocked);
        PlayerPrefs.SetInt(ClearedKey, cleared);
        PlayerPrefs.Save();
    }
}
