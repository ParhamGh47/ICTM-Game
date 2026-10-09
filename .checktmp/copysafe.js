// Which assets does a scene depend on? A scene copied to make a new level drags all of them along, so the
// interesting ones are the assets that belong to ONE level: its terrain, its road, its lighting data, its
// post-processing profile, its soundtrack. Sharing those between two scenes is how editing Core-5's terrain
// quietly edits Core-3's.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');

// guid -> asset path, from every .meta in the project.
const byGuid = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== '.git' && e.name !== '.checktmp') walk(p); continue; }
    if (!e.name.endsWith('.meta')) continue;
    const m = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]+)/);
    if (m) byGuid.set(m[1], path.relative(ROOT, p.replace(/\.meta$/, '')).replace(/\\/g, '/'));
  }
})(ROOT);

function refs(file) {
  const text = fs.readFileSync(path.join(ROOT, file), 'utf8');
  const out = new Map();
  for (const m of text.matchAll(/guid: ([0-9a-f]+)/g)) {
    const asset = byGuid.get(m[1]);
    if (!asset) continue;
    // Only the assets, not the scripts: a script is code and is meant to be shared.
    if (asset.endsWith('.cs') || asset.endsWith('.asmdef')) continue;
    out.set(m[1], asset);
  }
  return out;
}

// The groups worth naming, by extension/folder.
function classify(asset) {
  if (/\/PP Profiles\//i.test(asset)) return 'post-processing profile';
  if (/\.(asset)$/i.test(asset) && /terrain|Terrain/i.test(asset)) return 'terrain data';
  if (/lighting|Lightmap|_GI|LightingData/i.test(asset)) return 'lighting';
  if (/\.(unity)$/i.test(asset)) return 'scene';
  if (/\.(prefab)$/i.test(asset)) return 'prefab';
  if (/\.(png|jpg|jpeg|tga|psd)$/i.test(asset)) return 'texture';
  if (/\.(mat)$/i.test(asset)) return 'material';
  if (/\.(mp3|wav|ogg|aiff)$/i.test(asset)) return 'audio';
  if (/\.(fbx|obj|blend)$/i.test(asset)) return 'model';
  if (/\.(controller|overrideController)$/i.test(asset)) return 'animator';
  if (/\.(mixer)$/i.test(asset)) return 'audio mixer';
  return 'other';
}

const scenes = process.argv.slice(2);
const maps = {};
for (const s of scenes) maps[s] = refs(s);

const base = scenes[0];
const others = scenes.slice(1);

console.log('### ' + base);
const grouped = new Map();
for (const asset of maps[base].values()) {
  const g = classify(asset);
  if (!grouped.has(g)) grouped.set(g, []);
  grouped.get(g).push(asset);
}
for (const [g, list] of [...grouped].sort()) {
  console.log('  ' + g + ' (' + list.length + ')');
  for (const a of list.sort()) console.log('    ' + a);
}

// Assets referenced by BOTH the base and any level = shared, so editing one edits the other. Assets
// referenced only by the base are the ones a copy carries over on its own.
console.log('\n### references the base SHARES with another scene (editing one edits both)');
let shared = 0;
for (const other of others) {
  const both = [];
  for (const [guid, asset] of maps[base]) if (maps[other].has(guid)) both.push(asset);
  const notable = both.filter((a) => !/^Assets\/(Prefabs|Scripts|Sprites)\//.test(a) && !/^Assets\/RoadArchitect\//.test(a));
  console.log('  vs ' + other + ': ' + notable.length + ' notable of ' + both.length + ' total');
  for (const a of notable.sort()) { console.log('    ' + a); shared++; }
}
if (!shared) console.log('  (none)');
