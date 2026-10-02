// Adds ImpactMaterial to each prefab's root GameObject. 0 Solid, 1 Sheet, 2 Panel, 3 Barrel.
//
// This pass adds the Panel voice (the big flat sign's clang) to the billboards, the road lights and the
// mast. The billboard variant needs no entry of its own: a prefab variant is its base plus overrides, so a
// component added to Billboard.prefab appears on Billboard Variant.prefab the moment Unity reimports.
// Dry run unless --apply is passed.
const fs = require('fs');
const path = require('path');

const apply = process.argv.includes('--apply');
const guid = '3f7c1d90ab5e4c2f8d61b7a4e05c93d2';   // ImpactMaterial.cs

const targets = [
  ['Assets/Prefabs/Signs/Bilboards/Billboard.prefab', 2],
  ['Assets/Prefabs/Signs/Lights/Light.prefab', 2],
  ['Assets/Prefabs/Signs/Lights/Light Off.prefab', 2],
  ['Assets/Prefabs/Obstacles/Mast.prefab', 2],
];

function parse(raw) {
  const docs = [];
  let cur = null;
  for (const line of raw.split('\n')) {
    const m = line.match(/^--- !u!(\d+) &(\d+)(.*)$/);
    if (m) { if (cur) docs.push(cur); cur = { c: +m[1], id: BigInt(m[2]), head: line, l: [] }; continue; }
    if (cur) cur.l.push(line);
  }
  if (cur) docs.push(cur);
  return docs;
}

let problems = 0;

for (const [file, kind] of targets) {
  const full = path.join(process.cwd(), file);
  if (!fs.existsSync(full)) { console.log(`MISSING ${file}`); problems++; continue; }

  const raw = fs.readFileSync(full, 'utf8');
  const nl = raw.includes('\r\n') ? '\r\n' : '\n';
  const norm = raw.replace(/\r\n/g, '\n');

  if (norm.includes(guid)) { console.log(`SKIP   ${file} (already has it)`); continue; }

  const docs = parse(norm);

  // the root: the Transform whose father is fileID 0
  const roots = docs.filter((d) => d.c === 4 && /m_Father:\s*\{fileID:\s*0\}/.test(d.l.join('\n')));
  if (roots.length !== 1) { console.log(`PROBLEM ${file}: ${roots.length} root transforms`); problems++; continue; }

  const goId = (roots[0].l.join('\n').match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1];
  if (!goId) { console.log(`PROBLEM ${file}: root transform has no GameObject`); problems++; continue; }

  const go = docs.find((d) => d.c === 1 && d.id === BigInt(goId));
  if (!go) { console.log(`PROBLEM ${file}: GameObject ${goId} not found`); problems++; continue; }

  const goText = go.l.join('\n');
  const name = (goText.match(/m_Name:\s*(.*)/) || [])[1];
  if (!/m_Component:/.test(goText)) { console.log(`PROBLEM ${file}: root has no m_Component`); problems++; continue; }

  // A fresh id, above everything in the file. BigInt: these are int64 and lose precision as JS numbers.
  const maxId = docs.reduce((a, d) => (d.id > a ? d.id : a), 0n);
  const newId = maxId + 1n;

  // put the component entry at the end of the root's m_Component list
  const lines = norm.split('\n');
  const start = lines.indexOf(go.head);
  let insertAt = -1;
  for (let i = start + 1; i < lines.length; i++) {
    if (/^  m_Component:/.test(lines[i])) {
      let j = i + 1;
      while (j < lines.length && /^  - component: \{fileID: -?\d+\}/.test(lines[j])) j++;
      insertAt = j;
      break;
    }
  }
  if (insertAt < 0) { console.log(`PROBLEM ${file}: could not find where the component list ends`); problems++; continue; }

  const block =
    `--- !u!114 &${newId.toString()}\n` +
    `MonoBehaviour:\n` +
    `  m_ObjectHideFlags: 0\n` +
    `  m_CorrespondingSourceObject: {fileID: 0}\n` +
    `  m_PrefabInstance: {fileID: 0}\n` +
    `  m_PrefabAsset: {fileID: 0}\n` +
    `  m_GameObject: {fileID: ${goId}}\n` +
    `  m_Enabled: 1\n` +
    `  m_EditorHideFlags: 0\n` +
    `  m_Script: {fileID: 11500000, guid: ${guid}, type: 3}\n` +
    `  m_Name: \n` +
    `  m_EditorClassIdentifier: \n` +
    `  kind: ${kind}\n`;

  lines.splice(insertAt, 0, `  - component: {fileID: ${newId.toString()}}`);

  let out = lines.join('\n');
  if (!out.endsWith('\n')) out += '\n';
  out += block;

  console.log(`${apply ? 'ADD ' : 'would add'}  ${file}  kind=${kind}  root='${name}' (fileID ${goId})  new=${newId}`);

  if (apply) fs.writeFileSync(full, out.split('\n').join(nl));
}

console.log(`\n${problems} problem(s)`);
