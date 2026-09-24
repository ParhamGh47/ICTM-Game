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
/// The game's difficulty: three settings, what each one changes, and where the choice is kept.
///
/// A level's numbers are authored in the level itself - the time its timer counts down from, and how many
/// targets it asks to be hit - and those authored numbers are what Medium means. Medium is the game exactly
/// as it was built, so choosing it is the same as never having chosen anything, and it is the default. The
/// other two are the same level with those two numbers stretched or squeezed:
///
///  * the time limit, into which every level's timer counts down (see <see cref="TimeLimit"/>)
///  * the kill requirement, on the levels that ask for one (see <see cref="KillTarget"/>)
///
/// Only those two. Difficulty is deliberately not a damage multiplier or a physics tweak: a truck that
/// handles one way on Easy and another on Hard would be a different game, and what a level asks of the
/// player - get there in this long, hit this many - is what "hard" means for a racing game.
///
/// A level reads both numbers once, as its own HUD starts, so the setting cannot change a level that is
/// already running: it lands when the next one starts, or when the current one is restarted. That is the same
/// shape as the graphics preset, and it is answered the same way - a level that is open when the choice
/// changes says so and offers the restart (<see cref="ActiveSceneNeedsReload"/> and
/// <see cref="PauseOptionsPanel"/>), rather than quietly asking for something other than what it shows.
///
/// The choice is kept in PlayerPrefs, so it survives restarts, and it is the player's alone - nothing in a
/// level reads it but the HUD's two trackers.
/// </summary>
public static class GameDifficulty
{
    // ---------------------------------------------------------------- what a difficulty is

    /// <summary>Everything a difficulty changes. Edit <see cref="Levels"/> to retune, nothing else.</summary>
    private struct Settings
    {
        public string name;

        /// <summary>Multiplied into a level's authored time limit. Above 1 is more time, so easier.</summary>
        public float timeScale;

        /// <summary>Multiplied into a level's authored kill requirement, rounded to a whole target.</summary>
        public float killScale;
    }

    /// <summary>
    /// The three difficulties. Medium is the game as authored, and the two others are it with a quarter either
    /// way: a fifth more time and a third fewer targets on Easy, a fifth less time and a third more targets on
    /// Hard. Nothing here is more than a rounding away from a number the level already had, which is what keeps
    /// all three recognisably the same level.
    /// </summary>
    private static readonly Settings[] Levels =
    {
        new Settings { name = "Easy",   timeScale = 1.25f, killScale = 0.7f  },
        new Settings { name = "Medium", timeScale = 1f,    killScale = 1f    },
        new Settings { name = "Hard",   timeScale = 0.8f,  killScale = 1.35f },
    };

    /// <summary>The most a level's timer may be squeezed to, so no setting can make one impossible.</summary>
    private const float ShortestTime = 15f;

    // ---------------------------------------------------------------- player prefs key

    private const string DifficultyKey = "Game.Difficulty";     // 0 / 1 / 2, defaults to Medium

    // ---------------------------------------------------------------- state

    /// <summary>Raised whenever the difficulty changes, so anything showing it can refresh.</summary>
    public static event Action Changed;

    /// <summary>The difficulty in force.</summary>
    public static DifficultyLevel Current { get; private set; } = DifficultyLevel.Medium;

    /// <summary>The difficulty's own name, for anything that has to print it.</summary>
    public static string CurrentName => Levels[(int)Current].name;

    /// <summary>How much of a level's authored time limit the difficulty allows.</summary>
    public static float TimeScale => Levels[(int)Current].timeScale;

    /// <summary>How much of a level's authored kill requirement the difficulty asks for.</summary>
    public static float KillScale => Levels[(int)Current].killScale;

    // Which difficulty the level that is open was built with, and whether any level has been built at all.
    // A level marks itself the first time its HUD asks for one of these two numbers, which is the only moment
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
    /// The time a level's timer really counts down from, given the time the level was authored with.
    ///
    /// Also the moment the level is noted as started, which is what lets a screen in that level tell whether
    /// the choice has been changed since - so a tracker must take its number through here rather than reading
    /// <see cref="TimeScale"/> for itself.
    /// </summary>
    public static float TimeLimit(float authoredSeconds)
    {
        MarkLevelStarted();

        return Mathf.Max(ShortestTime, authoredSeconds * TimeScale);
    }

    /// <summary>
    /// The number of targets a level really asks for, given the number it was authored with. A level that asks
    /// for nothing keeps asking for nothing, and one that asks for anything keeps asking for at least one.
    /// </summary>
    public static int KillTarget(int authoredKills)
    {
        if (authoredKills <= 0) return 0;

        MarkLevelStarted();

        return Mathf.Max(1, Mathf.RoundToInt(authoredKills * KillScale));
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
