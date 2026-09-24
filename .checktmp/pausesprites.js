// List the pause panel's children with their Image sprite guid + colour, and resolve the sprite asset paths.
const fs = require("fs");
const path = require("path");

const prefab = process.argv[2] || "Assets/Prefabs/Utils/CanvasUI.prefab";
const t = fs.readFileSync(prefab, "utf8").replace(/\r/g, "");
const parts = t.split(/^--- /m);
const byId = new Map();
for (const p of parts) {
  const m = p.match(/^!u!(\d+) &(\d+)/);
  if (m) byId.set(m[2], { cls: m[1], p });
}
const val = (p, k) => (p.match(new RegExp("^  " + k + ": ?(.*)$", "m")) || [])[1];

// guid -> asset path index
const index = new Map();
function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) walk(full);
    else if (e.name.endsWith(".meta")) {
      const txt = fs.readFileSync(full, "utf8");
      const g = (txt.match(/^guid: ([0-9a-f]+)/m) || [])[1];
      if (g) index.set(g, full.replace(/\.meta$/, ""));
    }
  }
}
walk("Assets");

const pausePanel = parts.find((p) => p.startsWith("!u!224 &6890820110232687940"));
const children = parts.filter((p) => p.startsWith("!u!") && p.includes("m_Father: {fileID: 6890820110232687940}"));
for (const c of children) {
  const goId = (c.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const go = byId.get(goId);
  const name = go && val(go.p, "m_Name");
  const comps = (go.p.match(/- component: \{fileID: (\d+)\}/g) || []).map((x) => x.match(/\d+/)[0]);
  for (const id of comps) {
    const cc = byId.get(id);
    if (!cc) continue;
    const sg = (cc.p.match(/m_Script: .*guid: ([0-9a-f]+)/) || [])[1];
    if (sg !== "fe87c0e1cc204ed48ad3b37840f39efc") continue;
    const sp = (cc.p.match(/m_Sprite: \{fileID: \d+, guid: ([0-9a-f]+)/) || [])[1];
    console.log(
      (name || "?").padEnd(18),
      "color", val(cc.p, "m_Color"), "type", val(cc.p, "m_Type"),
      "sprite", sp ? (index.get(sp) || sp) : "(none)"
    );
  }
}
