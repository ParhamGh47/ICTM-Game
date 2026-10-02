// Removes the ImpactMaterial component from the car prefabs, so a car answers with the world's own impact
// recording again, exactly like a tree or a building. Both halves have to go: the MonoBehaviour document
// itself, and the "- component:" line that lists it on the root GameObject - a dangling entry there would
// leave Unity reporting a missing script.
//
// File IDs in these files are int64 and exceed JS's safe integer range, so every id is handled as a string.
// Dry run unless --apply is passed.
const fs = require('fs');
const path = require('path');

const apply = process.argv.includes('--apply');
const guid = '3f7c1d90ab5e4c2f8d61b7a4e05c93d2';   // ImpactMaterial.cs

const targets = [
  'Assets/Prefabs/Cars/206/206.prefab',
  'Assets/Prefabs/Cars/911/911.prefab',
  'Assets/Prefabs/Cars/Car/Car.prefab',
  'Assets/Prefabs/Cars/Car/Truck.prefab',
];

let problems = 0;

for (const file of targets) {
  const full = path.join(process.cwd(), file);
  const raw = fs.readFileSync(full, 'utf8');
  const nl = raw.includes('\r\n') ? '\r\n' : '\n';
  const lines = raw.replace(/\r\n/g, '\n').split('\n');

  // The document header that owns the ImpactMaterial script.
  const header = /^--- !u!114 &(-?\d+)(.*)$/;
  let blockStart = -1;
  let id = null;

  for (let i = 0; i < lines.length; i++) {
    const m = lines[i].match(header);
    if (!m) continue;

    // the body of this document, up to the next header (or EOF)
    let end = i + 1;
    while (end < lines.length && !header.test(lines[end])) end++;
    if (!lines.slice(i, end).some((l) => l.includes('guid: ' + guid))) continue;

    blockStart = i;
    id = m[1];
    break;
  }

  if (blockStart < 0) { console.log(`SKIP  ${file} (no ImpactMaterial)`); continue; }

  let blockEnd = blockStart + 1;
  while (blockEnd < lines.length && !header.test(lines[blockEnd])) blockEnd++;

  const listLine = `  - component: {fileID: ${id}}`;
  const listAt = lines.indexOf(listLine, 0);

  if (listAt < 0 || listAt >= blockStart) { console.log(`PROBLEM ${file}: no m_Component entry for ${id}`); problems++; continue; }

  console.log(`${apply ? 'REMOVE' : 'would remove'} ${file}  block &${id} (lines ${blockStart + 1}-${blockEnd}), list line ${listAt + 1}`);

  if (apply) {
    // The list line comes first in the file (documents are objects before components), so splicing the
    // block - the later index - leaves listAt valid.
    lines.splice(blockStart, blockEnd - blockStart);
    lines.splice(listAt, 1);

    let out = lines.join('\n');
    if (!out.endsWith('\n')) out += '\n';

    fs.writeFileSync(full, out.split('\n').join(nl));
  }
}

console.log(`\n${problems} problem(s)`);
