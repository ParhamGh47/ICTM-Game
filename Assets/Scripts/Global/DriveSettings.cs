using System;
using UnityEngine;

/// <summary>What the truck's gearbox does with the engine: choose itself, or wait to be told.</summary>
public enum GearboxMode
{
    /// <summary>The engine decides: it shifts up and down on its own revs, as it always has.</summary>
    Automatic = 0,

    /// <summary>The player decides: the truck holds the gear it is in until it is shifted, and it can be
    /// bogged down or revved out in the wrong one.</summary>
    Manual = 1,
}

/// <summary>
/// The gearbox the player has chosen to drive with.
///
/// Automatic is the game as it was built and the default, so nothing changes for a player who never opens
/// the settings. Manual hands the gear choice over: the engine no longer shifts itself (see
/// <see cref="EngineAudio"/>), the truck's pull depends on which gear it is in and on the revs, and the
/// trucks' controls move with it - the horn and the lights go to the D-pad so that the two face buttons can
/// be the gears (see <see cref="GameInput"/>).
///
/// Unlike the graphics preset and the difficulty, this needs no restart: the gearbox is read every frame, so
/// a level that is running picks the change up on the spot. That is why nothing here asks a level to be
/// reloaded - <see cref="ActiveSceneNeedsReload"/> is deliberately not a thing.
///
/// The choice is kept in PlayerPrefs, so it survives restarts, and it is the player's alone.
/// </summary>
public static class DriveSettings
{
    // ---------------------------------------------------------------- player prefs key

    private const string GearboxKey = "Drive.Gearbox";      // 0 / 1, defaults to Automatic

    // ---------------------------------------------------------------- state

    /// <summary>Raised whenever the gearbox is changed, so anything showing it can refresh.</summary>
    public static event Action Changed;

    /// <summary>The gearbox in force.</summary>
    public static GearboxMode Mode { get; private set; } = GearboxMode.Automatic;

    /// <summary>Whether the player is changing gear for themselves.</summary>
    public static bool Manual { get { return Mode == GearboxMode.Manual; } }

    /// <summary>The gearbox's own name, for anything that has to print it.</summary>
    public static string ModeName { get { return Manual ? "MANUAL" : "AUTO"; } }

    // ---------------------------------------------------------------- startup

    /// <summary>
    /// Reads what the player chose before the first scene loads. Nothing has to be applied at that point -
    /// this changes no engine setting and no scene content - but the settings screens show it, so it has to
    /// be in force before they build.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying) return;

        Load();
    }

    // ---------------------------------------------------------------- the player's choice

    /// <summary>
    /// Picks a gearbox and remembers it. It lands straight away - a level that is being driven reads it every
    /// frame - so nothing has to be restarted for it.
    /// </summary>
    public static void Choose(GearboxMode mode)
    {
        if (mode == Mode) return;

        Mode = mode;

        Save();

        if (Changed != null) Changed();
    }

    /// <summary>The two settings as one switch: on is manual.</summary>
    public static void SetManual(bool manual)
    {
        Choose(manual ? GearboxMode.Manual : GearboxMode.Automatic);
    }

    /// <summary>Flips it, which is what the switch on the settings screens is wired to.</summary>
    public static void Toggle()
    {
        SetManual(!Manual);
    }

    // ---------------------------------------------------------------- loading and saving

    private static void Load()
    {
        Mode = Clamp(PlayerPrefs.GetInt(GearboxKey, (int)GearboxMode.Automatic));
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(GearboxKey, (int)Mode);
        PlayerPrefs.Save();
    }

    /// <summary>A value read from PlayerPrefs, as a mode. Anything outside the enum - an old save, a
    /// hand-edited one - reads as the game as it was built rather than as a missing setting.</summary>
    private static GearboxMode Clamp(int value)
    {
        if (value <= (int)GearboxMode.Automatic) return GearboxMode.Automatic;
        return GearboxMode.Manual;
    }

    /// <summary>Back to Automatic, the game as authored.</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(GearboxKey);
        PlayerPrefs.Save();

        Load();

        if (Changed != null) Changed();
    }
}
