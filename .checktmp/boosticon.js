// Draws the boost icon: the milkshake the truck actually collects, in the HUD's own hand.
//
// The HUD has no artwork for this and the font cannot draw an emoji (Potk.ttf carries no U+1F680), so the
// icon has to be drawn. It is drawn as vector shapes and supersampled, because the thing that makes it look
// like a drawing rather than a downscaled photo is an even, unbroken outline - and a hand-rolled rasteriser
// at 4x with a box filter gives that with no dependency at all.
//
// Two things size the drawing. The colours are the pickup's own - pink shake, white foam, red cherry, green
// straw - mixed with the HUD's ink (#14698a) and paper blue (#7bcbe4), so it sits on the boost note as though
// stamped there. And every line is authored for a 50-pixel display: an outline much under 20 of these 512
// units does not survive the downscale, which is why this is a bold icon and not a detailed illustration.

const fs = require('fs');
const zlib = require('zlib');

const SS = 4;                     // supersample factor
const W = 512, H = 512;           // final size
const IW = W * SS, IH = H * SS;
const buf = new Float32Array(IW * IH * 4);   // premultiplied RGBA, 0..255 for rgb, 0..1 for a

// The drawing is authored around a nominal origin and shifted as a whole at the end, so the artwork can be
// centred in its square canvas by measuring where it actually landed rather than by guessing at coordinates.
const OX = 56, OY = 57;
const S = 1.15;                  // then sized about the canvas centre, so the icon fills the square it is given

const rgb = (hex) => [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)];

const INK = rgb('#14698a');
const PINK = rgb('#ff69c9');
const WHITE = rgb('#ffffff');
const CREAM = rgb('#fdf6e8');
const TINT = rgb('#eaf7fb');      // a whisper of paper blue in the glass, so it is not flat white
const RED = rgb('#ea3239');
const GREEN = rgb('#26b573');

// ------------------------------------------------------------------ rasterising

function over(x, y, c, a) {
  if (a <= 0 || x < 0 || y < 0 || x >= IW || y >= IH) return;
  const o = (y * IW + x) * 4;
  const ia = 1 - a;
  buf[o] = c[0] * a + buf[o] * ia;
  buf[o + 1] = c[1] * a + buf[o + 1] * ia;
  buf[o + 2] = c[2] * a + buf[o + 2] * ia;
  buf[o + 3] = a + buf[o + 3] * ia;
}

// Even-odd scanline fill. Fine for the simple polygons here.
function fillPoly(pts, c, a = 1) {
  const P = pts.map(([x, y]) => [
    ((x + OX - W / 2) * S + W / 2) * SS,
    ((y + OY - H / 2) * S + H / 2) * SS
  ]);
  let minY = Infinity, maxY = -Infinity;
  for (const p of P) { minY = Math.min(minY, p[1]); maxY = Math.max(maxY, p[1]); }
  minY = Math.max(0, Math.floor(minY));
  maxY = Math.min(IH - 1, Math.ceil(maxY));
  const xs = [];
  for (let y = minY; y <= maxY; y++) {
    const yc = y + 0.5;
    xs.length = 0;
    for (let i = 0; i < P.length; i++) {
      const [x1, y1] = P[i], [x2, y2] = P[(i + 1) % P.length];
      if ((y1 <= yc && y2 > yc) || (y2 <= yc && y1 > yc)) xs.push(x1 + ((yc - y1) * (x2 - x1)) / (y2 - y1));
    }
    xs.sort((p, q) => p - q);
    for (let i = 0; i + 1 < xs.length; i += 2) {
      const x0 = Math.max(0, Math.round(xs[i]));
      const x1 = Math.min(IW - 1, Math.round(xs[i + 1]) - 1);
      for (let x = x0; x <= x1; x++) over(x, y, c, a);
    }
  }
}

function circlePts(cx, cy, r, n = 128) {
  const out = [];
  for (let i = 0; i < n; i++) {
    const t = (i / n) * Math.PI * 2;
    out.push([cx + Math.cos(t) * r, cy + Math.sin(t) * r]);
  }
  return out;
}

const fillCircle = (cx, cy, r, c, a = 1) => fillPoly(circlePts(cx, cy, r), c, a);

function stroke(pts, width, c, a = 1, round = true) {
  for (let i = 0; i + 1 < pts.length; i++) {
    const [x1, y1] = pts[i], [x2, y2] = pts[i + 1];
    const dx = x2 - x1, dy = y2 - y1;
    const len = Math.hypot(dx, dy) || 1;
    const nx = (-dy / len) * (width / 2), ny = (dx / len) * (width / 2);
    fillPoly([[x1 + nx, y1 + ny], [x2 + nx, y2 + ny], [x2 - nx, y2 - ny], [x1 - nx, y1 - ny]], c, a);
  }
  if (round) for (const [x, y] of pts) fillCircle(x, y, width / 2, c, a);
}

// Speed lines want to thin out as they trail away, which is most of what makes them read as speed.
function strokeTapered(pts, w0, w1, c, a = 1) {
  for (let i = 0; i + 1 < pts.length; i++) {
    const t = i / (pts.length - 1);
    stroke([pts[i], pts[i + 1]], w0 + (w1 - w0) * t, c, a);
  }
}

const quad = (p0, p1, p2, n = 32) => {
  const out = [];
  for (let i = 0; i <= n; i++) {
    const t = i / n, u = 1 - t;
    out.push([u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]]);
  }
  return out;
};
// A closed blob: a curved top and a curved bottom, which is what a mound of cream is. The bottom curve is
// already drawn right-to-left (it starts where the top ends), so it is appended as it is - reversing it
// would splice a straight edge right across the shape and stroke it as a line across the icon.
const blob = (top, bottom) => top.concat(bottom.slice(1));

// ------------------------------------------------------------------ the icon
//
// Back to front, the way it is drawn on paper: the trails, then the straw (so the glass swallows its lower
// end), then the glass, then the cream mounded over its rim, then the cherry on top.

const cx = 216;                   // the glass's centre line
const RIM_Y = 150;                // where the glass's mouth is
const BASE_Y = 372;
const RIM_HW = 78;                // half-width at the mouth
const BASE_HW = 60;               // half-width at the foot
const taper = (y) => RIM_HW + (BASE_HW - RIM_HW) * ((y - RIM_Y) / (BASE_Y - RIM_Y));

const INK_W = 22;                 // the outline weight, in the drawing's own pixels

// The trails: tapering streaks behind the glass, thick where they meet it and drawn out to nothing behind,
// which is what reads as speed. They rise to the right and are well clear of the glass's own outline: laid
// flat against it they turn into handles sticking out of the drink.
strokeTapered(quad([20, 268], [74, 252], [118, 238], 16), 6, 19, INK, 0.8);
strokeTapered(quad([54, 334], [94, 322], [132, 306], 14), 5, 14, INK, 0.5);

// The straw, green like the pickup's, leaning out to the upper right.
const straw = [[cx + 40, 238], [cx + 154, 54]];
stroke(straw, 36, INK);
stroke(straw, 23, GREEN);

// The glass: a tapered tumbler, with the pink shake standing in it.
const glass = [
  [cx - RIM_HW, RIM_Y],
  [cx + RIM_HW, RIM_Y],
  [cx + BASE_HW, BASE_Y - 12],
  [cx + BASE_HW - 12, BASE_Y],
  [cx - BASE_HW + 12, BASE_Y],
  [cx - BASE_HW, BASE_Y - 12]
];
fillPoly(glass, TINT);
// The shake, a little short of the foot, following the taper.
const shakeTop = RIM_Y + 8, shakeBot = BASE_Y - 22;
fillPoly([
  [cx - taper(shakeTop), shakeTop],
  [cx + taper(shakeTop), shakeTop],
  [cx + taper(shakeBot), shakeBot],
  [cx - taper(shakeBot), shakeBot]
], PINK);
stroke(glass.concat([glass[0]]), INK_W, INK, 1, false);

// The cream, mounded over the mouth and hanging a little past it on both sides.
const cream = blob(
  [
    [cx - 100, 176],
    ...quad([cx - 100, 176], [cx - 92, 118], [cx - 50, 110], 14),
    ...quad([cx - 50, 110], [cx - 6, 102], [cx + 44, 108], 14),
    ...quad([cx + 44, 108], [cx + 86, 114], [cx + 100, 174], 14)
  ],
  [
    [cx + 100, 174],
    ...quad([cx + 100, 174], [cx, 204], [cx - 100, 176], 20)
  ]
);
fillPoly(cream, CREAM);
stroke(cream.concat([cream[0]]), INK_W, INK, 1, false);

// The cherry, sitting on the cream, with a short stalk - short on purpose: a long one runs alongside the
// straw in the same green and the two merge into one shape at HUD size.
const cherry = [cx - 34, 84];
stroke(quad([cherry[0], cherry[1] - 24], [cx - 24, 26], [cx - 2, 22], 16), 10, GREEN);
fillCircle(cherry[0], cherry[1], 33, RED);
stroke(circlePts(cherry[0], cherry[1], 33, 96).concat([[cherry[0] + 33, cherry[1]]]), INK_W * 0.9, INK, 1, false);
// A highlight, so it reads as glossy rather than as a flat disc.
fillCircle(cherry[0] - 11, cherry[1] - 11, 8, WHITE, 0.85);

// ------------------------------------------------------------------ encode

function crc32(bufIn) {
  let c, crc = 0xffffffff;
  for (let n = 0; n < bufIn.length; n++) {
    c = (crc ^ bufIn[n]) & 0xff;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    crc = c ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

function encode() {
  const raw = Buffer.alloc(H * (W * 4 + 1));
  let p = 0;
  for (let y = 0; y < H; y++) {
    raw[p++] = 0;                                  // filter: none
    for (let x = 0; x < W; x++) {
      let r = 0, g = 0, b = 0, a = 0;
      for (let sy = 0; sy < SS; sy++) {
        for (let sx = 0; sx < SS; sx++) {
          const o = ((y * SS + sy) * IW + (x * SS + sx)) * 4;
          r += buf[o]; g += buf[o + 1]; b += buf[o + 2]; a += buf[o + 3];
        }
      }
      const n = SS * SS;
      a /= n;
      // Un-premultiply: PNG holds straight alpha, and the division is the whole reason there are no grey
      // haloes around the ink where it meets transparency.
      const inv = a > 0.0001 ? 1 / a : 0;
      raw[p++] = Math.min(255, Math.round(r / n * inv));
      raw[p++] = Math.min(255, Math.round(g / n * inv));
      raw[p++] = Math.min(255, Math.round(b / n * inv));
      raw[p++] = Math.round(a * 255);
    }
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(W, 0);
  ihdr.writeUInt32BE(H, 4);
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0))
  ]);
}

// ------------------------------------------------------------------ report

// A coarse preview, mapped to the nearest authored colour, so the shape can be eyeballed without an image
// viewer. Two characters wide per character tall, because a terminal cell is about twice as tall as it is
// wide and the drawing must not come out stretched.
function preview() {
  const chars = { '#': INK, p: PINK, W: WHITE, r: RED, g: GREEN, c: CREAM };
  const sample = (fx, fy) => {
    const x = Math.min(IW - 1, Math.round(fx * W) * SS);
    const y = Math.min(IH - 1, Math.round(fy * H) * SS);
    const o = (y * IW + x) * 4;
    const a = buf[o + 3];
    if (a < 0.35) return '.';
    const inv = 1 / Math.max(a, 1e-4);
    const c = [buf[o] * inv, buf[o + 1] * inv, buf[o + 2] * inv];
    let best = '?', dist = Infinity;
    for (const k in chars) {
      const d = chars[k].reduce((s, v, i) => s + (v - c[i]) ** 2, 0);
      if (d < dist) { dist = d; best = k; }
    }
    return best;
  };
  const COLS = 88, ROWS = 44, ASPECT = 2;
  let minX = W, maxX = 0, minY = H, maxY = 0;
  for (let fy = 0; fy < ROWS; fy++)
    for (let fx = 0; fx < COLS; fx++)
      if (sample((fx + 0.5) / COLS, (fy + 0.5) / ROWS) !== '.') {
        const x = ((fx + 0.5) / COLS) * W, y = ((fy + 0.5) / ROWS) * H;
        if (x < minX) minX = x; if (x > maxX) maxX = x;
        if (y < minY) minY = y; if (y > maxY) maxY = y;
      }
  let out = '';
  for (let r = 0; r < ROWS; r++) {
    let line = '';
    for (let c = 0; c < COLS; c++) line += sample((c + 0.5) / COLS, (r + 0.5) / ROWS);
    out += line + '\n';
  }
  console.log(out);
  console.log('content spans x ' + Math.round(minX) + '..' + Math.round(maxX) + '   y ' + Math.round(minY) + '..' + Math.round(maxY) +
    '   (centre ' + Math.round((minX + maxX) / 2) + ',' + Math.round((minY + maxY) / 2) + '; canvas centre ' + W / 2 + ')');
  console.log('legend: # ink   p pink   W white   r red   g green   c cream   . clear');
}

const out = process.argv.slice(2).find((a) => !a.startsWith('--')) || 'Assets/Sprites/UI/hud/boostIcon.png';
const png = encode();
if (process.argv.includes('--write')) {
  fs.writeFileSync(out, png);
  console.log('wrote ' + out + '  ' + W + 'x' + H + '  ' + png.length + ' bytes');
}
if (process.argv.includes('--preview') || !process.argv.includes('--write')) preview();
