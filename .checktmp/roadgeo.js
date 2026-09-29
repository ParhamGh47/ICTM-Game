// The road's own geometry, as a level has it: where the spline runs (which is what a reset places the truck
// by) against where the road mesh actually is (which is what a truck may be put down on).
const fs = require('fs');

const scene = process.argv[2];
const txt = fs.readFileSync(scene, 'utf8').replace(/\r/g, '');
const docs = [];
for (const b of txt.split(/^--- /m).slice(1)) {
  const m = b.match(/^!u!(\d+) &(\d+)/);
  if (!m) continue;
  docs.push({ cls: +m[1], id: m[2], body: b });
}
const byId = {};
for (const d of docs) byId[d.id] = d;

const f = (body, name) => {
  const m = body.match(new RegExp('^\\s*' + name + ': ?(.*)$', 'm'));
  return m ? m[1].trim() : null;
};
const ref = (s) => (s || '').match(/fileID: (\d+)/);
const vec = (s) => {
  const m = (s || '').match(/x: ([-0-9.e]+), y: ([-0-9.e]+), z: ([-0-9.e]+)/);
  return m ? [+m[1], +m[2], +m[3]] : null;
};

const scripts = {};
for (const p of walk('Assets')) {
  if (!p.endsWith('.cs.meta')) continue;
  const g = fs.readFileSync(p, 'utf8').match(/guid: ([0-9a-f]{32})/);
  if (g) scripts[g[1]] = p.replace(/^.*[\\/]/, '').replace(/\.cs\.meta$/, '');
}
function* walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = dir + '/' + e.name;
    if (e.isDirectory()) yield* walk(p); else yield p;
  }
}
const roadGuid = Object.keys(scripts).find((g) => scripts[g] === 'Road');
console.log('Road.cs guid', roadGuid, '(file name ->', scripts[roadGuid] + ')');

// GameObjects and transforms, so a component can be traced to its object
const goOf = {};
const tfOf = {};
for (const d of docs) {
  const m = f(d.body, 'm_GameObject');
  const id = m && ref(m);
  if (id) goOf[d.id] = id[1];
  if (d.cls === 4 || d.cls === 224) tfOf[goOf[d.id]] = d.id;
}
const goName = (go) => {
  const d = byId[go];
  return d && d.cls === 1 ? f(d.body, 'm_Name') : null;
};
const localOf = (go) => {
  const d = byId[tfOf[go]];
  if (!d) return null;
  return {
    pos: vec(f(d.body, 'm_LocalPosition')),
    rot: vec(f(d.body, 'm_LocalRotation')),
    scale: vec(f(d.body, 'm_LocalScale')),
    father: (ref(f(d.body, 'm_Father')) || [])[1],
  };
};

const roadDoc = docs.find((d) => d.cls === 114 && f(d.body, 'm_Script') && roadGuid && f(d.body, 'm_Script').includes(roadGuid));
if (!roadDoc) { console.log('no Road component'); process.exit(0); }
const roadGo = goOf[roadDoc.id];
console.log('road object', roadGo, JSON.stringify(goName(roadGo)), JSON.stringify(localOf(roadGo)));

// the road mesh from the Road component's own MeshRoad reference
for (const field of ['MainMeshes', 'MeshRoad', 'MeshShoL', 'MeshShoR']) {
  const r = ref(f(roadDoc.body, field));
  if (!r) continue;
  const d = byId[r[1]];
  if (!d) continue;
  const go = goOf[r[1]] || r[1];
  if (d.cls === 33) {
    const mesh = ref(f(d.body, 'm_Mesh'));
    const meshDoc = mesh && byId[mesh[1]];
    const aabb = meshDoc ? f(meshDoc.body, 'localAABB') : null;
    const sub = meshDoc ? meshDoc.body.match(/localAABB:\s*\n\s*m_Center: (\{[^}]+\})\s*\n\s*m_Extent: (\{[^}]+\})/) : null;
    console.log(`  ${field}: MeshFilter ${r[1]} on ${JSON.stringify(goName(go))} ${JSON.stringify(localOf(go))}`);
    if (meshDoc) {
      const verts = f(meshDoc.body, 'm_VertexCount');
      console.log(`      mesh ${mesh[1]} name=${JSON.stringify(f(meshDoc.body, 'm_Name'))} verts=${verts} AABB ${sub ? sub[1] + ' ext ' + sub[2] : aabb}`);
    }
  } else {
    console.log(`  ${field}: ${r[1]} class ${d.cls} on ${JSON.stringify(goName(go))} ${JSON.stringify(localOf(go))}`);
  }
}

// the spline the reset uses
const splineRef = ref(f(roadDoc.body, 'spline'));
const splineDoc = splineRef && byId[splineRef[1]];
if (splineDoc) {
  const nodeIds = [...splineDoc.body.matchAll(/^\s*- \{fileID: (\d+)\}\s*$/gm)].map((m) => m[1]);
  console.log('spline', splineRef[1], 'nodes', nodeIds.length);
  const ys = [];
  for (const nid of nodeIds) {
    const d = byId[nid];
    if (!d) continue;
    const pos = vec(f(d.body, 'pos'));
    const go = goOf[nid];
    const l = go ? localOf(go) : null;
    ys.push({ id: nid, pos, world: l && l.pos, name: go ? goName(go) : null });
  }
  const usePos = ys.filter((y) => y.pos);
  const useWorld = ys.filter((y) => y.world);
  const range = (arr, pick) => {
    const v = arr.map(pick).filter((x) => x);
    return v.length ? `${Math.min(...v).toFixed(2)} .. ${Math.max(...v).toFixed(2)}` : '-';
  };
  console.log('  node pos y range      ', range(usePos, (y) => y.pos[1]));
  console.log('  node transform y range', range(useWorld, (y) => y.world[1]));
  for (const y of ys.slice(0, 4)) console.log('   node', y.id, JSON.stringify(y.name), 'pos', JSON.stringify(y.pos), 'transform', JSON.stringify(y.world));
}
