// What a prefab actually is, for working out why a hit does or does not make a sound.
//
// Prints the root GameObject's name/tag/layer, every collider on it (type, trigger flag, size/radius/extents)
// and every script component by name - the guids are resolved by scanning the .cs.meta files.
//
// Run: node .checktmp/prefabinfo.js <prefab> [prefab ...]
const fs = require('fs');
const path = require('path');

const args = process.argv.slice(2);
if (args.length === 0) { console.log('usage: node .checktmp/prefabinfo.js <prefab> ...'); process.exit(1); }

// guid -> script name, from every .cs.meta in the project
const scriptByGuid = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== 'Library') walk(p); continue; }
    if (!e.name.endsWith('.cs.meta')) continue;
    const guid = (fs.readFileSync(p, 'utf8').match(/^guid:\s*([0-9a-f]+)/m) || [])[1];
    if (guid) scriptByGuid.set(guid, e.name.replace(/\.cs\.meta$/, ''));
  }
})('Assets');

const COMPONENT_CLASS = {
  1: 'GameObject', 4: 'Transform', 20: 'Camera', 23: 'MeshRenderer', 33: 'MeshFilter',
  54: 'Rigidbody', 64: 'MeshCollider', 65: 'BoxCollider', 82: 'AudioSource',
  108: 'Light', 114: 'MonoBehaviour', 136: 'CapsuleCollider', 135: 'SphereCollider',
};

for (const file of args) {
  const raw = fs.readFileSync(path.join(process.cwd(), file), 'utf8').replace(/\r\n/g, '\n');

  const docs = [];
  let cur = null;
  for (const line of raw.split('\n')) {
    const m = line.match(/^--- !u!(\d+) &(-?\d+)/);
    if (m) { if (cur) docs.push(cur); cur = { c: +m[1], id: m[2], head: line, l: [] }; continue; }
    if (cur) cur.l.push(line);
  }
  if (cur) docs.push(cur);
  for (const d of docs) d.text = d.l.join('\n');

  console.log('\n=== ' + file + ' ===');

  const roots = docs.filter((d) => d.c === 4 && /m_Father:\s*\{fileID:\s*0\}/.test(d.text));
  for (const tr of roots) {
    const goId = (tr.text.match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1];
    const go = docs.find((d) => d.c === 1 && d.id === goId);
    if (!go) continue;

    const name = (go.text.match(/m_Name:\s*(.*)/) || [])[1];
    const tag = (go.text.match(/m_TagString:\s*(.*)/) || [])[1];
    const layer = (go.text.match(/m_Layer:\s*(\d+)/) || [])[1];

    console.log(`root  name='${name}'  tag='${tag}'  layer=${layer}  components=${(go.text.match(/- component:/g) || []).length}`);

    // its own descendants, by Transform parent chains
    const byTransform = new Map();
    for (const d of docs) if (d.c === 4) byTransform.set((d.text.match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1], d);

    const goName = (id) => {
      const g = docs.find((x) => x.c === 1 && x.id === id);
      return g ? (g.text.match(/m_Name:\s*(.*)/) || [])[1] : id;
    };

    // every GameObject in the file, with what is on it
    for (const d of docs) {
      if (d.c === 1) {
        const n = (d.text.match(/m_Name:\s*(.*)/) || [])[1];
        const t = (d.text.match(/m_TagString:\s*(.*)/) || [])[1];
        const comps = [...d.text.matchAll(/- component: \{fileID: (-?\d+)\}/g)].map((m) => m[1]);
        const kinds = comps.map((cid) => {
          const cd = docs.find((x) => x.id === cid);
          if (!cd) return 'MISSING(' + cid + ')';
          if (cd.c === 114) {
            const g = (cd.text.match(/guid: ([0-9a-f]+)/) || [])[1];
            return scriptByGuid.get(g) || 'script(' + (g || '?').slice(0, 8) + ')';
          }
          return COMPONENT_CLASS[cd.c] || ('!u!' + cd.c);
        });
        if (t && t !== 'Untagged') console.log(`  go '${n}'  tag='${t}'  [${kinds.join(', ')}]`);
      }
    }

    // colliders, wherever they are
    for (const d of docs) {
      if (![65, 135, 136, 64].includes(d.c)) continue;
      const goId2 = (d.text.match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/) || [])[1];
      const trig = (d.text.match(/m_IsTrigger:\s*(\d)/) || [])[1];
      const size = (d.text.match(/m_Size:\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+),\s*z:\s*([-\d.e]+)\}/) || []).slice(1).join(' x ');
      const radius = (d.text.match(/m_Radius:\s*([-\d.e]+)/) || [])[1];
      const scale = (d.text.match(/m_Scale:\s*6,4,4,/) ? '' : '');
      console.log(`  collider ${COMPONENT_CLASS[d.c]} on '${goName(goId2)}'  trigger=${trig}  ${size ? 'size=' + size : ''}${radius ? ' radius=' + radius : ''}`);
    }
  }
}
