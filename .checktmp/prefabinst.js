// Find PrefabInstance docs whose source prefab guid matches, and print the root's modified name/position.
const fs = require('fs');
const path = require('path');
const { read, splitDocs } = require('./scenelib');

const file = process.argv[2];
const guid = process.argv[3];
const root = path.resolve(__dirname, '..');
const { docs } = splitDocs(read(path.join(root, file)));

for (const d of docs) {
  if (d.cls !== '1001') continue;
  const m = d.body.match(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)/);
  if (!m || m[1] !== guid) continue;

  const name = (d.body.match(/propertyPath: m_Name\n\s*value: (.*)/) || [])[1] || '(instance)';
  const posOf = (axis) => {
    const t = d.body.match(new RegExp('propertyPath: m_LocalPosition\\.' + axis + '\\n\\s*value: ([^\\n]+)'));
    return t ? t[1].trim() : '0';
  };
  console.log(`[${name}] (${posOf('x')}, ${posOf('y')}, ${posOf('z')})`);
}
