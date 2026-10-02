// Reports the passing-car waypoint chains in a level: how many, how long, spacing, and where the sharp
// bends are. Then replays the controller's own drive loop around the sharpest bend for the raw vs current
// settings, measuring how much rotation the car spends there (a clean turnaround is ~180 deg).
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

// chains = children lists whose members are named WP_n, plus each root's own name
const chains = [];
for (const d of transforms.values()) {
  const block = flat(d).match(/m_Children:([\s\S]*?)(?:\n  \w|$)/);
  if (!block) continue;
  const kids = [...block[1].matchAll(/fileID:\s*(-?\d+)/g)].map((m) => +m[1]).filter((k) => { const t = transforms.get(k); if (!t) return false; const go = goOf(t); const n = go ? goName.get(go) : null; return n && /^WP_\d+$/.test(n); });
  if (kids.length > 20) { const go = goOf(d); chains.push({ name: go ? goName.get(go) : '?', kids, id: d.fileId }); }
}
chains.sort((a, b) => b.kids.length - a.kids.length);
console.log(`=== Core-${level}: ${chains.length} waypoint chain(s) with >20 waypoints ===`);
for (const c of chains.slice(0, 6)) {
  const w = c.kids.map((k) => worldPose(k).pos);
  let len = 0, gaps = [];
  for (let i = 0; i < w.length; i++) { const j = (i + 1) % w.length; const g = Math.hypot(w[j][0] - w[i][0], w[j][2] - w[i][2]); len += g; gaps.push(g); }
  gaps.sort((a, b) => a - b);
  console.log(`  ${c.name.padEnd(28)} ${String(w.length).padStart(4)} wps  len ${len.toFixed(0)} m  gap med ${gaps[gaps.length >> 1].toFixed(1)} min ${gaps[0].toFixed(2)} max ${gaps[gaps.length - 1].toFixed(1)}`);
}

// sharpest 3-leg bends in the biggest chain
const chain = chains[0];
const wps = chain.kids.map((k) => { const wp = worldPose(k); return { pos: wp.pos, fwd: qrot(wp.rot, [0, 0, 1]) }; });
const N = wps.length;
const sub2 = (a, b) => [a[0] - b[0], a[2] - b[2]];
const hlen2 = (a) => Math.hypot(a[0], a[1]);
const angBetween = (a, b) => (Math.acos(Math.max(-1, Math.min(1, (a[0] * b[0] + a[1] * b[1]) / ((hlen2(a) || 1) * (hlen2(b) || 1))))) * 180) / Math.PI;
const bends = [];
for (let i = 0; i < N; i++) {
  const a = wps[i].pos, b = wps[(i + 1) % N].pos, c = wps[(i + 4) % N].pos;
  bends.push({ i, d: angBetween(sub2(b, a), sub2(c, b)) });
}
bends.sort((a, b) => b.d - a.d);
console.log('  sharpest 3-leg bends:');
for (const b of bends.slice(0, 5)) {
  const gaps = [];
  for (let k = 0; k < 5; k++) gaps.push(hlen2(sub2(wps[(b.i + k + 1) % N].pos, wps[(b.i + k) % N].pos)).toFixed(1));
  console.log(`    WP_${b.i}  ${b.d.toFixed(0)} deg  next gaps ${gaps.join(',')} m`);
}

// Replay the drive with/without the two easing knobs, and dump the steering through the sharpest bend.
const cfg = {
  speedKPH: Number(process.env.KPH || 25), turnSpeed: 3, maxSteerAngle: 150, reachThreshold: 3,
  cornerAngle: 22, cornerFullAngle: 140, cornerSpeedFloor: 0.5,
  cornerBrakeRate: 1.6, cornerRecoveryRate: 0.5, cornerSteerBoost: 2,
  lookAhead: 12, cornerLookAhead: 4, lookAheadResponse: 20,
};
const dt = 0.02, speed = cfg.speedKPH / 3.6, hair = bends[0].i;
const norm2 = (a) => { const l = hlen2(a) || 1; return [a[0] / l, a[1] / l]; };
const signedAngle = (fx, fz, ax, az) => (Math.atan2(fz * ax - fx * az, fx * ax + fz * az) * 180) / Math.PI;
const tightness = (d) => Math.max(0, Math.min(1, (d - cfg.cornerAngle) / (cfg.cornerFullAngle - cfg.cornerAngle)));

function simulate(opts, seconds) {
  const steerLimit = opts.steerLimit || 0, tightLimit = opts.tightLimit || 0, rawBend = !!opts.rawBend;
  const easeLookOnly = !!opts.easeLookOnly;
  let pos = wps[0].pos.slice();
  let yaw = (Math.atan2(wps[0].fwd[0], wps[0].fwd[2]) * 180) / Math.PI;
  let cur = 0, speedFactor = 1, lookAheadNow = cfg.lookAhead, tightNow = 0, steerNow = 0;
  const log = [];
  for (let s = 0; s < Math.round(seconds / dt); s++) {
    let guard = 0;
    while (guard++ < N) { if (hlen2(sub2(wps[cur].pos, pos)) >= cfg.reachThreshold) break; cur = (cur + 1) % N; }
    const target = wps[cur].pos;
    const forward = [Math.sin((yaw * Math.PI) / 180), Math.cos((yaw * Math.PI) / 180)];
    const angle = signedAngle(forward[0], forward[1], norm2(sub2(target, pos))[0], norm2(sub2(target, pos))[1]);
    const after3 = wps[(cur + 3) % N].pos;
    const thisLeg = sub2(target, pos), nextLeg = sub2(after3, target);
    const bend = hlen2(thisLeg) > 0.01 && hlen2(nextLeg) > 0.01 ? angBetween(thisLeg, nextLeg) : 0;
    const rawTight = tightness(Math.max(Math.abs(angle), bend));
    tightNow = tightLimit > 0 ? (rawTight < tightNow ? Math.max(rawTight, tightNow - tightLimit * dt) : Math.min(rawTight, tightNow + tightLimit * dt)) : rawTight;
    const wanted = 1 + (cfg.cornerSpeedFloor - 1) * rawTight;
    const rate = (wanted < speedFactor ? cfg.cornerBrakeRate : cfg.cornerRecoveryRate) * dt;
    speedFactor = wanted < speedFactor ? Math.max(wanted, speedFactor - rate) : Math.min(wanted, speedFactor + rate);
    const steerRate = cfg.turnSpeed * (1 + (cfg.cornerSteerBoost - 1) * tightNow);
    let steerAt;
    { let from = [pos[0], pos[2]], index = cur, left = lookAheadNow;
      for (let k = 0; k < N; k++) { const to = [wps[index].pos[0], wps[index].pos[2]]; const leg = Math.hypot(to[0] - from[0], to[1] - from[1]);
        if (leg >= left) { const t = leg > 0.0001 ? left / leg : 0; steerAt = { x: from[0] + (to[0] - from[0]) * t, z: from[1] + (to[1] - from[1]) * t }; break; }
        left -= leg; from = to; index = (index + 1) % N; }
      if (!steerAt) steerAt = from; }
    const sd = norm2(sub2([steerAt.x, 0, steerAt.z], pos));
    let steerLook = Math.max(-cfg.maxSteerAngle, Math.min(cfg.maxSteerAngle, signedAngle(forward[0], forward[1], sd[0], sd[1])));
    const towardWaypoint = Math.max(-cfg.maxSteerAngle, Math.min(cfg.maxSteerAngle, angle));
    let steer;
    let bendTightness;
    if (opts.boostGate) {
      // Today's command chain, except the ease is allowed to move faster the sharper the path's bend is: on the
      // road it stays slow and smooths the junction steps, at a turnaround it is fast enough to follow the
      // command and the turn converges. Only the rate changes, so the output stays continuous.
      let cmd = steerLook;
      const bt = tightNow;
      if (bt > 0) cmd = cmd + (towardWaypoint - cmd) * bt;
      const [lo, hi, boost] = opts.boostGate;
      let s = (bend - lo) / (hi - lo);
      s = Math.max(0, Math.min(1, s)); s = s * s * (3 - 2 * s);
      const allowed = steerLimit * (1 + (boost - 1) * s);
      const maxStep = allowed * dt;
      steerNow = cmd < steerNow ? Math.max(cmd, steerNow - maxStep) : Math.min(cmd, steerNow + maxStep);
      steer = steerNow;
    } else if (opts.gate) {
      // Pre-regression: blend toward the waypoint with the raw bend reading, no easing (the version that took
      // turnarounds cleanly).
      let rawCmd = steerLook;
      const bt = tightness(bend);
      if (bt > 0) rawCmd = rawCmd + (towardWaypoint - rawCmd) * bt;
      // Eased: today's version.
      let eased = steerLook;
      const bt2 = tightNow;
      if (bt2 > 0) eased = eased + (towardWaypoint - eased) * bt2;
      if (steerLimit > 0) { const maxStep = steerLimit * dt; steerNow = eased < steerNow ? Math.max(eased, steerNow - maxStep) : Math.min(eased, steerNow + maxStep); eased = steerNow; }
      // Choose between them by how sharp the path's bend is: on ordinary road the eased command, at a
      // turnaround the raw one, so the turn converges instead of lagging.
      const [lo, hi] = opts.gate;
      let g = (bend - lo) / (hi - lo);
      g = Math.max(0, Math.min(1, g)); g = g * g * (3 - 2 * g);
      steer = eased + (rawCmd - eased) * g;
      if (opts.report) opts.report.push({ bend, g });
    } else if (opts.snapGate) {
      // Exactly today's road behaviour - blend by the eased corner reading, then ease the output - and on top
      // of it, at a genuine turnaround the eased state is snapped straight to the waypoint command so the turn
      // follows its command instead of lagging behind it.
      let cmd = steerLook;
      if (tightNow > 0) cmd = cmd + (towardWaypoint - cmd) * tightNow;
      if (bend >= opts.snapGate) steerNow = towardWaypoint;
      else if (steerLimit > 0) { const maxStep = steerLimit * dt; steerNow = cmd < steerNow ? Math.max(cmd, steerNow - maxStep) : Math.min(cmd, steerNow + maxStep); }
      else steerNow = cmd;
      steer = steerNow;
    } else if (opts.hardGate) {
      // The waypoint fallback only for a genuine turnaround, nothing at all on the road, and no ease: the
      // pure look-ahead command is already smooth, so there is nothing to lag the turn with.
      if (steerLimit > 0) { const maxStep = steerLimit * dt; steerNow = steerLook < steerNow ? Math.max(steerLook, steerNow - maxStep) : Math.min(steerLook, steerNow + maxStep); steerLook = steerNow; }
      steer = steerLook;
      if (bend >= opts.hardGate) steer = towardWaypoint;
    } else if (easeLookOnly) {
      // Ease only the path-following command, then blend toward the waypoint with the raw bend reading, so a
      // turnaround follows its command exactly instead of lagging behind it.
      if (steerLimit > 0) { const maxStep = steerLimit * dt; steerNow = steerLook < steerNow ? Math.max(steerLook, steerNow - maxStep) : Math.min(steerLook, steerNow + maxStep); steerLook = steerNow; }
      steer = steerLook;
      if (opts.lookGate) {
        const [lo, hi] = opts.lookGate;
        let g = (bend - lo) / (hi - lo); g = Math.max(0, Math.min(1, g)); bendTightness = g * g * (3 - 2 * g);
      } else bendTightness = tightness(bend);
      if (bendTightness > 0) steer = steer + (towardWaypoint - steer) * bendTightness;
    } else {
      steer = steerLook;
      bendTightness = rawBend ? tightness(bend) : tightNow;
      if (bendTightness > 0) steer = steer + (towardWaypoint - steer) * bendTightness;
      if (steerLimit > 0) { const maxStep = steerLimit * dt; steerNow = steer < steerNow ? Math.max(steer, steerNow - maxStep) : Math.min(steer, steerNow + maxStep); steer = steerNow; }
    }
    const steerRaw = steer;
    const wantedLook = cfg.lookAhead + (cfg.cornerLookAhead - cfg.lookAhead) * rawTight;
    const stepL = cfg.lookAheadResponse * dt;
    lookAheadNow = wantedLook < lookAheadNow ? Math.max(wantedLook, lookAheadNow - stepL) : Math.min(wantedLook, lookAheadNow + stepL);
    yaw += steer * Math.min(1, steerRate * dt);
    const step = speed * speedFactor * dt;
    pos = [pos[0] + forward[0] * step, pos[1], pos[2] + forward[1] * step];
    log.push({ cur, yaw, steer, steerRaw, bend, rawTight, tightNow, speedFactor, pos });
  }
  return log;
}

const near = (c) => { const d = ((c - hair) % N + N) % N; return d <= 30 || d >= N - 15; };
function turnThrough(name, log) {
  let abs = 0, net = 0, seen = new Set();
  for (let i = 1; i < log.length; i++) {
    if (!near(log[i].cur)) continue;
    const d = log[i].yaw - log[i - 1].yaw; abs += Math.abs(d); net += d;
  }
  console.log(`  ${name.padEnd(28)} turn region |yaw| ${abs.toFixed(0)}  net ${net.toFixed(0)}`);
}

function jolt(name, log) {
  const c = [];
  for (let i = 2; i < log.length; i++) {
    const r = [log[i - 2], log[i - 1], log[i]];
    if (near(r[0].cur) || near(r[2].cur)) continue;
    c.push(Math.abs((r[2].yaw - r[1].yaw) - (r[1].yaw - r[0].yaw)) / dt);
  }
  const s = c.sort((a, b) => a - b);
  const at = (p) => s[Math.min(s.length - 1, Math.floor(s.length * p))];
  console.log(`  ${name.padEnd(28)} road yaw-accel median ${at(0.5).toFixed(2)}  p99 ${at(0.99).toFixed(2)}  p99.9 ${at(0.999).toFixed(2)}  max ${s[s.length - 1].toFixed(1)} deg/s^2`);
}

function where(name, log) {
  const c = [];
  for (let i = 2; i < log.length; i++) {
    const r = [log[i - 2], log[i - 1], log[i]];
    if (near(r[0].cur) || near(r[2].cur)) continue;
    c.push({ v: Math.abs((r[2].yaw - r[1].yaw) - (r[1].yaw - r[0].yaw)) / dt, a: r[1], b: r[2] });
  }
  c.sort((x, y) => y.v - x.v);
  console.log(`  ${name} worst road yaw-accel:`);
  for (const s of c.slice(0, 5)) console.log(`     ${s.v.toFixed(0).padStart(5)} deg/s^2  WP_${s.b.cur}  bend ${s.b.bend.toFixed(0)}  tight ${s.b.tightNow.toFixed(2)}  steer ${s.a.steer.toFixed(1)}->${s.b.steer.toFixed(1)}`);
}

const SECS = Number(process.env.SECS || 1500);
console.log(`\n  chain ${N} wps, hairpin WP_${hair}, running ${SECS}s`);
jolt('raw', simulate({}, SECS));
jolt('current (steer120+tight2)', simulate({ steerLimit: 120, tightLimit: 2 }, SECS));
jolt('snap 95', simulate({ snapGate: 95, steerLimit: 120 }, SECS));
jolt('snap 110', simulate({ snapGate: 110, steerLimit: 120 }, SECS));
console.log('');
where('raw', simulate({}, SECS));
where('current', simulate({ steerLimit: 120, tightLimit: 2 }, SECS));
where('look gate 45-110', simulate({ steerLimit: 120, easeLookOnly: true, lookGate: [45, 110] }, SECS));
console.log('');
turnThrough('raw (pre-regression)', simulate({}, SECS));
turnThrough('current (steer120 + tight2)', simulate({ steerLimit: 120, tightLimit: 2 }, SECS));
for (const thr of [80, 95, 110, 125]) turnThrough(`snap gate ${thr}`, simulate({ snapGate: thr, steerLimit: 120 }, SECS));

const alog = simulate({}, SECS), blog = simulate({ steerLimit: 120, tightLimit: 2 }, SECS);
console.log('\n   cur  |  raw: bend tight steer   yaw |  current: bend tight steer   yaw');
for (let i = 1; i < alog.length; i++) {
  const c = alog[i].cur;
  if (!near(c)) continue;
  if (i % 25 !== 0) continue;
  const a = alog[i], b = blog[i];
  console.log(`  ${String(c).padStart(4)} | ${a.bend.toFixed(0).padStart(4)} ${a.rawTight.toFixed(2)} ${a.steer.toFixed(1).padStart(6)} ${a.yaw.toFixed(0).padStart(7)} | ${b.bend.toFixed(0).padStart(4)} ${b.tightNow.toFixed(2)} ${b.steer.toFixed(1).padStart(6)} ${b.yaw.toFixed(0).padStart(7)}`);
}

// sanity: the eased look must not step more than steerLimit*dt
{
  const log = simulate({ steerLimit: 120, easeLookOnly: true, lookGate: [45, 110] }, SECS);
  let mx = 0, at = null;
  for (let i = 1; i < log.length; i++) { const d = Math.abs(log[i].steer - log[i - 1].steer); if (d > mx) { mx = d; at = log[i]; } }
  console.log(`\n  look-gate sanity: max |d steer| ${mx.toFixed(3)} deg (limit ${(120 * dt).toFixed(2)})  at WP_${at.cur} bend ${at.bend.toFixed(0)} tight ${at.rawTight.toFixed(2)}`);
  const log2 = simulate({ steerLimit: 120, tightLimit: 2 }, SECS);
  let mx2 = 0, at2 = null;
  for (let i = 1; i < log2.length; i++) { const d = Math.abs(log2[i].steer - log2[i - 1].steer); if (d > mx2) { mx2 = d; at2 = log2[i]; } }
  console.log(`  current   sanity: max |d steer| ${mx2.toFixed(3)} deg  at WP_${at2.cur} bend ${at2.bend.toFixed(0)} tight ${at2.tightNow.toFixed(2)}`);
}

// probe: every steer step above 5 deg, with the blend factor in force
{
  const log = simulate({ steerLimit: 120, easeLookOnly: true, lookGate: [45, 110] }, SECS);
  let n = 0;
  for (let i = 1; i < log.length && n < 12; i++) {
    const d = Math.abs(log[i].steer - log[i - 1].steer);
    if (d > 5) { n++; console.log(`  step ${d.toFixed(1)} deg at WP_${log[i].cur} bend ${log[i].bend.toFixed(0)} rawTight ${log[i].rawTight.toFixed(2)} tight ${log[i].tightNow.toFixed(2)}`); }
  }
  console.log(`  total steps >5 deg: ${n}`);
}

// exact sequence around the WP_31 step
{
  const log = simulate({ steerLimit: 120, easeLookOnly: true, lookGate: [45, 110] }, SECS);
  for (let i = 1; i < log.length; i++) {
    if (Math.abs(log[i].steer - log[i - 1].steer) > 5 && log[i].cur < 40) {
      for (let k = i - 3; k <= i + 2; k++) {
        if (k < 0) continue;
        console.log(`   i=${k} cur=${log[k].cur} bend=${log[k].bend.toFixed(1)} rawTight=${log[k].rawTight.toFixed(2)} steer=${log[k].steer.toFixed(2)}`);
      }
      console.log('   ---');
      break;
    }
  }
}
where('hard gate 90', simulate({ hardGate: 90 }, SECS));
where('hard gate 110 + ease300', simulate({ hardGate: 110, steerLimit: 300 }, SECS));
where('snap 110', simulate({ snapGate: 110, steerLimit: 120 }, SECS));
where('snap 95', simulate({ snapGate: 95, steerLimit: 120 }, SECS));
where('snap2 110', simulate({ snapGate: 110, steerLimit: 120 }, SECS));
where('snap2 125', simulate({ snapGate: 125, steerLimit: 120 }, SECS));
