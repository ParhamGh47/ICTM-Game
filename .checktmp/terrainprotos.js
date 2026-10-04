// Which prefabs does each terrain use as tree prototypes?
//
// The TerrainData assets are serialized in Unity's binary format, so the prototype list can't be read as
// text - but a reference to another asset is stored as its guid, which is a plain hex string inside the
// binary. So: read the asset as bytes and look for each candidate prefab's guid.
//
// Run: node .checktmp/terrainprotos.js
const fs = require('fs');
const path = require('path');

// every guid -> asset path
const assetByGuid = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== 'Library') walk(p); continue; }
    if (!e.name.endsWith('.prefab.meta')) continue;
    const guid = (fs.readFileSync(p, 'utf8').match(/^guid:\s*([0-9a-f]+)/m) || [])[1];
    if (guid) assetByGuid.set(guid, p.replace(/\.meta$/, ''));
  }
})('Assets');

const terrains = [];
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p);
    else if (/\.asset$/.test(e.name)) terrains.push(p);
  }
})('Assets/Terrains');

for (const file of terrains.concat(fs.existsSync('Assets/New Terrain.asset') ? ['Assets/New Terrain.asset'] : [])) {
  const bytes = fs.readFileSync(file);
  const text = bytes.toString('latin1');

  const hits = [];
  for (const [guid, assetPath] of assetByGuid) {
    if (text.includes(guid)) hits.push(assetPath);
  }

  console.log('\n=== ' + file + '  (' + bytes.length + ' bytes) ===');
  if (hits.length === 0) console.log('   (no prefab references found)');
  for (const h of hits.sort()) console.log('   ' + h);
}
