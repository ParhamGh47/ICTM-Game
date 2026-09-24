// For every painted passing car in a scene: which prefab it came from, whether its waypointsRoot is a live
// path object, its starting waypoint, its speed, and where it sits.
const fs = require("fs");
const path = require("path");

const file = process.argv[2] || "Assets/Scenes/Levels Scenes/1/Core-1.unity";
const text = fs.readFileSync(file, "utf8").replace(/\r/g, "");
const parts = text.split(/^--- /m);

const byId = new Map();
for (const p of parts) {
  const m = p.match(/^!u!(\d+) &(\d+)/);
  if (m) byId.set(m[2], { cls: m[1], p });
}
const val = (p, k) => (p.match(new RegExp("^  " + k + ": ?(.*)$", "m")) || [])[1];
const ref = (p, k) => (p.match(new RegExp("^  " + k + ": \\{fileID: (\\d+)", "m")) || [])[1];

// guid -> asset path, straight off disk so untracked prefabs are found too
const guidIndex = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) walk(full);
    else if (e.name.endsWith(".meta")) {
      const g = (fs.readFileSync(full, "utf8").match(/^guid: ([0-9a-f]+)/m) || [])[1];
      if (g) guidIndex.set(g, full.replace(/\.meta$/, ""));
    }
  }
})("Assets");

const instances = parts.filter((p) => p.startsWith("!u!1001 &"));
console.log("prefab instances in " + file + ": " + instances.length);

let cars = 0;
const byPrefab = new Map();
const orphans = [];
const livePaths = new Map();
const byGeneration = new Map();

for (const inst of instances) {
  const sourceGuid = (inst.match(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)/) || [])[1] || "?";
  const at = inst.indexOf("m_Modifications:");
  if (at < 0) continue;
  const mods = inst.slice(at);
  if (!/propertyPath: waypointsRoot/.test(mods)) continue;

  cars++;

  const grab = (key) => {
    const marker = "propertyPath: " + key + "\n";
    const pos = mods.indexOf(marker);
    if (pos < 0) return null;
    const after = mods.slice(pos + marker.length);
    return {
      value: (after.match(/^      value: (.*)$/m) || [])[1],
      object: (after.match(/^      objectReference: \{fileID: (\d+)\}/m) || [])[1],
    };
  };

  const wp = grab("waypointsRoot");
  const speed = grab("speedKPH");
  const start = grab("startingWaypoint");
  const posX = grab("m_LocalPosition.x");
  const posZ = grab("m_LocalPosition.z");
  const nameMod = mods.match(/propertyPath: m_Name\n      value: (.*)\n/);

  const carName = nameMod ? nameMod[1] : "(no name)";
  const prefab = guidIndex.get(sourceGuid) || sourceGuid;
  const alive = wp && wp.object !== "0" && byId.has(wp.object);

  byPrefab.set(prefab, (byPrefab.get(prefab) || 0) + 1);
  const gen = carName.includes("_Fwd_") ? "Fwd" : carName.includes("_Rev_") ? "Rev" : "other";
  if (!byGeneration.has(gen)) byGeneration.set(gen, { total: 0, dead: 0 });
  byGeneration.get(gen).total++;
  if (!alive) byGeneration.get(gen).dead++;

  if (alive) livePaths.set(wp.object, (livePaths.get(wp.object) || 0) + 1);
  else orphans.push({ carName, prefab, path: wp ? wp.object : "(no modification)", posX, posZ, start, speed });
}

console.log("\ncars with a waypointsRoot modification: " + cars);
console.log("by prefab:");
for (const [k, v] of [...byPrefab].sort((a, b) => b[1] - a[1])) console.log("  " + String(v).padStart(4) + "  " + k);

console.log("\nby lane: " + [...byGeneration].map(([k, v]) => `${k} ${v.total} (${v.dead} with no path)`).join(", "));

console.log("\nstanding still (no live waypointsRoot): " + orphans.length + " of " + cars);
for (const c of orphans.slice(0, 12)) {
  console.log(
    "  " + c.carName.padEnd(20) + " prefab=" + path.basename(c.prefab).padEnd(14) +
    " path=" + c.path + (c.posX ? "  pos " + c.posX.value + "," + c.posZ.value : "") +
    (c.start ? "  start " + c.start.value : "")
  );
}
if (orphans.length > 12) console.log("  ... and " + (orphans.length - 12) + " more");

console.log("\nwaypoint paths that cars actually point at:");
for (const [id, count] of livePaths) {
  const o = byId.get(id);
  const go = byId.get(ref(o.p, "m_GameObject"));
  const nameGo = byId.get(ref(go.p, "m_GameObject"));
  const childCount = ((o.p.match(/m_Children:\n(?:  - \{fileID: \d+\}\n)+/) || [""])[0].match(/- \{fileID/g) || []).length;
  console.log(`  ${id}  "${nameGo ? val(nameGo.p, "m_Name") : "?"}"  ${count} car(s)  ${childCount} child transforms`);
}
