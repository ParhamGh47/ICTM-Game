// How far the player's spawn is from the road centreline in a level, and how high the road is there. A truck
// started beside the road rather than on it sits on the terrain, which is bumpy and sloped - that is a truck
// that jitters and creeps on the spot.
const fs = require("fs");

for (const scene of process.argv.slice(2)) {
  const truckGuid = fs
    .readFileSync("Assets/Prefabs/Utils/Truck (Player).prefab.meta", "utf8")
    .match(/guid: ([0-9a-f]{32})/)[1];

  const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
  const objs = {};
  for (const b of txt.split(/^--- /m).slice(1)) {
    const m = b.match(/^!u!(\d+) &(\d+)/);
    if (m) objs[m[2]] = { cls: m[1], body: b };
  }
  const name = {};
  for (const id in objs) {
    if (objs[id].cls !== "1") continue;
    const n = objs[id].body.match(/m_Name: (.*)/);
    name[id] = n ? n[1].trim() : "?";
  }
  const tr = {};
  for (const id in objs) {
    if (objs[id].cls !== "4" && objs[id].cls !== "224") continue;
    const g = objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/);
    const fa = objs[id].body.match(/m_Father: \{fileID: (\d+)\}/);
    const p = objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
    tr[id] = { go: g && g[1], father: fa ? fa[1] : null, pos: p ? [+p[1], +p[2], +p[3]] : [0, 0, 0] };
  }
  const world = (tid) => {
    let p = [0, 0, 0], cur = tid, n = 0;
    while (cur && tr[cur] && n++ < 60) {
      p = [p[0] + tr[cur].pos[0], p[1] + tr[cur].pos[1], p[2] + tr[cur].pos[2]];
      cur = tr[cur].father;
    }
    return p;
  };

  // truck spawn
  let spawn = null;
  for (const id in objs) {
    if (objs[id].cls !== "1001" || !objs[id].body.includes("guid: " + truckGuid)) continue;
    const b = objs[id].body;
    const props = {};
    for (const m of b.matchAll(
      /- target: \{fileID: (-?\d+), guid: [0-9a-f]+, type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g
    ))
      props[m[2]] = m[3];
    const local = [+(props["m_LocalPosition.x"] || 0), +(props["m_LocalPosition.y"] || 0), +(props["m_LocalPosition.z"] || 0)];
    const parent = (b.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
    const base = parent && parent !== "0" ? world(parent) : [0, 0, 0];
    spawn = [base[0] + local[0], base[1] + local[1], base[2] + local[2]];
  }

  // every road spline in the scene: an object with `nodes:` and a list of node objects carrying a position
  const roads = [];
  for (const id in objs) {
    const b = objs[id].body;
    if (!/^  nodes:/m.test(b)) continue;
    const list = [...b.matchAll(/^  - \{fileID: (\d+)\}$/gm)].map((m) => m[1]);
    const pts = list.map((i) => {
      const node = objs[i];
      if (!node) return null;
      const g = node.body.match(/m_GameObject: \{fileID: (\d+)\}/);
      const t = g ? Object.keys(tr).find((k) => tr[k].go === g[1]) : null;
      const p = node.body.match(/pos: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
      if (t) return world(t);
      return p ? [+p[1], +p[2], +p[3]] : null;
    });
    if (pts.length >= 3 && pts.every((p) => p)) roads.push({ id, name: name[id], pts });
  }

  console.log("== " + scene.split("/").slice(-2).join("/") + "   spawn " + (spawn ? spawn.map((v) => v.toFixed(2)).join(", ") : "?"));
  for (const r of roads) {
    let best = { d: Infinity, y: 0, i: -1, at: null };
    for (let i = 0; i < r.pts.length - 1; i++) {
      const a = r.pts[i], b = r.pts[i + 1];
      const vx = b[0] - a[0], vz = b[2] - a[2];
      const len2 = vx * vx + vz * vz;
      let t = len2 > 1e-6 ? ((spawn[0] - a[0]) * vx + (spawn[2] - a[2]) * vz) / len2 : 0;
      t = Math.max(0, Math.min(1, t));
      const x = a[0] + vx * t, z = a[2] + vz * t, y = a[1] + (b[1] - a[1]) * t;
      const d = Math.hypot(spawn[0] - x, spawn[2] - z);
      if (d < best.d) best = { d, y, i, at: [x, y, z] };
    }
    const total = r.pts.reduce((s, p, i) => {
      if (i === 0) return 0;
      const q = r.pts[i - 1];
      return s + Math.hypot(p[0] - q[0], p[1] - q[1], p[2] - q[2]);
    }, 0);
    console.log(
      "   road '" + r.name + "' (" + r.pts.length + " nodes, " + total.toFixed(0) + " m): spawn is " +
        best.d.toFixed(2) + " m off the centreline, road y there " + best.y.toFixed(2) +
        "  ->  truck is " + (spawn[1] - best.y).toFixed(2) + " m above the centreline"
    );
  }
}
