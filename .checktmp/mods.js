// Print a PrefabInstance doc's modifications as target.fileID :: path = value | objectReference
const fs = require('fs');
const path = require('path');
const { loadDocs, head, guidMap, ROOT } = require('./lcore');

const file = process.argv[2];
const which = process.argv[3];
const { docs } = loadDocs(path.join(ROOT, file));
const guids = guidMap();

for (const d of docs) {
  const h = head(d);
  if (h.cls !== 1001) continue;
  const src = /m_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-f]{32})/.exec(d);
  const p = src ? guids.get(src[2]) || src[2] : '?';
  if (which && !p.includes(which)) continue;
  console.log(`### ${h.id} ${p}`);
  console.log('    parent: ' + (/m_TransformParent: (.*)/.exec(d) || [])[1]);

  const lines = d.replace(/\r/g, '').split('\n');
  let cur = null;
  for (const l of lines) {
    let m = /^ {4,7}-? ?target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d+)\}/.exec(l);
    if (m) { cur = { t: m[1] }; continue; }
    if (!cur) continue;
    m = /^ {4,8}propertyPath: (.*)$/.exec(l);
    if (m) { cur.path = m[1]; continue; }
    m = /^ {4,8}value: ?(.*)$/.exec(l);
    if (m) { cur.value = m[1]; continue; }
    m = /^ {4,8}objectReference: \{fileID: (-?\d+)\}/.exec(l);
    if (m) {
      cur.ref = m[1];
      console.log(`  ${cur.t.padStart(20)}  ${cur.path} = ${cur.value || ''}${cur.ref && cur.ref !== '0' ? '  --> scene obj ' + cur.ref : ''}`);
      cur = null;
    }
  }
  console.log();
}
