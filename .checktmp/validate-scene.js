// Read-only scene check: every internal fileID must resolve to a document in the same file, no document id
// may appear twice, and every guid reference must point at an asset that exists in the project.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');

const assetPaths = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== '.git' && e.name !== '.checktmp') walk(p); continue; }
    if (!e.name.endsWith('.meta')) continue;
    const m = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]+)/);
    if (m) assetPaths.set(m[1], path.relative(ROOT, p).replace(/\\/g, '/'));
  }
})(ROOT);

let bad = 0;

for (const arg of process.argv.slice(2)) {
  const file = path.join(ROOT, arg);
  const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');

  const ids = [];
  const re = /^--- !u!(\d+) &(\d+)( stripped)?$/gm;
  let m;
  while ((m = re.exec(text))) ids.push(m[2]);

  const seen = new Set();
  const dupes = [];
  for (const id of ids) { if (seen.has(id)) dupes.push(id); seen.add(id); }

  let unresolved = 0;
  let missingAsset = 0;
  const missing = new Set();
  for (const r of text.matchAll(/\{fileID: (\d+)(?:, guid: ([0-9a-f]+))?, type: (\d+)\}/g)) {
    if (r[2]) {
      // Unity's own built-in libraries are sixteen zeros, one letter, then more zeros, and are not project
      // assets: 0000000000000000f000000000000000 is unity_builtin_extra, ...e000000000000000 the default
      // resources. Nothing in the project can own these.
      if (/^0{16}[1-9a-f]0+$/.test(r[2])) continue;
      if (!assetPaths.has(r[2])) { missingAsset++; missing.add('asset ' + r[2]); }
      continue;
    }
    if (r[1] === '0') continue;
    if (!seen.has(r[1])) { unresolved++; missing.add('fileID ' + r[1]); }
  }

  const name = path.relative(ROOT, file);
  const ok = !dupes.length && !unresolved && !missingAsset;
  if (!ok) bad++;
  console.log(
    (ok ? 'ok   ' : 'FAIL ') + name.padEnd(46) +
    ' docs=' + String(ids.length).padStart(4) +
    ' dupes=' + dupes.length +
    ' unresolved=' + unresolved +
    ' missingAssets=' + missingAsset
  );
  for (const x of missing) console.log('        ' + x);
}

process.exit(bad ? 1 : 0);
