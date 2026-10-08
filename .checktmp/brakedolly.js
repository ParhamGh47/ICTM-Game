// Models the brake dolly exactly as CameraController now computes it, so the numbers can be quoted rather
// than guessed. Read-only - it touches no project file.
//
// Run: node .checktmp/brakedolly.js
const dollyIn = 0.85;        // metres at full pull
const inSpeed = 1.4;         // metres per second of distance
const outSpeed = 0.9;
const floorKPH = 15;
const fullKPH = 105;
const holdTime = 0.7;        // seconds held before it counts as pressed all the way

const ease = (x) => { x = Math.min(1, Math.max(0, x)); return x * x * (3 - 2 * x); };
const share = (kph) => Math.min(1, Math.max(0, (kph - floorKPH) / (fullKPH - floorKPH)));

// A held brake: hardness climbs to 1 over holdTime, taking whichever of pedal and hold is greater.
const held = (seconds) => Math.min(1, seconds / holdTime);
const target = (kph, pedal, seconds) => dollyIn * ease(Math.max(pedal, held(seconds))) * share(kph);
const arrive = (m) => Math.max(0, m) / inSpeed;

console.log('Full brake held (hardness 1), by entry speed:');
console.log('  entry km/h   share   pull (m)   time to arrive (s)');
for (const kph of [15, 30, 60, 80, 105]) {
  const m = target(kph, 1, holdTime);
  console.log('  ' + String(kph).padStart(8) + '   ' + share(kph).toFixed(2) +
              '    ' + m.toFixed(3).padStart(6) + '          ' + arrive(m).toFixed(2));
}

console.log('\nEntry speed 90 km/h, by how hard the brake is used:');
console.log('  pedal   held (s)   hardness   pull (m)');
for (const [pedal, secs] of [[0.25, 0], [0.25, 1.0], [0.5, 0], [0.5, 1.0], [1, 0]]) {
  const hard = Math.max(pedal, held(secs));
  const m = target(90, pedal, secs);
  console.log('  ' + pedal.toFixed(2) + '    ' + secs.toFixed(1) + '        ' + hard.toFixed(2) +
              '       ' + m.toFixed(3));
}

console.log('\nA full-brake stop from 110 km/h at 9 m/s^2 (32.4 km/h per second):');
console.log('  the pull is flat at ' + target(110, 1, holdTime).toFixed(3) +
            ' m from ' + arrive(target(110, 1, holdTime)).toFixed(2) + ' s, held for the whole stop,');
console.log('  then let go over ' + (target(110, 1, holdTime) / outSpeed).toFixed(2) + ' s once the brakes come off.');
