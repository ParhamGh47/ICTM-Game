// Print every node (in order) of every RoadArchitect Road in a scene, so the course can be traced.
const fs = require('fs');
const path = require('path');
const { read, splitDocs, index, nameOf } = require('./scenelib');

const ROAD_GUID = 'b135db4f64080a94690da38eededc382';
const file = process.argv[2] || 'Assets/Scenes/Levels Scenes/4/Core-4.unity';
const root = path.resolve(__dirname, '..');
const { docs } = splitDocs(read(path.join(root, file)));
const byId = index(docs);

const goComponents = new Map();
for (const d of docs) {
  if (d.cls !== '1') continue;
  goComponents.set(d.id, [...d.body.matchAll(/component: \{fileID: (\d+)\}/g)].map((m) => m[1]));
}
function ownerGo(componentId) {
  for (const [goId, ids] of goComponents) if (ids.includes(componentId)) return byId.get(goId);
  return null;
}

const roads = docs.filter((d) => {
  if (d.cls !== '114') return false;
  const m = d.body.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/);
  return m && m[1] === ROAD_GUID;
});

for (const r of roads) {
  const go = ownerGo(r.id);
  const splineRef = (r.body.match(/spline: \{fileID: (\d+)/) || [])[1];
  const spline = byId.get(splineRef);
  const nodeIds = [...spline.body.matchAll(/- \{fileID: (\d+)\}/g)].map((m) => m[1]);
  const dist = (spline.body.match(/distance: ([0-9.]+)/) || [])[1];

  console.log(`\n=== ${nameOf(go)}  (go=${go.id}, spline=${splineRef}, nodes=${nodeIds.length}, distance=${dist}) ===`);
  for (const id of nodeIds) {
    const sn = byId.get(id);
    if (!sn) continue;
    const t = sn.body.match(/pos: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
    if (!t) continue;
    console.log(`  (${(+t[1]).toFixed(1)}, ${(+t[2]).toFixed(1)}, ${(+t[3]).toFixed(1)})`);
  }
}
