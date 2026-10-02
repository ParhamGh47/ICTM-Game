// Checks that each prefab still parses cleanly and that ImpactMaterial sits on the root GameObject with the
// kind we intended. Reads the component list of the root, finds the component doc by its fileID, and prints
// what it says.
const fs = require('fs');
const path = require('path');

const guid = '3f7c1d90ab5e4c2f8d61b7a4e05c93d2';

// 0 Solid, 1 Sheet, 2 Panel, 3 Barrel - or null for "must have no marker at all, so it takes the world's
// own recording": that is what a car does now.
const targets = [
  ['Assets/Prefabs/Obstacles/Rigids/Barells.prefab', 3],
  ['Assets/Prefabs/Obstacles/Rigids/Cone.prefab', 3],
  ['Assets/Prefabs/Obstacles/Rigids/Garbage.prefab', 1],
  ['Assets/Prefabs/Obstacles/Rigids/Log.prefab', 1],
  ['Assets/Prefabs/Obstacles/Rigids/TrashContainer.prefab', 1],
  ['Assets/Prefabs/Obstacles/Rigids/box.prefab', 3],
  ['Assets/Prefabs/Signs/Signs/Blinder.prefab', 1],
  ['Assets/Prefabs/Signs/Signs/ShareTheRoad.prefab', 2],
  ['Assets/Prefabs/Signs/Signs/Stop.prefab', 2],
  ['Assets/Prefabs/Signs/Bilboards/Billboard.prefab', 2],
  ['Assets/Prefabs/Signs/Lights/Light.prefab', 2],
  ['Assets/Prefabs/Signs/Lights/Light Off.prefab', 2],
  ['Assets/Prefabs/Obstacles/Mast.prefab', 2],
  ['Assets/Prefabs/Cars/206/206.prefab', null],
  ['Assets/Prefabs/Cars/911/911.prefab', null],
  ['Assets/Prefabs/Cars/Car/Car.prefab', null],
  ['Assets/Prefabs/Cars/Car/Truck.prefab', null],
];

let bad = 0;

for (const [file, want] of targets) {
  const raw = fs.readFileSync(path.join(process.cwd(), file), 'utf8').replace(/\r\n/g, '\n');

  const docs = [];
  let cur = null;
  for (const line of raw.split('\n')) {
    const m = line.match(/^--- !u!(\d+) &(\d+)/);
    if (m) { if (cur) docs.push(cur); cur = { c: +m[1], id: m[2], l: [] }; continue; }
    if (cur) cur.l.push(line);
  }
  if (cur) docs.push(cur);

  const mat = docs.find((d) => d.l.join('\n').includes(guid));

  if (want === null) {
    // Must be gone entirely - both the document and the entry that lists it.
    const ok = !mat && !raw.includes(guid);
    if (!ok) bad++;

    console.log(`${ok ? 'ok  ' : 'FAIL'} ${file.padEnd(48)} no ImpactMaterial (docs=${docs.length})`);
    continue;
  }

  if (!mat) { console.log(`FAIL ${file}: no ImpactMaterial`); bad++; continue; }

  const goId = (mat.l.join('\n').match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1];
  const kind = (mat.l.join('\n').match(/^\s*kind:\s*(\d+)\s*$/m) || [])[1];

  const go = docs.find((d) => d.c === 1 && d.id === goId);
  if (!go) { console.log(`FAIL ${file}: ImpactMaterial points at GameObject ${goId}, which is not in the file`); bad++; continue; }

  const name = (go.l.join('\n').match(/m_Name:\s*(.*)/) || [])[1];

  // is it the root? the root's transform has m_Father 0
  const tr = docs.filter((d) => d.c === 4 && (d.l.join('\n').match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1] === goId)[0];
  const isRoot = tr && /m_Father:\s*\{fileID:\s*0\}/.test(tr.l.join('\n'));

  // is it listed in the GameObject's own component list?
  const listed = new RegExp(`^  - component: \\{fileID: ${mat.id}\\}$`, 'm').test(go.l.join('\n'));

  const ok = kind === String(want) && isRoot && listed;
  if (!ok) bad++;

  console.log(`${ok ? 'ok  ' : 'FAIL'} ${file.padEnd(48)} root='${name}'  kind=${kind} (want ${want})  isRoot=${!!isRoot}  listed=${listed}  docs=${docs.length}`);
}

console.log(`\n${bad} problem(s)`);
