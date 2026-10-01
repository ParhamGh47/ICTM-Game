// Compares three steering laws over Core-1's real loop:
//   A  aim at the current waypoint, switching 3 m early   (what the game does now)
//   B  aim at the current waypoint, switching only when reached
//   C  aim at a point a fixed look-ahead along the path   (pure pursuit)
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

// arc lengths
const seg = [];
let total = 0;
for (let i = 0; i < N; i++) { const a = wps[i].pos, b = wps[(i + 1) % N].pos; const l = Math.hypot(b[0] - a[0], b[2] - a[2]); seg.push(l); total += l; }

const dt = 0.02;
const cfg = { speedKPH: 30, turnSpeed: 3, maxSteerAngle: 150, reachThreshold: 3, cornerAngle: 22, cornerFullAngle: 140, cornerSpeedFloor: 0.5, cornerBrakeRate: 1.6, cornerRecoveryRate: 0.5, cornerSteerBoost: 2 };
const speed = cfg.speedKPH / 3.6;
const sub = (a, b) => [a[0] - b[0], a[2] - b[2]];
const l2 = (a) => Math.hypot(a[0], a[1]);
const signedAngle = (fx, fz, ax, az) => (Math.atan2(fz * ax - fx * az, fx * ax + fz * az) * 180) / Math.PI;
const tightness = (d) => Math.min(1, Math.max(0, (d - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle)));

// the point `look` metres along the loop from arc position s
function pointAt(s) {
  s = ((s % total) + total) % total;
  let i = 0, acc = 0;
  while (acc + seg[i] < s) { acc += seg[i]; i = (i + 1) % N; }
  const t = seg[i] > 0.001 ? (s - acc) / seg[i] : 0;
  const a = wps[i].pos, b = wps[(i + 1) % N].pos;
  return { x: a[0] + (b[0] - a[0]) * t, z: a[2] + (b[2] - a[2]) * t, index: i };
}

// arc lengths before each waypoint
const arcBefore = [0];
for (let i = 0; i < N - 1; i++) arcBefore.push(arcBefore[i] + seg[i]);

// nearest arc position to a point, searched from a hint index
function arcOf(p, hint) {
  let best = 1e18, bestS = 0, bestI = hint;
  for (let k = -3; k <= 8; k++) {
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

function simulate(mode, lookAhead, seconds, tau, lookAheadRate, cornerL) {
  const nearL = cornerL === undefined ? 5 : cornerL;
  let pos = wps[0].pos.slice();
  let yaw = (Math.atan2(wps[0].fwd[0], wps[0].fwd[2]) * 180) / Math.PI;
  let cur = 0, speedFactor = 1, sPos = 0, steerSm = 0, Lstate = lookAhead;
  const log = [];
  for (let s = 0; s < Math.round(seconds / dt); s++) {
    // The game's own targeting: advance past every waypoint already reached (NextTarget), and never
    // take the index from a projection.
    let guard = 0;
    const threshold = mode === 'A' || mode === 'F' ? cfg.reachThreshold : 0.2;
    while (guard++ < N) {
      if (l2(sub(wps[cur].pos, pos)) >= threshold) break;
      cur = (cur + 1) % N;
    }
    const probe = arcOf(pos, cur);
    sPos = probe.s;
    const aim = { x: wps[cur].pos[0], z: wps[cur].pos[2], index: cur };
    const targetIndex = cur;

    // PointAlongPath as the C# does it: walk from the car's own position, not from a projected arc
    // position, so the harness and the game agree even when the car is a little off the line.
    function pointAlong(distance) {
      let from = [pos[0], pos[2]];
      let index = cur;
      let left = distance;
      for (let k = 0; k < N; k++) {
        const to = [wps[index].pos[0], wps[index].pos[2]];
        const leg = Math.hypot(to[0] - from[0], to[1] - from[1]);
        if (leg >= left) {
          const t = leg > 0.0001 ? left / leg : 0;
          return { x: from[0] + (to[0] - from[0]) * t, z: from[1] + (to[1] - from[1]) * t };
        }
        left -= leg;
        from = to;
        index = (index + 1) % N;
      }
      return { x: from[0], z: from[1] };
    }

    const toAim = [aim.x - pos[0], aim.z - pos[2]];
    const la = l2(toAim);
    const forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];
    const vertexAngle = la > 0.01 ? signedAngle(forward[0], forward[1], toAim[0] / la, toAim[1] / la) : 0;

    // mode E steers at a point a look-ahead along the path, but keeps today's corner reading.
    // The look-ahead shrink uses the same bend-ahead reading the speed does, so the turnaround
    // is warned about before the car is in it.
    let angle = vertexAngle;
    let tightFromPursuit = null;
    if (mode === 'E') { tightFromPursuit = tightness(Math.abs(vertexAngle)); }

    // corner tightness: how much the path bends between the leg being driven and the next
    const tPos = wps[targetIndex].pos, afterPos = wps[(targetIndex + 1) % N].pos;
    const thisLeg = sub(tPos, pos), nextLeg = sub(afterPos, tPos);
    const bend = l2(thisLeg) > 0.01 && l2(nextLeg) > 0.01
      ? (Math.acos(Math.max(-1, Math.min(1, (thisLeg[0] * nextLeg[0] + thisLeg[1] * nextLeg[1]) / (l2(thisLeg) * l2(nextLeg))))) * 180) / Math.PI : 0;
    let tight = tightness(Math.max(Math.abs(mode === 'E' || mode === 'F' ? vertexAngle : angle), bend));
    if (mode === 'E' || mode === 'F') {
      const wanted2 = lookAhead + (nearL - lookAhead) * tight;
      if (mode === 'F') {
        // rate-limit the look-ahead itself
        const maxStep = lookAheadRate * dt;
        Lstate = wanted2 < Lstate ? Math.max(wanted2, Lstate - maxStep) : Math.min(wanted2, Lstate + maxStep);
      } else Lstate = wanted2;
      const p = pointAlong(Lstate);
      const dx = p.x - pos[0], dz = p.z - pos[2];
      const dl = Math.hypot(dx, dz);
      angle = dl > 0.01 ? signedAngle(forward[0], forward[1], dx / dl, dz / dl) : 0;
      tightFromPursuit = tight;
    }
    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * tight;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);

    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tight);
    let steering = Math.max(-cfg.maxSteerAngle, Math.min(cfg.maxSteerAngle, angle));
    if (mode === 'D') {
      steerSm += (steering - steerSm) * Math.min(1, dt / tau);
      steering = steerSm;
    }
    const yawBefore = yaw;
    yaw += steering * steerRate * dt;

    const step = speed * speedFactor * dt;
    pos = [pos[0] + forward[0] * step, pos[1], pos[2] + forward[1] * step];

    log.push({ yawRate: (yaw - yawBefore) / dt, off: probe.off, aimJump: angle, s: sPos, aimDist: la, cur: targetIndex, gap: seg[targetIndex], gap2: seg[(targetIndex + 1) % N] });
  }
  return log;
}

function report(name, log) {
  const rates = log.map((e) => e.yawRate);
  const jolts = [];
  for (let i = 1; i < log.length; i++) jolts.push(Math.abs(log[i].yawRate - log[i - 1].yawRate));
  const sorted = jolts.slice().sort((a, b) => a - b);
  const off = log.map((e) => e.off).sort((a, b) => a - b);
  const at = (arr, p) => arr[Math.min(arr.length - 1, Math.floor(arr.length * p))];
  console.log(`${name}:`);
  console.log(`  yaw rate  max |${Math.max(...rates.map(Math.abs)).toFixed(0)}| deg/s`);
  console.log(`  jolt/step median ${at(sorted, 0.5).toFixed(2)}  p95 ${at(sorted, 0.95).toFixed(2)}  p99.9 ${at(sorted, 0.999).toFixed(2)}  max ${sorted[sorted.length - 1].toFixed(1)} deg/s`);
  console.log(`  lateral error mean ${(off.reduce((a, b) => a + b, 0) / off.length).toFixed(2)}  p95 ${at(off, 0.95).toFixed(2)}  max ${off[off.length - 1].toFixed(2)} m`);
}

function where(name, log) {
  const jolts = [];
  for (let i = 1; i < log.length; i++) jolts.push({ i, d: Math.abs(log[i].yawRate - log[i - 1].yawRate) });
  jolts.sort((a, b) => b.d - a.d);
  console.log(`\n${name} — 12 worst jolts, with what the car was aiming at:`);
  for (const j of jolts.slice(0, 12)) {
    const e = log[j.i];
    console.log(`  t=${(j.i * 0.02).toFixed(1)}s  jolt ${j.d.toFixed(0)} deg/s  yaw rate ${log[j.i - 1].yawRate.toFixed(0)} -> ${e.yawRate.toFixed(0)}  aim ${e.aimDist.toFixed(1)} m ahead  waypoint gaps here ${e.gap.toFixed(1)} / ${e.gap2.toFixed(1)} m`);
  }
}

console.log(`=== Core-${level}: ${wps.length} waypoints, loop ${total.toFixed(0)} m ===`);
const a = simulate('A', 0, 240, 0);
report('A  today: aim at the waypoint, switch 3 m early', a);
for (const [straight, corner, ease] of [[14, 5, 6], [12, 4, 20], [12, 4, 30], [14, 5, 30], [12, 4, 500]]) {
  report(`F  look-ahead ${straight} -> ${corner} m, eased at ${ease} m/s`, simulate('F', straight, 240, 0, ease, corner));
}
where('A', a);
