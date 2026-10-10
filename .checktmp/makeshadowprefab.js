// Build "Shadow Truck.prefab" from the player truck prefab: the same meshes and hierarchy, but with every
// driving script, rigidbody, collider, particle system, audio source, light and trail taken out, so an
// instance of it is pure visuals that cannot drive, collide or make a sound.
const fs = require('fs');
const path = require('path');
const { read, write, splitDocs } = require('./scenelib');

const root = path.resolve(__dirname, '..');
const src = path.join(root, 'Assets/Prefabs/Utils/Truck (Player).prefab');
const dst = path.join(root, 'Assets/Prefabs/Utils/Shadow Truck.prefab');

const { head, docs } = splitDocs(read(src));

// Components that must not exist on the shadow.
const REMOVE_CLASSES = new Set([
  '114', // MonoBehaviour (CarController, WheelPhysics, EngineAudio, TruckDamage, ...)
  '54',  // Rigidbody
  '65',  // BoxCollider (and any collider)
  '198', // ParticleSystem
  '199', // ParticleSystemRenderer
  '82',  // AudioSource
  '108', // Light
  '96',  // TrailRenderer
  '120', // LineRenderer
  '95', '137', // Animator (if any)
]);

const removed = new Set();
const kept = [];
for (const d of docs) {
  if (REMOVE_CLASSES.has(d.cls)) { removed.add(d.id); continue; }
  kept.push(d);
}

// Strip the now-dangling component references out of every GameObject's m_Component list.
const out = [];
for (const d of kept) {
  let body = d.body;
  if (d.cls === '1') {
    body = body
      .split('\n')
      .filter((line) => {
        const m = line.match(/^\s*- component: \{fileID: (\d+)\}/);
        return !(m && removed.has(m[1]));
      })
      .join('\n');
  }
  out.push({ cls: d.cls, id: d.id, stripped: d.stripped, body });
}

const text = head + out.map((d) => `--- !u!${d.cls} &${d.id}${d.stripped ? ' stripped' : ''}\n${d.body}`).join('');

fs.mkdirSync(path.dirname(dst), { recursive: true });
write(dst, text);

// Find the root GameObject (its Transform has m_Father 0).
let rootGo = null;
for (const d of out) {
  if (d.cls !== '4') continue;
  const father = (d.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
  if (father !== '0') continue;
  // find the GameObject that lists this transform
  for (const g of out) {
    if (g.cls !== '1') continue;
    if (new RegExp('- component: \\{fileID: ' + d.id + '\\}').test(g.body)) { rootGo = g.id; break; }
  }
}

console.log('removed components: ' + removed.size);
console.log('docs written: ' + out.length);
console.log('root GameObject id: ' + rootGo);
console.log('bytes: ' + text.length);
