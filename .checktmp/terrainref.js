// Which tree prefabs are the terrain's live prototypes?
//
// A TerrainData asset is serialized in Unity's binary format, and a reference to another asset is stored in
// the file's external-reference table as the target's guid in **raw 16-byte form**, not as the hex string
// from the .meta. A .NET Guid's bytes are the first three groups little-endian, so "5e576525c5539054..."
// is stored as 25 65 57 5e 53 c5 54 90 99 61 4d 2a 5e b0 fe df.
//
// Both orderings are tried, so a match is found whichever way Unity wrote it.
//
// Run: node .checktmp/terrainref.js <terrain.asset> [terrain.asset ...]
const fs = require('fs');
const path = require('path');

function metaBytes(hex, littleEndianGroups) {
  const b = [];
  for (let i = 0; i < 32; i += 2) b.push(parseInt(hex.slice(i, i + 2), 16));
  if (!littleEndianGroups) return Buffer.from(b);

  // groups are 4, 2, 2, then 8 bytes as-is
  const out = [];
  for (let i = 3; i >= 0; i--) out.push(b[i]);
  for (let i = 4; i <= 5; i++) out.push(b[7 - (i - 4)]);
  for (let i = 6; i <= 7; i++) out.push(b[9 - (i - 6)]);
  for (let i = 8; i < 16; i++) out.push(b[i]);
  return Buffer.from(out);
}

// guid -> prefab path
const prefabs = [];
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith('.prefab.meta')) {
      const guid = (fs.readFileSync(p, 'utf8').match(/^guid:\s*([0-9a-f]+)/m) || [])[1];
      if (guid) prefabs.push({ guid, path: p.replace(/\.meta$/, '') });
    }
  }
})('Assets/Prefabs');

for (const asset of process.argv.slice(2)) {
  const data = fs.readFileSync(asset);
  console.log('\n=== ' + asset + ' ===');

  let found = 0;
  for (const { guid, path: pf } of prefabs) {
    for (const le of [true, false]) {
      if (data.indexOf(metaBytes(guid, le)) >= 0) {
        console.log('   ' + pf + (le ? '' : '   (plain byte order)'));
        found++;
        break;
      }
    }
  }
  if (found === 0) console.log('   (no prefab references found)');
}
