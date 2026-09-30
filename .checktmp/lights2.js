const fs = require("fs");

const strip = s => s.replace(/\r/g, "");
const read = p => strip(fs.readFileSync(p, "utf8"));

// ---------------------------------------------------------------- index every prefab's light count
const prefabLights = new Map();   // guid -> { enabled, byType }
const prefabName = new Map();     // guid -> path
const tnames = { 0: "Spot", 1: "Dir", 2: "Point", 3: "Area" };

function lightsIn(text) {
  return [...text.matchAll(/--- !u!108 &\d+\nLight:\n([\s\S]*?)(?=\n--- )/g)]
    .map(m => m[1])
    .filter(b => !/m_Enabled: 0/.test(b))
    .map(b => { const t = b.match(/m_Type: (\d+)/); return tnames[t ? +t[1] : -1] || "?"; });
}

function walk(dir, fn) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = dir + "/" + e.name;
    if (e.isDirectory()) { walk(p, fn); continue; }
    fn(p);
  }
}

walk("Assets", p => {
  if (!p.endsWith(".prefab")) return;
  const meta = p + ".meta";
  if (!fs.existsSync(meta)) return;
  const g = (read(meta).match(/guid:\s*([0-9a-f]{32})/) || [])[1];
  if (!g) return;
  const l = lightsIn(read(p));
  prefabLights.set(g, l);
  prefabName.set(g, p);
});

// ---------------------------------------------------------------- per core scene
const base = "Assets/Scenes/Levels Scenes";
const terrainKeys = ["m_TreeDistance", "m_TreeBillboardDistance", "m_TreeMaximumFullLODCount",
  "m_DetailObjectDistance", "m_DetailObjectDensity", "m_HeightmapPixelError", "m_SplatMapDistance", "m_DrawInstanced"];

for (const d of fs.readdirSync(base)) {
  const dir = `${base}/${d}`;
  let st; try { st = fs.statSync(dir); } catch (e) { continue; }
  if (!st.isDirectory()) continue;

  for (const f of fs.readdirSync(dir)) {
    if (!/^Core-.*\.unity$/.test(f)) continue;
    const t = read(dir + "/" + f);

    // terrain dials
    const dials = terrainKeys.map(k => {
      const m = t.match(new RegExp(k + ":\\s*([\\d.]+)"));
      return k.replace("m_", "") + "=" + (m ? m[1] : "-");
    });

    // scene's own enabled lights
    const own = lightsIn(t);

    // lights arriving through prefab instances
    const counts = {};
    let instanceLights = 0;
    const carriers = {};
    for (const m of t.matchAll(/m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32})/g)) {
      const l = prefabLights.get(m[1]);
      if (!l || !l.length) continue;
      instanceLights += l.length;
      for (const ty of l) counts[ty] = (counts[ty] || 0) + 1;
      const nm = prefabName.get(m[1]) || m[1];
      carriers[nm] = (carriers[nm] || 0) + 1;
    }

    console.log(`=== ${d}/${f}`);
    console.log("   terrain:", dials.join("  "));
    console.log(`   lights: scene=${own.length} ${JSON.stringify(own.reduce((a, x) => (a[x] = (a[x] || 0) + 1, a), {}))}, via prefab instances=${instanceLights} ${JSON.stringify(counts)}  -> total ${own.length + instanceLights}`);
    const top = Object.entries(carriers).sort((a, b) => b[1] - a[1]).slice(0, 6);
    if (top.length) console.log("   carriers:", top.map(([k, v]) => `${k} x${v}`).join(", "));
  }
}

// ---------------------------------------------------------------- do scripts switch lights on?
console.log("\n=== scripts that touch Light components ===");
walk("Assets/Scripts", p => {
  if (!p.endsWith(".cs")) return;
  const t = read(p);
  const hits = [];
  for (const line of t.split("\n")) {
    if (/\.enabled\s*=\s*(true|false)/.test(line) && /light|Light/.test(line)) hits.push(line.trim());
    else if (/GetComponents?InChildren<Light>|AddComponent<Light>|\bLight\b\s+\w+\s*=/.test(line)) hits.push(line.trim());
  }
  if (hits.length) console.log(`  ${p}\n      ` + hits.slice(0, 4).join("\n      "));
});
