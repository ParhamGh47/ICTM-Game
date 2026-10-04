// The log/bin/trash-container hit "cannot be heard at all".
//
// The clip is generated correctly and the prefab marker is right, so the clip is not missing - it is being
// masked. Every voice the player CAN hear (the blinder's crack, the sign's clang, the barrel's knock) has a
// bright element, and the engine's rumble lives low. This measures the debris voice as it stands against the
// three that are audible, then against a sharper/longer rebuild, in the band an engine does NOT occupy:
// 1-4 kHz, where the ear is most sensitive.
//
// Current debris is transcribed from ImpactClip.BuildDebris; the rebuild is the one being proposed.
// Run: node .checktmp/debrisretune.js
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

// ---- the voices the player already approves of ----------------------------------------------------------

function sheetVoice(seconds) {
  const rand = makeRandom(20261003);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let crack = 0, bodyPhase = 0, edgePhase = 0, r1s = 0, r2s = 0;
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
    [r1s, r1] = tick(r1s, t, 0.055, rand, 2600, 0.013);
    [r2s, r2] = tick(r2s, t, 0.115, rand, 2000, 0.016);
    out[i] = 0.55 * strike + 0.95 * body + upper + edge + r1 * 0.3 + r2 * 0.18;
  }
  return out;
}

// ImpactClip.BuildDebris as it stands now: a dark strike at 900 Hz, no crack, 92 Hz body, all of it gone
// inside half a second.
function debrisNow(seconds) {
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
    let settle; [settleState, settle] = tick(settleState, t, settleAt, rand, 900, 0.02);
    out[i] = 0.6 * strike + 0.95 * body + hollow + settle * 0.22;
  }
  return out;
}

// The rebuild: a real crack to open it, weight still under it, a sharper hollow knock, a bright edge that
// lives where the engine does not, and two clatters as the junk settles. Longer, too.
function debrisNew(seconds) {
  const rand = makeRandom(20261006);
  const n = Math.round(seconds * SR);
  const out = new Float64Array(n);
  let strikeState = 0, bodyPhase = 0, knockPhase = 0, edgePhase = 0, ringPhase = 0;
  const cState = [0, 0, 0, 0, 0];
  const bodyPitch = 96, bodyRatio = 1.62, knockPitch = 420, edgePitch = 1800, ringPitch = 3150;
  // The junk coming apart: five clatters, each later, smaller and duller than the last, so the hit is
  // something that keeps happening rather than one instant the ear can miss.
  const clatter = [
    [0.04, 3800, 0.010, 0.6],
    [0.10, 3400, 0.012, 0.5],
    [0.17, 3000, 0.013, 0.4],
    [0.26, 2700, 0.015, 0.32],
    [0.36, 2400, 0.017, 0.25],
  ];

  for (let i = 0; i < n; i++) {
    const t = i / SR;

    // The blow landing on hard junk: broadband and sharp, not the soft 900 Hz nudge it used to be.
    strikeState = onePole(strikeState, noise(rand), coefficient(5200));
    const strike = strikeState * attack(t, 0.001) * Math.exp(-t / 0.0075);


    // The weight of the thing underneath it - kept, but no longer the whole sound.
    bodyPhase += 2 * Math.PI * (bodyPitch + (bodyPitch * 0.9 - bodyPitch) * Math.min(1, t / 0.16)) / SR;
    const body = Math.sin(bodyPhase) * attack(t, 0.0025) * Math.exp(-t / 0.3) +
                 Math.sin(bodyPhase * bodyRatio) * attack(t, 0.003) * Math.exp(-t / 0.14) * 0.5;

    // The hollow knock - sharper and higher than before, so it reads as a container and not as the world,
    // and held longer so the hit has something to carry.
    knockPhase += 2 * Math.PI * knockPitch / SR;
    const knock = Math.sin(knockPhase) * attack(t, 0.001) * Math.exp(-t / 0.28) * 0.9;

    // The bright edge and the ring over it: this is the part the engine cannot cover, and it is what makes
    // the hit carry rather than being a single instant the ear can miss.
    edgePhase += 2 * Math.PI * edgePitch / SR;
    const edge = Math.sin(edgePhase) * attack(t, 0.0008) * Math.exp(-t / 0.08) * 0.62;

    ringPhase += 2 * Math.PI * ringPitch / SR;
    const ring = Math.sin(ringPhase) * attack(t, 0.0015) * Math.exp(-t / 0.2) * 0.52;

    let mix = 0.85 * strike + 1.1 * body + knock + edge + ring;
    for (let k = 0; k < clatter.length; k++) {
      let hit;
      [cState[k], hit] = tick(cState[k], t, clatter[k][0], rand, clatter[k][1], clatter[k][2]);
      mix += hit * clatter[k][3];
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

// A real band-pass, which the first version of this was not: it low-passed at `low` and then low-passed
// again, so every voice measured as "95% in 1-4k" - a ruler that cannot see the difference, which is worse
// than no ruler. High-pass by subtracting a low-pass, then low-pass at the top.
function bandRms(s, low, high) {
  const cLow = coefficient(low), cHigh = coefficient(high);
  let lpLow = 0, lpHigh = 0, sum = 0;
  for (const v of s) {
    lpLow = onePole(lpLow, v, cLow);
    const highPassed = v - lpLow;
    lpHigh = onePole(lpHigh, highPassed, cHigh);
    sum += lpHigh * lpHigh;
  }
  return Math.sqrt(sum / s.length);
}

function rms(s) {
  let sum = 0;
  for (const v of s) sum += v * v;
  return Math.sqrt(sum / s.length);
}

function tailSeconds(s) {
  let peak = 0;
  for (const v of s) peak = Math.max(peak, Math.abs(v));
  for (let i = s.length - 1; i >= 0; i--) if (Math.abs(s[i]) > 0.05 * peak) return i / SR;
  return 0;
}

function windowRms(s, from, to) {
  const a = Math.max(0, Math.round(from * SR));
  const b = Math.min(s.length, Math.round(to * SR));
  let sum = 0;
  for (let i = a; i < b; i++) sum += s[i] * s[i];
  return Math.sqrt(sum / Math.max(1, b - a));
}

const rows = [];
for (const [name, fn, seconds, fade] of [
  ['sheet (approved)', sheetVoice, 0.5, 0.06],
  ['barrel (audible)', barrelVoice, 0.6, 0.1],
  ['debris NOW', debrisNow, 0.45, 0.08],
  ['debris NEW', debrisNew, 0.7, 0.12],
]) {
  const s = fn(seconds);
  normalise(s, 1);
  fadeOut(s, fade);

  // A rough A-weighting stand-in: energy 1-4 kHz is where the ear is sharpest and the engine is quietest.
  rows.push({
    voice: name,
    'total rms': rms(s).toFixed(4),
    '1-4k rms': bandRms(s, 1000, 4000).toFixed(4),
    '1-4k share': (100 * bandRms(s, 1000, 4000) / rms(s)).toFixed(1) + '%',
    '40-120 rms': bandRms(s, 40, 120).toFixed(4),
    '0-50ms rms': windowRms(s, 0, 0.05).toFixed(4),
    '50-150ms rms': windowRms(s, 0.05, 0.15).toFixed(4),
    '150-350ms rms': windowRms(s, 0.15, 0.35).toFixed(4),
    'audible for': tailSeconds(s).toFixed(2) + ' s',
  });
}

console.table(rows);
