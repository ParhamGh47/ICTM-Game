// Print each Selectable-ish GameObject's anchored position, to see how Unity's automatic navigation
// (and the project's "top-most button" default) will read a panel.
// Usage: node .checktmp/panelpos.js <file> <parentName>
const fs = require('fs');

const file = process.argv[2];
const parentName = process.argv[3];
const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');

const blocks = [];
const re = /^--- !u!(\d+) &(\d+)(?: stripped)?\n([\s\S]*?)(?=^--- |\Z)/gm;
let m;
while ((m = re.exec(text)) !== null) blocks.push({ classId: m[1], id: m[2], body: m[3] });

const byId = new Map(blocks.map((b) => [b.id, b]));

// GameObject id -> name, and transform id -> {go, position, children, father}
const goName = new Map();
const goComps = new Map();
for (const b of blocks) {
  if (b.classId !== '1') continue;
  const n = /^  m_Name: (.*)$/m.exec(b.body);
  goName.set(b.id, n ? n[1] : '?');
}

const byTransform = new Map();
for (const b of blocks) {
  if (b.classId !== '4' && b.classId !== '224') continue;
  const go = /^  m_GameObject: \{fileID: (\d+)\}/m.exec(b.body);
  const pos = /^  m_AnchoredPosition: (.*)$/m.exec(b.body);
  const kids = [...(/^  m_Children:\n((?:  - \{fileID: \d+\}\n)*)/m.exec(b.body)?.[1] || '').matchAll(/fileID: (\d+)/g)].map((x) => x[1]);
  const father = /^  m_Father: \{fileID: (\d+)\}/m.exec(b.body);
  byTransform.set(b.id, { id: b.id, go: go ? go[1] : null, pos: pos ? pos[1] : '?', kids, father: father ? father[1] : null });
}

// GameObject -> its component class ids
for (const b of blocks) {
  if (b.classId !== '1') continue;
  const list = /^  m_Component:\n((?:  - component: \{fileID: \d+\}\n)*)/m.exec(b.body);
  const ids = [...(list?.[1] || '').matchAll(/fileID: (\d+)/g)].map((x) => x[1]);
  goComps.set(b.id, ids.map((i) => byId.get(i)).filter((c) => c && c.classId !== '4' && c.classId !== '224').map((c) => c.classId));
}

for (const t of byTransform.values()) {
  const name = goName.get(t.go);
  if (!parentName || (name && name.toLowerCase().includes(parentName.toLowerCase()))) {
    const comps = (goComps.get(t.go) || []).join(',');
    console.log((name + ' '.repeat(20)).slice(0, 20), 'pos', t.pos.padEnd(34), 'comps', comps, 'children', t.kids.length);
  }
}
