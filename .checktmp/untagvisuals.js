// The two visual copies are film props for the tutorial stage: no scripts, no colliders, no rigidbodies, on a
// layer of their own. They inherited the tags of the prefabs they were copied from though, and a tag is a name
// the whole scene can look up - 'Player' is what ShadowRacer finds its truck by, so a stage copy wearing it is a
// copy that could be handed back as the player's own truck. They are props, so they are Untagged.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const files = [
  'Assets/Prefabs/Utils/Truck (Visual).prefab',
  'Assets/Prefabs/Utils/Adamak (Visual).prefab',
];

for (const file of files) {
  const full = path.join(root, file);
  const text = fs.readFileSync(full, 'utf8');

  const found = [...text.matchAll(/m_TagString: ([A-Za-z]+)/g)].map((m) => m[1]);
  const changed = found.filter((t) => t !== 'Untagged');

  const out = text.replace(/m_TagString: [A-Za-z]+/g, 'm_TagString: Untagged');
  fs.writeFileSync(full, out);

  console.log(`${file}\n  tags were: ${[...new Set(found)].join(', ')}\n  retagged:  ${changed.length} object(s)`);
}
