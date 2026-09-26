// What else sits near the player's spawn point in a level. A prop sharing the truck's place is what makes a
// truck jitter and creep on the spot, because the physics spends every physics step pushing the two apart.
const fs = require("fs");

const scene = process.argv[2];
const radius = +(process.argv[3] || 25);

const guidToPath = {};
for (const p of walk("Assets")) {
  if (!p.endsWith(".prefab.meta") && !p.endsWith(".unity.meta")) continue;
  const g = fs.readFileSync(p, "utf8").match(/guid: ([0-9a-f]{32})/);
  if (g) guidToPath[g[1]] = p.replace(/\.meta$/, "");
}

function* walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = dir + "/" + e.name;
    if (e.isDirectory()) yield* walk(p);
    else yield p;
  }
}

// guid -> the root GameObject's transform inside that prefab
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
  tr[id] = { go: g && g[1], father: fa ? fa[1] : null, pos: p ? [+p[1], +p[2], +p[3]] : [0, 0, 0] };
}
function world(tid) {
  let p = [0, 0, 0], cur = tid, n = 0;
  while (cur && tr[cur] && n++ < 60) {
    p = [p[0] + tr[cur].pos[0], p[1] + tr[cur].pos[1], p[2] + tr[cur].pos[2]];
    cur = tr[cur].father;
  }
  return p;
}

const truckGuid = fs
  .readFileSync("Assets/Prefabs/Utils/Truck (Player).prefab.meta", "utf8")
  .match(/guid: ([0-9a-f]{32})/)[1];

const instances = [];
for (const id in objs) {
  if (objs[id].cls !== "1001") continue;
  const b = objs[id].body;
  const guid = (b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1];
  if (!guid) continue;

  const root = rootOf(guid);
  let local = [0, 0, 0];
  if (root) {
    const props = {};
    const re = new RegExp(
      "- target: \\{fileID: " + root + ", guid: [0-9a-f]+, type: 3\\}\\s*\\n\\s*propertyPath: ([^\\n]+)\\s*\\n\\s*value: ([^\\n]*)",
      "g"
    );
    for (const m of b.matchAll(re)) props[m[1]] = m[2];
    local = [+(props["m_LocalPosition.x"] || 0), +(props["m_LocalPosition.y"] || 0), +(props["m_LocalPosition.z"] || 0)];
  }

  const parent = (b.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
  const base = parent && parent !== "0" ? world(parent) : [0, 0, 0];
  const pos = [base[0] + local[0], base[1] + local[1], base[2] + local[2]];

  instances.push({ id, guid, pos, path: guidToPath[guid] || guid, truck: guid === truckGuid });
}

const truck = instances.find((i) => i.truck);
if (!truck) {
  console.log(scene + ": truck spawn not found");
  process.exit(0);
}
console.log(
  scene.split("/").slice(-2).join("/") + "  truck spawn at " + truck.pos.map((v) => v.toFixed(2)).join(", ")
);

const near = instances
  .filter((i) => !i.truck)
  .map((i) => ({ d: Math.hypot(i.pos[0] - truck.pos[0], i.pos[1] - truck.pos[1], i.pos[2] - truck.pos[2]), i }))
  .sort((a, b) => a.d - b.d);

console.log("   prefab instances within " + radius + " m:");
for (const x of near.filter((x) => x.d < radius).slice(0, 25)) {
  console.log("      " + x.d.toFixed(1) + " m  " + x.i.path.replace("Assets/Prefabs/", "") + "  at " + x.i.pos.map((v) => v.toFixed(1)).join(", "));
}
console.log("   total within " + radius + " m: " + near.filter((x) => x.d < radius).length + " of " + instances.length + " instances");
