// Mirrors UiSquishClip.BuildMove / BuildConfirm (Node has no System.Random, so the noise is the same shape
// but a different sequence) and reports what the two menu sounds actually contain: how loud they are, how they
// decay, whether the one-shot ends on silence, and whether the confirm's splatter is really there.
const SR = 44100;

const exp = Math.exp;
const sin = Math.sin;
const pow = Math.pow;
const lerp = (a, b, t) => a + (b - a) * Math.max(0, Math.min(1, t));

function makeRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state ^= state << 13; state >>>= 0;
    state ^= state >>> 17;
    state ^= state << 5; state >>>= 0;
    return state / 4294967296;
  };
}

const noise = (r) => r() * 2 - 1;
const onePole = (state, input, coefficient) => state.s + (input - state.s) * coefficient;
const coefficient = (cutoff) => 1 - exp((-2 * Math.PI * cutoff) / SR);
const attack = (t, seconds) => (t >= seconds ? 1 : t / seconds);

function normalise(samples, peak) {
  let loudest = 0;
  for (const s of samples) loudest = Math.max(loudest, Math.abs(s));
  const scale = loudest > 1e-4 ? peak / loudest : 1;
  for (let i = 0; i < samples.length; i++) samples[i] *= scale;
}

function fadeOut(samples, seconds) {
  const fade = Math.max(1, Math.min(Math.round(seconds * SR), samples.length));
  for (let i = 0; i < fade; i++) samples[samples.length - fade + i] *= 1 - i / fade;
}

function buildMove() {
  const seconds = 0.17, r = makeRandom(20260926);
  const length = Math.round(seconds * SR);
  const samples = new Float64Array(length);
  let body = 0, squeakPhase = 0, thumpPhase = 0;

  for (let i = 0; i < length; i++) {
    const t = i / SR;
    const through = t / seconds;
    const corner = lerp(2400, 420, through);
    body = onePole({ s: body }, noise(r), coefficient(corner));
    squeakPhase += (2 * Math.PI * lerp(540, 210, through)) / SR;
    const squeak = sin(squeakPhase) * (1 + 0.35 * sin(2 * Math.PI * 55 * t));
    thumpPhase += (2 * Math.PI * lerp(150, 92, through)) / SR;
    const thump = sin(thumpPhase) * exp(-t / 0.018);
    const envelope = attack(t, 0.003) * exp(-t / 0.032);
    samples[i] = envelope * (body + 0.5 * squeak + 0.6 * thump);
  }

  normalise(samples, 0.55);
  fadeOut(samples, 0.008);
  return samples;
}

function buildConfirm() {
  const seconds = 0.55, r = makeRandom(20260927);
  const length = Math.round(seconds * SR);
  const samples = new Float64Array(length);
  let body = 0, tail = 0, squeakPhase = 0, thumpPhase = 0;

  for (let i = 0; i < length; i++) {
    const t = i / SR;
    const through = t / seconds;
    const white = noise(r);
    const corner = lerp(1600, 240, Math.min(1, through * 1.6));
    body = onePole({ s: body }, white, coefficient(corner));
    let wet = onePole({ s: tail }, white, coefficient(680));
    tail = wet;
    wet *= 1 + 0.45 * sin(2 * Math.PI * 42 * t);
    squeakPhase += (2 * Math.PI * lerp(430, 120, through)) / SR;
    const squeak = sin(squeakPhase) * (1 + 0.5 * sin(2 * Math.PI * 38 * t));
    thumpPhase += (2 * Math.PI * lerp(135, 55, through)) / SR;
    const thump = sin(thumpPhase) * exp(-t / 0.05);
    const envelope = attack(t, 0.004) * exp(-t / 0.07);
    samples[i] = envelope * (body + 0.55 * wet + 0.35 * squeak + 0.8 * thump);
  }

  // the splatter
  for (let g = 0; g < 26; g++) {
    const at = seconds * 0.32 * pow(r(), 1.7);
    const start = Math.round(at * SR);
    const frequency = 700 + r() * 2800;
    const decay = SR * (0.0015 + r() * 0.004);
    const level = 0.25 + r() * 0.75;
    const reach = Math.max(8, Math.round(decay * 4));
    for (let j = 0; j < reach; j++) {
      const index = start + j;
      if (index >= samples.length) break;
      samples[index] += level * exp(-j / decay) * sin((2 * Math.PI * frequency * j) / SR);
    }
  }

  normalise(samples, 0.8);
  fadeOut(samples, 0.008);
  return samples;
}

function report(name, samples) {
  let peak = 0, sum = 0, nan = 0;
  for (const s of samples) {
    if (!Number.isFinite(s)) nan++;
    peak = Math.max(peak, Math.abs(s));
    sum += s * s;
  }
  const rms = Math.sqrt(sum / samples.length);
  const seconds = samples.length / SR;

  // rms per 20 ms window, as a bar, so the shape of the envelope is visible
  const window = Math.round(0.02 * SR);
  const windows = [];
  for (let start = 0; start < samples.length; start += window) {
    let s = 0, n = 0;
    for (let i = start; i < Math.min(samples.length, start + window); i++) { s += samples[i] * samples[i]; n++; }
    windows.push(Math.sqrt(s / n));
  }
  const bars = windows.map((v) => "#".repeat(Math.round((v / peak) * 40)).padEnd(40)).join("\n    ");

  // zero crossings in the first 30 ms against 150..300 ms: brighter content later = splatter rather than a tail
  const crossings = (from, to) => {
    let count = 0;
    const a = Math.round(from * SR), b = Math.min(samples.length, Math.round(to * SR));
    for (let i = a + 1; i < b; i++) if (Math.sign(samples[i]) !== Math.sign(samples[i - 1])) count++;
    return count / ((b - a) / SR);
  };

  console.log(`\n${name}: ${seconds.toFixed(3)}s, ${samples.length} samples, peak ${peak.toFixed(3)}, rms ${rms.toFixed(4)}, nan ${nan}`);
  console.log(`  shape (20 ms windows, peak = 40):\n    ${bars}`);
  console.log(`  last sample ${samples[samples.length - 1].toExponential(2)}, 1 ms from the end ${Math.abs(samples[Math.round(samples.length - SR / 1000)]).toFixed(4)}`);
  console.log(`  zero crossings/s: ${crossings(0, 0.03).toFixed(0)} in the first 30 ms, ${crossings(0.15, 0.3).toFixed(0)} at 150..300 ms`);

  const problems = [];
  if (nan) problems.push(`${nan} non-finite samples`);
  if (peak > 1) problems.push(`peak ${peak.toFixed(3)} clips`);
  if (Math.abs(samples[samples.length - 1]) > 0.001) problems.push("the clip does not end on silence");
  if (rms < 0.01) problems.push("the clip is near-silent");
  if (windows[0] < peak * 0.1) problems.push("the clip takes too long to get going");
  if (windows[Math.floor(windows.length / 2)] > peak * 0.5) problems.push("the clip does not decay");
  return problems;
}

const problems = [...report("move", buildMove()), ...report("confirm", buildConfirm())];
console.log(problems.length ? "\nPROBLEMS:\n  " + problems.join("\n  ") : "\nBoth clips are in range: no clipping, no silence, decaying, ending on silence.");
