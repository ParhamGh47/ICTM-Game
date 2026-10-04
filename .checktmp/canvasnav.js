// Which MenuNavigation owns which panel in CanvasUI.prefab, which buttons are under it, and where they sit
// on screen - so the default button (a name, or the top-most fallback) can be checked. Read-only.
//
// Run: node .checktmp/canvasnav.js
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const file = path.join(root, 'Assets', 'Prefabs', 'Utils', 'CanvasUI.prefab');

const navGuid = /guid:\s*([0-9a-f]+)/.exec(
  fs.readFileSync(path.join(root, 'Assets', 'Scripts', 'UI', 'MenuNavigation.cs.meta'), 'utf8'))[1];

const lines = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n').split('\n');

const objects = new Map();      // fileID -> { name, components, active }
const mono = new Map();         // fileID -> { gameObject, script guid }
const transforms = new Map();   // fileID -> { gameObject, father }

let i = 0;
while (i < lines.length) {
  const m = /^--- !u!(\d+) &(\d+)/.exec(lines[i]);
  if (!m) { i++; continue; }

  const type = m[1];
  const id = m[2];

  let end = i + 1;
  while (end < lines.length && !/^--- !u!\d+ &/.test(lines[end])) end++;

  const block = lines.slice(i, end);
  const field = (name) => {
    for (const l of block) {
      const mm = new RegExp('^  ' + name + ': (.*)$').exec(l);
      if (mm) return mm[1];
    }
    return null;
  };
  const idOf = (v) => (v ? /\d+/.exec(v)[0] : '0');

  if (type === '1') {
    const obj = { name: field('m_Name'), components: [], active: field('m_IsActive') };
    const start = block.findIndex((l) => l.trim() === 'm_Component:');
    if (start >= 0) {
      for (let k = start + 1; k < block.length; k++) {
        const cm = /- component: \{fileID: (\d+)\}/.exec(block[k]);
        if (!cm) break;
        obj.components.push(cm[1]);
      }
    }
    objects.set(id, obj);
  } else if (type === '4' || type === '224') {
    transforms.set(id, { gameObject: idOf(field('m_GameObject')), father: idOf(field('m_Father')) });
  } else if (type === '114') {
    const script = field('m_Script');
    mono.set(id, { gameObject: idOf(field('m_GameObject')), guid: script ? (/guid: ([0-9a-f]+)/.exec(script) || [])[1] : null });
  }

  i = end;
}

// uGUI's own script guids: Image and Button are distinct components and only Button navigates.
const buttonGuid = '4e29b1a8efbd4b44bb3f3716e73f07ff';

console.log('Button script guid: ' + buttonGuid);

const isButton = (goId) => {
  const obj = objects.get(String(goId));
  if (!obj) return false;
  return obj.components.some((c) => { const m = mono.get(c); return m && m.guid === buttonGuid; });
};

// RectTransform positions, so the top-most button can be found the way MenuNavigation finds it.
const rects = new Map();
for (let n = 0; n < lines.length; n++) {
  const m = /^--- !u!224 &(\d+)/.exec(lines[n]);
  if (!m) continue;

  let end = n + 1;
  while (end < lines.length && !/^--- !u!\d+ &/.test(lines[end])) end++;

  const block = lines.slice(n, end);
  const field = (name) => {
    for (const l of block) {
      const mm = new RegExp('^  ' + name + ': (.*)$').exec(l);
      if (mm) return mm[1];
    }
    return null;
  };

  const go = field('m_GameObject');
  const anchored = field('m_AnchoredPosition');
  rects.set(go ? /\d+/.exec(go)[0] : null, {
    anchored: anchored || '(none)',
    anchorMin: field('m_AnchorMin'),
    anchorMax: field('m_AnchorMax'),
  });

  n = end - 1;
}

const owners = [];
for (let n = 0; n < lines.length; n++) {
  if (!lines[n].includes('guid: ' + navGuid)) continue;

  let start = n;
  while (start > 0 && !/^--- !u!\d+ &/.test(lines[start])) start--;

  const block = lines.slice(start, n + 40);
  const get = (name) => {
    for (const l of block) {
      const mm = new RegExp('^  ' + name + ': (.*)$').exec(l);
      if (mm) return mm[1];
    }
    return null;
  };

  owners.push({
    owner: (() => { const g = get('m_GameObject'); const obj = g ? objects.get(/\d+/.exec(g)[0]) : null; return obj ? obj.name : '?'; })(),
    goId: get('m_GameObject') ? /\d+/.exec(get('m_GameObject'))[0] : null,
    first: get('firstButtonName'),
    rememberLast: get('rememberLast'),
  });
}

// Descendants by transform parent chain.
function under(rootGoId) {
  const children = new Map();
  for (const t of transforms.values()) {
    if (!children.has(String(t.father))) children.set(String(t.father), []);
    children.get(String(t.father)).push(String(t.gameObject));
  }

  const out = [];
  const walk = (goId) => {
    for (const child of children.get(String(goId)) || []) { out.push(child); walk(child); }
  };
  walk(rootGoId);
  return out;
}

for (const o of owners) {
  const all = [o.goId, ...under(o.goId)];
  const buttons = all.filter((g) => isButton(g));
  const names = buttons.map((g) => objects.get(g).name + ' @' + ((rects.get(g) || {}).anchored || '?'));

  console.log('\n=== MenuNavigation on "' + o.owner + '"  first=' + JSON.stringify(o.first) +
    '  rememberLast=' + o.rememberLast);
  console.log('    buttons (' + buttons.length + '): ' + (names.join('\n                                ') || '(none)'));
}
