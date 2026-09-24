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

const treeGuids = Object.entries(map).filter(([g, p]) => /\/Trees\/.*\.prefab$/.test(p) || /Environments\/Trees\/.*\.prefab$/.test(p));
console.log("tree prefabs:", treeGuids.length);

const files = [];
(function w(d) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, e.name);
    if (e.isDirectory()) {
      if ([".git", "Library", "Temp"].includes(e.name)) continue;
      w(p);
    } else if (/\.(unity|prefab)$/.test(e.name)) files.push(p);
  }
})("Assets");

const contents = files.map((f) => ({ f, t: fs.readFileSync(f, "utf8") }));
for (const [g, p] of treeGuids) {
  const users = contents.filter((c) => c.t.includes(g)).map((c) => c.f);
  console.log(p + "  ->  " + (users.length ? users.join(" | ") : "UNUSED"));
}
