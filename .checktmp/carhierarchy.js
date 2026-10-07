const fs = require('fs');

const files = process.argv.slice(2);

for (const f of files) {
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
    const name = (d.body.match(/m_Name: (.*)/) || [])[1];
    const active = (d.body.match(/m_IsActive: (\d)/) || [])[1];
    const comps = [...d.body.matchAll(/- component: \{fileID: (\d+)\}/g)].map(x => x[1]);
    gos.set(id, { name, active, comps });
  }

  // transforms: id -> {go, parent, children}
  const trs = new Map();
  for (const [id, d] of docs) {
    if (d.name !== 'Transform') continue;
    const go = (d.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const parent = (d.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
    const children = [...d.body.matchAll(/- \{fileID: (\d+)\}/g)].map(x => x[1]);
    trs.set(id, { go, parent, children });
  }

  const goByName = new Map([...gos].map(([id, v]) => [v.name, id]));
  const compName = id => (docs.get(id) || {}).name || '?';

  console.log('========== ' + f.split('/').slice(-2).join('/'));
  const rootIds = [...trs.keys()].filter(k => {
    const t = trs.get(k);
    return !t.parent || t.parent === '0';
  });
  const walk = (trId, depth) => {
    const t = trs.get(trId);
    if (!t) return;
    const g = gos.get(t.go) || {};
    const kinds = (g.comps || []).map(compName).filter(x => x !== 'Transform');
    console.log('  '.repeat(depth) + `- ${g.name}${g.active === '0' ? ' [INACTIVE]' : ''} {${kinds.join(', ')}}`);
    for (const c of t.children) walk(c, depth + 1);
  };
  for (const r of rootIds) walk(r, 0);
  console.log();
}
