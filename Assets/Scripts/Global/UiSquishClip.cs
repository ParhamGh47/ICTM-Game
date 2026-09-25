using UnityEngine;

/// <summary>
/// The two sounds the menus make, made rather than shipped: a short, soft <b>squish</b> as the highlight moves
/// from one button to the next, and a heavier, wetter one with a splatter in it when a button is confirmed.
///
/// There is no UI recording in the project, and a menu of silent buttons is a menu that feels dead - so
/// <see cref="UiSounds"/> asks for these. Making them costs a couple of milliseconds once, puts nothing in the
/// build, and means the two sounds are exactly the pair they should be, since they are built from the same
/// three voices: a body of low-passed noise, a falling squeak on top of it and a thump underneath. The
/// confirm is the same squish, longer and lower - the noise corner falls further and the thump is bigger -
/// with a tail of wet grains scattered behind it, which is the splatter.
///
/// Both are deterministic: the same seed every run, so a menu sounds the same in every session and a change to
/// one of them can be compared against the last. Each is a one-shot, so both are faded out at the end rather
/// than being built to loop.
/// </summary>
public static class UiSquishClip
{
    /// <summary>The rate the sounds are generated at. They are short and mostly low, so full rate is cheap.</summary>
    private const int SampleRate = 44100;

    /// <summary>How long each sound is. The move is a tap; the confirm has a splatter to hear out.</summary>
    private const float MoveSeconds = 0.17f;
    private const float ConfirmSeconds = 0.55f;

    /// <summary>The peak each finished sound is scaled to, before the player's own settings.</summary>
    private const float MovePeak = 0.55f;
    private const float ConfirmPeak = 0.8f;

    /// <summary>How much of the end is faded to silence. A one-shot that stops on a non-zero sample clicks.</summary>
    private const float FadeOutSeconds = 0.008f;

    /// <summary>Fixed, so each sound is the same every time it is made.</summary>
    private const int MoveSeed = 20260926;
    private const int ConfirmSeed = 20260927;

    private static AudioClip move;
    private static AudioClip confirm;

    /// <summary>The squish the highlight moving between buttons makes.</summary>
    public static AudioClip Move
    {
        get
        {
            if (move == null) move = BuildMove();

            return move;
        }
    }

    /// <summary>The splat a button being confirmed makes.</summary>
    public static AudioClip Confirm
    {
        get
        {
            if (confirm == null) confirm = BuildConfirm();

            return confirm;
        }
    }

    // ---------------------------------------------------------------- the move

    /// <summary>
    /// A soft wet tap: noise with its top taken off and its corner falling, a short tone falling with it, and a
    /// low thump to give it weight. Everything falls away in about a sixth of a second.
    /// </summary>
    private static AudioClip BuildMove()
    {
        System.Random random = new System.Random(MoveSeed);

        int length = Mathf.RoundToInt(MoveSeconds * SampleRate);
        float[] samples = new float[length];

        float body = 0f;
        float squeakPhase = 0f;
        float thumpPhase = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float through = t / MoveSeconds;            // 0 at the start, 1 at the end

            // The body: noise with the top taken off, and the corner falling as the squish is squeezed out -
            // which is what makes it a wet push rather than a click.
            float corner = Mathf.Lerp(2400f, 420f, through);
            float noise = OnePole(ref body, Noise(random), Coefficient(corner));

            // The squeak: a small, tight tone falling away, which is the "squ" of it. Wobbled slowly, so it
            // reads as something wet moving rather than as a note.
            squeakPhase += 2f * Mathf.PI * Mathf.Lerp(540f, 210f, through) / SampleRate;
            float squeak = Mathf.Sin(squeakPhase) * (1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 55f * t));

            // The thump underneath: low, and gone before the squeak is.
            thumpPhase += 2f * Mathf.PI * Mathf.Lerp(150f, 92f, through) / SampleRate;
            float thump = Mathf.Sin(thumpPhase) * Mathf.Exp(-t / 0.018f);

            float envelope = Attack(t, 0.003f) * Mathf.Exp(-t / 0.032f);

            samples[i] = envelope * (noise + 0.5f * squeak + 0.6f * thump);
        }

        Normalise(samples, MovePeak);
        FadeOut(samples);

        return Make("UI Squish (move)", samples);
    }

    // ---------------------------------------------------------------- the confirm

    /// <summary>
    /// The same squish, heavier and twice as long, with a splatter of wet grains behind it and a squelchy tail
    /// under them. This is a button being confirmed, so it is allowed to be a little unpleasant.
    /// </summary>
    private static AudioClip BuildConfirm()
    {
        System.Random random = new System.Random(ConfirmSeed);

        int length = Mathf.RoundToInt(ConfirmSeconds * SampleRate);
        float[] samples = new float[length];

        float body = 0f;
        float tail = 0f;
        float squeakPhase = 0f;
        float thumpPhase = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float through = t / ConfirmSeconds;

            float white = Noise(random);

            // The body is the move's, but lower and longer: the corner starts well under where the move's does
            // by the time the ear is listening, so the same gesture reads as a bigger, fleshier push.
            float corner = Mathf.Lerp(1600f, 240f, Mathf.Min(1f, through * 1.6f));
            float noise = OnePole(ref body, white, Coefficient(corner));

            // The tail: a slower, darker noise with a slow wobble on it - the sound of something wet giving
            // way rather than a clean impact.
            float wet = OnePole(ref tail, white, Coefficient(680f));
            wet *= 1f + 0.45f * Mathf.Sin(2f * Mathf.PI * 42f * t);

            squeakPhase += 2f * Mathf.PI * Mathf.Lerp(430f, 120f, through) / SampleRate;
            float squeak = Mathf.Sin(squeakPhase) * (1f + 0.5f * Mathf.Sin(2f * Mathf.PI * 38f * t));

            thumpPhase += 2f * Mathf.PI * Mathf.Lerp(135f, 55f, through) / SampleRate;
            float thump = Mathf.Sin(thumpPhase) * Mathf.Exp(-t / 0.05f);

            float envelope = Attack(t, 0.004f) * Mathf.Exp(-t / 0.07f);

            samples[i] = envelope * (noise + 0.55f * wet + 0.35f * squeak + 0.8f * thump);
        }

        ScatterGrains(samples, random);

        Normalise(samples, ConfirmPeak);
        FadeOut(samples);

        return Make("UI Squish (confirm)", samples);
    }

    /// <summary>
    /// The splatter: short, bright, quickly damped droplets thrown behind the impact, scattered over the first
    /// third of the sound at irregular intervals and covering a wide range of pitches. Short bursts of tone are
    /// what the ear hears as spittle; noise would only sound like the hiss that is already under it.
    /// </summary>
    private static void ScatterGrains(float[] samples, System.Random random)
    {
        int grains = 26;

        for (int g = 0; g < grains; g++)
        {
            // Bunched towards the start - most of a splatter lands at once - but never on a regular beat.
            float at = ConfirmSeconds * 0.32f * Mathf.Pow((float)random.NextDouble(), 1.7f);
            int start = Mathf.RoundToInt(at * SampleRate);

            float frequency = 700f + (float)random.NextDouble() * 2800f;
            float decay = SampleRate * (0.0015f + (float)random.NextDouble() * 0.004f);
            float level = 0.25f + (float)random.NextDouble() * 0.75f;
            int reach = Mathf.Max(8, Mathf.RoundToInt(decay * 4f));

            for (int j = 0; j < reach; j++)
            {
                int index = start + j;
                if (index >= samples.Length) break;

                float envelope = Mathf.Exp(-j / decay);

                samples[index] += level * envelope * Mathf.Sin(2f * Mathf.PI * frequency * j / SampleRate);
            }
        }
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

    /// <summary>Ramps in from silence over <paramref name="seconds"/>, so the sound does not start with a step.</summary>
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
