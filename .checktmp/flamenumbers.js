// Read-only arithmetic on the shadow's fire, from the numbers actually authored on Core-4's Shadow Racer.
// The point is to check the two fires are the shapes I claim: one climbing, one a shorter bottom-heavy wake
// that really does go backwards, and that the wake is the smaller of the two.

const particleRadius = 2;      // scene
const sideSizeShare = 0.5;     // scene
const sideRateShare = 0.4;     // scene
const backBias = 1.8;          // scene
const particleRate = 90;       // scene
const particleMax = 150;       // scene
const parts = 6;               // WheelFL/FR/RL/RR/Engine/HeadLights

const flameSize = particleRadius * sideSizeShare;

function mid(a, b) { return (a + b) / 2; }

// The climbing fire.
const climbLife = mid(0.16, 0.36);
const climbY = mid(particleRadius * 1.2 * 0.7, particleRadius * 1.2 * 1.7);
const climbX = mid(-particleRadius * 0.8, particleRadius * 0.8);

// The wake.
const wakeLife = mid(0.16, 0.34);
const spread = flameSize * 1.6;
const back = particleRadius * backBias;
const wakeLo = -(back + spread * 0.5);
const wakeHi = -back * 0.25 + spread * 0.5;
const wakeY = mid(flameSize * 0.12, flameSize * 0.5);

console.log('one part, per fire:');
console.log('  climbing: life', climbLife.toFixed(2) + 's', ' climb', climbY.toFixed(2), 'm/s',
            '-> rises', (climbY * climbLife).toFixed(2), 'm', ' sideways', (climbX * climbLife).toFixed(2), 'm');
console.log('  wake    : life', wakeLife.toFixed(2) + 's', ' back ', wakeLo.toFixed(2), '..', wakeHi.toFixed(2), 'm/s',
            '-> back', (mid(wakeLo, wakeHi) * wakeLife).toFixed(2), 'm', ' sideways +/-', (spread * wakeLife).toFixed(2), 'm',
            ' up', (wakeY * wakeLife).toFixed(2), 'm');

const wakeBackShare = wakeHi < 0 ? 1 : null;
console.log('\n  every wake flame goes backwards:', wakeHi < 0, '(highest z is ' + wakeHi.toFixed(2) + ')');
console.log('  wake is shorter than the climb:', (mid(wakeLo, wakeHi) * wakeLife) < (climbY * climbLife));

const alive = (rate, life) => Math.round(rate * life * parts);
console.log('\nalive at once, whole truck (6 parts):');
console.log('  climbing:', alive(particleRate, climbLife), ' (each fire caps at',
            Math.max(6, Math.round(particleMax)) + ')');
console.log('  wake    :', alive(particleRate * sideRateShare, wakeLife), ' (each fire caps at',
            Math.max(6, Math.round(particleMax * sideRateShare)) + ')');
console.log('  wake is the smaller share:', (particleRate * sideRateShare) < particleRate);
