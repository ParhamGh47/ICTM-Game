using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps every <see cref="SoundChannelSource"/> in the game in step with the sound settings.
///
/// It does three small things. It holds the list of channel sources so one change of a setting reaches all of
/// them at once. It keeps the list of sources whose volume a script computes for itself (<see cref="MarkHandled"/>),
/// which is what stops the sweep below from taking one of them over. And when a scene loads it looks through
/// the scene for AudioSources nobody has claimed, and puts them in
/// <see cref="UnclassifiedChannel"/> - the group that means "a sound of the world" - so that a sound added to
/// a level later still obeys the settings even if nobody remembered to say what it is.
///
/// It installs itself once the game starts, the way <see cref="ButtonFocusEffect"/> does, so nothing has to be
/// placed in a scene for any of this to work.
/// </summary>
public static class SoundBus
{
    /// <summary>
    /// What an AudioSource nobody has classified follows. Effects, because a sound with no owner is almost
    /// always part of the world - a hum, a gate, a machine - rather than the soundtrack. Set it here if that
    /// guess is wrong for everything at once; a single sound is better served by its own
    /// <see cref="SoundChannelSource"/>.
    /// </summary>
    public static SoundChannel UnclassifiedChannel = SoundChannel.Effects;

    private static readonly List<SoundChannelSource> channels = new List<SoundChannelSource>();
    private static readonly HashSet<AudioSource> handled = new HashSet<AudioSource>();

    private static bool booted;

    // ---------------------------------------------------------------- startup

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying || booted) return;

        booted = true;

        SoundSettings.Changed += ApplyAll;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // A game started straight from a level scene never had a scene load of its own.
        Sweep(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Sweep(scene);
    }

    // ---------------------------------------------------------------- the channels

    /// <summary>A channel source coming to life. Called by <see cref="SoundChannelSource"/> itself.</summary>
    public static void Add(SoundChannelSource source)
    {
        if (source == null || channels.Contains(source)) return;

        channels.Add(source);
    }

    /// <summary>A channel source going away - a panel closing, a level unloading.</summary>
    public static void Remove(SoundChannelSource source)
    {
        channels.Remove(source);
    }

    /// <summary>
    /// Says that a source's volume is computed by the script that owns it, so the settings are folded in
    /// there and the sweep below leaves it alone. The engine does this, and so does anything that plays a
    /// sound at a volume it works out for itself.
    /// </summary>
    public static void MarkHandled(AudioSource source)
    {
        if (source == null) return;

        handled.Add(source);
    }

    /// <summary>Whether this source is already spoken for - by a script, or by a channel source above it.</summary>
    public static bool IsHandled(AudioSource source)
    {
        if (source == null) return true;
        if (handled.Contains(source)) return true;

        return source.GetComponentInParent<SoundChannelSource>() != null;
    }

    /// <summary>Puts the current settings onto every channel source in the game.</summary>
    public static void ApplyAll()
    {
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i] != null) channels[i].Apply();
        }
    }

    // ---------------------------------------------------------------- the sweep

    /// <summary>
    /// Looks through a scene for AudioSources nobody has claimed and puts each one in
    /// <see cref="UnclassifiedChannel"/>. The component is added to the running copy of the object and never
    /// to the asset it came from, so a level that has not thought about the sound settings yet still obeys
    /// them - and the object it was missing from is named in the console once, in the editor, so it can be
    /// given the right channel by whoever adds it.
    /// </summary>
    private static void Sweep(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        GameObject[] roots = scene.GetRootGameObjects();

        List<AudioSource> unclassified = null;

        for (int i = 0; i < roots.Length; i++)
        {
            AudioSource[] sources = roots[i].GetComponentsInChildren<AudioSource>(true);

            for (int s = 0; s < sources.Length; s++)
            {
                AudioSource source = sources[s];

                if (IsHandled(source)) continue;

                // Children included, which is what the component does by default and therefore what it has
                // already done by the time this line runs - a component added while the game is running wakes
                // at once. Orienting it says the same thing its own collecting did, so the field on it and its
                // behaviour agree; the child sources are then left to it, and the check at the top of this loop
                // skips them.
                SoundChannelSource channel = source.gameObject.AddComponent<SoundChannelSource>();
                channel.Orient(UnclassifiedChannel, true);

                if (unclassified == null) unclassified = new List<AudioSource>();
                unclassified.Add(source);
            }
        }

#if UNITY_EDITOR
        ReportUnclassified(unclassified);
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Says, once per scene load, which sounds were picked up without being classified. Only the editor: in a
    /// build the grouping is whatever <see cref="UnclassifiedChannel"/> says and there is nothing to read.
    /// </summary>
    private static void ReportUnclassified(List<AudioSource> unclassified)
    {
        if (unclassified == null || unclassified.Count == 0) return;

        string names = "";

        for (int i = 0; i < unclassified.Count && i < 5; i++)
        {
            if (i > 0) names += ", ";
            names += "'" + unclassified[i].gameObject.name + "'";
        }

        if (unclassified.Count > 5) names += ", and " + (unclassified.Count - 5) + " more";

        Debug.Log("[Sound] " + unclassified.Count + " sound source" + (unclassified.Count == 1 ? "" : "s") +
                  " had no SoundChannelSource, so they follow " + UnclassifiedChannel + ": " + names +
                  ". Add a SoundChannelSource to each to put it in its own setting.");
    }
#endif
}
