using UnityEngine;

/// <summary>
/// The three sounds a boost makes, made rather than shipped: the wet <b>slurp</b> of a milkshake being drunk as
/// the truck drives over it, the small bright <b>chime</b> that says one has gone into the tank, and the
/// <b>whoosh</b> of one being spent.
///
/// There is no boost recording in the project, and a pickup with no sound is a pickup the driver does not
/// notice - so <see cref="BoostSounds"/> asks for these. Making them costs a couple of milliseconds once,
/// puts nothing in the build, and means the three sounds are a matched set, since they are built from the same
/// handful of voices: filtered noise, a low body under it and a tone. The milkshake is wet and rising, which
/// is the sound of drinking something; the chime is a reward, three notes climbing an octave with a click and
/// a breath of froth on the front of them; the boost is longer, opens its top wide and swells, which is what
/// reads as being shoved forward.
///
/// Both are deterministic: the same seed every run, so a level sounds the same in every session and a change
/// to one of them can be compared against the last. Each is a one-shot with a tail that is faded to silence,
/// so nothing has to loop them.
/// </summary>
public static class BoostClip
{
    /// <summary>The rate the sounds are generated at. They are short and mostly low, so full rate is cheap.</summary>
    private const int SampleRate = 44100;

    /// <summary>How long each sound is. The sip is short, the chime rings out, the shove has a tail.</summary>
    private const float PickupSeconds = 0.42f;
    private const float CollectSeconds = 0.9f;
    private const float BoostSeconds = 1.1f;

    /// <summary>The peak each finished sound is scaled to, before the player's own settings.</summary>
    private const float PickupPeak = 0.7f;
    private const float CollectPeak = 0.8f;
    private const float BoostPeak = 0.85f;

    /// <summary>
    /// The chime's three notes: when each lands and what it is. C6, G6 and then C7 - the fifth and then the
    /// octave of the first - so the whole figure climbs, which is what the ear hears as a reward rather than as
    /// an alarm. Each note is struck a little after the last and rings longer than it, so the top one is the
    /// one left ringing when the sound finishes.
    /// </summary>
    private const float FirstNoteAt = 0.05f;
    private const float FirstNotePitch = 1046.5f;
    private const float SecondNoteAt = 0.13f;
    private const float SecondNotePitch = 1568f;
    private const float ThirdNoteAt = 0.21f;
    private const float ThirdNotePitch = 2093f;

    /// <summary>How long the glide into the first note takes, and where it starts from.</summary>
    private const float LiftSeconds = 0.05f;
    private const float LiftStartPitch = 520f;

    /// <summary>Fixed, so each sound is the same every time it is made.</summary>
    private const int PickupSeed = 20260928;
    private const int BoostSeed = 20260929;
    private const int CollectSeed = 20260930;

    private static AudioClip pickup;
    private static AudioClip collect;
    private static AudioClip boost;

    /// <summary>The milkshake going into the tank.</summary>
    public static AudioClip Pickup
    {
        get
        {
            if (pickup == null) pickup = BuildPickup();

            return pickup;
        }
    }

    /// <summary>A milkshake going into the tank: the reward on top of <see cref="Pickup"/>'s sip.</summary>
    public static AudioClip Collect
    {
        get
        {
            if (collect == null) collect = BuildCollect();

            return collect;
        }
    }

    /// <summary>A boost being spent.</summary>
    public static AudioClip Boost
    {
        get
        {
            if (boost == null) boost = BuildBoost();

            return boost;
        }
    }

    // ---------------------------------------------------------------- the milkshake

    /// <summary>
    /// Three things in a row. A slurp: the top of the noise climbing as the glass empties, which is the ear's
    /// cue for liquid being drawn up a straw, wobbled slowly so it reads as something thick rather than as
    /// hiss. A gulp underneath it as it goes down. And a clean note rising a fifth on top - the "got it".
    /// </summary>
    private static AudioClip BuildPickup()
    {
        System.Random random = new System.Random(PickupSeed);

        int length = Mathf.RoundToInt(PickupSeconds * SampleRate);
        float[] samples = new float[length];

        float slosh = 0f;
        float gulpPhase = 0f;
        float popPhase = 0f;

        // When the glass is finished: the gulp and the pop land together, just after the slurp has got going.
        const float gulpAt = 0.14f;
        const float popAt = 0.17f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            // The slurp: the corner climbing, so the noise goes from a low pull to a thin draw, and the whole
            // lot swelling and then stopping as the straw pulls air.
            float corner = Mathf.Lerp(240f, 2600f, Mathf.Clamp01(t / 0.16f));
            float slurp = OnePole(ref slosh, Noise(random), Coefficient(corner));
            slurp *= (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 34f * t)) * Mathf.Exp(-t / 0.09f);

            // The gulp: low, short and once, under the slurp.
            float gulp = 0f;

            if (t >= gulpAt)
            {
                float u = t - gulpAt;
                gulpPhase += 2f * Mathf.PI * Mathf.Lerp(185f, 95f, Mathf.Clamp01(u / 0.2f)) / SampleRate;
                gulp = Mathf.Sin(gulpPhase) * Attack(u, 0.004f) * Mathf.Exp(-u / 0.045f);
            }

            // The pop: one clean note climbing a fifth, bright enough to be heard over the engine at full
            // throttle, with a touch of second harmonic so it rings rather than beeps.
            float pop = 0f;

            if (t >= popAt)
            {
                float u = t - popAt;
                popPhase += 2f * Mathf.PI * Mathf.Lerp(700f, 1180f, Mathf.Clamp01(u / 0.16f)) / SampleRate;
                pop = (Mathf.Sin(popPhase) + 0.35f * Mathf.Sin(2f * popPhase)) * Attack(u, 0.006f) * Mathf.Exp(-u / 0.085f);
            }

            samples[i] = 0.55f * slurp + 0.6f * gulp + 0.5f * pop;
        }

        Normalise(samples, PickupPeak);
        FadeOut(samples, 0.02f);

        return Make("Boost pickup (generated)", samples);
    }

    // ---------------------------------------------------------------- the reward

    /// <summary>
    /// The "+1": three notes climbing an octave - the shape of a coin pick-up - without the metal, because this
    /// is a milkshake. It is opened by two things at once: a click, which is the very top of the noise gone in
    /// six milliseconds and is what gives the chime an edge to start on rather than only a tone, and a breath of
    /// froth, the fizz on the top of a shake. A quiet glide then climbs into the first note, so the figure
    /// sounds like it arrives somewhere instead of starting from nowhere.
    ///
    /// The three notes are struck rather than blown, each one landing a little after the last and ringing longer
    /// than it, so the sound builds and finishes on the top note.
    /// </summary>
    private static AudioClip BuildCollect()
    {
        System.Random random = new System.Random(CollectSeed);

        int length = Mathf.RoundToInt(CollectSeconds * SampleRate);
        float[] samples = new float[length];

        float click = 0f;
        float froth = 0f;
        float liftPhase = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            click = OnePole(ref click, Noise(random), Coefficient(12000f));
            float edge = click * Attack(t, 0.001f) * Mathf.Exp(-t / 0.006f);

            froth = OnePole(ref froth, Noise(random), Coefficient(9000f));
            float fizz = froth * Attack(t, 0.004f) * Mathf.Exp(-t / 0.03f);

            float lift = 0f;

            if (t < LiftSeconds)
            {
                liftPhase += 2f * Mathf.PI * Mathf.Lerp(LiftStartPitch, FirstNotePitch, t / LiftSeconds) / SampleRate;
                lift = Mathf.Sin(liftPhase) * Attack(t, 0.002f) * Mathf.Exp(-t / 0.028f);
            }

            float first = Note(t - FirstNoteAt, FirstNotePitch, 0.22f);
            float second = Note(t - SecondNoteAt, SecondNotePitch, 0.28f);
            float third = Note(t - ThirdNoteAt, ThirdNotePitch, 0.5f);

            samples[i] = 0.35f * edge + 0.2f * fizz + 0.2f * lift +
                         0.5f * first + 0.55f * second + 0.6f * third;
        }

        Normalise(samples, CollectPeak);
        FadeOut(samples, 0.12f);

        return Make("Boost collect (generated)", samples);
    }

    /// <summary>
    /// One struck note: a fundamental with four partials over it, the quiet ones up top ringing on a little
    /// after the note they belong to - which is what gives it its edge - and a millisecond and a half to get
    /// going so it does not start with a step. Silent before it is due.
    /// </summary>
    private static float Note(float since, float frequency, float decay)
    {
        if (since < 0f) return 0f;

        float phase = 2f * Mathf.PI * frequency * since;

        return Attack(since, 0.0015f) * (
            Mathf.Sin(phase) * Mathf.Exp(-since / decay) +
            0.55f * Mathf.Sin(2f * phase) * Mathf.Exp(-since / (decay * 0.85f)) +
            0.32f * Mathf.Sin(3f * phase) * Mathf.Exp(-since / (decay * 0.7f)) +
            0.16f * Mathf.Sin(4f * phase) * Mathf.Exp(-since / (decay * 0.55f)));
    }

    // ---------------------------------------------------------------- the shove

    /// <summary>
    /// A wide whoosh with an engine surging underneath it. The top of the noise is thrown open over the first
    /// third of a second and then closes again behind the truck, which is what a shove forward sounds like from
    /// the driver's seat; the note underneath climbs a couple of octaves and holds at the top, with its odd
    /// harmonics in so it reads as power rather than as a tone.
    /// </summary>
    private static AudioClip BuildBoost()
    {
        System.Random random = new System.Random(BoostSeed);

        int length = Mathf.RoundToInt(BoostSeconds * SampleRate);
        float[] samples = new float[length];

        float air = 0f;
        float surgePhase = 0f;

        const float openFor = 0.3f;         // how long the top keeps opening before it closes again
        const float climbFor = 0.75f;       // how long the note takes to reach its top

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            float corner = t < openFor
                ? Mathf.Lerp(260f, 6200f, t / openFor)
                : Mathf.Lerp(6200f, 1300f, Mathf.Clamp01((t - openFor) / 0.8f));

            float whoosh = OnePole(ref air, Noise(random), Coefficient(corner));
            whoosh *= Attack(t, 0.05f) * Mathf.Exp(-t / 0.5f);

            surgePhase += 2f * Mathf.PI * Mathf.Lerp(85f, 330f, Mathf.Clamp01(t / climbFor)) / SampleRate;

            float surge = Mathf.Sin(surgePhase) +
                          0.5f * Mathf.Sin(2f * surgePhase) +
                          0.25f * Mathf.Sin(3f * surgePhase);

            surge *= Attack(t, 0.03f) * Mathf.Exp(-t / 0.7f);

            samples[i] = 0.7f * whoosh + 0.45f * surge;
        }

        Normalise(samples, BoostPeak);
        FadeOut(samples, 0.25f);

        return Make("Boost fire (generated)", samples);
    }

    // ---------------------------------------------------------------- helpers

    private static float Noise(System.Random random)
    {
        return (float)(random.NextDouble() * 2.0 - 1.0);
    }

    /// <summary>One pole of a low-pass, which is all these sounds need.</summary>
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

    /// <summary>Ramps in from silence over <paramref name="seconds"/>, so a voice does not start with a step.</summary>
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

    /// <summary>Takes the end down to silence, so the one-shot finishes on nothing rather than on a click.</summary>
    private static void FadeOut(float[] samples, float seconds)
    {
        int fade = Mathf.Clamp(Mathf.RoundToInt(seconds * SampleRate), 1, samples.Length);

        for (int i = 0; i < fade; i++)
            samples[samples.Length - fade + i] *= 1f - i / (float)fade;
    }

    private static AudioClip Make(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);

        return clip;
    }
}
