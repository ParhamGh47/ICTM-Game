// What the painted scenes actually hold. The cars are prefab instances, so attribution is by the car
// prefabs' guids, not by the controller script: for every instance of a car prefab in every scene, is its
// lightsOn overridden, and what do its light overrides say?
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');

function walk(dir, out) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else out.push(p);
  }
  return out;
}

// Every prefab under Assets/Prefabs/Cars, by guid, so an instance can be named.
const carGuids = new Map();
for (const p of walk(path.join(root, 'Assets/Prefabs/Cars'), [])) {
  if (!p.endsWith('.prefab')) continue;
  const meta = p + '.meta';
  if (!fs.existsSync(meta)) continue;
  const m = fs.readFileSync(meta, 'utf8').match(/guid:\s*([0-9a-f]+)/);
  if (m) carGuids.set(m[1], path.basename(p, '.prefab'));
}
console.log('car prefabs: ' + [...carGuids.values()].join(', ') + '\n');

const scenes = walk(path.join(root, 'Assets/Scenes'), []).filter((p) => p.endsWith('.unity'));

for (const scene of scenes) {
  const lines = fs.readFileSync(scene, 'utf8').split(/\r?\n/);
  const name = path.relative(root, scene);

  const counts = new Map();
  let onOverrides = 0;
  let offOverrides = 0;

  for (let i = 0; i < lines.length; i++) {
    if (!lines[i].startsWith('--- !u!1001')) continue;

    // Read the instance block: from here to the next document.
    let end = i + 1;
    while (end < lines.length && !lines[end].startsWith('--- !u!')) end++;
    const block = lines.slice(i, end);

    const src = block.find((l) => /m_SourcePrefab:/.test(l));
    if (!src) continue;
    const g = src.match(/guid:\s*([0-9a-f]+)/);
    if (!g || !carGuids.has(g[1])) continue;

    const key = carGuids.get(g[1]);
    counts.set(key, (counts.get(key) || 0) + 1);

    for (let j = 0; j < block.length; j++) {
      const m = block[j].match(/^\s*propertyPath:\s*(lightsOn|m_Enabled)\s*$/);
      if (!m) continue;
      const value = (block[j + 1] || '').trim();
      if (m[1] === 'lightsOn') {
        if (/value:\s*0/.test(value)) offOverrides++;
        else onOverrides++;
        if (offOverrides <= 3) console.log('  ' + name + ': lightsOn override -> ' + value);
      }
    }
  }

  if (counts.size === 0) continue;
  const total = [...counts.values()].reduce((a, b) => a + b, 0);
  console.log(
    name + ' :: ' + total + ' cars (' +
      [...counts.entries()].map(([k, v]) => k + ' x' + v).join(', ') +
      ') lightsOn overrides: ' + onOverrides + ' on / ' + offOverrides + ' off'
  );
}
