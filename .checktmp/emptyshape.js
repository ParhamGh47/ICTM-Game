// A port of BoostClip.BuildEmpty, so a sound nobody can hear here can still be checked: that the snap and the
// knocks land where they should, how loud it now is against the version before it, and how much of it is
// inside the band a laptop or phone speaker can actually reproduce (the reason the first version was silent).
const SampleRate = 44100;
const EmptySeconds = 0.8;
const EmptyPeak = 0.95;
const EmptyVolume = 0.85;
const EmptyKnockPitch = 430, EmptyKnockFall = 300;
const EmptySecondPitch = 360, EmptySecondFall = 250;
const EmptySecondAt = 0.19, EmptySecondLevel = 0.62;

const next = (() => { let s = 20260931; return () => (s = (s * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff; })();
const noise = () => next() * 2 - 1;
const coeff = (cut) => 1 - Math.exp(-2 * Math.PI * cut / SampleRate);
const attack = (t, s) => (t >= s ? 1 : t / s);
const lerp = (a, b, t) => a + (b - a) * Math.max(0, Math.min(1, t));
const knock = (phase, since, decay) =>
  attack(since, 0.0012) * (Math.sin(phase) * Math.exp(-since / decay) +
    0.6 * Math.sin(phase * 2.35) * Math.exp(-since / (decay * 0.66)) +
    0.34 * Math.sin(phase * 3.9) * Math.exp(-since / (decay * 0.42)));

const n = Math.round(EmptySeconds * SampleRate);
const x = new Float64Array(n);
const parts = { click: new Float64Array(n), first: new Float64Array(n), second: new Float64Array(n), air: new Float64Array(n), body: new Float64Array(n) };

let snap = 0, puff = 0, p1 = 0, p2 = 0, pb = 0;
for (let i = 0; i < n; i++) {
  const t = i / SampleRate;

  snap += (noise() - snap) * coeff(6000);
  const click = snap * attack(t, 0.0006) * Math.exp(-t / 0.008);

  let first = 0;
  if (t >= 0.01) {
    const since = t - 0.01;
    p1 += 2 * Math.PI * lerp(EmptyKnockPitch, EmptyKnockFall, since / 0.07) / SampleRate;
    first = knock(p1, since, 0.055);
  }

  let second = 0;
  if (t >= EmptySecondAt) {
    const since = t - EmptySecondAt;
    p2 += 2 * Math.PI * lerp(EmptySecondPitch, EmptySecondFall, since / 0.07) / SampleRate;
    second = knock(p2, since, 0.05);
  }

  puff += (noise() - puff) * coeff(lerp(3600, 700, t / 0.35));
  const air = puff * attack(t, 0.008) * Math.exp(-t / 0.3);

  pb += 2 * Math.PI * 95 / SampleRate;
  const body = Math.sin(pb) * attack(t, 0.004) * Math.exp(-t / 0.18);

  parts.click[i] = 0.38 * click;
  parts.first[i] = 1.05 * first;
  parts.second[i] = 0.5 * EmptySecondLevel * second;
  parts.air[i] = 0.5 * air;
  parts.body[i] = 0.32 * body;
  x[i] = 0.38 * click + 1.05 * first + 0.5 * EmptySecondLevel * second + 0.5 * air + 0.32 * body;
}

let peak = 0, peakAt = 0;
for (let i = 0; i < n; i++) if (Math.abs(x[i]) > peak) { peak = Math.abs(x[i]); peakAt = i / SampleRate; }
const scale = EmptyPeak / peak;
const fade = Math.round(0.12 * SampleRate);
for (let i = 0; i < fade; i++) x[n - fade + i] *= 1 - i / fade;

// Simple one-pole band split, so "what a laptop speaker can reproduce" is a measurement rather than a claim.
const bandRms = (sig, cut) => {
  let state = 0, sum = 0;
  const c = coeff(cut);
  for (let i = 0; i < sig.length; i++) { state += (sig[i] - state) * c; const hi = sig[i] - state; sum += hi * hi; }
  return Math.sqrt(sum / sig.length) * scale;
};
const rms = (a) => Math.sqrt(a.reduce((s, v) => s + v * v, 0) / a.length) * scale;

console.log(`pre-normalise peak ${peak.toFixed(3)} at ${peakAt.toFixed(3)}s -> scale ${scale.toFixed(3)}`);
console.log('bucket  ms      rms   click   first  second     air    body  bar');
const bucket = Math.round(0.01 * SampleRate);
for (let b = 0; b * bucket + bucket <= n; b++) {
  let sum = 0;
  const per = {};
  for (const k of Object.keys(parts)) per[k] = 0;
  for (let i = b * bucket; i < b * bucket + bucket; i++) {
    sum += x[i] * x[i];
    for (const k of Object.keys(parts)) per[k] += parts[k][i] * parts[k][i];
  }
  const r = Math.sqrt(sum / bucket) * scale;
  const col = (k) => (Math.sqrt(per[k] / bucket) * scale).toFixed(3).padStart(7);
  console.log(String(b).padStart(4), String(b * 10).padStart(4), r.toFixed(4).padStart(8),
    col('click'), col('first'), col('second'), col('air'), col('body'), '#'.repeat(Math.round(r * 45)));
}
console.log('\noverall rms:', rms(x).toFixed(4), '  length:', (n / SampleRate).toFixed(2) + 's', '  peak:', EmptyPeak);
console.log('energy above 300 Hz :', bandRms(x, 300).toFixed(4), '   above 1 kHz:', bandRms(x, 1000).toFixed(4));
console.log('what the player hears (volume x peak):', (EmptyVolume * EmptyPeak).toFixed(3),
  ' - was', (0.42 * 0.55).toFixed(3), 'on the first version, and', (0.66 * 0.9).toFixed(3), 'on the last');
console.log('first sample:', x[0].toFixed(6), ' last sample:', x[n - 1].toFixed(8));
