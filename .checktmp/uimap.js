// Maps a Unity scene/prefab's UI: hierarchy tree, buttons with their label text,
// MenuNavigation components and their fields.
const fs = require('fs');
const file = process.argv[2];
const raw = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');

const NAV = process.argv[3];   // MenuNavigation script guid
const BUTTON = '4e29b1a8efbd4b44bb3f3716e73f07ff'; // UnityEngine.UI.Button
const TEXT = '5f7201a12d95ffc409449d95f23cf332';   // UnityEngine.UI.Text
const IMAGE = 'fe87c0e1cc204ed48ad3b37840f39efc';  // UnityEngine.UI.Image
const TMP = 'f4688fdb7df04437aeb418b961361dc5';     // TextMeshProUGUI

// split into documents
const docs = new Map(); // fileID -> {class, text}
const order = [];
let cur = null, buf = [];
for (const line of raw.split('\n')) {
  const m = /^--- !u!(\d+) &(\d+)/.exec(line);
  if (m) {
    if (cur) docs.set(cur.id, { cls: cur.cls, text: buf.join('\n') });
    cur = { cls: m[1], id: m[2] };
    order.push(m[2]);
    buf = [];
    continue;
  }
  if (cur) buf.push(line);
}
if (cur) docs.set(cur.id, { cls: cur.cls, text: buf.join('\n') });

const goName = new Map();
for (const id of order) {
  const d = docs.get(id);
  if (d.cls !== '1') continue;
  const n = /^  m_Name: (.*)$/m.exec(d.text);
  goName.set(id, n ? n[1] : '');
}

// RectTransform / Transform: gameObject + children + father
const rt = new Map(); // rtId -> {go, children[], father}
for (const id of order) {
  const d = docs.get(id);
  if (d.cls !== '224' && d.cls !== '4') continue;
  const go = /m_GameObject: \{fileID: (\d+)\}/.exec(d.text);
  const kids = [...d.text.matchAll(/^  - \{fileID: (\d+)\}$/gm)].map(x => x[1]);
  const father = /m_Father: \{fileID: (\d+)\}/.exec(d.text);
  rt.set(id, { go: go ? go[1] : null, children: kids, father: father ? father[1] : null, cls: d.cls });
}

// which transform belongs to each GameObject
const goTransform = new Map();
for (const [id, r] of rt) if (r.go && !goTransform.has(r.go)) goTransform.set(r.go, id);

// children recttransform -> owner GameObject
function labelOf(goId, depth) {
  const labels = [];
  const stack = [{ go: goId, d: 0 }];
  const seen = new Set();
  while (stack.length) {
    const { go, d } = stack.pop();
    if (seen.has(go) || d > depth) continue;
    seen.add(go);
    const tid = goTransform.get(go);
    const r = tid ? rt.get(tid) : null;
    if (!r) continue;
    for (const kid of r.children) {
      const kr = rt.get(kid);
      if (!kr) continue;
      const txt = compText(kr.go);
      if (txt != null) labels.push(txt);
      stack.push({ go: kr.go, d: d + 1 });
    }
  }
  return labels;
}

function compText(goId) {
  const tid = goTransform.get(goId);
  const r = tid ? rt.get(tid) : null;
  if (!r) return null;
  // components of that GameObject
  const d = docs.get(r.go ?? '');
  return null;
}

// better: gather all MonoBehaviour docs grouped by GameObject
const comps = new Map(); // goId -> [docs]
for (const id of order) {
  const d = docs.get(id);
  const go = /m_GameObject: \{fileID: (\d+)\}/.exec(d.text);
  if (!go) continue;
  if (!comps.has(go[1])) comps.set(go[1], []);
  comps.get(go[1]).push({ id, cls: d.cls, text: d.text });
}

function textsUnder(goId, maxDepth = 9) {
  const out = [];
  const stack = [{ go: goId, d: 0 }];
  const seen = new Set();
  while (stack.length) {
    const { go, d } = stack.pop();
    if (seen.has(go) || d > maxDepth) continue;
    seen.add(go);
    for (const c of comps.get(go) || []) {
      const sm = /m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})/.exec(c.text);
      if (!sm) continue;
      if (sm[1] === TEXT || sm[1] === TMP) {
        const t = /^  m_Text: (.*)$/m.exec(c.text);
        const en = /m_Enabled: (\d)/.exec(c.text);
        if (t && en && en[1] === '1') out.push(t[1].trim());
      }
    }
    const tid = goTransform.get(go);
    const r = tid ? rt.get(tid) : null;
    if (r) for (const kid of r.children) stack.push({ go: rt.get(kid)?.go, d: d + 1 });
  }
  return out;
}

// ---- report buttons and nav components ----
console.log('=== MenuNavigation components ===');
for (const [goId, list] of comps) {
  for (const c of list) {
    const sm = /m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})/.exec(c.text);
    if (!sm || sm[1] !== NAV) continue;
    const en = /m_Enabled: (\d)/.exec(c.text);
    const first = /firstButtonName: (.*)$/m.exec(c.text);
    const soe = /selectOnEnable: (\d)/.exec(c.text);
    const ksa = /keepSelectionAlive: (\d)/.exec(c.text);
    const rl = /rememberLast: (\d)/.exec(c.text);
    console.log(`  host "${goName.get(goId)}" (${goId}) enabled=${en ? en[1] : '?'}` +
      ` first="${first ? first[1] : ''}" selectOnEnable=${soe ? soe[1] : '?'}` +
      ` keepSelectionAlive=${ksa ? ksa[1] : '?'} rememberLast=${rl ? rl[1] : '?'}`);
    console.log(`     buttons: ${JSON.stringify(textsUnder(goId, 3))}`);
  }
}

console.log('=== Buttons (host GameObject -> labels) ===');
for (const [goId, list] of comps) {
  const isButton = list.some(c => {
    const sm = /m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})/.exec(c.text);
    return sm && sm[1] === BUTTON;
  });
  if (!isButton) continue;
  const btn = list.find(c => /m_Script: \{fileID: 11500000, guid: 4e29b1a8efbd4b44bb3f3716e73f07ff/.test(c.text));
  const onClick = /m_MethodName: (.*)$/m.exec(btn.text);
  const interactable = /m_Interactable: (\d)/.exec(btn.text);
  console.log(`  "${goName.get(goId)}" (${goId}) labels=${JSON.stringify(textsUnder(goId))}` +
    ` onClick=${onClick ? onClick[1] : '(none)'} interactable=${interactable ? interactable[1] : '?'}`);
}

console.log('=== Images (host -> enabled/sprite/color) ===');
for (const [goId, list] of comps) {
  for (const c of list) {
    const sm = /m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})/.exec(c.text);
    if (!sm || sm[1] !== IMAGE) continue;
    const en = /m_Enabled: (\d)/.exec(c.text);
    const col = /m_Color: \{r: ([-.\d]+), g: ([-.\d]+), b: ([-.\d]+), a: ([-.\d]+)\}/.exec(c.text);
    const rt2 = /m_RaycastTarget: (\d)/.exec(c.text);
    const spr = /m_Sprite: \{fileID: (-?\d+), guid: ([0-9a-f]+)/.exec(c.text);
    console.log(`  "${goName.get(goId)}" (${goId}) enabled=${en ? en[1] : '?'} raycast=${rt2 ? rt2[1] : '?'}` +
      ` color=${col ? [col[1], col[2], col[3], col[4]].join(',') : '?'} sprite=${spr ? spr[2] : 'none'}`);
  }
}
