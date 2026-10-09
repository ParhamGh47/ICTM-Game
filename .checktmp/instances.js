// Every prefab instance in a scene, its name and whether it is a root.
const fs = require('fs');
const path = require('path');
const ROOT = path.resolve(__dirname, '..');

const guidNames = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== '.git' && e.name !== '.checktmp') walk(p); continue; }
    if (!e.name.endsWith('.meta')) continue;
    const m = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]+)/);
    if (m) guidNames.set(m[1], e.name.replace(/\.meta$/, ''));
  }
})(ROOT);

function read(f) { return fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n'); }

for (const arg of process.argv.slice(2)) {
  const file = path.join(ROOT, arg);
  const text = read(file);
  const out = [];
  const re = /--- !u!1001 &(\d+)\nPrefabInstance:\n([\s\S]*?)m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/g;
  let m;
  while ((m = re.exec(text))) {
    const block = m[2];
    const parent = (block.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
    const name = (block.match(/propertyPath: m_Name\n\s*value: ([^\n]*)/) || [])[1];
    out.push({ name: name || '(unnamed)', src: guidNames.get(m[3]) || m[3], root: parent === '0' });
  }
  console.log('=== ' + arg + '  (' + out.length + ' prefab instances)');
  for (const o of out) console.log((o.root ? '  root  ' : '  child ') + String(o.name).padEnd(26) + o.src);
}
