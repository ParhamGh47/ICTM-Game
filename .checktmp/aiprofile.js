// Orders the passing-car waypoints by their parent's child list and reports the vertical
// profile in driving order (this is what a car actually drives over).
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

function num(line, key) {
  const m = line && line.match(new RegExp(key + ':\\s*(-?[\\d.eE+-]+)'));
  return m ? +m[1] : null;
}

function localPos(d) {
  const flat = d.lines.join('\n');
  const m = flat.match(/m_LocalPosition:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}/);
  return m ? { x: +m[1], y: +m[2], z: +m[3] } : null;
}

function parent(d) {
  const flat = d.lines.join('\n');
  const m = flat.match(/m_Father:\s*\{fileID:\s*(-?\d+)\}/);
  return m ? +m[1] : null;
}

// Every WP transform, grouped by parent.
const groups = new Map();
for (const d of transforms.values()) {
  const go = (d.lines.join('\n').match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1];
  const name = go ? goName.get(+go) : null;
  if (!name || !/^WP_\d+$/.test(name)) continue;
  const p = parent(d);
  if (!groups.has(p)) groups.set(p, []);
  groups.get(p).push({ fileId: d.fileId, name, pos: localPos(d) });
}

console.log('waypoint parents:', groups.size);

for (const [p, list] of groups) {
  const parentName = goName.get((transforms.get(p).lines.join('\n').match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1]) || '?';
  const ordered = [];
  const childIds = [];
  const parentDoc = transforms.get(p);
  if (parentDoc) {
    const flat = parentDoc.lines.join('\n');
    const block = flat.match(/m_Children:([\s\S]*?)(?:\n  \w|$)/);
    if (block) {
      for (const m of block[1].matchAll(/fileID:\s*(-?\d+)/g)) childIds.push(+m[1]);
    }
  }
  for (const id of childIds) {
    const t = transforms.get(id);
    if (!t) continue;
    const go = (t.lines.join('\n').match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1];
    const name = go ? goName.get(+go) : null;
    if (name && /^WP_\d+$/.test(name)) ordered.push({ id, name, pos: localPos(t) });
  }

  console.log(`\nparent ${p} (${parentName}): ${list.length} waypoints, ordered ${ordered.length}`);
  if (!ordered.length) continue;

  const ys = ordered.map((w) => w.pos.y);
  console.log(`local y: min ${Math.min(...ys).toFixed(2)} max ${Math.max(...ys).toFixed(2)} spread ${(Math.max(...ys) - Math.min(...ys)).toFixed(2)} m`);

  const steps = [];
  for (let i = 1; i < ordered.length; i++) {
    const a = ordered[i - 1].pos, b = ordered[i].pos;
    steps.push({ i, dy: b.y - a.y, gap: Math.hypot(b.x - a.x, b.z - a.z), grade: Math.abs(b.y - a.y) / Math.max(0.001, Math.hypot(b.x - a.x, b.z - a.z)) * 100 });
  }
  const gaps = steps.map((s) => s.gap).sort((a, b) => a - b);
  console.log(`gaps: min ${gaps[0].toFixed(2)} median ${gaps[Math.floor(gaps.length / 2)].toFixed(2)} max ${gaps[gaps.length - 1].toFixed(2)}  (<3m: ${gaps.filter((g) => g < 3).length}, <5m: ${gaps.filter((g) => g < 5).length})`);

  const steep = steps.slice().sort((a, b) => b.grade - a.grade);
  console.log('steepest steps (grade % / dy / gap):');
  for (const s of steep.slice(0, 8)) console.log(`  #${s.i}: ${s.grade.toFixed(1)}%  dy ${s.dy.toFixed(2)}  gap ${s.gap.toFixed(2)}`);
}
