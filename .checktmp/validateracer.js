const fs = require('fs');
const path = require('path');

const root = process.cwd();
const file = path.join(root, 'Assets/Prefabs/Utils/Shadow Racer.prefab');
const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');

// --- local object ids -------------------------------------------------------
const ids = [];
const reId = /^--- !u!(\d+) &(-?\d+)/gm;
let m;
while ((m = reId.exec(text)) !== null) ids.push({ cls: m[1], id: m[2] });

const seen = new Set();
let dup = 0;
for (const o of ids) { if (seen.has(o.id)) { dup++; console.log('DUPLICATE id', o.id); } seen.add(o.id); }

console.log('documents:', ids.length, 'classes:', ids.map(o => o.cls).join(','));
console.log('duplicate ids:', dup);

// --- every local fileID reference must resolve ------------------------------
let unresolved = 0, checked = 0;
const reRef = /\{fileID: (-?\d+)(?:, guid: ([a-f0-9]{32}), type: (\d+))?\}/g;
const lines = text.split('\n');
for (let i = 0; i < lines.length; i++) {
  let r;
  const re = new RegExp(reRef.source, 'g');
  while ((r = re.exec(lines[i])) !== null) {
    const id = r[1];
    if (id === '0') continue;
    if (r[2]) continue;                    // external asset reference
    checked++;
    if (!seen.has(id)) { unresolved++; console.log('UNRESOLVED local ref', id, 'at line', i + 1, ':', lines[i].trim()); }
  }
}
console.log('local refs checked:', checked, 'unresolved:', unresolved);

// --- scripts and assets referenced must exist -------------------------------
function guidOfMeta(p) {
  if (!fs.existsSync(p)) return null;
  const t = fs.readFileSync(p, 'utf8');
  const g = /guid: ([a-f0-9]{32})/.exec(t);
  return g ? g[1] : null;
}

const refs = new Map();
const reGuid = /guid: ([a-f0-9]{32}), type: (\d+)/g;
let g;
while ((g = reGuid.exec(text)) !== null) refs.set(g[1], g[2]);

for (const guid of refs.keys()) {
  // find the meta that owns this guid
  const found = [];
  const walk = (dir) => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== '.checktmp' && e.name !== 'Library') walk(p); }
      else if (e.name.endsWith('.meta')) { if (guidOfMeta(p) === guid) found.push(p); }
    }
  };
  walk(path.join(root, 'Assets'));
  console.log('guid', guid, found.length ? '-> ' + path.relative(root, found[0]).replace(/\.meta$/, '') : 'MISSING');
}

// --- the truck prefab's referenced fileID should be its own root GameObject --
const truckGuid = 'd4e7a1b35c9f42e8a0b6c2d3e4f5a6b7';
const truck = fs.readFileSync(path.join(root, 'Assets/Prefabs/Utils/Shadow Truck.prefab'), 'utf8').replace(/\r\n/g, '\n');
const rootObj = /^--- !u!1 &(\d+)\nGameObject:/m.exec(truck);
const referenced = /truckPrefab: \{fileID: (\d+)/.exec(text);
console.log('truckPrefab fileID', referenced[1], 'is a GameObject in Shadow Truck.prefab:', new RegExp('^--- !u!1 &' + referenced[1] + '\\nGameObject:', 'm').test(truck));
console.log('shadow truck root GameObject id:', rootObj ? rootObj[1] : '(none)');
console.log('tagged Player in Shadow Truck.prefab:', (truck.match(/m_TagString: Player/g) || []).length);
