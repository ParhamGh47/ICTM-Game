// Lists the CameraManager prefab's virtual cameras: name, the Cinemachine3rdPersonFollow's CameraDistance,
// ShoulderOffset and VerticalArmLength, and the CameraController's Cameras[] order. Then applies any
// prefab-instance overrides found in a scene, so we know what each index actually is in a level.
const fs = require('fs');
const path = require('path');

const rigGuid = '9de7ea8ebea5b664bb865e6f78009de3';
const prefabPath = 'Assets/Prefabs/Utils/CameraManager.prefab';
const scenePath = process.argv[2] || 'Assets/Scenes/Levels Scenes/1/Core-1.unity';

function parse(file) {
  const raw = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
  const docs = [];
  let cur = null;
  for (const line of raw.split('\n')) {
    const m = line.match(/^--- !u!(\d+) &(\d+)(.*)$/);
    if (m) { if (cur) docs.push(cur); cur = { classId: +m[1], fileId: +m[2], lines: [] }; continue; }
    if (cur) cur.lines.push(line);
  }
  if (cur) docs.push(cur);
  return docs;
}

function goName(docs) {
  const map = new Map();
  for (const d of docs) if (d.classId === 1) {
    const m = d.lines.join('\n').match(/m_Name:\s*(.*)/);
    map.set(d.fileId, m ? m[1].trim() : '');
  }
  return map;
}
const flat = (d) => d.lines.join('\n');
const goOf = (d) => { const m = flat(d).match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/); return m ? +m[1] : null; };
const scriptOf = (d) => { const m = flat(d).match(/m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]+)/); return m ? m[1] : null; };

const docs = parse(prefabPath);
const names = goName(docs);

// the CameraController component: its Cameras[] list
let camOrder = null;
for (const d of docs) {
  if (d.classId !== 114) continue;
  const t = flat(d);
  const m = t.match(/^  Cameras:\n((?:  - \{fileID: -?\d+\}\n)+)/m);
  if (m) { camOrder = [...m[1].matchAll(/fileID: (-?\d+)/g)].map((x) => +x[1]); break; }
}

function followOf(goId) {
  for (const d of docs) {
    if (d.classId !== 114) continue;
    if (goOf(d) !== goId) continue;
    const t = flat(d);
    if (!/ShoulderOffset|VerticalArmLength/.test(t)) continue;
    const dist = t.match(/CameraDistance:\s*(-?[\d.]+)/);
    const sh = t.match(/ShoulderOffset:\s*\{x:\s*(-?[\d.]+),\s*y:\s*(-?[\d.]+),\s*z:\s*(-?[\d.]+)\}/);
    const arm = t.match(/VerticalArmLength:\s*(-?[\d.]+)/);
    return {
      dist: dist ? +dist[1] : null,
      shoulder: sh ? +sh[2] : null,
      arm: arm ? +arm[1] : null,
      fileId: d.fileId,
    };
  }
  return null;
}
function lensOf(goId) {
  for (const d of docs) {
    if (d.classId !== 114) continue;
    if (goOf(d) !== goId) continue;
    const t = flat(d);
    const f = t.match(/FieldOfView:\s*(-?[\d.]+)/);
    if (f) return +f[1];
  }
  return null;
}

console.log(`CameraManager prefab: ${docs.length} objects`);
console.log('Cameras[] order and what each is:');
if (camOrder) camOrder.forEach((id, i) => {
  console.log(`  [${i}] ${names.get(id) || '?'}`);
});
// the parent GameObject of each transform, so a pipeline component's "cm" child can be traced to its vcam
const trOf = new Map();
for (const d of docs) if (d.classId === 4) trOf.set(d.fileId, d);
const goOfTransform = (d) => goOf(d);
const parentName = (goId) => {
  for (const d of docs) {
    if (d.classId !== 4) continue;
    if (goOfTransform(d) !== goId) continue;
    const m = flat(d).match(/m_Father:\s*\{fileID:\s*(-?\d+)\}/);
    const pid = m ? +m[1] : 0;
    const p = trOf.get(pid);
    return p ? (names.get(goOfTransform(p)) || '?') : '(root)';
  }
  return '?';
};

console.log('\nPipeline (3rd person follow) settings, by the camera they belong to:');
for (const d of docs) {
  if (d.classId !== 114) continue;
  const t = flat(d);
  if (!/ShoulderOffset|VerticalArmLength/.test(t)) continue;
  const go = goOf(d);
  const dist = t.match(/CameraDistance:\s*(-?[\d.]+)/);
  const sh = t.match(/ShoulderOffset:\s*\{x:\s*(-?[\d.]+),\s*y:\s*(-?[\d.]+),\s*z:\s*(-?[\d.]+)\}/);
  const arm = t.match(/VerticalArmLength:\s*(-?[\d.]+)/);
  const cam = camOrder ? camOrder.findIndex((id) => id === go) : -1;
  console.log(`  ${(parentName(go)).padEnd(18)} on '${names.get(go)}' dist=${dist ? dist[1] : '?'} shoulderY=${sh ? sh[2] : '?'} arm=${arm ? arm[1] : '?'}${cam >= 0 ? `  << Cameras[${cam}]` : ''}`);
}

// and which Cameras[] entry each pipeline belongs to
if (camOrder) {
  console.log('\nCameras[] entries:');
  camOrder.forEach((id, i) => console.log(`  [${i}] ${names.get(id)} (go ${id})`));
}

// scene overrides
const sdocs = parse(scenePath);
console.log(`\n--- overrides in ${scenePath} ---`);
for (const d of sdocs) {
  const t = flat(d);
  if (!/CameraDistance|ShoulderOffset|VerticalArmLength/.test(t)) continue;
  const src = t.match(/guid:\s*([0-9a-f]+)/);
  if (!src || src[1] !== rigGuid) continue;
  const paths = [...t.matchAll(/fileID:\s*(\d+),\s*guid:\s*[0-9a-f]+,\s*type:\s*3\}\n\s*propertyPath:\s*([\w.]+)\n\s*value:\s*([^\n]*)/g)];
  for (const p of paths) console.log(`  target ${p[1]}  ${p[2]} = ${p[3]}`);
}
