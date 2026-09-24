const fs = require('fs');
const p = 'Assets/Prefabs/Utils/Truck (Player).prefab';
const lines = fs.readFileSync(p, 'utf8').split(/\r?\n/);
const objs = {};
let cur = null;
for (let i = 0; i < lines.length; i++) {
  const m = lines[i].match(/^--- !u!(\d+) &(\d+)/);
  if (m) { cur = { cls: m[1], id: m[2], start: i, body: [] }; objs[m[2]] = cur; }
  else if (cur) cur.body.push(lines[i]);
}
const goName = {};
for (const id of Object.keys(objs)) if (objs[id].cls === '1') {
  for (const l of objs[id].body) { const m = l.match(/^\s*m_Name:\s*(.*)$/); if (m) { goName[id] = m[1]; break; } }
}
function f(o, k) { for (const l of o.body) { const m = l.match(/^\s*([A-Za-z0-9_]+):\s*(.*)$/); if (m && m[1] === k) return m[2]; } return null; }
const census = {};
for (const id of Object.keys(objs)) { const c = objs[id].cls; census[c] = (census[c] || 0) + 1; }
console.log('class census', census);
console.log('--- colliders / collider-ish ---');
const COLL = { '64': 'MeshCollider', '65': 'BoxCollider', '135': 'SphereCollider', '136': 'CapsuleCollider', '146': 'WheelCollider', '153': 'ConfigurableJoint', '144': 'CharacterController' };
for (const id of Object.keys(objs)) {
  const o = objs[id];
  if (!COLL[o.cls]) continue;
  const go = f(o, 'm_GameObject');
  const goId = go ? go.match(/\d+/)[0] : '?';
  console.log(`${COLL[o.cls]} ${id} on GO ${goId} "${goName[goId] || '??'}" size=${f(o, 'm_Size')} center=${f(o, 'm_Center')} radius=${f(o, 'm_Radius')} height=${f(o, 'm_Height')} convex=${f(o, 'm_Convex')} trigger=${f(o, 'm_IsTrigger')} layer=${f(o, 'm_Layer')}`);
}
console.log('--- prefab instances (nested) ---');
let m;
const re = /^--- !u!1001 &(\d+)/gm;
let mi;
const txt = lines.join('\n');
const instRe = /^--- !u!1001 &(\d+)/gm;
while ((mi = instRe.exec(txt))) {
  const start = mi.index;
  const next = txt.indexOf('\n--- !u!', start + 10);
  const block = txt.slice(start, next < 0 ? undefined : next);
  const g = block.match(/m_SourcePrefab:\s*\{fileID:\s*\d+, guid:\s*([0-9a-f]+)/);
  const mods = (block.match(/propertyPath: m_LocalScale.y/g) || []).length;
  console.log(`PrefabInstance ${mi[1]} source guid ${g ? g[1] : '?'} mods=${mods}`);
}
