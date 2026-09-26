// The player truck's collision, measured against the truck it is meant to cover.
//
// The prefab is read as text: every BoxCollider in it, worked up through its parents into prefab space, and
// every one of them checked against the model's own measurements (Truck.fbx, in the file's cm units, times
// the 0.1 the prefab gives the model root and the 0.01 the importer gives the file). The point of the check
// is that the chassis box the springs and the handling were tuned around has not moved, and that the union
// of the boxes now covers the truck's body from the ground clearance up to its roof.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const file = path.join(root, 'Assets/Prefabs/Utils/Truck (Player).prefab');
const text = fs.readFileSync(file, 'utf8').replace(/\r/g, '');

const blocks = [];
let current = null;
for (const line of text.split('\n')) {
  const header = line.match(/^--- !u!(\d+) &(\d+)/);
  if (header) {
    current = { cls: header[1], id: header[2], body: [] };
    blocks.push(current);
  } else if (current) current.body.push(line);
}

const byId = {};
for (const b of blocks) byId[b.id] = { cls: b.cls, body: b.body.join('\n') };

const vec = (body, key) => {
  const m = body.match(new RegExp('^\\s*' + key + ': \\{x: ([^,]+), y: ([^,]+), z: ([^}]+)\\}', 'm'));
  return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : null;
};

// ---- every transform in the prefab, so a collider can be placed through its parents
const transforms = {};
for (const b of blocks) {
  if (b.cls !== '4') continue;
  const body = b.body.join('\n');
  transforms[b.id] = {
    go: (body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1],
    father: (body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1],
    pos: vec(body, 'm_LocalPosition'),
    scale: vec(body, 'm_LocalScale'),
  };
}

const nameOf = (goId) => {
  const g = byId[goId];
  return g ? (g.body.match(/^\s*m_Name: (.*)$/m) || [])[1] : '?';
};

function worldOf(transformId) {
  const t = transforms[transformId];
  if (!t) return null;
  let p = t.pos.slice();
  let s = t.scale.slice();
  let father = t.father;
  let guard = 0;
  const chain = [];
  chain.push(t);

  while (father && father !== '0' && transforms[father] && guard++ < 32) {
    const parent = transforms[father];
    chain.push(parent);
    father = parent.father;
  }

  chain.reverse();

  const pos = [0, 0, 0];
  const scale = [1, 1, 1];
  for (const tr of chain) {
    for (let i = 0; i < 3; i++) pos[i] += tr.pos[i] * scale[i];
    for (let i = 0; i < 3; i++) scale[i] *= tr.scale[i];
  }

  return { pos, scale };
}

// the root: the object the level's truck is placed by, and what the reset measures from
const rootGo = Object.keys(nameOf ? byId : {}).find((id) => byId[id].cls === '1' && nameOf(id) === 'Truck (Player)');
const rootTransform = Object.keys(transforms).find((id) => transforms[id].go === rootGo);
const rootWorld = worldOf(rootTransform);

console.log('root', nameOf(rootGo), 'at', rootWorld.pos.map((v) => v.toFixed(4)).join(', '),
  'scale', rootWorld.scale.map((v) => v.toFixed(4)).join(', '));

// ---- the colliders, in prefab space relative to the root
const boxes = [];
for (const b of blocks) {
  if (b.cls !== '65') continue;
  const body = b.body.join('\n');
  const go = (body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const transformId = Object.keys(transforms).find((id) => transforms[id].go === go);
  const world = worldOf(transformId);
  const size = vec(body, 'm_Size');
  const centre = vec(body, 'm_Center');
  const enabled = (body.match(/m_Enabled: (\d+)/) || [])[1];

  const worldSize = size.map((v, i) => v * world.scale[i]);
  const worldCentre = centre.map((v, i) => world.pos[i] + v * world.scale[i]);

  const local = worldCentre.map((v, i) => v - rootWorld.pos[i]);
  const min = local.map((v, i) => v - worldSize[i] / 2);
  const max = local.map((v, i) => v + worldSize[i] / 2);

  boxes.push({ name: nameOf(go), enabled, file: b.id, size: worldSize, centre: local, min, max });

  console.log('collider on', nameOf(go).padEnd(16), 'enabled', enabled,
    'size', worldSize.map((v) => v.toFixed(4)).join(' x '),
    '| from', min.map((v) => v.toFixed(4)).join(', '),
    'to', max.map((v) => v.toFixed(4)).join(', '));
}

const f4 = (v) => v.toFixed(4);

console.log('\n--- the chassis box, which must not have moved');
const chassis = boxes.find((b) => b.name === 'Body');
const expected = { size: [0.5623, 0.1292, 1.182], centre: [0, 0.1852, 0.05] };
let ok = true;
for (let i = 0; i < 3; i++) {
  if (Math.abs(chassis.size[i] - expected.size[i]) > 0.001) { console.log('  FAIL size axis', i, f4(chassis.size[i])); ok = false; }
  if (Math.abs(chassis.centre[i] - expected.centre[i]) > 0.001) { console.log('  FAIL centre axis', i, f4(chassis.centre[i])); ok = false; }
}
console.log(ok ? '  chassis untouched: size ' + chassis.size.map(f4).join(' x ') + ' centre ' + chassis.centre.map(f4).join(', ')
               : '  chassis CHANGED');

console.log('\n--- the body box added above it');
const body = boxes.find((b) => b.name === 'Truck (Player)');
if (!body) {
  console.log('  MISSING: no collider on the root');
} else {
  const seam = Math.abs(body.min[1] - chassis.max[1]);
  console.log('  size ' + body.size.map(f4).join(' x ') + ' from ' + body.min.map(f4).join(', ') + ' to ' + body.max.map(f4).join(', '));
  console.log('  seam with the chassis top: ' + f4(seam) + (seam < 0.001 ? ' (flush)' : ' (GAP)'));
  const wider = [0, 2].some((i) => body.min[i] < chassis.min[i] - 0.0001 || body.max[i] > chassis.max[i] + 0.0001);
  console.log('  footprint inside the tuned chassis footprint: ' + (wider ? 'NO - wider, so side contacts change' : 'yes, so only what the truck hits above the chassis changes'));
}

console.log('\n--- the truck the boxes are meant to cover (Truck.fbx x 0.1 x 0.01, prefab space)');
const model = { min: [-0.311, -0.123, -0.677], max: [0.311, 0.614, 0.671] };
console.log('  model box (body, bumper skirt, wheels): ' + model.min.map(f4).join(', ') + ' to ' + model.max.map(f4).join(', '));
console.log('  roof, from the body mesh: 0.6140 above the root');

console.log('\n--- what the collision now covers, against the truck\'s body (x +-0.289, y -0.036..0.614, z -0.537..0.653)');
const bodyVolume = { min: [-0.289, -0.036, -0.537], max: [0.289, 0.614, 0.653] };
for (const axis of [0, 1, 2]) {
  const label = 'xyz'[axis];
  const covered = boxes.reduce((acc, b) => {
    const lo = Math.max(Math.min(model.min[axis], bodyVolume.min[axis]), b.min[axis]);
    const hi = Math.min(Math.max(model.max[axis], bodyVolume.max[axis]), b.max[axis]);
    return acc.concat(hi > lo ? [[lo, hi]] : []);
  }, []);
  const merged = covered.sort((a, b) => a[0] - b[0]).reduce((acc, span) => {
    if (acc.length && span[0] <= acc[acc.length - 1][1] + 0.0001) acc[acc.length - 1][1] = Math.max(acc[acc.length - 1][1], span[1]);
    else acc.push(span.slice());
    return acc;
  }, []);
  console.log('  ' + label + ': ' + (merged.length ? merged.map((s) => f4(s[0]) + '..' + f4(s[1])).join(' and ') : 'nothing'));
}

const uncovered =
  boxes.reduce((sum, b) => sum + (b.max[0] - b.min[0]) * (b.max[1] - b.min[1]) * (b.max[2] - b.min[2]), 0);
console.log('\n  solid volume of the collision: ' + f4(uncovered));
console.log('  the old single chassis box was:  ' + f4(0.5623 * 0.1292 * 1.182));

// where the mass would end up if Unity worked it out from both boxes, against the tuned chassis-only value
const volume = (b) => (b.max[0] - b.min[0]) * (b.max[1] - b.min[1]) * (b.max[2] - b.min[2]);
const total = boxes.reduce((sum, b) => sum + volume(b), 0);
const autoCom = boxes.reduce((sum, b) => sum + volume(b) * b.centre[1], 0) / total;
console.log('\n  centre of mass unity would pick from both boxes: y ' + f4(autoCom) + ' (the tuned one is y ' + f4(chassis.centre[1]) + ')');
