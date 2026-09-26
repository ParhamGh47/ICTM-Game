// The car prefabs, side by side: where the nested model sits, how big it is scaled, and what the collision
// box on the "Body" object covers - so the player's box can be measured against the same model rather than
// invented.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');

function guidNames() {
  const map = {};
  (function scan(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) { scan(full); continue; }
      if (!entry.name.endsWith('.meta')) continue;
      const m = fs.readFileSync(full, 'utf8').match(/^guid: (\w+)/m);
      if (m) map[m[1]] = path.relative(root, full.replace(/\.meta$/, ''));
    }
  })(path.join(root, 'Assets'));
  return map;
}

const names = guidNames();

function report(file) {
  const text = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
  let blocks = [], current = null;
  for (const line of text.split('\n')) {
    const h = line.match(/^--- !u!(\d+) &(\d+)/);
    if (h) { current = { cls: h[1], id: h[2], body: [] }; blocks.push(current); }
    else if (current) current.body.push(line);
  }

  console.log('=====', path.basename(file));
  const byId = {};
  for (const b of blocks) byId[b.id] = { cls: b.cls, body: b.body.join('\n') };

  // nested model instances: which prefab, and the scale applied to the model's root
  for (const b of blocks) {
    if (b.cls !== '1001') continue;
    const body = b.body.join('\n');
    const src = (body.match(/m_SourcePrefab: \{fileID: \d+, guid: (\w+)/) || [])[1];
    if (!src) continue;
    const parent = (body.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
    const parentName = (() => {
      const tr = byId[parent];
      if (!tr) return '?';
      const go = (tr.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
      const g = byId[go];
      return g ? (g.body.match(/^\s*m_Name: (.*)$/m) || [])[1] : '?';
    })();
    const scale = {};
    for (const m of body.matchAll(/propertyPath: m_LocalScale\.([xyz])\n\s+value: ([^\n]+)/g)) scale[m[1]] = Number(m[2]);
    const pos = {};
    for (const m of body.matchAll(/propertyPath: m_LocalPosition\.([xyz])\n\s+value: ([^\n]+)/g)) pos[m[1]] = Number(m[2]);
    console.log('  model', path.basename(names[src] || src), 'under', parentName,
      'pos', JSON.stringify(pos), 'scale', JSON.stringify(scale));
  }

  // wheels (objects with WheelPhysics or AICarController-style wheels) and their y
  for (const b of blocks) {
    if (b.cls !== '4') continue;
    const body = b.body.join('\n');
    const go = (body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const g = byId[go];
    const name = g ? (g.body.match(/^\s*m_Name: (.*)$/m) || [])[1] : '?';
    if (!/^Wheel/.test(name)) continue;
    const pos = (body.match(/m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number);
    console.log('  wheel', name.padEnd(10), 'local pos', pos.map((v) => v.toFixed(4)).join(', '));
  }

  // the Body collider, in world (prefab) space
  for (const b of blocks) {
    if (b.cls !== '65') continue;
    const body = b.body.join('\n');
    const go = (body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const name = byId[go] ? (byId[go].body.match(/^\s*m_Name: (.*)$/m) || [])[1] : '?';
    // find its transform and the parents' scales
    const trId = Object.keys(byId).find((id) => byId[id].cls === '4' &&
      (byId[id].body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1] === go);
    const own = (body.match(/m_Size: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number);
    const centre = (body.match(/m_Center: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number);
    // walk up for position/scale
    const pos = [0, 0, 0], sc = [1, 1, 1];
    const chain = [];
    let t = trId;
    let guard = 0;
    while (t && guard++ < 30) {
      const tr = byId[t];
      if (!tr) break;
      chain.push(tr);
      const f = (tr.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
      if (!f || f === '0' || !byId[f]) break;
      t = f;
    }
    for (const tr of chain.reverse()) {
      const p = (tr.body.match(/m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number);
      const s = (tr.body.match(/m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number);
      for (let i = 0; i < 3; i++) pos[i] += p[i] * sc[i];
      for (let i = 0; i < 3; i++) sc[i] *= s[i];
    }
    const size = own.map((v, i) => v * sc[i]);
    const c = centre.map((v, i) => pos[i] + v * sc[i]);
    console.log('  collider on', name, 'world size', size.map((v) => v.toFixed(4)).join(', '),
      '| centre', c.map((v) => v.toFixed(4)).join(', '),
      '| spans y', (c[1] - size[1] / 2).toFixed(3), '..', (c[1] + size[1] / 2).toFixed(3),
      '| z', (c[2] - size[2] / 2).toFixed(3), '..', (c[2] + size[2] / 2).toFixed(3));
  }
  console.log();
}

for (const file of process.argv.slice(2)) report(path.resolve(root, file));
