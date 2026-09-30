// Does lowering Max Objects still cover the whole route?
//
// Mirrors BuildPlan's arithmetic: the budget is split between the phases, and each phase is spread over the
// route instead of being walked from the start until the cap is hit.

function oldWalk(from, to, spacing, jitter, cap, seed) {
  let rnd = seed;
  const rand = () => ((rnd = (rnd * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);
  let distance = from;
  const spots = [];
  while (distance < to && spots.length < cap) {
    distance += Math.max(5, spacing * (1 - jitter + 2 * jitter * rand()));
    spots.push(distance);
  }
  return spots;
}

function spread(from, to, count, jitter, seed) {
  let rnd = seed;
  const rand = () => ((rnd = (rnd * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);
  const slot = (to - from) / count;
  const low = Math.max(0, Math.min(1, 0.5 - jitter * 0.5));
  const high = 1 - low;
  const spots = [];
  for (let i = 0; i < count; i++) spots.push(from + slot * (i + low + (high - low) * rand()));
  return spots;
}

function report(label, from, to, spacing, jitter, cap) {
  const old = oldWalk(from, to, spacing, jitter, cap, 7);
  const slots = Math.max(1, Math.floor((to - from) / Math.max(5, spacing)));
  const allowed = Math.min(cap, slots);
  const fresh = spread(from, to, allowed, jitter, 7);

  const reach = (spots) => (spots.length ? (spots[spots.length - 1] - from) / (to - from) : 0);
  const gap = (spots) => {
    let min = Infinity, max = 0;
    for (let i = 1; i < spots.length; i++) {
      const d = spots[i] - spots[i - 1];
      min = Math.min(min, d); max = Math.max(max, d);
    }
    return spots.length > 1 ? min.toFixed(0) + '-' + max.toFixed(0) : '-';
  };

  console.log(label.padEnd(28),
    'old: ' + old.length + ' signs reaching ' + (reach(old) * 100).toFixed(0) + '% of the route, gaps ' + gap(old),
    ' | new: ' + fresh.length + ' signs reaching ' + (reach(fresh) * 100).toFixed(0) + '%, gaps ' + gap(fresh));
}

const route = 3400;          // a Core level, roughly
const from = 20;             // routeEndTrim
const to = route - 20;

for (const cap of [10, 20, 30, 60, 120]) {
  report('45 m spacing, max ' + cap, from, to, 45, 0.5, cap);
}
for (const cap of [10, 30, 120]) {
  report('120 m spacing, max ' + cap, from, to, 120, 0.5, cap);
}

// The junction budget: with more junctions than the cap can sign, the stride has to spread them.
console.log('\njunction stride (30 junctions along the route):');
for (const cap of [6, 12, 20, 30]) {
  const junctions = 30;
  const signable = Math.min(junctions, cap);
  const picked = [];
  for (let k = 0; k < signable; k++) {
    picked.push(signable === junctions ? k : Math.floor(k * junctions / signable));
  }
  const at = picked.map((i) => ((i / (junctions - 1)) * 100).toFixed(0));
  console.log('  max ' + String(cap).padEnd(3) + ' -> ' + picked.length + ' junctions signed, at ' + at.join('%, ') + '%');
}
