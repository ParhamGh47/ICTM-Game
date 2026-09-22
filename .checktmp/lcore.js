// Library for reading/writing Unity scene YAML docs, plus a report of the
// objects in a Core scene that a new level needs (settings, terrain, camera,
// UI, truck, light, music).
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..', 'Assets');

function loadDocs(file) {
  const text = fs.readFileSync(file, 'utf8');
  const parts = text.split(/^--- /m);
  const header = parts.shift();
  return { header, docs: parts.map(p => '--- ' + p) };
}

function head(doc) {
  const m = /^--- !u!(\d+) &(\d+)( stripped)?/.exec(doc);
  return m ? { cls: +m[1], id: m[2], stripped: !!m[3] } : null;
}
function field(doc, name) {
  const m = new RegExp('^\\s*' + name + ': ?(.*)$', 'm').exec(doc);
  return m ? m[1].replace(/\r$/, '') : undefined;
}
function nameOf(doc) {
  const n = field(doc, 'm_Name');
  return n === undefined ? '' : n;
}

// guid -> asset path
function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out); else out.push(p);
  }
  return out;
}
function guidMap() {
  const map = new Map();
  for (const f of walk(ROOT)) {
    if (!f.endsWith('.meta')) continue;
    const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(f, 'utf8'));
    if (m) map.set(m[1], f.slice(0, -5).replace(/\\/g, '/').replace(ROOT.replace(/\\/g, '/') + '/', ''));
  }
  return map;
}

if (require.main === module) {
  const file = path.join(ROOT, process.argv[2]);
  const { header, docs } = loadDocs(file);
  const guids = guidMap();
  const byId = new Map(docs.map(d => [head(d).id, d]));

  // gameobjects and their transforms/components
  const go = new Map(), tr = new Map(), comps = new Map();
  for (const d of docs) {
    const h = head(d);
    if (h.stripped) continue;
    if (h.cls === 1) go.set(h.id, d);
    if (h.cls === 4) { const g = field(d, 'm_GameObject'); if (g) tr.set(g.match(/\d+/)[0], d); }
    const owner = field(d, 'm_GameObject');
    if (owner && h.cls !== 4) {
      const id = owner.match(/\d+/)[0];
      if (!comps.has(id)) comps.set(id, []);
      const s = field(d, 'm_Script');
      comps.get(id).push(`cls${h.cls}${s ? ' <' + (guids.get(s.match(/guid: ([0-9a-f]{32})/)[1]) || s.match(/guid: ([0-9a-f]{32})/)[1]) + '>' : ''}`);
    }
  }
  const roots = [...go.keys()].filter(id => tr.has(id) && /m_Father: \{fileID: 0\}/.test(tr.get(id)));
  console.log('### ROOTS');
  for (const id of roots) {
    console.log(`  ${id} "${nameOf(go.get(id))}" ${(comps.get(id) || []).join(', ')}`);
  }

  console.log('\n### SCRIPT-OWNED OBJECTS');
  for (const d of docs) {
    const h = head(d);
    if (h.cls !== 114 || h.stripped) continue;
    const s = field(d, 'm_Script');
    const p = guids.get(s.match(/guid: ([0-9a-f]{32})/)[1]) || '?';
    const owner = field(d, 'm_GameObject');
    const ownerId = owner && owner.match(/\d+/)[0];
    console.log(`  ${p}  on GO ${ownerId} "${ownerId && go.has(ownerId) ? nameOf(go.get(ownerId)) : '?'}"`);
  }

  console.log('\n### AUDIO (cls82)');
  for (const d of docs) {
    const h = head(d);
    if (h.cls !== 82 || h.stripped) continue;
    const owner = field(d, 'm_GameObject');
    const ownerId = owner && owner.match(/\d+/)[0];
    console.log(`  GO ${ownerId} "${ownerId && go.has(ownerId) ? nameOf(go.get(ownerId)) : '?'}"`);
    console.log('   ' + d.split('\n').filter(l => /m_Clip|m_Volume|m_Loop|m_PlayOnAwake|m_SpatialBlend|m_MixerGroup/.test(l)).join('\n   '));
  }

  console.log('\n### PREFAB INSTANCES (interesting)');
  for (const d of docs) {
    const h = head(d);
    if (h.cls !== 1001) continue;
    const src = /m_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-f]{32})/.exec(d);
    const p = src ? guids.get(src[2]) || src[2] : '?';
    if (!/Utils|Triggers|Signs|Pickups|IceCreams|Environments/.test(p)) continue;
    const mods = [...d.matchAll(/propertyPath: (.*)/g)].map(m => m[1].trim());
    console.log(`  ${h.id} ${p} mods=${mods.length}`);
    if (mods.length) console.log('     ' + [...new Set(mods)].join(', '));
  }
}
module.exports = { loadDocs, head, field, nameOf, guidMap, ROOT };
