const fs = require("fs");
const path = require("path");
const scene = process.argv[2];
const root = path.resolve(__dirname, "..");

const adamakDir = path.join(root, "Assets/Prefabs/Adamaks");
const guidToName = {};
for (const f of fs.readdirSync(adamakDir)) {
  if (!f.endsWith(".prefab.meta")) continue;
  const m = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(path.join(adamakDir, f), "utf8"));
  if (m) guidToName[m[1]] = f.replace(".prefab.meta", "");
}

const raw = fs.readFileSync(scene, "utf8");
const chunks = raw.split(/\r?\n(?=--- !u!)/);
const by = {};
for (const c of chunks) {
  const h = c.split(/\r?\n/)[0];
  const m = /--- !u!(\d+) &(-?\d+)/.exec(h);
  if (!m) continue;
  const b = c.replace(/^.*?\r?\n/, "");
  by[m[2]] = { cls: m[1], b, name: (/^  m_Name: (.*)$/m.exec(b) || [])[1] || "", go: (/^  m_GameObject: \{fileID: (-?\d+)\}/m.exec(b) || [])[1] };
}

function localPos(t) {
  const m = /m_LocalPosition: \{x: (-?[0-9.eE+-]+), y: (-?[0-9.eE+-]+), z: (-?[0-9.eE+-]+)\}/.exec(t.b);
  return m ? [+m[1], +m[2], +m[3]] : null;
}
// accumulate world position of a scene Transform fileID
function worldOfTransformId(id) {
  let cur = id, acc = [0, 0, 0], guard = 0;
  const chain = [];
  while (cur && cur !== "0" && guard++ < 40) {
    const t = by[cur];
    if (!t) { chain.push("missing:" + cur); break; }
    const p = localPos(t);
    if (!p) { chain.push("nolocal:" + cur); break; }
    acc[0] += p[0]; acc[1] += p[1]; acc[2] += p[2];
    const f = /m_Father: \{fileID: (-?\d+)\}/.exec(t.b);
    if (!f) break;
    cur = f[1];
  }
  return { pos: acc, chain };
}

// centre line
let splineC = null;
for (const r of Object.values(by)) if (r.cls === "114" && /cachedPoints:/.test(r.b)) splineC = r;
const pts = [];
const re = /^  - \{x: (-?[0-9.eE+-]+), y: (-?[0-9.eE+-]+), z: (-?[0-9.eE+-]+)\}$/gm;
let m;
while ((m = re.exec(splineC.b))) pts.push({ x: +m[1], y: +m[2], z: +m[3] });
console.log("centre-line points:", pts.length);

const adamaks = [];
for (const c of chunks) {
  const head = c.split(/\r?\n/)[0];
  if (!/1001/.test(head)) continue;
  const src = /m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})/.exec(c);
  if (!src || !guidToName[src[1]]) continue;
  const p = {};
  const r2 = /propertyPath: m_LocalPosition\.(x|y|z)\s*\r?\n\s*value: (-?[0-9.eE+-]+)/g;
  let mm;
  while ((mm = r2.exec(c))) p[mm[1]] = parseFloat(mm[2]);
  const par = /m_TransformParent: \{fileID: (-?\d+)\}/.exec(c);
  let base = [0, 0, 0];
  if (par && par[1] !== "0") base = worldOfTransformId(par[1]).pos;
  adamaks.push({ type: guidToName[src[1]], x: (p.x || 0) + base[0], y: (p.y || 0) + base[1], z: (p.z || 0) + base[2] });
}

const hist = [];
for (const a of adamaks) {
  let bi = -1, bd = Infinity;
  for (let i = 0; i < pts.length; i++) {
    const dx = pts[i].x - a.x, dz = pts[i].z - a.z;
    const d = dx * dx + dz * dz;
    if (d < bd) { bd = d; bi = i; }
  }
  const p0 = pts[Math.max(0, bi - 1)];
  const p1 = pts[Math.min(pts.length - 1, bi + 1)];
  let tx = p1.x - p0.x, tz = p1.z - p0.z;
  const tl = Math.hypot(tx, tz) || 1; tx /= tl; tz /= tl;
  const rx = tz, rz = -tx;                       // right of travel
  const dx = a.x - pts[bi].x, dz = a.z - pts[bi].z;
  const lateral = dx * rx + dz * rz;
  hist.push(lateral);
  console.log(`${a.type.padEnd(9)} world=(${a.x.toFixed(1)},${a.y.toFixed(1)},${a.z.toFixed(1)}) nearest=(${pts[bi].x.toFixed(1)},${pts[bi].y.toFixed(1)},${pts[bi].z.toFixed(1)}) lateral=${lateral.toFixed(2)} m  centreDist=${Math.sqrt(bd).toFixed(2)}`);
}
hist.sort((x, y) => x - y);
console.log("lateral min/median/max:", hist[0].toFixed(2), hist[Math.floor(hist.length / 2)].toFixed(2), hist[hist.length - 1].toFixed(2));
