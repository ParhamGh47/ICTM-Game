// Replays AICarController's own drive loop over a level's real waypoint chain and measures how jerky the
// motion is: the jolt in yaw rate from one physics step to the next, and how far the car cuts the path.
// Then it replays it with the steer command rate-limited (and optionally the corner reading smoothed).
const fs = require('fs');
const path = require('path');

const level = process.argv[2] || '1';
const scenePath = path.join(process.cwd(), `Assets/Scenes/Levels Scenes/${level}/Core-${level}.unity`);
const raw = fs.readFileSync(scenePath, 'utf8').replace(/\r\n/g, '\n');

const docs = [];
let current = null;
for (const line of raw.split('\n')) {
  const m = line.match(/^--- !u!(\d+) &(\d+)(.*)$/);
  if (m) { if (current) docs.push(current); current = { classId: +m[1], fileId: +m[2], lines: [] }; continue; }
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
const localPos = (d) => { const m = flat(d).match(/m_LocalPosition:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}/); return m ? [+m[1], +m[2], +m[3]] : null; };
const quadOf = (d) => { const m = flat(d).match(/m_LocalRotation:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+),\s*w:\s*(-?[\d.eE+-]+)\}/); return m ? [+m[1], +m[2], +m[3], +m[4]] : null; };
const parentOf = (d) => { const m = flat(d).match(/m_Father:\s*\{fileID:\s*(-?\d+)\}/); return m ? +m[1] : 0; };
const goOf = (d) => { const m = flat(d).match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/); return m ? +m[1] : null; };
function qrot(q, v) { const [x, y, z, w] = q; const t = [2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])]; return [v[0] + w * t[0] + (y * t[2] - z * t[1]), v[1] + w * t[1] + (z * t[0] - x * t[2]), v[2] + w * t[2] + (x * t[1] - y * t[0])]; }
function qmul(a, b) { return [a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1], a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0], a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3], a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]]; }
function worldPose(fileId) { let pos = [0, 0, 0], rot = [0, 0, 0, 1], id = fileId; while (id && transforms.has(id)) { const d = transforms.get(id); pos = qrot(rot, localPos(d) || [0, 0, 0]).map((x, i) => x + pos[i]); rot = qmul(rot, quadOf(d) || [0, 0, 0, 1]); id = parentOf(d); } return { pos, rot }; }

const chains = [];
for (const d of transforms.values()) {
  const block = flat(d).match(/m_Children:([\s\S]*?)(?:\n  \w|$)/);
  if (!block) continue;
  const kids = [...block[1].matchAll(/fileID:\s*(-?\d+)/g)].map((m) => +m[1]).filter((k) => { const t = transforms.get(k); if (!t) return false; const go = goOf(t); const n = go ? goName.get(go) : null; return n && /^WP_\d+$/.test(n); });
  if (kids.length > 100) chains.push(kids);
}

const wps = chains[0].map((k) => { const w = worldPose(k); return { pos: w.pos, fwd: qrot(w.rot, [0, 0, 1]) }; });
const N = wps.length;

const cfg = {
  speedKPH: Number(process.env.KPH || 25), turnSpeed: 3, maxSteerAngle: 150, reachThreshold: 3,
  cornerAngle: 22, cornerFullAngle: 140, cornerSpeedFloor: 0.5,
  cornerBrakeRate: 1.6, cornerRecoveryRate: 0.5, cornerSteerBoost: 2,
  lookAhead: 12, cornerLookAhead: 4, lookAheadResponse: 20,
};

const dt = 0.02;
const speed = cfg.speedKPH / 3.6;
const sub2 = (a, b) => [a[0] - b[0], a[2] - b[2]];
const hlen2 = (a) => Math.hypot(a[0], a[1]);
const norm2 = (a) => { const l = hlen2(a) || 1; return [a[0] / l, a[1] / l]; };
const signedAngle = (fx, fz, ax, az) => (Math.atan2(fz * ax - fx * az, fx * ax + fz * az) * 180) / Math.PI;
const tightness = (d) => Math.min(1, Math.max(0, (d - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle)));

function simulate(opts, seconds) {
  const steerLimit = opts.steerLimit || 0;       // deg/s the steer command may move
  const tightLimit = opts.tightLimit || 0;       // per-second limit on the corner reading
  let pos = wps[0].pos.slice();
  let yaw = (Math.atan2(wps[0].fwd[0], wps[0].fwd[2]) * 180) / Math.PI;
  let cur = 0, speedFactor = 1, lookAheadNow = cfg.lookAhead, tightNow = 0, steerNow = 0;
  const log = [];

  for (let s = 0; s < Math.round(seconds / dt); s++) {
    let guard = 0;
    while (guard++ < N) {
      if (hlen2(sub2(wps[cur].pos, pos)) >= cfg.reachThreshold) break;
      cur = (cur + 1) % N;
    }
    const target = wps[cur].pos;
    const forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];

    const ahead = norm2(sub2(target, pos));
    const angle = signedAngle(forward[0], forward[1], ahead[0], ahead[1]);

    const after3 = wps[(cur + 3) % N].pos;
    const thisLeg = sub2(target, pos), nextLeg = sub2(after3, target);
    const bend = hlen2(thisLeg) > 0.01 && hlen2(nextLeg) > 0.01
      ? (Math.acos(Math.max(-1, Math.min(1, (thisLeg[0] * nextLeg[0] + thisLeg[1] * nextLeg[1]) / (hlen2(thisLeg) * hlen2(nextLeg))))) * 180) / Math.PI
      : 0;

    const raw = tightness(Math.max(Math.abs(angle), bend));

    tightNow = tightLimit > 0
      ? (raw < tightNow ? Math.max(raw, tightNow - tightLimit * dt) : Math.min(raw, tightNow + tightLimit * dt))
      : raw;

    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * raw;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);

    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tightNow);

    let steerAt;
    {
      let from = [pos[0], pos[2]];
      let index = cur;
      let left = lookAheadNow;
      for (let k = 0; k < N; k++) {
        const to = [wps[index].pos[0], wps[index].pos[2]];
        const leg = Math.hypot(to[0] - from[0], to[1] - from[1]);
        if (leg >= left) { const t = leg > 0.0001 ? left / leg : 0; steerAt = { x: from[0] + (to[0] - from[0]) * t, z: from[1] + (to[1] - from[1]) * t }; break; }
        left -= leg; from = to; index = (index + 1) % N;
      }
      if (!steerAt) steerAt = from;
    }

    const sd = norm2(sub2([steerAt.x, 0, steerAt.z], pos));
    let steer = Math.max(-cfg.maxSteerAngle, Math.min(cfg.maxSteerAngle, signedAngle(forward[0], forward[1], sd[0], sd[1])));
    const towardWaypoint = Math.max(-cfg.maxSteerAngle, Math.min(cfg.maxSteerAngle, angle));
    const bendTightness = tightNow;
    if (bendTightness > 0) steer = steer + (towardWaypoint - steer) * bendTightness;

    const steerRaw = steer;

    if (steerLimit > 0) {
      const maxStep = steerLimit * dt;
      steerNow = steer < steerNow ? Math.max(steer, steerNow - maxStep) : Math.min(steer, steerNow + maxStep);
      steer = steerNow;
    }

    const wantedLook = cfg.lookAhead + (cfg.cornerLookAhead - cfg.lookAhead) * raw;
    const stepL = cfg.lookAheadResponse * dt;
    lookAheadNow = wantedLook < lookAheadNow ? Math.max(wantedLook, lookAheadNow - stepL) : Math.min(wantedLook, lookAheadNow + stepL);

    const yawBefore = yaw;
    yaw += steer * Math.min(1, steerRate * dt);

    const step = speed * speedFactor * dt;
    pos = [pos[0] + forward[0] * step, pos[1], pos[2] + forward[1] * step];

    // lateral distance from the driven path
    let best = 1e9;
    for (let i = 0; i < N; i++) {
      const a = wps[i].pos, b = wps[(i + 1) % N].pos;
      const ab = sub2(b, a), ap = sub2(pos, a);
      const L = ab[0] * ab[0] + ab[1] * ab[1];
      const t = L ? Math.max(0, Math.min(1, (ap[0] * ab[0] + ap[1] * ab[1]) / L)) : 0;
      const d = Math.hypot(pos[0] - (a[0] + ab[0] * t), pos[2] - (a[2] + ab[1] * t));
      if (d < best) best = d;
    }

    log.push({ yawRate: (yaw - yawBefore) / dt, steer, steerRaw, off: best, cur, yaw });
  }
  return log;
}

function report(name, log) {
  const jolts = [];
  for (let i = 1; i < log.length; i++) jolts.push(Math.abs(log[i].yawRate - log[i - 1].yawRate));
  const sorted = jolts.slice().sort((a, b) => a - b);
  const at = (p) => sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))];
  const off = log.map((e) => e.off).sort((a, b) => a - b);
  const oa = (p) => off[Math.min(off.length - 1, Math.floor(off.length * p))];
  console.log(`${name.padEnd(30)} jolt/step median ${at(0.5).toFixed(2)}  p95 ${at(0.95).toFixed(2)}  p99.9 ${at(0.999).toFixed(2)}  max ${sorted[sorted.length - 1].toFixed(1)} | off path mean ${(off.reduce((a, b) => a + b, 0) / off.length).toFixed(2)} p95 ${oa(0.95).toFixed(2)} max ${off[off.length - 1].toFixed(2)} m`);
}

function spins(name, log) {
  // Total heading change that does not cancel: a car taking a hairpin cleanly turns ~180 deg there, one that
  // drives round the turn instead turns 360 or more. Counting absolute change catches it either way.
  let abs = 0, net = 0;
  for (let i = 1; i < log.length; i++) {
    const d = log[i].yaw - log[i - 1].yaw;
    abs += Math.abs(d);
    net += d;
  }
  console.log(`${name.padEnd(30)} total |yaw| ${abs.toFixed(0)} deg   net yaw ${net.toFixed(0)} deg`);
}

console.log(`=== Core-${level}: ${N} waypoints, ${cfg.speedKPH} km/h ===`);
spins('today', simulate({}, 200));
spins('steer 120 + tight 2/s', simulate({ steerLimit: 120, tightLimit: 2 }, 200));

report('today (raw steer command)', simulate({}, 200));
for (const limit of [360, 180, 120, 90, 60]) {
  report(`steer limited ${limit} deg/s`, simulate({ steerLimit: limit }, 200));
}
report('steer 120 + tight 2/s', simulate({ steerLimit: 120, tightLimit: 2 }, 200));

// where the raw run jerks
{
  const log = simulate({}, 200);
  const jolts = [];
  for (let i = 1; i < log.length; i++) jolts.push({ i, d: Math.abs(log[i].yawRate - log[i - 1].yawRate) });
  jolts.sort((a, b) => b.d - a.d);
  console.log('\nworst jolts (raw):');
  for (const j of jolts.slice(0, 8)) {
    const a = log[j.i - 1], b = log[j.i];
    console.log(`  t=${(j.i * 0.02).toFixed(1)}s  jolt ${j.d.toFixed(0)} deg/s  steer ${a.steerRaw.toFixed(1)}->${b.steerRaw.toFixed(1)} deg  yaw rate ${a.yawRate.toFixed(0)}->${b.yawRate.toFixed(0)}`);
  }
}
