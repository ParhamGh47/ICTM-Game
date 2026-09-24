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

// raw guid bytes -> asset
const rawHex = new Map();
for (const [g, p] of Object.entries(map)) rawHex.set(Buffer.from(g, "hex").toString("latin1"), p);

const files = process.argv.slice(2);
for (const f of files) {
  const b = fs.readFileSync(f);
  const s = b.toString("latin1");
  let hits = 0;
  const found = new Map();
  for (let off = 0; off + 16 <= s.length; off++) {
    const key = s.substr(off, 16);
    const p = rawHex.get(key);
    if (p) { hits++; if (found.size < 25) found.set(p, (found.get(p) || 0) + 1); }
  }
  console.log("\n=== " + f + " (" + b.length + ") guid-like matches at any offset: " + hits + " in " + found.size + " distinct (capped)");
  for (const [p, c] of found) console.log("   " + p);
}
