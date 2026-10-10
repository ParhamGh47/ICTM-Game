// Run the same greedy course build the ShadowRacer does, on Core-4's real roads, and report the legs, the
// gaps (jumps), the total length and the estimated lap time at each difficulty.
const fs = require('fs');
const path = require('path');
const { read, splitDocs, index } = require('./scenelib');

const ROAD_GUID = 'b135db4f64080a94690da38eededc382';
const root = path.resolve(__dirname, '..');
const file = process.argv[2] || 'Assets/Scenes/Levels Scenes/4/Core-4.unity';
const { docs } = splitDocs(read(path.join(root, file)));
const byId = index(docs);

const goComponents = new Map();
for (const d of docs) if (d.cls === '1') goComponents.set(d.id, [...d.body.matchAll(/component: \{fileID: (\d+)\}/g)].map((m) => m[1]));
function ownerGo(cid) { for (const [g, ids] of goComponents) if (ids.includes(cid)) return byId.get(g); return null; }

const player = { x: 74.301, y: 2.0809264, z: 931.781 };

function roadNodes(spline) {
  const ids = [...spline.body.matchAll(/- \{fileID: (\d+)\}/g)].map((m) => m[1]);
  return ids.map((id) => byId.get(id)).filter(Boolean).map((sn) => {
    const t = sn.body.match(/pos: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
    return t ? { x: +t[1], y: +t[2], z: +t[3] } : null;
  }).filter(Boolean);
}

const roads = [];
for (const d of docs) {
  if (d.cls !== '114') continue;
  const m = d.body.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/);
  if (!m || m[1] !== ROAD_GUID) continue;
  const go = ownerGo(d.id);
  const splineRef = (d.body.match(/spline: \{fileID: (\d+)/) || [])[1];
  const spline = byId.get(splineRef);
  const nodes = roadNodes(spline);
  const dist = parseFloat((spline.body.match(/distance: ([0-9.]+)/) || [])[1]);
  roads.push({ go: go.id, nodes, dist });
}

const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);

const legs = [];
let courseLength = 0;
let remaining = roads.slice();

// first road, nearest end to the player
let first = null, firstForward = true, best = Infinity;
for (const r of remaining) {
  const ds = dist(r.nodes[0], player) ** 2, de = dist(r.nodes[r.nodes.length - 1], player) ** 2;
  if (ds < best) { best = ds; first = r; firstForward = true; }
  if (de < best) { best = de; first = r; firstForward = false; }
}
remaining = remaining.filter((r) => r !== first);

function appendRoad(r, forward) {
  legs.push({ kind: 'road', go: r.go, forward, length: r.dist, start: courseLength, from: forward ? r.nodes[0] : r.nodes[r.nodes.length - 1] });
  courseLength += r.dist;
}
function appendJump(from, to) {
  const flat = Math.hypot(to.x - from.x, to.z - from.z);
  legs.push({ kind: 'jump', from, to, length: Math.max(0.1, flat), start: courseLength });
  courseLength += Math.max(0.1, flat);
}

appendRoad(first, firstForward);
let cursor = firstForward ? first.nodes[first.nodes.length - 1] : first.nodes[0];

const MAXLINK = 170, GAPMIN = 4;
while (remaining.length) {
  let next = null, nextForward = true, nearest = Infinity;
  for (const r of remaining) {
    const a = dist(r.nodes[0], cursor), b = dist(r.nodes[r.nodes.length - 1], cursor);
    if (a < nearest) { nearest = a; next = r; nextForward = true; }
    if (b < nearest) { nearest = b; next = r; nextForward = false; }
  }
  if (!next || nearest > MAXLINK) { console.log(`stop: nearest unused end ${nearest.toFixed(1)} m`); break; }
  const entry = nextForward ? next.nodes[0] : next.nodes[next.nodes.length - 1];
  if (dist(cursor, entry) > GAPMIN) appendJump(cursor, entry);
  appendRoad(next, nextForward);
  cursor = nextForward ? next.nodes[next.nodes.length - 1] : next.nodes[0];
  remaining = remaining.filter((r) => r !== next);
}

console.log('legs:');
for (const l of legs) {
  if (l.kind === 'road') console.log(`  road go=${l.go} ${l.forward ? 'fwd' : 'rev'}  ${l.length.toFixed(1)} m  start@${l.start.toFixed(1)}`);
  else console.log(`  JUMP ${Math.hypot(l.to.x - l.from.x, l.to.z - l.from.z).toFixed(1)} m (dy ${(l.to.y - l.from.y).toFixed(1)})  start@${l.start.toFixed(1)}`);
}
console.log('course length: ' + courseLength.toFixed(0) + ' m');

for (const [name, kph] of [['Easy', 82], ['Medium', 102], ['Hard', 118]]) {
  console.log(`  ${name}: ${(courseLength / (kph / 3.6)).toFixed(0)} s`);
}
