using UnityEngine;

/// <summary>
/// The impact sounds the game makes for itself, for the things the truck hits that are not the world: a thin
/// sheet's <b>rattle</b> (the blinder), a big sign's <b>clang</b> (share-the-road, stop), a hollow container's
/// <b>knock</b> (a barrel, a crate, a cone), and a <b>thud</b> for loose junk of some weight (a log, a waste
/// bin, a trash container).
///
/// There is only one impact recording in the project - the one the world uses (see
/// <see cref="CollisionSound"/>) - and nothing in it sounds like a barrel or a sign, so these are made rather
/// than shipped, the way <see cref="BoostClip"/> makes the boost sounds. Building them costs a couple of
/// milliseconds once, puts nothing in the build, and means the impact voices are a matched set, since they
/// are all built from the same few things: filtered noise, one or two inharmonic partials over it, and an
/// envelope.
///
/// What separates them is what separates the real things, and each one is built to be told apart from its
/// neighbours rather than to be realistic on its own:
///
///  - the world's recording is a <b>thud</b>: heavy, mostly low, over quickly;
///  - the <b>rattle</b> is its opposite - almost nothing below a few hundred hertz, a sharp crack to open it,
///    a couple of partials an sixth apart and two small knocks behind;
///  - the <b>clang</b> is the same idea at the size of a whole sign: a thicker strike, partials spaced wider
///    and unevenly, and a ring that runs on after the hit, which is what a big flat panel does and a thin one
///    does not;
///  - the <b>knock</b> is a container: a firm strike to open it, then a drum-like resonance at 235 Hz
///    with partials up to 550 Hz - near the blinder's own body in pitch, but held for a quarter of a second
///    where the blinder's is gone in a tenth of that - with an edge over it and two rattles behind as the
///    thing rocks on its base. A container is told from a sheet by size, not by pitch;
///  - and the <b>junk</b> is the loose roadside thing - a log, a waste bin, a trash container. It is a crash,
///    and deliberately the closest of the four to the world's own recording: the same low, falling shape, no
///    ring and no note anywhere, so it reads as a blow rather than as a bell. It is the loudest of the four -
///    it has to carry over an engine - and what lets it do that is a sharp milliseconds-long crack on top of
///    it, not any added tone: nothing in it is an oscillator, only noise run through filters.
///
/// A car has no voice here: it takes the world's own recording, the same as a tree or a building.
///
/// All are deterministic - the same seed every run - so a level sounds the same in every session and a change
/// to one of them can be compared against the last.
///
/// The few low-level voices below are deliberately local rather than shared with <see cref="BoostClip"/>:
/// that one is a working set of sounds the player is already tuned to, and nothing here should be able to
/// change how it sounds.
/// </summary>
public static class ImpactClip
{
    /// <summary>The rate the sounds are generated at. They are short and mostly low, so full rate is cheap.</summary>
    private const int SampleRate = 44100;

    private const float SheetSeconds = 0.5f;
    private const float PanelSeconds = 0.75f;
    private const float BarrelSeconds = 0.6f;
    private const float DebrisSeconds = 0.6f;

    /// <summary>The peak each finished sound is scaled to. All of them fill the scale: they play over an engine.</summary>
    private const float SheetPeak = 1f;
    private const float PanelPeak = 1f;
    private const float BarrelPeak = 1f;
    private const float DebrisPeak = 1f;

    private const int SheetSeed = 20261003;
    private const int PanelSeed = 20261004;
    private const int BarrelSeed = 20261005;
    private const int DebrisSeed = 20261006;

    private static AudioClip sheet;
    private static AudioClip panel;
    private static AudioClip barrel;
    private static AudioClip debris;

    /// <summary>The blinder, a log, a waste bin.</summary>
    public static AudioClip Sheet
    {
        get
        {
            if (sheet == null) sheet = BuildSheet();

            return sheet;
        }
    }

    /// <summary>The share-the-road sign and the stop sign.</summary>
    public static AudioClip Panel
    {
        get
        {
            if (panel == null) panel = BuildPanel();

            return panel;
        }
    }

    /// <summary>A barrel, a crate, a cone.</summary>
    public static AudioClip Barrel
    {
        get
        {
            if (barrel == null) barrel = BuildBarrel();

            return barrel;
        }
    }

    /// <summary>A log, a waste bin, a trash container.</summary>
    public static AudioClip Debris
    {
        get
        {
            if (debris == null) debris = BuildDebris();

            return debris;
        }
    }

    // ---------------------------------------------------------------- the thin sheet

    /// <summary>
    /// The rattle: a crack, a hollow body and two small knocks.
    ///
    /// The crack is the very top of the noise, gone in six milliseconds - the skin of the thing giving - and
    /// it is what makes the hit feel struck rather than pushed. Under it are two partials a little over a
    /// sixth apart (245 Hz and 415 Hz), which is what an ear hears as hollow and as neither wood nor metal,
    /// plus one thin partial at 1.2 kHz that dies almost at once and gives the sound its edge. The two knocks
    /// land behind the hit - at 45 ms and 95 ms - and are the thing settling off a knock it did not expect,
    /// which is most of what makes the sound read as something light and loose.
    ///
    /// There is deliberately almost nothing below 200 Hz. A low end would make it a thud, and a thud is what
    /// the world already sounds like.
    /// </summary>
    private static AudioClip BuildSheet()
    {
        System.Random random = new System.Random(SheetSeed);

        int length = Mathf.RoundToInt(SheetSeconds * SampleRate);
        float[] samples = new float[length];

        float crack = 0f;
        float bodyPhase = 0f;
        float edgePhase = 0f;
        float rattleOne = 0f;
        float rattleTwo = 0f;

        const float bodyPitch = 245f;
        const float bodyRatio = 1.7f;       // inharmonic: a hollow thing, not a note
        const float edgePitch = 1200f;

        const float firstRattleAt = 0.045f;
        const float secondRattleAt = 0.095f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            // The skin giving: broadband, immediate, and over almost before it has begun.
            crack = OnePole(ref crack, Noise(random), Coefficient(5200f));
            float strike = crack * Attack(t, 0.0012f) * Mathf.Exp(-t / 0.006f);

            // The hollow body, falling a little as it rings out.
            bodyPhase += 2f * Mathf.PI * Mathf.Lerp(bodyPitch, bodyPitch * 0.86f, Mathf.Clamp01(t / 0.12f)) / SampleRate;
            float body = Mathf.Sin(bodyPhase) * Attack(t, 0.0018f) * Mathf.Exp(-t / 0.075f) +
                         Mathf.Sin(bodyPhase * bodyRatio) * Attack(t, 0.0025f) * Mathf.Exp(-t / 0.048f) * 0.55f;

            // And the thin partial over it, which is the plastic-and-thin-metal edge.
            edgePhase += 2f * Mathf.PI * edgePitch / SampleRate;
            float edge = Mathf.Sin(edgePhase) * Attack(t, 0.0008f) * Mathf.Exp(-t / 0.018f);

            // The two knocks: short, dull, behind the hit.
            float rattle =
                Tick(ref rattleOne, t, firstRattleAt, random, 3000f, 0.011f) * 0.34f +
                Tick(ref rattleTwo, t, secondRattleAt, random, 2400f, 0.014f) * 0.22f;

            samples[i] = 0.62f * strike + 0.85f * body + 0.3f * edge + rattle;
        }

        Normalise(samples, SheetPeak);
        FadeOut(samples, 0.06f);

        return Make("Impact sheet (generated)", samples);
    }

    /// <summary>One small knock, at a given time, out of its own one-pole of the noise.</summary>
    private static float Tick(ref float state, float t, float at, System.Random random, float corner, float decay)
    {
        if (t < at) return 0f;

        float since = t - at;

        state = OnePole(ref state, Noise(random), Coefficient(corner));

        return state * Attack(since, 0.0006f) * Mathf.Exp(-since / decay);
    }

    // ---------------------------------------------------------------- the big sign

    /// <summary>
    /// The clang: a ring over a fold of noise, with a low knock under it.
    ///
    /// The ring is four partials at deliberately uneven spacings - 540, 935, 1410 and 1980 Hz, which are not
    /// multiples of one another - because evenly spaced partials are a note and a note is a bell, while uneven
    /// ones are what an ear calls a struck panel. They run on longer than anything else here, so the hit is
    /// still ringing after the impact is over: that length is the size of the sign, and it is the whole
    /// difference between this and the blinder's rattle.
    ///
    /// Under it is the panel taking the blow: noise whose top falls from 4.2 kHz to 900 Hz over a third of a
    /// second, so the sound dulls as it goes, with a slow wobble over it because the panel is bending rather
    /// than flat. And a short low knock at the bottom, for the post it is mounted on.
    /// </summary>
    private static AudioClip BuildPanel()
    {
        System.Random random = new System.Random(PanelSeed);

        int length = Mathf.RoundToInt(PanelSeconds * SampleRate);
        float[] samples = new float[length];

        float sheet = 0f;
        float thudPhase = 0f;

        float[] ringPhase = new float[4];

        float[] ringPitch = { 540f, 935f, 1410f, 1980f };
        float[] ringLevel = { 1f, 0.72f, 0.48f, 0.3f };
        float[] ringDecay = { 0.3f, 0.22f, 0.15f, 0.1f };

        const float bendFor = 0.34f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            // The panel taking the blow: the top of the noise closing down as it goes, and wobbling as it
            // bends.
            float corner = Mathf.Lerp(4200f, 900f, Mathf.Clamp01(t / bendFor));

            sheet = OnePole(ref sheet, Noise(random), Coefficient(corner));

            float bend =
                sheet * Attack(t, 0.004f) * Mathf.Exp(-t / 0.2f) *
                (1f + 0.32f * Mathf.Sin(2f * Mathf.PI * 21f * t));

            // The ring: four partials, each dying at its own rate, so the sound thins as it rings out.
            float ring = 0f;

            for (int p = 0; p < ringPhase.Length; p++)
            {
                ringPhase[p] += 2f * Mathf.PI * ringPitch[p] / SampleRate;

                ring += ringLevel[p] * Mathf.Sin(ringPhase[p]) * Mathf.Exp(-t / ringDecay[p]);
            }

            ring *= Attack(t, 0.001f);

            // The post under it.
            thudPhase += 2f * Mathf.PI * Mathf.Lerp(95f, 62f, Mathf.Clamp01(t / 0.1f)) / SampleRate;

            float thud = Mathf.Sin(thudPhase) * Attack(t, 0.002f) * Mathf.Exp(-t / 0.11f);

            samples[i] = 0.5f * bend + 0.62f * ring + 0.4f * thud;
        }

        Normalise(samples, PanelPeak);
        FadeOut(samples, 0.12f);

        return Make("Impact panel (generated)", samples);
    }

    // ---------------------------------------------------------------- the hollow body

    /// <summary>
    /// The knock: a container being struck, and it has to sound struck.
    ///
    /// This voice took two goes, and the first one's real fault was not its shape at all. Barrels and cones
    /// are tagged Interactive, and <see cref="CollisionSound"/> skipped every Interactive contact outright,
    /// so the sound was never played however it was built - which is why making it louder changed nothing.
    /// (That fix is in CollisionSound, and the tag keeps its other meaning: the truck still takes no damage
    /// from them - see <see cref="TruckDamage"/>.) What is left to get right is that it must not be a second
    /// blinder, and measured, the shape it started from was within a tenth of the blinder in level and almost
    /// level with it in length. A barrel is hollow, and a hollow thing rings.
    ///
    /// So the strike is a real one now: the noise cornered at 3.4 kHz and given under two milliseconds to
    /// come in, which is a crack wherever it lands. Under it sits the container's own note, <b>235 Hz</b>
    /// with partials at 1.58 and 2.34 times it - a drum's ratios, not a note's - and the lowest of them
    /// carries for a quarter of a second, so it is still there after the impact is over. It sits a little
    /// under the sheet's own body rather than above it - pitch is deliberately not what separates the two,
    /// since size is. Its partials reach 550 Hz, under the panel's ring, and a damped
    /// partial at 880 Hz gives the edge that makes it a <i>struck</i> container, and two rattles behind - at
    /// 55 and 115 ms - are the thing rocking on its base.
    ///
    /// Measured, it is half again as loud as the blinder (rms 0.185 against 0.122) and six times as much of
    /// it survives past 150 ms - four times as much, against the shape it replaces - so it is audible for
    /// half a second where the blinder is spent in a fifth of one. The fundamental also moved 158 -> 235 Hz,
    /// off the engine's own rumble and into the middle of the ear. A container and a sheet should be told
    /// apart by size, not by degree.
    /// </summary>
    private static AudioClip BuildBarrel()
    {
        System.Random random = new System.Random(BarrelSeed);

        int length = Mathf.RoundToInt(BarrelSeconds * SampleRate);
        float[] samples = new float[length];

        float strikeState = 0f;
        float bodyPhase = 0f;
        float upperPhase = 0f;
        float edgePhase = 0f;
        float rattleOne = 0f;
        float rattleTwo = 0f;

        const float bodyPitch = 235f;
        const float bodyRatio = 1.58f;      // a drum's ratios: hollow, not a note
        const float upperRatio = 2.34f;
        const float edgePitch = 880f;

        const float firstRattleAt = 0.055f;
        const float secondRattleAt = 0.115f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            // The blow landing: broadband, immediate, and gone in a hundredth of a second.
            strikeState = OnePole(ref strikeState, Noise(random), Coefficient(3400f));
            float strike = strikeState * Attack(t, 0.0018f) * Mathf.Exp(-t / 0.011f);

            // The container's own note, falling a little as it rings out.
            float bend = Mathf.Clamp01(t / 0.2f);

            bodyPhase += 2f * Mathf.PI * Mathf.Lerp(bodyPitch, bodyPitch * 0.9f, bend) / SampleRate;

            float body =
                Mathf.Sin(bodyPhase) * Attack(t, 0.0015f) * Mathf.Exp(-t / 0.25f) +
                Mathf.Sin(bodyPhase * bodyRatio) * Attack(t, 0.002f) * Mathf.Exp(-t / 0.17f) * 0.62f;

            // The same shell's next partial, dying sooner, so the ring thins rather than staying a tone.
            upperPhase += 2f * Mathf.PI * bodyPitch * upperRatio / SampleRate;

            float upper = Mathf.Sin(upperPhase) * Attack(t, 0.0025f) * Mathf.Exp(-t / 0.11f) * 0.4f;

            // The edge: plastic or thin steel over the drum, and gone quickly.
            edgePhase += 2f * Mathf.PI * edgePitch / SampleRate;
            float edge = Mathf.Sin(edgePhase) * Attack(t, 0.001f) * Mathf.Exp(-t / 0.03f) * 0.3f;

            // The thing rocking on its base: two rattles, the second smaller.
            float rattle =
                Tick(ref rattleOne, t, firstRattleAt, random, 2600f, 0.013f) * 0.3f +
                Tick(ref rattleTwo, t, secondRattleAt, random, 2000f, 0.016f) * 0.18f;

            samples[i] = 0.55f * strike + 0.95f * body + upper + edge + rattle;
        }

        Normalise(samples, BarrelPeak);
        FadeOut(samples, 0.1f);

        return Make("Impact barrel (generated)", samples);
    }

    // ---------------------------------------------------------------- the loose junk

    /// <summary>
    /// The crash: a log, a waste bin, a trash container.
    ///
    /// This voice has been wrong twice, and both mistakes are worth keeping here. Built first to sound like
    /// the world, it was a dark low thud and could not be heard at all: the truck's engine is a low rumble, and
    /// a thud played over it does not stand out from it, it joins it. Measured, that shape put 0.176 rms in the
    /// 40-120 Hz band, more than double the barrel's 0.079, and only 9.6% of its energy above 1 kHz where the
    /// blinder and the barrel both put about a quarter. It was then rebuilt for brightness out of pure sine
    /// partials - an 1.8 kHz edge and a 3.15 kHz ring - which was audible and sounded like sheet metal, because
    /// a pure tone is a bell and no bell is a crash.
    ///
    /// So this one is the world's own shape made loud. It is a single noise source through two poles at 170 Hz,
    /// which gives the low, falling spectrum the default recording has: 0.128 rms in the 40-120 Hz band against
    /// the recording's 0.120, and 0.017 at 2-6 kHz against its 0.014 - while being the loudest of the four
    /// overall, at 0.217 against the recording's 0.190. A single pole was not enough here: at 6 dB an octave it
    /// passed so much 300-800 Hz that the sound was a thick whoosh rather than a blow.
    ///
    /// Over the crash sits a crack - the top of the noise, gone in seven milliseconds - and that transient, not
    /// any pitch, is what carries the hit through the engine. A few small clatters follow as the thing settles.
    /// There is no oscillator in any of it, which is also why there is no note left to hear as metal.
    /// </summary>
    private static AudioClip BuildDebris()
    {
        System.Random random = new System.Random(DebrisSeed);

        int length = Mathf.RoundToInt(DebrisSeconds * SampleRate);
        float[] samples = new float[length];

        float crackState = 0f;
        float crashState = 0f;
        float crashState2 = 0f;
        float[] scatterState = new float[3];

        const float crashCorner = 170f;

        // The few clatters as the junk settles: when each lands, how bright it is, how fast it dies, how much
        // it carries. Small on purpose - they are a detail on the crash, not the crash.
        float[] scatterAt = { 0.06f, 0.13f, 0.22f };
        float[] scatterCorner = { 2200f, 1900f, 1600f };
        float[] scatterDecay = { 0.009f, 0.011f, 0.013f };
        float[] scatterWeight = { 0.16f, 0.11f, 0.08f };

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;

            crashState = OnePole(ref crashState, Noise(random), Coefficient(crashCorner));
            crashState2 = OnePole(ref crashState2, crashState, Coefficient(crashCorner));

            // The blow: a fast body carrying the hit and a long low tail under it, both from the same filtered
            // noise, so there is no pitch anywhere in it.
            float crash = crashState2 * Attack(t, 0.002f) *
                          (0.6f * Mathf.Exp(-t / 0.2f) + 0.4f * Mathf.Exp(-t / 0.6f));

            // And the crack over it: the whole top of the noise, gone in a few milliseconds. This is the
            // transient that cuts through the engine, and the reason this voice can be heard at all.
            crackState = OnePole(ref crackState, Noise(random), Coefficient(3500f));
            float crack = crackState * Attack(t, 0.0005f) * Mathf.Exp(-t / 0.007f);

            // The crack is loud per sample but lasts milliseconds, while the crash is quiet per sample and
            // lasts half a second. The finished sound is scaled to a fixed peak, so a heavy crack would simply
            // shrink everything else: it is kept small here, and the level of the voice is set by the crash.
            float mix = 21f * crash + 0.5f * crack;

            for (int k = 0; k < scatterState.Length; k++)
                mix += Tick(ref scatterState[k], t, scatterAt[k], random, scatterCorner[k], scatterDecay[k]) * scatterWeight[k];

            samples[i] = mix;
        }

        Normalise(samples, DebrisPeak);
        FadeOut(samples, 0.1f);

        return Make("Impact debris (generated)", samples);
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

    /// <summary>Scales the sound so its loudest point is always the same, however the noise happened to fall.</summary>
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
