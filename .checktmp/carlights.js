const fs = require('fs');
const path = require('path');

const files = [
  'Assets/Prefabs/Cars/206/206.prefab',
  'Assets/Prefabs/Cars/911/911.prefab',
  'Assets/Prefabs/Cars/Car/Car.prefab',
  'Assets/Prefabs/Cars/Car/Truck.prefab',
];

// Split the YAML into documents keyed by their fileID.
function docs(text) {
  const out = new Map();
  const re = /^--- !u!(\d+) &(\d+)(?: stripped)?\r?\n([A-Za-z]+):/gm;
  const marks = [];
  let m;
  while ((m = re.exec(text)) !== null) marks.push({ cls: m[1], id: m[2], name: m[3], at: m.index, bodyAt: re.lastIndex });
  for (let i = 0; i < marks.length; i++) {
    const end = i + 1 < marks.length ? marks[i + 1].at : text.length;
    out.set(marks[i].id, { cls: marks[i].cls, name: marks[i].name, body: text.slice(marks[i].bodyAt, end) });
  }
  return out;
}

for (const f of files) {
  const raw = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');
  const d = docs(raw);
  console.log('===================== ' + path.basename(f));

  // GameObjects: id -> {name, active}
  const gos = new Map();
  for (const [id, doc] of d) {
    if (doc.name !== 'GameObject') continue;
    const name = (doc.body.match(/m_Name:\s*(.*)/) || [])[1] || '';
    const active = (doc.body.match(/m_IsActive:\s*(\d)/) || [])[1] || '?';
    gos.set(id, { name, active });
  }

  console.log('-- lights:');
  for (const [id, doc] of d) {
    if (doc.name !== 'Light') continue;
    const go = (doc.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const en = (doc.body.match(/m_Enabled:\s*(\d)/) || [])[1];
    const type = (doc.body.match(/m_Type:\s*(\d)/) || [])[1];
    const color = (doc.body.match(/m_Color: \{[^}]*\}/) || [])[0];
    const inten = (doc.body.match(/m_Intensity:\s*([\d.]+)/) || [])[1];
    const g = gos.get(go) || {};
    console.log(`   light ${id}: go=${g.name} goActive=${g.active} enabled=${en} type=${type} ${color} intensity=${inten}`);
  }

  console.log('-- renderers (enabled / go active):');
  for (const [id, doc] of d) {
    if (doc.name !== 'MeshRenderer') continue;
    const go = (doc.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const en = (doc.body.match(/m_Enabled:\s*(\d)/) || [])[1];
    const g = gos.get(go) || {};
    const mats = [...doc.body.matchAll(/m_Materials:[\s\S]*?(?=\n  [a-z])/g)].map(x => x[0]);
    const n = (doc.body.match(/m_Materials:/) || [])[0] ? 'has-materials' : '';
    console.log(`   renderer ${id}: go=${g.name} goActive=${g.active} enabled=${en} ${n}`);
  }
}
