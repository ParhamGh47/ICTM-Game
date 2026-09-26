// How close the two painted platoons (Fwd and Rev) come to each other in a level, so "the cars are too close
// at some points" can be told apart from "a stray pair happens to overlap".
const fs = require("fs");

const f = process.argv[2];
const txt = fs.readFileSync(f, "utf8").replace(/\r/g, "");
const blocks = txt.split(/^--- /m).slice(1);

const objs = {};
for (const b of blocks) {
  const m = b.match(/^!u!(\d+) &(\d+)/);
  if (m) objs[m[2]] = { cls: m[1], body: b };
}
const name = {};
for (const id in objs) {
  if (objs[id].cls === "1") {
    const n = objs[id].body.match(/m_Name: (.*)/);
    name[id] = n ? n[1].trim() : "?";
  }
}
const tr = {};
for (const id in objs) {
  if (objs[id].cls === "4" || objs[id].cls === "224") {
    const g = objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/);
    const fa = objs[id].body.match(/m_Father: \{fileID: (\d+)\}/);
    const p = objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/);
    tr[id] = { go: g && g[1], father: fa && fa[1], pos: p ? [+p[1], +p[2], +p[3]] : [0, 0, 0], name: name[g && g[1]] };
  }
}
function world(tid) {
  let p = [0, 0, 0], cur = tid, n = 0;
  while (cur && tr[cur] && n++ < 50) {
    const t = tr[cur];
    p = [p[0] + t.pos[0], p[1] + t.pos[1], p[2] + t.pos[2]];
    cur = t.father;
  }
  return p;
}

const cars = [];
for (const id in objs) {
  if (objs[id].cls !== "1001") continue;
  const b = objs[id].body;
  const mods = [
    ...b.matchAll(
      /- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g
    ),
  ];
  const d = {};
  for (const m of mods) d[m[3]] = m[4];
  const mm = (d["m_Name"] || "").match(/^(\w+?)_(Fwd|Rev)_(\d+)$/);
  if (!mm) continue;
  const par = (b.match(/m_TransformParent: \{fileID: (\d+)\}/) || [])[1];
  const base = par ? world(par) : [0, 0, 0];
  const lp = [+(d["m_LocalPosition.x"] || 0), +(d["m_LocalPosition.y"] || 0), +(d["m_LocalPosition.z"] || 0)];
  cars.push({
    nm: d["m_Name"],
    dir: mm[2],
    wp: d["startingWaypoint"] !== undefined ? Math.round(+d["startingWaypoint"]) : -1,
    pos: [base[0] + lp[0], base[1] + lp[1], base[2] + lp[2]],
  });
}

const fwd = cars.filter((c) => c.dir === "Fwd");
const rev = cars.filter((c) => c.dir === "Rev");
console.log(f + "  fwd=" + fwd.length + " rev=" + rev.length);

const pairs = [];
for (const a of fwd) {
  for (const b of rev) {
    const d = Math.hypot(a.pos[0] - b.pos[0], a.pos[1] - b.pos[1], a.pos[2] - b.pos[2]);
    pairs.push({ d, a, b });
  }
}
pairs.sort((x, y) => x.d - y.d);
console.log("closest Fwd/Rev pairs:");
for (const p of pairs.slice(0, 8)) {
  console.log(
    "   " + p.d.toFixed(1) + "m  " + p.a.nm + " (wp " + p.a.wp + ")  <->  " + p.b.nm + " (wp " + p.b.wp + ")"
  );
}
const within = [10, 20, 40, 60, 100];
for (const w of within) console.log("   pairs closer than " + w + "m: " + pairs.filter((p) => p.d < w).length);
