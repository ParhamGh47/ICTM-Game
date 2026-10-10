// Visual-only copies of the real models, for the tutorial card's little 3D stage.
//
// A copy of a prefab with every driving script, rigidbody, collider, audio source, particle system, light and
// trail taken out, so an instance of it is pure meshes: it cannot drive, collide, make a sound or think. The
// card can then put the game's own truck and the game's own targets on its stage without any of the game
// happening.
const fs = require('fs');
const path = require('path');
const { read, write, splitDocs } = require('./scenelib');

const root = path.resolve(__dirname, '..');

const REMOVE_CLASSES = new Set([
  '114', // MonoBehaviour - controllers, physics helpers, damage, audio logic
  '54',  // Rigidbody
  '65',  // BoxCollider
  '135', // SphereCollider
  '136', // CapsuleCollider
  '64',  // MeshCollider
  '198', // ParticleSystem
  '199', // ParticleSystemRenderer
  '82',  // AudioSource
  '81',  // AudioListener
  '108', // Light
  '96',  // TrailRenderer
  '120', // LineRenderer
  '95',  // Animator
  '137', // (Animator, again, in some versions)
  '208', // NavMeshObstacle
  '195', // NavMeshAgent
  '154', // TerrainCollider
  '146', // CharacterController
]);

function makeVisual(srcRelative, dstRelative, label) {
  const src = path.join(root, srcRelative);
  const dst = path.join(root, dstRelative);

  const { head, docs } = splitDocs(read(src));

  const removed = new Set();
  const kept = [];

  for (const d of docs) {
    if (REMOVE_CLASSES.has(d.cls)) { removed.add(d.id); continue; }
    kept.push(d);
  }

  const out = [];
  for (const d of kept) {
    let body = d.body;

    // Strip the references to the components that are gone out of every GameObject's component list, and any
    // script field that pointed at one - a reference to a removed document would be a dangling id.
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

  const here = new Set(out.filter((d) => !d.stripped).map((d) => d.id));
  let dangling = 0;
  for (const d of out) {
    const re = /\{fileID: (-?\d+)\}/g;
    let m;
    while ((m = re.exec(d.body)) !== null) {
      if (m[1] === '0') continue;
      if (!here.has(m[1])) dangling++;
    }
  }

  console.log(label + ': removed ' + removed.size + ' components, ' + out.length + ' documents, ' + text.length + ' bytes, dangling refs ' + dangling);
  console.log('  -> ' + dstRelative);

  return dstRelative;
}

const truck = makeVisual('Assets/Prefabs/Utils/Truck (Player).prefab', 'Assets/Prefabs/Utils/Truck (Visual).prefab', 'Truck');
const adamak = makeVisual('Assets/Prefabs/Adamaks/Adamak1.prefab', 'Assets/Prefabs/Utils/Adamak (Visual).prefab', 'Adamak');

// Meta files, with guids chosen here so the card can reference them.
const metas = [
  { file: truck, guid: 'd1e2f3a4b5c60718293a4b5c6d7e8f90' },
  { file: adamak, guid: 'e2f3a4b5c60718293a4b5c6d7e8f9012' },
];

for (const m of metas) {
  const metaPath = path.join(root, m.file + '.meta');
  fs.writeFileSync(metaPath,
    'fileFormatVersion: 2\nguid: ' + m.guid + '\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',
    'utf8');
  console.log('  meta ' + path.basename(metaPath) + ' guid ' + m.guid);
}
