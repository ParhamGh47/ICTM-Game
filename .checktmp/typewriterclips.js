// A mirror of TypewriterClip, so the sounds can be measured instead of guessed at: peak against the target,
// the tail against a click, whether the carriage is actually in the return, whether the bell really rings
// past the strike, and how different the four voices are from one another.
const SR = 44100;

const cos = Math.cos, exp = Math.exp, sin = Math.sin, PI = Math.PI, abs = Math.abs, sqrt = Math.sqrt;
const round = Math.round, min = Math.min, max = Math.max;

function mulberry(seed) {
  return function () {
    seed |= 0; seed = (seed + 0x6D2B79F5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
// .NET's System.Random (the project uses it) is a subtractive generator; for the checks here any fixed
// sequence will do, so the mirror uses its own but the same seeding discipline: one stream per sound.
function rng(seed) { const r = mulberry(seed); return () => r() * 2 - 1; }

const STRIKE_DECAY = 0.0021, RUSTLE_CENTRE = 900, RUSTLE_DECAY = 0.02, ATTACK_SECONDS = 0.0006;
const CARRIAGE_LOW = 700, CARRIAGE_HIGH = 3200;
const BAR_RATIOS = [1, 1.62, 2.41], BAR_LEVELS = [1, 0.55, 0.3], BAR_LIVES = [1, 0.7, 0.45];
const BELL_RATIOS = [1, 2.76, 5.4], BELL_LEVELS = [1, 0.45, 0.2], BELL_LIVES = [0.36, 0.2, 0.09];
const BELL_NOTE = 2100, BELL_SECONDS = 1, BELL_PEAK = 0.5, BELL_FADE = 0.09;
const STRIKE_FADE = 0.004;

const coeff = (cutoff) => 1 - exp(-2 * PI * cutoff / SR);
const attack = (t) => (t >= ATTACK_SECONDS ? 1 : t / ATTACK_SECONDS);

const VOICE = [
  { click: 2700, thock: 158, ring: 3300, life: 0.0135, metal: 0.95, seed: 20260921 },
  { click: 2350, thock: 186, ring: 2950, life: 0.0155, metal: 0.8, seed: 20260922 },
  { click: 3100, thock: 142, ring: 3650, life: 0.0125, metal: 1.1, seed: 20260923 },
  { click: 2150, thock: 172, ring: 2750, life: 0.0165, metal: 0.7, seed: 20260924 },
];

function build(s, carriage) {
  const noise = rng(s.seed);
  const length = round(s.seconds * SR);
  const out = new Float64Array(length);
  let lowBody = 0, highBody = 0, thockPhase = 0, thockTop = 0, rustleBody = 0;
  let carBody = 0, carFloor = 0, carPhase = 0;
  const barPhase = [0, 0, 0];
  const carStart = s.seconds * 0.24;
  const carRun = max(0.001, s.seconds - carStart);

  for (let i = 0; i < length; i++) {
    const t = i / SR;
    const w = noise();

    lowBody += (w - lowBody) * coeff(s.click * 0.6);
    highBody += (w - highBody) * coeff(s.click * 2.8);
    const strike = (highBody - lowBody) * exp(-t / STRIKE_DECAY);

    thockPhase += 2 * PI * s.thock / SR;
    thockTop += 2 * PI * (s.thock * 2.37) / SR;
    const platen = sin(thockPhase) * exp(-t / (s.life * 1.6)) + 0.45 * sin(thockTop) * exp(-t / (s.life * 0.5));

    let metal = 0;
    for (let p = 0; p < 3; p++) {
      barPhase[p] += 2 * PI * s.ring * BAR_RATIOS[p] / SR;
      metal += BAR_LEVELS[p] * sin(barPhase[p]) * exp(-t / (s.life * BAR_LIVES[p]));
    }

    rustleBody += (w - rustleBody) * coeff(RUSTLE_CENTRE);
    const rustle = rustleBody * exp(-t / RUSTLE_DECAY);

    let run = 0;
    if (carriage) {
      const since = t - carStart;
      if (since > 0) {
        const progress = min(1, max(0, since / carRun));
        const speed = sin(progress * PI);
        const centre = CARRIAGE_LOW + (CARRIAGE_HIGH - CARRIAGE_LOW) * progress;
        carBody += (w - carBody) * coeff(centre);
        carFloor += (w - carFloor) * coeff(centre * 0.35);
        carPhase += 2 * PI * (430 * (1 + 0.05 * sin(2 * PI * 21 * t))) / SR;
        run = speed * (0.5 * (carBody - carFloor) + 0.22 * sin(carPhase));
      }
    }

    out[i] = attack(t) * (1.5 * strike + 1.05 * platen + s.metal * 0.22 * metal + 0.05 * rustle + run);
  }

  const peak = max(...Array.from(out, abs));
  const scale = s.peak / peak;
  for (let i = 0; i < length; i++) out[i] *= scale;

  const fade = min(length, round(STRIKE_FADE * SR));
  for (let i = 0; i < fade; i++) out[length - fade + i] *= 1 - i / fade;

  return out;
}

function buildBell() {
  const noise = rng(20260928);
  const length = round(BELL_SECONDS * SR);
  const out = new Float64Array(length);
  const phase = [0, 0, 0];
  let below = 0, above = 0;

  for (let i = 0; i < length; i++) {
    const t = i / SR;
    const w = noise();
    let ring = 0;
    for (let p = 0; p < 3; p++) {
      phase[p] += 2 * PI * BELL_NOTE * BELL_RATIOS[p] / SR;
      ring += BELL_LEVELS[p] * sin(phase[p]) * exp(-t / BELL_LIVES[p]);
    }
    below += (w - below) * coeff(1800);
    above += (w - above) * coeff(6000);
    out[i] = attack(t) * (ring + 0.3 * (above - below) * exp(-t / 0.0016));
  }

  const peak = max(...Array.from(out, abs));
  const scale = BELL_PEAK / peak;
  for (let i = 0; i < length; i++) out[i] *= scale;

  const fade = min(length, round(BELL_FADE * SR));
  for (let i = 0; i < fade; i++) out[length - fade + i] *= 1 - i / fade;

  return out;
}

const rms = (a) => sqrt(a.reduce((s, v) => s + v * v, 0) / a.length);
const band = (a, from, to) => a.slice(round(from * SR), round(to * SR));
const zeroCrossings = (a) => {
  let n = 0;
  for (let i = 1; i < a.length; i++) if ((a[i - 1] < 0) !== (a[i] < 0)) n++;
  return n / (a.length / SR);
};
const finite = (a) => a.every(Number.isFinite);

function report(name, a, target) {
  const peak = max(...Array.from(a, abs));
  console.log(
    name.padEnd(22),
    'n=' + String(a.length).padStart(6),
    'peak=' + peak.toFixed(4),
    'target=' + target.toFixed(2),
    'rms=' + rms(a).toFixed(4),
    'end=' + abs(a[a.length - 1]).toExponential(1),
    'zc=' + zeroCrossings(a).toFixed(0) + 'Hz',
    finite(a) ? 'finite' : 'NaN!'
  );
  return peak;
}

const bars = { name: 'bar', seconds: 0.085, peak: 0.75, seed: 20260926, click: 1700, thock: 120, ring: 2300, life: 0.019, metal: 1 };
const ret = { name: 'return', seconds: 0.21, peak: 0.7, seed: 20260927, click: 1750, thock: 118, ring: 2400, life: 0.017, metal: 0.9 };

console.log('--- keys ---');
const keys = VOICE.map((v) => build(Object.assign({ seconds: 0.06, peak: 0.6 }, v), false));
keys.forEach((k, i) => report('key ' + (i + 1), k, 0.6));

console.log('--- bar and return ---');
const barPcm = build(Object.assign({}, bars), false);
const retPcm = build(Object.assign({}, ret), true);
report('bar', barPcm, 0.75);
report('return', retPcm, 0.7);

console.log('--- bell ---');
const bellPcm = buildBell();
report('bell', bellPcm, BELL_PEAK);

console.log('\n--- how they differ ---');
console.log('voice zero-crossing rates (Hz):', keys.map((k) => zeroCrossings(k).toFixed(0)).join(' / '));
let closest = Infinity, pair = '';
for (let i = 0; i < keys.length; i++)
  for (let j = i + 1; j < keys.length; j++) {
    // how alike two voices are: correlation of their first 20 ms
    const a = band(keys[i], 0, 0.02), b = band(keys[j], 0, 0.02);
    let dot = 0, na = 0, nb = 0;
    for (let n = 0; n < a.length; n++) { dot += a[n] * b[n]; na += a[n] * a[n]; nb += b[n] * b[n]; }
    const corr = dot / sqrt(na * nb);
    if (corr < closest) { closest = corr; pair = (i + 1) + '/' + (j + 1); }
  }
console.log('most alike pair of voices: ' + pair + ', correlation ' + closest.toFixed(3) + ' (lower is more different)');

const head = rms(band(retPcm, 0, 0.045));
const tail = rms(band(retPcm, 0.06, 0.21));
console.log('return: strike head rms ' + head.toFixed(4) + ', carriage-tail rms ' + tail.toFixed(4) + ' (ratio ' + (tail / head).toFixed(2) + ')');

const bellStrike = rms(band(bellPcm, 0, 0.02));
const bellRing = rms(band(bellPcm, 0.25, 0.4));
const bellLate = rms(band(bellPcm, 0.5, 0.8));
console.log('bell: strike rms ' + bellStrike.toFixed(4) + ', ring at 0.25-0.4s ' + bellRing.toFixed(4) + ', still ringing at 0.5-0.8s ' + bellLate.toFixed(4));
