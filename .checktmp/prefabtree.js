// Dump a Unity prefab/scene as an object tree, with each GameObject's component types.
// Usage: node .checktmp/prefabtree.js <file> [nameFilter]
const fs = require('fs');

const file = process.argv[2];
const filter = process.argv[3];
const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');

const blocks = [];
const re = /^--- !u!(\d+) &(\d+)(?: stripped)?\n([\s\S]*?)(?=^--- |\Z)/gm;
let m;
while ((m = re.exec(text)) !== null) {
  blocks.push({ classId: m[1], fileId: m[2], body: m[3] });
}

const byId = new Map();
for (const b of blocks) {
  const nameMatch = /^  m_Name: (.*)$/m.exec(b.body);
  const goMatch = /^  m_GameObject: \{fileID: (\d+)\}/m.exec(b.body);
  byId.set(b.fileId, {
    classId: b.classId,
    name: nameMatch ? nameMatch[1] : null,
    go: goMatch ? goMatch[1] : null,
    body: b.body,
  });
}

const classNames = {
  1: 'GameObject', 4: 'Transform', 224: 'RectTransform', 114: 'MonoBehaviour',
  65: 'BoxCollider', 135: 'SphereCollider', 54: 'Rigidbody', 222: 'CanvasRenderer',
  223: 'Canvas', 225: 'CanvasGroup', 1140: 'CanvasScaler', 1146: 'GraphicRaycaster',
  1001: 'PrefabInstance', 33: 'MeshFilter', 23: 'MeshRenderer', 81: 'AudioListener',
  20: 'Camera', 108: 'Light',
};

// script guid -> name (from .cs.meta files), best effort
const guidNames = new Map();
function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = dir + '/' + entry.name;
    if (entry.isDirectory()) walk(p);
    else if (entry.name.endsWith('.cs.meta')) {
      const g = /guid: (\w+)/.exec(fs.readFileSync(p, 'utf8'));
      if (g) guidNames.set(g[1], entry.name.replace('.cs.meta', ''));
    }
  }
}
walk('Assets/Scripts');

function scriptName(body) {
  const g = /m_Script: \{fileID: \d+, guid: (\w+)/.exec(body);
  if (!g) return 'MonoBehaviour';
  return guidNames.get(g[1]) || ('MonoBehaviour(' + g[1].slice(0, 8) + ')');
}

// GameObject -> components
const gameObjects = new Map();
for (const [id, b] of byId) {
  if (b.classId === '1') gameObjects.set(id, { fileId: id, name: b.name, comps: [], children: [], parent: null });
}
for (const [id, b] of byId) {
  const compMatch = /^  m_Component:\n((?:  - component: \{fileID: \d+\}\n)*)/m.exec(b.body);
  if (!compMatch) continue;
  const ids = [...compMatch[1].matchAll(/fileID: (\d+)/g)].map((x) => x[1]);
  const go = gameObjects.get(id);
  if (!go) continue;
  for (const cid of ids) {
    const comp = byId.get(cid);
    if (!comp) continue;
    if (comp.classId === '4' || comp.classId === '224') {
      // Transform: pull children and father
      const kids = [...(/^  m_Children:\n((?:  - \{fileID: \d+\}\n)*)/m.exec(comp.body)?.[1] || '').matchAll(/fileID: (\d+)/g)].map((x) => x[1]);
      const father = /  m_Father: \{fileID: (\d+)\}/.exec(comp.body);
      go.transformId = cid;
      go.children = kids;
      go.father = father ? father[1] : null;
    } else {
      go.comps.push(classNames[comp.classId] || (classNames[comp.classId] = classNames[comp.classId]) || scriptName(comp.body));
    }
  }
}
for (const go of gameObjects.values()) {
  if (go.comps.length === 0 && go.children.length === 0) continue;
}

const byTransform = new Map();
for (const go of gameObjects.values()) if (go.transformId) byTransform.set(go.transformId, go);

const childOf = new Set();
for (const go of gameObjects.values()) for (const k of go.children) childOf.add(k);
const roots = [...gameObjects.values()].filter((go) => !childOf.has(go.transformId)).map((go) => go.transformId);

function print(id, depth) {
  const go = byTransform.get(id);
  if (!go) return;
  const isTarget = !filter || go.name.toLowerCase().includes(filter.toLowerCase());
  const info = go.name + '  #' + go.fileId + (go.comps.length ? '   [' + go.comps.join(', ') + ']' : '');
  if (isTarget || depth === 0) console.log('  '.repeat(depth) + info);
  for (const k of go.children) print(k, depth + 1);
}

console.log('=== ' + file + ' (' + gameObjects.size + ' game objects) ===');
for (const r of roots) print(r, 0);
