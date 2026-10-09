const path = require('path');
const fs = require('fs');
const ROOT = path.resolve(__dirname, '..');
const { read, splitDocs, index } = require('./scenelib');

const guidNames = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (!['.git', '.checktmp'].includes(e.name)) walk(p); continue; }
    if (!e.name.endsWith('.meta')) continue;
    const m = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]+)/);
    if (m) guidNames.set(m[1], e.name.replace(/\.meta$/, ''));
  }
})(ROOT);

function describe(docs, doc) {
  const name = (doc.body.match(/m_Name: (.*)/) || [])[1];
  const script = (doc.body.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/) || [])[1];
  const src = (doc.body.match(/m_CorrespondingSourceObject: \{fileID: (\d+), guid: ([0-9a-f]+)/) || []);
  return 'cls' + doc.cls + (name !== undefined ? ' "' + name + '"' : '') +
    (script ? ' @' + (guidNames.get(script) || script.slice(0, 8)) : '') +
    (src[1] ? ' src=' + src[1] : '');
}

function analyze(file, instanceIds) {
  const { docs } = splitDocs(read(file));
  const byId = index(docs);

  console.log('\n=== ' + file);
  for (const inst of instanceIds) {
    const id = String(inst.id);
    const stripped = docs.filter((d) => (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1] === id);
    console.log('  ' + inst.label + ' (' + id + ')' + (byId.has(id) ? '' : '  <MISSING>'));
    for (const s of stripped) console.log('     stripped ' + s.id + '  ' + describe(docs, s));
  }

  // scene objects (no prefab instance of their own) whose ancestor chain climbs into one of these instances
  const owner = new Map();
  for (const d of docs) {
    if (d.cls === '1') {
      const tr = (d.body.match(/- component: \{fileID: (\d+)\}/) || [])[1];
      const inst = (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
      owner.set(tr, { go: d, inst });
    }
  }
  const wanted = new Set(instanceIds.map((i) => String(i.id)));
  for (const d of docs) {
    if (d.cls !== '1') continue;
    if ((d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1]) continue;
    const tr = (d.body.match(/- component: \{fileID: (\d+)\}/) || [])[1];
    let cur = tr;
    let hops = 0;
    let host = null;
    while (cur && hops++ < 64) {
      const td = byId.get(cur);
      if (!td) break;
      if (td.stripped) {
        const inst = (td.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
        if (wanted.has(inst)) host = inst;
        break;
      }
      const father = (td.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
      if (!father || father === '0') break;
      cur = father;
    }
    if (host) console.log('  scene child of inst ' + host + ': ' + describe(docs, d));
  }
}

analyze(path.join(ROOT, 'Assets/Scenes/Levels Scenes/3/Core-3.unity'), [
  { id: '1932669414', label: 'Truck (Player)' },
  { id: '208287291', label: 'CameraManager' },
  { id: '6890820110328195566', label: 'CanvasUI' },
]);

// the Template's own instances, for comparison
const t = splitDocs(read(path.join(ROOT, 'Assets/Scenes/Template.unity')));
for (const d of t.docs) {
  if (d.cls !== '1001') continue;
  const parent = (d.body.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
  const src = (d.body.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1];
  console.log('  Template inst ' + d.id + ' ' + (guidNames.get(src) || src) + '  parent=' + parent);
}
