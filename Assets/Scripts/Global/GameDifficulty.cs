using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>How hard the game is set to be.</summary>
public enum DifficultyLevel
{
    Easy = 0,
    Medium = 1,
    Hard = 2,
}

/// <summary>
/// The game's difficulty: three settings and the player's choice between them.
///
/// Every level authors its own numbers - a time limit and a kill requirement for each of the three
/// difficulties (see <see cref="TimeTracker"/> and <see cref="KillDisplay"/>) - and this only decides
/// which of them is used. Nothing is derived from a shared scale, so "Easy" on a level is exactly what
/// that level says Easy is: the levels are not obliged to be a fixed fraction of one another, and one
/// level can be made easier or harder on its own without touching the other two.
///
/// Medium is the game as it was built and the default, which is why the fields on a tracker are named
/// for the three difficulties rather than for a base value and two adjustments.
///
/// A level reads its difficulty's numbers once, as its own HUD starts, so the setting cannot change a
/// level that is already running: it lands when the next one starts, or when the current one is
/// restarted. That is the same shape as the graphics preset, and it is answered the same way - a level
/// that is open when the choice changes says so and offers the restart (<see cref="ActiveSceneNeedsReload"/>
/// and <see cref="PauseOptionsPanel"/>), rather than quietly asking for something other than what it shows.
///
/// The choice is kept in PlayerPrefs, so it survives restarts, and it is the player's alone - nothing in a
/// level reads it but the HUD's two trackers.
/// </summary>
public static class GameDifficulty
{
    // ---------------------------------------------------------------- the three settings

    /// <summary>The difficulties' own names, in the order the enum defines them.</summary>
    private static readonly string[] Names = { "Easy", "Medium", "Hard" };

    /// <summary>A timer never counts down from less than this, however low a level authors its time.</summary>
    private const float ShortestTime = 1f;

    // ---------------------------------------------------------------- player prefs key

    private const string DifficultyKey = "Game.Difficulty";     // 0 / 1 / 2, defaults to Medium

    // ---------------------------------------------------------------- state

    /// <summary>Raised whenever the difficulty changes, so anything showing it can refresh.</summary>
    public static event Action Changed;

    /// <summary>The difficulty in force.</summary>
    public static DifficultyLevel Current { get; private set; } = DifficultyLevel.Medium;

    /// <summary>The difficulty's own name, for anything that has to print it.</summary>
    public static string CurrentName => Names[(int)Current];

    // Which difficulty the level that is open was built with, and whether any level has been built at all.
    // A level marks itself the first time its HUD asks for one of these numbers, which is the only moment
    // the choice is read; clearing the mark on every scene load is what makes the next level a fresh one.
    private static bool levelStarted;
    private static DifficultyLevel levelDifficulty = DifficultyLevel.Medium;

    // ---------------------------------------------------------------- startup

    /// <summary>
    /// Reads what the player chose before the first scene loads. Nothing has to be applied at that point -
    /// unlike a graphics preset, this changes no engine setting - but the scene hook is registered here so a
    /// level loaded later is a fresh one rather than the one that was open before it.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying) return;

        Load();

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Playing a level straight from the editor - rather than from the menu - enters the game with that
        // level already open, and whether the scene hook above counts as having delivered it is not something
        // to rely on. Starting the count from here covers it either way: a level's own HUD is what marks it,
        // and this only makes sure it is not still marked from wherever the game was before.
        BeginLevel();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BeginLevel();
    }

    /// <summary>Forgets that a level was built, so the next one to ask is the one that marks it.</summary>
    private static void BeginLevel()
    {
        levelStarted = false;
        levelDifficulty = Current;
    }

    // ---------------------------------------------------------------- the player's choice

    /// <summary>
    /// Picks a difficulty and remembers it. It applies to the next level that starts - a level that is
    /// running keeps the numbers it began with, and asks for a restart instead.
    /// </summary>
    public static void Choose(DifficultyLevel difficulty)
    {
        if (difficulty == Current) return;

        Current = difficulty;

        Save();

        if (Changed != null) Changed();
    }

    // ---------------------------------------------------------------- what a level asks for

    /// <summary>
    /// The time a level's timer really counts down from: the time the level authored for the difficulty in
    /// force. The three are read independently, so a level can be made harder or easier on its own.
    ///
    /// Also the moment the level is noted as started, which is what lets a screen in that level tell whether
    /// the choice has been changed since - so a tracker must take its number through here rather than reading
    /// <see cref="Current"/> for itself.
    /// </summary>
    public static float TimeLimit(float easySeconds, float mediumSeconds, float hardSeconds)
    {
        MarkLevelStarted();

        return Mathf.Max(ShortestTime, Pick(easySeconds, mediumSeconds, hardSeconds));
    }

    /// <summary>
    /// The number of targets a level really asks for, at the difficulty in force. A level that asks for
    /// nothing on the chosen difficulty keeps asking for nothing there, and one that asks for anything keeps
    /// asking for at least one.
    /// </summary>
    public static int KillTarget(int easyKills, int mediumKills, int hardKills)
    {
        MarkLevelStarted();

        int authored = Pick(easyKills, mediumKills, hardKills);

        return authored <= 0 ? 0 : Mathf.Max(1, authored);
    }

    private static float Pick(float easy, float medium, float hard)
    {
        switch (Current)
        {
            case DifficultyLevel.Easy: return easy;
            case DifficultyLevel.Hard: return hard;
            default: return medium;
        }
    }

    private static int Pick(int easy, int medium, int hard)
    {
        switch (Current)
        {
            case DifficultyLevel.Easy: return easy;
            case DifficultyLevel.Hard: return hard;
            default: return medium;
        }
    }

    private static void MarkLevelStarted()
    {
        if (levelStarted) return;

        levelStarted = true;
        levelDifficulty = Current;
    }

    /// <summary>
    /// Whether the level that is open now would be played differently if it were started again - that is,
    /// whether it is running with a different difficulty from the one the player has since chosen.
    ///
    /// Answered by comparing the choice with the difficulty the level's own HUD was built with, rather than by
    /// remembering that something changed, so it is true exactly while the level really is being played at the
    /// wrong setting - including after the player changes their mind and picks the one the level already has,
    /// which needs no restart. A scene with no level in it - a menu, the customize screen - has nothing that
    /// asks for a time or a target, so it is never left asking to be restarted.
    /// </summary>
    public static bool ActiveSceneNeedsReload()
    {
        return levelStarted && levelDifficulty != Current;
    }

    // ---------------------------------------------------------------- loading and saving

    private static void Load()
    {
        Current = Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)DifficultyLevel.Medium));
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(DifficultyKey, (int)Current);
        PlayerPrefs.Save();
    }

    private static DifficultyLevel Clamp(int value)
    {
        if (value < 0) return DifficultyLevel.Easy;
        if (value > 2) return DifficultyLevel.Hard;
        return (DifficultyLevel)value;
    }

    /// <summary>Back to Medium, the game as authored.</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(DifficultyKey);
        PlayerPrefs.Save();

        Load();

        if (Changed != null) Changed();
    }
}
