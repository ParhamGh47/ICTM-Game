// Does the new reset rule actually put the truck on the racing side of Core-1's start wall?
//
// The rule: the truck is measured along the road (param * spline.distance, which is what RoadArchitect's
// SplineC gives - node distances are metres), and compared with the gate's own along-road position. A gate
// whose course runs past it may not be left ahead of the truck; one whose course runs up to it may not be
// left behind.
//
// Geometry read from the scene:
//   Core-1 road nodes  (960.22, 5.36) -> (961.09, 50.39) -> (962.75, 179.0) ...
//   Core-1 truck spawn (967.4, 52.5)
//   Core-1 Block wall  three pieces centred near (955.1, 32.1), (960.7, 32.1), (966.2, 32.1)

const NODES = [
  [960.2241, 5.3649],
  [961.0888, 50.390896],
  [962.75116, 178.99196],
  [961.92816, 293.72174],
];

const REACH = 25;
const CLEARANCE = 6;

// Along-road distance of the first node, then cumulative to each node: metres, since node distances are.
const cumulative = [0];
for (let i = 1; i < NODES.length; i++) {
  const dx = NODES[i][0] - NODES[i - 1][0];
  const dz = NODES[i][1] - NODES[i - 1][1];
  cumulative.push(cumulative[i - 1] + Math.hypot(dx, dz));
}

// The along-road distance of a point, on this (nearly straight) stretch: closest point on the polyline.
function along(x, z) {
  let best = 0;
  let bestD = Infinity;
  for (let i = 1; i < NODES.length; i++) {
    const [ax, az] = NODES[i - 1];
    const [bx, bz] = NODES[i];
    const dx = bx - ax;
    const dz = bz - az;
    const len2 = dx * dx + dz * dz;
    let t = ((x - ax) * dx + (z - az) * dz) / len2;
    t = Math.max(0, Math.min(1, t));
    const px = ax + t * dx;
    const pz = az + t * dz;
    const d = Math.hypot(x - px, z - pz);
    if (d < bestD) {
      bestD = d;
      best = cumulative[i - 1] + t * Math.sqrt(len2);
    }
  }
  return best;
}

// Measured from the points themselves: the truck's own spawn, and the middle piece of the start wall.
const truckAlong = along(967.4, 52.5);
const wallAlong = along(960.7, 32.1);

console.log('road length to last node:', cumulative[3].toFixed(1), 'm');
console.log('truck start along road:', truckAlong.toFixed(1), 'm  (x 967.4, z 52.5)');
console.log('wall along road:      ', wallAlong.toFixed(1), 'm  (x 960.7, z 32.1)');
console.log('');

// The wall is a long thin box rotated in the scene, so its extent ALONG the road matters: a truck 6 m past
// the wall's centre could still be inside its far end.
const wallLength = 3.5430894 * 5.29338; // size.z * child scale.z
const wallYaw = -116;                    // degrees, from the prefab instance's quaternion
const alongExtent = Math.abs(wallLength * Math.cos((wallYaw * Math.PI) / 180));
console.log('wall length:', wallLength.toFixed(1), 'm -> extent along the road:', alongExtent.toFixed(1), 'm');
console.log('far edge along road:', (wallAlong + alongExtent / 2).toFixed(1), 'm');
console.log('');

const dir = 1; // travelling with the spline (road ascends in z)
const cases = [20, 25, 30, 32.1, 34, 40, 52.5];

// The road here is very nearly straight along z, so a metre along it is very nearly a metre of z; the along
// figure is what the code compares, and the z is printed beside it so the answer can be read off the scene.
console.log('reset lands at    along   gate ahead  action   placed at');
for (const z of cases) {
  const here = along(967.4, z);
  const ahead = (wallAlong - here) * dir;
  const wrong = ahead > 0 && ahead <= REACH; // the course runs past the wall
  const clearance = CLEARANCE;
  const placedAlong = wrong ? wallAlong + dir * clearance : here;
  const zPlaced = 5.3649 + placedAlong; // first segment: along metres are z metres
  const verdict = wrong ? 'CLAMP' : 'leave';
  const note = wrong ? (placedAlong > wallAlong + alongExtent / 2 ? '  clear of the wall' : '  STILL IN THE WALL') : '';
  console.log(
    `  z ${String(z).padStart(5)}  ${here.toFixed(1).padStart(6)}  ${ahead.toFixed(1).padStart(7)}  ${verdict}   z ${
      zPlaced.toFixed(1)
    }${note}`
  );
}
