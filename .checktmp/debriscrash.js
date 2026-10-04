// "It sounds like metal" - and it does, because the rebuild that made it audible did it with sine partials:
// a pure 1.8 kHz edge and a pure 3.15 kHz ring. Pure tones are metal. A crash is *noise*.
//
// So this builds the same thing out of filtered noise instead, and measures its spectrum against the game's
// own impact recording (hit.wav), which is the sound the player actually asked it to be like. Note that
// recording has a smooth falling spectrum with no tone in it anywhere - that is the target shape.
//
// Run: node .checktmp/debriscrash.js
const fs = require('fs');
const path = require('path');
const SR = 44100;

function noise(rand) { return rand() * 2 - 1; }
function coefficient(cutoff) { return 1 - Math.exp(-2 * Math.PI * cutoff / SR); }
function attack(t, s) { return t >= s ? 1 : t / s; }
function onePole(state, input, c) { return state + (input - state) * c; }

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

// ---- the game's own impact recording -------------------------------------------------------------------

function readWav(file) {
  const buf = fs.readFileSync(file);
  let pos = 12, fmt = null, data = null;
  while (pos + 8 <= buf.length) {
    const id = buf.toString('ascii', pos, pos + 4);
    const size = buf.readUInt32LE(pos + 4);
    const body = pos + 8;
    if (id === 'fmt ') fmt = { channels: buf.readUInt16LE(body + 2), sampleRate: buf.readUInt32LE(body + 4), bits: buf.readUInt16LE(body + 14) };
    else if (id === 'data') data = buf.subarray(body, body + size);
    pos = body + size + (size % 2);
  }
  const frames = data.length / (fmt.channels * (fmt.bits / 8));
  const scale = 1 / Math.pow(2, fmt.bits - 1);
  const samples = new Float64Array(frames);
  for (let i = 0; i < frames; i++) {
    let sum = 0;
    for (let c = 0; c < fmt.channels; c++) sum += data.readInt16LE((i * fmt.channels + c) * 2) * scale;
    samples[i] = sum / fmt.channels;
  }
  return samples;
}

// ---- the voices the player already approves of ---------------------------------------------------------

function sheetVoice(seconds) {
  const rand = makeRandom(20261003);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let crack = 0, bodyPhase = 0, edgePhase = 0, r1s = 0, r2s = 0;
  for (let i = 0; i < n; i++) {
    const t = i / SR;
    crack = onePole(crack, noise(rand), coefficient(5200));
    const strike = crack * attack(t, 0.0012) * Math.exp(-t / 0.006);
    bodyPhase += 2 * Math.PI * (245 + (245 * 0.86 - 245) * Math.min(1, t / 0.12)) / SR;
    const body = Math.sin(bodyPhase) * attack(t, 0.0018) * Math.exp(-t / 0.075) +
                 Math.sin(bodyPhase * 1.7) * attack(t, 0.0025) * Math.exp(-t / 0.048) * 0.55;
    edgePhase += 2 * Math.PI * 1200 / SR;
    const edge = Math.sin(edgePhase) * attack(t, 0.0008) * Math.exp(-t / 0.018);
    let r1, r2;
    [r1s, r1] = tick(r1s, t, 0.045, rand, 3000, 0.011);
    [r2s, r2] = tick(r2s, t, 0.095, rand, 2400, 0.014);
    out[i] = 0.62 * strike + 0.85 * body + 0.3 * edge + r1 * 0.34 + r2 * 0.22;
  }
  return out;
}

function barrelVoice(seconds) {
  const rand = makeRandom(20261005);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let strikeState = 0, bodyPhase = 0, upperPhase = 0, edgePhase = 0, r1s = 0, r2s = 0;
  for (let i = 0; i < n; i++) {
    const t = i / SR;
    strikeState = onePole(strikeState, noise(rand), coefficient(3400));
    const strike = strikeState * attack(t, 0.0018) * Math.exp(-t / 0.011);
    bodyPhase += 2 * Math.PI * (235 + (235 * 0.9 - 235) * Math.min(1, t / 0.2)) / SR;
    const body = Math.sin(bodyPhase) * attack(t, 0.0015) * Math.exp(-t / 0.25) +
                 Math.sin(bodyPhase * 1.58) * attack(t, 0.002) * Math.exp(-t / 0.17) * 0.62;
    upperPhase += 2 * Math.PI * 235 * 2.34 / SR;
    const upper = Math.sin(upperPhase) * attack(t, 0.0025) * Math.exp(-t / 0.11) * 0.4;
    edgePhase += 2 * Math.PI * 880 / SR;
    const edge = Math.sin(edgePhase) * attack(t, 0.001) * Math.exp(-t / 0.03) * 0.3;
    let r1, r2;
    [r1s, r1] = tick(r1s, t, 0.055, rand, 2600, 0.013);
    [r2s, r2] = tick(r2s, t, 0.115, rand, 2000, 0.016);
    out[i] = 0.55 * strike + 0.95 * body + upper + edge + r1 * 0.3 + r2 * 0.18;
  }
  return out;
}

// The metallic rebuild now in the game: bright, audible, and made of sine partials. Kept here only to
// measure the thing being replaced against the thing replacing it.
function debrisMetal(seconds) {
  const rand = makeRandom(20261006);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let strikeState = 0, bodyPhase = 0, knockPhase = 0, edgePhase = 0, ringPhase = 0;
  const cs = [0, 0, 0, 0, 0];
  const clatter = [[0.04, 3800, 0.010, 0.6], [0.1, 3400, 0.012, 0.5], [0.17, 3000, 0.013, 0.4], [0.26, 2700, 0.015, 0.32], [0.36, 2400, 0.017, 0.25]];
  for (let i = 0; i < n; i++) {
    const t = i / SR;
    strikeState = onePole(strikeState, noise(rand), coefficient(5200));
    const strike = strikeState * attack(t, 0.001) * Math.exp(-t / 0.0075);
    bodyPhase += 2 * Math.PI * (96 + (96 * 0.9 - 96) * Math.min(1, t / 0.16)) / SR;
    const body = Math.sin(bodyPhase) * attack(t, 0.0025) * Math.exp(-t / 0.3) +
                 Math.sin(bodyPhase * 1.62) * attack(t, 0.003) * Math.exp(-t / 0.14) * 0.5;
    knockPhase += 2 * Math.PI * 420 / SR;
    const knock = Math.sin(knockPhase) * attack(t, 0.001) * Math.exp(-t / 0.28) * 0.9;
    edgePhase += 2 * Math.PI * 1800 / SR;
    const edge = Math.sin(edgePhase) * attack(t, 0.0008) * Math.exp(-t / 0.08) * 0.62;
    ringPhase += 2 * Math.PI * 3150 / SR;
    const ring = Math.sin(ringPhase) * attack(t, 0.0015) * Math.exp(-t / 0.2) * 0.52;
    let mix = 0.85 * strike + 1.1 * body + knock + edge + ring;
    for (let k = 0; k < clatter.length; k++) {
      let hit;
      [cs[k], hit] = tick(cs[k], t, clatter[k][0], rand, clatter[k][1], clatter[k][2]);
      mix += hit * clatter[k][3];
    }
    out[i] = mix;
  }
  return out;
}

// The crash: no sine anywhere. A sharp broadband crack to open it, a body of filtered noise for the blow
// itself, a low weight under it, and three short noise ticks as the junk scatters. Every part is noise run
// through a one-pole, which is the same thing the default recording is made of.
function debrisCrash(seconds) {
  const rand = makeRandom(20261006);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let crack = 0, lowState = 0, lowState2 = 0;
  const ts = [0, 0, 0];
  const scatter = [[0.06, 2200, 0.009, 0.16], [0.13, 1900, 0.011, 0.11], [0.22, 1600, 0.013, 0.08]];

  for (let i = 0; i < n; i++) {
    const t = i / SR;

    // The crash is one noise source through two poles at 250 Hz. One pole was not enough - at 6 dB an octave
    // it let so much 300-800 Hz through that the sound was a thick whoosh rather than a thud - and the
    // default recording falls away from the low end across every band, so this has to as well.
    lowState = onePole(lowState, noise(rand), coefficient(170));
    lowState2 = onePole(lowState2, lowState, coefficient(170));

    // The strike: a slow body that carries the hit, and a long low tail under it, which is what makes it a
    // crash and not a click. Both from the same filtered noise, so there is still no pitch anywhere.
    const body = lowState2 * attack(t, 0.002) * (0.6 * Math.exp(-t / 0.2) + 0.4 * Math.exp(-t / 0.6));

    // The bright crack over it: the whole top of the noise, gone in a few milliseconds. This is the transient
    // that cuts through the engine, and it is why the rebuild became audible at all - it is just noise here
    // rather than the partials that made it sound like metal.
    crack = onePole(crack, noise(rand), coefficient(3500));
    const edge = crack * attack(t, 0.0005) * Math.exp(-t / 0.007);

    // The crack is loud per sample but only lasts milliseconds, while the body is quiet per sample and
    // lasts half a second. Since the finished sound is scaled to a fixed peak, a heavy crack would simply
    // shrink everything else, so it is kept small here and the level is set by the crash itself.
    let mix = 21.0 * body + 0.5 * edge;
    for (let k = 0; k < scatter.length; k++) {
      let h;
      [ts[k], h] = tick(ts[k], t, scatter[k][0], rand, scatter[k][1], scatter[k][2]);
      mix += h * scatter[k][3];
    }
    out[i] = mix;
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

function lowPass(s, cutoff) {
  const c = coefficient(cutoff);
  const out = new Float64Array(s.length);
  let state = 0;
  for (let i = 0; i < s.length; i++) { state += (s[i] - state) * c; out[i] = state; }
  return out;
}
function bandRms(s, low, high) {
  const below = lowPass(s, low);
  const rest = new Float64Array(s.length);
  for (let i = 0; i < s.length; i++) rest[i] = s[i] - below[i];
  const inside = lowPass(rest, high);
  let sum = 0;
  for (let i = 0; i < inside.length; i++) sum += inside[i] * inside[i];
  return Math.sqrt(sum / inside.length);
}
function rms(s) { let sum = 0; for (let i = 0; i < s.length; i++) sum += s[i] * s[i]; return Math.sqrt(sum / s.length); }
function tail(s) {
  let peak = 0;
  for (let i = 0; i < s.length; i++) peak = Math.max(peak, Math.abs(s[i]));
  for (let i = s.length - 1; i >= 0; i--) if (Math.abs(s[i]) > 0.05 * peak) return i / SR;
  return 0;
}
function winRms(s, from, to) {
  const a = Math.max(0, Math.round(from * SR)), b = Math.min(s.length, Math.round(to * SR));
  let sum = 0;
  for (let i = a; i < b; i++) sum += s[i] * s[i];
  return Math.sqrt(sum / Math.max(1, b - a));
}

const world = readWav(path.join(__dirname, '..', 'Assets', 'Audio', 'SFX', 'Truck', 'hit.wav'));

const rows = [];
for (const [name, s] of [
  ['DEFAULT (hit.wav)', world],
  ['sheet (approved)', (() => { const v = sheetVoice(0.5); normalise(v, 1); fadeOut(v, 0.06); return v; })()],
  ['barrel (audible)', (() => { const v = barrelVoice(0.6); normalise(v, 1); fadeOut(v, 0.1); return v; })()],
  ['debris METAL (now)', (() => { const v = debrisMetal(0.7); normalise(v, 1); fadeOut(v, 0.12); return v; })()],
  ['debris CRASH (new)', (() => { const v = debrisCrash(0.6); normalise(v, 1); fadeOut(v, 0.1); return v; })()],
]) {
  rows.push({
    voice: name,
    'total rms': rms(s).toFixed(4),
    '40-120': bandRms(s, 40, 120).toFixed(4),
    '120-300': bandRms(s, 120, 300).toFixed(4),
    '300-800': bandRms(s, 300, 800).toFixed(4),
    '800-2k': bandRms(s, 800, 2000).toFixed(4),
    '2-6k': bandRms(s, 2000, 6000).toFixed(4),
    '0-50ms': winRms(s, 0, 0.05).toFixed(4),
    '150-350ms': winRms(s, 0.15, 0.35).toFixed(4),
    'audible for': tail(s).toFixed(2) + ' s',
  });
}

console.table(rows);
