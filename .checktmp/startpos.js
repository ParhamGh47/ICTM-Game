const fs = require('fs');
const path = require('path');
const ROOT = path.resolve(__dirname, '..');
const TRUCK = 'd3631120a8772da41ac0a07b6ee0b9e6';

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
    out.set(m[1], { pos: tri('LocalPosition'), parent: father && father[1] !== '0' ? father[1] : null });
  }
  return out;
}
function world(t, id, d = 0) {
  if (!t.has(id) || d > 64) return [0, 0, 0];
  const e = t.get(id);
  const p = e.parent ? world(t, e.parent, d + 1) : [0, 0, 0];
  return [p[0] + e.pos[0], p[1] + e.pos[1], p[2] + e.pos[2]];
}

for (const level of [1, 2, 3, 4]) {
  const file = path.join(ROOT, 'Assets/Scenes/Levels Scenes', String(level), 'Core-' + level + '.unity');
  const text = read(file);
  const t = transforms(text);
  const re = /--- !u!1001 &(\d+)\nPrefabInstance:\n([\s\S]*?)m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/g;
  let m;
  while ((m = re.exec(text))) {
    if (m[3] !== TRUCK) continue;
    const block = m[2];
    const parent = (block.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
    const name = (block.match(/propertyPath: m_Name\n\s*value: ([^\n]*)/) || [])[1];
    const props = {};
    const pre = /propertyPath: m_LocalPosition\.([xyz])\n\s*value: ([^\n]+)/g;
    let pm;
    while ((pm = pre.exec(block))) props[pm[1]] = parseFloat(pm[2]);
    const w = parent ? world(t, parent) : [0, 0, 0];
    const pos = [w[0] + (props.x || 0), w[1] + (props.y || 0), w[2] + (props.z || 0)];
    console.log('Core-' + level, name, 'player truck at x', pos[0].toFixed(1), 'y', pos[1].toFixed(1), 'z', pos[2].toFixed(1),
      '(parent world', w.map((v) => v.toFixed(1)).join(','), ')');
  }
}
