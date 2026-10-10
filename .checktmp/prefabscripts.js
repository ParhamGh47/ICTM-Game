// List every MonoBehaviour's script name on a prefab/scene, plus the non-script components that matter.
const fs = require('fs');
const path = require('path');
const { read, splitDocs } = require('./scenelib');

const file = process.argv[2];
const root = path.resolve(__dirname, '..');

// Build guid -> asset path for every .cs in Assets (cheap: read .meta files).
function guidMap(dir, out) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) { guidMap(p, out); continue; }
    if (!entry.name.endsWith('.cs.meta')) continue;
    const g = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]+)/);
    if (g) out[g[1]] = entry.name.replace(/\.cs\.meta$/, '');
  }
  return out;
}
const scripts = guidMap(path.join(root, 'Assets'), {});

const { docs } = splitDocs(read(path.join(root, file)));
const counts = new Map();
for (const d of docs) {
  if (d.cls !== '114') continue;
  const m = d.body.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/);
  const name = m ? (scripts[m[1]] || '?' + m[1]) : '?';
  counts.set(name, (counts.get(name) || 0) + 1);
}
const classNames = { '198': 'ParticleSystem', '199': 'ParticleSystemRenderer', '82': 'AudioSource', '23': 'MeshRenderer', '33': 'MeshFilter', '169': 'SkinnedMeshRenderer', '96': 'TrailRenderer', '65': 'BoxCollider', '108': 'Light', '54': 'Rigidbody', '120': 'LineRenderer', '137': 'Animator', '95': 'Animator' };
for (const d of docs) {
  if (classNames[d.cls]) counts.set(classNames[d.cls], (counts.get(classNames[d.cls]) || 0) + 1);
}
for (const [k, v] of [...counts].sort((a, b) => b[1] - a[1])) console.log(String(v).padStart(4) + '  ' + k);
