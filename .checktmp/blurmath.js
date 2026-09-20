// The shader's own maths, run at 1920x1080, to see what the streak actually is per speed and per place on
// the screen. Corners, mid-side (the verges), top middle (sky), bottom middle (the road under the truck).
const W = 1920, H = 1080, aspect = W / H;

function streakAt(uv, o, intensity) {
  const dx = (uv[0] - o.focus[0]) * aspect, dy = uv[1] - o.focus[1];
  const r = Math.hypot(dx, dy);
  const edge = Math.pow(Math.max(0, Math.min(1, (r - o.clear) / (1 - o.clear))), o.falloff);
  const sideways = r > 1e-5 ? Math.abs(dx) / r : 0;
  const side = 1 + (o.sideBoost - 1) * sideways;
  const amount = o.maxBlur * intensity * Math.max(0, o.pull) * edge * side;
  const dirx = (dx / r) / aspect, diry = dy / r;
  // length of the streak in pixels
  return Math.hypot(dirx * amount * W, diry * amount * H);
}

const o = { focus: [0.5, 0.52], clear: 0.2, falloff: 1.2, sideBoost: 1.5, maxBlur: 0.04, pull: 1, start: 0.18 };
const places = { 'corner': [0, 0], 'mid-side (verge)': [1, 0.52], 'top middle (sky)': [0.5, 1], 'bottom middle (road)': [0.5, 0], 'just off centre': [0.62, 0.5] };

function intensityFor(kph) {
  const speed = Math.min(1, kph / 120);
  let w = Math.max(0, Math.min(1, (speed - o.start) / (1 - o.start)));
  return w * w * (3 - 2 * w);
}

for (const kph of [30, 50, 70, 90, 110, 120]) {
  const i = intensityFor(kph);
  const row = Object.entries(places)
    .map(([n, uv]) => n + ' ' + streakAt(uv, o, i).toFixed(1) + 'px')
    .join('   ');
  console.log((kph + ' km/h  blue=' + i.toFixed(2)).padEnd(22) + row);
}
