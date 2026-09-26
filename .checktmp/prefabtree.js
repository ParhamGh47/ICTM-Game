// Dump a Unity prefab/scene YAML's GameObject tree with components, collider sizes and script guids.
const fs = require("fs");
const file = process.argv[2];
const txt = fs.readFileSync(file, "utf8").replace(/\r/g, "");
const blocks = txt.split(/^--- /m).slice(1);
const objs = {};
for (const b of blocks) {
  const m = b.match(/^!u!(\d+) &(-?\d+)/);
  if (m) objs[m[2]] = { cls: m[1], body: b };
}
const scriptGuid = {};
// map guid -> script name from .meta? skip; just print guid
const compName = {
  "1": "GameObject", "4": "Transform", "224": "RectTransform", "114": "MonoBehaviour",
  "65": "BoxCollider", "64": "MeshCollider", "135": "SphereCollider", "136": "CapsuleCollider",
  "54": "Rigidbody", "23": "MeshRenderer", "33": "MeshFilter", "82": "AudioSource",
  "146": "WheelCollider", "108": "Light", "198": "ParticleSystem", "199": "ParticleSystemRenderer",
  "20": "Camera", "81": "AudioListener", "95": "Animator",
};
const gos = {};
for (const id in objs) if (objs[id].cls === "1") {
  const n = objs[id].body.match(/m_Name: (.*)/);
  const comps = [...objs[id].body.matchAll(/- component: \{fileID: (-?\d+)\}/g)].map(m => m[1]);
  const genc = (objs[id].body.match(/m_Component:/) && [...objs[id].body.matchAll(/- component: \{fileID: (-?\d+)\}/g)].map(m => m[1])) || [];
  gos[id] = { name: n ? n[1].trim() : "?", comps: comps, body: objs[id].body };
}
// Build parent map from transforms
const tr = {};
for (const id in objs) {
  const c = objs[id].cls;
  if (c !== "4" && c !== "224") continue;
  const g = objs[id].body.match(/m_GameObject: \{fileID: (-?\d+)\}/);
  const fa = objs[id].body.match(/m_Father: \{fileID: (-?\d+)\}/);
  const kids = [...objs[id].body.matchAll(/- \{fileID: (-?\d+)\}/g)].map(m => m[1]);
  tr[id] = { go: g && g[1], father: fa ? fa[1] : null, kids, id };
}
const trOfGo = {};
for (const id in tr) trOfGo[tr[id].go] = id;

function comp(goId, cls) {
  const g = gos[goId];
  if (!g) return null;
  for (const cid of g.comps) if (objs[cid] && objs[cid].cls === cls) return objs[cid].body;
  return null;
}
function line(goId, depth) {
  const g = gos[goId];
  if (!g) return;
  const t = trOfGo[goId];
  const parts = [];
  for (const cid of g.comps) {
    if (objs[cid] && compName[objs[cid].cls]) {
      let s = compName[objs[cid].cls];
      if (objs[cid].cls === "65") {
        const sz = objs[cid].body.match(/m_Size: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/);
        const ce = objs[cid].body.match(/m_Center: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/);
        s += ` sz(${sz ? sz.slice(1).join(",") : "?"}) ctr(${ce ? ce.slice(1).join(",") : "?"})`;
      }
      if (objs[cid].cls === "114") {
        const sg = objs[cid].body.match(/m_Script: \{fileID: 11500000, guid: ([0-9a-f]+)/);
        s += ` script:${sg ? sg[1].slice(0, 8) : "?"}`;
      }
      parts.push(s);
    }
  }
  const pos = t && objs[t] && (objs[t].body.match(/m_LocalPosition: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/));
  const sc = t && objs[t] && (objs[t].body.match(/m_LocalScale: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/));
  console.log("  ".repeat(depth) + g.name + (pos ? `  pos(${pos.slice(1).join(",")})` : "") + (sc && sc[1] !== "1" ? `  scale(${sc.slice(1).join(",")})` : "") + "  [" + parts.join(", ") + "]");
  if (t) for (const k of tr[t].kids) { const kgo = tr[k] && tr[k].go; if (kgo && gos[kgo]) line(kgo, depth + 1); }
}
for (const id in gos) {
  const t = trOfGo[id];
  const fa = t && tr[t].father;
  if (!fa || fa === "0") line(id, 0);
}
