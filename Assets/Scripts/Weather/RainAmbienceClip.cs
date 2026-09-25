using UnityEngine;

/// <summary>
/// A light-rain loop, made rather than shipped.
///
/// The project has no rain recording in it, and a level with rain that is silent reads as a bug rather than as
/// a quiet shower - so <see cref="RainSystem"/> asks for this when it has no clip of its own. Making the sound
/// costs a few milliseconds once, keeps nothing in the build, and means a level gets rain sound the moment it
/// has rain.
///
/// What is made is the sound of rain rather than a recording of it: broadband hiss with the low end taken
/// back out so it sits above an engine instead of muddying it, a slow swell so it breathes the way real rain
/// does, and a scattering of droplets on top for the character. The swell is built from whole numbers of
/// cycles over the loop's own length and the tail is crossfaded into the head, so the last sample runs into
/// the first without a click - which is the whole trick with a looping bed.
///
/// It is deterministic: the same seed every run, so the rain sounds the same in a level every time it is
/// played, and a bug in it can be reproduced.
/// </summary>
public static class RainAmbienceClip
{
    /// <summary>How long the loop is. Long enough that the ear does not catch the repeat.</summary>
    private const float Seconds = 6f;

    /// <summary>
    /// The rate the noise is generated at. Rain is hiss, so the top two octaves are the only thing lost by
    /// staying at 22 kHz, and it halves what the clip costs in memory.
    /// </summary>
    private const int SampleRate = 22050;

    /// <summary>How much of the end is mixed back into the start to close the loop without a click.</summary>
    private const float CrossfadeSeconds = 0.75f;

    /// <summary>The peak the finished loop is scaled to, before the player's own settings.</summary>
    private const float Peak = 0.55f;

    /// <summary>Droplets per second, and what they are worth against the hiss.</summary>
    private const float DropletsPerSecond = 11f;
    private const float DropletLevel = 0.16f;

    /// <summary>Fixed, so the rain is the same every time it is made.</summary>
    private const int Seed = 20260925;

    private static AudioClip cached;

    /// <summary>
    /// The shared loop, made on first ask. One clip serves every rain system in the game: it is the same six
    /// seconds whatever the level is, so making a second copy would only cost memory.
    /// </summary>
    public static AudioClip Shared
    {
        get
        {
            if (cached == null) cached = Build();

            return cached;
        }
    }

    private static AudioClip Build()
    {
        System.Random random = new System.Random(Seed);

        int length = Mathf.Max(SampleRate, Mathf.RoundToInt(Seconds * SampleRate));
        int fade = Mathf.Clamp(Mathf.RoundToInt(CrossfadeSeconds * SampleRate), 1, length / 2);

        // The tail that is folded back over the head has to be generated too, which is what the extra fade
        // samples are for.
        int total = length + fade;
        float[] raw = new float[total];

        float low = 0f;         // the body of the hiss
        float lowSlow = 0f;     // the part of it that is rumble rather than rain
        float bright = 0f;      // the fizz on top

        for (int i = 0; i < total; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);

            low += (white - low) * 0.45f;
            lowSlow += (low - lowSlow) * 0.06f;
            bright += (white - bright) * 0.55f;

            // Hiss with the rumble subtracted, brightened with what the first filter let through.
            raw[i] = 0.6f * (low - lowSlow) + 0.4f * (bright - low);
        }

        DropDroplets(raw, random);

        // The swell, as a couple of whole cycles over the loop's length - so it, too, is periodic and can
        // survive the crossfade below.
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)SampleRate;

            float swell = 1f
                + 0.18f * Mathf.Sin(2f * Mathf.PI * (1f / Seconds) * t)
                + 0.10f * Mathf.Sin(2f * Mathf.PI * (3f / Seconds) * t + 1.1f);

            raw[i] *= swell;
        }

        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
            samples[i] = raw[i];

        // The join: the tail is mixed over the head, so the end of the loop already carries what comes after
        // the start of it.
        for (int i = 0; i < fade; i++)
        {
            float w = i / (float)fade;

            samples[i] = raw[i] * w + raw[length + i] * (1f - w);
        }

        Normalise(samples);

        AudioClip clip = AudioClip.Create("Rain Ambience (generated)", length, 1, SampleRate, false);
        clip.SetData(samples, 0);

        return clip;
    }

    /// <summary>
    /// Scatters droplets over the bed: short, high, quickly damped notes, which is what tells the ear this is
    /// rain falling on something rather than a hiss. They are put in at irregular intervals rather than on a
    /// beat, because anything regular here reads as a machine.
    /// </summary>
    private static void DropDroplets(float[] raw, System.Random random)
    {
        float averageGap = 1f / DropletsPerSecond;

        for (float at = 0f; at < raw.Length / (float)SampleRate; at += averageGap * (float)(0.35 + random.NextDouble() * 1.3))
        {
            int start = Mathf.RoundToInt(at * SampleRate);
            float frequency = 1500f + (float)random.NextDouble() * 2600f;
            float decay = SampleRate * (0.002f + (float)random.NextDouble() * 0.004f);
            float level = DropletLevel * (0.5f + (float)random.NextDouble() * 0.5f);
            int reach = Mathf.Max(8, Mathf.RoundToInt(decay * 3f));

            for (int j = 0; j < reach; j++)
            {
                int index = start + j;
                if (index >= raw.Length) break;

                float envelope = Mathf.Exp(-j / decay);
                raw[index] += level * envelope * Mathf.Sin(2f * Mathf.PI * frequency * j / SampleRate);
            }
        }
    }

    /// <summary>
    /// Scales the loop so its loudest point is always the same, so the volume it is played at means the same
    /// thing however the noise above happened to fall.
    /// </summary>
    private static void Normalise(float[] samples)
    {
        float peak = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float magnitude = Mathf.Abs(samples[i]);
            if (magnitude > peak) peak = magnitude;
        }

        if (peak <= 0.0001f) return;

        float scale = Peak / peak;

        for (int i = 0; i < samples.Length; i++)
            samples[i] *= scale;
    }
}
