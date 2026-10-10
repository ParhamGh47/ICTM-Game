// Sanity-check the generated shadow prefab: every internal reference resolves, no scripts survive, and
// list the objects and nested prefab instances.
const fs = require('fs');
const path = require('path');
const { read, splitDocs, index, nameOf, refsIn } = require('./scenelib');

const root = path.resolve(__dirname, '..');
const file = path.join(root, 'Assets/Prefabs/Utils/Shadow Truck.prefab');
const { docs } = splitDocs(read(file));
const byId = index(docs);

const ids = new Set(docs.map((d) => d.id));
let dangling = 0;
for (const d of docs) {
  for (const r of refsIn(d.body)) {
    if (!ids.has(r)) { dangling++; if (dangling <= 10) console.log('  dangling in !u!' + d.cls + ' &' + d.id + ' -> ' + r); }
  }
}

// names of GameObjects
const names = docs.filter((d) => d.cls === '1').map((d) => nameOf(d));

// nested prefabs
const nested = docs.filter((d) => d.cls === '1001');
for (const n of nested) {
  const src = n.body.match(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)/);
  console.log('nested prefab: ' + (src ? src[1] : '?'));
}

console.log('docs: ' + docs.length + '  dangling refs: ' + dangling);
console.log('scripts left: ' + docs.filter((d) => d.cls === '114').length);
console.log('GameObjects: ' + names.length);
console.log(names.join(', '));
