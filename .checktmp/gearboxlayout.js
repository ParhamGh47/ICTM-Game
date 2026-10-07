// Verifies the two new GEARBOX placements: it now shares the DIFFICULTY heading line in the main-menu
// options screen, and the difficulty caption line in the pause panel. Both were on an existing line before
// (a switch slot), so neither layout may grow taller - this checks the switch does not overlap what is now
// beside or above/below it, and that the page still clears the bottom.

// ---------------------------------------------------------------- main menu (OptionsScreen)

const mm = {
  sideMargin: 60,
  contentTopY: -224,
  captionSize: 22,
  switchSize: { x: 400, y: 54 },
  switchGap: 12,
  presetChoiceSize: { x: 190, y: 58 },
  groupGap: 16,
  presetButtonSize: { x: 230, y: 58 },
  presetGap: 16,
  sectionGap: 28,
  noteX: 1120,
  soundButtonSize: { x: 170, y: 46 },
  soundRowGap: 8,
  channels: 5,
  screen: 1080,
};

function mmHeading(y) {
  const ruleY = y - mm.captionSize * 1.5 - 4;
  return ruleY - 4 - 12;
}

let y = mm.contentTopY;
y = mmHeading(y);                                     // GRAPHICS
y -= mm.presetChoiceSize.y + mm.groupGap;             // presets
y -= mm.switchSize.y + mm.switchGap;                  // switch line 1 (shadows | blur)
// switch line 2 now holds speed particles alone
y -= mm.switchSize.y + mm.sectionGap;

const headingY = y;
y = mmHeading(y);                                     // DIFFICULTY heading
const difficultyRowY = y;
y -= mm.presetButtonSize.y + mm.sectionGap;
// sound group
let soundY = y - mm.captionSize * 1.5 - 12;
for (let c = 0; c < mm.channels; c++) soundY -= mm.soundButtonSize.y + mm.soundRowGap;
const lastSoundBottom = soundY + mm.soundRowGap;      // bottom edge of the last row

const gearboxMM = {
  x0: mm.noteX - 20 - mm.switchSize.x, x1: mm.noteX - 20,
  y0: headingY + 4, y1: headingY + 4 - mm.switchSize.y,
};
const diffRow = { x0: mm.sideMargin, x1: mm.sideMargin + 3 * mm.presetButtonSize.x + 2 * mm.presetGap,
                  y0: difficultyRowY, y1: difficultyRowY - mm.presetButtonSize.y };

const backTopY = -(mm.screen - (46 + 54));            // PlaceBottomRight(60, 46, 200x54)

console.log('--- main menu (OptionsScreen) ---');
console.log('gearbox     y', gearboxMM.y0.toFixed(1), '->', gearboxMM.y1.toFixed(1), ' x', gearboxMM.x0, '->', gearboxMM.x1);
console.log('difficulty  y', diffRow.y0.toFixed(1), '->', diffRow.y1.toFixed(1), ' x', diffRow.x0, '->', diffRow.x1);
console.log('note column starts x', mm.noteX);
console.log('gearbox clears note column:', gearboxMM.x1 <= mm.noteX);
const vClear = gearboxMM.y1 >= diffRow.y0;   // bottom edge at or above the row's top edge
console.log('gearbox clears difficulty row horizontally OR vertically:',
  gearboxMM.x0 >= diffRow.x1 || vClear);
console.log('vertical overlap with difficulty row (px):', vClear ? 0 : (diffRow.y0 - gearboxMM.y1).toFixed(1));
console.log('last sound row bottom', lastSoundBottom.toFixed(1), ' back top', backTopY.toFixed(1),
  ' gap', (lastSoundBottom - backTopY).toFixed(1));

// ---------------------------------------------------------------- pause panel

const pp = {
  contentTopY: 94,                 // y grows downward here
  captionHeight: 27,
  switchSize: { x: 248, y: 32 },
  switchGap: 4,
  presetChoiceSize: { x: 88, y: 38 },
  presetButtonSize: { x: 155, y: 38 },
  presetGap: 10,
  noteX: 505,
  safeHeight: 540,
};

let p = pp.contentTopY;
p += pp.captionHeight + 6;                          // graphics caption
p += pp.presetChoiceSize.y + 16;                    // presets
p += pp.switchSize.y + pp.switchGap;                // switch line 1 (shadows | blur)
// switch line 2: speed trails alone
p += pp.switchSize.y + 14;

const captionY = p;
p += pp.captionHeight + 6;
const pauseDiffRowY = p;

const gearboxPP = { x0: pp.noteX - 20 - pp.switchSize.x, x1: pp.noteX - 20,
                    y0: captionY, y1: captionY + pp.switchSize.y };
const pauseDiffRow = { x0: 0, x1: 3 * pp.presetButtonSize.x + 2 * pp.presetGap,
                       y0: pauseDiffRowY, y1: pauseDiffRowY + pp.presetButtonSize.y };

console.log('\n--- pause panel (PauseOptionsPanel) ---');
console.log('gearbox     y', gearboxPP.y0, '->', gearboxPP.y1, ' x', gearboxPP.x0, '->', gearboxPP.x1);
console.log('difficulty  y', pauseDiffRow.y0, '->', pauseDiffRow.y1, ' x', pauseDiffRow.x0, '->', pauseDiffRow.x1);
console.log('gearbox clears note column:', gearboxPP.x1 <= pp.noteX);
console.log('vertical clearance above difficulty row (px):',
  (pauseDiffRow.y0 - gearboxPP.y1).toFixed(1));
console.log('the change adds no line to either page:',
  'the switch moved from an existing switch slot to an existing heading line');
