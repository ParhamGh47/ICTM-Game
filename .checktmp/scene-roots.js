const fs = require("fs");
const path = require("path");

function rootsOf(scene) {
  const text = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
  const objs = new Map();
  for (const part of text.split(/^--- /m)) {
    const m = part.match(/^!u!(\d+) &(\d+)/);
    if (!m) continue;
    const name = (part.match(/^  m_Name: (.*)$/m) || [])[1];
    const go = (part.match(/^  m_GameObject: \{fileID: (\d+)\}/m) || [])[1];
    const father = (part.match(/^  m_Father: \{fileID: (\d+)\}/m) || [])[1];
    objs.set(m[2], { cls: m[1], id: m[2], name, go, father, part });
  }
  const out = [];
  for (const o of objs.values()) {
    if (o.cls !== "4") continue;
    if (o.father && Number(o.father) !== 0) continue;
    const go = objs.get(o.go);
    const kids = [...objs.values()].filter((x) => x.cls === "4" && x.father === o.id).length;
    out.push({ name: go ? go.name : "(unnamed)", kids });
  }
  return out;
}

for (let n = 1; n <= 6; n++) {
  const d = "Assets/Scenes/Levels Scenes/" + n;
  if (!fs.existsSync(d)) continue;
  for (const f of fs.readdirSync(d)) {
    if (!/^Core-.*\.unity$/.test(f)) continue;
    const p = path.join(d, f);
    const r = rootsOf(p);
    console.log("\n=== " + f + " roots: " + r.length);
    console.log("   " + r.map((x) => x.name + "(" + x.kids + ")").join(", "));
  }
}
