// Dump a Unity prefab's GameObject tree with component types (script names resolved from .cs.meta guids).
// Read-only. Usage: node .checktmp/trucktree.js "Assets/Prefabs/Utils/Truck (Player).prefab"
const fs = require('fs');
const path = require('path');

const file = process.argv[2];
const raw = fs.readFileSync(file, 'utf8');
const docs = raw.split(/\r?\n(?=--- !u!\d+)/).filter(t => /^--- !u!\d+/.test(t));

const CLASS = { '1': 'GameObject', '4': 'Transform', '224': 'RectTransform', '114': 'MonoBehaviour', '54': 'Rigidbody', '65': 'BoxCollider', '136': 'CapsuleCollider', '64': 'MeshCollider', '135': 'SphereCollider', '20': 'Camera', '108': 'Light', '198': 'ParticleSystem', '199': 'ParticleSystemRenderer', '23': 'MeshRenderer', '33': 'MeshFilter', '82': 'AudioSource', '146': '??', '1001': 'PrefabInstance', '1002': 'PrefabInstance(stripped)' };

const byId = new Map();
for (const text of docs) {
  const m = text.match(/^--- !u!(\d+) &(\d+)( stripped)?/);
  if (!m) continue;
  byId.set(m[2], { cls: m[1], kind: CLASS[m[1]] || ('class' + m[1]), text, stripped: !!m[3] });
}

const field = (t, name) => {
  const m = t.match(new RegExp('^ *' + name + ': ?(.*)$', 'm'));
  return m ? m[1].trim() : null;
};

// guid -> script file name, via every .cs.meta in the project.
function scriptNames() {
  const map = new Map();
  const walk = dir => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== '.git') walk(p); }
      else if (e.name.endsWith('.cs.meta')) {
        const g = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]+)/);
        if (g) map.set(g[1], e.name.replace(/\.cs\.meta$/, ''));
      }
    }
  };
  walk('Assets');
  return map;
}
const scripts = scriptNames();

function componentName(id) {
  const d = byId.get(id);
  if (!d) return '?';
  if (d.cls === '114') {
    const g = d.text.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/);
    return 'Script:' + (g ? (scripts.get(g[1]) || g[1].slice(0, 8)) : '?');
  }
  return d.kind;
}

// GameObjects and their transforms
const gos = new Map();
for (const [id, d] of byId) {
  if (d.cls !== '1') continue;
  const comps = [...d.text.matchAll(/- component: \{fileID: (\d+)\}/g)].map(m => m[1]);
  gos.set(id, { name: field(d.text, 'm_Name'), comps, go: id });
}

const childOf = new Map(); // transform id -> parent transform id
for (const [id, d] of byId) {
  if (d.cls !== '4' && d.cls !== '224') continue;
  const goRef = d.text.match(/m_GameObject: \{fileID: (\d+)\}/);
  if (goRef) { const g = gos.get(goRef[1]); if (g) g.transform = id; }
  const kids = [...d.text.matchAll(/- \{fileID: (\d+)\}/g)].map(m => m[1]);
  for (const k of kids) childOf.set(k, id);
}

for (const [id, g] of gos) {
  let depth = 0;
  let t = g.transform;
  while (t && childOf.has(t)) { t = childOf.get(t); depth++; }
  const comps = g.comps.map(componentName).filter(c => c !== 'Transform' && c !== 'RectTransform').join(', ') || '(no components)';
  console.log('  '.repeat(depth) + '- ' + g.name + '  [' + comps + ']');
}

// Nested prefab instances: what they are, and on which parent transform.
let nested = 0;
for (const [id, d] of byId) {
  if (!/^--- !u!1001/.test('--- !u!' + d.cls)) continue;
  if (d.cls !== '1001') continue;
  nested++;
  const src = d.text.match(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)/);
  const parent = d.text.match(/m_TransformParent: \{fileID: (\d+)\}/);
  let pname = '(none)';
  if (parent) {
    const pt = byId.get(parent[1]);
    const pg = pt && pt.text.match(/m_GameObject: \{fileID: (\d+)\}/);
    pname = pg && gos.get(pg[1]) ? gos.get(pg[1]).name : parent[1];
  }
  const mods = [...d.text.matchAll(/- target: \{fileID: (\d+)/g)].length;
  console.log(`\n[PrefabInstance ${id}] source guid ${src ? src[1].slice(0, 8) : '?'}  parent: ${pname}  modifications: ${mods}`);
}
console.log('\ntotal GameObjects: ' + gos.size + ', prefab instances: ' + nested);
