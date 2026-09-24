/// Print the children of a named root object in a Unity scene.
const fs = require("fs");
const path = require("path");

const scene = process.argv[2];
const rootName = process.argv[3];

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

const text = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const objs = new Map();
for (const part of text.split(/^--- /m)) {
  const m = part.match(/^!u!(\d+) &(\d+)/);
  if (!m) continue;
  const id = m[2];
  const name = (part.match(/^  m_Name: (.*)$/m) || [])[1];
  const go = (part.match(/^  m_GameObject: \{fileID: (\d+)\}/m) || [])[1];
  const father = (part.match(/^  m_Father: \{fileID: (\d+)\}/m) || [])[1];
  objs.set(id, { cls: m[1], id, name, go, father, part });
}

// children of a transform
function childrenOf(tid) {
  const out = [];
  for (const o of objs.values()) if (o.cls === "4" && o.father === tid) out.push(o);
  // sort by declaration order in file (approximate)
  out.sort((a, b) => Number(a.id) - Number(b.id));
  return out;
}

// GameObject -> transform
function transformOf(goId) {
  for (const o of objs.values()) if (o.cls === "4" && o.go === goId) return o;
  return null;
}

// find the GameObject with the name, then its transform
let goObj = null;
for (const o of objs.values()) if (o.cls === "1" && o.name === rootName) { goObj = o; break; }
if (!goObj) { console.log("no GameObject named", rootName); process.exit(0); }
const rootT = transformOf(goObj.id);
console.log("root", rootName, "transform", rootT.id);

const kids = childrenOf(rootT.id);
console.log("children:", kids.length);
const counts = {};
let shown = 0;
for (const c of kids) {
  const go = objs.get(c.go);
  const nm = go ? go.name : "?";
  counts[nm] = (counts[nm] || 0) + 1;
  if (shown < 25) { console.log("   ", nm, "(transform", c.id + ")"); shown++; }
}
console.log("\n=== distinct child names ===");
console.log(Object.entries(counts).sort((a, b) => b[1] - a[1]).map(([n, c]) => c + "x " + n).join("\n"));
