// Does the rebuilt barrel voice actually put its energy where it can be heard?
//
// The complaint was that the barrel/cone hit was not sensible - and the old voice explains why: a soft
// opening, a 158 Hz body and no ring at all leaves almost nothing between 200 Hz and 1 kHz, which is the
// band an engine's own rumble does not sit in. This rebuilds both voices exactly as the game builds them and
// measures the energy in that band, plus how long the sound lasts.
//
// The old voice is transcribed from the previous ImpactClip.BuildBarrel; the new one from the current file.
// Run: node .checktmp/barrelband.js
const SR = 44100;

function noise(rand) { return rand() * 2 - 1; }
function coefficient(cutoff) { return 1 - Math.exp(-2 * Math.PI * cutoff / SR); }
function attack(t, s) { return t >= s ? 1 : t / s; }
function onePole(state, input, c) { return state + (input - state) * c; }

// a seeded PRNG so the measurement is the same every run
function makeRandom(seed) {
  let s = seed >>> 0;
  return () => { s = (s * 1664525 + 1013904223) >>> 0; return s / 4294967296; };
}

function tick(state, t, at, rand, corner, decay) {
  if (t < at) return [state, 0];
  const since = t - at;
  state = onePole(state, noise(rand), coefficient(corner));
  return [state, state * attack(since, 0.0006) * Math.exp(-since / decay)];
}

function oldVoice(seconds) {
  const rand = makeRandom(20261005);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let thump = 0, bodyPhase = 0, deadPhase = 0, flopState = 0;
  const bodyPitch = 158, bodyRatio = 1.62, deadPitch = 690, flopAt = 0.06;

  for (let i = 0; i < n; i++) {
    const t = i / SR;
    thump = onePole(thump, noise(rand), coefficient(1500));
    const strike = thump * attack(t, 0.0035) * Math.exp(-t / 0.022);

    bodyPhase += 2 * Math.PI * (bodyPitch + (bodyPitch * 0.82 - bodyPitch) * Math.min(1, t / 0.14)) / SR;
    const body = Math.sin(bodyPhase) * attack(t, 0.004) * Math.exp(-t / 0.085) +
                 Math.sin(bodyPhase * bodyRatio) * attack(t, 0.005) * Math.exp(-t / 0.05) * 0.5;

    deadPhase += 2 * Math.PI * deadPitch / SR;
    const dead = Math.sin(deadPhase) * attack(t, 0.002) * Math.exp(-t / 0.025) * 0.35;

    let flop; [flopState, flop] = tick(flopState, t, flopAt, rand, 1200, 0.02);

    out[i] = 0.5 * strike + 0.95 * body + dead + flop * 0.3;
  }
  return out;
}

// The blinder's rattle, transcribed from ImpactClip.BuildSheet, so the barrel can be measured against the
// voice the player already approves of.
function sheetVoice(seconds) {
  const rand = makeRandom(20261003);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let crack = 0, bodyPhase = 0, edgePhase = 0, rattleOne = 0, rattleTwo = 0;
  const bodyPitch = 245, bodyRatio = 1.7, edgePitch = 1200;

  for (let i = 0; i < n; i++) {
    const t = i / SR;
    crack = onePole(crack, noise(rand), coefficient(5200));
    const strike = crack * attack(t, 0.0012) * Math.exp(-t / 0.006);

    bodyPhase += 2 * Math.PI * (bodyPitch + (bodyPitch * 0.86 - bodyPitch) * Math.min(1, t / 0.12)) / SR;
    const body = Math.sin(bodyPhase) * attack(t, 0.0018) * Math.exp(-t / 0.075) +
                 Math.sin(bodyPhase * bodyRatio) * attack(t, 0.0025) * Math.exp(-t / 0.048) * 0.55;

    edgePhase += 2 * Math.PI * edgePitch / SR;
    const edge = Math.sin(edgePhase) * attack(t, 0.0008) * Math.exp(-t / 0.018);

    let r1, r2;
    [rattleOne, r1] = tick(rattleOne, t, 0.045, rand, 3000, 0.011);
    [rattleTwo, r2] = tick(rattleTwo, t, 0.095, rand, 2400, 0.014);

    out[i] = 0.62 * strike + 0.85 * body + 0.3 * edge + r1 * 0.34 + r2 * 0.22;
  }
  return out;
}

function newVoice(seconds) {
  const rand = makeRandom(20261005);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let strikeState = 0, bodyPhase = 0, upperPhase = 0, edgePhase = 0, rattleOne = 0, rattleTwo = 0;
  const bodyPitch = 235, bodyRatio = 1.58, upperRatio = 2.34, edgePitch = 880;

  for (let i = 0; i < n; i++) {
    const t = i / SR;
    strikeState = onePole(strikeState, noise(rand), coefficient(3400));
    const strike = strikeState * attack(t, 0.0018) * Math.exp(-t / 0.011);

    const bend = Math.min(1, t / 0.2);
    bodyPhase += 2 * Math.PI * (bodyPitch + (bodyPitch * 0.9 - bodyPitch) * bend) / SR;

    const body = Math.sin(bodyPhase) * attack(t, 0.0015) * Math.exp(-t / 0.25) +
                 Math.sin(bodyPhase * bodyRatio) * attack(t, 0.002) * Math.exp(-t / 0.17) * 0.62;

    upperPhase += 2 * Math.PI * bodyPitch * upperRatio / SR;
    const upper = Math.sin(upperPhase) * attack(t, 0.0025) * Math.exp(-t / 0.11) * 0.4;

    edgePhase += 2 * Math.PI * edgePitch / SR;
    const edge = Math.sin(edgePhase) * attack(t, 0.001) * Math.exp(-t / 0.03) * 0.3;

    let r1, r2;
    [rattleOne, r1] = tick(rattleOne, t, 0.055, rand, 2600, 0.013);
    [rattleTwo, r2] = tick(rattleTwo, t, 0.115, rand, 2000, 0.016);

    out[i] = 0.55 * strike + 0.95 * body + upper + edge + r1 * 0.3 + r2 * 0.18;
  }
  return out;
}

function normalise(s, peak) {
  let loudest = 0;
  for (const v of s) loudest = Math.max(loudest, Math.abs(v));
  if (loudest < 1e-4) return;
  const k = peak / loudest;
  for (let i = 0; i < s.length; i++) s[i] *= k;
}

function fadeOut(s, seconds) {
  const fade = Math.max(1, Math.min(Math.round(seconds * SR), s.length));
  for (let i = 0; i < fade; i++) s[s.length - fade + i] *= 1 - i / fade;
}

// RMS inside a band, by one-pole high-pass then low-pass. Rough, but the same ruler for both voices.
function bandRms(s, low, high) {
  const cLow = coefficient(low), cHigh = coefficient(high);
  let hp = 0, lp = 0, sum = 0;
  for (const v of s) {
    hp = onePole(hp, v, cLow);
    lp = onePole(lp, hp, cHigh);
    sum += lp * lp;
  }
  return Math.sqrt(sum / s.length);
}

function rms(s) {
  let sum = 0;
  for (const v of s) sum += v * v;
  return Math.sqrt(sum / s.length);
}

// how long until it is 5% of the loudest - the point the ear stops hearing it
function tailSeconds(s) {
  let peak = 0;
  for (const v of s) peak = Math.max(peak, Math.abs(v));
  for (let i = s.length - 1; i >= 0; i--) if (Math.abs(s[i]) > 0.05 * peak) return i / SR;
  return 0;
}

// rms inside a time window
function windowRms(s, from, to) {
  const a = Math.max(0, Math.round(from * SR));
  const b = Math.min(s.length, Math.round(to * SR));
  let sum = 0;
  for (let i = a; i < b; i++) sum += s[i] * s[i];
  return Math.sqrt(sum / Math.max(1, b - a));
}

const rows = [];
for (const [name, fn, seconds, fade] of [
  ['sheet (blinder, approved)', sheetVoice, 0.5, 0.06],
  ['barrel: old (158 Hz)', oldVoice, 0.55, 0.08],
  ['barrel: new (235 Hz)', newVoice, 0.6, 0.1],
]) {
  const s = fn(seconds);
  normalise(s, 1);
  fadeOut(s, fade);

  rows.push({
    voice: name,
    'total rms': rms(s).toFixed(4),
    '200-900 Hz rms': bandRms(s, 200, 900).toFixed(4),
    '200-900 Hz share': (100 * bandRms(s, 200, 900) / rms(s)).toFixed(1) + '%',
    'middle (150-1k) rms': bandRms(s, 150, 1000).toFixed(4),
    '0-50ms rms': windowRms(s, 0, 0.05).toFixed(4),
    '50-150ms rms': windowRms(s, 0.05, 0.15).toFixed(4),
    '150-350ms rms': windowRms(s, 0.15, 0.35).toFixed(4),
    'audible for': tailSeconds(s).toFixed(2) + ' s',
  });
}

console.table(rows);
