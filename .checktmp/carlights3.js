// What each passing-car prefab can be lit with: its Light components, and the materials on it that glow.
const fs = require("fs");
const cp = require("child_process");

const files = cp
  .execSync("find Assets/Prefabs/Cars -name '*.prefab'", { encoding: "utf8" })
  .trim()
  .split("\n");

// material guid -> path + emissive colour
const mats = {};
for (const p of cp.execSync("find Assets -name '*.mat'", { encoding: "utf8" }).trim().split("\n")) {
  const t = fs.readFileSync(p, "utf8").replace(/\r/g, "");
  const g = (fs.readFileSync(p + ".meta", "utf8").replace(/\r/g, "").match(/guid: ([0-9a-f]{32})/) || []);
  if (!g[1]) continue;
  const em =
    t.match(/m_EmissiveColor: \{r: ([-+\d.e]+), g: ([-+\d.e]+), b: ([-+\d.e]+), a: ([-+\d.e]+)\}/) ||
    t.match(/- _EmissionColor: \{r: ([-+\d.e]+), g: ([-+\d.e]+), b: ([-+\d.e]+), a: ([-+\d.e]+)\}/);
  mats[g[1]] = { path: p, em: em ? [+em[1], +em[2], +em[3]] : null };
}

for (const f of files) {
  const txt = fs.readFileSync(f, "utf8").replace(/\r/g, "");
  const blocks = txt.split(/^--- /m).slice(1);

  const name = {};
  for (const b of blocks) {
    const m = b.match(/^!u!1 &(\d+)/);
    if (m) {
      const n = b.match(/m_Name: (.*)/);
      name[m[1]] = n ? n[1].trim() : "?";
    }
  }

  const lights = [];
  for (const b of blocks) {
    if (!/^!u!108 &/.test(b)) continue;
    const g = b.match(/m_GameObject: \{fileID: (\d+)\}/);
    const t = b.match(/m_Type: (\d+)/);
    const c = b.match(/m_Color: \{r: ([-\d.]+), g: ([-\d.]+), b: ([-\d.]+)/);
    const rm = b.match(/m_RenderMode: (\d+)/);
    const en = b.match(/m_Enabled: (\d+)/);
    lights.push(
      name[g[1]] +
        "(type " + (t && t[1]) + ", rm " + (rm && rm[1]) + ", enabled " + (en && en[1]) +
        ", col " + (c ? [c[1], c[2], c[3]].join(",") : "-") + ")"
    );
  }

  const emissive = [];
  for (const b of blocks) {
    if (!/^!u!23 &/.test(b)) continue;
    const g = b.match(/m_GameObject: \{fileID: (\d+)\}/);
    const guids = [...b.matchAll(/guid: ([0-9a-f]{32}), type: 2/g)].map((m) => m[1]);
    const lit = guids.map((x) => mats[x]).filter((x) => x && x.em && (x.em[0] || x.em[1] || x.em[2]));
    if (lit.length) {
      emissive.push(
        name[g[1]] + " -> " + lit.map((x) => x.path.split("/").pop() + "(" + x.em.join(",") + ")").join(" | ")
      );
    }
  }

  // and every material slot, to see the lens naming
  const slots = [];
  for (const b of blocks) {
    if (!/^!u!23 &/.test(b)) continue;
    const g = b.match(/m_GameObject: \{fileID: (\d+)\}/);
    const guids = [...b.matchAll(/guid: ([0-9a-f]{32}), type: 2/g)].map((m) => m[1]);
    const active = /m_Enabled: 1/.test(b) && /m_IsActive: 1/.test(txt.split("--- !u!1 &" + g[1])[0] || "");
    slots.push(
      name[g[1]] + " [" + guids.map((x) => (mats[x] ? mats[x].path.split("/").pop() : "?")).join(", ") + "]" +
        (b.match(/m_Enabled: 0/) ? " (renderer off)" : "")
    );
  }

  console.log("== " + f);
  console.log("   lights: " + (lights.length ? lights.join("  ") : "NONE"));
  console.log("   emissive: " + (emissive.length ? emissive.join("  ") : "NONE"));
  for (const s of slots) console.log("   slot: " + s);
}
