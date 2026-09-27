// Validate a Unity YAML asset (prefab or scene): duplicate anchors, dangling local references, component
// lists that disagree with the documents claiming them, and parent/child lists that disagree with each
// other. Read-only. Usage: node .checktmp/prefabcheck.js <file> [<file> ...]
const fs = require('fs');

const CLASS = { '1': 'GameObject', '4': 'Transform', '224': 'RectTransform', '222': 'CanvasRenderer', '114': 'MonoBehaviour', '223': 'Canvas', '20': 'Camera' };

function check(file) {
  const raw = fs.readFileSync(file, 'utf8');
  const docs = raw.split(/\r?\n(?=--- !u!\d+)/).filter(t => /^--- !u!\d+/.test(t));
  const problems = [];
  const byId = new Map();
  const dupes = [];

  for (const text of docs) {
    const m = text.match(/^--- !u!(\d+) &(\d+)( stripped)?/);
    if (!m) continue;
    const id = m[2];
    if (byId.has(id)) dupes.push(id);
    byId.set(id, { cls: m[1], kind: CLASS[m[1]] || m[1], text, stripped: !!m[3] });
  }

  const field = (block, name) => {
    const m = block.match(new RegExp('^ *' + name + ': ?(.*)$', 'm'));
    return m ? m[1].trim() : null;
  };

  // Every local reference (a fileID without a matching guid on the same line) must resolve.
  for (const [id, d] of byId) {
    for (const line of d.text.split(/\r?\n/)) {
      if (/guid:/.test(line)) continue;
      for (const r of line.matchAll(/\{fileID: (\d+)\}/g)) {
        const target = r[1];
        if (target === '0') continue;
        if (!byId.has(target)) problems.push(`${d.kind} ${id}: dangling reference to ${target} in "${line.trim()}"`);
      }
    }
  }

  // GameObjects: component list must match the documents that claim this GameObject.
  const claims = new Map();
  for (const [id, d] of byId) {
    if (d.kind === 'GameObject') continue;
    const go = field(d.text, 'm_GameObject');
    if (!go) continue;
    const gid = (go.match(/\d+/) || [])[0];
    if (!gid) continue;
    if (!claims.has(gid)) claims.set(gid, []);
    claims.get(gid).push(id);
  }

  for (const [id, d] of byId) {
    if (d.kind !== 'GameObject') continue;
    const listed = [...(d.text.match(/m_Component:\r?\n((?:  - component: \{fileID: \d+\}\r?\n)*)/) || [, ''])[1]
      .matchAll(/fileID: (\d+)/g)].map(x => x[1]);
    const claimed = claims.get(id) || [];
    const name = field(d.text, 'm_Name');
    for (const c of listed) {
      if (!byId.has(c)) problems.push(`GameObject ${id} (${name}): component ${c} missing`);
      else if (!claimed.includes(c)) problems.push(`GameObject ${id} (${name}): lists component ${c} (${byId.get(c).kind}) which does not point back at it`);
    }
    for (const c of claimed) {
      if (!listed.includes(c)) problems.push(`GameObject ${id} (${name}): ${byId.get(c).kind} ${c} points at it but is not in m_Component`);
    }
  }

  // RectTransforms: child lists must agree with m_Father, and nothing may be orphaned.
  const parentOf = new Map();
  for (const [id, d] of byId) {
    if (d.kind !== 'RectTransform' && d.kind !== 'Transform') continue;
    const dad = field(d.text, 'm_Father');
    if (dad) parentOf.set(id, (dad.match(/\d+/) || [])[0]);
  }
  const listedChild = new Set();
  for (const [id, d] of byId) {
    if (d.kind !== 'RectTransform' && d.kind !== 'Transform') continue;
    const m = d.text.match(/m_Children:\r?\n((?:  - \{fileID: \d+\}\r?\n)*)/);
    if (!m) continue;
    for (const c of [...m[1].matchAll(/fileID: (\d+)/g)].map(x => x[1])) {
      listedChild.add(c);
      if (!byId.has(c)) problems.push(`transform ${id}: child ${c} missing`);
      else if (parentOf.get(c) !== id) problems.push(`transform ${id}: lists child ${c} whose m_Father is ${parentOf.get(c)}`);
    }
  }
  for (const [id, dad] of parentOf) {
    if (!dad || dad === '0') continue;
    if (!byId.has(dad)) { problems.push(`transform ${id}: m_Father ${dad} missing`); continue; }
    if (!listedChild.has(id)) problems.push(`transform ${id} (${goName(byId, id)}): m_Father ${dad} does not list it as a child`);
  }

  function goName(map, t) {
    const d = map.get(t);
    if (!d) return '?';
    const gid = (field(d.text, 'm_GameObject') || '').match(/\d+/);
    if (!gid) return '?';
    const go = map.get(gid[0]);
    return go ? field(go.text, 'm_Name') : '?';
  }

  const goCount = [...byId.values()].filter(d => d.kind === 'GameObject').length;
  console.log(`${file}`);
  console.log(`  ${docs.length} docs, ${goCount} GameObjects`);
  if (dupes.length) console.log('  DUPLICATE ANCHORS: ' + dupes.join(', '));
  if (problems.length === 0 && dupes.length === 0) console.log('  OK - no problems found');
  else for (const p of problems) console.log('  ! ' + p);
  return problems.length + dupes.length;
}

let bad = 0;
for (const f of process.argv.slice(2)) bad += check(f);
console.log(bad === 0 ? '\nALL CLEAN' : `\n${bad} PROBLEM(S)`);
