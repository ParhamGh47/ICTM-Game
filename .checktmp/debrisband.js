// Is the new loose-junk voice actually "similar to the default sound"? Measured, not guessed: the world's
// own recording is band-passed and compared with the generated voice band by band, and both are timed.
//
// Run: node .checktmp/debrisband.js
const fs = require('fs');
const path = require('path');
const SR = 44100;

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

  const channels = fmt.channels;
  const frames = data.length / (channels * (fmt.bits / 8));
  const scale = 1 / Math.pow(2, fmt.bits - 1);
  const samples = new Float64Array(frames);

  for (let i = 0; i < frames; i++) {
    let sum = 0;
    for (let c = 0; c < channels; c++) {
      const at = (i * channels + c) * (fmt.bits / 8);
      sum += fmt.bits === 16 ? data.readInt16LE(at) * scale : data.readFloatLE(at);
    }
    samples[i] = sum / channels;
  }

  return { samples, sampleRate: fmt.sampleRate };
}

function lowPass(s, cutoff, sr) {
  const c = 1 - Math.exp(-2 * Math.PI * cutoff / sr);
  const out = new Float64Array(s.length);
  let state = 0;
  for (let i = 0; i < s.length; i++) { state += (s[i] - state) * c; out[i] = state; }
  return out;
}

function highPass(s, cutoff, sr) {
  const low = lowPass(s, cutoff, sr);
  const out = new Float64Array(s.length);
  for (let i = 0; i < s.length; i++) out[i] = s[i] - low[i];
  return out;
}

function bandRms(s, low, high, sr) {
  const inside = lowPass(highPass(s, low, sr), high, sr);
  let sum = 0;
  for (let i = 0; i < inside.length; i++) sum += inside[i] * inside[i];
  return Math.sqrt(sum / inside.length);
}

function rms(s) { let sum = 0; for (let i = 0; i < s.length; i++) sum += s[i] * s[i]; return Math.sqrt(sum / s.length); }

function tailSeconds(s, sr) {
  let peak = 0;
  for (let i = 0; i < s.length; i++) peak = Math.max(peak, Math.abs(s[i]));
  for (let i = s.length - 1; i >= 0; i--) if (Math.abs(s[i]) > 0.05 * peak) return i / sr;
  return 0;
}

// --- the generated voice, transcribed from ImpactClip.BuildDebris -------------------------------------

function makeRandom(seed) {
  let s = seed >>> 0;
  return () => { s = (s * 1664525 + 1013904223) >>> 0; return s / 4294967296; };
}

function noise(rand) { return rand() * 2 - 1; }
function coefficient(cutoff) { return 1 - Math.exp(-2 * Math.PI * cutoff / SR); }
function attack(t, s) { return t >= s ? 1 : t / s; }
function onePole(state, input, c) { return state + (input - state) * c; }

function tick(state, t, at, rand, corner, decay) {
  if (t < at) return [state, 0];
  const since = t - at;
  state = onePole(state, noise(rand), coefficient(corner));
  return [state, state * attack(since, 0.0006) * Math.exp(-since / decay)];
}

function debrisVoice(seconds) {
  const rand = makeRandom(20261006);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let strikeState = 0, bodyPhase = 0, hollowPhase = 0, settleState = 0;
  const bodyPitch = 92, bodyRatio = 1.6, hollowPitch = 230, settleAt = 0.09;

  for (let i = 0; i < n; i++) {
    const t = i / SR;

    strikeState = onePole(strikeState, noise(rand), coefficient(900));
    const strike = strikeState * attack(t, 0.003) * Math.exp(-t / 0.028);

    bodyPhase += 2 * Math.PI * (bodyPitch + (bodyPitch * 0.9 - bodyPitch) * Math.min(1, t / 0.15)) / SR;
    const body = Math.sin(bodyPhase) * attack(t, 0.0035) * Math.exp(-t / 0.2) +
                 Math.sin(bodyPhase * bodyRatio) * attack(t, 0.004) * Math.exp(-t / 0.1) * 0.5;

    hollowPhase += 2 * Math.PI * hollowPitch / SR;
    const hollow = Math.sin(hollowPhase) * attack(t, 0.002) * Math.exp(-t / 0.07) * 0.32;

    let settle;
    [settleState, settle] = tick(settleState, t, settleAt, rand, 900, 0.02);

    out[i] = 0.6 * strike + 0.95 * body + hollow + settle * 0.22;
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

// --- compare -------------------------------------------------------------------------------------------

const world = readWav(path.join(__dirname, '..', 'Assets', 'Audio', 'SFX', 'Truck', 'hit.wav'));
const debris = debrisVoice(0.45);
normalise(debris, 1);
fadeOut(debris, 0.08);

const bands = [[40, 120], [120, 300], [300, 800], [800, 2000], [2000, 6000], [6000, 16000]];
const rows = [];

for (const [name, s, sr] of [['world (hit.wav)', world.samples, world.sampleRate], ['debris (generated)', debris, SR]]) {
  const row = { voice: name, 'total rms': rms(s).toFixed(4), 'audible for': tailSeconds(s, sr).toFixed(3) + ' s' };
  for (const [lo, hi] of bands) row[lo + '-' + hi] = bandRms(s, lo, hi, sr).toFixed(4);
  rows.push(row);
}

console.table(rows);

// And the share each band has of the total, which is what "similar" means when the clips are not the same
// length or the same peak.
console.log('\nshare of the band energy, by band:');
for (const [lo, hi] of bands) {
  const w = bandRms(world.samples, lo, hi, world.sampleRate);
  const d = bandRms(debris, lo, hi, SR);
  console.log('  ' + String(lo + '-' + hi + ' Hz').padEnd(12) + ' world ' + (100 * w / rms(world.samples)).toFixed(1).padStart(5) + '%   debris ' + (100 * d / rms(debris)).toFixed(1).padStart(5) + '%');
}
