// Simulates the AI controller over Core-1's real waypoint loop and reports where the motion
// is jerky: step-to-step changes in yaw rate and in speed, and how far the car cuts the path.
const fs = require('fs');
const path = require('path');

const scenePath = path.join(process.cwd(), 'Assets/Scenes/Levels Scenes/1/Core-1.unity');
const raw = fs.readFileSync(scenePath, 'utf8').replace(/\r\n/g, '\n');

const docs = [];
let current = null;
for (const line of raw.split('\n')) {
  const m = line.match(/^--- !u!(\d+) &(\d+)(.*)$/);
  if (m) {
    if (current) docs.push(current);
    current = { classId: +m[1], fileId: +m[2], lines: [] };
    continue;
  }
  if (current) current.lines.push(line);
}
if (current) docs.push(current);

const transforms = new Map();
for (const d of docs) if (d.classId === 4) transforms.set(d.fileId, d);
const goName = new Map();
for (const d of docs) {
  if (d.classId !== 1) continue;
  const m = d.lines.join('\n').match(/m_Name:\s*(.*)/);
  if (m) goName.set(d.fileId, m[1].trim());
}
const flat = (d) => d.lines.join('\n');
const localPos = (d) => {
  const m = flat(d).match(/m_LocalPosition:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}/);
  return m ? [+m[1], +m[2], +m[3]] : null;
};
const quadOf = (d) => {
  const m = flat(d).match(/m_LocalRotation:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+),\s*w:\s*(-?[\d.eE+-]+)\}/);
  return m ? [+m[1], +m[2], +m[3], +m[4]] : null;
};
const parentOf = (d) => {
  const m = flat(d).match(/m_Father:\s*\{fileID:\s*(-?\d+)\}/);
  return m ? +m[1] : 0;
};
const goOf = (d) => {
  const m = flat(d).match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/);
  return m ? +m[1] : null;
};
function qrot(q, v) {
  const [x, y, z, w] = q;
  const t = [2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])];
  return [
    v[0] + w * t[0] + (y * t[2] - z * t[1]),
    v[1] + w * t[1] + (z * t[0] - x * t[2]),
    v[2] + w * t[2] + (x * t[1] - y * t[0]),
  ];
}
function qmul(a, b) {
  return [
    a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
    a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
    a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
    a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
  ];
}
function worldPose(fileId) {
  let pos = [0, 0, 0], rot = [0, 0, 0, 1], id = fileId;
  while (id && transforms.has(id)) {
    const d = transforms.get(id);
    pos = qrot(rot, localPos(d) || [0, 0, 0]).map((x, i) => x + pos[i]);
    rot = qmul(rot, quadOf(d) || [0, 0, 0, 1]);
    id = parentOf(d);
  }
  return { pos, rot };
}

const data = [];
for (const d of transforms.values()) {
  const block = flat(d).match(/m_Children:([\s\S]*?)(?:\n  \w|$)/);
  if (!block) continue;
  const kids = [...block[1].matchAll(/fileID:\s*(-?\d+)/g)].map((m) => +m[1]).filter((k) => {
    const t = transforms.get(k);
    if (!t) return false;
    const go = goOf(t);
    const n = go ? goName.get(go) : null;
    return n && /^WP_\d+$/.test(n);
  });
  if (kids.length > 100) data.push(kids);
}

const wps = data[0].map((k) => {
  const w = worldPose(k);
  return { pos: w.pos, fwd: qrot(w.rot, [0, 0, 1]) };
});

const dt = 0.02;
const cfg = {
  speedKPH: 30, turnSpeed: 3, maxSteerAngle: 150, reachThreshold: 3,
  cornerAngle: 22, cornerFullAngle: 140, cornerSpeedFloor: 0.5,
  cornerBrakeRate: 1.6, cornerRecoveryRate: 0.5, cornerSteerBoost: 2,
};
const speed = cfg.speedKPH / 3.6;
const sub = (a, b) => [a[0] - b[0], 0, a[2] - b[2]];
const hlen = (a) => Math.hypot(a[0], a[2]);
const norm = (a) => { const l = hlen(a) || 1; return [a[0] / l, 0, a[2] / l]; };
const signedAngle = (f, t) => (Math.atan2(f[2] * t[0] - f[0] * t[2], f[0] * t[0] + f[2] * t[2]) * 180) / Math.PI;
const tightness = (d) => Math.min(1, Math.max(0, (d - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle)));

function simulate(perStepRadii) {
  let pos = wps[0].pos.slice();
  let yaw = (Math.atan2(wps[0].fwd[0], wps[0].fwd[2]) * 180) / Math.PI;
  let cur = 0, speedFactor = 1;
  const log = [];
  for (let s = 0; s < Math.round(240 / dt); s++) {
    let guard = 0;
    while (guard++ < wps.length) {
      if (hlen(sub(wps[cur].pos, pos)) >= cfg.reachThreshold) break;
      cur = (cur + 1) % wps.length;
    }
    const target = wps[cur].pos;
    const ahead = norm(sub(target, pos));
    const forward = [Math.sin((yaw * Math.PI) / 180), 0, Math.cos((yaw * Math.PI) / 180)];
    const angle = signedAngle(forward, ahead);

    const after = wps[(cur + 1) % wps.length].pos;
    const thisLeg = sub(target, pos), nextLeg = sub(after, target);
    const bend = hlen(thisLeg) > 0.01 && hlen(nextLeg) > 0.01
      ? Math.acos(Math.max(-1, Math.min(1, (thisLeg[0] * nextLeg[0] + thisLeg[2] * nextLeg[2]) / (hlen(thisLeg) * hlen(nextLeg))))) * 180 / Math.PI
      : 0;
    const tight = tightness(Math.max(Math.abs(angle), bend));
    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * tight;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);

    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tight);
    const steering = Math.max(-cfg.maxSteerAngle, Math.min(cfg.maxSteerAngle, angle));
    const yawBefore = yaw;
    yaw += steering * steerRate * dt;

    const step = speed * speedFactor * dt;
    pos = [pos[0] + forward[0] * step, pos[1], pos[2] + forward[2] * step];

    // lateral distance from the path, against the whole polyline (nearest point)
    let best = 1e9;
    for (let i = 0; i < wps.length; i++) {
      const a = wps[i].pos, b = wps[(i + 1) % wps.length].pos;
      const ab = sub(b, a), ap = sub(pos, a);
      const L = ab[0] * ab[0] + ab[2] * ab[2];
      const t = L ? Math.max(0, Math.min(1, (ap[0] * ab[0] + ap[2] * ab[2]) / L)) : 0;
      const d = Math.hypot(pos[0] - (a[0] + ab[0] * t), pos[2] - (a[2] + ab[2] * t));
      if (d < best) best = d;
    }

    log.push({ s, yawRate: (yaw - yawBefore) / dt, speedFactor, off: best, cur });
  }
  return log;
}

const log = simulate();
const rates = log.map((e) => e.yawRate);
const jolts = [];
for (let i = 1; i < log.length; i++) jolts.push({ i, d: Math.abs(log[i].yawRate - log[i - 1].yawRate) });
const sorted = jolts.slice().sort((a, b) => b.d - a.d);
const pct = (arr, p) => arr[Math.min(arr.length - 1, Math.floor(arr.length * p))];

console.log(`yaw rate: min ${Math.min(...rates).toFixed(1)} max ${Math.max(...rates).toFixed(1)} deg/s`);
console.log(`yaw-rate jolt per step (deg/s change): median ${pct(jolts.map((j) => j.d).sort((a, b) => a - b), 0.5).toFixed(2)}  p95 ${pct(jolts.map((j) => j.d).sort((a, b) => a - b), 0.95).toFixed(2)}  max ${sorted[0].d.toFixed(2)}`);
console.log('worst jolts at elapsed seconds:', sorted.slice(0, 10).map((j) => (j.i * dt).toFixed(2)).join(', '));
const off = log.map((e) => e.off);
console.log(`lateral error from the path: mean ${(off.reduce((a, b) => a + b, 0) / off.length).toFixed(2)} m  p95 ${pct(off.slice().sort((a, b) => a - b), 0.95).toFixed(2)} m  max ${Math.max(...off).toFixed(2)} m`);
// how much of the run is spent more than a metre off the path
console.log(`share of steps >1 m off: ${((off.filter((o) => o > 1).length / off.length) * 100).toFixed(1)}%`);

// speed factor changes
const sf = log.map((e) => e.speedFactor);
let sfJolts = 0;
for (let i = 1; i < sf.length; i++) if (Math.abs(sf[i] - sf[i - 1]) > 0.02) sfJolts++;
console.log(`speed-factor steps over 2%: ${sfJolts} of ${sf.length} (${((sfJolts / sf.length) * 100).toFixed(1)}%)`);
