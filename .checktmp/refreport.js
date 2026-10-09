const path = require('path');
const ROOT = path.resolve(__dirname, '..');
const { read, splitDocs, index, refsIn, nameOf } = require('./scenelib');

const file = path.join(ROOT, 'Assets/Scenes/Levels Scenes/3/Core-3.unity');
const { docs } = splitDocs(read(file));
const byId = index(docs);

const want = {
  '208287291': 'CameraManager',
  '6890820110328195566': 'CanvasUI',
  '1932669414': 'Truck(Player)',
  '2006328015': 'Finish',
};

const strippedOf = new Map();
for (const d of docs) {
  const inst = (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
  if (!inst) continue;
  if (!strippedOf.has(inst)) strippedOf.set(inst, []);
  strippedOf.get(inst).push(d);
}

for (const [inst, label] of Object.entries(want)) {
  const own = new Set([inst]);
  const strips = strippedOf.get(inst) || [];
  for (const s of strips) own.add(s.id);
  const refs = new Set();
  for (const d of [byId.get(inst), ...strips]) {
    for (const r of refsIn(d.body)) if (!own.has(r)) refs.add(r);
  }
  console.log('\n=== ' + label + ' (inst ' + inst + ', stripped docs: ' + strips.length + ')');
  for (const r of [...refs].sort((a, b) => a - b)) {
    const d = byId.get(r);
    console.log('   ' + String(r).padEnd(22) + 'cls=' + (d ? d.cls : '?') +
      '  ' + (d ? JSON.stringify(nameOf(d)) : '(MISSING)') +
      (d && d.stripped ? '  [stripped, inst ' + ((d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1] || '?') + ']' : ''));
  }
}
