// The truck, left alone with no throttle, on a slope.
//
// What the wheels do today with no input: one force, rolling resistance, 300 N per wheel per m/s of forward
// speed. A grade, though, pushes with a CONSTANT force - m g sin(theta) - so the two meet at whatever speed
// balances them, and the truck creeps down the slope at that speed rather than stopping. That is the bug:
// there is no speed at which it settles.
//
// The hold adds a stiffness in the last idleHoldSpeed of motion, capped so it settles the truck instead of
// stopping it like a wall.
const MASS = 2000;      // Truck (Player) prefab, m_Mass
const WHEELS = 4;
const G = 9.81;

const ROLLING_PER_WHEEL = 300;     // N per m/s, as the wheels have it
const HOLD_SPEED = 1.5;            // m/s
const HOLD_STIFFNESS = 15000;      // N per m/s per wheel
const HOLD_MAX = 2000;             // N per wheel, as shipped

const gradeForce = (grade) => MASS * G * Math.sin(Math.atan(grade / 100));
const holdAt = (v) => Math.min(Math.abs(v) * HOLD_STIFFNESS, HOLD_MAX) * WHEELS;
const rollingAt = (v) => Math.abs(v) * ROLLING_PER_WHEEL * WHEELS;

// The speed a slope settles the truck at: balance the push against what resists it.
function creep(g, withHold) {
  const push = gradeForce(g);
  for (let v = 0.0005; v < 5; v += 0.0005) {
    const resist = rollingAt(v) + (withHold ? holdAt(v) : 0);
    if (resist >= push) return v;
  }
  return NaN;
}

console.log('grade     creep today        creep with the hold');
for (const g of [5, 10, 15, 20, 25, 30, 40]) {
  const now = creep(g, false);
  const fixed = creep(g, true);
  const kmh = (v) => (v * 3.6).toFixed(2) + ' km/h';
  console.log(
    String(g + '%').padEnd(8) +
      ('backwards at ' + kmh(now)).padEnd(30) +
      (fixed === undefined || Number.isNaN(fixed) ? '(never settles)' : kmh(fixed) + '  (' + fixed.toFixed(3) + ' m/s)')
  );
}

console.log('\nwhat entering the hold costs: coming in at the gate speed with no throttle');
for (const v of [0.5, 1.0, 1.5]) {
  const force = rollingAt(v) + holdAt(v);
  const decel = force / MASS;
  console.log(
    '  at ' + v.toFixed(1) + ' m/s: ' + force.toFixed(0) + ' N -> ' + decel.toFixed(2) + ' m/s²  (' +
      (v / decel).toFixed(2) + ' s, ' + ((v * v) / (2 * decel) * 100).toFixed(0) + ' cm)'
  );
  void HOLD_SPEED;
}

console.log('\nfor scale: the brakes are ' + ((5000 * WHEELS) / MASS).toFixed(1) + ' m/s², the engine at full th' +
  'rottle is ' + ((5000 * WHEELS) / MASS).toFixed(1) + ' m/s²');
