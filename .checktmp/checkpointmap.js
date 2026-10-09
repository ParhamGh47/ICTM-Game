// Each compass checkpoint is literally the Checkpoint trigger's own transform (a stripped transform
// belonging to the trigger's PrefabInstance). So verify the identity mapping and the index order.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const TRIGGER_GUID = '71faf8cb724d0af4f99404929ca705a9';
const MAIN_TRANSFORM = '2214681332534666674';

function read(file) { return fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n'); }

function analyze(file) {
  const text = read(file);

  // instance id -> stripped transform id whose source object is the trigger's main transform
  const strippedBy = new Map();
  const re = /--- !u!4 &(\d+) stripped\nTransform:\n\s*m_CorrespondingSourceObject: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\n\s*m_PrefabInstance: \{fileID: (\d+)\}/g;
  let m;
  while ((m = re.exec(text))) {
    if (m[2] === MAIN_TRANSFORM && m[3] === TRIGGER_GUID) strippedBy.set(m[4], m[1]);
  }

  // instance id -> name, and the trigger's local position/parent
  const info = new Map();
  const re2 = /--- !u!1001 &(\d+)\nPrefabInstance:\n([\s\S]*?)m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/g;
  while ((m = re2.exec(text))) {
    if (m[3] !== TRIGGER_GUID) continue;
    const block = m[2];
    const name = (block.match(/propertyPath: m_Name\n\s*value: ([^\n]*)/) || [])[1];
    info.set(m[1], { name, hasPos: /propertyPath: m_LocalPosition\.x/.test(block) });
  }

  const arraySize = parseInt((text.match(/propertyPath: checkpoints\.Array\.size\n\s*value: (\d+)/) || [])[1], 10);
  const arrayIds = [];
  for (let i = 0; i < arraySize; i++) {
    const mm = text.match(new RegExp('propertyPath: checkpoints\\.Array\\.data\\[' + i + '\\]\\n\\s*value: \\n\\s*objectReference: \\{fileID: (\\d+)\\}'));
    arrayIds.push(mm ? mm[1] : null);
  }

  const indexOfTransform = new Map();
  arrayIds.forEach((id, i) => indexOfTransform.set(id, i));

  const triggers = [...info.keys()].filter((id) => strippedBy.has(id));
  const mapped = triggers.map((id) => ({ name: info.get(id).name, index: indexOfTransform.get(strippedBy.get(id)) }));

  const missing = mapped.filter((r) => r.index === undefined);
  const indices = mapped.map((r) => r.index).filter((i) => i !== undefined);
  const unique = new Set(indices);
  console.log(path.relative(ROOT, file));
  console.log('  triggers=' + triggers.length, 'compass entries=' + arraySize,
    'mapped=' + indices.length, 'distinct=' + unique.size,
    'missing=' + missing.length);
  if (missing.length) console.log('   no compass entry:', missing.map((x) => x.name).join(', '));
  const sequential = indices.every((v, i) => i === 0 || v > indices[i - 1]);
  console.log('   array order matches trigger order:', sequential);
  console.log('   first five:', indices.slice(0, 5).join(', '), '... last:', indices.slice(-3).join(', '));
  return { triggers, mapped };
}

for (const level of [1, 2, 3, 4]) {
  analyze(path.join(ROOT, 'Assets/Scenes/Levels Scenes', String(level), 'Core-' + level + '.unity'));
}
