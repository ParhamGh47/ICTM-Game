const fs = require("fs");
const path = require("path");

const roots = ["Assets/Prefabs/DetailsTerrain", "Assets/Prefabs/Environments", "Assets/Prefabs/Signs/Lights"];
const files = [];
(function walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name);
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith(".prefab")) files.push(p);
  }
})(roots[0]);
for (const r of roots.slice(1)) if (fs.existsSync(r)) (function walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name);
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith(".prefab")) files.push(p);
  }
})(r);

for (const f of files.sort()) {
  const t = fs.readFileSync(f, "utf8").replace(/\r/g, "");
  const lodGroups = [...t.matchAll(/--- !u!\d+ &\d+\nLODGroup:[\s\S]*?(?=\n--- |\s*$)/g)];
  if (!lodGroups.length) continue;
  const parts = lodGroups.map((g) => [...g[0].matchAll(/screenRelativeHeight: ([0-9.eE+-]+)/g)].map((m) => format(Number(m[1]))).join("/"));
  console.log((f.split(path.sep).join("/") + "                                        ").slice(0, 62) + "  " + parts.join("  |  "));
}
function format(v) { return String(v); }
