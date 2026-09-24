// Works out what deleting a scene's pathless passing cars would involve, without writing anything.
const fs = require("fs");

const file = process.argv[2] || "Assets/Scenes/Levels Scenes/1/Core-1.unity";
const text = fs.readFileSync(file, "utf8").replace(/\r/g, "");

// blocks: the header line plus its body
const blocks = [];
const re = /^--- (!u!\d+ &(\d+)[^\n]*)\n/gm;
let m;
let last = null;
while ((m = re.exec(text)) !== null) {
  if (last) last.end = m.index;
  last = { header: m[1], id: m[2], cls: m[1].match(/!u!(\d+)/)[1], start: m.index, end: text.length, body: "" };
  blocks.push(last);
}
for (const b of blocks) b.body = text.slice(b.start, b.end);

console.log(file);
console.log("total blocks: " + blocks.length);

const dead = [];
for (const b of blocks) {
  if (b.cls !== "1001") continue;
  if (!/propertyPath: waypointsRoot/.test(b.body)) continue;
  const at = b.body.indexOf("propertyPath: waypointsRoot\n");
  const after = b.body.slice(at);
  const obj = (after.match(/^      objectReference: \{fileID: (\d+)\}/m) || [])[1];
  if (obj === "0") dead.push(b);
}

console.log("pathless car instances: " + dead.length);

// what else points at these instances (the stripped GameObject / Transform of each instance)
const deadIds = new Set(dead.map((b) => b.id));
const stripped = [];
for (const b of blocks) {
  const owner = (b.body.match(/^  m_PrefabInstance: \{fileID: (\d+)\}/m) || [])[1];
  if (owner && deadIds.has(owner)) stripped.push(b);
}
console.log("stripped objects belonging to them: " + stripped.length + " (" +
  [...new Set(stripped.map((b) => b.cls))].join(", ") + ")");

// any OTHER block referencing a doomed id?
const doomed = new Set([...deadIds, ...stripped.map((b) => b.id)]);
const outsiders = [];
for (const b of blocks) {
  if (doomed.has(b.id)) continue;
  for (const d of doomed) {
    if (new RegExp("fileID: " + d + "(,|\\})").test(b.body)) {
      outsiders.push({ id: b.id, cls: b.cls, refs: d, name: (b.body.match(/^  m_Name: (.*)$/m) || [])[1] });
      break;
    }
  }
}
console.log("outside blocks referencing them: " + outsiders.length);
for (const o of outsiders.slice(0, 10)) console.log(`  block ${o.id} (class ${o.cls}${o.name ? ", " + o.name : ""}) -> ${o.refs}`);

console.log("\nremoving them would take " + (dead.length + stripped.length) + " blocks out of " + blocks.length +
  " (" + ((100 * (dead.length + stripped.length)) / blocks.length).toFixed(1) + "% of the file)");

// a parent reference check: these stripped transforms' m_Father
for (const b of stripped.slice(0, 3)) {
  console.log("  example stripped block " + b.header + "  father " + ((b.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1] || "-"));
}
