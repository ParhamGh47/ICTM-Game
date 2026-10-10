// Tuning for the rebuilt stage: the truck and the target are now built from primitives, so their sizes are
// known exactly. This mirrors the layout maths for the shot we want: the truck big at the start, the target
// clearly readable when it is hit, and everything inside the frame.
const V = {
  add: (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]],
  sub: (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]],
  mul: (a, s) => [a[0] * s, a[1] * s, a[2] * s],
  dot: (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2],
  cross: (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]],
  norm: (a) => V.mul(a, 1 / Math.hypot(a[0], a[1], a[2])),
};

const fieldOfView = 34, resolution = [764, 360], targetCount = 3;

// The models the stage now builds: a chunky truck and a figure, in metres.
const TRUCK = { length: 4.6, width: 2.7, height: 3.3 };
const TARGET = { width: 0.64, height: 1.88, depth: 0.64 };

const T = {
  carry: 0.45,        // truck lengths of road behind the start of the run
  targetAt: 0.38,     // where the first target stands, in truck lengths
  spacing: 0.3,      // between targets, in truck lengths
  stripHeight: 0.56,  // the frame box's height, in truck lengths
  padZ: 0.45,         // the frame box's slack along the road, in truck lengths
  padX: 0.8,          // the frame box's slack across the road, in truck widths
  view: [-0.86, -0.34, 0.20],
};

const { length, width, height } = TRUCK;

const roadHalfWidth = Math.max(width * 0.95, length * 0.24);
const truckStartZ = -length * T.carry;
const targetZ = length * T.targetAt;
const hitZ = targetZ - length * 0.55;
const targetSpacing = Math.max(length * T.spacing, 1.0);
const roadLength = (targetZ + (targetCount - 1) * targetSpacing - truckStartZ) + length * 1.6;
const stripHeight = length * T.stripHeight;

const viewDirection = V.norm(T.view);
const farTargetZ = targetZ + (targetCount - 1) * targetSpacing;
const centre = [0, stripHeight * 0.3, (truckStartZ + farTargetZ) * 0.5];
const size = [roadHalfWidth * 2 + width * T.padX, stripHeight * 1.25,
  Math.abs(farTargetZ - truckStartZ) + length * T.padZ];
const half = V.mul(size, 0.5);

const right = V.norm(V.cross([0, 1, 0], viewDirection));
const up = V.norm(V.cross(viewDirection, right));
const tanV = Math.tan(fieldOfView * 0.5 * Math.PI / 180);
const tanH = tanV * (resolution[0] / resolution[1]);

let distance = 6;
for (let c = 0; c < 8; c++) {
  const offset = [(c & 1) ? half[0] : -half[0], (c & 2) ? half[1] : -half[1], (c & 4) ? half[2] : -half[2]];
  const depth = V.dot(viewDirection, offset);
  distance = Math.max(distance,
    Math.abs(V.dot(right, offset)) / tanH - depth, Math.abs(V.dot(up, offset)) / tanV - depth);
}
distance *= 1.06;

const world = (p) => p;   // the root offset does not change any of the angles
const eye = V.sub(world(centre), V.mul(viewDirection, distance));
const forward = V.norm(V.sub(world(V.add(centre, [0, 0.2, 0])), eye));
const camRight = V.norm(V.cross([0, 1, 0], forward));
const camUp = V.cross(forward, camRight);

const viewport = (p) => {
  const rel = V.sub(p, eye);
  const z = V.dot(rel, forward);
  return [0.5 + (V.dot(rel, camRight) / z) / (2 * tanH), 0.5 + (V.dot(rel, camUp) / z) / (2 * tanV)];
};

console.log(`truck ${length} x ${width} x ${height}, target ${TARGET.width} x ${TARGET.height}`);
console.log(`road ${(roadHalfWidth * 2).toFixed(2)} wide, ${roadLength.toFixed(1)} long; camera ${distance.toFixed(1)} away`);

const boxes = [
  ['truck at start', [0, height / 2, truckStartZ], [width, height, length]],
  ['truck at the hit', [0, height / 2, hitZ], [width, height, length]],
  ['first target', [0, TARGET.height / 2, targetZ], [TARGET.width, TARGET.height, TARGET.depth]],
  ['target row', [0, TARGET.height / 2, (targetZ + farTargetZ) / 2],
    [width * 1.1, TARGET.height, (farTargetZ - targetZ) + TARGET.depth]],
];

let all = { low: 1, high: 0 };
for (const [name, mid, dims] of boxes) {
  let minX = 1, maxX = 0, minY = 1, maxY = 0;
  for (let c = 0; c < 8; c++) {
    const p = V.add(mid, [(c & 1) ? dims[0] / 2 : -dims[0] / 2, (c & 2) ? dims[1] / 2 : -dims[1] / 2,
      (c & 4) ? dims[2] / 2 : -dims[2] / 2]);
    const [x, y] = viewport(p);
    minX = Math.min(minX, x); maxX = Math.max(maxX, x);
    minY = Math.min(minY, y); maxY = Math.max(maxY, y);
  }
  const inside = minX >= 0 && maxX <= 1 && minY >= 0 && maxY <= 1;
  console.log(`  ${name.padEnd(16)} u ${minX.toFixed(2)}..${maxX.toFixed(2)}  v ${minY.toFixed(2)}..${maxY.toFixed(2)}` +
    `  width ${((maxX - minX) * 100).toFixed(0)}%  height ${((maxY - minY) * 100).toFixed(0)}%  ${inside ? 'INSIDE' : '*** OUTSIDE ***'}`);
  all.low = Math.min(all.low, minX, minY);
  all.high = Math.max(all.high, maxX, maxY);
}
console.log('  all inside:', all.low >= 0 && all.high <= 1);
