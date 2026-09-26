// How far above (or below) the road the player truck is placed in a level, so a spawn that starts buried in
// the tarmac can be told from one that is simply a few centimetres off.
const fs = require("fs");

const scene = process.argv[2];
const truckGuid = fs
  .readFileSync("Assets/Prefabs/Utils/Truck (Player).prefab.meta", "utf8")
  .match(/guid: ([0-9a-f]{32})/)[1];

const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const blocks = txt.split(/^--- /m).slice(1);
const objs = {};
for (const b of blocks) {
  const m = b.match(/^!u!(\d+) &(\d+)/);
  if (m) objs[m[2]] = { cls: m[1], body: b };
}
const name = {};
for (const id in objs) {
  if (objs[id].cls === "1") {
    const n = objs[id].body.match(/m_Name: (.*)/);
    name[id] = n ? n[1].trim() : "?";
  }
}
const tr = {};
for (const id in objs) {
  if (objs[id].cls !== "4" && objs[id].cls !== "224") continue;
  const g = objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/);
  const fa = objs[id].body.match(/m_Father: \{fileID: (\d+)\}/);
  const p = objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
  tr[id] = { go: g && g[1], father: fa && fa[1], pos: p ? [+p[1], +p[2], +p[3]] : [0, 0, 0], name: name[g && g[1]] };
}
function world(tid) {
  let p = [0, 0, 0], chain = [], cur = tid, n = 0;
  while (cur && tr[cur] && n++ < 60) {
    const t = tr[cur];
    chain.push(t.name);
    p = [p[0] + t.pos[0], p[1] + t.pos[1], p[2] + t.pos[2]];
    cur = t.father;
  }
  return { pos: p, chain: chain.join("/") };
}

let spawn = null;
for (const id in objs) {
  if (objs[id].cls !== "1001") continue;
  const b = objs[id].body;
  if (!b.includes("guid: " + truckGuid)) continue;
  const mods = [
    ...b.matchAll(
      /- target: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g
    ),
  ];
  const d = {};
  for (const m of mods) d[m[2] + ":" + m[3]] = m[4];
  const px = d["*:"] || null;
  const re = new Map();
  for (const m of mods) (re.get(m[2]) || re.set(m[2], {}).get(m[2]))[m[3]] = m[4];
  for (const [target, props] of re) {
    if (props["m_LocalPosition.x"] === undefined) continue;
    const parent = (b.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
    const base = parent && parent !== "0" ? world(parent) : { pos: [0, 0, 0], chain: "(scene root)" };
    spawn = {
      local: [+props["m_LocalPosition.x"], +props["m_LocalPosition.y"], +props["m_LocalPosition.z"]],
      parent: base,
    };
    break;
  }
}

if (!spawn) {
  console.log(scene + ": no truck prefab instance found");
  process.exit(0);
}

const worldY = spawn.parent.pos[1] + spawn.local[1];
console.log(scene.split("/").slice(-2).join("/"));
console.log("   truck local " + spawn.local.join(", ") + "  under " + spawn.parent.chain);
console.log("   world y = " + spawn.parent.pos[1].toFixed(3) + " + " + spawn.local[1].toFixed(3) + " = " + worldY.toFixed(3));

// the road's own spline nodes
const roads = [];
for (const id in objs) {
  const b = objs[id].body;
  if (!/^  nodes:/m.test(b)) continue;
  const list = [...b.matchAll(/^  - \{fileID: (\d+)\}$/gm)].map((m) => m[1]);
  const pts = list
    .map((i) => {
      const p = objs[i] && objs[i].body.match(/^  pos: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/m);
      return p ? [+p[1], +p[2], +p[3]] : null;
    })
    .filter(Boolean);
  if (pts.length >= 3) roads.push({ id, pts });
}
for (const r of roads) {
  let best = null;
  for (const p of r.pts) {
    const d = Math.hypot(p[0] - spawn.parent.pos[0] - spawn.local[0], p[2] - spawn.parent.pos[2] - spawn.local[2]);
    if (!best || d < best.d) best = { d, p };
  }
  console.log(
    "   spline " + r.id + " (" + r.pts.length + " nodes): nearest node " + best.d.toFixed(1) + " m away at y " +
      best.p[1].toFixed(3) + "  ->  truck is " + (worldY - best.p[1]).toFixed(3) + " m above it"
  );
}
