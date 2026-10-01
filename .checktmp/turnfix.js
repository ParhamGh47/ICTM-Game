// Replays AICarController's Drive() law over a level's real loop with swappable steering variants and
// reports, for a full lap: how many times the car reverses its direction of travel (2 is correct - one
// per U-turn), how smooth it is (worst one-step yaw-rate jolt), and how closely it holds the path.
const fs = require('fs');
const path = require('path');

const level = process.argv[2] || '1';
const car = process.argv[3] || '206';
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
  if (kids.length > 100) chains.push({ kids });
}
const wps = chains[0].kids.map((k) => { const w = worldPose(k); return { pos: w.pos, fwd: qrot(w.rot, [0, 0, 1]) }; });
const N = wps.length;

const seg = []; let total = 0;
for (let i = 0; i < N; i++) { const a = wps[i].pos, b = wps[(i + 1) % N].pos; const l = Math.hypot(b[0] - a[0], b[2] - a[2]); seg.push(l); total += l; }
const arcBefore = [0];
for (let i = 0; i < N - 1; i++) arcBefore.push(arcBefore[i] + seg[i]);

const P = {
  '206': { turnSpeed: 3, maxSteerAngle: 150, reachThreshold: 3 },
  '911': { turnSpeed: 6, maxSteerAngle: 35, reachThreshold: 2 },
};
const base = { speedKPH: 43.26, lookAhead: 12, cornerLookAhead: 4, lookAheadResponse: 20, cornerAngle: 22, cornerFullAngle: 140, cornerSpeedFloor: 0.5, cornerBrakeRate: 1.6, cornerRecoveryRate: 0.5, cornerSteerBoost: 2, ...(P[car] || P['206']) };
const dt = 0.02;
const l2 = (a) => Math.hypot(a[0], a[1]);
const sub = (a, b) => [a[0] - b[0], a[2] - b[2]];
const signedAngle = (fx, fz, ax, az) => (Math.atan2(fz * ax - fx * az, fx * ax + fz * az) * 180) / Math.PI;
const clip = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

// True projection onto the loop: searches every segment, so the metric can be trusted even when the car
// has driven well off the part of the path it is being driven along.
function project(p) {
  let best = 1e18, bestS = 0, bestI = 0;
  for (let i = 0; i < N; i++) {
    const a = wps[i].pos, b = wps[(i + 1) % N].pos;
    const ab = sub(b, a), ap = sub(p, a);
    const L = ab[0] * ab[0] + ab[1] * ab[1];
    const t = L ? Math.max(0, Math.min(1, (ap[0] * ab[0] + ap[1] * ab[1]) / L)) : 0;
    const d = Math.hypot(p[0] - (a[0] + ab[0] * t), p[2] - (a[2] + ab[1] * t));
    if (d < best) { best = d; bestS = arcBefore[i] + seg[i] * t; bestI = i; }
  }
  return { s: bestS, off: best, index: bestI };
}

function arcOf(p, hint) {
  let best = 1e18, bestS = 0, bestI = hint;
  for (let k = -8; k <= 24; k++) {
    const i = (((hint + k) % N) + N) % N;
    const a = wps[i].pos, b = wps[(i + 1) % N].pos;
    const ab = sub(b, a), ap = sub(p, a);
    const L = ab[0] * ab[0] + ab[1] * ab[1];
    const t = L ? Math.max(0, Math.min(1, (ap[0] * ab[0] + ap[1] * ab[1]) / L)) : 0;
    const d = Math.hypot(p[0] - (a[0] + ab[0] * t), p[2] - (a[2] + ab[1] * t));
    if (d < best) { best = d; bestS = arcBefore[i] + seg[i] * t; bestI = i; }
  }
  return { s: bestS, off: best, index: bestI };
}

function tightness(cfg, d) { return Math.max(0, Math.min(1, (d - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle))); }

// variant: 'W' = aim at the waypoint (the old law), 'P' = the shipped look-ahead law,
//          'B' = look-ahead, but steer at the waypoint when the pursuit point is behind the car.
function run(variant, cfg, seconds) {
  const speed = cfg.speedKPH / 3.6;
  let pos = wps[0].pos.slice();
  let yaw = (Math.atan2(wps[0].fwd[0], wps[0].fwd[2]) * 180) / Math.PI;
  let cur = 0, speedFactor = 1, Lstate = cfg.lookAhead;
  const log = [];
  for (let s = 0; s < Math.round(seconds / dt); s++) {
    let guard = 0;
    while (guard++ < N) {
      if (l2(sub(wps[cur].pos, pos)) >= cfg.reachThreshold) break;
      cur = (cur + 1) % N;
    }
    const probe = arcOf(pos, cur);
    const target = wps[cur];
    const toTarget = [target.pos[0] - pos[0], target.pos[2] - pos[2]];
    const la = l2(toTarget);
    let forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];
    const vertexAngle = la > 0.01 ? signedAngle(forward[0], forward[1], toTarget[0] / la, toTarget[1] / la) : 0;

    const tPos = target.pos, afterPos = wps[(cur + 1) % N].pos;
    const thisLeg = sub(tPos, pos), nextLeg = sub(afterPos, tPos);
    const bend = l2(thisLeg) > 0.01 && l2(nextLeg) > 0.01
      ? (Math.acos(clip((thisLeg[0] * nextLeg[0] + thisLeg[1] * nextLeg[1]) / (l2(thisLeg) * l2(nextLeg)), -1, 1)) * 180) / Math.PI : 0;
    const tight = tightness(cfg, Math.max(Math.abs(vertexAngle), bend));

    let steerAngle = vertexAngle;
    if (variant === 'P' || variant === 'B') {
      const wantedLook = cfg.lookAhead + (cfg.cornerLookAhead - cfg.lookAhead) * tight;
      const maxStep = cfg.lookAheadResponse * dt;
      Lstate = wantedLook < Lstate ? Math.max(wantedLook, Lstate - maxStep) : Math.min(wantedLook, Lstate + maxStep);

      let from = [pos[0], pos[2]], index = cur, left = Lstate, steer = null;
      for (let k = 0; k < N; k++) {
        const to = [wps[index].pos[0], wps[index].pos[2]];
        const leg = Math.hypot(to[0] - from[0], to[1] - from[1]);
        if (leg >= left) { const t = leg > 0.0001 ? left / leg : 0; steer = { x: from[0] + (to[0] - from[0]) * t, z: from[1] + (to[1] - from[1]) * t }; break; }
        left -= leg; from = to; index = (index + 1) % N;
      }
      if (!steer) steer = { x: from[0], z: from[1] };
      const sx = steer.x - pos[0], sz = steer.z - pos[2];
      const dl = Math.hypot(sx, sz);
      const pursuit = dl > 0.01 ? signedAngle(forward[0], forward[1], sx / dl, sz / dl) : 0;
      // the fallback: a pursuit point more than 110 deg off is one the car has turned past - steering at
      // it while moving forward is what makes it circle, so it hands back to the waypoint it is driving to
      steerAngle = variant === 'B' && Math.abs(pursuit) > cfg.fallback ? vertexAngle : pursuit;
    }

    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * tight;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);

    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tight);
    const clamped = clip(steerAngle, -cfg.maxSteerAngle, cfg.maxSteerAngle);

    const yawBefore = yaw;
    yaw += clamped * Math.min(1, steerRate * dt);
    forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];

    const step = speed * speedFactor * dt;
    pos = [pos[0] + forward[0] * step, pos[1], pos[2] + forward[1] * step];

    log.push({ t: s * dt, s: probe.s, off: probe.off, yaw, cur, la, vertexAngle, steerAngle: clamped, tight, Lstate, yawRate: (yaw - yawBefore) / dt, ps: project(pos).s });
  }
  for (let i = 1; i < log.length; i++) {
    let pd = log[i].ps - log[i - 1].ps;
    if (pd > total / 2) pd -= total;
    if (pd < -total / 2) pd += total;
    log[i].pathRate = pd / dt;
  }
  log[0].pathRate = log[1] ? log[1].pathRate : 0;
  return log;
}

function metrics(name, log) {
  const rates = log.map((e) => e.yawRate);
  const jolts = [];
  for (let i = 1; i < log.length; i++) jolts.push(Math.abs(rates[i] - rates[i - 1]));
  const sorted = jolts.slice().sort((a, b) => a - b);
  const off = log.map((e) => Math.abs(e.off)).sort((a, b) => a - b);
  const at = (arr, p) => arr[Math.min(arr.length - 1, Math.floor(arr.length * p))];
  // reversals of travel direction, ignoring noise
  let sign = 0, reversals = 0;
  for (const e of log) {
    if (Math.abs(e.pathRate) < 0.5) continue;
    const sgn = Math.sign(e.pathRate);
    if (sign !== 0 && sgn !== sign) reversals++;
    sign = sgn;
  }
  // total heading change actually turned through. A healthy lap is a shuttle loop: out one way, back the
  // other, so it turns a net 360 deg and roughly 360 deg in total. An extra loop the car drives round on
  // its own shows up here as another 360, whatever it nets out to.
  let absYaw = 0;
  for (let i = 1; i < log.length; i++) { let d = log[i].yaw - log[i - 1].yaw; if (d > 180) d -= 360; if (d < -180) d += 360; absYaw += Math.abs(d); }
  // How far it travelled BACKWARDS along its own loop, and the net progress made. A car being driven
  // round the path makes ground the whole time and ends up a lap further on; a car driving in a circle
  // at a U-turn goes backwards past waypoints it has already taken.
  let back = 0, net = 0;
  for (let i = 1; i < log.length; i++) {
    let pd = log[i].ps - log[i - 1].ps;
    if (pd > total / 2) pd -= total;
    if (pd < -total / 2) pd += total;
    if (pd < 0) back += -pd;
    net += pd;
  }
  console.log(`${name}`);
  console.log(`   driven BACKWARDS along its own loop ${back.toFixed(0)} m   net progress ${net.toFixed(0)} m (one lap is ${total.toFixed(0)})   reversals ${reversals}   worst yaw rate ${Math.max(...rates.map(Math.abs)).toFixed(0)} deg/s`);
  console.log(`   jolt/step  p50 ${at(sorted, 0.5).toFixed(2)}  p95 ${at(sorted, 0.95).toFixed(2)}  p99.9 ${at(sorted, 0.999).toFixed(2)}  max ${sorted[sorted.length - 1].toFixed(0)} deg/s`);
  console.log(`   lateral error  mean ${(off.reduce((a, b) => a + b, 0) / off.length).toFixed(2)}  p95 ${at(off, 0.95).toFixed(2)}  max ${off[off.length - 1].toFixed(2)} m`);
}

const lapSeconds = total / (base.speedKPH / 3.6) + 30;

if (process.env.DUMP) {
  const variant = process.env.DUMP;
  const fallback = +(process.env.FB || 110);
  const log = run(variant, { ...base, fallback }, lapSeconds);
  // the U-turns: where the car's path doubles back. Find the biggest cumulative turn over a 5-point window.
  let worst = null;
  for (let i = 0; i < N; i++) {
    let sum = 0;
    for (let k = 0; k < 5; k++) {
      const a = wps[(i + k) % N].pos, b = wps[(i + k + 1) % N].pos, c = wps[(i + k + 2) % N].pos;
      const ab = sub(b, a), bc = sub(c, b);
      if (l2(ab) < 0.01 || l2(bc) < 0.01) continue;
      sum += (Math.acos(clip((ab[0] * bc[0] + ab[1] * bc[1]) / (l2(ab) * l2(bc)), -1, 1)) * 180) / Math.PI;
    }
    if (!worst || sum > worst.sum) worst = { i, sum, s: arcBefore[i] };
  }
  console.log(`\nDUMP variant ${variant} fallback ${fallback}: sharpest 5-point turn #${worst.i} (${worst.sum.toFixed(0)} deg) at s=${worst.s.toFixed(1)}`);
  const win = log.filter((e) => Math.abs(((e.s - worst.s + total / 2) % total) - total / 2) < 30);
  console.log('   t     s     yaw   wp    la  vertex steer  tight  LA  yawRate');
  let prev = -1;
  for (const e of win) {
    if (e.t - prev < 0.1) continue;
    prev = e.t;
    console.log(`  ${e.t.toFixed(2)}  ${e.s.toFixed(1).padStart(6)}  ${e.yaw.toFixed(0).padStart(4)}  ${String(e.cur).padStart(3)}  ${e.la.toFixed(1).padStart(5)}  ${e.vertexAngle.toFixed(0).padStart(5)}  ${e.steerAngle.toFixed(0).padStart(5)}  ${e.tight.toFixed(2)}  ${e.Lstate.toFixed(1)}  ${e.yawRate.toFixed(0)}`);
  }
  process.exit(0);
}
console.log(`=== Core-${level} / ${car}: ${N} waypoints, loop ${total.toFixed(0)} m, cruise ${base.speedKPH} km/h, lap ${lapSeconds.toFixed(0)}s ===`);
metrics('W  old law: aim at the waypoint', run('W', base, lapSeconds));
metrics('P  shipped law: look-ahead pursuit', run('P', base, lapSeconds));
for (const t of [60, 80, 90, 110, 140]) {
  metrics(`B${t}  look-ahead, waypoint when the pursuit point is >${t} deg off`, run('B', { ...base, fallback: t }, lapSeconds));
}
