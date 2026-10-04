// Sets the ImpactMaterial kind on one or more prefabs, rewriting only the "kind:" line inside the component's
// block. 0 Solid, 1 Sheet, 2 Panel, 3 Barrel, 4 Wood. Dry run unless --apply.
//
// Run: node .checktmp/setkind.js --apply Assets/Prefabs/Obstacles/Rigids/Log.prefab=4
const fs = require('fs');
const path = require('path');

const apply = process.argv.includes('--apply');
const guid = '3f7c1d90ab5e4c2f8d61b7a4e05c93d2';   // ImpactMaterial.cs

const targets = process.argv
  .slice(2)
  .filter((a) => !a.startsWith('--'))
  .map((a) => {
    const at = a.lastIndexOf('=');
    return [a.slice(0, at), a.slice(at + 1)];
  });

if (targets.length === 0) { console.log('usage: node .checktmp/setkind.js [--apply] <prefab>=<kind> ...'); process.exit(1); }

let bad = 0;

for (const [file, want] of targets) {
  const full = path.join(process.cwd(), file);
  const raw = fs.readFileSync(full, 'utf8');
  const nl = raw.includes('\r\n') ? '\r\n' : '\n';
  const lines = raw.replace(/\r\n/g, '\n').split('\n');

  const gi = lines.findIndex((l) => l.includes('guid: ' + guid));
  if (gi < 0) { console.log(`FAIL ${file}: no ImpactMaterial`); bad++; continue; }

  let blockStart = gi;
  while (blockStart > 0 && !/^--- !u!\d+ &/.test(lines[blockStart])) blockStart--;

  let blockEnd = gi;
  while (blockEnd < lines.length && (blockEnd === blockStart || !/^--- !u!\d+ &/.test(lines[blockEnd]))) blockEnd++;

  let kindLine = -1;
  for (let i = blockStart; i < blockEnd; i++) if (/^\s*kind:\s*-?\d+\s*$/.test(lines[i])) kindLine = i;

  if (kindLine < 0) { console.log(`FAIL ${file}: no kind line`); bad++; continue; }

  const before = lines[kindLine].trim();
  lines[kindLine] = `  kind: ${want}`;

  console.log(`${apply ? 'set ' : 'would set'} ${file}  ${before} -> kind: ${want}`);

  if (apply) fs.writeFileSync(full, lines.join('\n').split('\n').join(nl));
}

console.log(`\n${bad} problem(s)`);
