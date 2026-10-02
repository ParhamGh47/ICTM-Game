// Sets the ImpactMaterial kind on each prefab to the new mapping. Only rewrites the kind line inside the
// ImpactMaterial block. Dry run unless --apply.
const fs = require('fs');
const path = require('path');

const apply = process.argv.includes('--apply');
const guid = '3f7c1d90ab5e4c2f8d61b7a4e05c93d2';

// 0 Solid, 1 Sheet, 2 Panel, 3 Barrel, 4 Car
const targets = [
  ['Assets/Prefabs/Signs/Signs/Blinder.prefab', 1],
  ['Assets/Prefabs/Obstacles/Rigids/Log.prefab', 1],
  ['Assets/Prefabs/Obstacles/Rigids/Garbage.prefab', 1],
  ['Assets/Prefabs/Obstacles/Rigids/TrashContainer.prefab', 1],
  ['Assets/Prefabs/Signs/Signs/ShareTheRoad.prefab', 2],
  ['Assets/Prefabs/Signs/Signs/Stop.prefab', 2],
  ['Assets/Prefabs/Obstacles/Rigids/Barells.prefab', 3],
  ['Assets/Prefabs/Obstacles/Rigids/box.prefab', 3],
  ['Assets/Prefabs/Obstacles/Rigids/Cone.prefab', 3],
  ['Assets/Prefabs/Cars/206/206.prefab', 4],
  ['Assets/Prefabs/Cars/911/911.prefab', 4],
  ['Assets/Prefabs/Cars/Car/Car.prefab', 4],
  ['Assets/Prefabs/Cars/Car/Truck.prefab', 4],
];

let bad = 0;

for (const [file, want] of targets) {
  const full = path.join(process.cwd(), file);
  const raw = fs.readFileSync(full, 'utf8');
  const nl = raw.includes('\r\n') ? '\r\n' : '\n';
  const norm = raw.replace(/\r\n/g, '\n');

  const lines = norm.split('\n');
  const start = lines.findIndex((l) => l.startsWith('--- !u!114 &') && norm.slice(norm.indexOf(l)).split('--- ')[0].includes(guid));
  // simpler: find the block containing the guid, then scan forward to the next '--- ' or EOF
  const gi = lines.findIndex((l) => l.includes('guid: ' + guid));
  if (gi < 0) { console.log(`FAIL ${file}: no ImpactMaterial block`); bad++; continue; }

  let blockStart = gi;
  while (blockStart > 0 && !/^--- !u!\d+ &/.test(lines[blockStart])) blockStart--;

  let blockEnd = gi;
  while (blockEnd < lines.length && (blockEnd === blockStart || !/^--- !u!\d+ &/.test(lines[blockEnd]))) blockEnd++;

  let kindLine = -1;
  for (let i = blockStart; i < blockEnd; i++) if (/^\s*kind:\s*-?\d+\s*$/.test(lines[i])) kindLine = i;

  if (kindLine < 0) { console.log(`FAIL ${file}: no kind line in the block`); bad++; continue; }

  const before = lines[kindLine];
  lines[kindLine] = `  kind: ${want}`;

  console.log(`${apply ? 'set ' : 'would set'} ${file.padEnd(48)} ${before.trim()} -> kind: ${want}`);

  if (apply) fs.writeFileSync(full, lines.join('\n').split('\n').join(nl));
}

console.log(`\n${bad} problem(s)`);
