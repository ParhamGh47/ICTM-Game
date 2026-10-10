// Print the GameObject names in a scene that match a pattern, with their world positions, so the course
// can be read off (player start, checkpoints, finish, roads, adamaks...).
const fs = require('fs');
const path = require('path');
const { read, splitDocs, index, nameOf } = require('./scenelib');

const file = process.argv[2];
const pattern = new RegExp(process.argv[3] || '.', 'i');
const root = path.resolve(__dirname, '..');
const text = read(path.join(root, file));
const { docs } = splitDocs(text);
const byId = index(docs);

const goComponents = new Map();
for (const d of docs) {
  if (d.cls !== '1') continue;
  const ids = [...d.body.matchAll(/component: \{fileID: (\d+)\}/g)].map((m) => m[1]);
  goComponents.set(d.id, ids);
}

function transformOf(goId) {
  const go = byId.get(goId);
  if (!go) return null;
  for (const m of go.body.matchAll(/component: \{fileID: (\d+)\}/g)) {
    const c = byId.get(m[1]);
    if (c && c.cls === '4') return c;
  }
  return null;
}

// Resolve a transform's world position by walking parents' local positions (correct enough for a list;
// the road spline nodes are already world-space in their own fields, but transforms are local).
function localPos(t) {
  const m = t.body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
  return m ? { x: +m[1], y: +m[2], z: +m[3] } : { x: 0, y: 0, z: 0 };
}

function parentGo(t) {
  const f = (t.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
  if (!f || f === '0') return null;
  const parentT = byId.get(f);
  if (!parentT) return null;
  for (const [goId, ids] of goComponents) if (ids.includes(f)) return byId.get(goId);
  return null;
}

function worldPos(t) {
  let p = { x: 0, y: 0, z: 0 };
  let cur = t;
  while (cur) {
    const l = localPos(cur);
    p.x += l.x; p.y += l.y; p.z += l.z;
    const go = parentGo(cur);
    cur = go ? transformOf(go.id) : null;
  }
  return p;
}

let n = 0;
for (const [goId, ids] of goComponents) {
  const go = byId.get(goId);
  const name = nameOf(go);
  if (!pattern.test(name)) continue;
  const t = transformOf(goId);
  const p = t ? worldPos(t) : { x: 0, y: 0, z: 0 };
  console.log(`${name.padEnd(34)} (${p.x.toFixed(1)}, ${p.y.toFixed(1)}, ${p.z.toFixed(1)})`);
  if (++n > 80) { console.log('...'); break; }
}
console.log('total: ' + n);
