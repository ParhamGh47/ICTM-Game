// Dumps the player truck prefab: the hierarchy with the components on each object, and the numbers that
// decide how it sits on the road - the rigidbody's mass and centre of mass, every collider's shape and where
// it is, and every wheel collider's mounting point, radius and suspension.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const file = process.argv[2] || path.join(root, 'Assets/Prefabs/Utils/Truck (Player).prefab');
const text = fs.readFileSync(file, 'utf8').replace(/\r/g, '');

const blocks = [];
let current = null;
for (const line of text.split('\n')) {
  const header = line.match(/^--- !u!(\d+) &(\d+)/);
  if (header) {
    current = { cls: header[1], id: header[2], body: [] };
    blocks.push(current);
  } else if (current) current.body.push(line);
}

const objs = {}, trs = {}, comps = {};
for (const b of blocks) {
  const body = b.body.join('\n');
  if (b.cls === '1') {
    objs[b.id] = {
      name: (body.match(/^\s*m_Name: (.*)$/m) || [])[1] || '',
      active: (body.match(/m_IsActive: (\d+)/) || [])[1],
      comps: (body.match(/- component: \{fileID: (\d+)\}/g) || []).map((x) => x.match(/(\d+)/)[1]),
    };
  } else if (b.cls === '4' || b.cls === '224') {
    trs[b.id] = {
      go: (body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1],
      father: (body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1],
      pos: (body.match(/m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number),
      scale: (body.match(/m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/) || []).slice(1, 4).map(Number),
    };
  }
  comps[b.id] = { cls: b.cls, body };
}

const transformOfGo = {};
for (const id in trs) transformOfGo[trs[id].go] = id;

const parentOfGo = {};
for (const id in trs) parentOfGo[trs[id].go] = trs[id].father;

function chain(goid) {
  const names = [];
  let t = parentOfGo[goid];
  let guard = 0;
  while (t && guard++ < 20) {
    const owner = trs[t] && trs[t].go;
    if (!owner) break;
    names.push(objs[owner] ? objs[owner].name : '?');
    const next = parentOfGo[owner];
    if (next === t) break;
    t = next;
  }
  return names.reverse();
}

const classNames = {
  54: 'Rigidbody', 65: 'BoxCollider', 136: 'CapsuleCollider', 135: 'SphereCollider',
  64: 'MeshCollider', 146: 'WheelCollider', 82: 'AudioSource', 108: 'Light',
  114: 'MonoBehaviour', 33: 'MeshFilter', 23: 'MeshRenderer', 11411: 'x',
};

function label(comp) {
  const tan = (comp.body.match(/m_TargetAssemblyTypeName: ([^\n]+)/) || [])[1];
  if (tan) return tan.split(',')[0].trim();
  return classNames[comp.cls] || ('class ' + comp.cls);
}

const verbose = process.argv.includes('-v');

console.log('=== hierarchy ===');
for (const id in objs) {
  const o = objs[id];
  const labels = o.comps.map((c) => (comps[c] ? label(comps[c]) : '?'));
  console.log(
    chain(id).join('/').padEnd(46),
    o.name.padEnd(24),
    (o.active === '0' ? '[off] ' : '') + labels.join(' ')
  );
}

console.log('\n=== rigidbody ===');
for (const id in comps) {
  if (comps[id].cls !== '54') continue;
  const b = comps[id].body;
  const grab = (re) => (b.match(re) || [])[1];
  console.log(
    'mass', grab(/m_Mass: ([^\n]+)/),
    '| drag', grab(/m_Drag: ([^\n]+)/),
    '| angularDrag', grab(/m_AngularDrag: ([^\n]+)/),
    '| useGravity', grab(/m_UseGravity: (\d)/),
    '| isKinematic', grab(/m_IsKinematic: (\d)/),
    '| interpolation', grab(/m_Interpolation: (\d)/),
    '| collisionDetection', grab(/m_CollisionDetection: (\d)/)
  );
  console.log('centre of mass', grab(/m_CenterOfMass: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/));
  console.log('inertia tensor', grab(/m_InertiaTensor: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/),
              '| rotation', grab(/m_InertiaRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}/));
  console.log('constraints', grab(/m_Constraints: (\d+)/), '| autoCOM', grab(/m_AutomaticCenterOfMass: (\d)/));
}

console.log('\n=== colliders (local to their own object) ===');
for (const id in comps) {
  const cls = comps[id].cls;
  if (!['54', '65', '136', '135', '64', '146'].includes(cls) || cls === '54') continue;
  const b = comps[id].body;
  const goid = (b.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const grab = (re) => (b.match(re) || [])[1];
  const t = transformOfGo[goid] ? trs[transformOfGo[goid]] : null;
  console.log(
    label(comps[id]).padEnd(14), chain(goid).join('/').padEnd(40), (objs[goid] ? objs[goid].name : '?').padEnd(20),
    'type=', grab(/m_Shape: (\d)/),
    'centre=', grab(/m_Center: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/),
    'size=', grab(/m_Size: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}/),
    'radius=', grab(/m_Radius: ([^\n]+)/),
    'height=', grab(/m_Height: ([^\n]+)/),
    'convex=', grab(/m_Convex: (\d)/),
    'material=', grab(/m_Material: \{fileID: (\d+)/),
    'layer=', (b.match(/m_Layer: (\d+)/) || [])[1]
  );
  console.log('    object local pos', t ? t.pos : '?', 'scale', t ? t.scale : '?');
}

if (verbose) {
  console.log('\n=== scripts with numbers ===');
  for (const id in comps) {
    if (comps[id].cls !== '114') continue;
    const b = comps[id].body;
    const tan = (b.match(/m_TargetAssemblyTypeName: ([^\n]+)/) || [])[1] || '';
    if (!/WheelPhysics|CarController|TireSkid|CollisionSound|TireSmoke|ExhaustSmoke|SpeedEffect|BoostManager|LightToggle|CarHorn|ReverseBeep|EngineAudio/.test(tan)) continue;
    const goid = (b.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    console.log('---', tan.split(',')[0].trim(), 'on', (objs[goid] ? objs[goid].name : '?'));
    const body = b.split('\n').filter((l) => /^  [a-zA-Z_][\w]*:/.test(l));
    console.log(body.join('\n'));
  }
}
