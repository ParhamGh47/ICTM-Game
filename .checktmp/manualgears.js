// What the manual gearbox actually does, integrated from the same numbers the truck ships with.
//
// Every wheel applies its own force (four wheels at engineForce each) and its own drag, and the truck's
// mass is the prefab's, so the model below is the drive the physics really gets - not a re-derivation from a
// spec sheet. Automatic is included as the reference the manual has to be judged against.
const fs = require('fs');

const PREFAB = 'Assets/Prefabs/Utils/Truck (Player).prefab';
const text = fs.readFileSync(PREFAB, 'utf8');

function field(name) {
  const m = text.match(new RegExp('  ' + name + ': ([^\\r\\n]*)'));
  return m ? m[1].trim() : null;
}

const mass = parseFloat(field('m_Mass'));
const engineForce = parseFloat(field('engineForce'));      // per wheel
const dragK = 1.2;                                          // per wheel
const wheels = 4;

const idleRPM = 850, redlineRPM = 6500, blip = 900;
const finalDrive = 3.7;
const ratios = [3.8, 2.2, 1.5, 1.15, 0.92];
const wheelCircumference = 2 * Math.PI * 0.33;
const topSpeed = 120 / 3.6;                                 // the car's own topSpeed, m/s

// The torque curve as authored: 0.5 at a standstill, its peak at 35% of top speed, zero at top speed.
function curve(t) {
  t = Math.max(0, Math.min(1, t));
  if (t <= 0.35) return 0.5 + (t / 0.35) * 0.5;
  return 1 - (t - 0.35) / (1 - 0.35);
}

const lowGearPunch = 0.45, luggingTorque = 0.55, luggingFullRPM = 2800, revLimitStart = 0.96;

const gearScale = ratios.map(r => 1 + lowGearPunch * (r - ratios[ratios.length - 1]) / (ratios[0] - ratios[ratios.length - 1]));

function wheelRPM(v) { return (v / wheelCircumference) * 60; }

// Automatic: the box's own gear choice is a step function of speed (its downshift thresholds), and the
// drive is the same in every gear, which is what makes the reference simple.
function autoDrive(v, throttle) {
  const torque = engineForce * wheels * curve(v / topSpeed);
  return torque;
}

function manualDrive(v, gearIndex, throttle) {
  const ratio = ratios[Math.max(0, gearIndex)];
  const crank = Math.max(idleRPM, wheelRPM(v) * ratio * finalDrive);
  const engineRPM = crank + throttle * blip;
  const onCam = Math.max(0, Math.min(1, (crank - idleRPM) / (luggingFullRPM - idleRPM)));
  const lug = luggingTorque + (1 - luggingTorque) * onCam;
  const limiter = 1 - Math.max(0, Math.min(1, (engineRPM - redlineRPM * revLimitStart) / (redlineRPM - redlineRPM * revLimitStart)));
  const drive = engineForce * wheels * curve(v / topSpeed) * gearScale[Math.max(0, gearIndex)] * lug * limiter;
  return drive;
}

// A held gear, from a standstill: how fast it goes and how long it takes to get there.
function run(gearIndex, drive) {
  const dt = 0.01;
  let v = 0, t = 0;
  const marks = [];
  while (t < 60) {
    const a = (drive(v, gearIndex) - dragK * wheels * v * v) / mass;
    v += a * dt;
    t += dt;
    for (const kph of [20, 40, 60, 80, 100]) {
      if (!marks[kph] && v >= kph / 3.6) marks[kph] = t;
    }
    if (v <= 0.01 && t > 0.5) break;
    if (t > 3 && Math.abs(a) < 0.002) break;
  }
  return { v, t, marks };
}

console.log(`mass ${mass}, engineForce ${engineForce}/wheel x ${wheels} = ${engineForce * wheels} N`);
console.log(`gear scales: ${gearScale.map(g => g.toFixed(2)).join(', ')}\n`);

const auto = run(0, (v, g) => autoDrive(v, 1));
console.log(`AUTOMATIC: tops out around ${(auto.v * 3.6).toFixed(1)} km/h   ` +
  [20, 40, 60, 80, 100].filter(k => auto.marks[k]).map(k => `${k} in ${auto.marks[k].toFixed(1)}s`).join(', '));

console.log('\nMANUAL, each gear held to its own limit:');
for (let g = 0; g < ratios.length; g++) {
  const r = run(g, (v) => manualDrive(v, g, 1));
  const marks = [20, 40, 60, 80, 100].filter(k => r.marks[k]).map(k => `${k} in ${r.marks[k].toFixed(1)}s`);
  console.log(`  gear ${g + 1} (${ratios[g]}): ${(r.v * 3.6).toFixed(1)} km/h   ${marks.join(', ')}`);
}

console.log('\nMANUAL, pull at a given speed (the same speed, different gears):');
for (const kph of [10, 30, 60, 90]) {
  const v = kph / 3.6;
  const pulls = ratios.map((_, g) => manualDrive(v, g, 1));
  const autoPull = autoDrive(v, 1);
  console.log(`  at ${kph} km/h: ` + ratios.map((_, g) => `${g + 1}: ${(pulls[g] / 1000).toFixed(1)}kN`).join('  ') +
    `   (automatic ${(autoPull / 1000).toFixed(1)}kN)`);
}

console.log('\nREVERSE (first gear\'s scale, its own 20 km/h ceiling):');
{
  const reverseForce = parseFloat(field('reverseForce')) * wheels;
  const cap = 20 / 3.6;
  let v = 0, t = 0;
  const dt = 0.01;
  while (t < 30) {
    const a = (reverseForce * curve(v / cap) * gearScale[0] - dragK * wheels * v * v * 0) / mass;
    v += a * dt; t += dt;
    if (Math.abs(a) < 0.002 && t > 1) break;
  }
  console.log(`  reverse force ${reverseForce} N, reaches ${(v * 3.6).toFixed(1)} km/h in ${t.toFixed(1)}s`);
}
