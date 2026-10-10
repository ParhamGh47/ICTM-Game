// List named objects within a radius of a world point, to see what sits in a suspicious gap.
const fs = require('fs');
const path = require('path');
const { read, splitDocs, index, nameOf } = require('./scenelib');

const file = process.argv[2];
const px = +process.argv[3], py = +process.argv[4], pz = +process.argv[5], radius = +process.argv[6] || 40;
const root = path.resolve(__dirname, '..');
const { docs } = splitDocs(read(path.join(root, file)));
const byId = index(docs);

const goComponents = new Map();
for (const d of docs) if (d.cls === '1') goComponents.set(d.id, [...d.body.matchAll(/component: \{fileID: (\d+)\}/g)].map((m) => m[1]));
function transformOf(goId) { const go = byId.get(goId); if (!go) return null; for (const m of go.body.matchAll(/component: \{fileID: (\d+)\}/g)) { const c = byId.get(m[1]); if (c && c.cls === '4') return c; } return null; }
function localPos(t) { const m = t.body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/); return m ? { x: +m[1], y: +m[2], z: +m[3] } : { x: 0, y: 0, z: 0 }; }
function parentGo(t) { const f = (t.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1]; if (!f || f === '0') return null; for (const [goId, ids] of goComponents) if (ids.includes(f)) return byId.get(goId); return null; }
function worldPos(t) { let p = { x: 0, y: 0, z: 0 }, cur = t; while (cur) { const l = localPos(cur); p.x += l.x; p.y += l.y; p.z += l.z; const go = parentGo(cur); cur = go ? transformOf(go.id) : null; } return p; }

let n = 0;
for (const goId of goComponents.keys()) {
  const t = transformOf(goId);
  if (!t) continue;
  const p = worldPos(t);
  const d = Math.hypot(p.x - px, p.z - pz);
  if (d > radius) continue;
  console.log(`${nameOf(byId.get(goId)).padEnd(30)} (${p.x.toFixed(1)}, ${p.y.toFixed(1)}, ${p.z.toFixed(1)})  ${d.toFixed(1)}m`);
  if (++n > 60) break;
}
console.log('total: ' + n);
