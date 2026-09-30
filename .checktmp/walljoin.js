// Does the new junction join really keep the wall as far from the road as the offset says?
//
// The two roads are modelled as polylines meeting at the origin: A comes in along +X, B leaves at the turn
// angle. The verge walls are sampled every 2 m the way the tool samples them, at the tool's offset, and are
// placeable where they clear every road's asphalt edge by the tool's hard minimum. The old sealing used the
// first and last sample of the verge - placeable or not - and joined them with one straight wall; the new one
// uses the samples the wall actually reaches and joins them by carrying each wall on along its own line.

const O = 35;        // offset from the road edge
const H = 5;         // asphalt + shoulders, half width
const REQUIRED = 2;  // keepOffRoads + RoadField.Slack
const STEP = 2;
const LEN = 400;

function pointSegDistance(p, a, b) {
  const abx = b[0] - a[0], abz = b[1] - a[1];
  const apx = p[0] - a[0], apz = p[1] - a[1];
  const denom = abx * abx + abz * abz;
  let t = denom > 1e-9 ? (apx * abx + apz * abz) / denom : 0;
  t = Math.max(0, Math.min(1, t));
  const dx = p[0] - (a[0] + abx * t), dz = p[1] - (a[1] + abz * t);
  return Math.hypot(dx, dz);
}

// Clearance to the asphalt edge of the nearest road, and the distance to each road's own line.
function measure(p, roads) {
  let clearance = Infinity;
  const per = [];
  for (const r of roads) {
    let best = Infinity;
    for (let i = 0; i < r.length - 1; i++) best = Math.min(best, pointSegDistance(p, r[i], r[i + 1]));
    per.push(best);
    clearance = Math.min(clearance, best - H);
  }
  return { clearance, per };
}

function lineDistance(from, to, roads) {
  const length = Math.hypot(to[0] - from[0], to[1] - from[1]);
  const steps = Math.max(2, Math.ceil(length / 0.5));
  let clearance = Infinity;
  const per = [Infinity, Infinity];
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const p = [from[0] + (to[0] - from[0]) * t, from[1] + (to[1] - from[1]) * t];
    const m = measure(p, roads);
    clearance = Math.min(clearance, m.clearance);
    for (let r = 0; r < per.length; r++) per[r] = Math.min(per[r], m.per[r]);
  }
  return { clearance, per };
}

function pathMeasure(points, roads) {
  let clearance = Infinity;
  const per = [Infinity, Infinity];
  for (let i = 0; i < points.length - 1; i++) {
    const m = lineDistance(points[i], points[i + 1], roads);
    clearance = Math.min(clearance, m.clearance);
    for (let r = 0; r < per.length; r++) per[r] = Math.min(per[r], m.per[r]);
  }
  return { clearance, per };
}

function buildRoads(turnDeg) {
  const phi = turnDeg * Math.PI / 180;
  const a = [[-LEN, 0], [0, 0]];
  const b = [[0, 0], [Math.cos(phi) * LEN, Math.sin(phi) * LEN]];
  return { a, b, phi };
}

// A road's own "right" is across = (tangent.z, -tangent.x).
function rightOf(tangent) { return [tangent[1], -tangent[0]]; }

function run(turnDeg) {
  const { a, b, phi } = buildRoads(turnDeg);
  const roads = [a, b];

  const forwardA = [1, 0];
  const forwardB = [Math.cos(phi), Math.sin(phi)];
  const rightA = rightOf(forwardA);
  const rightB = rightOf(forwardB);

  const samplesA = [];
  for (let x = -LEN; x <= 0.0001; x += STEP) samplesA.push([x, 0]);
  const samplesB = [];
  for (let d = 0; d <= LEN; d += STEP) samplesB.push([forwardB[0] * d, forwardB[1] * d]);

  const out = [];

  for (const side of [1, -1]) {
    // Where the wall stands at each sample (full offset) and whether that is legal at all.
    const wallA = samplesA.map((c) => {
      const p = [c[0] + rightA[0] * side * O, c[1] + rightA[1] * side * O];
      return { centre: c, point: p, placeable: measure(p, roads).clearance >= REQUIRED };
    });
    const wallB = samplesB.map((c) => {
      const p = [c[0] + rightB[0] * side * O, c[1] + rightB[1] * side * O];
      return { centre: c, point: p, placeable: measure(p, roads).clearance >= REQUIRED };
    });

    // --- the tool's own minimum lateral at an unplaceable sample: 2 m past the asphalt edge.
    const minLateral = H + REQUIRED;
    const oldEndA = x => [x, rightA[1] * side * minLateral];
    const oldEndB = c => [c[0] + rightB[0] * side * minLateral, c[1] + rightB[1] * side * minLateral];

    const oldA = oldEndA(samplesA[samplesA.length - 1][0]);
    const oldB = oldEndB(samplesB[0]);

    let lastStand = -1, firstStand = -1;
    for (let i = 0; i < wallA.length; i++) if (wallA[i].placeable) lastStand = i;
    for (let i = 0; i < wallB.length; i++) if (wallB[i].placeable && firstStand < 0) firstStand = i;

    const newA = wallA[lastStand].point;
    const newB = wallB[firstStand].point;

    // --- the new join: carry each wall on along its own line until the two meet.
    const da = side > 0 ? forwardA : forwardA;   // both ends lead off their road along the road heading
    const outwardA = [-1, 0];                    // A's wall is walked towards the corner; outward is past it
    const outwardB = [-forwardB[0], -forwardB[1]];

    let path = [newA, newB];
    const cross = outwardA[0] * outwardB[1] - outwardA[1] * outwardB[0];
    let cornered = false;
    if (Math.abs(cross) >= 0.05) {
      const delta = [newB[0] - newA[0], newB[1] - newA[1]];
      const along = (delta[0] * outwardB[1] - delta[1] * outwardB[0]) / cross;
      const meeting = [newA[0] + outwardA[0] * along, newA[1] + outwardA[1] * along];
      const reach = Math.max(Math.hypot(delta[0], delta[1]), O) * 1.5;
      if (Math.abs(along) <= reach && Math.hypot(newB[0] - meeting[0], newB[1] - meeting[1]) <= reach) {
        // Both legs have to reach the corner without crossing a road, exactly as the tool checks.
        const legOk = (p, q) => lineDistance(p, q, roads).clearance >= REQUIRED;
        if (legOk(newA, meeting) && legOk(meeting, newB)) {
          path = [newA, meeting, newB];
          cornered = true;
        }
      }
    }

    const old = pathMeasure([oldA, oldB], roads);
    const fresh = pathMeasure(path, roads);
    const endDistance = Math.hypot(newA[0] - newB[0], newA[1] - newB[1]);

    out.push({
      side: side > 0 ? 'right of travel' : 'left of travel',
      'old end lateral': minLateral.toFixed(1),
      'new end lateral': O.toFixed(1),
      'end gap (m)': endDistance.toFixed(1),
      corner: cornered ? 'corner' : 'chord',
      'old closest to asphalt': old.clearance.toFixed(1),
      'old closest to a road line': Math.min(old.per[0], old.per[1]).toFixed(1),
      'new closest to asphalt': fresh.clearance.toFixed(1),
      'new closest to a road line': Math.min(fresh.per[0], fresh.per[1]).toFixed(1),
    });
  }

  return { turnDeg, out };
}

for (const turn of [30, 45, 60, 90, 120, 150, 180]) {
  const r = run(turn);
  console.log('=== turn ' + turn + ' deg ===');
  console.table(r.out);
}
