const fs = require("fs");
const path = require("path");

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

// a binary material that references a texture: find its guids by reading its YAML if text
const mat = "Assets/Prefabs/Environments/Trees/Level-1/Materials/TreeColor.mat";
const raw = fs.readFileSync(mat);
console.log("material is text?", raw.slice(0, 40).toString("latin1").includes("%YAML"));
const text = raw.toString("latin1").replace(/\r/g, "");
const refs = [...text.matchAll(/guid: ([0-9a-f]{32})/g)].map((m) => m[1]);
console.log("material refs:", refs.map((g) => g + " -> " + (map[g] || "?")).join("\n  "));

if (refs.length) {
  const g = refs[0];
  const b = raw;
  const asIs = b.indexOf(Buffer.from(g, "hex"));
  const rev = b.indexOf(Buffer.from(g, "hex").reverse());
  console.log("first ref stored in the file as-is?", asIs, " reversed?", rev);
}

// now check a binary texture import? and the terrains
console.log("\n=== search terrains for the mat guid and for any 16-byte guid ===");
for (const f of ["Assets/Terrains/New Terrain 5.asset", "Assets/New Terrain 2.asset", "Assets/New Terrain 1.asset"]) {
  const b = fs.readFileSync(f);
  console.log("\n" + f + " (" + b.length + ")");
  for (const g of refs) {
    console.log("   mat guid " + g + " as-is@" + b.indexOf(Buffer.from(g, "hex")));
  }
  // count "guid-looking" 16 byte blocks: eh, skip
}
