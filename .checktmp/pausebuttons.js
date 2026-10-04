// The pause and game-over panels: which buttons they hold, where each sits, and what each panel's
// MenuNavigation resolves its default button to. MenuNavigation falls back on the top-most button by y, so
// the anchored positions are what decide it.
//
// Run: node .checktmp/pausebuttons.js
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const file = path.join(root, 'Assets', 'Prefabs', 'Utils', 'CanvasUI.prefab');
const lines = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n').split('\n');

const navGuid = /guid:\s*([0-9a-f]+)/.exec(
  fs.readFileSync(path.join(root, 'Assets', 'Scripts', 'UI', 'MenuNavigation.cs.meta'), 'utf8'))[1];
const buttonGuid = '4e29b1a8efbd4b44bb3f3716e73f07ff';

const names = new Map();       // gameObject id -> name
const rects = new Map();       // gameObject id -> { father, anchored, anchorMin, y0 }
const buttons = new Set();     // gameObject ids carrying a Button
const monoOf = new Map();      // component id -> script guid

function field(block, name) {
  for (const l of block) {
    const m = new RegExp('^  ' + name + ': (.*)$').exec(l);
    if (m) return m[1];
  }
  return null;
}

const idOf = (v) => (v ? (/\d+/.exec(v) || ['0'])[0] : null);

for (let i = 0; i < lines.length;) {
  const m = /^--- !u!(\d+) &(\d+)/.exec(lines[i]);
  if (!m) { i++; continue; }

  const type = m[1];
  const id = m[2];

  let end = i + 1;
  while (end < lines.length && !/^--- !u!\d+ &/.test(lines[end])) end++;
  const block = lines.slice(i, end);

  if (type === '1') names.set(id, field(block, 'm_Name'));

  if (type === '224' || type === '4') {
    const go = idOf(field(block, 'm_GameObject'));
    if (go) rects.set(go, {
      father: idOf(field(block, 'm_Father')),
      anchored: field(block, 'm_AnchoredPosition'),
      anchorMin: field(block, 'm_AnchorMin'),
    });
  }

  if (type === '114') {
    const go = idOf(field(block, 'm_GameObject'));
    const script = field(block, 'm_Script');
    const guid = script ? (/guid: ([0-9a-f]+)/.exec(script) || [])[1] : null;
    if (guid === buttonGuid && go) buttons.add(go);
    if (guid === navGuid && go) monoOf.set(go, true);
  }

  i = end;
}

// Children, by father.
const children = new Map();
for (const [go, r] of rects) {
  if (!r.father) continue;
  if (!children.has(r.father)) children.set(r.father, []);
  children.get(r.father).push(go);
}

function chain(go) {
  const out = [];
  let cur = go;
  for (let guard = 0; cur && guard < 30; guard++) {
    const r = rects.get(cur) || {};
    out.push((names.get(cur) || '?') + (buttons.has(cur) ? '[B]' : ''));
    cur = r.father;
  }
  return out.join(' < ');
}

function under(go) {
  const out = [];
  const walk = (id) => { for (const c of children.get(id) || []) { out.push(c); walk(c); } };
  walk(go);
  return out;
}

const navOwners = [];
for (const [go] of monoOf) navOwners.push(go);

console.log('Buttons in CanvasUI.prefab (' + buttons.size + '):');
for (const go of buttons) console.log('  ' + chain(go) + '   anchored=' + ((rects.get(go) || {}).anchored || '?'));

console.log('\nMenuNavigation owners and what their buttons are:');
for (const owner of navOwners) {
  const all = [owner, ...under(owner)];
  const own = all.filter((g) => buttons.has(g));

  console.log('\n  "' + (names.get(owner) || '?') + '"');
  for (const g of own) console.log('     ' + (names.get(g) || '?') + '  anchored=' + ((rects.get(g) || {}).anchored || '?'));
}
