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

const groups = {
  "Environments/Trees": /Environments\/Trees\/.*\.prefab$/,
  "DetailsTerrain Trees": /DetailsTerrain\/Level-\d\/Trees\/.*\.prefab$/,
  "Objects/Obstacles/Trees": /Obstacles\/Trees\/.*/,
};
const treeGuids = [];
for (const [name, re] of Object.entries(groups)) {
  const entries = Object.entries(map).filter(([g, p]) => re.test(p));
  console.log("=== " + name + " (" + entries.length + ") ===");
  for (const [g, p] of entries) console.log("  " + g + "  " + p);
  if (name !== "Objects/Obstacles/Trees") treeGuids.push(...entries.map(([g]) => g));
}

const scenes = [];
for (let n = 1; n <= 6; n++) {
  const d = "Assets/Scenes/Levels Scenes/" + n;
  if (!fs.existsSync(d)) continue;
  for (const f of fs.readdirSync(d)) if (/^Core-.*\.unity$/.test(f)) scenes.push(path.join(d, f));
}

console.log("\n=== tree prefab references per core scene ===");
for (const f of scenes) {
  const t = fs.readFileSync(f, "utf8");
  let total = 0;
  const hits = {};
  for (const g of treeGuids) {
    const m = t.match(new RegExp(g, "g"));
    if (m) { hits[map[g]] = m.length; total += m.length; }
  }
  console.log(path.basename(f), "refs=" + total, total ? JSON.stringify(hits, null, 0) : "");
}
