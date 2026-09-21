// Dumps the level buttons of the Levels scene: their hierarchy, components, labels and what their Button
// onClick actually calls, so a lock can be wired to the right objects.
const fs = require("fs");
const src = fs.readFileSync("Assets/Scenes/Levels.unity", "utf8");
const docs = src.split(/^--- /m).slice(1).map((d) => "--- " + d);

const byId = {};
const goName = {};
for (const d of docs) {
  const m = d.match(/^--- !u!(\d+) &(\d+)/);
  if (m) byId[m[2]] = { cls: m[1], text: d };
  if (m && m[1] === "1") {
    const n = d.match(/^\s*m_Name: (.*)$/m);
    goName[m[2]] = n ? n[1].trim() : "";
  }
}

// GameObject children and components
const children = {};
for (const [id, doc] of Object.entries(byId)) {
  if (doc.cls !== "4") continue; // Transform
  const go = (doc.text.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const father = (doc.text.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
  if (!father) continue;
  const parentGo = (byId[father] && (byId[father].text.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1]) || "?";
  (children[parentGo] = children[parentGo] || []).push(go);
}

const components = {};
for (const [id, doc] of Object.entries(byId)) {
  if (doc.cls === "1" || doc.cls === "4") continue;
  const go = (doc.text.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  if (!go) continue;
  (components[go] = components[go] || []).push({ id, cls: doc.cls, text: doc.text });
}

const label = (doc) => {
  const m = doc.match(/m_Text: (.*)$/m);
  const t = doc.match(/^\s*m_Name: (.*)$/m);
  const s = doc.match(/m_Sprite: \{fileID: (\d+)/);
  return "name=" + (t ? t[1].trim() : "?") + (m ? ' text="' + m[1].trim() + '"' : "") + (s ? " sprite=" + s[1] : "");
};

const walk = (go, depth, out) => {
  if (!go) return;
  out.push("  ".repeat(depth) + goName[go] + "  [" + (components[go] || []).map((c) => c.cls).join(",") + "]");
  for (const c of components[go] || []) {
    if (c.cls === "222" || c.cls === "223") out.push("  ".repeat(depth + 1) + "gfx: " + label(c.text));
    if (c.cls === "114") {
      const target = (c.text.match(/m_Target: \{fileID: (\d+)\}/) || [])[1];
      const method = (c.text.match(/m_MethodName: (.*)$/m) || [])[1];
      const mode = (c.text.match(/m_Mode: (\d+)/) || [])[1];
      if (method) out.push("  ".repeat(depth + 1) + "calls " + method.trim() + " on go " + (goName[target] || target) + " (mode " + mode + ")");
      const interactable = (c.text.match(/m_Interactable: (\d)/) || [])[1];
      const colors = (c.text.match(/m_Colors: \{m_NormalColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}/) || []);
      if (interactable !== undefined) out.push("  ".repeat(depth + 1) + "button interactable=" + interactable + (colors.length ? " normal=(" + colors.slice(1, 5).join(",") + ")" : ""));
    }
  }
  for (const kid of children[go] || []) walk(kid, depth + 1, out);
};

const root = Object.keys(goName).find((id) => goName[id] === "level1");
const out = [];
// print each level button and anything named like one
walk(root, 0, out);
console.log("=== level1 subtree ===");
console.log(out.join("\n"));

const canvas = Object.keys(goName).filter((id) => /^level\d$/.test(goName[id]));
console.log("\n=== level buttons found: " + canvas.map((id) => goName[id]).join(", "));
console.log("\n=== siblings of level1 (the canvas) ===");
for (const id of canvas) {
  const t = byId[Object.entries(byId).find(([, d]) => d.cls === "4" && d.text.includes("fileID: " + id)) ? 0 : id];
}
// list all canvas children
const all = Object.keys(goName).filter((id) => goName[id]);
for (const id of all) {
  if (!/^level\d$/.test(goName[id])) continue;
  const o = [];
  walk(id, 0, o);
  console.log("\n--- " + goName[id] + " ---");
  console.log(o.slice(1).join("\n"));
}
