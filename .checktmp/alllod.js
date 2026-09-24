const fs = require("fs");
const path = require("path");

const files = [];
(function walk(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name);
    if (e.isDirectory()) {
      if ([".git", "Library", "Temp"].includes(e.name)) continue;
      walk(p);
    } else if (e.name.endsWith(".prefab")) files.push(p);
  }
})("Assets");

const bad = [];
for (const f of files) {
  const t = fs.readFileSync(f, "utf8").replace(/\r/g, "");
  const groups = [...t.matchAll(/--- !u!\d+ &\d+\nLODGroup:([\s\S]*?)(?=\n--- |$)/g)];
  for (const g of groups) {
    const th = [...g[1].matchAll(/screenRelativeHeight: ([0-9.eE+-]+)/g)].map((m) => Number(m[1]));
    if (!th.length) continue;
    if (th[th.length - 1] > 0.0001) bad.push({ f, th });
  }
}

console.log("prefabs with a non-zero last LOD threshold: " + bad.length + " of " + files.length);
const byDir = {};
for (const b of bad) {
  const d = path.dirname(b.f).split(path.sep).join("/");
  (byDir[d] = byDir[d] || []).push(path.basename(b.f) + " [" + b.th.join(",") + "]");
}
for (const [d, list] of Object.entries(byDir).sort()) {
  console.log("\n" + d + "  (" + list.length + ")");
  for (const l of list.slice(0, 40)) console.log("   " + l);
  if (list.length > 40) console.log("   ... +" + (list.length - 40) + " more");
}
