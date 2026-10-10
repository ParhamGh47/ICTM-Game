// Parses a Unity prefab's YAML and reports, for every renderer: where its transform sits in the prefab ROOT's
// own space (that is what matters: the stage moves the root and everything else follows) and the accumulated
// scale on the way down. A renderer a long way from the root, or scaled wildly, is a renderer that ends up
// somewhere other than where the model looks like it is.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const file = process.argv[2];
const text = fs.readFileSync(path.join(root, file), 'utf8');

// ---- documents
const docs = {};
const blocks = text.split(/\n(?=--- !u!\d+ &)/);
for (const block of blocks) {
  const m = block.match(/^--- !u!(\d+) &(\d+)/);
  if (!m) continue;
  docs[m[2]] = { type: Number(m[1]), body: block };

  const name = block.match(/\n  m_Name: ?(.*)/);
  docs[m[2]].name = name ? name[1].trim() : '';
}
const field = (body, key) => {
  const m = body.match(new RegExp('\\n  ' + key + ': ?(.*)'));
  return m ? m[1].trim() : '';
};
const ref = (body, key) => {
  const v = field(body, key).match(/\{fileID: (-?\d+)\}/);
  return v ? v[1] : null;
};

// ---- transforms
const transforms = {};
for (const id in docs) {
  if (docs[id].type !== 4) continue;
  const b = docs[id].body;
  transforms[id] = {
    id,
    go: ref(b, 'm_GameObject'),
    father: ref(b, 'm_Father'),
    position: field(b, 'm_LocalPosition'),
    scale: field(b, 'm_LocalScale'),
    children: [...b.matchAll(/m_Children:\n(?:    - \{fileID: \d+\}\n)*/g)].length ? [] : [],
  };
}
for (const id in transforms) {
  const t = transforms[id];
  t.children = Object.values(transforms).filter((o) => o.father === id).map((o) => o.id);
}

// Which GameObject holds which component
const owned = {};
for (const id in docs) {
  const d = docs[id];
  if (d.type === 4) continue;
  const go = ref(d.body, 'm_GameObject');
  if (go) (owned[go] = owned[go] || []).push(d.type);
}

// A prefab root is the transform whose father is fileID 0 (it is not "no father" - the key is there and its
// value is zero), so every other transform has a real parent to be walked from.
const roots = Object.values(transforms).filter((t) => t.father === '0');
console.log(file);
console.log('  root transform(s):', roots.map((r) => `${r.id} (go ${r.go}, name '${docs[r.go] ? docs[r.go].name : '?'}', pos ${r.position}, scale ${r.scale})`).join('\n                      '));

const parse = (v) => {
  const m = v.match(/x: (-?[\d.eE+-]+), y: (-?[\d.eE+-]+), z: (-?[\d.eE+-]+)/);
  return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : [1, 1, 1];
};

const walk = (id, parentPos, parentScale, depth, out) => {
  const t = transforms[id];
  const pos = parse(t.position);
  const scale = parse(t.scale);

  // accumulated: local position scaled by the parent chain, and the scale multiplied down
  const world = [
    parentPos[0] + pos[0] * parentScale[0],
    parentPos[1] + pos[1] * parentScale[1],
    parentPos[2] + pos[2] * parentScale[2],
  ];
  // (a rotation is present on many of these, so this treats the chain as translation + scale only - enough to
  // answer "is the model near its pivot or a long way from it", which is the question being asked here)
  const totalScale = [parentScale[0] * scale[0], parentScale[1] * scale[1], parentScale[2] * scale[2]];

  const kinds = owned[t.go] || [];
  const renderer = kinds.some((k) => [23, 137, 212, 169, 120, 199, 96].includes(k));
  const collider = kinds.some((k) => [64, 65, 135, 136].includes(k));

  if (renderer || collider) {
    out.push({ name: docs[t.go] ? docs[t.go].name : '?', depth, world, scale: totalScale,
      renderer, collider, kinds: kinds.join(',') });
  }

  for (const child of t.children) walk(child, world, totalScale, depth + 1, out);
};

const out = [];
for (const r of roots) walk(r.id, [0, 0, 0], [1, 1, 1], 0, out);

const r3 = (a) => a.map((n) => n.toFixed(3).padStart(9)).join(' ');
console.log(`  entries: ${out.length}`);
for (const e of out.slice(0, 60)) {
  console.log(`    ${' '.repeat(e.depth * 2)}${e.name.padEnd(26)} pos ${r3(e.world)}  scale ${r3(e.scale)}  ${e.renderer ? 'RENDER' : ''}${e.collider ? ' COLLIDE' : ''}`);
}
if (out.length > 60) console.log(`    ... ${out.length - 60} more`);

const extremes = out.reduce((acc, e) => {
  acc.min[0] = Math.min(acc.min[0], e.world[0]); acc.max[0] = Math.max(acc.max[0], e.world[0]);
  acc.min[1] = Math.min(acc.min[1], e.world[1]); acc.max[1] = Math.max(acc.max[1], e.world[1]);
  acc.min[2] = Math.min(acc.min[2], e.world[2]); acc.max[2] = Math.max(acc.max[2], e.world[2]);
  return acc;
}, { min: [Infinity, Infinity, Infinity], max: [-Infinity, -Infinity, -Infinity] });

console.log('  pivots span  x', extremes.min[0].toFixed(2), '..', extremes.max[0].toFixed(2),
  ' y', extremes.min[1].toFixed(2), '..', extremes.max[1].toFixed(2),
  ' z', extremes.min[2].toFixed(2), '..', extremes.max[2].toFixed(2));
