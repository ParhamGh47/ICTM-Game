// Builds .checktmp/tutorialpreview.html: the card laid out on its grid, with the stage's own truck, targets and
// road projected from the same numbers and the same maths TutorialStage3D uses. The models are primitives on
// both sides, so this is a fair picture of what the card will show rather than a sketch of it.
//
// Usage: node .checktmp/stage-mock.js
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const target = path.join(root, '.checktmp/tutorialpreview.html');

// The project's two faces, lifted out of the previous mock so the page is written in the real hands.
const previous = fs.readFileSync(target, 'utf8');
const fonts = [...previous.matchAll(/@font-face \{[^}]*\}/g)].map((m) => m[0]);
console.log('reused', fonts.length, 'font-face rules from the previous mock');

const page = `<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>Tutorial card - the grid and the stage</title>
<style>
  ${fonts.join('\n  ')}
  html, body { margin: 0; background: #0b0d12; color: #cfd8e3; font: 14px/1.5 "Segoe UI", system-ui, sans-serif; }
  .wrap { padding: 20px; }
  h1 { font-size: 16px; font-weight: 600; letter-spacing: .06em; text-transform: uppercase; color: #7bcbe4; margin: 0 0 6px; }
  p.note { max-width: 940px; color: #8fa0b4; margin: 0 0 16px; }
  .bar { display: flex; gap: 14px; align-items: center; margin-bottom: 14px; flex-wrap: wrap; }
  .bar button, .bar select { background: #1c2330; color: #cfd8e3; border: 1px solid #2e3947; border-radius: 6px; padding: 6px 12px; }
  .bar input[type=range] { width: 320px; }
  .viewport { position: relative; overflow: auto; border: 1px solid #222b38; border-radius: 8px; background: #0b0d12; }
  .card { position: absolute; left: 0; top: 0; width: 1250px; height: 670px; transform-origin: top left; }

  /* the card, laid out on the grid in TutorialIntro.Layout */
  .box { position: absolute; box-sizing: border-box; }
  .lbl { position: absolute; box-sizing: border-box; white-space: nowrap; display: flex; align-items: center; justify-content: center; }
  .card-face { font-family: 'Saad', "Segoe UI", system-ui, sans-serif; font-weight: 700; }
  .hud-face { font-family: 'Potk', "Segoe UI", system-ui, sans-serif; font-weight: 700; }
  .hud-shadow { text-shadow: 4px -3px 0 rgba(255,255,255,.55); }
  canvas { position: absolute; left: 0; top: 0; }
</style>
</head>
<body>
<div class="wrap">
  <h1>Tutorial card &mdash; the grid and the stage</h1>
  <p class="note">
    The card's layout and the stage's camera are the ones in the code: the columns are the widths in
    <code>TutorialIntro.Layout</code>, and the truck, the targets and the road are the primitives
    <code>TutorialStage3D</code> builds, projected through the same camera the card films them with. The fonts are
    the project's own. The counter, the burst and the +1 are the card's own elements; the rest of the picture is
    the stage.
  </p>
  <div class="bar">
    <button id="play">pause</button>
    <input id="scrub" type="range" min="0" max="1" step="0.001" value="0">
    <span>zoom</span>
    <select id="zoom">
      <option value="fit" selected>fit</option>
      <option value="0.5">50%</option>
      <option value="0.75">75%</option>
      <option value="1">100%</option>
    </select>
    <span id="scale-note" style="color:#8fa0b4"></span>
  </div>
  <div class="viewport" id="viewport"><div class="card" id="card"></div></div>
</div>

<script>
// ------------------------------------------------------------------ the card
const CARD = { w: 1240, h: 660 };
const COLOUR = {
  backdrop: 'rgba(5,8,15,0.72)',
  paper: '#7bcbe4',
  ink: '#14698a',
  accent: '#ea3239',
  road: '#2f3440',
  line: '#f0f7fb',
};

// TutorialIntro.Layout, to the digit.
const L = {
  edge: 40, gap: 40, innerHalf: 602,
  pictureWidth: 764, noteWidth: 320,
  stageHeight: 360, middleRow: 10, titleRow: 252, captionRow: -200, controlsRow: -272,
  get pictureCentre() { return -this.innerHalf + this.edge + this.pictureWidth / 2; },
  get noteCentre() { return this.innerHalf - this.edge - this.noteWidth / 2; },
};

const card = document.getElementById('card');

// Place a box by its CENTRE, in the coordinate space of whatever holds it: the card's centre for the card
// itself, and the centre of its own parent for anything inside one - which is how Unity's RectTransforms work,
// and why the counter note can sit outside the picture column it is parented to.
function add(cls, parent, x, y, w, h, style) {
  const el = document.createElement('div');
  el.className = cls;

  const host = parent || card;
  const hostW = host === card ? CARD.w : parseFloat(host.style.width);
  const hostH = host === card ? CARD.h : parseFloat(host.style.height);

  el.style.left = (hostW / 2 + x - w / 2) + 'px';
  el.style.top = (hostH / 2 - y - h / 2) + 'px';
  el.style.width = w + 'px';
  el.style.height = h + 'px';
  Object.assign(el.style, style || {});

  host.appendChild(el);
  return el;
}

// the card's own frame, paper and rule (the frame is the card's whole box, as TutorialIntro builds it)
add('box', null, 0, 0, CARD.w, CARD.h, { background: COLOUR.ink });
add('box', null, 0, 0, CARD.w - 10, CARD.h - 10, { background: COLOUR.paper });
add('box', null, 0, 0, 1214, 634, { border: '3px solid rgba(20,105,138,.35)', boxSizing: 'border-box' });

const title = add('lbl', null, 0, L.titleRow, CARD.w - 120, 84);
title.classList.add('card-face');
title.textContent = 'TARGETS & KILLS';
title.style.color = COLOUR.ink;
title.style.fontSize = '64px';

// the stage's picture: a canvas exactly the picture column's size, inside the picture column
const stage = add('box', null, L.pictureCentre, L.middleRow, L.pictureWidth, L.stageHeight);
const canvas = document.createElement('canvas');
canvas.width = L.pictureWidth * 2;
canvas.height = L.stageHeight * 2;
canvas.style.width = L.pictureWidth + 'px';
canvas.style.height = L.stageHeight + 'px';
stage.appendChild(canvas);
const ctx = canvas.getContext('2d');
const SCALE = 2;   // drawn at twice the size and shown at one, so the edges are clean

const caption = add('lbl', null, L.pictureCentre, L.captionRow, L.pictureWidth, 52);
caption.classList.add('card-face');
caption.style.color = COLOUR.ink;
caption.style.fontSize = '34px';

// the counter note: a child of the stage, on the count column, as the code parents it
const note = add('box', stage, L.noteCentre - L.pictureCentre, 0, L.noteWidth, L.stageHeight,
  { background: COLOUR.ink, boxSizing: 'border-box' });
add('box', note, 0, 0, L.noteWidth - 12, L.stageHeight - 12, { background: COLOUR.paper });

const noteLabel = add('lbl', note, 0, 118, 300, 44);
noteLabel.classList.add('hud-face');
noteLabel.textContent = 'TARGET';
noteLabel.style.color = COLOUR.ink;
noteLabel.style.fontSize = '30px';

const noteWanted = add('lbl', note, 0, 58, 300, 56);
noteWanted.classList.add('hud-face');
noteWanted.textContent = '10';
noteWanted.style.color = COLOUR.accent;
noteWanted.style.fontSize = '44px';

const noteCount = add('lbl', note, 0, -62, 300, 140);
noteCount.classList.add('hud-face', 'hud-shadow');
noteCount.textContent = '3';
noteCount.style.color = COLOUR.ink;
noteCount.style.fontSize = '120px';

const plusOne = add('lbl', stage, L.noteCentre - L.pictureCentre, L.stageHeight / 2 + 45, 220, 70);
plusOne.classList.add('card-face');
plusOne.textContent = '+1';
plusOne.style.color = COLOUR.accent;
plusOne.style.fontSize = '56px';
plusOne.style.opacity = '0';

// the controls, on the columns' edges
const startX = -L.innerHalf + L.edge + 150;
const startRing = add('box', null, startX, L.controlsRow, 324, 92, { background: COLOUR.accent });
add('box', startRing, 0, 0, 300, 76, { background: COLOUR.ink });
const startLabel = add('lbl', startRing, 0, 0, 300, 76);
startLabel.classList.add('card-face');
startLabel.textContent = 'START';
startLabel.style.color = COLOUR.paper;
startLabel.style.fontSize = '38px';

const hideX = L.innerHalf - L.edge - 280;
const hideRow = add('box', null, hideX, L.controlsRow, 560, 76, { background: COLOUR.paper, border: '3px solid ' + COLOUR.ink });
add('box', hideRow, -238, 0, 48, 48, { background: COLOUR.paper, border: '3px solid ' + COLOUR.ink, boxSizing: 'border-box' });
const hideLabel = add('lbl', hideRow, 52, 0, 430, 60);
hideLabel.classList.add('card-face');
hideLabel.textContent = "DON'T SHOW THIS AGAIN";
hideLabel.style.color = COLOUR.ink;
hideLabel.style.fontSize = '30px';
hideLabel.style.justifyContent = 'flex-start';

// ------------------------------------------------------------------ the stage's maths (TutorialStage3D)
const V = {
  add: (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]],
  sub: (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]],
  mul: (a, s) => [a[0] * s, a[1] * s, a[2] * s],
  dot: (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2],
  cross: (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]],
  norm: (a) => V.mul(a, 1 / Math.hypot(a[0], a[1], a[2])),
};

const RES = [L.pictureWidth, L.stageHeight], FOV = 34;
const TARGET_COUNT = 3;

// ---- the models, exactly as BuildTruckModel / BuildTargetModel make them
const TRUCK_MODEL = [
  { kind: 'box', pos: [0, 1.05, -0.35], size: [2.3, 1.05, 3.9], colour: '#176066' },   // body
  { kind: 'box', pos: [0, 1.8, 1.5], size: [2.2, 0.95, 1.4], colour: '#176066' },        // cab
  { kind: 'box', pos: [0, 2.32, 1.5], size: [2.1, 0.12, 1.3], colour: '#0f424a' },       // roof
  { kind: 'box', pos: [0, 1.95, 2.19], size: [1.9, 0.6, 0.06], colour: '#9ed1e6' },      // windscreen
  { kind: 'box', pos: [-0.78, 1.25, 2.22], size: [0.42, 0.24, 0.1], colour: '#ffeeB8' },
  { kind: 'box', pos: [0.78, 1.25, 2.22], size: [0.42, 0.24, 0.1], colour: '#ffeeB8' },
  { kind: 'drum', pos: [0, 2.6, 1.5], diameter: 0.34, height: 0.5, colour: '#c79957' },  // cone
  { kind: 'ball', pos: [0, 2.98, 1.5], diameter: 0.7, colour: COLOUR.paper },            // scoop
  { kind: 'wheel', pos: [-1.16, 0.55, 1.45], radius: 0.55, width: 0.36, colour: '#141419' },
  { kind: 'wheel', pos: [1.16, 0.55, 1.45], radius: 0.55, width: 0.36, colour: '#141419' },
  { kind: 'wheel', pos: [-1.16, 0.55, -1.5], radius: 0.55, width: 0.36, colour: '#141419' },
  { kind: 'wheel', pos: [1.16, 0.55, -1.5], radius: 0.55, width: 0.36, colour: '#141419' },
];

const TARGET_MODEL = [
  { kind: 'box', pos: [0, 0.3, 0], size: [0.5, 0.6, 0.34], colour: '#575c6b' },
  { kind: 'box', pos: [0, 0.9, 0], size: [0.62, 0.6, 0.4], colour: COLOUR.paper },
  { kind: 'ball', pos: [0, 1.38, 0], diameter: 0.44, colour: '#efdcc2' },
  { kind: 'drum', pos: [0, 1.66, 0], diameter: 0.64, height: 0.1, colour: COLOUR.accent },
  { kind: 'drum', pos: [0, 1.78, 0], diameter: 0.42, height: 0.2, colour: COLOUR.accent },
];

// The truck's drawn size, measured from the model the same way DrawnBounds measures it.
function modelBounds(model) {
  const lo = [Infinity, Infinity, Infinity], hi = [-Infinity, -Infinity, -Infinity];
  for (const s of model) {
    const ext = s.kind === 'box' ? V.mul(s.size, 0.5)
      : s.kind === 'wheel' ? [s.width / 2, s.radius, s.radius]
        : [s.diameter / 2, (s.kind === 'drum' ? s.height : s.diameter) / 2, s.diameter / 2];
    for (let a = 0; a < 3; a++) {
      lo[a] = Math.min(lo[a], s.pos[a] - ext[a]);
      hi[a] = Math.max(hi[a], s.pos[a] + ext[a]);
    }
  }
  return { size: V.sub(hi, lo), low: lo[1] };
}

const TRUCK_BOUNDS = modelBounds(TRUCK_MODEL);
const LENGTH = TRUCK_BOUNDS.size[2], WIDTH = TRUCK_BOUNDS.size[0];
const RADIUS = Math.max(WIDTH * 0.95, LENGTH * 0.24);
const TRUCK_START_Z = -LENGTH * 0.45;
const TARGET_Z = LENGTH * 0.38;
const HIT_Z = TARGET_Z - LENGTH * 0.55;
const SPACING = Math.max(LENGTH * 0.3, 1);
const STRIP_HEIGHT = LENGTH * 0.56;
const ROAD_LENGTH = (TARGET_Z + (TARGET_COUNT - 1) * SPACING - TRUCK_START_Z) + LENGTH * 1.6;
const FAR_TARGET_Z = TARGET_Z + (TARGET_COUNT - 1) * SPACING;

// ---- the camera, framed by FrameCamera
const VIEW = V.norm([-0.86, -0.34, 0.2]);
const CENTRE = [0, STRIP_HEIGHT * 0.3, (TRUCK_START_Z + FAR_TARGET_Z) * 0.5];
const BOX = [RADIUS * 2 + WIDTH * 0.8, STRIP_HEIGHT * 1.25, Math.abs(FAR_TARGET_Z - TRUCK_START_Z) + LENGTH * 0.45];
const half = V.mul(BOX, 0.5);
const RIGHT = V.norm(V.cross([0, 1, 0], VIEW));
const UP = V.norm(V.cross(VIEW, RIGHT));
const tanV = Math.tan(FOV * 0.5 * Math.PI / 180);
const tanH = tanV * (RES[0] / RES[1]);
let DISTANCE = 6;
for (let c = 0; c < 8; c++) {
  const o = [(c & 1) ? half[0] : -half[0], (c & 2) ? half[1] : -half[1], (c & 4) ? half[2] : -half[2]];
  const d = V.dot(VIEW, o);
  DISTANCE = Math.max(DISTANCE,
    Math.abs(V.dot(RIGHT, o)) / tanH - d, Math.abs(V.dot(UP, o)) / tanV - d);
}
DISTANCE *= 1.06;

const EYE = V.sub(CENTRE, V.mul(VIEW, DISTANCE));
const AIM = V.add(CENTRE, [0, 0.2, 0]);
const FORWARD = V.norm(V.sub(AIM, EYE));
const CAM_RIGHT = V.norm(V.cross([0, 1, 0], FORWARD));
const CAM_UP = V.cross(FORWARD, CAM_RIGHT);

function project(p) {
  const rel = V.sub(p, EYE);
  const z = V.dot(rel, FORWARD);
  return [
    (0.5 + (V.dot(rel, CAM_RIGHT) / z) / (2 * tanH)) * RES[0],
    (0.5 - (V.dot(rel, CAM_UP) / z) / (2 * tanV)) * RES[1],
    z,
  ];
}

function depth(p) { return V.dot(V.sub(p, EYE), FORWARD); }

// A shape in world space, turned by yaw about y and moved to the truck's place.
function placeShape(shape, origin, yaw) {
  const c = Math.cos(yaw), s = Math.sin(yaw);
  const pos = [
    origin[0] + shape.pos[0] * c + shape.pos[2] * s,
    origin[1] + shape.pos[1],
    origin[2] + -shape.pos[0] * s + shape.pos[2] * c,
  ];
  return { ...shape, world: pos, yaw };
}

function worldPoint(shape, local) {
  const c = Math.cos(shape.yaw), s = Math.sin(shape.yaw);
  return [
    shape.world[0] + local[0] * c + local[2] * s,
    shape.world[1] + local[1],
    shape.world[2] + -local[0] * s + local[2] * c,
  ];
}

function drawBox(shape) {
  const ext = V.mul(shape.size, 0.5);
  const pts = [];
  for (let c = 0; c < 8; c++) {
    const local = [(c & 1) ? ext[0] : -ext[0], (c & 2) ? ext[1] : -ext[1], (c & 4) ? ext[2] : -ext[2]];
    const p = project(worldPoint(shape, local));
    pts.push([p[0], p[1]]);
  }
  // convex hull
  pts.sort((a, b) => a[0] - b[0] || a[1] - b[1]);
  const cross = (o, a, b) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
  const lower = [], upper = [];
  for (const p of pts) {
    while (lower.length >= 2 && cross(lower[lower.length - 2], lower[lower.length - 1], p) <= 0) lower.pop();
    lower.push(p);
  }
  for (let i = pts.length - 1; i >= 0; i--) {
    const p = pts[i];
    while (upper.length >= 2 && cross(upper[upper.length - 2], upper[upper.length - 1], p) <= 0) upper.pop();
    upper.push(p);
  }
  const hull = lower.concat(upper.slice(1, -1));
  ctx.beginPath();
  ctx.moveTo(hull[0][0] * SCALE, hull[0][1] * SCALE);
  for (let i = 1; i < hull.length; i++) ctx.lineTo(hull[i][0] * SCALE, hull[i][1] * SCALE);
  ctx.closePath();
  ctx.fillStyle = shape.colour;
  ctx.fill();
}

function drawRound(shape) {
  const centre = project(shape.world);
  const radius = shape.kind === 'wheel' ? shape.radius : shape.diameter / 2;
  const a = project(V.add(shape.world, V.mul(CAM_RIGHT, radius)));
  const b = project(V.add(shape.world, V.mul(CAM_UP, radius)));
  const rx = Math.abs(a[0] - centre[0]) * SCALE;
  const ry = Math.abs(b[1] - centre[1]) * SCALE;
  ctx.beginPath();
  ctx.ellipse(centre[0] * SCALE, centre[1] * SCALE, Math.max(0.6, rx), Math.max(0.6, ry), 0, 0, Math.PI * 2);
  ctx.fillStyle = shape.colour;
  ctx.fill();
}

function drawShapes(shapes) {
  shapes.sort((a, b) => depth(b.world) - depth(a.world));
  for (const s of shapes) (s.kind === 'box' ? drawBox : drawRound)(s);
}

// ------------------------------------------------------------------ the loop
const CYCLE = 5.2, HIT_PHASE = 0.5;
let time = 0, playing = true, last = performance.now();
const clamp01 = (v) => Math.max(0, Math.min(1, v));
const lerp = (a, b, t) => a + (b - a) * t;

function road() {
  const road = { kind: 'box', pos: [0, -0.15, 0], size: [RADIUS * 2, 0.3, ROAD_LENGTH], colour: COLOUR.road };
  const paint = Math.max(0.06, ROAD_LENGTH * 0.012);
  const slabs = [road,
    { kind: 'box', pos: [-RADIUS + paint * 2, 0.01, 0], size: [paint, 0.04, ROAD_LENGTH - paint * 4], colour: COLOUR.line },
    { kind: 'box', pos: [RADIUS - paint * 2, 0.01, 0], size: [paint, 0.04, ROAD_LENGTH - paint * 4], colour: COLOUR.line }];
  const dash = ROAD_LENGTH * 0.05;
  for (let z = -ROAD_LENGTH / 2 + dash * 1.5; z < ROAD_LENGTH / 2 - dash * 1.5; z += dash * 2.4)
    slabs.push({ kind: 'box', pos: [0, 0.01, z], size: [paint * 0.9, 0.04, dash], colour: COLOUR.line });
  return slabs;
}

function animate(t) {
  const hit = t >= HIT_PHASE;
  const sinceHit = hit ? t - HIT_PHASE : 0;

  ctx.clearRect(0, 0, canvas.width, canvas.height);

  // the road
  drawShapes(road().map((s) => placeShape(s, [0, 0, 0], 0)));

  // the truck: up the road, working across its lane, settling onto the target
  const travel = clamp01(t / HIT_PHASE);
  const eased = travel * travel * (3 - 2 * travel);
  const z = lerp(TRUCK_START_Z, HIT_Z, eased);
  const sway = Math.sin(travel * Math.PI * 2.6) * WIDTH * 0.22 * (1 - travel);
  const yaw = sway / LENGTH * -45 * Math.PI / 180;
  const origin = [sway, -TRUCK_BOUNDS.low, z];
  drawShapes(TRUCK_MODEL.map((s) => placeShape(s, origin, yaw)));

  // the targets, and the first one knocked off its feet
  for (let i = 0; i < TARGET_COUNT; i++) {
    const home = [Math.max(-RADIUS * 0.6, Math.min(RADIUS * 0.6, Math.sin(i * 2.4) * RADIUS * 0.45)),
      -modelBounds(TARGET_MODEL).low, TARGET_Z + i * SPACING];
    const facing = (180 + Math.sin(i * 1.9) * 40) * Math.PI / 180;

    let origin = home, yaw = facing;
    if (i === 0 && hit) {
      const life = clamp01(sinceHit / 0.55);
      const toss = LENGTH * 0.42;
      origin = [home[0] + toss * 0.75 * life,
        home[1] + toss * 1.1 * life - toss * 1.45 * life * life,
        home[2] + toss * 0.55 * life];
      yaw = facing + (220 * life) * Math.PI / 180;
    }

    drawShapes(TARGET_MODEL.map((s) => placeShape(s, origin, yaw)));
  }

  // the counter note above the picture would cover it, so it is drawn over the canvas by the DOM, not here
  noteCount.textContent = hit ? '4' : '3';
  const pop = hit ? clamp01(sinceHit / 0.4) : 0;
  noteCount.style.transform = 'scale(' + (hit ? lerp(1.3, 1, pop) : 1) + ')';
  const mix = hit ? clamp01(sinceHit / 0.45) : 1;
  noteCount.style.color = mix < 1 ? mixColour(COLOUR.accent, COLOUR.ink, mix) : COLOUR.ink;

  const plusLife = hit ? clamp01(sinceHit / 0.45) : 0;
  plusOne.style.opacity = hit ? 1 - plusLife : 0;
  plusOne.style.transform = 'translateY(' + (-50 * plusLife) + 'px)';   // it lifts off the note as it fades

  const shouting = hit && sinceHit < 0.32;
  caption.textContent = shouting ? '+1 KILL' : 'SMASH THEM ON THE ROAD';
  caption.style.color = shouting ? COLOUR.accent : COLOUR.ink;
}

function mixColour(a, b, t) {
  const pa = [parseInt(a.slice(1, 3), 16), parseInt(a.slice(3, 5), 16), parseInt(a.slice(5, 7), 16)];
  const pb = [parseInt(b.slice(1, 3), 16), parseInt(b.slice(3, 5), 16), parseInt(b.slice(5, 7), 16)];
  return 'rgb(' + pa.map((v, i) => Math.round(lerp(v, pb[i], t))).join(',') + ')';
}

// ------------------------------------------------------------------ page controls
function fit() {
  const choice = document.getElementById('zoom').value;
  const viewport = document.getElementById('viewport');
  // Fit to whichever of the two is tighter, never below a size the card can still be read at - the pane the
  // preview is shown in can be short enough for a straight ratio to come out negative.
  const scale = choice === 'fit'
    ? Math.max(0.16, Math.min((window.innerWidth - 70) / 1250, (window.innerHeight - 250) / 670, 1))
    : parseFloat(choice);
  card.style.transform = 'scale(' + scale + ')';
  viewport.style.width = Math.min(window.innerWidth - 62, 1250 * scale) + 'px';
  viewport.style.height = Math.max(200, 670 * scale) + 'px';
  document.getElementById('scale-note').textContent = Math.round(scale * 100) + '%';
}

document.getElementById('play').addEventListener('click', (e) => {
  playing = !playing;
  e.target.textContent = playing ? 'pause' : 'play';
});
document.getElementById('scrub').addEventListener('input', (e) => {
  playing = false;
  document.getElementById('play').textContent = 'play';
  time = parseFloat(e.target.value) * CYCLE;
});
document.getElementById('zoom').addEventListener('change', fit);
window.addEventListener('resize', fit);

function frameLoop(now) {
  const delta = (now - last) / 1000;
  last = now;
  if (playing) time += delta;
  const t = ((time / CYCLE) % 1 + 1) % 1;
  document.getElementById('scrub').value = t;
  animate(t);
  requestAnimationFrame(frameLoop);
}

console.log('stage: truck', LENGTH.toFixed(2), 'long, camera', DISTANCE.toFixed(1), 'away');
fit();
animate(0);
requestAnimationFrame(frameLoop);
</script>
</body>
</html>
`;

fs.writeFileSync(target, page);
console.log('wrote', target, page.length, 'chars');
