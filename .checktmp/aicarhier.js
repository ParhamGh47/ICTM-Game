// Dumps the hierarchy of a passing-car prefab with the components that matter to collision callbacks:
// which GameObject carries the Rigidbody, which carry colliders, and where AICarController sits. Read-only.
//
// Run: node .checktmp/aicarhier.js Assets/Prefabs/Cars/911/911.prefab
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const file = process.argv[2];

const guidOf = (name) => {
  const meta = fs.readFileSync(path.join(root, 'Assets', 'Scripts', name + '.cs.meta'), 'utf8');
  return /guid:\s*([0-9a-f]+)/.exec(meta)[1];
};

const wanted = {
  [guidOf('206/AICarController')]: 'AICarController',
  [guidOf('PassingCar/PassingCarDamage')]: 'PassingCarDamage',
  [guidOf('PassingCar/PathTracker')]: 'PathTracker',
};

const text = fs.readFileSync(path.join(root, file), 'utf8').replace(/\r\n/g, '\n');
const blocks = [];
const re = /^--- !u!(\d+) &(\d+)(?: stripped)?\n([\s\S]*?)(?=^--- !u!\d+ &\d+|$(?![\s\S]))/gm;
let m;
while ((m = re.exec(text)) !== null) blocks.push({ type: m[1], id: m[2], body: m[3] });

const field = (body, name) => {
  const mm = new RegExp('^  ' + name + ': (.*)$', 'm').exec(body);
  return mm ? mm[1].trim() : null;
};
const idOf = (v) => (v ? (/fileID: (\d+)/.exec(v) || [])[1] || null : null);

const objects = new Map();
const transforms = new Map();
const comps = new Map();   // gameObject id -> [labels]

for (const b of blocks) {
  if (b.type === '1') {
    const names = [];
    const start = b.body.indexOf('m_Component:');
    if (start >= 0) {
      const tail = b.body.slice(start).split('\n');
      for (const line of tail) {
        const cm = /- component: \{fileID: (\d+)\}/.exec(line);
        if (!cm) break;
        names.push(cm[1]);
      }
    }
    objects.set(b.id, { name: field(b.body, 'm_Name'), comps: names, active: field(b.body, 'm_IsActive') });
  } else if (b.type === '4' || b.type === '224') {
    transforms.set(b.id, { go: idOf(field(b.body, 'm_GameObject')), father: idOf(field(b.body, 'm_Father')) });
  }
}

const byComponent = new Map();
for (const b of blocks) {
  if (b.type === '114' || b.type === '54' || /^(64|65|135|136|143)$/.test(b.type)) {
    byComponent.set(b.id, idOf(field(b.body, 'm_GameObject')));
  }
}

function label(componentId, text) {
  const go = byComponent.get(componentId);
  if (!go) return;
  if (!labels.has(go)) labels.set(go, []);
  labels.get(go).push(text);
}
const labels = new Map();

// Second pass, now that byComponent is filled.
for (const b of blocks) {
  if (b.type === '54') label(b.id, 'Rigidbody kinetic=' + field(b.body, 'm_IsKinematic'));
  else if (/^(64|65|135|136|143)$/.test(b.type)) {
    const kind = { 64: 'MeshCollider', 65: 'BoxCollider', 135: 'SphereCollider', 136: 'CapsuleCollider', 143: 'TerrainCollider' }[b.type];
    label(b.id, kind);
  } else if (b.type === '114') {
    const g = field(b.body, 'm_Script');
    const guid = g ? (/guid: ([0-9a-f]+)/.exec(g) || [])[1] : null;
    if (wanted[guid]) label(b.id, wanted[guid]);
  }
}

const children = new Map();
for (const t of transforms.values()) {
  if (!children.has(t.father)) children.set(t.father, []);
  children.get(t.father).push(t.go);
}

const roots = [...transforms.values()].filter((t) => !t.father || t.father === '0').map((t) => t.go);

const print = (go, depth) => {
  const o = objects.get(go);
  if (!o) return;
  const own = (labels.get(go) || []).join(', ');
  console.log('  '.repeat(depth) + (o.name || '?') + (own ? '   [' + own + ']' : ''));
  for (const c of children.get(go) || []) print(c, depth + 1);
};

console.log('=== ' + file);
for (const r of roots) print(r, 0);
console.log('\nRigidbody on: ' + [...labels.entries()].filter(([, l]) => l.some((x) => x.startsWith('Rigidbody'))).map(([g]) => objects.get(g).name).join(', '));
console.log('AICarController on: ' + [...labels.entries()].filter(([, l]) => l.includes('AICarController')).map(([g]) => objects.get(g).name).join(', '));
