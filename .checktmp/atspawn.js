// Every collider at the player's spawn point in a level, scene objects included - a wall or a prop sharing the
// truck's place is what makes it jitter and creep on the spot, because the physics spends every step pushing
// the two apart.
const fs = require("fs");

const scene = process.argv[2];
const radius = +(process.argv[3] || 12);

const guidToPath = {};
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = dir + "/" + e.name;
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith(".meta")) {
      const g = fs.readFileSync(p, "utf8").match(/guid: ([0-9a-f]{32})/);
      if (g) guidToPath[g[1]] = p.replace(/\.meta$/, "");
    }
  }
})("Assets");

// guid -> the transform id of that prefab's root object
const rootTransform = {};
function rootOf(guid) {
  if (guid in rootTransform) return rootTransform[guid];
  const path = guidToPath[guid];
  let result = null;
  if (path && path.endsWith(".prefab")) {
    const txt = fs.readFileSync(path, "utf8").replace(/\r/g, "");
    for (const b of txt.split(/^--- /m).slice(1)) {
      const m = b.match(/^!u!(\d+) &(\d+)/);
      if (!m || (m[1] !== "4" && m[1] !== "224")) continue;
      const fa = b.match(/m_Father: \{fileID: (\d+)\}/);
      if (!fa || fa[1] === "0") {
        result = m[2];
        break;
      }
    }
  }
  rootTransform[guid] = result;
  return result;
}

const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const objs = {};
for (const b of txt.split(/^--- /m).slice(1)) {
  const m = b.match(/^!u!(\d+) &(\d+)/);
  if (m) objs[m[2]] = { cls: m[1], body: b };
}
const name = {};
const tag = {};
for (const id in objs) {
  if (objs[id].cls !== "1") continue;
  const n = objs[id].body.match(/m_Name: (.*)/);
  name[id] = n ? n[1].trim() : "?";
  const t = objs[id].body.match(/m_TagString: (.*)/);
  tag[id] = t ? t[1].trim() : "?";
}
const tr = {};
for (const id in objs) {
  if (objs[id].cls !== "4" && objs[id].cls !== "224") continue;
  const g = objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/);
  const fa = objs[id].body.match(/m_Father: \{fileID: (\d+)\}/);
  const p = objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
  const inst = objs[id].body.match(/m_PrefabInstance: \{fileID: (\d+)\}/);
  tr[id] = {
    go: g && g[1],
    father: fa ? fa[1] : null,
    pos: p ? [+p[1], +p[2], +p[3]] : null,
    instance: inst ? inst[1] : null,
    stripped: /stripped/.test(objs[id].body.split("\n")[0]),
  };
}

// An instance root's own position lives in the PrefabInstance's modifications for the prefab's root transform.
const instancePos = {};
for (const id in objs) {
  if (objs[id].cls !== "1001") continue;
  const b = objs[id].body;
  const guid = (b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1];
  const root = guid ? rootOf(guid) : null;
  let local = [0, 0, 0];
  if (root) {
    const props = {};
    for (const m of b.matchAll(
      new RegExp(
        "- target: \\{fileID: " + root + ", guid: [0-9a-f]+, type: 3\\}\\s*\\n\\s*propertyPath: ([^\\n]+)\\s*\\n\\s*value: ([^\\n]*)",
        "g"
      )
    ))
      props[m[1]] = m[2];
    local = [+(props["m_LocalPosition.x"] || 0), +(props["m_LocalPosition.y"] || 0), +(props["m_LocalPosition.z"] || 0)];
  }
  instancePos[id] = local;
}

function world(tid) {
  let p = [0, 0, 0], cur = tid, n = 0, owner = null;
  const chain = [];
  while (cur && tr[cur] && n++ < 60) {
    if (tr[cur].instance !== null && tr[cur].instance !== undefined && tr[cur].stripped) {
      // a child of a prefab instance: add the instance root's position and stop
      if (!owner) {
        const base = instancePos[tr[cur].instance] || [0, 0, 0];
        p = [p[0] + base[0], p[1] + base[1], p[2] + base[2]];
        chain.push((name[tr[cur].go] || "?") + "(prefab " + tr[cur].instance + ")");
      }
      break;
    }
    if (tr[cur].pos) p = [p[0] + tr[cur].pos[0], p[1] + tr[cur].pos[1], p[2] + tr[cur].pos[2]];
    chain.push(name[tr[cur].go] || "?");
    if (tr[cur].instance !== null && tr[cur].instance !== undefined) owner = tr[cur].instance;
    cur = tr[cur].father;
  }
  return { pos: p, chain: chain.reverse().join("/") };
}

const truckGuid = fs
  .readFileSync("Assets/Prefabs/Utils/Truck (Player).prefab.meta", "utf8")
  .match(/guid: ([0-9a-f]{32})/)[1];

// the truck's spawn, from the instance of the truck prefab
let spawn = null;
let spawnChain = "";
for (const id in objs) {
  if (objs[id].cls !== "1001") continue;
  if (!objs[id].body.includes("guid: " + truckGuid)) continue;
  const b = objs[id].body;
  const root = rootOf(truckGuid);
  const props = {};
  for (const m of b.matchAll(
    new RegExp(
      "- target: \\{fileID: " + root + ", guid: [0-9a-f]+, type: 3\\}\\s*\\n\\s*propertyPath: ([^\\n]+)\\s*\\n\\s*value: ([^\\n]*)",
      "g"
    )
  ))
    props[m[1]] = m[2];
  const local = [+(props["m_LocalPosition.x"] || 0), +(props["m_LocalPosition.y"] || 0), +(props["m_LocalPosition.z"] || 0)];
  const parent = (b.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
  const base = parent && parent !== "0" ? world(parent) : { pos: [0, 0, 0], chain: "(scene root)" };
  spawn = [base.pos[0] + local[0], base.pos[1] + local[1], base.pos[2] + local[2]];
  spawnChain = base.chain;
}

console.log(scene.split("/").slice(-2).join("/"));
console.log("   truck spawn " + spawn.map((v) => v.toFixed(2)).join(", ") + "  under " + spawnChain);

// every collider object near it
const rows = [];
for (const id in objs) {
  if (!/^(64|65|135|136|61|143|54|153|322)$/.test(objs[id].cls)) continue; // collider classes
  const g = objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/);
  if (!g) continue;
  const t = Object.keys(tr).find((k) => tr[k].go === g[1]);
  if (!t) continue;
  const w = world(t);
  const d = Math.hypot(w.pos[0] - spawn[0], w.pos[1] - spawn[1], w.pos[2] - spawn[2]);
  if (d > radius) continue;
  const size = objs[id].body.match(/m_Size: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
  const center = objs[id].body.match(/m_Center: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
  rows.push({
    d,
    cls: objs[id].cls,
    name: name[g[1]] || "?",
    tag: tag[g[1]] || "?",
    chain: w.chain,
    pos: w.pos,
    size: size ? [+size[1], +size[2], +size[3]] : null,
    center: center ? [+center[1], +center[2], +center[3]] : null,
  });
}
rows.sort((a, b) => a.d - b.d);
console.log("   colliders within " + radius + " m: " + rows.length);
for (const r of rows.slice(0, 25)) {
  console.log(
    "      " + r.d.toFixed(2) + " m  " + r.name + " [" + r.tag + "]  world " + r.pos.map((v) => v.toFixed(2)).join(",") +
      (r.size ? "  size " + r.size.join(",") + " center " + r.center.join(",") : "")
  );
  console.log("            " + r.chain);
}
