// Prints the hierarchy of the four passing car prefabs: object names by depth, plus which carry a mesh,
// a light, a collider or a recognisable script, so the damage part list can be written against real names.
const fs = require('fs');
const path = require('path');

const files = process.argv.slice(2);
if (!files.length) files.push('Assets/Prefabs/Cars/206/206.prefab', 'Assets/Prefabs/Cars/911/911.prefab', 'Assets/Prefabs/Cars/Car/Car.prefab', 'Assets/Prefabs/Cars/Car/Truck.prefab');

const CLASS = { 1: 'GameObject', 4: 'Transform', 20: 'Camera', 23: 'MeshRenderer', 33: 'MeshFilter', 54: 'Rigidbody', 64: 'MeshCollider', 65: 'BoxCollider', 108: 'Light', 114: 'MonoBehaviour', 135: 'SphereCollider', 136: 'CapsuleCollider', 137: 'WheelCollider', 154: 'TerrainCollider', 198: 'ParticleSystem', 199: 'ParticleSystemRenderer', 212: 'SpriteRenderer', 222: 'CanvasRenderer' };

for (const file of files) {
  const raw = fs.readFileSync(path.join(process.cwd(), file), 'utf8').replace(/\r\n/g, '\n');
  const docs = [];
  let cur = null;
  for (const line of raw.split('\n')) {
    const m = line.match(/^--- !u!(\d+) &(\d+)(.*)$/);
    if (m) { if (cur) docs.push(cur); cur = { classId: +m[1], fileId: +m[2], lines: [] }; continue; }
    if (cur) cur.lines.push(line);
  }
  if (cur) docs.push(cur);
  const flat = (d) => d.lines.join('\n');
  const byId = new Map();
  for (const d of docs) byId.set(d.fileId, d);
  const goOf = (d) => { const m = flat(d).match(/m_GameObject:\s*\{fileID:\s*(-?\d+)\}/); return m ? +m[1] : null; };
  const nameOf = (goId) => { const d = byId.get(goId); if (!d) return '?'; const m = flat(d).match(/m_Name:\s*(.*)/); return m ? m[1].trim() : '?'; };

  const components = new Map();   // goId -> [class names]
  for (const d of docs) {
    const go = goOf(d);
    if (go == null || d.classId === 4 || d.classId === 1) continue;
    if (!components.has(go)) components.set(go, []);
    let label = CLASS[d.classId] || ('class ' + d.classId);
    if (d.classId === 114) {
      const m = flat(d).match(/m_Script:\s*\{fileID:\s*(-?\d+)/);
      const g = m && byId.get(+m[1]);
      const guid = g ? (flat(g).match(/m_Name:\s*(.*)/) || [])[1] : null;
      label = 'Script(' + (guid ? guid.trim() : '?') + ')';
    }
    if (d.classId === 108) {
      const typ = flat(d).match(/m_Type:\s*(-?\d+)/);
      const en = flat(d).match(/m_Enabled:\s*(\d+)/);
      label = 'Light(type ' + (typ ? typ[1] : '?') + (en && en[1] === '0' ? ', off' : '') + ')';
    }
    components.get(go).push(label);
  }

  const parents = new Map();   // transformId -> parent transformId
  const goToTransform = new Map();
  for (const d of docs) {
    if (d.classId !== 4) continue;
    const m = flat(d).match(/m_Father:\s*\{fileID:\s*(-?\d+)\}/);
    const parentId = m ? +m[1] : 0;
    parents.set(d.fileId, parentId);
    const go = goOf(d);
    if (go != null) goToTransform.set(go, d.fileId);
  }
  const childrenOf = new Map();
  for (const [t, p] of parents) {
    if (p === 0) continue;
    if (!childrenOf.has(p)) childrenOf.set(p, []);
    childrenOf.get(p).push(t);
  }

  console.log('\n===== ' + file + ' =====');
  const roots = [...parents.entries()].filter(([, p]) => p === 0).map(([t]) => t);
  const walk = (t, depth) => {
    const go = [...goToTransform.entries()].find(([, tt]) => tt === t);
    const goId = go ? go[0] : null;
    const comps = goId != null ? (components.get(goId) || []) : [];
    console.log('  '.repeat(depth) + '- ' + (goId != null ? nameOf(goId) : '?') + (comps.length ? '   [' + comps.join(', ') + ']' : ''));
    for (const c of childrenOf.get(t) || []) walk(c, depth + 1);
  };
  for (const r of roots) walk(r, 0);
}
