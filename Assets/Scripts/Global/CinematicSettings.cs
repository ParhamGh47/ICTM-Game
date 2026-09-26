using System;
using UnityEngine;

/// <summary>
/// The game's cinematic settings: the switches over the camera flourishes in a level, and where they are
/// kept.
///
/// There is one for now - the jump camera, the slow-motion tracking shot a level plays while the truck is
/// in the air over a break in the road (see <see cref="JumpCinematicCamera"/>). It is a thing a player can
/// reasonably love or find in the way, so it is theirs to turn off, and it stays off until they turn it
/// back on.
///
/// The choice is kept in PlayerPrefs like the graphics and the sound, and a level reads it as it runs, so
/// switching it off mid-jump ends the shot there and then rather than at the end of it.
/// </summary>
public static class CinematicSettings
{
    // ---------------------------------------------------------------- player prefs keys

    private const string JumpCameraKey = "Cinematic.JumpCamera";   // 0 / 1, defaults on

    // ---------------------------------------------------------------- state

    /// <summary>Raised whenever a setting changes, so anything showing it can refresh.</summary>
    public static event Action Changed;

    /// <summary>Whether a jump plays the slow-motion tracking shot.</summary>
    public static bool JumpCamera { get; private set; } = true;

    // ---------------------------------------------------------------- startup

    /// <summary>
    /// Reads what the player chose before the first scene loads. Nothing has to be applied at that point -
    /// the cinematic reads this for itself - but reading it early keeps the first jump of a session from
    /// being played at a setting the player has already changed.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying) return;

        Load();
    }

    // ---------------------------------------------------------------- the player's choice

    /// <summary>Turns the jump camera on or off and remembers it.</summary>
    public static void SetJumpCamera(bool on)
    {
        if (JumpCamera == on) return;

        JumpCamera = on;

        Save();

        if (Changed != null) Changed();
    }

    // ---------------------------------------------------------------- loading and saving

    private static void Load()
    {
        JumpCamera = PlayerPrefs.GetInt(JumpCameraKey, 1) != 0;
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(JumpCameraKey, JumpCamera ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Back to on. For a "restore defaults" button.</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(JumpCameraKey);
        PlayerPrefs.Save();

        Load();

        if (Changed != null) Changed();
    }
}
