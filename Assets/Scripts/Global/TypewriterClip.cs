using UnityEngine;

/// <summary>
/// The sounds a typewriter makes as a story types itself out, made rather than shipped: a short <b>clack</b> for
/// an ordinary character, and a heavier, lower one for the bar at the bottom of the keyboard - the space and
/// the line break - which is what a real typewriter does.
///
/// There is no typewriter recording in the project, and a story that appears in silence loses the one thing that
/// says it is being typed at all. Making them costs a couple of milliseconds once, puts nothing in the build,
/// and keeps the two the same instrument: both are the same three voices, a bright click of the key, a thump of
/// the platen under it and a small ring of the typebar, and the bar's version is only the same gesture lower,
/// longer and darker.
///
/// Every clack is a single key on its own, so the variety comes from how <see cref="StoryTypeWriter"/> plays
/// them - retriggered at a slightly different pitch and level each time - rather than from a set of recordings.
/// Both are deterministic: the same seed every run, so a story sounds the same in every session and a change to
/// one of them can be compared against the last.
/// </summary>
public static class TypewriterClip
{
    /// <summary>The rate the sounds are generated at. They are short and mostly mid-range, so full rate is cheap.</summary>
    private const int SampleRate = 44100;

    /// <summary>
    /// How long each sound is. Short on purpose: a keystroke is over in a moment, and a long clip would have a
    /// tail hanging over the next one at any typing speed worth listening to.
    /// </summary>
    private const float KeySeconds = 0.055f;
    private const float BarSeconds = 0.085f;

    /// <summary>The peak each finished sound is scaled to, before the player's own settings. The bar is heavier.</summary>
    private const float KeyPeak = 0.6f;
    private const float BarPeak = 0.75f;

    /// <summary>How much of the end is faded to silence. A one-shot that stops on a non-zero sample clicks.</summary>
    private const float FadeOutSeconds = 0.004f;

    /// <summary>Fixed, so each sound is the same every time it is made.</summary>
    private const int KeySeed = 20260925;
    private const int BarSeed = 20260926;

    private static AudioClip key;
    private static AudioClip bar;

    /// <summary>One key of the letters: the clack a character being typed makes.</summary>
    public static AudioClip Key
    {
        get
        {
            if (key == null) key = BuildKey();

            return key;
        }
    }

    /// <summary>The space bar and the carriage return: the same clack, heavier and lower.</summary>
    public static AudioClip Bar
    {
        get
        {
            if (bar == null) bar = BuildBar();

            return bar;
        }
    }

    // ---------------------------------------------------------------- the two sounds

    private static AudioClip BuildKey()
    {
        return Build("Typewriter Key", KeySeconds, KeyPeak, KeySeed,
            click: 2600f, clack: 190f, ring: 3100f, life: 0.012f, depth: 1f);
    }

    private static AudioClip BuildBar()
    {
        // The bar is the same key struck with more of the machine behind it: everything below the click is
        // lower and lasts about half again as long, which is what makes it read as the wide key.
        return Build("Typewriter Bar", BarSeconds, BarPeak, BarSeed,
            click: 1700f, clack: 120f, ring: 2300f, life: 0.019f, depth: 1.35f);
    }

    /// <summary>
    /// The clack itself: three voices, all gone in a few milliseconds.
    ///
    /// The <b>click</b> is the key coming down - a burst of noise with a band taken out of the middle of it, so
    /// it is bright and hollow rather than a hiss. The <b>clack</b> is the platen and the paper underneath: a
    /// low, quickly damped tone, which is the weight of the sound. The <b>ring</b> is the typebar and its spring:
    /// a short, high, clean tone, which is the part the ear hears as a machine rather than as a knock on wood.
    /// </summary>
    private static AudioClip Build(
        string name, float seconds, float peak, int seed,
        float click, float clack, float ring, float life, float depth)
    {
        System.Random random = new System.Random(seed);

        int length = Mathf.RoundToInt(seconds * SampleRate);
        float[] samples = new float[length];

        float lowBody = 0f;     // the noise under the click's band
        float highBody = 0f;    // the noise above it
        float clackPhase = 0f;
        float ringPhase = 0f;

        float clickDecay = SampleRate * 0.0016f;        // how fast the click falls away, in samples
        float clackDecay = SampleRate * 0.0075f * depth;
        float ringDecay = SampleRate * 0.009f * depth;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            float white = Noise(random);

            // The click: two poles, one above the band and one below it, and what is left when they are taken
            // off each other is the band. A single low-pass would only ever sound like a dull thud.
            float below = OnePole(ref lowBody, white, Coefficient(click * 0.7f));
            float above = OnePole(ref highBody, white, Coefficient(click * 2.6f));
            float clickBand = above - below;

            // The clack: the machine's own weight, falling away before the click's ring is done.
            clackPhase += 2f * Mathf.PI * clack / SampleRate;
            float body = Mathf.Sin(clackPhase) * Mathf.Exp(-i / clackDecay);

            // The ring: the typebar, higher than the click's own band so it stands out of it.
            ringPhase += 2f * Mathf.PI * ring / SampleRate;
            float tone = Mathf.Sin(ringPhase) * Mathf.Exp(-i / ringDecay);

            float envelope = Attack(t, 0.0008f);

            samples[i] = envelope * (1.4f * clickBand * Mathf.Exp(-i / clickDecay) + 0.75f * body + 0.3f * tone);
        }

        Normalise(samples, peak);
        FadeOut(samples);

        return Make(name, samples);
    }

    // ---------------------------------------------------------------- helpers

    private static float Noise(System.Random random)
    {
        return (float)(random.NextDouble() * 2.0 - 1.0);
    }

    /// <summary>One pole of a low-pass, which is all a sound this short needs.</summary>
    private static float OnePole(ref float state, float input, float coefficient)
    {
        state += (input - state) * coefficient;

        return state;
    }

    /// <summary>What a one-pole filter needs to sit at a given corner frequency.</summary>
    private static float Coefficient(float cutoff)
    {
        return 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);
    }

    /// <summary>
    /// Ramps in from silence over <paramref name="seconds"/>. Only a fraction of a millisecond here: a keystroke
    /// has to start with the strike, but not with a step.
    /// </summary>
    private static float Attack(float t, float seconds)
    {
        return t >= seconds ? 1f : t / seconds;
    }

    /// <summary>
    /// Scales the sound so its loudest point is always the same, so the volume it is played at means the same
    /// thing however the noise above happened to fall.
    /// </summary>
    private static void Normalise(float[] samples, float peak)
    {
        float loudest = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float magnitude = Mathf.Abs(samples[i]);
            if (magnitude > loudest) loudest = magnitude;
        }

        if (loudest <= 0.0001f) return;

        float scale = peak / loudest;

        for (int i = 0; i < samples.Length; i++)
            samples[i] *= scale;
    }

    /// <summary>Takes the last few milliseconds down to silence, so the one-shot ends on nothing.</summary>
    private static void FadeOut(float[] samples)
    {
        int fade = Mathf.Clamp(Mathf.RoundToInt(FadeOutSeconds * SampleRate), 1, samples.Length);

        for (int i = 0; i < fade; i++)
            samples[samples.Length - fade + i] *= 1f - i / (float)fade;
    }

    private static AudioClip Make(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name + " (generated)", samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);

        return clip;
    }
}
