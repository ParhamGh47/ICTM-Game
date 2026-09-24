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

for (let n = 1; n <= 4; n++) {
  const d = "Assets/Scenes/Levels Scenes/" + n;
  for (const f of fs.readdirSync(d)) {
    if (!/^Core-.*\.unity$/.test(f)) continue;
    const t = fs.readFileSync(path.join(d, f), "utf8");
    const guids = new Set([...t.matchAll(/guid: ([0-9a-f]{32})/g)].map((m) => m[1]));
    const kinds = { model: [], other: [] };
    for (const g of guids) {
      const p = map[g] || "(unknown " + g + ")";
      if (/\.(fbx|obj|FBX|OBJ|blend)$/.test(p)) kinds.model.push(p);
      else kinds.other.push(p);
    }
    console.log("\n=== " + f);
    console.log("  models: " + kinds.model.length);
    for (const p of kinds.model) console.log("    " + p);
  }
}
