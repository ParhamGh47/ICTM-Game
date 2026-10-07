// How far a shoved passing car is carried ACROSS the path it is being driven along.
//
// The drive sets the car's position every physics step, so any speed across its own line rides on top of
// that: the car is carried sideways as well as along, and the path it is following keeps pulling it the
// other way. A rigidbody only loses speed through its drag, which is 0.5 per second here. This works out
// that sideways distance for a few shove strengths, with drag alone and with the sideways bleed added
// this turn. (Only the sideways part is bled - speed along the car's own heading is left exactly as the
// drive sets it, so nothing here can change how fast a car is driven.)
const dt = 1 / 50;          // Unity's default fixed step
const drag = 0.5;           // AICarController sets rb.drag = 0.5
const bleed = 14;           // the new shoveResponse, in m/s²

function drift(shove, bled) {
  let v = shove;
  let distance = 0;
  let t = 0;
  while (v > 0.01 && t < 30) {
    distance += v * dt;
    v = v / (1 + drag * dt);                 // the rigidbody's own drag
    if (bled) v = Math.max(0, v - bleed * dt);
    t += dt;
  }
  return { distance, time: t };
}

console.log('sideways shove | carried sideways, drag only | with the bleed   | time to settle');
for (const shove of [2, 4, 6, 8, 12]) {
  const a = drift(shove, false);
  const b = drift(shove, true);
  console.log(
    `${String(shove).padStart(6)} m/s     | ${a.distance.toFixed(2).padStart(6)} m / ${a.time.toFixed(2)} s` +
    `          | ${b.distance.toFixed(2).padStart(5)} m / ${b.time.toFixed(2)} s`);
}

// And the spin: how long a wobble takes to come out, and whether a shove that is meant to roll a car still
// can. The tilt needed to take a car out of the drive is 55 degrees (uprightLimit).
const spinBleed = 60;       // degrees per second per second
console.log('');
console.log('spin (deg/s) | time to bleed off at 60 deg/s² | angle turned before it is gone');
for (const spin of [20, 60, 120, 240]) {
  const t = spin / spinBleed;
  const angle = spin * t / 2;
  console.log(`${String(spin).padStart(11)}  | ${t.toFixed(2)} s${' '.repeat(24)}| ${angle.toFixed(0)} deg`);
}
