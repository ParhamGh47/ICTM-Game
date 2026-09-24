// Mirrors the layout PauseOptionsPanel builds and checks it for overflow and collisions.
const S = {
  safe: [830, 570],
  headingTopY: 0, headingHeight: 46,
  back: [140, 38], backTopY: 56,
  tabTopY: 52, tab: [160, 38], tabGap: 12,
  contentTopY: 100,
  columnHeaderHeight: 18, captionHeight: 30, rowHeight: 28, rowGap: 2, groupGap: 10,
  rowInset: 10, actionWidth: 196, keyboardX: 216, keyboardWidth: 312, gamepadX: 538, gamepadWidth: 292,
  preset: [165, 44], presetGap: 12, switch: [320, 42], switchGap: 8,
  noteX: 545, noteWidth: 285,
  promptTopY: 300, promptHeight: 58, reload: [200, 42],
};

const groups = [
  ["DRIVING", 9],
  ["MENUS", 3],
];
const notes = 0;

const rects = []; // {page, name, x, y, w, h}
const add = (page, name, x, y, w, h) => rects.push({ page, name, x, y, w, h });
const W = S;

// ---- shared
add("both", "Title", 0, S.headingTopY, S.safe[0], S.headingHeight);
add("both", "Back", S.safe[0] - S.back[0], S.backTopY, S.back[0], S.back[1]);
const tabTotal = S.tab[0] * 2 + S.tabGap;
const tabLeft = (S.safe[0] - tabTotal) / 2;
add("both", "Tab Controls", tabLeft, S.tabTopY, S.tab[0], S.tab[1]);
add("both", "Tab Settings", tabLeft + S.tab[0] + S.tabGap, S.tabTopY, S.tab[0], S.tab[1]);

// ---- controls page
let y = S.contentTopY;
add("controls", "Header Keyboard", S.keyboardX, y, S.keyboardWidth, S.columnHeaderHeight);
add("controls", "Header Gamepad", S.gamepadX, y, S.gamepadWidth, S.columnHeaderHeight);
y += S.columnHeaderHeight;
for (const [title, rows] of groups) {
  add("controls", "Caption " + title, 0, y, S.safe[0], S.captionHeight);
  y += S.captionHeight;
  for (let i = 0; i < rows; i++) {
    add("controls", `Row ${title} ${i}`, 0, y, S.safe[0], S.rowHeight);
    y += S.rowHeight + S.rowGap;
  }
  y += S.groupGap;
}
for (let i = 0; i < notes; i++) {
  add("controls", "Note " + i, 14, y, S.safe[0] - S.back[0] - 40, 20);
  y += 24;
}
const controlsBottom = y;

// ---- settings page
let sy = S.contentTopY;
add("settings", "Settings Note", S.noteX, sy, S.noteWidth, 44);
add("settings", "Caption Graphics", 0, sy, S.noteX - 20, S.captionHeight);
sy += S.captionHeight + 6;
for (let i = 0; i < 3; i++)
  add("settings", "Preset " + i, i * (S.preset[0] + S.presetGap), sy, S.preset[0], S.preset[1]);
sy += S.preset[1] + 16;
add("settings", "Shadows", 0, sy, S.switch[0], S.switch[1]);
sy += S.switch[1] + S.switchGap;
add("settings", "Blur", 0, sy, S.switch[0], S.switch[1]);
add("settings", "Prompt", 0, S.promptTopY, S.safe[0], S.promptHeight);
add("settings", "Reload", S.safe[0] - S.reload[0] - 12, S.promptTopY + (S.promptHeight - S.reload[1]) / 2, S.reload[0], S.reload[1]);

// ---- report
let problems = 0;
for (const r of rects) {
  if (r.x < 0 || r.y < 0 || r.x + r.w > S.safe[0] + 0.01 || r.y + r.h > S.safe[1] + 0.01) {
    console.log(`OVERFLOW  ${r.page.padEnd(9)} ${r.name.padEnd(20)} x ${r.x}..${r.x + r.w}  y ${r.y}..${r.y + r.h}`);
    problems++;
  }
}
console.log(`controls page ends at y=${controlsBottom} of ${S.safe[1]}`);
console.log(`settings page: switches end at y=${sy + S.switch[1]}, prompt ${S.promptTopY}..${S.promptTopY + S.promptHeight}`);

// collisions between things that should not touch (rows/plates contain cells, so skip same-row pairs)
const containment = (a, b, pad = 0) =>
  a.x >= b.x + pad && a.x + a.w <= b.x + b.w + pad && a.y >= b.y + pad && a.y + a.h <= b.y + b.h + pad;
const overlaps = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;

for (let i = 0; i < rects.length; i++) {
  for (let j = i + 1; j < rects.length; j++) {
    const a = rects[i], b = rects[j];
    if (a.page !== b.page && a.page !== "both" && b.page !== "both") continue;
    if (!overlaps(a, b)) continue;
    if (containment(a, b, 1) || containment(b, a, 1)) continue;
    // a border-only touch of the BACK button is fine where the boxes share no text
    console.log(`COLLISION ${a.page}/${a.name} overlaps ${b.page}/${b.name}\n          ${a.x},${a.y} ${a.w}x${a.h}  vs  ${b.x},${b.y} ${b.w}x${b.h}`);
    problems++;
  }
}

// does the safe box sit inside the plate sprite's visible extent in the 1300x850 window?
const visible = { x0: -486.7, x1: 488.3, y0: -335.0, y1: 331.8 };
console.log("\nplate visible box (window coords):", visible);
console.log("safe box:", { x0: -S.safe[0] / 2, x1: S.safe[0] / 2, y0: -S.safe[1] / 2, y1: S.safe[1] / 2 });
const inside = -S.safe[0] / 2 >= visible.x0 && S.safe[0] / 2 <= visible.x1 && -S.safe[1] / 2 >= visible.y0 && S.safe[1] / 2 <= visible.y1;
console.log("fits inside the plate:", inside);
if (inside) {
  console.log(
    "padding inside the plate: left/right " + (-S.safe[0] / 2 - visible.x0).toFixed(0) + "/" + (visible.x1 - S.safe[0] / 2).toFixed(0) +
    ", top/bottom " + (-S.safe[1] / 2 - visible.y0).toFixed(0) + "/" + (visible.y1 - S.safe[1] / 2).toFixed(0)
  );
}

// text widths at a rough 0.5em, checked against the cell each string is drawn in
const TEXT = [
  ["action column", "Move the highlight", 20, S.actionWidth - S.rowInset],
  ["keyboard column", "A  D   or   Left / Right arrow", 20, S.keyboardWidth],
  ["keyboard column", "Arrow keys   or   W A S D", 20, S.keyboardWidth],
  ["gamepad column", "D-pad   or   Left stick", 20, S.gamepadWidth],
  ["gamepad column", "Right trigger  RT", 20, S.gamepadWidth],
  ["reload prompt", "The level's ground detail changed - restart to apply it.", 16, S.safe[0] - S.reload[0] - 60],
];
// the one line that is expected to wrap, so it is checked for room rather than for one line
const wrapped = { text: "Your choice is saved and applied straight away.", size: 16, width: S.noteWidth, height: 44 };
const needed = Math.ceil((wrapped.text.length * wrapped.size * 0.5) / wrapped.width) * (wrapped.size * 1.25);
if (needed > wrapped.height) {
  console.log(`TEXT the settings note needs ~${needed.toFixed(0)}px of height in ${wrapped.height}px`);
  problems++;
}
for (const [where, text, size, box] of TEXT) {
  const width = text.length * size * 0.5;
  if (width > box) {
    console.log(`TEXT SLack ${where.padEnd(16)} "${text}" needs ~${width.toFixed(0)}px in ${box.toFixed(0)}px`);
    problems++;
  }
}

console.log(problems === 0 ? "\nNo overflow, no collisions, every string fits its box." : `\n${problems} problem(s).`);
