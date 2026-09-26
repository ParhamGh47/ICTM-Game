// Where everything in the player truck actually is, in the prefab's own space: each object's world position
// and world scale worked out through its parents, so the collision cube can be compared with the wheels and
// the rest of the truck rather than guessed at.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const file = process.argv[2] || path.join(root, 'Assets/Prefabs/Utils/Truck (Player).prefab');
const text = fs.readFileSync(file, 'utf8').replace(/\r/g, '');

// ---- script names, from the metas
const guids = {};
(function scan(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) { scan(full); continue; }
    if (!entry.name.endsWith('.meta')) continue;
    const m = fs.readFileSync(full, 'utf8').match(/^guid: (\w+)/m);
    if (m) guids[m[1]] = path.basename(full, '.meta');
  }
})(path.join(root, 'Assets'));

const blocks = [];
let current = null;
for (const line of text.split('\n')) {
  const header = line.match(/^--- !u!(\d+) &(\d+)/);
  if (header) { current = { cls: header[1], id: header[2], body: [] }; blocks.push(current); }
  else if (current) current.body.push(line);
}

const objs = {}, trs = {};
for (const b of blocks) {
  const body = b.body.join('\n');
  if (b.cls === '1') {
    objs[b.id] = {
      name: (body.match(/^\s*m_Name: (.*)$/m) || [])[1] || '',
      layer: (body.match(/m_Layer: (\d+)/) || [])[1],
      tag: (body.match(/m_TagString: (\S+)/) || [])[1],
      comps: (body.match(/- component: \{fileID: (\d+)\}/g) || []).map((x) => x.match(/(\d+)/)[1]),
    };
  } else if (b.cls === '4' || b.cls === '224') {
    const num = (re) => {
      const m = body.match(re);
      return m ? Number(m[1]) : 0;
    };
    trs[b.id] = {
      cls: b.cls,
      go: (body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1],
      father: (body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1],
      // a RectTransform keeps its x,y under m_AnchoredPosition; ordinary ones use m_LocalPosition
      pos: [num(/m_LocalPosition: \{x: ([^,]+)/), num(/m_LocalPosition: \{x: [^,]+, y: ([^,]+)/), num(/m_LocalPosition: \{x: [^,]+, y: [^,]+, z: ([^}]+)\}/)],
      scale: [num(/m_LocalScale: \{x: ([^,]+)/), num(/m_LocalScale: \{x: [^,]+, y: ([^,]+)/), num(/m_LocalScale: \{x: [^,]+, y: [^,]+, z: ([^}]+)\}/)],
      rot: (body.match(/m_LocalEulerAnglesHint: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number),
      fatherIsRoot: false,
    };
  }
}
const transformOfGo = {};
for (const id in trs) transformOfGo[trs[id].go] = id;
// the root object: no father
const roots = Object.keys(trs).filter((id) => trs[id].father === '0');

function world(id) {
  // world position and scale of a transform, walking up
  let p = [0, 0, 0], s = [1, 1, 1], t = id, guard = 0;
  const pos = [0, 0, 0], sc = [1, 1, 1];
  const chain = [];
  while (t && guard++ < 40) {
    chain.push(t);
    const tr = trs[t];
    if (!tr) break;
    const f = tr.father;
    if (f === '0' || !trs[f]) break;
    t = f;
  }
  for (const id2 of chain.reverse()) {
    const tr = trs[id2];
    for (let i = 0; i < 3; i++) pos[i] += tr.pos[i] * sc[i];
    for (let i = 0; i < 3; i++) sc[i] *= tr.scale[i];
  }
  return { pos, scale: sc };
}

const comps = {};
for (const b of blocks) comps[b.id] = { cls: b.cls, body: b.body.join('\n') };
function label(id) {
  const c = comps[id];
  if (!c) return '?';
  const tan = (c.body.match(/m_TargetAssemblyTypeName: ([^\n]+)/) || [])[1];
  if (tan) return tan.split(',')[0].trim();
  const guid = (c.body.match(/m_Script: \{fileID: \d+, guid: (\w+)/) || [])[1];
  if (guid) return guids[guid] || ('script:' + guid.slice(0, 6));
  return { 54: 'Rigidbody', 65: 'BoxCollider', 136: 'Capsule', 135: 'Sphere', 64: 'MeshCollider', 146: 'WheelCollider', 33: 'MeshFilter', 23: 'MeshRenderer', 82: 'AudioSource', 198: 'ParticleSystem', 96: 'TrailRenderer', 108: 'Light', 199: 'ParticleSystemRenderer', 169: 'AudioSource?' }[c.cls] || ('class ' + c.cls);
}

const names = process.argv[3] ? process.argv[3].split(',') : null;
for (const id in objs) {
  const o = objs[id];
  const w = world(transformOfGo[id]);
  const labels = o.comps.map(label);
  if (names && !names.some((n) => o.name.includes(n))) continue;
  console.log(
    o.name.padEnd(22),
    'wpos ' + w.pos.map((v) => v.toFixed(4)).join(', ').padEnd(26),
    'wscale ' + w.scale.map((v) => v.toFixed(3)).join(', ').padEnd(22),
    'layer=' + o.layer,
    '| ' + labels.join(' ')
  );
}

console.log('\n=== the collision cube, in the prefab\'s own space ===');
for (const id in comps) {
  if (comps[id].cls !== '65') continue;
  const b = comps[id].body;
  const num = (re) => Number((b.match(re) || [])[1]);
  const size = [num(/m_Size: \{x: ([^,]+)/), num(/m_Size: \{x: [^,]+, y: ([^,]+)/), num(/m_Size: \{x: [^,]+, y: [^,]+, z: ([^}]+)\}/)];
  const centre = [num(/m_Center: \{x: ([^,]+)/), num(/m_Center: \{x: [^,]+, y: ([^,]+)/), num(/m_Center: \{x: [^,]+, y: [^,]+, z: ([^}]+)\}/)];
  const goid = (b.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const w = world(transformOfGo[goid]);
  const worldSize = size.map((v, i) => v * w.scale[i]);
  const worldCentre = centre.map((v, i) => w.pos[i] + v * w.scale[i]);
  console.log('on', objs[goid].name, 'layer', objs[goid].layer, 'tag', objs[goid].tag);
  console.log('  size (local)', size.join(', '), '-> world', worldSize.map((v) => v.toFixed(4)).join(', '));
  console.log('  centre world', worldCentre.map((v) => v.toFixed(4)).join(', '));
  console.log('  so the cube spans x', (worldCentre[0] - worldSize[0] / 2).toFixed(3) + ' .. ' + (worldCentre[0] + worldSize[0] / 2).toFixed(3),
              '| y', (worldCentre[1] - worldSize[1] / 2).toFixed(3) + ' .. ' + (worldCentre[1] + worldSize[1] / 2).toFixed(3),
              '| z', (worldCentre[2] - worldSize[2] / 2).toFixed(3) + ' .. ' + (worldCentre[2] + worldSize[2] / 2).toFixed(3));
}

console.log('\n=== wheel references in the scripts ===');
for (const id in comps) {
  const c = comps[id];
  if (c.cls !== '114') continue;
  const tan = (c.body.match(/m_TargetAssemblyTypeName: ([^\n]+)/) || [])[1] || '';
  if (!/WheelPhysics/.test(tan)) continue;
  const goid = (c.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const go = objs[goid];
  const w = world(transformOfGo[goid]);
  const fields = c.body.split('\n').filter((l) => /^  [a-zA-Z_][\w]*:/.test(l) && !/m_/.test(l));
  console.log(go.name, 'wpos', w.pos.map((v) => v.toFixed(4)).join(', '), 'scale', w.scale.join(', '));
  console.log('   ', fields.filter((l) => /Radius|restLength|springTravel|isFront|isRear|Grip|Mass/.test(l)).map((s) => s.trim()).join(' | '));
}
