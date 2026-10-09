// Rebuilds the Template scene's rig from a current level scene.
//
// The Template scene is the starting point for a new level, and the parts of it that every level shares -
// the truck, the camera rig and the HUD canvas - were last touched when it was made. Bringing a prefab
// instance up to date means re-instantiating it, so this takes the three instances (and everything that
// hangs off them: their stripped children, and the scene objects the level added under them, like the
// soundtrack object parented to the truck), gives every one of those documents a fresh id, and puts them in
// the Template in place of the copies that are there.
//
// Usage: node .checktmp/template.js [--apply]
const fs = require('fs');
const path = require('path');
const ROOT = path.resolve(__dirname, '..');
const { read, write, splitDocs, index } = require('./scenelib');

const SRC = path.join(ROOT, 'Assets/Scenes/Levels Scenes/3/Core-3.unity');
const DST = path.join(ROOT, 'Assets/Scenes/Template.unity');
const APPLY = process.argv.includes('--apply');

// The prefabs whose instances are being brought over, by the guid of the prefab.
const RIG = {
  'd3631120a8772da41ac0a07b6ee0b9e6': 'Truck (Player)',
  '9de7ea8ebea5b664bb865e6f78009de3': 'CameraManager',
  '81c7d71664e97214bb3397d11772ac21': 'CanvasUI',
};

// Prefab-instance overrides that belong to the level rather than to the rig: the level's checkpoints (the
// compass's own list) and the extra camera a level can place. Dropping them leaves the prefab's own values.
const LEVEL_OVERRIDES = [/^checkpoints\.Array\./, /^Cameras\.Array\./];

const source = splitDocs(read(SRC));
const target = splitDocs(read(DST));
const srcById = index(source.docs);
const dstById = index(target.docs);

const srcInstances = source.docs.filter(
  (d) => d.cls === '1001' && RIG[(d.body.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1]],
);

console.log('Source instances to bring over:');
for (const d of srcInstances) {
  const guid = (d.body.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1];
  console.log('  ' + RIG[guid] + '  instance ' + d.id);
}

// ------------------------------------------------------------------ which documents are moving

const moving = new Set(srcInstances.map((d) => d.id));

// Their stripped documents.
for (const d of source.docs) {
  const inst = (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
  if (inst && moving.has(inst)) moving.add(d.id);
}

// Scene-added parts of an instance: a component the level put on the instance's own root (the level loader on
// the canvas), or a whole object it parented into it. Both belong to the rig the level shares, so they come
// with it - and both are scene documents, with no PrefabInstance of their own.
for (let pass = 0; pass < 8; pass++) {
  let added = 0;
  for (const d of source.docs) {
    if (d.stripped || moving.has(d.id)) continue;
    const inst = (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
    if (inst && inst !== '0') continue;
    const go = (d.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    if (go && moving.has(go)) { moving.add(d.id); added++; }
  }
  if (!added) break;
}

// Scene objects (and their components) sitting under one of those instances: the level adds its own children
// to the truck - the soundtrack object is one - and they have no PrefabInstance of their own.
function addSceneObjectAndChildren(goDoc) {
  if (moving.has(goDoc.id)) return;
  const componentRefs = [...goDoc.body.matchAll(/- component: \{fileID: (\d+)\}/g)].map((m) => m[1]);
  if (!componentRefs.length) return;
  moving.add(goDoc.id);
  const transformId = componentRefs[0];
  moving.add(transformId);
  for (const c of componentRefs) moving.add(c);
  for (const other of source.docs) {
    if (other.cls !== '4' || other.stripped) continue;
    if (moving.has(other.id)) continue;
    const father = (other.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
    if (father !== transformId) continue;
    const go = (other.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    if (go && srcById.has(go)) addSceneObjectAndChildren(srcById.get(go));
  }
}

for (const d of source.docs) {
  if (d.cls !== '1') continue;
  const ownInstance = (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
  if (ownInstance && ownInstance !== '0') continue;
  // Walk up the transform chain: does it climb into one of the moving instances?
  const transformId = (d.body.match(/- component: \{fileID: (\d+)\}/) || [])[1];
  let cur = transformId;
  for (let hops = 0; cur && hops < 64; hops++) {
    const td = srcById.get(cur);
    if (!td) break;
    if (td.stripped) {
      const inst = (td.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
      if (inst && moving.has(inst)) addSceneObjectAndChildren(d);
      break;
    }
    const father = (td.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
    if (!father || father === '0') break;
    cur = father;
  }
}

console.log('\nDocuments moving: ' + moving.size);
for (const id of moving) {
  const d = srcById.get(id);
  const name = (d.body.match(/m_Name: (.*)/) || [])[1];
  const inst = (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
  console.log('  ' + id.padEnd(22) + 'cls' + d.cls + (d.stripped ? ' stripped' : '') +
    (name !== undefined ? ' "' + name + '"' : '') + (inst ? '  inst ' + inst : ''));
}

// ------------------------------------------------------------------ new ids

function maxId(docs) {
  let max = 1n;
  for (const d of docs) if (BigInt(d.id) > max) max = BigInt(d.id);
  return max;
}
const taken = new Set([...source.docs, ...target.docs].map((d) => d.id));
let next = maxId(target.docs) + 1n;
function freshId() {
  while (taken.has(next.toString())) next += 1n;
  const id = next.toString();
  taken.add(id);
  next += 1n;
  return id;
}
const remap = new Map();
for (const id of moving) remap.set(id, freshId());

// ------------------------------------------------------------------ rewrite references

const dropped = [];
function rewrite(text, where) {
  return text.replace(/\{fileID: (\d+)(, guid: [0-9a-f]+)?\}/g, (whole, id, guid) => {
    // A reference carrying a guid is an asset (a prefab's source object, a mesh, a material): leave it alone.
    if (guid) return whole;
    if (id === '0') return whole;
    if (remap.has(id)) return '{fileID: ' + remap.get(id) + '}';
    if (dstById.has(id)) return whole;                          // a document the Template already has
    if (srcById.has(id)) {
      dropped.push(where + ': ' + id + ' (' + (srcById.get(id).body.match(/m_Name: (.*)/) || [])[1] + ')');
      return '{fileID: 0}';
    }
    dropped.push(where + ': ' + id + ' (missing even in the source)');
    return '{fileID: 0}';
  });
}

const moved = [];
for (const [oldId, newId] of remap) {
  const src = srcById.get(oldId);
  let body = src.body;

  if (src.cls === '1001') {
    // Drop the level's own overrides from the instance's modification list.
    const before = body;
    body = body.replace(
      / {4}- target: \{fileID: \d+, guid: [0-9a-f]+, type: 3\}\n {6}propertyPath: ([^\n]*)\n {6}value: [^\n]*\n {6}objectReference: \{fileID: \d+\}\n/g,
      (whole, prop) => (LEVEL_OVERRIDES.some((re) => re.test(prop)) ? '' : whole),
    );
    if (before !== body) console.log('\nDropped level-only overrides from instance ' + oldId);
  }

  // The level loader on the canvas names the level's own scenes. A template is not a level, so the names go:
  // they are the first thing a new level has to fill in, and leaving them would have it load level 3's.
  if (/guid: c4b1585b316e7a04989302c57ad9e633/.test(body)) {
    body = body.replace(/^  gameplaySceneName: .*$/m, '  gameplaySceneName: ')
               .replace(/^  endScene: .*$/m, '  endScene: ');
    console.log('\nCleared the level loader\'s scene names in doc ' + oldId);
  }

  body = rewrite(body, 'doc ' + oldId);
  moved.push('--- !u!' + src.cls + ' &' + newId + (src.stripped ? ' stripped' : '') + '\n' + body);
}

console.log('\nReferences dropped (they pointed at level content):');
for (const d of [...new Set(dropped)]) console.log('  ' + d);

// ------------------------------------------------------------------ take the old ones out

// Every document belonging to an instance of one of the same prefabs in the Template goes, along with the
// instances themselves - that is the stale rig being replaced.
// The old player truck is a different prefab (the one the levels used before it was replaced), so it is
// named here: leaving it in would give the scene a second, fully drivable truck.
const SUPERSEDED = { 'b33661988ae95f84ca7edb2885e86146': 'Truck (the prefab the levels used before Truck (Player))' };

const oldInstanceIds = new Set();
for (const d of target.docs) {
  if (d.cls !== '1001') continue;
  const guid = (d.body.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1];
  if (RIG[guid] || SUPERSEDED[guid]) oldInstanceIds.add(d.id);
}
const removing = new Set(oldInstanceIds);
for (const d of target.docs) {
  const inst = (d.body.match(/m_PrefabInstance: \{fileID: (\d+)\}/) || [])[1];
  if (inst && oldInstanceIds.has(inst)) removing.add(d.id);
}

console.log('\nRemoving ' + removing.size + ' stale documents (instances + their stripped children)');

// Where the new CanvasUI's own parts are, so a document that survives in the Template and pointed at the old
// canvas's can be pointed at the new one's: matched by the object in the prefab each one stands for.
const canvasGuid = '81c7d71664e97214bb3397d11772ac21';
const bySource = new Map();     // prefab object id -> new document id
for (const [oldId, newId] of remap) {
  const d = srcById.get(oldId);
  const src = (d.body.match(/m_CorrespondingSourceObject: \{fileID: (\d+), guid: ([0-9a-f]+)/) || []);
  if (src[2] === canvasGuid) bySource.set(src[1], newId);
}
const oldCanvasParts = new Map();   // prefab object id -> old document id in the Template
for (const d of target.docs) {
  const src = (d.body.match(/m_CorrespondingSourceObject: \{fileID: (\d+), guid: ([0-9a-f]+)/) || []);
  if (src[2] === canvasGuid) oldCanvasParts.set(src[1], d.id);
}

const rewired = [];
const kept = target.docs.filter((d) => !removing.has(d.id)).map((d) => {
  let body = d.body;
  if (d.cls === '1001' || !d.stripped) {
    body = body.replace(/\{fileID: (\d+)\}/g, (whole, id) => {
      const prefabObject = [...oldCanvasParts.entries()].find(([, docId]) => docId === id);
      if (!prefabObject) return whole;
      const replacement = bySource.get(prefabObject[0]);
      if (!replacement) return whole;
      rewired.push(d.id + ' -> ' + replacement + ' (canvas part ' + prefabObject[0] + ')');
      return '{fileID: ' + replacement + '}';
    });
  }
  return { cls: d.cls, id: d.id, stripped: d.stripped, body };
});

console.log('\nSurviving documents rewired to the new canvas:');
for (const r of rewired) console.log('  ' + r);

// ------------------------------------------------------------------ write it out

const out = target.head + kept.map((d) => '--- !u!' + d.cls + ' &' + d.id + (d.stripped ? ' stripped' : '') + '\n' + d.body).join('') +
  moved.map((t) => t + '\n').join('');

// Every internal reference must resolve, or Unity has a broken scene on its hands.
const check = splitDocs(out);
const present = new Set(check.docs.map((d) => d.id));
let unresolved = 0;
for (const d of check.docs) {
  for (const m of d.body.matchAll(/\{fileID: (\d+)(, guid: [0-9a-f]+)?\}/g)) {
    if (m[1] === '0') continue;
    if (m[2]) continue;   // carries a guid: an asset
    if (!present.has(m[1])) {
      console.log('UNRESOLVED ' + d.cls + ' &' + d.id + ' -> ' + m[1]);
      unresolved++;
    }
  }
}
console.log('\nresult: ' + check.docs.length + ' documents, ' + unresolved + ' unresolved references');

if (APPLY) {
  if (unresolved) {
    console.log('Not writing: there are unresolved references.');
    process.exit(1);
  }
  write(DST, out);
  console.log('Wrote ' + path.relative(ROOT, DST));
} else {
  fs.writeFileSync(path.join(ROOT, '.checktmp/template-out.unity'), out.split('\n').join('\r\n'));
  console.log('Dry run - wrote .checktmp/template-out.unity');
}
