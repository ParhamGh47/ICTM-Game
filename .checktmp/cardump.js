// Dump the interesting parts of a painted-car prefab: the root transform, the rigidbody, and every
// AICarController with its serialized fields.
const fs = require("fs");

const GUID_AI = "d9c3d48bc623c6b4b8ba5dfa74867e5d";

function load(file) {
  const text = fs.readFileSync(file, "utf8").replace(/\r/g, "");
  const byId = new Map();
  for (const part of text.split(/^--- /m)) {
    const m = part.match(/^!u!(\d+) &(\d+)/);
    if (!m) continue;
    byId.set(m[2], { cls: m[1], p: part });
  }
  return byId;
}

const val = (p, k) => (p.match(new RegExp("^  " + k + ": ?(.*)$", "m")) || [])[1];
const ref = (p, k) => (p.match(new RegExp("^  " + k + ": \\{fileID: (\\d+)", "m")) || [])[1];

for (const file of process.argv.slice(2)) {
  const byId = load(file);
  console.log("\n================ " + file);

  // prefab root: the GameObject with no m_Father transform
  const transforms = [...byId.values()].filter((o) => o.cls === "4" || o.cls === "224");
  const children = new Set(transforms.map((t) => ref(t.p, "m_Father")).filter(Boolean));
  const roots = transforms.filter((t) => !children.has(t.p.match(/&(\d+)/)[1]));

  for (const t of roots) {
    const go = byId.get(ref(t.p, "m_GameObject"));
    console.log(`root GO: ${go ? val(go.p, "m_Name") : "?"}  transform ${t.p.match(/&(\d+)/)[1]}`);
    console.log(`  localRotation ${val(t.p, "m_LocalRotation")}  eulerHint ${val(t.p, "m_LocalEulerAnglesHint")}`);
    console.log(`  localPosition ${val(t.p, "m_LocalPosition")}  scale ${val(t.p, "m_LocalScale")}`);
    if (go) {
      const comps = (go.p.match(/- component: \{fileID: (\d+)\}/g) || []).map((c) => c.match(/\d+/)[0]);
      for (const c of comps) {
        const cc = byId.get(c);
        if (!cc) continue;
        const sg = (cc.p.match(/m_Script: .*guid: ([0-9a-f]+)/) || [])[1];
        if (cc.cls === "54") {
          console.log(`  Rigidbody: mass ${val(cc.p, "m_Mass")} kinematic ${val(cc.p, "m_IsKinematic")} drag ${val(cc.p, "m_Drag")} interp ${val(cc.p, "m_Interpolate")} collDetect ${val(cc.p, "m_CollisionDetection")} gravity ${val(cc.p, "m_UseGravity")}`);
        } else if (sg === GUID_AI) {
          console.log(`  AICarController ${c}:`);
          for (const line of cc.p.split("\n")) {
            if (/^  [a-zA-Z_]/.test(line) && !/m_(Object|Script|Editor|Name|Corresponding|Prefab)/.test(line)) console.log("    " + line.trim());
          }
        } else if (sg) {
          console.log(`  other script ${sg}`);
        }
      }
    }
  }

  // every AICarController anywhere in the file
  for (const o of byId.values()) {
    if (!o.p.includes(GUID_AI)) continue;
    const go = byId.get(ref(o.p, "m_GameObject"));
    if (!go) continue;
    const goName = val(go.p, "m_Name");
    const tx = ref(o.p, "m_GameObject");
    // is this the root's GameObject?
    const tr = [...byId.values()].find((x) => (x.cls === "4" || x.cls === "224") && ref(x.p, "m_GameObject") === tx);
    const father = tr ? ref(tr.p, "m_Father") : undefined;
    console.log(`  AICarController on "${goName}" (transform ${tr ? tr.p.match(/&(\d+)/)[1] : "?"}, father ${father || "none"})`);
  }
}
