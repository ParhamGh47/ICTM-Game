// Which prefabs can actually be collided with? Walks the given directories and prints each prefab's root
// name plus how many non-trigger colliders it has, so we only put a sound marker on things the truck can
// really hit.
//
// Run: node .checktmp/colliders.js <dir> [dir ...]
const fs = require('fs');
const path = require('path');

function walk(dir, out) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else if (e.name.endsWith('.prefab')) out.push(p);
  }
  return out;
}

for (const dir of process.argv.slice(2)) {
  const files = walk(dir, []).sort();
  console.log('\n=== ' + dir + ' (' + files.length + ' prefabs) ===');

  for (const file of files) {
    const raw = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');

    const docs = [];
    let cur = null;
    for (const line of raw.split('\n')) {
      const m = line.match(/^--- !u!(\d+) &(-?\d+)/);
      if (m) { if (cur) docs.push(cur); cur = { c: +m[1], l: [] }; continue; }
      if (cur) cur.l.push(line);
    }
    if (cur) docs.push(cur);
    for (const d of docs) d.text = d.l.join('\n');

    const colliders = docs.filter((d) => [64, 65, 135, 136].includes(d.c));
    const solid = colliders.filter((d) => !/m_IsTrigger:\s*1/.test(d.text));

    const root = docs.find((d) => d.c === 4 && /m_Father:\s*\{fileID:\s*0\}/.test(d.text));
    const goId = root ? (root.text.match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1] : null;
    const go = goId ? docs.find((d) => d.c === 1 && d.text.includes('m_GameObject') === false && d.id === goId) : null;
    const name = go ? (go.text.match(/m_Name:\s*(.*)/) || [])[1] : (goId ? '?' : '(variant)');

    const isVariant = /m_SourcePrefab:/.test(raw.split('--- !u!')[0] || '') || raw.includes('PrefabInstance:');

    console.log(
      `  ${String(solid.length).padStart(2)} solid / ${String(colliders.length).padStart(2)} total  ` +
      `root='${name}'${isVariant ? ' [variant/instance]' : ''}  ${file}`
    );
  }
}
