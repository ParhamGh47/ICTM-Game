// Mirrors RainAmbienceClip.Build and PoliceSirenClip.Build to check what they actually produce: the loop
// seam (no click), the peak they normalise to, and that nothing is NaN or silent.
const SR = 22050;

// ---------------- rain
function rain() {
  const seconds = 6, fadeSec = 0.75, peakTarget = 0.55, seed = 20260925;
  const length = Math.round(seconds * SR);
  const fade = Math.max(1, Math.min(Math.round(fadeSec * SR), length / 2));
  const total = length + fade;
  const raw = new Float64Array(total);

  // xorshift-free: Node has no System.Random, so this is a stand-in of the same shape.
  let state = seed >>> 0;
  const next = () => {
    state ^= state << 13; state >>>= 0;
    state ^= state >>> 17;
    state ^= state << 5; state >>>= 0;
    return state / 4294967296;
  };

  let low = 0, lowSlow = 0, bright = 0;
  for (let i = 0; i < total; i++) {
    const white = next() * 2 - 1;
    low += (white - low) * 0.45;
    lowSlow += (low - lowSlow) * 0.06;
    bright += (white - bright) * 0.55;
    raw[i] = 0.6 * (low - lowSlow) + 0.4 * (bright - low);
  }

  // droplets
  const rate = 11, level = 0.16;
  for (let at = 0; at < total / SR; at += (1 / rate) * (0.35 + next() * 1.3)) {
    const start = Math.round(at * SR);
    const frequency = 1500 + next() * 2600;
    const decay = SR * (0.002 + next() * 0.004);
    const amp = level * (0.5 + next() * 0.5);
    const reach = Math.max(8, Math.round(decay * 3));
    for (let j = 0; j < reach; j++) {
      const index = start + j;
      if (index >= total) break;
      raw[index] += amp * Math.exp(-j / decay) * Math.sin((2 * Math.PI * frequency * j) / SR);
    }
  }

  for (let i = 0; i < total; i++) {
    const t = i / SR;
    raw[i] *= 1 + 0.18 * Math.sin((2 * Math.PI * (1 / seconds)) * t) + 0.1 * Math.sin((2 * Math.PI * (3 / seconds)) * t + 1.1);
  }

  const samples = new Float64Array(length);
  for (let i = 0; i < length; i++) samples[i] = raw[i];
  for (let i = 0; i < fade; i++) {
    const w = i / fade;
    samples[i] = raw[i] * w + raw[length + i] * (1 - w);
  }

  let peak = 0;
  for (const s of samples) peak = Math.max(peak, Math.abs(s));
  const scale = peak > 1e-4 ? peakTarget / peak : 1;
  for (let i = 0; i < samples.length; i++) samples[i] *= scale;

  return { name: 'rain', samples, seconds: length / SR };
}

// ---------------- siren
function siren() {
  const seconds = 6, centre = 900, span = 300, sweepSec = 1.5, peakTarget = 0.6;
  const length = Math.round(seconds * SR);
  const samples = new Float64Array(length);
  let phase = 0;
  for (let i = 0; i < length; i++) {
    const t = i / SR;
    const f = centre + span * Math.sin((2 * Math.PI * t) / sweepSec);
    phase += (2 * Math.PI * f) / SR;
    samples[i] = Math.sin(phase) + 0.32 * Math.sin(2 * phase) + 0.12 * Math.sin(3 * phase);
  }
  let peak = 0;
  for (const s of samples) peak = Math.max(peak, Math.abs(s));
  const scale = peakTarget / peak;
  for (let i = 0; i < samples.length; i++) samples[i] *= scale;
  return { name: 'siren', samples, seconds: length / SR, phaseAtEnd: phase / (2 * Math.PI) };
}

function report(c) {
  const n = c.samples.length;
  let peak = 0, sum = 0, nan = 0;
  for (const s of c.samples) {
    if (!Number.isFinite(s)) nan++;
    peak = Math.max(peak, Math.abs(s));
    sum += s * s;
  }
  const rms = Math.sqrt(sum / n);

  // The seam, measured the way a click is actually heard: the jump from the last sample to the first is one
  // more sample step of the same waveform. If it is no bigger than the biggest step inside the clip, the loop
  // is as smooth as the sound itself and there is nothing to click.
  let maxStep = 0;
  for (let i = 1; i < n; i++) maxStep = Math.max(maxStep, Math.abs(c.samples[i] - c.samples[i - 1]));
  const seam = Math.abs(c.samples[0] - c.samples[n - 1]);
  console.log(
    c.name.padEnd(6) +
      ' samples ' + String(n).padStart(7) +
      '  ' + c.seconds.toFixed(2) + 's' +
      '  peak ' + peak.toFixed(3) +
      '  rms ' + rms.toFixed(3) +
      '  seam step ' + seam.toFixed(4) + ' vs biggest inner step ' + maxStep.toFixed(4) +
      (seam <= maxStep ? '  seamless' : '  CLICK') +
      (nan ? '  ' + nan + ' non-finite!' : '') +
      (c.phaseAtEnd !== undefined ? '  cycles ' + c.phaseAtEnd.toFixed(4) : '')
  );
}

report(rain());
report(siren());
