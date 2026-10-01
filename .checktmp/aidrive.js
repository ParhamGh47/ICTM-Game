// Extracts the passing-car waypoint chain in driving order from Core-1 and simulates the
// AICarController steering exactly as written, to see whether the motion itself shimmies.
const fs = require('fs');
const path = require('path');

const root = process.cwd();
const scenePath = path.join(root, 'Assets/Scenes/Levels Scenes/1/Core-1.unity');
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

function flat(d) { return d.lines.join('\n'); }
function vec(re, text) {
  const m = text.match(re);
  return m ? [+m[1], +m[2], +m[3]] : null;
}
function quad(text) {
  const m = text.match(/m_LocalRotation:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+),\s*w:\s*(-?[\d.eE+-]+)\}/);
  return m ? [+m[1], +m[2], +m[3], +m[4]] : null;
}
function localPos(d) { return vec(/m_LocalPosition:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}/, flat(d)); }
function parentOf(d) {
  const m = flat(d).match(/m_Father:\s*\{fileID:\s*(-?\d+)\}/);
  return m ? +m[1] : 0;
}
function goOf(d) {
  const m = flat(d).match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/);
  return m ? +m[1] : null;
}

// quaternion helpers: rotate v by q
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
function qaxis(axis, deg) {
  const r = (deg * Math.PI) / 180, s = Math.sin(r / 2);
  return [axis[0] * s, axis[1] * s, axis[2] * s, Math.cos(r / 2)];
}
function qslerp(a, b, t) {
  let dot = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
  let bb = b;
  if (dot < 0) { bb = b.map((x) => -x); dot = -dot; }
  if (dot > 0.9995) {
    const r = a.map((x, i) => x + t * (bb[i] - x));
    const n = Math.hypot(...r);
    return r.map((x) => x / n);
  }
  const th = Math.acos(dot), s = Math.sin(th);
  const w1 = Math.sin((1 - t) * th) / s, w2 = Math.sin(t * th) / s;
  return a.map((x, i) => x * w1 + bb[i] * w2);
}

// world transform of a transform doc (walk up the parents)
function worldMatrix(fileId) {
  let pos = [0, 0, 0], rot = [0, 0, 0, 1];
  let id = fileId;
  while (id && transforms.has(id)) {
    const d = transforms.get(id);
    const p = localPos(d) || [0, 0, 0];
    const q = quad(flat(d)) || [0, 0, 0, 1];
    pos = add(qrot(rot, p), pos);
    rot = qmul(rot, q);
    id = parentOf(d);
  }
  return { pos, rot };
}
function add(a, b) { return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]; }
function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
function len(a) { return Math.hypot(a[0], a[1], a[2]); }
function norm(a) { const l = len(a) || 1; return [a[0] / l, a[1] / l, a[2] / l]; }

// gather the two paths
const paths = [];
for (const [fileId, d] of transforms) {
  const kids = [];
  const block = flat(d).match(/m_Children:([\s\S]*?)(?:\n  \w|$)/);
  if (!block) continue;
  for (const m of block[1].matchAll(/fileID:\s*(-?\d+)/g)) kids.push(+m[1]);
  const named = kids.filter((k) => {
    const t = transforms.get(k);
    if (!t) return false;
    const go = goOf(t);
    const n = go ? goName.get(go) : null;
    return n && /^WP_\d+$/.test(n);
  });
  if (named.length > 100) paths.push({ root: fileId, kids: named });
}

console.log('paths:', paths.length, paths.map((p) => p.kids.length).join(' / '));

const p = paths[0];
const wps = p.kids.map((k) => {
  const t = transforms.get(k);
  const { pos } = worldMatrix(k);
  const w = worldMatrix(k);
  const fwd = qrot(w.rot, [0, 0, 1]);
  return { pos, fwd };
});

// ---- simulate the controller
const cfg = {
  speedKPH: 30,
  turnSpeed: 3,
  maxSteerAngle: 150,
  reachThreshold: 3,
  cornerAngle: 22,
  cornerFullAngle: 140,
  cornerSpeedFloor: 0.5,
  cornerBrakeRate: 1.6,
  cornerRecoveryRate: 0.5,
  cornerSteerBoost: 2,
};
const dt = 0.02;
const v = cfg.speedKPH / 3.6;

// Unity's Vector3.SignedAngle(from, to, Vector3.up)
function signedAngle(from, to) {
  const crossY = from[2] * to[0] - from[0] * to[2];
  const dot = from[0] * to[0] + from[2] * to[2];
  return (Math.atan2(crossY, dot) * 180) / Math.PI;
}
function tightness(deg) {
  return Math.min(1, Math.max(0, (deg - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle)));
}

function run(lagged) {
  let pos = wps[0].pos.slice();
  let rot = [0, 0, 0, 1];
  let yaw = (Math.atan2(wps[0].fwd[0], wps[0].fwd[2]) * 180) / Math.PI;
  let cur = 0;
  let speedFactor = 1;
  const log = [];

  // lagged mode: the pose the controller reads is the previous step's pose
  let readPos = pos.slice(), readYaw = yaw;

  const steps = Math.round(180 / dt);
  for (let s = 0; s < steps; s++) {
    let guard = 0;
    while (guard++ < wps.length) {
      const t = wps[cur].pos;
      const to = [t[0] - readPos[0], 0, t[2] - readPos[2]];
      if (len(to) >= cfg.reachThreshold) break;
      cur = (cur + 1) % wps.length;
    }
    const t = wps[cur].pos;
    const toT = [t[0] - readPos[0], 0, t[2] - readPos[2]];
    const ahead = norm(toT);
    const desiredYaw = (Math.atan2(ahead[0], ahead[2]) * 180) / Math.PI;
    const forward = [Math.sin((readYaw * Math.PI) / 180), 0, Math.cos((readYaw * Math.PI) / 180)];
    const angle = signedAngle(forward, ahead);

    // bend to the next leg
    const after = wps[(cur + 1) % wps.length].pos;
    const thisLeg = sub(t, readPos);
    const nextLeg = sub(after, t);
    const bend = (len([0, 0, Math.hypot(thisLeg[0], thisLeg[2])]) && Math.hypot(nextLeg[0], nextLeg[2])) > 0
      ? Math.acos(Math.max(-1, Math.min(1, (thisLeg[0] * nextLeg[0] + thisLeg[2] * nextLeg[2]) / (Math.hypot(thisLeg[0], thisLeg[2]) * Math.hypot(nextLeg[0], nextLeg[2]) || 1)))) * 180 / Math.PI
      : 0;
    const tight = tightness(Math.max(Math.abs(angle), bend));
    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * tight;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);

    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tight);
    const steering = Math.max(-cfg.maxSteerAngle, Math.min(cfg.maxSteerAngle, angle));
    yaw += steering * steerRate * dt; // slerp with small t ~= this

    const step = v * speedFactor * dt;
    pos = [pos[0] + forward[0] * step, pos[1], pos[2] + forward[2] * step];

    // distance from the segment we're on
    const segA = wps[(cur - 1 + wps.length) % wps.length].pos;
    const segB = t;
    const ab = [segB[0] - segA[0], 0, segB[2] - segA[2]];
    const ap = [pos[0] - segA[0], 0, pos[2] - segA[2]];
    const abLen = ab[0] * ab[0] + ab[2] * ab[2];
    const tt = Math.max(0, Math.min(1, abLen ? (ap[0] * ab[0] + ap[2] * ab[2]) / abLen : 0));
    const closest = [segA[0] + ab[0] * tt, 0, segA[2] + ab[2] * tt];
    const off = Math.hypot(pos[0] - closest[0], pos[2] - closest[2]);

    log.push({ s, angle, steering, yaw, off, speedFactor, cur });

    readPos = pos.slice();
    readYaw = yaw;
  }
  return log;
}

function report(name, log) {
  const straight = log.slice(0).filter((e) => Math.abs(e.angle) < 5);
  const errs = log.map((e) => e.off);
  const maxOff = Math.max(...errs);
  const avgOff = errs.reduce((a, b) => a + b, 0) / errs.length;

  // "shimmy" = sign changes of the steering angle in the straight bits
  let flips = 0, prev = 0;
  for (const e of log) {
    if (Math.abs(e.angle) < 1) continue;
    const sgn = Math.sign(e.angle);
    if (prev !== 0 && sgn !== prev) flips++;
    prev = sgn;
  }
  console.log(`\n${name}:  lateral offset avg ${avgOff.toFixed(2)} m, max ${maxOff.toFixed(2)} m; steering sign flips ${flips} over 180 s`);
  const worst = errs.map((e, i) => ({ i, e })).sort((a, b) => b.e - a.e).slice(0, 5);
  console.log('  worst offsets at steps:', worst.map((w) => `${w.i}(${w.e.toFixed(2)}m)`).join(' '));
}

report('as written (reads the live pose)', run(false));
report('old (reads a one-step-lagged pose)', run(true));
