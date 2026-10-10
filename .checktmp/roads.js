// List every RoadArchitect Road in a scene: its name, its spline's node count, total distance, and the
// world position of its first and last nodes - enough to see the course and any gaps between roads.
const fs = require('fs');
const path = require('path');
const { read, splitDocs, index, nameOf } = require('./scenelib');

const ROAD_GUID = 'b135db4f64080a94690da38eededc382';
const SPLINE_GUID = 'unknown-spline';

const file = process.argv[2] || 'Assets/Scenes/Levels Scenes/4/Core-4.unity';
const root = path.resolve(__dirname, '..');
const full = path.join(root, file);

const text = read(full);
const { docs } = splitDocs(text);
const byId = index(docs);

// All MonoBehaviour docs whose m_Script points at a given guid.
function monobytes(guid) {
  const out = [];
  for (const d of docs) {
    if (d.cls !== '114') continue;
    const m = d.body.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/);
    if (m && m[1] === guid) out.push(d);
  }
  return out;
}

// GameObject -> component ids
const goComponents = new Map();
for (const d of docs) {
  if (d.cls !== '1') continue; // GameObject
  const ids = [...d.body.matchAll(/component: \{fileID: (\d+)\}/g)].map((m) => m[1]);
  goComponents.set(d.id, ids);
}

function ownerGo(componentId) {
  for (const [goId, ids] of goComponents) if (ids.includes(componentId)) return byId.get(goId);
  return null;
}

function transformOfGo(goId) {
  const go = byId.get(goId);
  if (!go) return null;
  for (const m of go.body.matchAll(/component: \{fileID: (\d+)\}/g)) {
    const c = byId.get(m[1]);
    if (c && c.cls === '4') return c; // Transform
  }
  return null;
}

const roads = monobytes(ROAD_GUID);
console.log('Road components: ' + roads.length);

for (const r of roads) {
  const go = ownerGo(r.id);
  const name = nameOf(go);
  const splineRef = (r.body.match(/spline: \{fileID: (\d+)/) || [])[1];
  const spline = splineRef ? byId.get(splineRef) : null;

  let nodeCount = '?';
  let distance = '?';
  let first = '-', last = '-';
  if (spline) {
    nodeCount = [...spline.body.matchAll(/nodes:/g)].length ? '' : '';
    const n = (spline.body.match(/nodes:\s*-\s*nodes:\s*(\d+)/) || [])[1];
    // nodes are stored as an array of references to SplineN components
    const nodeIds = [...spline.body.matchAll(/- \{fileID: (\d+)\}/g)].map((m) => m[1]);
    nodeCount = nodeIds.length;
    distance = (spline.body.match(/distance: ([0-9.]+)/) || [])[1];

    const positions = nodeIds
      .map((id) => byId.get(id))
      .filter(Boolean)
      .map((sn) => {
        const t = (sn.body.match(/pos: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/) || []);
        return t.length ? `${t[1]},${t[2]},${t[3]}` : null;
      })
      .filter(Boolean);
    if (positions.length) { first = positions[0]; last = positions[positions.length - 1]; }
  }

  console.log(`\n[${name}]  go=${go ? go.id : '?'}`);
  console.log(`  spline=${splineRef || '-'}  nodes=${nodeCount}  distance=${distance}`);
  console.log(`  first node: ${first}`);
  console.log(`  last  node: ${last}`);
}
