// Framing check for the tutorial stage: the strip is laid out from the truck's measured size, the camera solves
// its own distance, and this reports what fraction of the picture the truck and the targets end up filling.
//
// Run: node .checktmp/stageframe2.js [truckLength] [truckWidth] [truckHeight] [targetCount]

const L = Number(process.argv[2] || 5.0);       // the truck's own length (z)
const W = Number(process.argv[3] || 2.4);       // its width (x)
const H = Number(process.argv[4] || 2.6);       // its height (y)
const N = Number(process.argv[5] || 4);         // targets on the stage

// The card's picture, and the stage's own numbers, both straight out of the scripts.
const CARD_W = 764, CARD_H = 360;
const FOV = 34;
const aspect = CARD_W / CARD_H;
const tanV = Math.tan((FOV * 0.5 * Math.PI) / 180);
const tanH = tanV * aspect;

// ---- the strip, laid out from the truck (BuildTruck)
let roadHalfWidth = Math.max(W * 0.95, L * 0.24);
const truckStartZ = -L * 0.45;
const targetZ = L * 0.38;
const hitZ = targetZ - L * 0.55;
const targetSpacing = Math.max(L * 0.3, 1);
const roadLength = (targetZ + (N - 1) * targetSpacing - truckStartZ) + L * 1.6;
const stripHeight = L * 0.56;

// ---- the camera (FrameCamera)
const view = norm([-0.86, -0.34, 0.2]);
const farZ = targetZ + (N - 1) * targetSpacing;
const centre = [0, stripHeight * 0.3, (truckStartZ + farZ) * 0.5];
const size = [roadHalfWidth * 2 + W * 0.5, stripHeight * 1.25, Math.abs(farZ - truckStartZ) + L * 0.25];
const half = size.map((v) => v * 0.5);
let right = norm(cross([0, 1, 0], view));
let up = norm(cross(view, right));

let distance = 6;
for (let c = 0; c < 8; c++) {
  const off = [
    (c & 1) === 0 ? -half[0] : half[0],
    (c & 2) === 0 ? -half[1] : half[1],
    (c & 4) === 0 ? -half[2] : half[2],
  ];
  const depth = dot(view, off);
  const side = Math.abs(dot(right, off));
  const upOff = Math.abs(dot(up, off));
  distance = Math.max(distance, side / tanH - depth, upOff / tanV - depth);
}
distance *= 1.02;

const camPos = sub(centre, mul(view, distance));

// ---- how the shot reads: each box's own width and height, as a fraction of the frame
function fractionOfFrame(p) {
  const rel = sub(p, camPos);
  const depth = dot(view, rel);
  return {
    x: dot(right, rel) / depth / tanH, // -1..1 across the frame
    y: dot(up, rel) / depth / tanV,
  };
}

console.log(`truck ${L.toFixed(2)} x ${W.toFixed(2)} x ${H.toFixed(2)} m, ${N} targets`);
console.log(`strip: road half width ${roadHalfWidth.toFixed(2)} m, camera ${distance.toFixed(2)} m off`);
console.log(`road ${roadLength.toFixed(2)} m long, strip height ${stripHeight.toFixed(2)} m\n`);

const truck = { x: W / 2, y: H / 2, z: L / 2 };
const truckMid = [0, H * 0.5, truckStartZ];
const t = fractionOfFrame(truckMid);
console.log(
  `truck at the start: ${t.x.toFixed(2)}..${(-t.x).toFixed(2)} across (${(2 * t.x).toFixed(0)}% of the width), ` +
    `its own width (${truckMid[1].toFixed(2)} m up) spans ${(2 * W / 2 / 1).toFixed(2)} m`
);

// The truck's own two edges, as the camera sees them: its width, and its length along the strip.
const truckCornerL = fractionOfFrame([-W / 2, H * 0.5, truckStartZ]);
const truckCornerR = fractionOfFrame([W / 2, H * 0.5, truckStartZ]);
const truckNose = fractionOfFrame([0, H * 0.5, truckStartZ - L / 2]);
const truckTail = fractionOfFrame([0, H * 0.5, truckStartZ + L / 2]);
const truckTop = fractionOfFrame([0, H, truckStartZ]);
const truckBottom = fractionOfFrame([0, 0, truckStartZ]);

console.log(
  `truck: width ${Math.abs(truckCornerR.x - truckCornerL.x).toFixed(2)} of the frame, ` +
    `length ${Math.abs(truckTail.x - truckNose.x).toFixed(2)}, ` +
    `height ${Math.abs(truckTop.y - truckBottom.y).toFixed(2)}`
);

// Adamak: measure it as the real prefab does, roughly - a figure about two metres tall on a small base.
const AH = 2.0, AW = 0.8;
for (let i = 0; i < N; i++) {
  const z = targetZ + i * targetSpacing;
  const x = Math.max(-roadHalfWidth * 0.6, Math.min(roadHalfWidth * 0.6, Math.sin(i * 2.4) * roadHalfWidth * 0.45));
  const base = fractionOfFrame([x, 0, z]);
  const top = fractionOfFrame([x, AH, z]);
  const l = fractionOfFrame([x - AW / 2, AH * 0.5, z]);
  const r = fractionOfFrame([x + AW / 2, AH * 0.5, z]);
  console.log(
    `target ${i} at z=${z.toFixed(2)} x=${x.toFixed(2)}: ` +
      `height ${Math.abs(top.y - base.y).toFixed(2)} of the frame, width ${Math.abs(r.x - l.x).toFixed(2)}, ` +
      `centre (${base.x.toFixed(2)}, ${base.y.toFixed(2)})`
  );
}

const far = fractionOfFrame([0, 0, farZ]);
console.log(`\nthe far target sits at (${far.x.toFixed(2)}, ${far.y.toFixed(2)}) - inside the frame is |x| < 1, |y| < 1`);

function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
function mul(a, s) { return [a[0] * s, a[1] * s, a[2] * s]; }
function dot(a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
function cross(a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; }
function norm(a) { const m = Math.sqrt(dot(a, a)); return mul(a, 1 / m); }

