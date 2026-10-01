// Replays AICarController's exact Drive() law over a level's real waypoint loop and reports what
// happens at the loop's U-turns: how the car's heading, target and look-ahead behave, and how many
// times its direction of travel reverses (a healthy lap reverses once per U-turn).
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
  if (kids.length > 100) chains.push({ kids, name: goName.get(goOf(d)) });
}
if (!chains.length) { console.log('no waypoint chain found'); process.exit(1); }
const chain = chains[0];
const wps = chain.kids.map((k) => { const w = worldPose(k); return { pos: w.pos, fwd: qrot(w.rot, [0, 0, 1]) }; });
const N = wps.length;

const seg = []; let total = 0;
for (let i = 0; i < N; i++) { const a = wps[i].pos, b = wps[(i + 1) % N].pos; const l = Math.hypot(b[0] - a[0], b[2] - a[2]); seg.push(l); total += l; }
const arcBefore = [0];
for (let i = 0; i < N - 1; i++) arcBefore.push(arcBefore[i] + seg[i]);

// params straight out of the prefabs
const P = {
  '206': { turnSpeed: 3, maxSteerAngle: 150, reachThreshold: 3, speedFactorFloor: 0.5 },
  '911': { turnSpeed: 6, maxSteerAngle: 35, reachThreshold: 2, speedFactorFloor: 0.5 },
};
const param = P[car] || P['206'];
const cfg = { speedKPH: 43.26, lookAhead: 12, cornerLookAhead: 4, lookAheadResponse: 20, cornerAngle: 22, cornerFullAngle: 140, cornerSpeedFloor: 0.5, cornerBrakeRate: 1.6, cornerRecoveryRate: 0.5, cornerSteerBoost: 2, ...param };
const speed = cfg.speedKPH / 3.6;
const dt = 0.02;
const l2 = (a) => Math.hypot(a[0], a[1]);
const sub = (a, b) => [a[0] - b[0], a[2] - b[2]];
const signedAngle = (fx, fz, ax, az) => (Math.atan2(fz * ax - fx * az, fx * ax + fz * az) * 180) / Math.PI;
const tightness = (d) => Math.max(0, Math.min(1, (d - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle)));

function arcOf(p, hint) {
  let best = 1e18, bestS = 0, bestI = hint;
  for (let k = -3; k <= 12; k++) {
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

const clip = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

function run(seconds) {
  let pos = [wps[0].pos[0], wps[0].pos[1], wps[0].pos[2]];
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
    const forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];
    const vertexAngle = la > 0.01 ? signedAngle(forward[0], forward[1], toTarget[0] / la, toTarget[1] / la) : 0;

    const tPos = target.pos, afterPos = wps[(cur + 1) % N].pos;
    const thisLeg = sub(tPos, pos), nextLeg = sub(afterPos, tPos);
    const bend = l2(thisLeg) > 0.01 && l2(nextLeg) > 0.01
      ? (Math.acos(clip((thisLeg[0] * nextLeg[0] + thisLeg[1] * nextLeg[1]) / (l2(thisLeg) * l2(nextLeg)), -1, 1)) * 180) / Math.PI : 0;
    const tight = tightness(Math.max(Math.abs(vertexAngle), bend));

    const wantedLook = cfg.lookAhead + (cfg.cornerLookAhead - cfg.lookAhead) * tight;
    const maxStep = cfg.lookAheadResponse * dt;
    Lstate = wantedLook < Lstate ? Math.max(wantedLook, Lstate - maxStep) : Math.min(wantedLook, Lstate + maxStep);

    // PointAlongPath
    let from = [pos[0], pos[2]], index = cur, left = Lstate, steer;
    for (let k = 0; k < N; k++) {
      const to = [wps[index].pos[0], wps[index].pos[2]];
      const leg = Math.hypot(to[0] - from[0], to[1] - from[1]);
      if (leg >= left) { const t = leg > 0.0001 ? left / leg : 0; steer = { x: from[0] + (to[0] - from[0]) * t, z: from[1] + (to[1] - from[1]) * t }; break; }
      left -= leg; from = to; index = (index + 1) % N;
    }
    if (!steer) steer = { x: from[0], z: from[1] };
    let sx = steer.x - pos[0], sz = steer.z - pos[2];
    let dl = Math.hypot(sx, sz);
    let steerAngle = dl > 0.01 ? signedAngle(forward[0], forward[1], sx / dl, sz / dl) : 0;

    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * tight;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);

    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tight);
    const steerClamped = clip(steerAngle, -cfg.maxSteerAngle, cfg.maxSteerAngle);

    const yawBefore = yaw;
    // Unity: Slerp(rb.rotation, rb.rotation * steer, steerRate*dt) => yaw += steer * (steerRate*dt)
    yaw += steerClamped * Math.min(1, steerRate * dt);
    forward[0] = Math.sin((yaw * Math.PI) / 180); forward[1] = Math.cos((yaw * Math.PI) / 180);

    const step = speed * speedFactor * dt;
    pos = [pos[0] + forward[0] * step, pos[1], pos[2] + forward[1] * step];

    // is the waypoint the car is driving at BEHIND it?
    const behind = la > 0.01 && vertexAngle !== 0 && Math.abs(vertexAngle) > 90;
    log.push({ t: s * dt, s: probe.s, off: probe.off, yaw, cur, la, vertexAngle, steerAngle: steerClamped, tight, Lstate, steerRate, behind, pathRate: 0 });
  }
  for (let i = 1; i < log.length; i++) {
    let d = log[i].s - log[i - 1].s;
    if (d > total / 2) d -= total;
    if (d < -total / 2) d += total;
    log[i].pathRate = d / dt;
  }
  return log;
}

const log = run(820);
console.log(`=== Core-${level} / ${car}: ${N} waypoints, loop ${total.toFixed(0)} m, cruise ${cfg.speedKPH} km/h ===`);
console.log(`reachThreshold ${cfg.reachThreshold}, turnSpeed ${cfg.turnSpeed}, maxSteerAngle ${cfg.maxSteerAngle}`);

// where are the U-turns? the segments with the biggest bends
const bends = [];
for (let i = 0; i < N; i++) {
  const a = wps[(i + N - 1) % N].pos, b = wps[i].pos, c = wps[(i + 1) % N].pos;
  const ab = sub(b, a), bc = sub(c, b);
  if (l2(ab) < 0.01 || l2(bc) < 0.01) continue;
  bends.push({ i, deg: (Math.acos(clip((ab[0] * bc[0] + ab[1] * bc[1]) / (l2(ab) * l2(bc)), -1, 1)) * 180) / Math.PI, s: arcBefore[i] });
}
bends.sort((x, y) => y.deg - x.deg);
console.log('sharpest bends (waypoint index / degrees / arc m):');
for (const b of bends.slice(0, 10)) console.log(`  #${b.i}  ${b.deg.toFixed(0)} deg  s=${b.s.toFixed(1)}  gaps ${seg[(b.i + N - 1) % N].toFixed(1)} / ${seg[b.i].toFixed(1)} m`);

// direction-of-travel reversals: pathRate sign flips that are not noise
const reversals = [];
let sign = 0;
for (let i = 0; i < log.length; i++) {
  const r = log[i].pathRate;
  if (Math.abs(r) < 0.5) continue;
  const sgn = Math.sign(r);
  if (sign !== 0 && sgn !== sign) reversals.push(log[i].t);
  sign = sgn;
}
console.log(`\ntravel-direction reversals in ${log[log.length - 1].t.toFixed(0)}s: ${reversals.length} at t = ${reversals.map((t) => t.toFixed(1)).join(', ')}`);

const behindFrames = log.filter((e) => e.behind);
console.log(`frames driving at a waypoint BEHIND it (>90 deg off): ${behindFrames.length} (${(100 * behindFrames.length / log.length).toFixed(1)}%)`);

// show the sharpest U-turn in detail
const worst = bends[0];
const inWindow = log.filter((e) => Math.abs(((e.s - worst.s + total / 2) % total) - total / 2) < 25);
let prev = null;
console.log(`\ntimeline within 25 m of the sharpest bend (s=${worst.s.toFixed(1)}, #${worst.i}):`);
console.log('   t     s     yaw   wp    la  vertex steer  tight  LA');
for (const e of inWindow) {
  if (prev !== null && e.t - prev < 0.1) continue;
  prev = e.t;
  console.log(`  ${e.t.toFixed(2)}  ${e.s.toFixed(1).padStart(5)}  ${e.yaw.toFixed(0).padStart(4)}  ${String(e.cur).padStart(3)}  ${e.la.toFixed(1).padStart(5)}  ${e.vertexAngle.toFixed(0).padStart(5)}  ${e.steerAngle.toFixed(0).padStart(5)}  ${e.tight.toFixed(2)}  ${e.Lstate.toFixed(1)}`);
}
