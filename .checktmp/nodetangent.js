// Nearest road spline node to a point: its position, tangent direction (yaw) and grade, in a scene.
const fs = require("fs");
const scene = process.argv[2];
const cx = +process.argv[3], cz = +process.argv[4];

const txt = fs.readFileSync(scene, "utf8").replace(/\r/g, "");
const blocks = txt.split(/^--- /m).slice(1);
const nodes = [];
for (const b of blocks) {
  const p = b.match(/pos: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/);
  const t = b.match(/tangent: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+), z: ([-0-9.eE]+)\}/);
  if (!p || !t) continue;
  nodes.push({ pos: [+p[1], +p[2], +p[3]], tan: [+t[1], +t[2], +t[3]] });
}
nodes.sort((a, b) => Math.hypot(a.pos[0] - cx, a.pos[2] - cz) - Math.hypot(b.pos[0] - cx, b.pos[2] - cz));
console.log(`${nodes.length} spline nodes in scene`);
for (const n of nodes.slice(0, 6)) {
  const d = Math.hypot(n.pos[0] - cx, n.pos[2] - cz);
  const yaw = (Math.atan2(n.tan[0], n.tan[2]) * 180 / Math.PI + 360) % 360;
  const grade = Math.asin(n.tan[1] / Math.hypot(...n.tan)) * 180 / Math.PI;
  console.log(`  ${d.toFixed(1)}m  pos ${n.pos.map(v => v.toFixed(2)).join(", ")}  tangentYaw ${yaw.toFixed(1)}deg  grade ${grade.toFixed(1)}deg`);
}
