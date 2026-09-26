// The level's own objects, so an odd one - a wind zone, a stray collider, a system that pushes the player -
// can be spotted by comparing levels.
const fs = require("fs");

const clsName = {
  1: "GameObject",
  4: "Transform",
  20: "Camera",
  23: "MeshRenderer",
  33: "MeshFilter",
  54: "Rigidbody",
  64: "MeshCollider",
  65: "BoxCollider",
  81: "AudioListener",
  82: "AudioSource",
  108: "Light",
  114: "MonoBehaviour",
  120: "LineRenderer",
  135: "SphereCollider",
  136: "CapsuleCollider",
  154: "TerrainCollider",
  182: "WindZone",
  198: "ParticleSystem",
  199: "ParticleSystemRenderer",
  218: "Terrain",
  222: "CanvasRenderer",
  223: "Canvas",
  224: "RectTransform",
  225: "CanvasGroup",
};

const scriptPath = {};
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = dir + "/" + e.name;
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith(".cs.meta")) {
      const g = fs.readFileSync(p, "utf8").match(/guid: ([0-9a-f]{32})/);
      if (g) scriptPath[g[1]] = p.replace(/\.meta$/, "").replace("Assets/", "");
    }
  }
})("Assets");

for (const scene of process.argv.slice(2)) {
  const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
  const objs = {};
  for (const b of txt.split(/^--- /m).slice(1)) {
    const m = b.match(/^!u!(\d+) &(\d+)/);
    if (m) objs[m[2]] = { cls: m[1], body: b };
  }
  const name = {};
  const comps = {};
  for (const id in objs) {
    if (objs[id].cls !== "1") continue;
    const n = objs[id].body.match(/m_Name: (.*)/);
    name[id] = n ? n[1].trim() : "?";
    comps[id] = [...objs[id].body.matchAll(/- component: \{fileID: (\d+)\}/g)].map((m) => m[1]);
  }
  const kids = new Set();
  for (const id in objs) {
    if (objs[id].cls !== "4" && objs[id].cls !== "224") continue;
    const fa = objs[id].body.match(/m_Father: \{fileID: (\d+)\}/);
    if (fa && fa[1] !== "0") kids.add(objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/)[1]);
  }

  console.log("== " + scene.split("/").slice(-2).join("/"));
  const roots = Object.keys(name).filter((g) => !kids.has(g));
  for (const g of roots) {
    const list = [];
    for (const c of comps[g] || []) {
      const o = objs[c];
      if (!o) continue;
      let label = clsName[o.cls] || o.cls;
      if (o.cls === "114") {
        const sg = o.body.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})/);
        label = sg ? (scriptPath[sg[1]] || "script " + sg[1].slice(0, 8)).split("/").pop().replace(".cs", "") : "MonoBehaviour";
      }
      list.push(label);
    }
    if (list.length === 1 && list[0] === "Transform") continue; // an empty holder
    console.log("   " + name[g] + ":  " + [...new Set(list)].join(", "));
  }
}
