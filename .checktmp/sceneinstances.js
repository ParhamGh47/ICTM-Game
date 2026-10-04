// What prefabs does a scene actually contain? Counts every prefab-instance source guid in the scene and
// resolves the guids to asset paths, so a claim like "the trees are scene objects" can be checked rather
// than assumed.
//
// Run: node .checktmp/sceneinstances.js <scene> [scene ...]
const fs = require('fs');
const path = require('path');

// guid -> asset path, from every .meta under Assets
const assetByGuid = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== 'Library') walk(p); continue; }
    if (!e.name.endsWith('.meta')) continue;
    const guid = (fs.readFileSync(p, 'utf8').match(/^guid:\s*([0-9a-f]+)/m) || [])[1];
    if (guid) assetByGuid.set(guid, p.replace(/\.meta$/, ''));
  }
})('Assets');

for (const scene of process.argv.slice(2)) {
  const raw = fs.readFileSync(path.join(process.cwd(), scene), 'utf8').replace(/\r\n/g, '\n');

  const counts = new Map();
  for (const m of raw.matchAll(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+), type: 3\}/g)) {
    counts.set(m[1], (counts.get(m[1]) || 0) + 1);
  }

  // The scene's own root GameObjects are not instances and are not counted here.
  console.log('\n=== ' + scene + '  (' + counts.size + ' distinct prefabs) ===');
  const rows = [...counts.entries()].sort((a, b) => b[1] - a[1]);
  for (const [guid, n] of rows) {
    const p = assetByGuid.get(guid) || '(unknown guid ' + guid.slice(0, 8) + ')';
    console.log(String(n).padStart(6) + '  ' + p);
  }
}
