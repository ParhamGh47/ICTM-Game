// Inspect a Unity scene: scene settings (skybox/fog), prefab instances and their
// source prefabs, root GameObjects, and the component scripts on the roots.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..', 'Assets');
const scene = process.argv[2];

// guid -> path
function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out); else out.push(p);
  }
  return out;
}
const guidToPath = new Map();
for (const f of walk(ROOT)) {
  if (!f.endsWith('.meta')) continue;
  const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(f, 'utf8'));
  if (m) guidToPath.set(m[1], f.slice(0, -5).replace(/\\/g, '/').replace(ROOT.replace(/\\/g, '/') + '/', ''));
}

const text = fs.readFileSync(path.join(ROOT, scene), 'utf8');
const docs = text.split(/^--- /m).slice(1).map(d => '--- ' + d);

function head(doc) {
  const m = /^--- !u!(\d+) &(\d+)/.exec(doc);
  return m ? { cls: m[1], id: m[2] } : null;
}
function nameOf(doc) {
  const m = /^\s*m_Name: (.*)$/m.exec(doc);
  return m ? m[1] : '';
}

// prefab instances
console.log('=== PREFAB INSTANCES ===');
for (const d of docs) {
  const h = head(d);
  if (!h || h.cls !== '1001') continue;
  const g = /m_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-f]{32})/.exec(d);
  const n = /propertyPath: m_Name\n\s*value: (.*)/.exec(d);
  console.log(`  ${h.id}  ${g ? guidToPath.get(g[2]) || g[2] : '?'}   ${n ? 'name=' + n[1] : ''}`);
}

// render settings
console.log('\n=== SETTINGS ===');
for (const d of docs) {
  const h = head(d);
  if (!h) continue;
  if (h.cls === '104') {
    const sky = /m_SkyboxMaterial: \{fileID: (-?\d+), guid: ([0-9a-f]{32})/.exec(d);
    const fog = /m_Fog: (\d)/.exec(d);
    const fogc = /m_FogColor: \{r: ([-\d.]+), g: ([-\d.]+), b: ([-\d.]+)/.exec(d);
    const amb = /m_AmbientSkyColor: \{r: ([-\d.]+), g: ([-\d.]+), b: ([-\d.]+)/.exec(d);
    const ambint = /m_AmbientIntensity: ([-\d.]+)/.exec(d);
    console.log(`  RenderSettings skybox=${sky ? guidToPath.get(sky[2]) || sky[2] : 'none'} fog=${fog && fog[1]} fogColor=${fogc ? [fogc[1], fogc[2], fogc[3]].join(',') : ''} ambSky=${amb ? [amb[1], amb[2], amb[3]].join(',') : ''} ambInt=${ambint ? ambint[1] : ''}`);
  }
  if (h.cls === '157' || h.cls === '196' || h.cls === '29') console.log(`  class ${h.cls}`);
}

// root game objects: those whose Transform's m_Father is 0, and their components
const goDocs = new Map();
for (const d of docs) {
  const h = head(d);
  if (h && h.cls === '1') goDocs.set(h.id, d);
}
const goOfTransform = new Map();
for (const d of docs) {
  const h = head(d);
  if (!h || h.cls !== '4') continue;
  const go = /m_GameObject: \{fileID: (\d+)\}/.exec(d);
  const fam = /m_Father: \{fileID: (\d+)\}/.exec(d);
  if (go && fam) goOfTransform.set(go[1], fam[1]);
}
const rootIds = [...goDocs.keys()].filter(id => goOfTransform.get(id) === '0');
console.log('\n=== ROOT GAMEOBJECTS ===');
// map component -> owner gameobject
const compOwner = new Map();
for (const d of docs) {
  const h = head(d);
  if (!h || h.cls === '1' || h.cls === '1001') continue;
  const go = /m_GameObject: \{fileID: (\d+)\}/.exec(d);
  if (go) {
    if (!compOwner.has(go[1])) compOwner.set(go[1], []);
    const script = /m_Script: \{fileID: (-?\d+), guid: ([0-9a-f]{32})/.exec(d);
    compOwner.get(go[1]).push(`cls${h.cls}${script ? ':' + (guidToPath.get(script[2]) || script[2]) : ''}`);
  }
}
for (const id of rootIds) {
  const d = goDocs.get(id);
  const comps = compOwner.get(id) || [];
  const xid = doc => head(doc).id;
  const kids = [...goDocs.values()].filter(x => {
    return goOfTransform.get(xid(x)) === id;
  }).length;
  console.log(`  ${id}  "${nameOf(d)}"  children=${kids}  comps=[${comps.join(', ')}]`);
}
console.log(`\ntotal GameObjects: ${goDocs.size}, root: ${rootIds.length}`);
