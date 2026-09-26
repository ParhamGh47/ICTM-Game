// List colliders near a point in a scene, showing owner name and collider type.
const fs = require("fs");

const scene = process.argv[2];
const cx = +process.argv[3], cz = +process.argv[4], R = +(process.argv[5] || 12);

const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const blocks = txt.split(/^--- /m).slice(1);
const objs = {};
for (const b of blocks) {
  const m = b.match(/^!u!(\d+) &(-?\d+)/);
  if (m) objs[m[2]] = { cls: m[1], body: b };
}
const gname = {};
for (const id in objs) if (objs[id].cls === "1") {
  const n = objs[id].body.match(/m_Name: (.*)/);
  gname[id] = n ? n[1].trim() : "?";
}
// transform -> position & parent & gameobject
const tr = {};
for (const id in objs) {
  const c = objs[id].cls;
  if (c !== "4" && c !== "224") continue;
  const g = objs[id].body.match(/m_GameObject: \{fileID: (-?\d+)\}/);
  const fa = objs[id].body.match(/m_Father: \{fileID: (-?\d+)\}/);
  const p = objs[id].body.match(/m_LocalPosition: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/);
  const s = objs[id].body.match(/m_LocalScale: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/);
  tr[id] = { go: g && g[1], father: fa && fa[1], pos: p ? [+p[1], +p[2], +p[3]] : [0, 0, 0], scale: s ? [+s[1], +s[2], +s[3]] : [1, 1, 1], name: g && gname[g[1]] };
}
function world(tid) {
  let p = [0, 0, 0], chain = [], cur = tid, n = 0;
  while (cur && tr[cur] && n++ < 80) {
    const t = tr[cur];
    chain.push(t.name + (t.scale[0] !== 1 ? `[${t.scale.join(",")}]` : ""));
    p = [p[0] + t.pos[0], p[1] + t.pos[1], p[2] + t.pos[2]];
    cur = t.father;
  }
  return { pos: p, chain: chain.join("/") };
}
// collider classes: 65 BoxCollider, 64 MeshCollider, 135 SphereCollider, 136 CapsuleCollider, 154 TerrainCollider, 146? 
const names = { "65": "BoxCollider", "64": "MeshCollider", "135": "SphereCollider", "136": "CapsuleCollider", "154": "TerrainCollider" };
const out = [];
for (const id in objs) {
  const c = objs[id].cls;
  if (!names[c]) continue;
  const g = objs[id].body.match(/m_GameObject: \{fileID: (-?\d+)\}/);
  if (!g) continue;
  // find the transform of this gameobject
  let t = null;
  for (const tid in tr) if (tr[tid].go === g[1]) { t = tid; break; }
  if (!t) continue;
  const w = world(t);
  const d = Math.hypot(w.pos[0] - cx, w.pos[2] - cz);
  const enabled = /m_Enabled: 0/.test(objs[id].body);
  const trig = /m_IsTrigger: 1/.test(objs[id].body);
  if (d <= R) out.push({ d, type: names[c], enabled, trig, chain: w.chain, pos: w.pos });
}
out.sort((a, b) => a.d - b.d);
for (const o of out) {
  console.log(`${o.d.toFixed(2)}m  ${o.type}${o.trig ? "(trigger)" : ""}${o.enabled ? "(disabled)" : ""}  @ ${o.pos.map(v => v.toFixed(2)).join(", ")}  ${o.chain}`);
}
console.log(`--- ${out.length} colliders within ${R}m`);
