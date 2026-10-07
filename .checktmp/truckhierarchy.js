// Where CarController and EngineAudio sit in the player truck prefab, and the mass/gears it ships.
const fs = require('fs');
const path = process.argv[2] || 'Assets/Prefabs/Utils/Truck (Player).prefab';
const text = fs.readFileSync(path, 'utf8');

const docs = {};
const ids = [];
const chunks = text.split(/^--- /m).slice(1);
for (const chunk of chunks) {
  const m = chunk.match(/^!u!(\d+) &(\d+)(.*)$/m);
  if (!m) continue;
  const id = m[2];
  ids.push(id);
  docs[id] = { cls: m[1], body: chunk };
}

function name(id) {
  const d = docs[id];
  if (!d) return null;
  const n = d.body.match(/^  m_Name: (.*)$/m);
  return n ? n[1] : '';
}

// children of each transform
const transformChildren = {};
for (const id of ids) {
  const d = docs[id];
  if (d.cls !== '4') continue;                 // !u!4 Transform
  const parent = d.body.match(/m_Father: \{fileID: (\d+)\}/);
  transformChildren[parent ? parent[1] : '0'] = transformChildren[parent ? parent[1] : '0'] || [];
  transformChildren[parent ? parent[1] : '0'].push(id);
}
const goOf = {};
for (const id of ids) {
  const d = docs[id];
  if (d.cls !== '1') continue;                 // !u!1 GameObject
  const comps = [...d.body.matchAll(/component: \{fileID: (\d+)\}/g)].map(x => x[1]);
  for (const c of comps) goOf[c] = id;
}

const guids = { CarController: '963b32074d7570b44a181a43be3db3f0', EngineAudio: '3f88875c240bb144489d115350471f5a' };
for (const id of ids) {
  const d = docs[id];
  if (d.cls !== '114') continue;               // MonoBehaviour
  const g = (d.body.match(/guid: ([0-9a-f]+)/) || [])[1];
  const which = Object.keys(guids).find(k => guids[k] === g);
  if (!which) continue;
  const go = goOf[id];
  const t = (docs[go].body.match(/component: \{fileID: (\d+)\}/g) || []).map(x => x.match(/\d+/)[0])
    .find(c => docs[c] && docs[c].cls === '4');
  console.log(`${which}: on GameObject "${name(go)}" (id ${go})`);
}

// mass + gear fields
for (const id of ids) {
  const d = docs[id];
  if (d.cls === '54') {                        // Rigidbody
    console.log(`Rigidbody on "${name(goOf[id])}": mass=${(d.body.match(/m_Mass: ([\d.]+)/) || [])[1]}`);
  }
  if (d.cls === '114' && /gearRatios/.test(d.body)) {
    const ratios = (d.body.match(/gearRatios: ([^\n]*)/) || [])[1];
    const top = (d.body.match(/shiftUpRPM: ([^\n]*)/) || [])[1];
    console.log(`EngineAudio on "${name(goOf[id])}": gearRatios=${ratios} shiftUpRPM=${top}`);
  }
}

// full tree names
function tree(id, depth) {
  const d = docs[id];
  if (!d || d.cls !== '4') return;
  const go = goOf[id];
  console.log('  '.repeat(depth) + '- ' + name(go));
  for (const c of transformChildren[id] || []) tree(c, depth + 1);
}
for (const rootId of transformChildren['0'] || []) tree(rootId, 0);
