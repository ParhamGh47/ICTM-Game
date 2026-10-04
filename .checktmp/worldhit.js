// What does the world's own impact recording actually look like, so a "similar but its own" voice can be
// built against it rather than guessed at?
//
// Run: node .checktmp/worldhit.js
const fs = require('fs');
const path = require('path');

function readWav(file) {
  const buf = fs.readFileSync(file);
  let pos = 12;
  let fmt = null;
  let data = null;

  while (pos + 8 <= buf.length) {
    const id = buf.toString('ascii', pos, pos + 4);
    const size = buf.readUInt32LE(pos + 4);
    const body = pos + 8;

    if (id === 'fmt ') {
      fmt = {
        channels: buf.readUInt16LE(body + 2),
        sampleRate: buf.readUInt32LE(body + 4),
        bits: buf.readUInt16LE(body + 14),
      };
    } else if (id === 'data') {
      data = buf.subarray(body, body + size);
    }

    pos = body + size + (size % 2);
  }

  if (!fmt || !data) throw new Error('no fmt/data: ' + file);

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

// A real one-pole low-pass, and a real one-pole high-pass (input minus its own low-pass).
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

// Energy in a band: low-pass at the top of the band, taken from the signal with everything below the
// bottom of the band removed. The two filters are one-pole, so the edges are soft - the same ruler for
// every voice compared here, which is what matters.
function bandRms(s, low, high, sr) {
  const above = highPass(s, low, sr);
  const inside = lowPass(above, high, sr);
  let sum = 0;
  for (let i = 0; i < inside.length; i++) sum += inside[i] * inside[i];
  return Math.sqrt(sum / inside.length);
}

function rms(s) { let sum = 0; for (let i = 0; i < s.length; i++) sum += s[i] * s[i]; return Math.sqrt(sum / s.length); }

function envelope(s, sr, step) {
  const out = [];
  const hop = Math.round(step * sr);
  for (let i = 0; i < s.length; i += hop) {
    let peak = 0;
    for (let j = i; j < Math.min(s.length, i + hop); j++) peak = Math.max(peak, Math.abs(s[j]));
    out.push(peak.toFixed(3));
  }
  return out.join(' ');
}

function tailSeconds(s, sr) {
  let peak = 0;
  for (let i = 0; i < s.length; i++) peak = Math.max(peak, Math.abs(s[i]));
  for (let i = s.length - 1; i >= 0; i--) if (Math.abs(s[i]) > 0.05 * peak) return i / sr;
  return 0;
}

const file = path.join(__dirname, '..', 'Assets', 'Audio', 'SFX', 'Truck', 'hit.wav');
const wav = readWav(file);
const s = wav.samples;
const sr = wav.sampleRate;

let peak = 0;
for (let i = 0; i < s.length; i++) peak = Math.max(peak, Math.abs(s[i]));

console.log('hit.wav  sr=' + sr + '  seconds=' + (s.length / sr).toFixed(3) + '  peak=' + peak.toFixed(3));
console.log('rms=' + rms(s).toFixed(4) + '   audible for=' + tailSeconds(s, sr).toFixed(3) + ' s');
console.log('peak envelope every 50 ms:');
console.log('  ' + envelope(s, sr, 0.05));

console.log('bands (rms):');
for (const [lo, hi] of [[40, 120], [120, 300], [300, 800], [800, 2000], [2000, 6000], [6000, 16000]]) {
  console.log('  ' + lo + '-' + hi + ' Hz: ' + bandRms(s, lo, hi, sr).toFixed(4));
}
