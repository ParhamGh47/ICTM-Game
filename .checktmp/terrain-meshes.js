const fs = require("fs");
const path = require("path");

// guid -> asset path map
const map = {};
(function walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name);
    if (e.isDirectory()) {
      if ([".git", "Library", "Temp"].includes(e.name)) continue;
      walk(p);
    } else if (e.name.endsWith(".meta")) {
      const m = fs.readFileSync(p, "utf8").match(/guid: ([0-9a-f]{32})/);
      if (m) map[m[1]] = p.replace(/\.meta$/, "").split(path.sep).join("/");
    }
  }
})("Assets");

const treePrefabs = [
  "Assets/Prefabs/Environments/Trees/Level-1/Tree-1.prefab",
  "Assets/Prefabs/Environments/Trees/Level-1/Tree-1 1.prefab",
  "Assets/Prefabs/Environments/Trees/Level-1/Tree-2.prefab",
  "Assets/Prefabs/Environments/Trees/Level-1/Tree-2 1.prefab",
  "Assets/Prefabs/Environments/Trees/Level-1/Tree-3.prefab",
  "Assets/Prefabs/Environments/Trees/Level-1/Tree-3 1.prefab",
  "Assets/Prefabs/Environments/Trees/Level-1/Bush.prefab",
  "Assets/Prefabs/Environments/Trees/Level-1/Bush 1.prefab",
  "Assets/Prefabs/Environments/Trees/Grass.prefab",
];

console.log("=== meshes/materials referenced by level-1 environment tree prefabs ===");
const wanted = new Set();
for (const f of treePrefabs) {
  if (!fs.existsSync(f)) { console.log("  missing", f); continue; }
  const t = fs.readFileSync(f, "utf8").replace(/\r/g, "");
  const guids = new Set([...t.matchAll(/guid: ([0-9a-f]{32})/g)].map((m) => m[1]));
  console.log("  " + path.basename(f) + ":");
  for (const g of guids) {
    const p = map[g] || "(?)";
    console.log("     " + g + "  " + p);
    if (/\.(fbx|obj|FBX|OBJ)$/.test(p) || /\.mat$/.test(p)) wanted.add(g);
  }
}

const terrains = ["Assets/Terrains/New Terrain 5.asset", "Assets/New Terrain 2.asset", "Assets/New Terrain 1.asset"];
console.log("\n=== searching terrains for those mesh/material guids ===");
for (const f of terrains) {
  const b = fs.readFileSync(f);
  const found = [];
  for (const g of wanted) {
    const asIs = b.indexOf(Buffer.from(g, "hex"));
    const rev = b.indexOf(Buffer.from(g, "hex").reverse());
    if (asIs !== -1 || rev !== -1) found.push((map[g] || g) + (asIs !== -1 ? " [as-is@" + asIs + "]" : " [reversed@" + rev + "]"));
  }
  console.log("  " + f + " (" + b.length + "): " + (found.length ? found.join("\n      ") : "nothing"));
}
