// What Core-3's player-truck prefab instance overrides, resolved to the object inside the truck prefab, so
// a whole-subtree move can be told apart from a single object's move.
const fs = require("fs");

const scene = process.argv[2];
const prefabPath = "Assets/Prefabs/Utils/Truck (Player).prefab";

const pguid = fs.readFileSync(prefabPath + ".meta", "utf8").match(/guid: ([0-9a-f]{32})/)[1];
const ptxt = fs.readFileSync(prefabPath, "utf8").replace(/\r/g, "");
const pblocks = ptxt.split(/^--- /m).slice(1);
const pname = {};
const powner = {};
for (const b of pblocks) {
  const m = b.match(/^!u!(\d+) &(\d+)/);
  if (!m) continue;
  if (m[1] === "1") {
    const n = b.match(/m_Name: (.*)/);
    pname[m[2]] = n ? n[1].trim() : "?";
  }
}
for (const b of pblocks) {
  const m = b.match(/^!u!(4|114|23|108|65|54) &(\d+)/);
  if (!m) continue;
  const g = b.match(/m_GameObject: \{fileID: (\d+)\}/);
  powner[m[2]] = g ? pname[g[1]] : "?";
}
const ppos = {};
for (const b of pblocks) {
  const m = b.match(/^!u!(4|224) &(\d+)/);
  if (!m) continue;
  const p = b.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
  ppos[m[2]] = p ? [+p[1], +p[2], +p[3]] : null;
}

const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const blocks = txt.split(/^--- /m).slice(1);

for (const b of blocks) {
  if (!/^!u!1001 &/.test(b)) continue;
  const src = (b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1];
  if (src !== pguid) continue;

  const mods = [
    ...b.matchAll(
      /- target: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g
    ),
  ];
  const grouped = {};
  for (const m of mods) {
    const t = m[1];
    (grouped[t] = grouped[t] || []).push(m[3] + " = " + m[4]);
  }
  console.log("prefab instance of " + prefabPath + "  modifications on " + Object.keys(grouped).length + " targets");
  for (const t of Object.keys(grouped)) {
    console.log(
      "  target " + t + "  (" + (powner[t] || "?") + ")  prefab local pos " +
        (ppos[t] ? ppos[t].join(", ") : "-")
    );
    for (const g of grouped[t]) console.log("      " + g);
  }
}
