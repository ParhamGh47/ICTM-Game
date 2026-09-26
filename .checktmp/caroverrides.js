// What the painted cars actually have overridden on them: the light state and the lens glow the painter
// was supposed to write into the scene, resolved back to the object inside the car prefab.
const fs = require("fs");

const scene = process.argv[2];

const prefabs = {};
for (const p of [
  "Assets/Prefabs/Cars/206/206.prefab",
  "Assets/Prefabs/Cars/911/911.prefab",
  "Assets/Prefabs/Cars/Car/Car.prefab",
  "Assets/Prefabs/Cars/Car/Truck.prefab",
]) {
  const guid = fs.readFileSync(p + ".meta", "utf8").match(/guid: ([0-9a-f]{32})/)[1];
  const txt = fs.readFileSync(p, "utf8").replace(/\r/g, "");
  const blocks = txt.split(/^--- /m).slice(1);
  const name = {};
  const owner = {};
  const cls = {};
  for (const b of blocks) {
    const m = b.match(/^!u!(\d+) &(\d+)/);
    if (!m) continue;
    cls[m[2]] = m[1];
    if (m[1] === "1") {
      const n = b.match(/m_Name: (.*)/);
      name[m[2]] = n ? n[1].trim() : "?";
    }
  }
  for (const b of blocks) {
    const m = b.match(/^!u!(4|114|23|108|65) &(\d+)/);
    if (!m) continue;
    const g = b.match(/m_GameObject: \{fileID: (\d+)\}/);
    owner[m[2]] = g ? name[g[1]] : "?";
  }
  prefabs[guid] = { file: p, name, cls, owner, blocks };
}

const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const blocks = txt.split(/^--- /m).slice(1);

const perCar = [];
for (const b of blocks) {
  if (!/^!u!1001 &/.test(b)) continue;
  const mods = [
    ...b.matchAll(
      /- target: \{fileID: (-?\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g
    ),
  ];
  const d = {};
  for (const m of mods) d[m[3]] = m[4];
  if (!/^(206|911|Car|Truck)_(Fwd|Rev)_\d+$/.test(d["m_Name"] || "")) continue;

  const guid = (b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/) || [])[1];
  const pf = prefabs[guid];
  const notes = [];
  for (const m of mods) {
    const target = m[1];
    const path = m[3];
    if (path.startsWith("m_Local") || path === "m_Name" || path === "m_RootOrder") continue;
    const cls = pf ? pf.cls[target] : "?";
    const obj = pf ? pf.owner[target] : "?";
    notes.push(`${path}=${m[4]} @${cls}:${obj}`);
  }
  const blocksOf = mods.filter((m) => m[3] === "m_Enabled").length;
  perCar.push({ name: d["m_Name"], src: pf ? pf.file.split("/").pop() : guid, notes, blocksOf });
}

const byModel = {};
for (const c of perCar) (byModel[c.src] = byModel[c.src] || []).push(c);

for (const model in byModel) {
  console.log("== " + model + "  instances=" + byModel[model].length);
  const noteCount = {};
  for (const c of byModel[model]) for (const n of c.notes) noteCount[n] = (noteCount[n] || 0) + 1;
  for (const n of Object.keys(noteCount).sort()) console.log("     " + String(noteCount[n]).padStart(3) + " x " + n);
  console.log("   sample: " + byModel[model][0].name + " -> " + byModel[model][0].notes.join(" | "));
}
