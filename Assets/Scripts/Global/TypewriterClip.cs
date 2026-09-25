using UnityEngine;

/// <summary>
/// The sounds a typewriter makes as a story types itself out, made rather than shipped.
///
/// There is no typewriter recording in the project, and a story that appears in silence loses the one thing
/// that says it is being typed at all. Making them costs a few milliseconds once and puts nothing in the
/// build. Every sound here is the same three things happening in the same order - a key strikes the paper,
/// the platen takes the blow, the typebar rings on its way back - and the sounds differ only in how much of
/// each is in them, which is what makes four keys and a space bar sound like one machine.
///
/// The set:
/// <list type="bullet">
/// <item><b>Key</b> - the letters. Four voices, each a slightly different typebar, so a character can be
/// given the same key every time it is typed. <see cref="VoiceFor"/> is the mapping.</item>
/// <item><b>Bar</b> - the space bar: the same strike with more of the machine behind it.</item>
/// <item><b>Return</b> - a line break: the bar again, and then the carriage running back along its rail.</item>
/// <item><b>Bell</b> - the little bell at the margin, which on a typewriter is the sound that comes just
/// before the carriage does.</item>
/// </list>
///
/// The voices are not decoration. One clack retriggered at different pitches is an obvious trick the moment
/// a sentence is long enough to hear it; giving each character its own typebar means a page of prose is
/// spread over four genuinely different sounds without any of them being picked at random, and the pitch
/// wobble <see cref="StoryTypeWriter"/> adds on top is what keeps a repeated character from being identical.
///
/// All of them are deterministic - the same seed every run - so a story sounds the same in every session and
/// a change to one of them can be compared against the last.
/// </summary>
public static class TypewriterClip
{
    /// <summary>The rate the sounds are generated at. They are short and mostly mid-range, so full rate is cheap.</summary>
    private const int SampleRate = 44100;

    /// <summary>
    /// How many different keys the letters are struck with. Four is enough to break the repetition and few
    /// enough that each one is still a distinct instrument rather than a shade of the last.
    /// </summary>
    public const int KeyCount = 4;

    /// <summary>
    /// How long each sound is. The strikes are short on purpose - a keystroke is over in a moment, and a long
    /// clip would have a tail hanging over the next one at any typing speed worth listening to. The return is
    /// longer because the carriage is in it, and the bell longer still because it rings.
    /// </summary>
    private const float KeySeconds = 0.06f;
    private const float BarSeconds = 0.085f;
    private const float ReturnSeconds = 0.21f;
    private const float BellSeconds = 1f;

    /// <summary>The peak each finished sound is scaled to, before the player's own settings. The bar is heavier.</summary>
    private const float KeyPeak = 0.6f;
    private const float BarPeak = 0.75f;
    private const float ReturnPeak = 0.7f;
    private const float BellPeak = 0.5f;

    /// <summary>
    /// How much of the end is taken down to silence. A one-shot that stops on a non-zero sample clicks, and a
    /// clip that is still ringing when it ends needs a longer fade than one that has already died: a few
    /// milliseconds would only put a step where the bell's tail used to be.
    /// </summary>
    private const float StrikeFadeSeconds = 0.004f;
    private const float BellFadeSeconds = 0.09f;

    /// <summary>How quickly the strike itself is over - the click's own decay. Roughly two milliseconds.</summary>
    private const float StrikeDecay = 0.0021f;

    /// <summary>The paper and the springs under the strike: a whisper of noise that outlives it.</summary>
    private const float RustleCentre = 900f;
    private const float RustleDecay = 0.02f;

    /// <summary>How the strike ramps in. Only a fraction of a millisecond: it has to start with the strike, but not with a step.</summary>
    private const float AttackSeconds = 0.0006f;

    /// <summary>Where the carriage's own noise runs from and to as it comes back across the rail.</summary>
    private const float CarriageLow = 700f;
    private const float CarriageHigh = 3200f;

    /// <summary>
    /// The typebar's partials. A struck bar of metal does not ring at whole multiples of one note - it rings at
    /// awkward ratios, and the three here are what makes it metal rather than a sine. The levels and lives
    /// follow the same order: the lowest partial is loudest and lasts longest, so the top ones brighten the
    /// strike and get out of the way.
    /// </summary>
    private static readonly float[] BarRatios = { 1f, 1.62f, 2.41f };
    private static readonly float[] BarLevels = { 1f, 0.55f, 0.3f };
    private static readonly float[] BarLives = { 1f, 0.7f, 0.45f };

    /// <summary>
    /// The bell: a small one, which is a note and its twelfth and something two octaves up, all struck together
    /// and left to die at their own rates. That pair of high partials dying faster than the note under them is
    /// the whole difference between a bell and a beep.
    /// </summary>
    private static readonly float[] BellRatios = { 1f, 2.76f, 5.4f };
    private static readonly float[] BellLevels = { 1f, 0.45f, 0.2f };
    private static readonly float[] BellLives = { 0.36f, 0.2f, 0.09f };     // seconds

    private const float BellNote = 2100f;

    /// <summary>Fixed, so each sound is the same every time it is made.</summary>
    private const int BarSeed = 20260926;
    private const int ReturnSeed = 20260927;
    private const int BellSeed = 20260928;

    /// <summary>
    /// The four keys. Different typebars sit at different places on the machine, so each is a little brighter
    /// or duller, a little heavier or lighter, and rings a little higher or lower than its neighbours. The
    /// seeds and not just the numbers differ too, so the noise under each one is its own.
    /// </summary>
    private static readonly float[] VoiceClick = { 2700f, 2350f, 3100f, 2150f };
    private static readonly float[] VoiceThock = { 158f, 186f, 142f, 172f };
    private static readonly float[] VoiceRing = { 3300f, 2950f, 3650f, 2750f };
    private static readonly float[] VoiceLife = { 0.0135f, 0.0155f, 0.0125f, 0.0165f };
    private static readonly float[] VoiceMetal = { 0.95f, 0.8f, 1.1f, 0.7f };
    private static readonly int[] VoiceSeed = { 20260921, 20260922, 20260923, 20260924 };

    private static AudioClip[] keys;
    private static AudioClip bar;
    private static AudioClip carriageReturn;
    private static AudioClip bell;

    /// <summary>
    /// One key of the letters, by voice number. Anything outside the voices is wrapped, so a caller can hand
    /// over whatever number it has.
    /// </summary>
    public static AudioClip Key(int voice)
    {
        voice = ((voice % KeyCount) + KeyCount) % KeyCount;

        if (keys == null) keys = new AudioClip[KeyCount];

        if (keys[voice] == null)
            keys[voice] = Build(KeySettings(voice), carriage: false);

        return keys[voice];
    }

    /// <summary>
    /// Which key a character is struck with. A letter keeps the typebar it is thrown by - the same character
    /// is always the same sound, the way it is on a machine - while different characters land on different
    /// ones.
    /// </summary>
    public static int VoiceFor(char character)
    {
        // A multiplicative hash rather than the obvious "character % KeyCount", which walks the four voices
        // in a four-step loop down the alphabet and is audible in a sentence.
        uint scattered = (uint)character * 2654435761u;

        return (int)((scattered >> 24) % KeyCount);
    }

    /// <summary>The space bar and the tab: the same clack, heavier and lower.</summary>
    public static AudioClip Bar
    {
        get
        {
            if (bar == null)
                bar = Build(
                    new Strike
                    {
                        name = "Typewriter Bar",
                        seconds = BarSeconds,
                        peak = BarPeak,
                        seed = BarSeed,
                        click = 1700f,
                        thock = 120f,
                        ring = 2300f,
                        life = 0.019f,
                        metal = 1f,
                    },
                    carriage: false);

            return bar;
        }
    }

    /// <summary>The space bar again, with the carriage running back along its rail after it.</summary>
    public static AudioClip Return
    {
        get
        {
            if (carriageReturn == null)
                carriageReturn = Build(
                    new Strike
                    {
                        name = "Typewriter Return",
                        seconds = ReturnSeconds,
                        peak = ReturnPeak,
                        seed = ReturnSeed,
                        click = 1750f,
                        thock = 118f,
                        ring = 2400f,
                        life = 0.017f,
                        metal = 0.9f,
                    },
                    carriage: true);

            return carriageReturn;
        }
    }

    /// <summary>The bell at the margin.</summary>
    public static AudioClip Bell
    {
        get
        {
            if (bell == null) bell = BuildBell();

            return bell;
        }
    }

    // ---------------------------------------------------------------- the strike

    /// <summary>Everything a struck key is made of, so the three of them can be built by one method.</summary>
    private struct Strike
    {
        public string name;
        public float seconds;
        public float peak;
        public int seed;
        public float click;     // where the strike's noise band sits: bright and hollow
        public float thock;     // the platen's own note, which is the weight of the sound
        public float ring;      // the typebar's base partial
        public float life;      // how long the whole thing takes to die away
        public float metal;     // how much of the typebar's ring is in it against the rest
    }

    private static Strike KeySettings(int voice)
    {
        return new Strike
        {
            name = "Typewriter Key " + (voice + 1),
            seconds = KeySeconds,
            peak = KeyPeak,
            seed = VoiceSeed[voice],
            click = VoiceClick[voice],
            thock = VoiceThock[voice],
            ring = VoiceRing[voice],
            life = VoiceLife[voice],
            metal = VoiceMetal[voice],
        };
    }

    /// <summary>
    /// A key being struck, and - if <paramref name="carriage"/> - the carriage coming back after it.
    ///
    /// The <b>strike</b> is the key landing on the paper: a burst of noise with a band taken out of the middle
    /// of it, so it is bright and hollow rather than a hiss. The <b>platen</b> is the roller and the paper
    /// underneath: a low note with a shorter one above it, which is the thump the hand feels. The <b>typebar</b>
    /// is the metal, ringing at its own awkward partials. The <b>rustle</b> is the paper and the springs, a
    /// whisper that outlives all of them.
    ///
    /// The carriage is a different thing wearing the same sound: it starts once the strike has happened, moves
    /// through a band of noise that climbs as it picks up speed, and stops at the other end when the spring
    /// runs out - which is why its own envelope rises and falls rather than just decaying.
    /// </summary>
    private static AudioClip Build(Strike s, bool carriage)
    {
        System.Random random = new System.Random(s.seed);

        int length = Mathf.RoundToInt(s.seconds * SampleRate);
        float[] samples = new float[length];

        float lowBody = 0f;         // the noise under the strike's band
        float highBody = 0f;        // the noise above it
        float thockPhase = 0f;
        float thockTopPhase = 0f;
        float rustleBody = 0f;
        float carriageBody = 0f;
        float carriageFloor = 0f;
        float carriagePhase = 0f;

        float[] barPhase = new float[BarRatios.Length];

        // The run takes the back half of the clip, so the strike is heard on its own before the carriage moves.
        float carriageStart = s.seconds * 0.24f;
        float carriageRun = Mathf.Max(0.001f, s.seconds - carriageStart);

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            float white = Noise(random);

            float below = OnePole(ref lowBody, white, Coefficient(s.click * 0.6f));
            float above = OnePole(ref highBody, white, Coefficient(s.click * 2.8f));
            float strikeVoice = (above - below) * Mathf.Exp(-t / StrikeDecay);

            thockPhase += 2f * Mathf.PI * s.thock / SampleRate;
            thockTopPhase += 2f * Mathf.PI * (s.thock * 2.37f) / SampleRate;

            float platen = Mathf.Sin(thockPhase) * Mathf.Exp(-t / (s.life * 1.6f))
                         + 0.45f * Mathf.Sin(thockTopPhase) * Mathf.Exp(-t / (s.life * 0.5f));

            float metal = 0f;
            for (int p = 0; p < BarRatios.Length; p++)
            {
                barPhase[p] += 2f * Mathf.PI * s.ring * BarRatios[p] / SampleRate;
                metal += BarLevels[p] * Mathf.Sin(barPhase[p]) * Mathf.Exp(-t / (s.life * BarLives[p]));
            }

            float rustle = OnePole(ref rustleBody, white, Coefficient(RustleCentre)) * Mathf.Exp(-t / RustleDecay);

            float run = 0f;

            if (carriage)
            {
                float since = t - carriageStart;

                if (since > 0f)
                {
                    float progress = Mathf.Clamp01(since / carriageRun);

                    // Up and back down: the carriage is pushed, runs, and is caught by the spring at the end.
                    float speed = Mathf.Sin(progress * Mathf.PI);

                    float centre = CarriageLow + (CarriageHigh - CarriageLow) * progress;
                    float bandBody = OnePole(ref carriageBody, white, Coefficient(centre));
                    float bandFloor = OnePole(ref carriageFloor, white, Coefficient(centre * 0.35f));

                    // The spring: a clean tone high in the run, wobbling the way anything on a spring does.
                    carriagePhase += 2f * Mathf.PI * (430f * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 21f * t))) / SampleRate;

                    run = speed * (0.5f * (bandBody - bandFloor) + 0.22f * Mathf.Sin(carriagePhase));
                }
            }

            samples[i] = Attack(t) * (
                1.5f * strikeVoice
                + 1.05f * platen
                + s.metal * 0.22f * metal
                + 0.05f * rustle
                + run);
        }

        Normalise(samples, s.peak);
        FadeOut(samples, StrikeFadeSeconds);

        return Make(s.name, samples);
    }

    // ---------------------------------------------------------------- the bell

    /// <summary>
    /// The bell at the margin: its partials, struck together by a bright little transient and left ringing.
    /// Nothing else in the set is allowed to hang in the air this long, which is what makes it the sound the
    /// end of a line is known by.
    /// </summary>
    private static AudioClip BuildBell()
    {
        System.Random random = new System.Random(BellSeed);

        int length = Mathf.RoundToInt(BellSeconds * SampleRate);
        float[] samples = new float[length];
        float[] phase = new float[BellRatios.Length];

        float strikeBelow = 0f;
        float strikeAbove = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            float white = Noise(random);

            float ring = 0f;
            for (int p = 0; p < BellRatios.Length; p++)
            {
                phase[p] += 2f * Mathf.PI * BellNote * BellRatios[p] / SampleRate;
                ring += BellLevels[p] * Mathf.Sin(phase[p]) * Mathf.Exp(-t / BellLives[p]);
            }

            // What strikes it: a band of the highest noise in the set, over before the note has started.
            float below = OnePole(ref strikeBelow, white, Coefficient(1800f));
            float above = OnePole(ref strikeAbove, white, Coefficient(6000f));
            float strike = (above - below) * Mathf.Exp(-t / 0.0016f);

            samples[i] = Attack(t) * (ring + 0.3f * strike);
        }

        Normalise(samples, BellPeak);
        FadeOut(samples, BellFadeSeconds);

        return Make("Typewriter Bell", samples);
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

    /// <summary>Ramps in from silence, so a sound starts with the strike rather than with a step.</summary>
    private static float Attack(float t)
    {
        return t >= AttackSeconds ? 1f : t / AttackSeconds;
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
    private static void FadeOut(float[] samples, float seconds)
    {
        int fade = Mathf.Clamp(Mathf.RoundToInt(seconds * SampleRate), 1, samples.Length);

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
