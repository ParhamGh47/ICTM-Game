using UnityEngine;

/// <summary>
/// The four sounds a boost makes, made rather than shipped: the wet <b>slurp</b> of a milkshake being drunk as
/// the truck drives over it, the small bright <b>chime</b> that says one has gone into the tank, the
/// <b>whoosh</b> of one being spent, and the dry <b>knock</b> of the button being pressed with nothing left to
/// spend.
///
/// There is no boost recording in the project, and a pickup with no sound is a pickup the driver does not
/// notice - so <see cref="BoostSounds"/> asks for these. Making them costs a couple of milliseconds once,
/// puts nothing in the build, and means the four sounds are a matched set, since they are built from the same
/// handful of voices: filtered noise, a low body under it and a tone. The milkshake is wet and rising, which
/// is the sound of drinking something; the chime is a reward, three notes climbing an octave with a click and
/// a breath of froth on the front of them; the boost is longer, opens its top wide and swells, which is what
/// reads as being shoved forward. The empty tank is the one sound in the set that is not a reward: it is the
/// only one whose notes fall, so the ear hears that nothing happened without having to be told.
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
    private const float EmptySeconds = 0.8f;

    /// <summary>
    /// The peak each finished sound is scaled to, before the player's own settings. The two the player has to
    /// hear over a level - the shove of a boost and the refusal of an empty tank - fill the scale; the sip and
    /// the jingle are left a little under it, because they come often and are meant to sit behind the engine
    /// rather than in front of it.
    /// </summary>
    private const float PickupPeak = 0.7f;
    private const float CollectPeak = 0.8f;
    private const float BoostPeak = 1f;
    private const float EmptyPeak = 1f;

    /// <summary>
    /// The empty tank's two knocks and the puff of air between them, in Hz.
    ///
    /// These sit around ten times higher than the first version of this sound did, and that is the whole
    /// reason it was inaudible: knocks at 130-210 Hz are almost entirely inside the range a laptop or a phone
    /// speaker cannot reproduce at all, so the sound was being played and simply not coming out. Everything
    /// that matters here is between 250 Hz and 3 kHz, which any speaker can manage, with the two lowest
    /// partials only filling in weight on something with a woofer.
    /// </summary>
    private const float EmptyKnockPitch = 430f;
    private const float EmptyKnockFall = 300f;
    private const float EmptySecondPitch = 360f;
    private const float EmptySecondFall = 250f;

    /// <summary>Where the second knock lands, and how loud it is against the first.</summary>
    private const float EmptySecondAt = 0.19f;
    private const float EmptySecondLevel = 0.62f;

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
    private const int EmptySeed = 20260931;

    private static AudioClip pickup;
    private static AudioClip collect;
    private static AudioClip boost;
    private static AudioClip empty;

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

    /// <summary>The boost button pressed with nothing in the tank.</summary>
    public static AudioClip Empty
    {
        get
        {
            if (empty == null) empty = BuildEmpty();

            return empty;
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

    // ---------------------------------------------------------------- the empty tank

    /// <summary>
    /// A dry double knock over a puff of escaping air, and nothing else: the button travelled, the tank
    /// answered, and there was nothing behind it. Both knocks fall as they die - 430 Hz down to 300, then
    /// 360 down to 250 - and that is what makes it read as something being asked of an empty tank rather
    /// than as something happening. The second is further behind and quieter than the first, so the pair
    /// trails off rather than sounding like two hits.
    ///
    /// Three things make it a sound rather than a thud. The knock is <b>wooden</b>: its partials are set at
    /// inharmonic ratios of the fundamental, which is what the ear hears as plastic and dead rather than as
    /// metal or as a note. It is opened by a <b>snap</b> - a few milliseconds of broadband noise, which is
    /// the button travelling and is most of what makes the press feel answered. And under it is a
    /// <b>puff</b>: noise whose top falls from 3.6 kHz to 800 Hz as it dies, the sound of air leaving
    /// something that has nothing to give, which is the part that gives the sound its length and its
    /// character.
    /// </summary>
    private static AudioClip BuildEmpty()
    {
        System.Random random = new System.Random(EmptySeed);

        int length = Mathf.RoundToInt(EmptySeconds * SampleRate);
        float[] samples = new float[length];

        float snap = 0f;
        float puff = 0f;
        float firstPhase = 0f;
        float secondPhase = 0f;
        float bodyPhase = 0f;

        // The first knock is struck a moment after the snap, so the button is heard to travel before the
        // thing it was pressed against answers.
        const float firstAt = 0.01f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            // The button, and the frame around the tank: the top of the noise, gone in four milliseconds.
            snap = OnePole(ref snap, Noise(random), Coefficient(6000f));

            float click =
                snap * Attack(t, 0.0006f) * Mathf.Exp(-t / 0.008f);

            // The first knock: a woody one, with its partials at inharmonic ratios so it is a knock and not
            // a note, falling as it dies.
            float first = 0f;

            if (t >= firstAt)
            {
                float since = t - firstAt;

                firstPhase +=
                    2f * Mathf.PI * Mathf.Lerp(EmptyKnockPitch, EmptyKnockFall, Mathf.Clamp01(since / 0.07f)) /
                    SampleRate;

                first = Knock(firstPhase, since, 0.055f);
            }

            // The second knock, behind it and quieter: the same answer, given up on.
            float second = 0f;

            if (t >= EmptySecondAt)
            {
                float since = t - EmptySecondAt;

                secondPhase +=
                    2f * Mathf.PI *
                    Mathf.Lerp(EmptySecondPitch, EmptySecondFall, Mathf.Clamp01(since / 0.07f)) / SampleRate;

                second = Knock(secondPhase, since, 0.05f);
            }

            // The puff: air leaving it, its top falling away as it goes, which is what gives the sound its
            // length without adding a tone to it. This is the part that runs on - a refusing button that is
            // over in a quarter of a second is a click, and what the driver should hear is the tank letting
            // the air out of them.
            float corner =
                Mathf.Lerp(3600f, 700f, Mathf.Clamp01(t / 0.35f));

            puff = OnePole(ref puff, Noise(random), Coefficient(corner));

            float air =
                puff * Attack(t, 0.008f) * Mathf.Exp(-t / 0.3f);

            // And a low body under all of it, for the weight that only a bigger speaker will show.
            bodyPhase += 2f * Mathf.PI * 95f / SampleRate;

            float body =
                Mathf.Sin(bodyPhase) * Attack(t, 0.004f) * Mathf.Exp(-t / 0.18f);

            samples[i] = 0.38f * click + 1.05f * first +
                         0.5f * EmptySecondLevel * second + 0.5f * air + 0.32f * body;
        }

        Normalise(samples, EmptyPeak);
        FadeOut(samples, 0.12f);

        return Make("Boost empty (generated)", samples);
    }

    /// <summary>
    /// One struck knock: a fundamental with two partials over it at deliberately <b>inharmonic</b> ratios
    /// (2.35 and 3.9, rather than the 2 and 3 of a musical note), each dying faster than the one below it.
    /// Inharmonic partials are what the ear calls wood, plastic or a dull clack; evenly spaced ones would
    /// make it a drum or a bell.
    /// </summary>
    private static float Knock(float phase, float since, float decay)
    {
        return Attack(since, 0.0012f) * (
            Mathf.Sin(phase) * Mathf.Exp(-since / decay) +
            0.6f * Mathf.Sin(phase * 2.35f) * Mathf.Exp(-since / (decay * 0.66f)) +
            0.34f * Mathf.Sin(phase * 3.9f) * Mathf.Exp(-since / (decay * 0.42f)));
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
