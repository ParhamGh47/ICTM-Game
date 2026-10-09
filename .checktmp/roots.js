// Root GameObjects of a scene, with the prefab each came from and the scripts on it.
const fs = require('fs');
const path = require('path');
const ROOT = path.resolve(__dirname, '..');

// guid -> asset name, from every .meta in the project
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

function analyze(file) {
  const text = read(file);
  // gameobjects: id -> {name, components:[ids]}
  const gos = new Map();
  const re = /--- !u!1 &(\d+)(?: stripped)?\nGameObject:\n([\s\S]*?)(?=\n--- !u!|\n?$)/g;
  let m;
  while ((m = re.exec(text))) {
    const body = m[2];
    const name = (body.match(/m_Name: (.*)/) || [])[1] || '';
    const comps = [...body.matchAll(/- component: \{fileID: (\d+)\}/g)].map((c) => c[1]);
    const inst = (body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
    gos.set(m[1], { name, comps, inst });
  }
  // transforms: id -> {go, father}
  const trs = new Map();
  const re2 = /--- !u!4 &(\d+)(?: stripped)?\nTransform:\n([\s\S]*?)(?=\n--- !u!|\n?$)/g;
  while ((m = re2.exec(text))) {
    const body = m[2];
    const go = (body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const father = (body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
    trs.set(m[1], { go, father });
  }
  // script components: id -> script guid
  const scripts = new Map();
  const re3 = /--- !u!114 &(\d+)(?: stripped)?\nMonoBehaviour:\n([\s\S]*?)(?=\n--- !u!|\n?$)/g;
  while ((m = re3.exec(text))) {
    const g = (m[2].match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/) || [])[1];
    if (g) scripts.set(m[1], g);
  }
  // prefab instances: id -> source guid
  const insts = new Map();
  const re4 = /--- !u!1001 &(\d+)\nPrefabInstance:[\s\S]*?m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/g;
  while ((m = re4.exec(text))) insts.set(m[1], m[2]);

  const roots = [];
  for (const [id, t] of trs) {
    if (t.father !== '0') continue;
    const go = gos.get(t.go);
    if (!go) continue;
    const comps = go.comps.map((c) => {
      if (scripts.has(c)) return '@' + (guidNames.get(scripts.get(c)) || scripts.get(c));
      return String(c);
    });
    roots.push({
      name: go.name,
      prefab: go.inst && insts.has(go.inst) ? guidNames.get(insts.get(go.inst)) : null,
      comps,
    });
  }
  console.log('=== ' + path.relative(ROOT, file) + ' (' + roots.length + ' roots)');
  for (const r of roots) {
    console.log('  ' + String(r.name).padEnd(28) + (r.prefab ? '[' + r.prefab + ']' : '[scene]') +
      '  ' + r.comps.join(', '));
  }
}

for (const f of process.argv.slice(2)) analyze(path.join(ROOT, f));
