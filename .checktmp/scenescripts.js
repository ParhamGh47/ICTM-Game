// List MonoBehaviour script guids used in a scene, resolved to script file names via .meta files.
const fs = require("fs");
const path = require("path");

const scene = process.argv[2];
const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const counts = {};
for (const m of txt.matchAll(/m_Script: \{fileID: 11500000, guid: ([0-9a-f]+)/g)) {
  counts[m[1]] = (counts[m[1]] || 0) + 1;
}
// Build guid -> script path map by walking Assets for .cs.meta
const map = {};
function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== "Library" && e.name !== ".git") walk(p); }
    else if (e.name.endsWith(".cs.meta")) {
      const g = fs.readFileSync(p, "utf8").match(/guid: ([0-9a-f]+)/);
      if (g) map[g[1]] = p.replace(/\.meta$/, "");
    }
  }
}
walk("Assets");
const rows = Object.entries(counts).sort((a, b) => b[1] - a[1]);
for (const [g, c] of rows) console.log(`${String(c).padStart(4)}  ${map[g] || "(unknown " + g.slice(0, 8) + ")"}`);
