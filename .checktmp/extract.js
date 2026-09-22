// Extract the level-independent objects of a Core scene (settings, terrain,
// camera, UI, truck, light, music) into a body that a new level scene can be
// built from - keeping every fileID unchanged so internal links stay valid.
const fs = require('fs');
const path = require('path');
const { loadDocs, head, field, nameOf, guidMap, ROOT } = require('./lcore');

const src = process.argv[2] || 'Scenes/Levels Scenes/4/Core-4.unity';
const out = process.argv[3] || '.checktmp/out/core-body.txt';

const { header, docs } = loadDocs(path.join(ROOT, src));
const guids = guidMap();
const byId = new Map(docs.map(d => [head(d).id, d]));

const SETTINGS = [29, 104, 157, 196];
const ROOTS = ['Ground', 'Main Camera', 'Directional Light', 'Soundtrack', 'Checkpoints', 'EventSystem'];

const keep = new Set();
const why = new Map();
const note = (id, r) => { keep.add(id); why.set(id, r); };

// settings docs
for (const d of docs) if (SETTINGS.includes(head(d).cls)) note(head(d).id, `settings:${head(d).cls}`);

// root objects by name -> their whole component set (not children)
for (const d of docs) {
  const h = head(d);
  if (h.cls !== 1 || h.stripped) continue;
  if (!ROOTS.includes(nameOf(d))) continue;
  note(h.id, `GO:${nameOf(d)}`);
  for (const d2 of docs) {
    const h2 = head(d2);
    if (h2.stripped || h2.cls === 1) continue;
    const owner = field(d2, 'm_GameObject');
    if (owner && owner.match(/\d+/)[0] === h.id) note(h2.id, `comp of ${nameOf(d)}`);
  }
  // audio listener / camera live on the GO; nothing else
}

// prefab instances we want, plus their stripped contents
const WANT_PREFAB = ['Prefabs/Utils/CameraManager.prefab', 'Prefabs/Utils/CanvasUI.prefab', 'Prefabs/Utils/Truck (Player).prefab'];
const instances = new Map(); // instanceId -> path
for (const d of docs) {
  const h = head(d);
  if (h.cls !== 1001) continue;
  const src2 = /m_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-f]{32})/.exec(d);
  const p = src2 ? guids.get(src2[2]) || src2[2] : '?';
  if (WANT_PREFAB.includes(p)) { note(h.id, 'prefab:' + p); instances.set(h.id, p); }
}
for (const d of docs) {
  const h = head(d);
  if (h.cls === 1001) continue;
  const inst = /m_PrefabInstance: \{fileID: (\d+)\}/.exec(d);
  if (inst && instances.has(inst[1])) note(h.id, 'stripped of ' + instances.get(inst[1]));
}

// report
console.log(`source: ${src}`);
console.log(`docs total ${docs.length}, kept ${keep.size}`);
const byClass = new Map();
for (const id of keep) {
  const c = head(byId.get(id)).cls;
  byClass.set(c, (byClass.get(c) || 0) + 1);
}
console.log('kept by class:', [...byClass].sort((a, b) => a[0] - b[0]).map(([c, n]) => `${c}:${n}`).join(' '));
console.log('\nkept non-stripped docs:');
for (const id of keep) {
  const h = head(byId.get(id));
  if (h.stripped) continue;
  let extra = '';
  if (h.cls === 1) extra = nameOf(byId.get(id));
  if (h.cls === 114) {
    const s = field(byId.get(id), 'm_Script');
    extra = guids.get(s.match(/guid: ([0-9a-f]{32})/)[1]) || s.match(/guid: ([0-9a-f]{32})/)[1];
  }
  if (h.cls === 1001) extra = why.get(id);
  if (h.cls === 104) extra = field(byId.get(id), 'm_SkyboxMaterial');
  console.log(`  ${String(id).padStart(12)} cls${String(h.cls).padStart(4)} ${extra}`);
}

fs.mkdirSync(path.dirname(out), { recursive: true });
const body = docs.filter(d => keep.has(head(d).id));
fs.writeFileSync(out, body.join(''), 'utf8');
console.log(`\nwrote ${out} (${body.length} docs, ${body.join('').split('\n').length} lines)`);
fs.writeFileSync('.checktmp/out/core-header.txt', header, 'utf8');
console.log(`header written (${header.split('\n').length} lines)`);
