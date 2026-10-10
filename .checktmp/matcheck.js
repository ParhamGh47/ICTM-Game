// Resolve the materials referenced by a prefab to their asset files and shaders, so we can tell whether a
// runtime _Color tint will actually darken them.
const fs = require('fs');
const path = require('path');
const { read, splitDocs } = require('./scenelib');

const root = path.resolve(__dirname, '..');
const file = process.argv[2] || 'Assets/Prefabs/Utils/Shadow Truck.prefab';

// guid -> asset path (for .mat and .cs)
const byGuid = {};
function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { walk(p); continue; }
    if (!e.name.endsWith('.meta')) continue;
    const g = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]+)/);
    if (g) byGuid[g[1]] = p.replace(/\.meta$/, '');
  }
}
walk(path.join(root, 'Assets'));

const { docs } = splitDocs(read(path.join(root, file)));

const used = new Set();
for (const d of docs) {
  if (d.cls !== '23' && d.cls !== '169') continue;
  for (const m of d.body.matchAll(/m_Materials:[\s\S]*?(?=\n  [a-zA-Z])/g)) {
    for (const r of m[0].matchAll(/guid: ([0-9a-f]+)/g)) used.add(r[1]);
  }
}

for (const g of used) {
  const p = byGuid[g];
  let shader = '?';
  if (p && p.endsWith('.mat')) {
    const mat = fs.readFileSync(path.join(root, p), 'utf8');
    const sm = mat.match(/m_Shader: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]+))?/);
    if (sm) {
      shader = sm[2] ? (byGuid[sm[2]] || ('builtin ' + sm[1])) : ('builtin ' + sm[1]);
      if (shader.startsWith('Assets/')) shader = path.basename(shader);
    }
    const hasColor = /_Color: \{[^}]*\}/.test(mat);
    const hasBase = /_BaseColor: \{[^}]*\}/.test(mat);
    console.log(`${path.basename(p).padEnd(28)} shader=${shader}  _Color=${hasColor} _BaseColor=${hasBase}`);
  } else {
    console.log(`${g} -> ${p || '(not found)'}`);
  }
}
