// Does Core-1's terrain even have grass detail and trees? detailObjectDistance and friends only do anything if
// the terrain has detail prototypes, so this finds which prefab GUIDs the terrain asset holds.
const fs = require('fs');
const path = require('path');

const root = process.cwd();

function guidsIn(dir) {
  const out = [];
  if (!fs.existsSync(dir)) return out;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) { out.push(...guidsIn(full)); continue; }
    if (!entry.name.endsWith('.prefab.meta') && !entry.name.endsWith('.png.meta') && !entry.name.endsWith('.asset.meta')) continue;

    const text = fs.readFileSync(full, 'utf8');
    const m = text.match(/^guid: ([0-9a-f]{32})/m);
    if (m) out.push({ name: entry.name.replace(/\.meta$/, ''), guid: m[1], dir });
  }
  return out;
}

function hexToBytes(hex, swap) {
  const raw = Buffer.from(hex, 'hex');
  if (!swap) return raw;
  const out = Buffer.alloc(16);
  out.writeUInt32BE(raw.readUInt32LE(0), 0);
  out.writeUInt32BE(raw.readUInt32LE(4), 4);
  out.writeUInt32BE(raw.readUInt32LE(8), 8);
  out.writeUInt32BE(raw.readUInt32LE(12), 12);
  return out;
}

const terrain = fs.readFileSync('Assets/Terrains/New Terrain 5.asset');

const groups = [
  ['trees', guidsIn('Assets/Prefabs/Trees')],
  ['detail (grass)', guidsIn('Assets/Prefabs/DetailsTerrain')],
];

for (const [label, list] of groups) {
  let found = 0;
  const names = [];
  for (const g of list) {
    const raw = terrain.includes(hexToBytes(g.guid, false));
    const swapped = terrain.includes(hexToBytes(g.guid, true));
    if (raw || swapped) {
      found++;
      names.push(g.name + (raw ? '' : ' (swapped)'));
    }
  }
  console.log(label + ': ' + found + ' of ' + list.length + ' referenced by the terrain');
  if (names.length) console.log('   ' + names.slice(0, 12).join(', '));
}

// Sanity check the byte order with something the terrain must hold: the terrain's own material?
for (const probe of ['Assets/Materials', 'Assets/Materials/Terrain', 'Assets/Terrains']) {
  if (!fs.existsSync(probe)) continue;
  const list = guidsIn(probe);
  let found = 0;
  for (const g of list) {
    if (terrain.includes(hexToBytes(g.guid, false)) || terrain.includes(hexToBytes(g.guid, true))) found++;
  }
  console.log(probe + ': ' + found + ' of ' + list.length + ' referenced');
}
