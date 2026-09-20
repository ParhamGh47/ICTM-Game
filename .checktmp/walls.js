// Mirror of TerrainInvisibleWallPainter's subdivision, run on circular bends, to measure the two claims:
//  - the chain never comes closer to the centre line than the asphalt + a shoulder (never on the road)
//  - no opening between neighbouring segments
function dist(a, b) { return Math.hypot(a[0]-b[0], a[1]-b[1]); }
function segDistToOrigin(a, b) {                       // shortest distance from (0,0) to the chord segment
  const abx = b[0]-a[0], aby = b[1]-a[1];
  const t = Math.max(0, Math.min(1, -(a[0]*abx + a[1]*aby) / (abx*abx + aby*aby)));
  return Math.hypot(a[0]+abx*t, a[1]+aby*t);
}

function plan(R, length, halfRoad, asphaltHalf, offset, maxDev, longest, overlap, inside) {
  const clearance = (halfRoad + offset) - asphaltHalf;
  const tol = Math.min(maxDev, Math.max(0.05, clearance));
  const desired = halfRoad + offset;
  const at = (s) => {                                  // offset point at arc length s
    const phi = s / R;
    const r = inside ? R - desired : R + desired;
    return [r*Math.cos(phi), r*Math.sin(phi)];
  };
  const lateralAt = () => Math.min(halfRoad + offset, R * 0.85);
  const lat = lateralAt();
  const pointAt = (s) => {
    const phi = s / R;
    const r = inside ? R - lat : R + lat;
    return [r*Math.cos(phi), r*Math.sin(phi)];
  };

  const segs = [];
  let s = 0;
  while (s < length - 1e-6) {
    let step = Math.min(longest, length - s);
    for (let i = 0; i < 12; i++) {
      const a = pointAt(s), b = pointAt(s + step);
      let stray = 0;
      for (let k = 1; k <= 3; k++) {
        const t = k * 0.25;
        const onCurve = pointAt(s + step * t);
        const onChord = [a[0] + (b[0]-a[0])*t, a[1] + (b[1]-a[1])*t];
        stray = Math.max(stray, dist(onCurve, onChord));
      }
      if (stray <= tol) break;
      step *= 0.5;
    }
    segs.push([pointAt(s), pointAt(s + step), step]);
    s += step;
  }

  // clearance: the closest any segment's inner face gets to the road centre line's circle
  let closest = Infinity, opened = 0;
  for (const [a, b] of segs) closest = Math.min(closest, segDistToOrigin(a, b));
  // opening: with the joint overlap, neighbouring segments must overlap; measure the largest uncovered
  // span along the barrier by walking the true offset curve and checking it is inside a segment
  let worst = 0;
  const steps = 4000;
  for (let i = 0; i <= steps; i++) {
    const s2 = (i / steps) * length;
    const p = pointAt(s2);
    let best = Infinity;
    for (const [a, b] of segs) {
      const abx = b[0]-a[0], aby = b[1]-a[1];
      const len = Math.hypot(abx, aby) + overlap;
      const t = Math.max(0, Math.min(1, ((p[0]-a[0])*abx + (p[1]-a[1])*aby) / (abx*abx+aby*aby)));
      best = Math.min(best, dist(p, [a[0]+abx*t, a[1]+aby*t]));
    }
    worst = Math.max(worst, best);
  }

  const asphaltEdge = inside ? R - asphaltHalf : R + asphaltHalf;
  const drivableEdge = inside ? R - halfRoad : R + halfRoad;
  return { segs: segs.length, closest, asphaltEdge, drivableEdge, curveToChain: worst, tol };
}

const halfRoad = 8, asphaltHalf = 5, offset = 8, maxDev = 0.25, longest = 15, overlap = 1;

for (const R of [200, 60, 30, 20, 14, 10]) {
  const bend = 'a bend of radius ' + R + 'm, road ' + (asphaltHalf*2) + 'm wide + shoulders';
  for (const inside of [false, true]) {
    const p = plan(R, 200, halfRoad, asphaltHalf, offset, maxDev, longest, overlap, inside);
    const clearancePastAsphalt = inside ? (p.asphaltEdge - p.closest) : (p.closest - p.asphaltEdge);
    console.log(
      (inside ? 'inside ' : 'outside') + ' ' + bend +
      '\n   segments=' + p.segs +
      '  closest approach to centre = ' + p.closest.toFixed(2) + 'm' +
      '  asphalt edge = ' + p.asphaltEdge.toFixed(1) + 'm' +
      '  => ' + clearancePastAsphalt.toFixed(2) + 'm ' + (clearancePastAsphalt >= 0 ? 'CLEAR of the asphalt' : '*** ON THE ASPHALT ***') +
      '\n   furthest the curve strays from the chain = ' + p.curveToChain.toFixed(3) + 'm (tolerance ' + p.tol + ')'
    );
  }
}
