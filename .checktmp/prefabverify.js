// Checks that each passing car prefab's root GameObject lists the new PassingCarDamage component, that the
// component points back at the root, and that the script guid and parts are what they should be.
const fs = require('fs');

const GUID = 'c0dd1386bfa4008a89355353d6ba17e1';
const files = ['Assets/Prefabs/Cars/206/206.prefab', 'Assets/Prefabs/Cars/911/911.prefab', 'Assets/Prefabs/Cars/Car/Car.prefab', 'Assets/Prefabs/Cars/Car/Truck.prefab'];

function parse(raw) {
  const nl = raw.includes('\r\n') ? '\r\n' : '\n';
  const docs = [];
  let c = null;
  for (const l of raw.split(nl)) {
    const m = l.match(/^--- !u!(\d+) &(\d+)(\s+stripped)?$/);
    if (m) { if (c) docs.push(c); c = { cls: +m[1], id: m[2], stripped: !!m[3], lines: [] }; continue; }
    if (c) c.lines.push(l);
  }
  if (c) docs.push(c);
  return { docs, nl, flat: (d) => d.lines.join(nl) };
}

let bad = 0;
for (const f of files) {
  const raw = fs.readFileSync(f, 'utf8');
  const { docs, nl, flat } = parse(raw);
  const rootT = docs.find((d) => d.cls === 4 && /m_Father: \{fileID: 0\}/.test(flat(d)));
  const rootGoId = flat(rootT).match(/m_GameObject: \{fileID: (-?\d+)\}/)[1];
  const rootGo = docs.find((d) => d.cls === 1 && d.id === rootGoId);
  const goText = flat(rootGo);

  const idMatches = [...goText.matchAll(/- component: \{fileID: (-?\d+)\}/g)].map((m) => m[1]);
  console.log('== ' + f);
  console.log('   root GameObject ' + rootGoId + ' "' + (goText.match(/m_Name: (.*)/) || [])[1] + '"');
  console.log('   components listed: ' + idMatches.join(', '));

  // duplicate ids anywhere in the file
  const allIds = docs.filter((d) => !d.stripped).map((d) => d.id);
  const dupes = allIds.filter((id, i) => allIds.indexOf(id) !== i);
  if (dupes.length) { console.log('   !! duplicate object ids: ' + dupes.join(', ')); bad++; }

  const mine = docs.find((d) => d.cls === 114 && flat(d).includes(GUID));
  if (!mine) { console.log('   !! no PassingCarDamage component found'); bad++; continue; }

  if (!idMatches.includes(mine.id)) { console.log('   !! root GameObject does not list component ' + mine.id); bad++; }
  const mgo = flat(mine).match(/m_GameObject: \{fileID: (-?\d+)\}/)[1];
  if (mgo !== rootGoId) { console.log('   !! component points at GameObject ' + mgo + ', not the root'); bad++; }

  const parts = [...flat(mine).matchAll(/- name: (.+)/g)].map((m) => m[1].trim());
  console.log('   found by name: ' + parts.join(' | '));
  if (parts.some((p) => p.toLowerCase().includes('body'))) { console.log('   !! the shell is in the list - it must stay on the car'); bad++; }
  if (!parts.length) { console.log('   !! no parts'); bad++; }

  // every other doc must still parse into a known class
  const unknown = docs.filter((d) => d.cls !== 1 && d.cls !== 4 && d.cls !== 114 && d.cls !== 1001 && !d.stripped && d.cls !== 54 && d.cls !== 65 && d.cls !== 108 && d.cls !== 33 && d.cls !== 23 && d.cls !== 198 && d.cls !== 199 && d.cls !== 212 && d.cls !== 222);
  if (unknown.length) console.log('   note: ' + unknown.length + ' docs of other classes: ' + [...new Set(unknown.map((d) => d.cls))].join(', '));
}

console.log(bad === 0 ? '\nAll four prefabs verified.' : '\n' + bad + ' problem(s) found.');
