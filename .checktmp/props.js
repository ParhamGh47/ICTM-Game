// Where are the level's solid props - the Blocks and the Flag - placed in each core scene?
const fs = require('fs');
const path = require('path');
const ROOT = path.resolve(__dirname, '..');

const GUID = {
  block: '1eccf2e439c379c408862aec2f9d53bc',
  flag: '30f0a67231ecd7b40a52d9e9aa5da8fa',
  finish: '162716bc0ae05a443b5e67b461f30270',
  player: null,
};
const byGuid = Object.fromEntries(Object.entries(GUID).filter(([, v]) => v).map(([k, v]) => [v, k]));

function read(f) { return fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n'); }

function transforms(text) {
  const out = new Map();
  const re = /--- !u!4 &(\d+)(?: stripped)?\nTransform:\n([\s\S]*?)(?=\n--- !u!|\n?$)/g;
  let m;
  while ((m = re.exec(text))) {
    const body = m[2];
    const tri = (k) => {
      const mm = body.match(new RegExp('m_' + k + ': \\{x: ([^,]+), y: ([^,]+), z: ([^}]+)\\}'));
      return mm ? [parseFloat(mm[1]), parseFloat(mm[2]), parseFloat(mm[3])] : null;
    };
    const father = body.match(/m_Father: \{fileID: (\d+)\}/);
    const src = body.match(/m_CorrespondingSourceObject: \{fileID: (\d+), guid: ([0-9a-f]+)/);
    out.set(m[1], {
      pos: tri('LocalPosition'),
      parent: father && father[1] !== '0' ? father[1] : null,
      src: src ? src[2] : null,
      srcId: src ? src[1] : null,
      inst: (body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1] || null,
    });
  }
  return out;
}

function world(t, id, depth = 0) {
  if (!t.has(id) || depth > 64) return [0, 0, 0];
  const e = t.get(id);
  const p = e.parent ? world(t, e.parent, depth + 1) : [0, 0, 0];
  return [p[0] + e.pos[0], p[1] + e.pos[1], p[2] + e.pos[2]];
}

function analyze(file) {
  const text = read(file);
  const t = transforms(text);
  const found = [];

  const re = /--- !u!1001 &(\d+)\nPrefabInstance:\n([\s\S]*?)m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/g;
  let m;
  while ((m = re.exec(text))) {
    const kind = byGuid[m[3]];
    if (!kind) continue;
    const block = m[2];
    const parent = (block.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
    const name = (block.match(/propertyPath: m_Name\n\s*value: ([^\n]*)/) || [])[1];
    const props = {};
    const pre = /target: \{fileID: \d+, guid: [0-9a-f]+, type: 3\}\n\s*propertyPath: m_LocalPosition\.([xyz])\n\s*value: ([^\n]+)/g;
    let pm;
    while ((pm = pre.exec(block))) props[pm[1]] = parseFloat(pm[2]);
    const w = parent ? world(t, parent) : [0, 0, 0];
    found.push({
      kind,
      name,
      world: [w[0] + (props.x || 0), w[1] + (props.y || 0), w[2] + (props.z || 0)],
    });
  }

  console.log(path.relative(ROOT, file));
  for (const f of found) {
    console.log('  ' + f.kind.padEnd(6) + ' ' + String(f.name).padEnd(24) +
      ' x ' + f.world[0].toFixed(1).padStart(9) + '  y ' + f.world[1].toFixed(1).padStart(7) +
      '  z ' + f.world[2].toFixed(1).padStart(9));
  }
}

for (const level of [1, 2, 3, 4]) {
  analyze(path.join(ROOT, 'Assets/Scenes/Levels Scenes', String(level), 'Core-' + level + '.unity'));
}
