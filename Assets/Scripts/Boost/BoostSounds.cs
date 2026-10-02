using UnityEngine;

/// <summary>
/// Plays the game's four boost sounds: the milkshake going into the tank, the little jingle that says one has
/// gone in, the shove of one being spent, and the dry knock that answers the button when the tank is empty.
///
/// The clips are made rather than shipped (<see cref="BoostClip"/>), and this is the one place that plays them
/// - so every level sounds the same without a single object being wired up, and there is one volume and one
/// set of settings rather than one per pickup. It installs itself once the game starts, the way
/// <see cref="UiSounds"/> does, and lives for the session.
///
/// All three sounds belong to the EFFECTS channel, so the player's own sound settings govern them: turning
/// effects down makes the pickups quieter along with the crashes and the horn, and the master caps them like
/// everything else (see <see cref="SoundSettings"/>). Their sources are marked as handled for that reason -
/// the settings are folded in here, where a sound is played, rather than by anything sweeping over the sources
/// behind this.
///
/// The sip and the jingle are separate sounds on separate sources because they answer different questions: the
/// sip says what the driver just drove through, and the jingle answers it by saying the tank went up. A
/// milkshake collected with the tank already full is still heard - it was still drunk - but it does not get
/// the jingle. The empty tank has a source of its own for the same reason: it answers the *button* rather than
/// the world, and it must never cut a boost short or be cut short by one.
///
/// Who calls it: <see cref="BoostManager"/> - when a milkshake is collected, when a boost is spent, and when
/// the button is pressed with nothing left to spend - so these follow the tank wherever a level keeps it.
/// </summary>
public static class BoostSounds
{
    /// <summary>
    /// How loud each sound is before the player's own settings. The milkshake is the quietest of the four: it
    /// happens often, sometimes several times a corner, and it is there to be noticed rather than to be an
    /// event. The jingle is up with the boost, because it is the reward - it is the sound of the count going
    /// up, and it is worth hearing over the engine. The boost and the empty tank are the two the player has to
    /// hear over everything else a level is doing, and both are set above the rest for that reason: the boost
    /// is the effect they asked for and a shove they cannot hear reads as nothing happening, and the empty
    /// tank is a refusal - a refusal the driver does not hear is a button that reads as broken. Neither is
    /// tiring at this level: the boost is a swell with a tail and the empty is short and dry, and it is played
    /// at most every third of a second - see <see cref="BoostManager"/>.
    ///
    /// They sit a little over one on purpose, against a clip that is normalised to full scale: the pair are
    /// transients the engine is playing over, and a couple of decibels of overshoot on a knock or a whoosh is
    /// heard as punch rather than as clipping.
    /// </summary>
    private const float PickupVolume = 0.5f;
    private const float CollectVolume = 0.7f;
    private const float BoostVolume = 0.8f;
    private const float EmptyVolume = 1.1f;

    /// <summary>
    /// How far the pitch is allowed to wander from one play to the next. Milkshakes come in runs - a straight
    /// with four on it would otherwise be the same sip four times - and a few percent either way is enough to
    /// make each one its own.
    /// </summary>
    private const float PickupPitchSpread = 0.08f;
    private const float CollectPitchSpread = 0.06f;
    private const float BoostPitchSpread = 0.03f;
    private const float EmptyPitchSpread = 0.07f;

    private static GameObject host;
    private static AudioSource pickup;
    private static AudioSource collect;
    private static AudioSource boost;
    private static AudioSource empty;

    // ---------------------------------------------------------------- startup

    /// <summary>
    /// Makes all four sources before the first scene, so the first milkshake in a level already has a sound.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying) return;

        if (pickup == null) pickup = Create("Pickup", BoostClip.Pickup);
        if (collect == null) collect = Create("Collect", BoostClip.Collect);
        if (boost == null) boost = Create("Boost", BoostClip.Boost);
        if (empty == null) empty = Create("Empty", BoostClip.Empty);
    }

    // ---------------------------------------------------------------- playing

    /// <summary>A milkshake has gone into the tank.</summary>
    public static void PlayPickup()
    {
        if (pickup == null) pickup = Create("Pickup", BoostClip.Pickup);

        Play(pickup, BoostClip.Pickup, PickupVolume, PickupPitchSpread);
    }

    /// <summary>A milkshake has gone into the tank and the count has gone up.</summary>
    public static void PlayCollect()
    {
        if (collect == null) collect = Create("Collect", BoostClip.Collect);

        Play(collect, BoostClip.Collect, CollectVolume, CollectPitchSpread);
    }

    /// <summary>A boost is being spent and the truck is being shoved.</summary>
    public static void PlayBoost()
    {
        if (boost == null) boost = Create("Boost", BoostClip.Boost);

        Play(boost, BoostClip.Boost, BoostVolume, BoostPitchSpread);
    }

    /// <summary>The boost button was pressed and there was nothing in the tank to spend.</summary>
    public static void PlayEmpty()
    {
        if (empty == null) empty = Create("Empty", BoostClip.Empty);

        Play(empty, BoostClip.Empty, EmptyVolume, EmptyPitchSpread);
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
    /// One of the sources, on the object this owns for the session. Each sound has a source of its own, so the
    /// jingle cannot cut the sip short as it starts - <see cref="AudioSource.PlayOneShot"/> mixes rather than
    /// replaces, and separate sources also let the sounds be pitched independently.
    /// </summary>
    private static AudioSource Create(string name, AudioClip clip)
    {
        if (host == null)
        {
            host = new GameObject("Boost Sounds");

            Object.DontDestroyOnLoad(host);
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(host.transform, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;               // heard from the driver's seat, not from a place in the world
        source.volume = 1f;
        source.clip = clip;

        // Its volume is worked out where it is played, so nothing else may write to it - see SoundBus.
        SoundBus.MarkHandled(source);

        return source;
    }
}
