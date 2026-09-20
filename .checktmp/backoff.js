// Mirror of CarController.BackOffAlongRoad, run on the real node distances of Core-4's roads, to see where
// a "reset to the last road" now puts the car compared with the old behaviour (right on the lip).
function backOff(nodes, length, param, direction, count, metres) {
  if (nodes.length < 2) return param;
  const travelled = Math.min(1, Math.max(0, param)) * length;
  const ascending = direction >= 0;

  let at = -1;
  for (let i = 0; i < nodes.length; i++) if (nodes[i] <= travelled + 0.01) at = i;
  if (at < 0) at = 0;

  const step = ascending ? -1 : 1;
  let index = at;
  let remaining = Math.max(0, count - (ascending ? 1 : 0));
  while (remaining > 0) {
    const next = index + step;
    if (next < 0 || next >= nodes.length) break;
    index = next;
    remaining--;
  }

  const byNodes = nodes[index];
  const byMetres = ascending ? travelled - metres : travelled + metres;
  let target = ascending ? Math.max(byNodes, byMetres) : Math.min(byNodes, byMetres);
  target = Math.min(length, Math.max(0, target));
  return Math.min(1, Math.max(0, param + (target - travelled) / length));
}

const roads = [
  { id: 41127978, length: 1822.7, nodes: [0,36.3,93.7,122.5,179.2,213.7,267.6,330.4,634.1,739.7,804.5,868.6,976.1,1013.2,1046.4,1069.0,1101.2,1223.2,1363.0,1466.1,1510.7,1568.3,1677.2,1722.5,1760.9,1778.7,1822.7] },
  { id: 1155506138, length: 730.0, nodes: [0,43.3,85.6,149.1,184.9,228.1,270.4,377.6,483.4,548.9,626.5,730.0] },
  { id: 1054468967, length: 127.8, nodes: [0,15.0,56.5,79.1,127.8] },
];

for (const r of roads) {
  const at95 = backOff(r.nodes, r.length, 0.95, 1, 2, 45);
  const atEnd = backOff(r.nodes, r.length, 0.999, 1, 2, 45);
  console.log('road ' + r.id + ' (' + r.length + 'm)');
  console.log('  car at 95%: was ' + (0.95 * r.length).toFixed(1) + 'm -> now ' +
    (at95 * r.length).toFixed(1) + 'm  (' + ((0.95 - at95) * r.length).toFixed(1) + 'm further back)');
  console.log('  car at end: was ' + (0.999 * r.length).toFixed(1) + 'm -> now ' +
    (atEnd * r.length).toFixed(1) + 'm  (' + ((0.999 - atEnd) * r.length).toFixed(1) + 'm further back)');
}
// And a road whose nodes are close together: two nodes should be honoured in full.
const dense = { length: 200, nodes: [0, 8, 16, 24, 32, 40, 48, 56, 64, 72, 80, 88, 96, 104, 112, 120, 128, 136, 144, 152, 160, 168, 176, 184, 192, 200] };
const d = backOff(dense.nodes, dense.length, 0.98, 1, 2, 45);
console.log('dense road (8m nodes), car at 196m -> now ' + (d * 200).toFixed(1) + 'm  (' +
  ((0.98 - d) * 200).toFixed(1) + 'm back = two whole nodes)');
