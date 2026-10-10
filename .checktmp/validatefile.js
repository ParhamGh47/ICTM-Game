// Checks a Unity asset or scene: documents parse, no duplicate local ids, no dangling local references.
const fs = require('fs');
const path = require('path');

const root = process.cwd();
const file = path.join(root, process.argv[2]);

const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
const lines = text.split('\n');

// Every document header --- !u!<class> &<id>
const docs = [];
const reDoc = /^--- !u!(\d+) &(-?\d+)( stripped)?$/;
for (let i = 0; i < lines.length; i++) {
  const m = reDoc.exec(lines[i]);
  if (m) docs.push({ cls: m[1], id: m[2], stripped: !!m[3], line: i + 1 });
}

// Local ids are unique per file, but a stripped object shares its id with the prefab's own object, so only
// non-stripped documents can be checked for duplicates.
const ids = new Set();
let dup = 0;
for (const d of docs) {
  if (d.stripped) continue;
  if (ids.has(d.id)) { dup++; if (dup < 6) console.log('DUPLICATE id', d.id, 'at line', d.line); }
  ids.add(d.id);
}

console.log(path.relative(root, file));
console.log('  documents:', docs.length, '| non-stripped ids:', ids.size, '| duplicates:', dup);

// Any reference written as {fileID: N} with no guid is local and must resolve - either to a document here or
// to a stripped object, which carries the same id as the prefab's own.
const strippedIds = new Set(docs.filter((d) => d.stripped).map((d) => d.id));
let checked = 0, missing = 0;
for (let i = 0; i < lines.length; i++) {
  const re = /\{fileID: (-?\d+)(?:, guid: ([a-f0-9]{32}), type: (\d+))?\}/g;
  let m;
  while ((m = re.exec(lines[i])) !== null) {
    if (m[2]) continue;                       // external asset reference
    const id = m[1];
    if (id === '0') continue;
    checked++;
    if (!ids.has(id) && !strippedIds.has(id)) {
      missing++;
      if (missing < 8) console.log('  MISSING local ref', id, 'at line', i + 1, ':', lines[i].trim());
    }
  }
}

console.log('  local refs:', checked, '| dangling:', missing);

// External guids must exist on disk as a .meta somewhere under Assets.
const guids = new Set();
{
  const re = /guid: ([a-f0-9]{32})/g;
  let m;
  while ((m = re.exec(text)) !== null) guids.add(m[1]);
}

const metaByGuid = new Map();
const walk = (dir) => {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== '.checktmp' && e.name !== 'Library') walk(p); }
    else if (e.name.endsWith('.meta')) {
      const g = /guid: ([a-f0-9]{32})/.exec(fs.readFileSync(p, 'utf8'));
      if (g) metaByGuid.set(g[1], path.relative(root, p).replace(/\.meta$/, ''));
    }
  }
};
walk(path.join(root, 'Assets'));

let unknown = 0;
for (const g of guids) {
  if (!metaByGuid.has(g)) { unknown++; console.log('  UNKNOWN guid', g); }
}

console.log('  external guids:', guids.size, '| unknown:', unknown);

for (const g of guids) {
  if (metaByGuid.has(g)) console.log('    ', g, '->', metaByGuid.get(g));
}
