using UnityEngine;

/// <summary>
/// A police siren, made rather than shipped.
///
/// The project has no siren recording in it, and the level that wants one is still to be built - so this is
/// what a police car is heard through until somebody drops a real siren in. Like <see cref="RainAmbienceClip"/>
/// it costs a few milliseconds once, keeps nothing in the build, and means a car can be put in a level today
/// and be heard, rather than the sound being the last thing that has to arrive.
///
/// It is the "wail" a patrol car makes: one tone sliding up and down between about 600 and 1200 Hz, once every
/// second and a half. The tone carries its second and third harmonics, because a siren heard through nothing
/// but a sine sounds like a test tone rather than a car.
///
/// The loop is exact rather than crossfaded, which is the one piece of care worth taking here: the sweep's
/// average frequency times the loop's length is a whole number of cycles, so the phase at the end of the loop
/// is the phase at the start of it and there is nothing to smooth out. Change either number and that stops
/// being true - so if the sweep is ever retuned, keep the two multiplied into a whole number.
/// </summary>
public static class PoliceSirenClip
{
    /// <summary>How long the loop is: four complete sweeps, and a whole number of carrier cycles.</summary>
    private const float Seconds = 6f;

    /// <summary>The tone the sweep swings around, and how far it swings either side of it.</summary>
    private const float CentreHz = 900f;
    private const float SpanHz = 300f;

    /// <summary>How long one up-and-down sweep takes.</summary>
    private const float SweepSeconds = 1.5f;

    private const int SampleRate = 22050;

    /// <summary>The peak the finished loop is scaled to, before the player's own settings.</summary>
    private const float Peak = 0.6f;

    private static AudioClip cached;

    /// <summary>The shared wail, made on first ask. One clip serves every police car in the game.</summary>
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
        int length = Mathf.RoundToInt(Seconds * SampleRate);
        float[] samples = new float[length];

        float phase = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float frequency = CentreHz + SpanHz * Mathf.Sin(2f * Mathf.PI * t / SweepSeconds);

            // Integrating the frequency, rather than reading the tone straight off a sine of it, is what keeps
            // the slide smooth: a tone whose pitch is rising is not the same thing as a sine at a rising rate.
            phase += 2f * Mathf.PI * frequency / SampleRate;

            samples[i] = Mathf.Sin(phase) + 0.32f * Mathf.Sin(2f * phase) + 0.12f * Mathf.Sin(3f * phase);
        }

        Normalise(samples);

        AudioClip clip = AudioClip.Create("Police Siren (generated)", length, 1, SampleRate, false);
        clip.SetData(samples, 0);

        return clip;
    }

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
