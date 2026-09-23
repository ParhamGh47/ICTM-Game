// Replicates AdamakSpawner's lateral rule against a level's real spline, to see where targets land.
const fs = require("fs");
const path = require("path");
const root = path.resolve(__dirname, "..");
const scene = process.argv[2] || "Assets/Scenes/Levels Scenes/3/Core-3.unity";
const band = process.argv[3] || "RoadEdge";

// --- the spline's centre line, straight out of the scene -------------------------
const raw = fs.readFileSync(path.join(root, scene), "utf8").replace(/\r\n/g, "\n");
const pts = [];
for (const m of raw.matchAll(/^  - \{x: (-?[0-9.eE+-]+), y: (-?[0-9.eE+-]+), z: (-?[0-9.eE+-]+)\}$/gm)) {
  pts.push({ x: +m[1], y: +m[2], z: +m[3] });
}
// the first cachedPoints block is the road's own spline
const distances = [...raw.matchAll(/^  distance: ([-0-9.]+)$/gm)].map((m) => +m[1]);
const roadWidth = +(/^  RoadWidth: ([-0-9.]+)$/m.exec(raw) || [])[1];
const laneWidth = +(/^  laneWidth: ([-0-9.]+)$/m.exec(raw) || [])[1];
const laneAmount = +(/^  laneAmount: ([-0-9.]+)$/m.exec(raw) || [])[1];

const halfWidth = (roadWidth || laneWidth * laneAmount) / 2;
const laneCentre = (Math.max(1, laneAmount / 2) - 0.5) * laneWidth;

const EDGE_INSET = 0.6;
const LANE_CLEARANCE = 1.6;
const LATERAL_JITTER = 0.35;
const SPACING = 55;
const START_TRIM = 30;
const END_TRIM = 30;
const MAX_COUNT = 90;

console.log(`roadWidth=${roadWidth} laneWidth=${laneWidth} laneAmount=${laneAmount} ` +
            `half=${halfWidth} laneCentre=${laneCentre}`);
console.log(`splines in scene (distance): ${distances.join(", ")}`);

function chooseLateral(rng) {
  if (band === "CentreDivider") {
    const lateral = rng() * LATERAL_JITTER;
    return laneCentre - lateral < LANE_CLEARANCE ? null : lateral;
  }

  const inside = laneCentre + LANE_CLEARANCE;
  const outside = halfWidth - 0.3;
  if (inside > outside) return null;

  let lateral = Math.min(Math.max(halfWidth - EDGE_INSET, inside), outside);
  lateral = lateral + (rng() * 2 - 1) * LATERAL_JITTER;
  return Math.min(Math.max(lateral, inside), outside);
}

// deterministic PRNG so the run is repeatable
let seed = 12345;
const rng = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);

// the route: one spline, walked by arc length approximated over the cached points
const total = distances[0] || 3871;
let distance = START_TRIM;
const placed = [];
let skipped = 0;

while (distance <= total - END_TRIM && placed.length < MAX_COUNT) {
  const t = distance / total;
  const i = Math.min(pts.length - 1, Math.max(0, Math.round(t * (pts.length - 1))));
  const p = pts[i];
  const q = pts[Math.min(pts.length - 1, i + 1)];
  const r = pts[Math.max(0, i - 1)];

  let tx = q.x - r.x, tz = q.z - r.z;
  const tl = Math.hypot(tx, tz) || 1;
  tx /= tl; tz /= tl;
  const rx = tz, rz = -tx;

  const side = rng() < 0.5 ? 1 : -1;
  const lateral = chooseLateral(rng);

  if (lateral == null) { skipped++; }
  else {
    placed.push({
      x: p.x + rx * lateral * side,
      z: p.z + rz * lateral * side,
      lateral: lateral * side,
      distance,
    });
  }

  distance += SPACING * (0.8 + rng() * 0.4);
}

const laterals = placed.map((p) => Math.abs(p.lateral)).sort((a, b) => a - b);
console.log(`\nplaced ${placed.length}, skipped ${skipped}, at ~${SPACING} m spacing over ${total.toFixed(0)} m`);
console.log(`|lateral| min/median/max: ${laterals[0].toFixed(2)} / ` +
            `${laterals[Math.floor(laterals.length / 2)].toFixed(2)} / ${laterals[laterals.length - 1].toFixed(2)} m`);
console.log(`clearance from the passing-car lane (${laneCentre} m): ` +
            `${(laterals[0] - laneCentre).toFixed(2)} m at the closest`);
console.log(`clearance from the asphalt edge (${halfWidth} m): ` +
            `${(halfWidth - laterals[laterals.length - 1]).toFixed(2)} m at the closest`);
console.log(`\nfirst few: ` + placed.slice(0, 8).map((p) =>
  `(${p.x.toFixed(0)},${p.z.toFixed(0)}) lat=${p.lateral.toFixed(2)}`).join("  "));
