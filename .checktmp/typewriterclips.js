// A mirror of TypewriterClip.Build in C#, read back from the source so the numbers below are the ones the
// game will actually generate: the parameters come out of the file, and the two functions below are TMP-free
// copies of the same arithmetic. It reports what can be checked without ears - the peak, that the sound dies
// away, how bright it is, and that it ends on silence.
const fs = require('fs');
const source = fs.readFileSync('Assets/Scripts/Global/TypewriterClip.cs', 'utf8');

const num = (name) => Number(source.match(new RegExp(name + ' = (-?[0-9.]+)f?'))[1]);

const SampleRate = num('SampleRate');
const KeySeconds = num('KeySeconds');
const BarSeconds = num('BarSeconds');
const KeyPeak = num('KeyPeak');
const BarPeak = num('BarPeak');
const FadeOutSeconds = num('FadeOutSeconds');

// The two calls in the file, so the voices are the authored ones.
const readCall = (fn) => {
  const block = source.slice(source.indexOf('private static AudioClip Build' + fn + '()'));
  const m = block.match(/Build\("([^"]+)", \w+, \w+, \w+,\s*click: ([0-9.]+)f, clack: ([0-9.]+)f, ring: ([0-9.]+)f, life: ([0-9.]+)f, depth: ([0-9.]+)f\)/);
  return { name: m[1], click: Number(m[2]), clack: Number(m[3]), ring: Number(m[4]), life: Number(m[5]), depth: Number(m[6]) };
};

function rand(seed) {           // System.Random's first draws are not what matters; any uniform noise will do
  let state = seed >>> 0;
  return () => { state = (state * 1664525 + 1013904223) >>> 0; return (state / 4294967296) * 2 - 1; };
}
const coefficient = (cutoff) => 1 - Math.exp(-2 * Math.PI * cutoff / SampleRate);

function build({ name, click, clack, ring, depth }, seconds, peak, seed) {
  const random = rand(seed);
  const length = Math.round(seconds * SampleRate);
  const samples = new Float64Array(length);

  let lowBody = 0, highBody = 0, clackPhase = 0, ringPhase = 0;
  const clickDecay = SampleRate * 0.0016;
  const clackDecay = SampleRate * 0.0075 * depth;
  const ringDecay = SampleRate * 0.009 * depth;

  for (let i = 0; i < length; i++) {
    const t = i / SampleRate;
    const white = random();

    lowBody += (white - lowBody) * coefficient(click * 0.7);
    highBody += (white - highBody) * coefficient(click * 2.6);
    const band = highBody - lowBody;

    clackPhase += 2 * Math.PI * clack / SampleRate;
    const body = Math.sin(clackPhase) * Math.exp(-i / clackDecay);

    ringPhase += 2 * Math.PI * ring / SampleRate;
    const tone = Math.sin(ringPhase) * Math.exp(-i / ringDecay);

    const envelope = t >= 0.0008 ? 1 : t / 0.0008;
    samples[i] = envelope * (1.4 * band * Math.exp(-i / clickDecay) + 0.75 * body + 0.3 * tone);
  }

  let loudest = 0;
  for (const s of samples) loudest = Math.max(loudest, Math.abs(s));
  const scale = peak / loudest;
  for (let i = 0; i < length; i++) samples[i] *= scale;

  const fade = Math.max(1, Math.round(FadeOutSeconds * SampleRate));
  for (let i = 0; i < fade; i++) samples[length - fade + i] *= 1 - i / fade;

  return { name, samples, seconds, peak };
}

const report = (clip) => {
  const n = clip.samples.length;
  let peak = 0, nan = 0;
  for (const s of clip.samples) { if (!Number.isFinite(s)) nan++; peak = Math.max(peak, Math.abs(s)); }

  const rms = (from, to) => {
    let sum = 0, count = 0;
    for (let i = from; i < to; i++) { sum += clip.samples[i] * clip.samples[i]; count++; }
    return Math.sqrt(sum / count);
  };
  const crossings = (from, to) => {
    let c = 0;
    for (let i = from + 1; i < to; i++) if ((clip.samples[i - 1] < 0) !== (clip.samples[i] < 0)) c++;
    return c;
  };

  const head = rms(0, Math.round(0.005 * SampleRate));
  const tail = rms(Math.round((clip.seconds - 0.008) * SampleRate), n);
  const brightHead = crossings(0, Math.round(0.005 * SampleRate)) / 0.005;
  const brightTail = crossings(Math.round(0.02 * SampleRate), Math.round(0.04 * SampleRate)) / 0.02;

  console.log(clip.name + ':  ' + n + ' samples (' + (clip.seconds * 1000).toFixed(0) + ' ms)  peak ' + peak.toFixed(3) +
    '  NaN ' + nan);
  console.log('   loud at the start ' + head.toFixed(3) + ' -> ' + tail.toFixed(4) + ' by the end  (' +
    (20 * Math.log10(tail / head)).toFixed(1) + ' dB down)');
  console.log('   brightness ' + brightHead.toFixed(0) + ' crossings/s at the strike -> ' + brightTail.toFixed(0) + ' later');
  console.log('   last sample ' + clip.samples[n - 1].toFixed(6) + ' (a one-shot has to end on silence)');
};

report(build(readCall('Key'), KeySeconds, KeyPeak, num('KeySeed')));
report(build(readCall('Bar'), BarSeconds, BarPeak, num('BarSeed')));

// The clack rate the story script's two settings produce, in the two modes.
const script = fs.readFileSync('Assets/Scripts/UI/StoryTypeWriter.cs', 'utf8');
const field = (name) => Number(script.match(new RegExp('public float ' + name + ' = ([0-9.]+)f'))[1]);
const cps = field('charactersPerSecond'), mult = field('speedUpMultiplier');
const interval = field('keystrokeInterval'), intervalFast = field('keystrokeIntervalSpeedUp');

console.log('\nclack rate');
for (const [mode, rate, gap] of [['normal', cps, interval], ['speed up', cps * mult, intervalFast]]) {
  const byGap = 1 / gap;
  console.log('   ' + mode.padEnd(9) + ': ' + rate.toFixed(0) + ' characters/s, at most ' + byGap.toFixed(0) +
    ' clacks/s -> ' + Math.min(rate, byGap).toFixed(1) + ' actually heard');
}
