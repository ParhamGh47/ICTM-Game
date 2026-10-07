const fs = require('fs');

for (const f of process.argv.slice(2)) {
  const text = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');
  const docs = new Map();
  const re = /^--- !u!(\d+) &(\d+)(?: stripped)?\n([A-Za-z]+):/gm;
  let m, marks = [];
  while ((m = re.exec(text)) !== null) marks.push({ cls: m[1], id: m[2], name: m[3], at: m.index, bodyAt: re.lastIndex });
  for (let i = 0; i < marks.length; i++) {
    const end = i + 1 < marks.length ? marks[i + 1].at : text.length;
    docs.set(marks[i].id, { cls: marks[i].cls, name: marks[i].name, body: text.slice(marks[i].bodyAt, end) });
  }

  const gos = new Map();
  for (const [id, d] of docs) {
    if (d.name !== 'GameObject') continue;
    gos.set(id, (d.body.match(/m_Name: (.*)/) || [])[1]);
  }

  console.log('===== ' + f);
  for (const [id, d] of docs) {
    if (d.name !== 'Transform') continue;
    const go = (d.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const name = gos.get(go) || '(instance child)';
    const pos = (d.body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/) || []).slice(1).map(Number);
    const scale = (d.body.match(/m_LocalScale: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/) || []).slice(1).map(Number);
    const parent = (d.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
    const parentName = (() => {
      const pt = docs.get(parent);
      if (!pt) return 'root';
      return gos.get((pt.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1]) || '(instance child)';
    })();
    if (name !== 'LightL' && name !== 'LightR') continue;
    console.log(`   ${name} under ${parentName}: pos=[${pos.join(', ')}] scale=[${scale.join(', ')}]`);
  }

  // The model instance's own placement, from the prefab-instance modifications.
  const mods = [...text.matchAll(/- target: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: 3\}\n\s+propertyPath: (m_LocalPosition\.[xyz]|m_LocalScale\.[xyz])\n\s+value: ([-\d.e]+)/g)];
  for (const mod of mods) console.log(`   model override ${mod[3]} = ${mod[4]}  (target ${mod[1]})`);
}
