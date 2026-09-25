using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts the AudioSources on this object - and, unless told otherwise, on its children - into one of the
/// player's <see cref="SoundChannel"/> settings.
///
/// This is how a sound says which setting it belongs to. There is no way to tell from outside: an AudioSource
/// on a menu is the music and the same component on a lamp is an effect, and only whoever placed it knows
/// which. The volume it starts with is the volume the game was built with; this scales it by the player's own
/// setting - with the master folded in as a ceiling, so turning the master down brings this down with it -
/// which leaves a sound exactly as authored at <see cref="SoundLevel.High"/> all round.
///
/// The volume is written when the object wakes and again whenever the settings change, and never in between -
/// which is the point. A source belongs to one hand: this component owns the volumes of sources nothing else
/// moves (music, an ambience, a one-shot with a fixed level), and a source a script computes for itself - the
/// engine, the rain - belongs to that script, which multiplies by <see cref="SoundSettings.Volume"/> instead.
/// Two things writing to one AudioSource would fight, and the loser would be whichever wrote first.
///
/// A source with no <see cref="SoundChannelSource"/> anywhere above it is picked up by
/// <see cref="SoundBus"/> when its scene loads and follows <see cref="SoundBus.UnclassifiedChannel"/>, so a
/// sound added without one still obeys the settings - it is simply in the wrong group until somebody says
/// otherwise.
/// </summary>
[DisallowMultipleComponent]
public class SoundChannelSource : MonoBehaviour
{
    [Header("Channel")]
    [Tooltip("Which of the player's sound settings these sounds follow.")]
    public SoundChannel channel = SoundChannel.Soundtrack;

    [Tooltip("Also govern the AudioSources on this object's children. Off means the ones on this object only, " +
             "which is what a group of different sounds sharing a parent wants.")]
    public bool includeChildren = true;

    // The sources governed, and the volume each of them was authored with. Taken once, when this object
    // wakes: after that the volumes here are ours and re-reading them would read our own writing back.
    private readonly List<AudioSource> sources = new List<AudioSource>();
    private readonly List<float> authoredVolume = new List<float>();

    private bool collected;

    private void Awake()
    {
        Collect();
    }

    private void OnEnable()
    {
        Collect();

        SoundBus.Add(this);
        Apply();
    }

    private void OnDisable()
    {
        SoundBus.Remove(this);
    }

    /// <summary>
    /// Says which setting these sounds follow, for a component that was added while the game was running (see
    /// <see cref="SoundBus"/>) - and applies it. The authored volumes are left as they were taken, so
    /// re-pointing a channel never re-reads a volume this has already written.
    /// </summary>
    public void Orient(SoundChannel newChannel, bool children)
    {
        Collect();

        channel = newChannel;
        includeChildren = children;

        Apply();
    }

    /// <summary>Takes the sources to govern and remembers the volume each was authored with.</summary>
    private void Collect()
    {
        if (collected) return;

        collected = true;

        AudioSource[] found = includeChildren
            ? GetComponentsInChildren<AudioSource>(true)
            : GetComponents<AudioSource>();

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null) continue;

            sources.Add(found[i]);
            authoredVolume.Add(found[i].volume);
        }
    }

    /// <summary>
    /// Puts the current settings onto every source this governs - the player's step for this channel, capped
    /// by the master (see <see cref="SoundSettings.Volume"/>).
    /// </summary>
    public void Apply()
    {
        float scale = SoundSettings.Volume(channel);

        for (int i = 0; i < sources.Count; i++)
        {
            AudioSource source = sources[i];

            if (source == null) continue;

            source.volume = authoredVolume[i] * scale;
        }
    }

    /// <summary>How many sources this is responsible for, for anything that has to report on it.</summary>
    public int SourceCount { get { return sources.Count; } }
}
