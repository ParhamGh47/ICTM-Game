// Renders a mock-up of the boost note, with the new icon in it, as a single self-contained HTML page - every
// asset inlined as a data URI, so the page needs nothing from the server and can be shown in the Preview tab.
//
// The numbers are the ones the prefab actually serializes: the CanvasUI canvas scales from a 1920x1080
// reference, so the note is 115x77 on a 1080p screen and about 77x51 on a 720p one. The icon has to read at
// the small end, which is why the mock shows it at three sizes.

const fs = require('fs');
const path = require('path');
const root = path.join(__dirname, '..');

const uri = (file, mime) => 'data:' + mime + ';base64,' + fs.readFileSync(path.join(root, file)).toString('base64');

const icon = uri('Assets/Sprites/UI/hud/boostIcon.png', 'image/png');
const paper = uri('Assets/Sprites/UI/hud/hudPaperStyle.png', 'image/png');
const compass = uri('Assets/Sprites/UI/hud/compass.png', 'image/png');
const font = uri('Assets/Fonts/Potk.ttf', 'font/ttf');

// The note, exactly as the prefab lays it out: a 115x77 plate, the icon centred 27 left of the note's middle
// and the count centred in what is left.
const note = (scale, iconPx, fontSize) => `
  <div class="note" style="width:${115 * scale}px;height:${77 * scale}px">
    <img class="plate" src="${paper}">
    <img class="boost" src="${icon}" style="
      width:${iconPx * scale}px;height:${iconPx * scale}px;
      left:${(57.5 - 27 - iconPx / 2) * scale}px;
      top:${(77 - iconPx) / 2 * scale}px">
    <div class="count" style="
      left:${(0.44 * 115) * scale}px;width:${(0.56 * 115) * scale}px;
      font-size:${fontSize * scale}px;
      transform:translateY(${1.8 * scale}px)">3</div>
  </div>`;

const row = (scale) => `
  <div class="pair">${note(scale, 52, 36)}${note(scale, 52, 36)}</div>`;

fs.writeFileSync(path.join(__dirname, 'boostpreview.html'), `<!doctype html>
<meta charset="utf-8">
<title>Boost icon</title>
<style>
  @font-face { font-family: Potk; src: url(${font}); }
  body { margin:0; background:#20444f; color:#eaf7fb; font:14px/1.5 system-ui, sans-serif; padding:26px 30px 40px; }
  h1 { font-size:17px; font-weight:600; margin:0 0 2px; }
  h2 { font-size:12px; font-weight:600; letter-spacing:.14em; text-transform:uppercase; opacity:.65; margin:26px 0 10px; }
  p { margin:0 0 14px; max-width:74ch; opacity:.8; }
  .big { display:flex; gap:22px; align-items:center; }
  .big > div { border-radius:14px; padding:16px; display:flex; gap:16px; align-items:center; }
  .onpaper { background:#7bcbe4; }
  .ondark { background:#1b3a44; }
  .pair { display:flex; gap:26px; align-items:flex-start; }
  .note { position:relative; flex:none; }
  .note .plate { position:absolute; inset:0; width:100%; height:100%; }
  .note .boost { position:absolute; display:block; image-rendering:auto; }
  .note .count {
    position:absolute; top:0; height:100%; display:flex; align-items:center; justify-content:center;
    font-family:Potk, sans-serif; color:#3f859c; text-shadow:4px -2px 0 rgba(255,255,255,.5);
  }
  .sizes { display:flex; gap:16px; align-items:flex-end; }
  .sizes figure { margin:0; text-align:center; }
  .sizes img { display:block; background:#7bcbe4; border-radius:9px; }
  .sizes figcaption { font-size:11px; opacity:.6; margin-top:6px; }
</style>

<h1>Boost icon — the milkshake, drawn</h1>
<p>Left: the artwork at 256 px on the HUD's own paper blue, and on the dark the truck usually drives over.
Right: how it reads small, which is the size that matters.</p>

<div class="big">
  <div class="onpaper"><img src="${icon}" width="256" height="256"></div>
  <div class="ondark"><img src="${icon}" width="256" height="256"></div>
</div>

<div class="sizes">
  ${[64, 52, 44, 36, 28].map((s) => `<figure><img src="${icon}" width="${s}" height="${s}"><figcaption>${s} px</figcaption></figure>`).join('')}
</div>

<h2>The note, as the prefab lays it out</h2>
<p>1&times; is a 1080p screen, and &times;0.67 is 720p — the note is 77&times;51 there and the icon 35 px.</p>
<div class="pair">
  ${row(1)}
  ${row(0.67)}
</div>

<h2>Zoomed, to check the fit</h2>
${row(3)}

<h2>For reference, the HUD's existing art</h2>
<div class="sizes">
  <figure><img src="${compass}" width="160"><figcaption>compass</figcaption></figure>
  <figure><img src="${paper}" width="220"><figcaption>the note's paper</figcaption></figure>
</div>
`);
console.log('wrote .checktmp/boostpreview.html');
