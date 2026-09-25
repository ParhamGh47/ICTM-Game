using System;
using UnityEngine;

/// <summary>
/// One of the game's sound channels - a group of sounds the player turns up or down together.
///
/// The split is by what the player hears, not by where the sound comes from: music is the soundtrack, a
/// truck's engine is the engine, a crash is an effect, and the world around the road is the environment.
/// <see cref="SoundChannelSource"/> is how a sound is put in a group, and the two screens that show these
/// (the options screen and the pause menu's options) build their rows straight from this enum, so a channel
/// added here shows up in both without either screen being touched.
/// </summary>
public enum SoundChannel
{
    /// <summary>Everything at once, the way a volume knob works.</summary>
    Master = 0,

    /// <summary>The music: the menu song, a level's own track, the pause and game over songs.</summary>
    Soundtrack = 1,

    /// <summary>The player's own truck: the engine, its gear shifts and its exhaust.</summary>
    Engine = 2,

    /// <summary>Everything the truck hits or is told to do: crashes, horns, the reverse beep, targets.</summary>
    Effects = 3,

    /// <summary>The world around the road: the rain, sirens, whatever a level puts out there.</summary>
    Environment = 4,
}

/// <summary>
/// How loud a channel is, as one of four steps.
///
/// Four steps rather than a slider because these screens are driven by a keyboard and a gamepad as much as
/// by a mouse, and a row of choices is something both devices already know how to move around. The exact
/// share each step is worth is <see cref="SoundSettings.StepVolume"/>.
/// </summary>
public enum SoundLevel
{
    Muted = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>
/// The game's sound settings: five channels, four steps each, and where the choice is kept.
///
/// Every channel starts at <see cref="SoundLevel.High"/>, which is the game exactly as it was authored - so a
/// player who never opens the screen hears what the project was built to sound like, and anything they do
/// choose is remembered. <see cref="Volume"/> is what everything else multiplies by, and it folds the master
/// channel in, so nothing has to remember to ask about two settings.
///
/// The master is a ceiling rather than another number multiplied in: a channel set above it plays at the
/// master's step, so turning the master down turns everything down with it - which is what the setting says
/// and what its row of choices should show. <see cref="EffectiveLevel"/> is that resolved step, and it is what
/// the two settings screens light up, so a bar that has been capped by the master says so instead of claiming
/// a loudness the player cannot hear. Each channel's own choice is kept underneath, so raising the master
/// again gives every channel back exactly what it had.
///
/// Loudness is applied at the point a sound is played or owned rather than being swept up afterwards, because
/// a source has one volume: two things writing to it fight, and the loser is whichever wrote first. So a sound
/// belongs to exactly one of two hands - a <see cref="SoundChannelSource"/> for a source nothing else moves
/// (music, a hum, a one-shot), or the script that computes it (the engine, the rain) which multiplies by
/// <see cref="Volume"/> itself. Either way <see cref="Changed"/> is what tells everyone to catch up.
/// </summary>
public static class SoundSettings
{
    // ---------------------------------------------------------------- what a channel can be

    /// <summary>How many channels the game has, and therefore how many rows the screens show.</summary>
    public static int ChannelCount { get { return ChannelNames.Length; } }

    /// <summary>How many steps a channel has, and therefore how many choices each row shows.</summary>
    public static int StepCount { get { return StepNames.Length; } }

    /// <summary>The channels' names, in enum order - what the settings screens label their rows with.</summary>
    public static readonly string[] ChannelNames = { "MASTER", "SOUNDTRACK", "ENGINE", "EFFECTS", "ENVIRONMENT" };

    /// <summary>The steps' names, in enum order - what the settings screens label the buttons with.</summary>
    public static readonly string[] StepNames = { "MUTED", "LOW", "MEDIUM", "HIGH" };

    /// <summary>
    /// What each step is worth, as a share of full volume, in enum order.
    ///
    /// 0.35 and 0.7 rather than 0.25 and 0.5, because loudness is heard on a curve: halfway between silence
    /// and full volume sounds far quieter than half as loud, and a "MEDIUM" that sounds like a whisper would
    /// make the setting feel broken.
    /// </summary>
    private static readonly float[] StepVolume = { 0f, 0.35f, 0.7f, 1f };

    // ---------------------------------------------------------------- player prefs keys

    private static readonly string[] Keys =
    {
        "Sound.Master",
        "Sound.Soundtrack",
        "Sound.Engine",
        "Sound.Effects",
        "Sound.Environment",
    };

    private const string CameraMixKey = "Sound.CameraMix";   // 0 / 1, defaults on

    // ---------------------------------------------------------------- state

    /// <summary>Raised whenever a channel or the camera mix changes, so every sound can catch up.</summary>
    public static event Action Changed;

    private static readonly SoundLevel[] levels =
    {
        SoundLevel.High, SoundLevel.High, SoundLevel.High, SoundLevel.High, SoundLevel.High,
    };

    private static bool loaded;

    /// <summary>
    /// Whether the engine is re-balanced for the camera the player is driving from. See
    /// <see cref="EngineAudio"/> - it is the only thing that reads it.
    /// </summary>
    public static bool CameraMix { get; private set; } = true;

    // ---------------------------------------------------------------- startup

    /// <summary>
    /// Reads the saved choices before the first scene loads, so the first sound of the game is already at the
    /// right volume instead of being corrected a moment later.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying) return;

        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;

        loaded = true;

        for (int i = 0; i < levels.Length; i++)
            levels[i] = Clamp(PlayerPrefs.GetInt(Keys[i], (int)SoundLevel.High));

        CameraMix = PlayerPrefs.GetInt(CameraMixKey, 1) != 0;
    }

    // ---------------------------------------------------------------- the player's choice

    /// <summary>The step a channel is on, as the player chose it (the master aside).</summary>
    public static SoundLevel LevelOf(SoundChannel channel)
    {
        EnsureLoaded();

        int index = Index(channel);

        return levels[index];
    }

    /// <summary>The step a channel is on, by row index - what the screens build their rows from.</summary>
    public static SoundLevel LevelOf(int index)
    {
        EnsureLoaded();

        return levels[Mathf.Clamp(index, 0, levels.Length - 1)];
    }

    /// <summary>
    /// The step a channel is really playing at, which is the one the screens show: the master is a ceiling, so
    /// a channel chosen louder than the master answers with the master's step. The channel's own choice is
    /// untouched underneath - turn the master back up and it is what the channel returns to.
    /// </summary>
    public static SoundLevel EffectiveLevel(SoundChannel channel)
    {
        EnsureLoaded();

        return Effective(Index(channel));
    }

    /// <summary>What a row is really playing at, by row index - what the screens light up.</summary>
    public static SoundLevel EffectiveLevel(int index)
    {
        EnsureLoaded();

        return Effective(Mathf.Clamp(index, 0, levels.Length - 1));
    }

    private static SoundLevel Effective(int index)
    {
        SoundLevel own = levels[index];

        // The master answers for itself, or it would be capped by the master twice over.
        if (index == (int)SoundChannel.Master) return own;

        SoundLevel master = levels[(int)SoundChannel.Master];

        return (int)own < (int)master ? own : master;
    }

    /// <summary>Puts a channel on a step and remembers it.</summary>
    public static void SetLevel(SoundChannel channel, SoundLevel level)
    {
        EnsureLoaded();

        levels[Index(channel)] = level;

        Save();

        if (Changed != null) Changed();
    }

    /// <summary>Puts a channel on a step, addressed by row index - what a choice row's buttons carry.</summary>
    public static void SetLevel(int index, SoundLevel level)
    {
        EnsureLoaded();

        int clamped = Mathf.Clamp(index, 0, levels.Length - 1);

        if (levels[clamped] == level) return;

        levels[clamped] = level;

        Save();

        if (Changed != null) Changed();
    }

    /// <summary>Turns the engine's camera balancing on or off.</summary>
    public static void SetCameraMix(bool on)
    {
        EnsureLoaded();

        if (CameraMix == on) return;

        CameraMix = on;

        Save();

        if (Changed != null) Changed();
    }

    // ---------------------------------------------------------------- what a sound asks for

    /// <summary>
    /// The share of full volume a channel is playing at, with the master already folded in - so this is the
    /// one number a sound has to multiply its own volume by. The master caps rather than multiplies, so a
    /// channel set louder than the master plays at the master's step, and one set quieter than the master
    /// keeps its own - the same as the bar the screens light up says (see <see cref="EffectiveLevel"/>).
    /// </summary>
    public static float Volume(SoundChannel channel)
    {
        EnsureLoaded();

        return StepVolume[(int)EffectiveLevel(channel)];
    }

    /// <summary>What a step is worth as a share of full volume, for anything that has to show it.</summary>
    public static float VolumeOf(SoundLevel level)
    {
        return StepVolume[Mathf.Clamp((int)level, 0, StepVolume.Length - 1)];
    }

    /// <summary>The channel a row index stands for.</summary>
    public static SoundChannel ChannelAt(int index)
    {
        return (SoundChannel)Mathf.Clamp(index, 0, ChannelNames.Length - 1);
    }

    // ---------------------------------------------------------------- loading and saving

    /// <summary>Everything back to full volume and the camera mix on, for a "restore defaults" button.</summary>
    public static void ResetToDefaults()
    {
        EnsureLoaded();

        PlayerPrefs.DeleteKey(CameraMixKey);
        for (int i = 0; i < Keys.Length; i++) PlayerPrefs.DeleteKey(Keys[i]);
        PlayerPrefs.Save();

        loaded = false;
        EnsureLoaded();

        if (Changed != null) Changed();
    }

    private static void Save()
    {
        for (int i = 0; i < levels.Length; i++)
            PlayerPrefs.SetInt(Keys[i], (int)levels[i]);

        PlayerPrefs.SetInt(CameraMixKey, CameraMix ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static int Index(SoundChannel channel)
    {
        int index = (int)channel;

        if (index < 0 || index >= levels.Length) return 0;

        return index;
    }

    private static SoundLevel Clamp(int value)
    {
        if (value < 0) return SoundLevel.Muted;
        if (value > (int)SoundLevel.High) return SoundLevel.High;

        return (SoundLevel)value;
    }
}
