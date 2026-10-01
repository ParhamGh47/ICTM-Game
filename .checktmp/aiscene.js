// Reads a Unity text scene and reports the vertical spread of the passing-car waypoints,
// plus how the AICarController on the car prefabs is configured.
const fs = require('fs');
const path = require('path');

const root = process.cwd();
const scenePath = path.join(root, 'Assets/Scenes/Levels Scenes/1/Core-1.unity');
const raw = fs.readFileSync(scenePath, 'utf8').replace(/\r\n/g, '\n');

// Split the YAML stream into documents.
const docs = [];
let current = null;
for (const line of raw.split('\n')) {
  const m = line.match(/^--- !u!(\d+) &(\d+)(.*)$/);
  if (m) {
    if (current) docs.push(current);
    current = { classId: +m[1], fileId: +m[2], strip: m[3], lines: [] };
    continue;
  }
  if (current) current.lines.push(line);
}
if (current) docs.push(current);

const byFileId = new Map();
for (const d of docs) byFileId.set(d.fileId, d);

function field(doc, name) {
  for (const line of doc.lines) {
    const m = line.match(new RegExp('^\\s*' + name + ':\\s*(.*)$'));
    if (m) return m[1].trim();
  }
  return null;
}

// GameObject name -> transform fileId
const names = new Map(); // fileId of Transform/GameObject ...
const goName = new Map();
for (const d of docs) {
  if (d.classId === 1) {
    const n = field(d, 'm_Name');
    if (n !== null) goName.set(d.fileId, n);
  }
}

// Map each transform (class 4) to its GameObject and local position.
const wp = [];
for (const d of docs) {
  if (d.classId !== 4) continue;
  // find m_GameObject fileID
  let go = null;
  let idx = d.lines.findIndex((l) => /m_GameObject:/.test(l));
  if (idx >= 0) {
    const m = d.lines[idx].match(/fileID:\s*(-?\d+)/);
    if (m) go = +m[1];
  }
  const name = go !== null ? goName.get(go) : null;
  if (!name || !/^WP_\d+$/.test(name)) continue;
  let pos = null;
  const flat = d.lines.join('\n');
  const lm = flat.match(/m_LocalPosition:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}/);
  if (lm) pos = { x: +lm[1], y: +lm[2], z: +lm[3] };
  if (pos) wp.push({ name, pos });
}

console.log('waypoints found:', wp.length);
if (wp.length) {
  const ys = wp.map((w) => w.pos.y);
  const minY = Math.min(...ys), maxY = Math.max(...ys);
  console.log(`local y: min ${minY.toFixed(3)}  max ${maxY.toFixed(3)}  spread ${(maxY - minY).toFixed(3)} m`);

  // Biggest y step between consecutive waypoints in index order.
  let steps = [];
  for (let i = 1; i < wp.length; i++) {
    const d = wp[i].pos.y - wp[i - 1].pos.y;
    const dist = Math.hypot(wp[i].pos.x - wp[i - 1].pos.x, wp[i].pos.z - wp[i - 1].pos.z);
    steps.push({ i, dy: d, dist });
  }
  steps.sort((a, b) => Math.abs(b.dy) - Math.abs(a.dy));
  console.log('largest |dy| between consecutive waypoints:');
  for (const s of steps.slice(0, 8)) console.log(`  #${s.i}: dy ${s.dy.toFixed(3)} over ${s.dist.toFixed(2)} m`);

  // How close do consecutive waypoints get (the U-turns)?
  const byDist = steps.slice().sort((a, b) => a.dist - b.dist);
  console.log('closest consecutive waypoint gaps:');
  for (const s of byDist.slice(0, 10)) console.log(`  #${s.i}: ${s.dist.toFixed(2)} m`);
  const dists = steps.map((s) => s.dist).sort((a, b) => a - b);
  const median = dists[Math.floor(dists.length / 2)];
  console.log(`median gap ${median.toFixed(2)} m; below 3 m: ${dists.filter((d) => d < 3).length}; below 5 m: ${dists.filter((d) => d < 5).length}`);
}
