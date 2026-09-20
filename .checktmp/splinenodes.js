// Reads a Unity scene and groups its RoadArchitect spline nodes by spline, printing node distances so the
// spacing (and what "two nodes back" means) can be seen without opening Unity.
const fs = require('fs');
const path = process.argv[2];

const text = fs.readFileSync(path, 'utf8');
const blocks = text.split(/^--- /m).slice(1);

const splines = new Map(); // fileID -> { nodes: [{dist,time,id,special}] }

for (const block of blocks) {
  const cls = block.match(/^!u!(\d+) &(\d+)/);
  if (!cls) continue;
  if (!/^  idOnSpline: /m.test(block)) continue;

  const dist = parseFloat((block.match(/^  dist: (.+)$/m) || [])[1]);
  const time = parseFloat((block.match(/^  time: (.+)$/m) || [])[1]);
  const id = parseInt((block.match(/^  idOnSpline: (.+)$/m) || [])[1], 10);
  const spline = (block.match(/^  spline: \{fileID: (\d+)\}/m) || [])[1];
  const special = /^  isSpecialEndNode: 1/m.test(block);

  if (!splines.has(spline)) splines.set(spline, []);
  splines.get(spline).push({ dist, time, id, special });
}

const out = [];
for (const [spline, nodes] of splines) {
  nodes.sort((a, b) => a.dist - b.dist);
  const gaps = [];
  for (let i = 1; i < nodes.length; i++) gaps.push(nodes[i].dist - nodes[i - 1].dist);
  out.push({ spline, count: nodes.length, length: nodes[nodes.length - 1].dist, gaps, nodes });
}

out.sort((a, b) => b.length - a.length);

for (const s of out) {
  const real = s.gaps.filter((g) => g > 1);
  const avg = real.length ? real.reduce((a, b) => a + b, 0) / real.length : 0;
  console.log(
    'spline ' + s.spline +
    '  nodes=' + s.count +
    '  length=' + s.length.toFixed(1) + 'm' +
    '  gap avg=' + avg.toFixed(1) + 'm  min=' + (real.length ? Math.min(...real).toFixed(1) : '-') +
    '  max=' + (real.length ? Math.max(...real).toFixed(1) : '-')
  );
  console.log('   dists: ' + s.nodes.map((n) => n.dist.toFixed(1) + (n.special ? '*' : '')).join(', '));
  console.log('   2 nodes back from the far end = ' +
    (s.length - s.nodes[Math.max(0, s.nodes.length - 3)].dist).toFixed(1) + 'm');
}
