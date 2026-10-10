// Geometry + framing check for the tutorial stage, after the run-up was lengthened, the truck was made to stop
// short of the target, and the target was made to come apart.
//
// Run: node .checktmp/stagecheck.js [truckLength] [truckWidth] [truckHeight] [targetWidth] [targetHeight]

const L = Number(process.argv[2] || 5.0);
const W = Number(process.argv[3] || 2.4);
const H = Number(process.argv[4] || 2.6);
const TW = Number(process.argv[5] || 1.0);   // the Adamak's own width, roughly
const TH = Number(process.argv[6] || 2.0);   // and its height

const CARD_W = 764, CARD_H = 360, FOV = 34;
const tanV = Math.tan((FOV * 0.5 * Math.PI) / 180);
const tanH = tanV * (CARD_W / CARD_H);

// ---- the strip (BuildTruck)
const roadHalfWidth = Math.max(W * 0.95, L * 0.24);
const truckStartZ = -L * 1.35;
const targetZ = L * 0.38;
// The stop: the truck's nose is pulled up to the face the target turns towards it, with a hand's width spare.
const front = targetZ - TW * 0.5;                       // the face it arrives at, as measured off the model
const gap = Math.max(0.12, L * 0.035);
const stopA = targetZ - (L * 0.5 + L * 0.2);   // the fallback, for a target that never measures
const stopB = front - gap - L * 0.5;           // the stop the model's own face settles (Measure wins)
const hitZ = stopB;
const stripHeight = L * 0.56;
const roadLength = targetZ - truckStartZ + L * 1.6;

// ---- the shot (FrameCamera)
const view = norm([-0.86, -0.34, 0.2]);
const centre = [0, stripHeight * 0.3, (truckStartZ + targetZ) * 0.5];
const size = [roadHalfWidth * 2 + W * 0.5, stripHeight * 1.25, Math.abs(targetZ - truckStartZ) + L * 0.25];
const half = size.map((v) => v * 0.5);
const right = norm(cross([0, 1, 0], view));
const up = norm(cross(view, right));

let distance = 6;
for (let c = 0; c < 8; c++) {
  const off = [(c & 1) ? half[0] : -half[0], (c & 2) ? half[1] : -half[1], (c & 4) ? half[2] : -half[2]];
  const d = dot(view, off);
  distance = Math.max(distance, Math.abs(dot(right, off)) / tanH - d, Math.abs(dot(up, off)) / tanV - d);
}
distance *= 1.02;
const camPos = sub(centre, mul(view, distance));

const at = (p) => {
  const rel = sub(p, camPos);
  const depth = dot(view, rel);
  return { x: dot(right, rel) / depth / tanH, y: dot(up, rel) / depth / tanV };
};

const nose = hitZ + L / 2;

console.log(`truck ${L} x ${W} x ${H} m;  target ${TW} wide, ${TH} tall`);
console.log(`run: from z=${truckStartZ.toFixed(2)} to z=${hitZ.toFixed(2)}  (${(hitZ - truckStartZ).toFixed(2)} m)`);
console.log(`camera ${distance.toFixed(2)} m off, road ${roadLength.toFixed(2)} m, road half width ${roadHalfWidth.toFixed(2)} m`);
console.log(`\nat the hit: truck nose z=${nose.toFixed(2)}, target face z=${front.toFixed(2)} -> gap ${(front - nose).toFixed(2)} m`);

const truckAtHit = [
  ["truck nose", [0, H * 0.5, nose]],
  ["truck tail", [0, H * 0.5, hitZ - L / 2]],
  ["truck roof", [0, H, hitZ]],
];
const targetAt = [
  ["target feet", [0, 0, targetZ]],
  ["target top", [0, TH, targetZ]],
  ["target front", [0, TH * 0.5, targetZ - TW / 2]],
];

for (const [name, p] of truckAtHit.concat(targetAt)) {
  const f = at(p);
  console.log(`${name.padEnd(13)} (${f.x.toFixed(2)}, ${f.y.toFixed(2)})`);
}

const truckHeightFrac = Math.abs(at([0, H, hitZ]).y - at([0, 0, hitZ]).y);
const truckLengthFrac = Math.abs(at([0, H / 2, nose]).x - at([0, H / 2, hitZ - L / 2]).x);
const targetHeightFrac = Math.abs(at([0, TH, targetZ]).y - at([0, 0, targetZ]).y);
console.log(`\ntruck at the hit: ${(truckHeightFrac * 100).toFixed(0)}% of the frame tall, ${(truckLengthFrac * 100).toFixed(0)}% across`);
console.log(`target: ${(targetHeightFrac * 100).toFixed(0)}% of the frame tall`);
console.log(`frame is |x| < 1, |y| < 1`);

function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
function mul(a, s) { return [a[0] * s, a[1] * s, a[2] * s]; }
function dot(a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
function cross(a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; }
function norm(a) { const m = Math.sqrt(dot(a, a)); return mul(a, 1 / m); }
