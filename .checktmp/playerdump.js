const fs = require('fs');
const p = 'Assets/Prefabs/Utils/Truck (Player).prefab';
const lines = fs.readFileSync(p, 'utf8').split(/\r?\n/);

// collect objects: key -> {class, startLine, lines}
const objs = {};
let cur = null;
for (let i = 0; i < lines.length; i++) {
  const m = lines[i].match(/^--- !u!(\d+) &(\d+)/);
  if (m) {
    cur = { cls: m[1], id: m[2], start: i, body: [] };
    objs[m[2]] = cur;
  } else if (cur) cur.body.push(lines[i]);
}
function field(o, key) {
  for (const l of o.body) {
    const m = l.match(/^\s*([A-Za-z0-9_]+):\s*(.*)$/);
    if (m && m[1] === key) return m[2];
  }
  return null;
}
const GO = '1';
console.log('total objects', Object.keys(objs).length);
for (const id of Object.keys(objs)) {
  const o = objs[id];
  if (o.cls !== GO) continue;
  const name = field(o, 'm_Name');
  const comps = (field(o, 'm_Component') || '');
  // components are listed in following lines as - component: {fileID: N}
  const compIds = [];
  const idx = o.body.findIndex(l => /^\s*m_Component:/.test(l));
  for (let i = idx + 1; i < o.body.length; i++) {
    const m = o.body[i].match(/component:\s*\{fileID:\s*(\d+)\}/);
    if (m) compIds.push(m[1]);
    else if (!/^\s*-\s/.test(o.body[i])) break;
  }
  const types = compIds.map(cid => (objs[cid] ? objs[cid].cls : '?'));
  console.log(`GO ${id} "${name}" comps=[${types.join(',')}]`);
  for (const cid of compIds) {
    const c = objs[cid];
    if (!c) continue;
    if (c.cls === '65') { // BoxCollider
      console.log(`    BoxCollider ${cid} size=${field(c,'m_Size')} center=${field(c,'m_Center')} isTrigger=${field(c,'m_IsTrigger')} layer=${field(c,'m_Layer')}`);
    } else if (c.cls === '136') {
      console.log(`    CapsuleCollider ${cid} radius=${field(c,'m_Radius')} height=${field(c,'m_Height')} center=${field(c,'m_Center')} layer=${field(c,'m_Layer')}`);
    } else if (c.cls === '135') {
      console.log(`    SphereCollider ${cid} radius=${field(c,'m_Radius')} center=${field(c,'m_Center')}`);
    } else if (c.cls === '64' || c.cls === '66') {
      // mesh collider
      console.log(`    MeshCollider ${cid} convex=${field(c,'m_Convex')} sharedMesh=${field(c,'m_Mesh')}`);
    }
  }
}
