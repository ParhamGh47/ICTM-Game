// A faithful port of BoostClip.BuildCollect (sharpened version), so a sound nobody can hear here can still be
// checked: where the energy is over time, whether the click and the lift land, and how loud it now is against
// the version before it (peak 0.6, overall rms 0.166, 0.5 s).
const SampleRate = 44100;
const CollectSeconds = 0.9;
const CollectPeak = 0.8;
const FirstNoteAt = 0.05, FirstNotePitch = 1046.5;
const SecondNoteAt = 0.13, SecondNotePitch = 1568;
const ThirdNoteAt = 0.21, ThirdNotePitch = 2093;
const LiftSeconds = 0.05, LiftStartPitch = 520;

const next = (() => { let s = 20260930; return () => (s = (s * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff; })();
const noise = () => next() * 2 - 1;
const coeff = (cut) => 1 - Math.exp(-2 * Math.PI * cut / SampleRate);
const attack = (t, s) => (t >= s ? 1 : t / s);

function note(since, freq, decay) {
  if (since < 0) return 0;
  const p = 2 * Math.PI * freq * since;
  return attack(since, 0.0015) * (
    Math.sin(p) * Math.exp(-since / decay) +
    0.55 * Math.sin(2 * p) * Math.exp(-since / (decay * 0.85)) +
    0.32 * Math.sin(3 * p) * Math.exp(-since / (decay * 0.7)) +
    0.16 * Math.sin(4 * p) * Math.exp(-since / (decay * 0.55)));
}

const n = Math.round(CollectSeconds * SampleRate);
const x = new Float64Array(n);
const parts = { click: new Float64Array(n), froth: new Float64Array(n), lift: new Float64Array(n), notes: new Float64Array(n) };
let click = 0, froth = 0, liftPhase = 0;

for (let i = 0; i < n; i++) {
  const t = i / SampleRate;

  click += (noise() - click) * coeff(12000);
  const edge = click * attack(t, 0.001) * Math.exp(-t / 0.006);

  froth += (noise() - froth) * coeff(9000);
  const fizz = froth * attack(t, 0.004) * Math.exp(-t / 0.03);

  let lift = 0;
  if (t < LiftSeconds) {
    liftPhase += 2 * Math.PI * (LiftStartPitch + (FirstNotePitch - LiftStartPitch) * (t / LiftSeconds)) / SampleRate;
    lift = Math.sin(liftPhase) * attack(t, 0.002) * Math.exp(-t / 0.028);
  }

  const notes = 0.5 * note(t - FirstNoteAt, FirstNotePitch, 0.22) +
                0.55 * note(t - SecondNoteAt, SecondNotePitch, 0.28) +
                0.6 * note(t - ThirdNoteAt, ThirdNotePitch, 0.5);

  parts.click[i] = 0.35 * edge;
  parts.froth[i] = 0.2 * fizz;
  parts.lift[i] = 0.2 * lift;
  parts.notes[i] = notes;
  x[i] = 0.35 * edge + 0.2 * fizz + 0.2 * lift + notes;
}

let peak = 0, peakAt = 0;
for (let i = 0; i < n; i++) if (Math.abs(x[i]) > peak) { peak = Math.abs(x[i]); peakAt = i / SampleRate; }
const scale = CollectPeak / peak;

// the same fade the clip uses, so the tail can be judged
const fade = Math.round(0.12 * SampleRate);
for (let i = 0; i < fade; i++) x[n - fade + i] *= 1 - i / fade;

const rms = (a) => Math.sqrt(a.reduce((s, v) => s + v * v, 0) / a.length);
console.log(`pre-normalise peak ${peak.toFixed(3)} at ${peakAt.toFixed(3)}s -> scale ${scale.toFixed(3)}`);
console.log('bucket  ms      rms    click   froth    lift   notes  bar');
for (let b = 0; b * 2205 + 2205 <= n; b++) {
  let sum = 0;
  const per = {};
  for (const k of Object.keys(parts)) per[k] = 0;
  for (let i = b * 2205; i < b * 2205 + 2205; i++) {
    sum += x[i] * x[i];
    for (const k of Object.keys(parts)) per[k] += parts[k][i] * parts[k][i];
  }
  const r = Math.sqrt(sum / 2205) * scale;
  const col = (k) => (Math.sqrt(per[k] / 2205) * scale).toFixed(3).padStart(7);
  console.log(String(b).padStart(4), String(b * 50).padStart(5), r.toFixed(4).padStart(8),
    col('click'), col('froth'), col('lift'), col('notes'), '#'.repeat(Math.round(r * 60)));
}
console.log('\noverall rms:', rms(x).toFixed(4), ' (was 0.1664)');
console.log('loudest rms in any 20ms:', (() => {
  let m = 0;
  for (let i = 0; i + 882 < n; i += 882) {
    let s = 0; for (let j = i; j < i + 882; j++) s += x[j] * x[j];
    m = Math.max(m, Math.sqrt(s / 882));
  }
  return m.toFixed(4);
})(), ' (was 0.273)');
console.log('length:', (n / SampleRate).toFixed(2) + 's (was 0.50s)', ' peak:', CollectPeak, ' (was 0.6)');
console.log('first sample:', x[0].toFixed(6), ' last sample:', x[n - 1].toFixed(6));
