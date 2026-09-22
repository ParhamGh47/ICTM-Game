// Build guid -> asset path from .meta files, then diff the guids referenced by
// each Core-N scene so the level-specific assets are obvious.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..', 'Assets');

function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else out.push(p);
  }
  return out;
}

const files = walk(ROOT);
const guidToPath = new Map();
for (const f of files) {
  if (!f.endsWith('.meta')) continue;
  const t = fs.readFileSync(f, 'utf8');
  const m = /^guid: ([0-9a-f]{32})/m.exec(t);
  if (m) guidToPath.set(m[1], f.slice(0, -5).replace(/\\/g, '/'));
}

const scenes = [1, 2, 3, 4].map(n => `${ROOT}/Scenes/Levels Scenes/${n}/Core-${n}.unity`);
const sets = scenes.map(p => {
  const t = fs.readFileSync(p, 'utf8');
  const s = new Set();
  for (const m of t.matchAll(/guid: ([0-9a-f]{32})/g)) s.add(m[1]);
  return s;
});

const rel = p => p.replace(/\\/g, '/').replace(ROOT.replace(/\\/g, '/') + '/', '');

// guids present in at least one but not all four
const all = new Set([].concat(...sets.map(s => [...s])));
const partial = [];
for (const g of all) {
  const inCount = sets.filter(s => s.has(g)).length;
  if (inCount < 4) partial.push({ g, inCount, path: guidToPath.get(g) || '(builtin/unknown)' });
}
partial.sort((a, b) => (a.path < b.path ? -1 : 1));
console.log('=== guids NOT shared by all four Core scenes ===');
for (const x of partial) console.log(`${x.inCount}/4  ${x.g}  ${rel(x.path)}`);

console.log('\n=== per-scene line counts ===');
for (const p of scenes) console.log(fs.readFileSync(p, 'utf8').split('\n').length, rel(p));
