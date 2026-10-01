// Replays AICarController.Drive() over a real level loop with swappable steering variants, and reports the
// thing the player actually sees at a turnaround: how many degrees the car rotates while it is in it. A
// clean U-turn is ~180 deg. A car that turns, overshoots and drives a circle scores ~540 deg.
const fs = require('fs');
const path = require('path');

const level = process.argv[2] || '1';
const car = process.argv[3] || '206';
const raw = fs.readFileSync(path.join(process.cwd(), `Assets/Scenes/Levels Scenes/${level}/Core-${level}.unity`), 'utf8').replace(/\r\n/g, '\n');
const docs = [];
let cur = null;
for (const line of raw.split('\n')) {
  const m = line.match(/^--- !u!(\d+) &(\d+)(.*)$/);
  if (m) { if (cur) docs.push(cur); cur = { classId: +m[1], fileId: +m[2], lines: [] }; continue; }
  if (cur) cur.lines.push(line);
}
if (cur) docs.push(cur);
const transforms = new Map();
for (const d of docs) if (d.classId === 4) transforms.set(d.fileId, d);
const goName = new Map();
for (const d of docs) { if (d.classId !== 1) continue; const m = d.lines.join('\n').match(/m_Name:\s*(.*)/); if (m) goName.set(d.fileId, m[1].trim()); }
const flat = (d) => d.lines.join('\n');
const localPos = (d) => { const m = flat(d).match(/m_LocalPosition:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}/); return m ? [+m[1], +m[2], +m[3]] : null; };
const quadOf = (d) => { const m = flat(d).match(/m_LocalRotation:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+),\s*w:\s*(-?[\d.eE+-]+)\}/); return m ? [+m[1], +m[2], +m[3], +m[4]] : null; };
const parentOf = (d) => { const m = flat(d).match(/m_Father:\s*\{fileID:\s*(-?\d+)\}/); return m ? +m[1] : 0; };
const goOf = (d) => { const m = flat(d).match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/); return m ? +m[1] : null; };
function qrot(q, v) { const [x, y, z, w] = q; const t = [2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])]; return [v[0] + w * t[0] + (y * t[2] - z * t[1]), v[1] + w * t[1] + (z * t[0] - x * t[2]), v[2] + w * t[2] + (x * t[1] - y * t[0])]; }
function qmul(a, b) { return [a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1], a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0], a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3], a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]]; }
function worldPose(id) { let pos = [0, 0, 0], rot = [0, 0, 0, 1]; while (id && transforms.has(id)) { const d = transforms.get(id); pos = qrot(rot, localPos(d) || [0, 0, 0]).map((x, i) => x + pos[i]); rot = qmul(rot, quadOf(d) || [0, 0, 0, 1]); id = parentOf(d); } return { pos, rot }; }

const chains = [];
for (const d of transforms.values()) {
  const block = flat(d).match(/m_Children:([\s\S]*?)(?:\n  \w|$)/);
  if (!block) continue;
  const kids = [...block[1].matchAll(/fileID:\s*(-?\d+)/g)].map((m) => +m[1]).filter((k) => { const t = transforms.get(k); if (!t) return false; const go = goOf(t); const n = go ? goName.get(go) : null; return n && /^WP_\d+$/.test(n); });
  if (kids.length > 100) chains.push(kids);
}
const wps = chains[0].map((k) => { const w = worldPose(k); return { pos: w.pos, fwd: qrot(w.rot, [0, 0, 1]) }; });
const N = wps.length;
const seg = []; let total = 0;
for (let i = 0; i < N; i++) { const a = wps[i].pos, b = wps[(i + 1) % N].pos; const l = Math.hypot(b[0] - a[0], b[2] - a[2]); seg.push(l); total += l; }
const arcBefore = [0];
for (let i = 0; i < N - 1; i++) arcBefore.push(arcBefore[i] + seg[i]);
const l2 = (a) => Math.hypot(a[0], a[1]);
const sub = (a, b) => [a[0] - b[0], a[2] - b[2]];
const clip = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const signedAngle = (fx, fz, ax, az) => (Math.atan2(fz * ax - fx * az, fx * ax + fz * az) * 180) / Math.PI;

// The real turnarounds: points where the path itself doubles back. The direction of the path a few
// waypoints earlier and a few later are opposite there, and nowhere else on the loop.
const tangent = [];
for (let i = 0; i < N; i++) { const a = wps[i].pos, b = wps[(i + 1) % N].pos; const d = sub(b, a); const m = l2(d); tangent.push(m > 0.01 ? [d[0] / m, d[1] / m] : [0, 0]); }
const turns = [];
for (let i = 0; i < N; i++) {
  const back = tangent[(i + N - 3) % N], fwd = tangent[(i + 3) % N];
  const dot = back[0] * fwd[0] + back[1] * fwd[1];
  if (dot > -0.5) continue;
  if (turns.some((t) => { let d = arcBefore[i] - t.s; if (d > total / 2) d -= total; if (d < -total / 2) d += total; return Math.abs(d) < 60; })) continue;
  turns.push({ i, s: arcBefore[i], sum: (Math.acos(clip(dot, -1, 1)) * 180) / Math.PI });
}

function project(p) {
  let best = 1e18, bestS = 0;
  for (let i = 0; i < N; i++) {
    const a = wps[i].pos, b = wps[(i + 1) % N].pos;
    const ab = sub(b, a), ap = sub(p, a);
    const L = ab[0] * ab[0] + ab[1] * ab[1];
    const t = L ? Math.max(0, Math.min(1, (ap[0] * ab[0] + ap[1] * ab[1]) / L)) : 0;
    const d = Math.hypot(p[0] - (a[0] + ab[0] * t), p[2] - (a[2] + ab[1] * t));
    if (d < best) { best = d; bestS = arcBefore[i] + seg[i] * t; }
  }
  return { s: bestS, off: best };
}

const P = { '206': { turnSpeed: 3, maxSteerAngle: 150, reachThreshold: 3 }, '911': { turnSpeed: 6, maxSteerAngle: 35, reachThreshold: 2 } };
const base = { speedKPH: 43.26, lookAhead: 12, cornerLookAhead: 4, lookAheadResponse: 20, cornerAngle: 22, cornerFullAngle: 140, cornerSpeedFloor: 0.5, cornerBrakeRate: 1.6, cornerRecoveryRate: 0.5, cornerSteerBoost: 2, tightSpan: 14, bendType: 'one', ...(P[car] || P['206']) };
const dt = 0.02;
const tightness = (cfg, d) => clip((d - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle), 0, 1);

function run(variant, cfg, seconds) {
  const speed = cfg.speedKPH / 3.6;
  let pos = wps[0].pos.slice();
  let yaw = (Math.atan2(wps[0].fwd[0], wps[0].fwd[2]) * 180) / Math.PI;
  let cw = 0, speedFactor = 1, Lstate = cfg.lookAhead;
  const log = [];
  for (let step = 0; step < Math.round(seconds / dt); step++) {
    let guard = 0;
    while (guard++ < N) { if (l2(sub(wps[cw].pos, pos)) >= cfg.reachThreshold) break; cw = (cw + 1) % N; }
    const target = wps[cw];
    const toTarget = [target.pos[0] - pos[0], target.pos[2] - pos[2]];
    const la = l2(toTarget);
    let forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];
    const vertexAngle = la > 0.01 ? signedAngle(forward[0], forward[1], toTarget[0] / la, toTarget[1] / la) : 0;

    // point `distance` metres along the loop past the car, walked from the car's own position
    function pointAlong(distance) {
      let from = [pos[0], pos[2]], index = cw, left = distance;
      for (let k = 0; k < N; k++) {
        const to = [wps[index].pos[0], wps[index].pos[2]];
        const leg = Math.hypot(to[0] - from[0], to[1] - from[1]);
        if (leg >= left) { const t = leg > 0.0001 ? left / leg : 0; return { x: from[0] + (to[0] - from[0]) * t, z: from[1] + (to[1] - from[1]) * t }; }
        left -= leg; from = to; index = (index + 1) % N;
      }
      return { x: from[0], z: from[1] };
    }

    // The bend the path takes ahead. The shipped code reads one junction only - the change of direction
    // between the leg the car is on and the next one - so a hairpin built from several waypoints only
    // reads 90 deg, which is not enough to warn it. 'three' reads across three legs instead, which sees
    // the turn from well back; 'span' measures the angle between the headings at two points a fixed
    // distance apart along the path.
    let bendAhead;
    if (cfg.bendType === 'span') {
      const here = pointAlong(0.5);
      const there = pointAlong(cfg.tightSpan);
      const a = [here.x - pos[0], here.z - pos[2]], b = [there.x - pos[0], there.z - pos[2]];
      bendAhead = l2(a) > 0.05 && l2(b) > 0.05 ? Math.abs(signedAngle(a[0] / l2(a), a[1] / l2(a), b[0] / l2(b), b[1] / l2(b))) : 0;
    } else if (cfg.bendType === 'three') {
      const a = sub(target.pos, pos);
      const b = sub(wps[(cw + 3) % N].pos, target.pos);
      bendAhead = l2(a) > 0.05 && l2(b) > 0.05 ? Math.abs(signedAngle(a[0] / l2(a), a[1] / l2(a), b[0] / l2(b), b[1] / l2(b))) : 0;
    } else {
      const tPos = target.pos, afterPos = wps[(cw + 1) % N].pos;
      const thisLeg = sub(tPos, pos), nextLeg = sub(afterPos, tPos);
      bendAhead = l2(thisLeg) > 0.01 && l2(nextLeg) > 0.01
        ? (Math.acos(clip((thisLeg[0] * nextLeg[0] + thisLeg[1] * nextLeg[1]) / (l2(thisLeg) * l2(nextLeg)), -1, 1)) * 180) / Math.PI : 0;
    }
    const tight = tightness(cfg, Math.max(Math.abs(vertexAngle), bendAhead));

    let steerAngle = vertexAngle;
    if (variant !== 'W') {
      const wantedLook = cfg.lookAhead + (cfg.cornerLookAhead - cfg.lookAhead) * tight;
      const maxStep = cfg.lookAheadResponse * dt;
      Lstate = wantedLook < Lstate ? Math.max(wantedLook, Lstate - maxStep) : Math.min(wantedLook, Lstate + maxStep);
      const p = pointAlong(Lstate);
      const sx = p.x - pos[0], sz = p.z - pos[2];
      const dl = Math.hypot(sx, sz);
      const pursuit = dl > 0.01 ? signedAngle(forward[0], forward[1], sx / dl, sz / dl) : 0;
      if (variant === 'P') steerAngle = pursuit;
      else if (variant === 'C') steerAngle = pursuit + (vertexAngle - pursuit) * tight;   // aim at the waypoint through the tightest part
      else if (variant === 'D') steerAngle = Math.abs(pursuit) > 90 ? vertexAngle : pursuit;
      else if (variant === 'E') steerAngle = bendAhead >= (cfg.bendBlend || 90) ? vertexAngle : pursuit;
      else if (variant === 'F') steerAngle = pursuit + (vertexAngle - pursuit) * tightness(cfg, bendAhead);
    }

    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * tight;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);
    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tight);
    const clamped = clip(steerAngle, -cfg.maxSteerAngle, cfg.maxSteerAngle);

    const yawBefore = yaw;
    yaw += clamped * Math.min(1, steerRate * dt);
    forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];
    const stepLen = speed * speedFactor * dt;
    pos = [pos[0] + forward[0] * stepLen, pos[1], pos[2] + forward[1] * stepLen];

    const proj = project(pos);
    log.push({ t: step * dt, s: proj.s, off: proj.off, yaw, cw, la, vertexAngle, steerAngle: clamped, tight, Lstate, speed: speedFactor, yawRate: (yaw - yawBefore) / dt });
  }
  return log;
}

function metrics(name, log) {
  const rates = log.map((e) => e.yawRate);
  const jolts = [];
  for (let i = 1; i < log.length; i++) jolts.push(Math.abs(rates[i] - rates[i - 1]));
  const sorted = jolts.slice().sort((a, b) => a - b);
  const off = log.map((e) => Math.abs(e.off)).sort((a, b) => a - b);
  const at = (arr, p) => arr[Math.min(arr.length - 1, Math.floor(arr.length * p))];

  // rotation accumulated while the car is within 25 m of each turnaround
  const perTurn = turns.map(() => 0);
  for (let i = 1; i < log.length; i++) {
    let d = log[i].yaw - log[i - 1].yaw;
    if (d > 180) d -= 360;
    if (d < -180) d += 360;
    for (let k = 0; k < turns.length; k++) {
      let delta = log[i].s - turns[k].s;
      if (delta > total / 2) delta -= total;
      if (delta < -total / 2) delta += total;
      if (Math.abs(delta) < 20) perTurn[k] += d;
    }
  }
  console.log(`${name}`);
  console.log(`   rotation in the turnarounds: ${perTurn.map((v) => `${v.toFixed(0)} deg`).join(', ')}   (a clean U-turn is ~180-200)`);
  console.log(`   jolt/step  p50 ${at(sorted, 0.5).toFixed(2)}  p95 ${at(sorted, 0.95).toFixed(2)}  p99.9 ${at(sorted, 0.999).toFixed(2)}  max ${sorted[sorted.length - 1].toFixed(0)} deg/s`);
  console.log(`   lateral error  mean ${(off.reduce((a, b) => a + b, 0) / off.length).toFixed(2)}  p95 ${at(off, 0.95).toFixed(2)}  max ${off[off.length - 1].toFixed(2)} m`);
  console.log(`   average speed ${(100 * log.reduce((a, e) => a + e.speed, 0) / log.length).toFixed(0)}% of cruise   time spent under 60% ${(100 * log.filter((e) => e.speed < 0.6).length / log.length).toFixed(1)}%`);
}

console.log(`=== Core-${level} / ${car}: ${N} waypoints, loop ${total.toFixed(0)} m, turnarounds at s = ${turns.map((t) => t.s.toFixed(0) + ' (' + t.sum.toFixed(0) + ' deg)').join(', ')} ===`);
const lap = total / (base.speedKPH / 3.6) + 40;
metrics('W  old law: aim at the waypoint', run('W', { ...base, tightSpan: 0 }, lap));
metrics('P  shipped: look-ahead, one-waypoint bend', run('P', { ...base, tightSpan: 0 }, lap));
if (process.env.QUICK) {
  metrics('F  three-leg bend + blend to the waypoint', run('F', { ...base, bendType: 'three' }, lap));
  process.exit(0);
}
for (const span of [8, 10, 14, 20]) metrics(`C  span ${span} m + blend to the waypoint`, run('C', { ...base, bendType: 'span', tightSpan: span }, lap));
metrics('F  three-leg bend + blend to the waypoint', run('F', { ...base, bendType: 'three' }, lap));
metrics('F  span 10 m + blend', run('F', { ...base, bendType: 'span', tightSpan: 10 }, lap));
for (const b of [70, 90, 110]) metrics(`E  three-leg bend: waypoint once it bends ${b} deg`, run('E', { ...base, bendType: 'three', bendBlend: b }, lap));
for (const b of [70, 90, 110]) metrics(`E  span 10 m: waypoint once it bends ${b} deg`, run('E', { ...base, bendType: 'span', tightSpan: 10, bendBlend: b }, lap));

if (process.env.DUMP) {
  const variant = process.env.DUMP;
  const log = run(variant, { ...base, bendBlend: +(process.env.BB || 90) }, lap);
  for (const t of turns) {
    const win = log.filter((e) => { let d = e.s - t.s; if (d > total / 2) d -= total; if (d < -total / 2) d += total; return Math.abs(d) < 22; });
    if (!win.length) continue;
    const ys = win.map((e) => e.yaw);
    let swept = 0;
    for (let i = 1; i < win.length; i++) { let d = win[i].yaw - win[i - 1].yaw; if (d > 180) d -= 360; if (d < -180) d += 360; swept += d; }
    console.log(`\nU-turn s=${t.s.toFixed(0)}: swept ${swept.toFixed(0)} deg over ${win.length} frames`);
    let prev = -1;
    for (const e of win) { if (e.t - prev < 0.15) continue; prev = e.t; console.log(`   t=${e.t.toFixed(2)} s=${e.s.toFixed(1)} yaw=${e.yaw.toFixed(0)} wp=${e.cw} la=${e.la.toFixed(1)} vertex=${e.vertexAngle.toFixed(0)} steer=${e.steerAngle.toFixed(0)} tight=${e.tight.toFixed(2)} LA=${e.Lstate.toFixed(1)}`); }
  }
}
