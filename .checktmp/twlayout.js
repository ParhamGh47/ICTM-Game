// Reads the typewriter scenes straight out of the YAML and prints the layout of the boxes that matter:
// the story text, and the three buttons under it. No Unity, no guessing - the numbers below are the
// same ones the editor shows in the RectTransform inspector.
const fs = require('fs'), path = require('path');

const root = process.argv[2] || '.';
const scenes = [];
for (const dir of fs.readdirSync(path.join(root, 'Assets/Scenes/Levels Scenes'))) {
  const full = path.join(root, 'Assets/Scenes/Levels Scenes', dir);
  if (!fs.statSync(full).isDirectory()) continue;
  for (const f of fs.readdirSync(full)) {
    if (/^TW-.*\.unity$/.test(f)) scenes.push(path.join(full, f));
  }
}
scenes.sort();

// guid -> our scripts we care about
const guidOf = {};
for (const f of ['Assets/Scripts/UI/StoryTypeWriter.cs.meta']) {
  guidOf[fs.readFileSync(path.join(root, f), 'utf8').match(/guid: ([0-9a-f]+)/)[1]] = 'StoryTypeWriter';
}

function parse(file) {
  const s = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
  const parts = s.split(/^--- /m);
  const objs = {};
  for (const p of parts) {
    const m = p.match(/^!u!(\d+) &(\d+)/);
    if (!m) continue;
    objs[m[2]] = { cls: m[1], body: p };
  }
  return objs;
}

for (const file of scenes) {
  const objs = parse(file);
  const val = (body, key) => {
    const m = body.match(new RegExp('^  ' + key + ': (.*)$', 'm'));
    return m ? m[1] : null;
  };
  const vec = (body, key) => {
    const m = body.match(new RegExp('^  ' + key + ': \\{x: ([-0-9.e]+), y: ([-0-9.e]+)\\}', 'm'));
    return m ? [Number(m[1]), Number(m[2])] : null;
  };
  const nameOf = id => {
    const o = objs[id];
    if (!o) return '?';
    const m = o.body.match(/m_Name: (.*)/);
    return m ? m[1] : '(unnamed)';
  };
  // GameObject -> components
  const comps = {};
  for (const id in objs) {
    const o = objs[id];
    const g = o.body.match(/m_GameObject: \{fileID: (\d+)\}/);
    if (g && o.cls !== '1') {
      (comps[g[1]] = comps[g[1]] || []).push(id);
    }
  }
  const goOf = {}; // component id -> gameobject id
  for (const id in objs) {
    const g = objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/);
    if (g) goOf[id] = g[1];
  }

  console.log('===== ' + file.replace(/\\/g, '/'));
  const rows = [];
  for (const id in objs) {
    const o = objs[id];
    if (o.cls !== '224') continue;               // RectTransform
    const name = nameOf(goOf[id]);
    const kids = (o.body.match(/m_Children:/) ? o.body.split('m_Children:')[1].split('m_Father:')[0] : '');
    const childIds = [...kids.matchAll(/fileID: (\d+)/g)].map(m => m[1]).map(cid => nameOf(goOf[cid] || cid));
    // what is on this object
    const own = (comps[goOf[id]] || []).map(cid => {
      const c = objs[cid];
      if (!c) return null;
      const g = c.body.match(/m_Script: \{fileID: (-?\d+), guid: ([0-9a-f]+)/);
      if (!g) return 'RectTransform';
      if (guidOf[g[2]]) return guidOf[g[2]];
      return null;
    }).filter(Boolean);
    rows.push({ name, size: vec(o.body, 'm_SizeDelta'), pos: vec(o.body, 'm_AnchoredPosition'), min: vec(o.body, 'm_AnchorMin'), max: vec(o.body, 'm_AnchorMax'), pivot: vec(o.body, 'm_Pivot'), own, childIds });
  }
  for (const r of rows) {
    console.log(('  ' + r.name).padEnd(26) +
      ' size ' + (r.size ? r.size.map(n => n.toFixed(0)).join('x') : '?').padEnd(11) +
      ' pos ' + (r.pos ? r.pos.map(n => n.toFixed(0)).join(',').padEnd(13) : '?         ') +
      ' anchor ' + (r.min ? r.min.map(n => n.toFixed(2)).join(',') + ' .. ' + r.max.map(n => n.toFixed(2)).join(',') : '?') +
      ' pivot ' + (r.pivot ? r.pivot.map(n => n.toFixed(2)).join(',') : '?') +
      (r.own.length ? '  [' + r.own.join(',') + ']' : '') +
      (r.childIds.length ? "  kids: " + r.childIds.join(",") : ""));
  }
}
