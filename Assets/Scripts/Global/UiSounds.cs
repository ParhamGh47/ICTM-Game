using UnityEngine;

/// <summary>
/// Plays the game's two menu sounds: the squish the highlight makes as it moves from one button to the next,
/// and the wetter splat a button makes when it is confirmed.
///
/// The clips are made rather than shipped (<see cref="UiSquishClip"/>), and this is the one place that plays
/// them - so every menu in the game sounds the same without a single scene being wired up, and there is one
/// volume and one pair of settings rather than one per screen. It installs itself once the game starts, the way
/// <see cref="ButtonFocusEffect"/> does, and lives for the session.
///
/// Both sounds belong to the EFFECTS channel, so the player's own sound settings govern them: turning effects
/// down makes the menus quieter with the crashes and the horn, and the master caps them like everything else
/// (see <see cref="SoundSettings"/>). Their sources are marked as handled for that reason - the settings are
/// folded in here, where a sound is played, rather than by anything sweeping over the sources behind this.
///
/// Who calls it: <see cref="ButtonFocusEffect"/> already works out which button is highlighted and whether it
/// is being pressed, from every device the menus are used with, so it is what tells these two sounds when to
/// play. Nothing else has to know they exist.
/// </summary>
public static class UiSounds
{
    /// <summary>
    /// How loud each sound is before the player's own settings.
    ///
    /// The two are close on purpose. The move is a sixth of a second and the confirm is half a second with a
    /// splatter behind it, and a short sound is heard as quieter than a long one at the same level - so the
    /// move needs to be played at least as loud as the confirm to read as "a button, quieter" rather than as
    /// "a button you can barely hear". The confirm still wins clearly, because its own clip is the louder of
    /// the two (<see cref="UiSquishClip"/>).
    /// </summary>
    private const float MoveVolume = 0.6f;
    private const float ConfirmVolume = 0.6f;

    /// <summary>
    /// How far the pitch is allowed to wander from one play to the next. The same squish twice in a row sounds
    /// like a machine; a few percent either way sounds like a menu.
    /// </summary>
    private const float MovePitchSpread = 0.07f;
    private const float ConfirmPitchSpread = 0.03f;

    private static GameObject host;
    private static AudioSource move;
    private static AudioSource confirm;

    // ---------------------------------------------------------------- startup

    /// <summary>
    /// Makes both sources before the first scene, so the first button the player lands on already squishes.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying) return;

        if (move == null) move = Create("Move", UiSquishClip.Move);
        if (confirm == null) confirm = Create("Confirm", UiSquishClip.Confirm);
    }

    // ---------------------------------------------------------------- playing

    /// <summary>The highlight has moved to another button.</summary>
    public static void PlayMove()
    {
        if (move == null) move = Create("Move", UiSquishClip.Move);

        Play(move, UiSquishClip.Move, MoveVolume, MovePitchSpread);
    }

    /// <summary>A button has been confirmed - by a key, a pad or the pointer.</summary>
    public static void PlayConfirm()
    {
        if (confirm == null) confirm = Create("Confirm", UiSquishClip.Confirm);

        Play(confirm, UiSquishClip.Confirm, ConfirmVolume, ConfirmPitchSpread);
    }

    private static void Play(AudioSource source, AudioClip clip, float volume, float pitchSpread)
    {
        if (source == null) return;

        source.pitch = 1f + Random.Range(-pitchSpread, pitchSpread);

        // The player's own setting for effects, with the master already folded in. The source's own volume
        // stays at one, so this is the only thing scaling it and nothing has to be kept in step afterwards.
        source.PlayOneShot(clip, volume * SoundSettings.Volume(SoundChannel.Effects));
    }

    /// <summary>
    /// One of the two sources, on the object this owns for the session. Each sound has a source of its own, so
    /// a confirm cannot cut a squish short as it starts - <see cref="AudioSource.PlayOneShot"/> mixes rather
    /// than replaces, and separate sources also let the two sounds be pitched independently.
    /// </summary>
    private static AudioSource Create(string name, AudioClip clip)
    {
        if (host == null)
        {
            host = new GameObject("UI Sounds");

            Object.DontDestroyOnLoad(host);
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(host.transform, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;               // a menu sound comes from the screen, not from a place in it
        source.volume = 1f;
        source.clip = clip;

        // Its volume is worked out where it is played, so nothing else may write to it - see SoundBus.
        SoundBus.MarkHandled(source);

        return source;
    }
}
