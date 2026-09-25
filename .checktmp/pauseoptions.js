// Mirrors the layout PauseOptionsPanel builds and checks it for overflow and collisions.
// The pause panel lays out downwards from the top of its content box, so y grows downwards here too.
const src = require("fs").readFileSync("Assets/Scripts/UI/Pause/PauseOptionsPanel.cs", "utf8");

function num(name) {
  const m = src.match(new RegExp(name + "\\s*=\\s*(-?[0-9.]+)f"));
  if (!m) throw new Error("not found: " + name);
  return parseFloat(m[1]);
}
function vec(name) {
  const m = src.match(new RegExp(name + "\\s*=\\s*new Vector2\\(([0-9.]+)f, ([0-9.]+)f\\)"));
  if (!m) throw new Error("not found: " + name);
  return [parseFloat(m[1]), parseFloat(m[2])];
}

const S = {
  safe: vec("safeSize"),
  headingTopY: num("headingTopY"), headingHeight: num("headingHeight"),
  back: vec("backButtonSize"), backTopY: num("backTopY"),
  tabTopY: num("tabTopY"), tab: vec("tabSize"), tabGap: num("tabGap"),
  contentTopY: num("contentTopY"),
  columnHeaderHeight: num("columnHeaderHeight"), captionHeight: num("captionHeight"),
  rowHeight: num("rowHeight"), rowGap: num("rowGap"), groupGap: num("groupGap"),
  rowInset: num("rowInset"), actionWidth: num("actionColumnWidth"),
  keyboardX: num("keyboardColumnX"), keyboardWidth: num("keyboardColumnWidth"),
  gamepadX: num("gamepadColumnX"), gamepadWidth: num("gamepadColumnWidth"),
  preset: vec("presetButtonSize"), presetGap: num("presetGap"),
  switch: vec("switchSize"), switchGap: num("switchGap"),
  noteX: num("noteX"), noteWidth: num("noteWidth"),
  prompt: vec("promptSize"), reload: vec("reloadButtonSize"), promptButtonGap: num("promptButtonGap"),
  promptScrimAlpha: num("promptScrimAlpha"),
  soundButton: vec("soundButtonSize"), soundButtonGap: num("soundButtonGap"), soundRowGap: num("soundRowGap"),
  soundLabelWidth: num("soundLabelWidth"), soundFirstButtonX: num("soundFirstButtonX"),
  cameraMix: vec("cameraMixSize"), cameraMixX: num("cameraMixX"), cameraMixNoteX: num("cameraMixNoteX"),
};

const tabs = [...src.matchAll(/CreateTab\(root, "([A-Za-z ]+)"/g)].map((m) => m[1]);
const groups = [["DRIVING", 9], ["MENUS", 3]];

const rects = [];
const add = (page, name, x, y, w, h) => rects.push({ page, name, x, y, w, h });

// ---- shared
add("both", "Title", 0, S.headingTopY, S.safe[0], S.headingHeight);
add("both", "Back", S.safe[0] - S.back[0], S.backTopY, S.back[0], S.back[1]);
const tabTotal = S.tab[0] * tabs.length + S.tabGap * (tabs.length - 1);
const tabLeft = (S.safe[0] - tabTotal) / 2;
tabs.forEach((name, i) =>
  add("both", "Tab " + name, tabLeft + i * (S.tab[0] + S.tabGap), S.tabTopY, S.tab[0], S.tab[1])
);

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
const controlsBottom = y;

// ---- settings page
let sy = S.contentTopY;
add("settings", "Settings Note", S.noteX, sy, S.noteWidth, 44);
const notesBottom = sy + 44;

add("settings", "Caption Graphics", 0, sy, S.noteX - 20, S.captionHeight);
sy += S.captionHeight + 6;
for (let i = 0; i < 3; i++)
  add("settings", "Preset " + i, i * (S.preset[0] + S.presetGap), sy, S.preset[0], S.preset[1]);
const presetsRight = 3 * S.preset[0] + 2 * S.presetGap;
sy += S.preset[1] + 16;
add("settings", "Shadows", 0, sy, S.switch[0], S.switch[1]);
sy += S.switch[1] + S.switchGap;
add("settings", "Blur", 0, sy, S.switch[0], S.switch[1]);
sy += S.switch[1] + 14;
add("settings", "Caption Difficulty", 0, sy, S.noteX - 20, S.captionHeight);
sy += S.captionHeight + 6;
for (let i = 0; i < 3; i++)
  add("settings", "Difficulty " + i, i * (S.preset[0] + S.presetGap), sy, S.preset[0], S.preset[1]);
const difficultyBottom = sy + S.preset[1];
sy += S.preset[1] + 10;

// the sound group
const soundCaptionHeight = 18 * 1.5;
add("settings", "Caption Sound", 0, sy, S.cameraMixX - 10, soundCaptionHeight);
add("settings", "Camera Mix", S.cameraMixX, sy, S.cameraMix[0], S.cameraMix[1]);
add("settings", "Camera Mix Note", S.cameraMixNoteX, sy + (S.cameraMix[1] - 15 * 1.5) * 0.5,
  S.safe[0] - S.cameraMixNoteX, soundCaptionHeight);
sy += Math.max(soundCaptionHeight, S.cameraMix[1]) + 10;

for (let i = 0; i < 5; i++) {
  add("settings", "Channel label " + i, 0, sy + (S.soundButton[1] - 18 * 1.5) / 2, S.soundLabelWidth, 18 * 1.5);
  for (let s = 0; s < 4; s++)
    add("settings", `Step ${i}/${s}`, S.soundFirstButtonX + s * (S.soundButton[0] + S.soundButtonGap), sy, S.soundButton[0], S.soundButton[1]);
  sy += S.soundButton[1] + S.soundRowGap;
}
const settingsBottom = sy;
const soundRowsRight = S.soundFirstButtonX + 4 * S.soundButton[0] + 3 * S.soundButtonGap;

// the restart prompt: a dialog centred on the window, over a scrim. It is meant to cover the page, so it is
// checked against the content box and its own children rather than against the rows underneath it.
const prompt = {
  x: (S.safe[0] - S.prompt[0]) / 2,
  y: (S.safe[1] - S.prompt[1]) / 2,
  w: S.prompt[0],
  h: S.prompt[1],
};
const promptCentreY = prompt.y + prompt.h / 2;
// the label is centred 44 above the plate's middle, and the buttons 54 below it
const promptLabel = { x: prompt.x + 40, y: promptCentreY - 44 - (S.prompt[1] * 0.5 - 24) / 2, w: S.prompt[0] - 80, h: S.prompt[1] * 0.5 - 24 };
const half = (S.reload[0] + S.promptButtonGap) / 2;
const buttonY = promptCentreY + (S.prompt[1] * 0.5 - S.reload[1] * 0.5 - 34);
const dismiss = { x: prompt.x + prompt.w / 2 - half - S.reload[0] / 2, y: buttonY - S.reload[1] / 2, w: S.reload[0], h: S.reload[1] };
const reload = { x: prompt.x + prompt.w / 2 + half - S.reload[0] / 2, y: dismiss.y, w: S.reload[0], h: S.reload[1] };
add("prompt", "Prompt", prompt.x, prompt.y, prompt.w, prompt.h);
add("prompt", "Prompt Label", promptLabel.x, promptLabel.y, promptLabel.w, promptLabel.h);
add("prompt", "Keep Playing", dismiss.x, dismiss.y, dismiss.w, dismiss.h);
add("prompt", "Restart Level", reload.x, reload.y, reload.w, reload.h);

// ---- report
const problems = [];
for (const r of rects) {
  const inside = r.x >= -0.01 && r.y >= -0.01 && r.x + r.w <= S.safe[0] + 0.01 && r.y + r.h <= S.safe[1] + 0.01;
  if (!inside) problems.push(`OVERFLOW ${r.page}/${r.name}: x ${r.x}..${r.x + r.w}, y ${r.y}..${r.y + r.h}`);
}
const containment = (a, b, pad = 0) =>
  a.x >= b.x + pad && a.x + a.w <= b.x + b.w + pad && a.y >= b.y + pad && a.y + a.h <= b.y + b.h + pad;
const overlaps = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
for (let i = 0; i < rects.length; i++) {
  for (let j = i + 1; j < rects.length; j++) {
    const a = rects[i], b = rects[j];
    // the prompt is a dialog over the page, so it and its own children are meant to cover what is under them
    if (a.page === "prompt" || b.page === "prompt") continue;
    if (a.page !== b.page && a.page !== "both" && b.page !== "both") continue;
    if (!overlaps(a, b)) continue;
    if (containment(a, b, 1) || containment(b, a, 1)) continue;
    problems.push(`COLLISION ${a.page}/${a.name} overlaps ${b.page}/${b.name}: ${a.x},${a.y} ${a.w}x${a.h} vs ${b.x},${b.y} ${b.w}x${b.h}`);
  }
}

// everything the prompt draws must sit on the prompt's own plate
for (const r of rects) {
  if (r.page !== "prompt" || r.name === "Prompt") continue;
  if (!containment(r, prompt, 1)) problems.push(`the prompt's ${r.name} does not sit on its plate`);
  if (r.name === "Prompt Label" || r.name === "Keep Playing") continue;
  if (promptLabel.y + promptLabel.h + 8 > dismiss.y) problems.push("the prompt's message runs into its buttons");
}

// the sound rows must start below the notes column, or they would run under it
if (settingsBottom - 5 * (S.soundButton[1] + S.soundRowGap) < notesBottom && soundRowsRight > S.noteX)
  problems.push("the sound rows start above the bottom of the note column");

console.log("tabs:", tabs.join(", "));
console.log(`controls page ends at y=${controlsBottom} of ${S.safe[1]}`);
console.log(`settings page: difficulty row ends ${difficultyBottom}, sound rows end ${settingsBottom} of ${S.safe[1]}`);
console.log(`sound rows end x=${soundRowsRight} of ${S.safe[0]}; presets end x=${presetsRight}; notes x ${S.noteX}..${S.noteX + S.noteWidth}`);
console.log(`prompt (${prompt.w}x${prompt.h}) centred: x ${prompt.x}..${prompt.x + prompt.w}, y ${prompt.y}..${prompt.y + prompt.h} of the content box`);
console.log(`  message y ${promptLabel.y.toFixed(0)}..${(promptLabel.y + promptLabel.h).toFixed(0)}, buttons y ${dismiss.y.toFixed(0)}..${(dismiss.y + dismiss.h).toFixed(0)}`);

// the safe box and the prompt against the plate artwork in the 1300x850 window
const visible = { x0: -486.7, x1: 488.3, y0: -335.0, y1: 331.8 };
const box = { x0: -S.safe[0] / 2, x1: S.safe[0] / 2, y0: -S.safe[1] / 2, y1: S.safe[1] / 2 };
console.log("padding inside the plate: left/right " + (box.x0 - visible.x0).toFixed(0) + "/" + (visible.x1 - box.x1).toFixed(0) +
  ", top/bottom " + (visible.y1 - box.y1).toFixed(0) + "/" + (box.y0 - visible.y0).toFixed(0));
if (!(box.x0 >= visible.x0 && box.x1 <= visible.x1 && box.y0 >= visible.y0 && box.y1 <= visible.y1))
  problems.push("the content box is not inside the plate");

// text widths at a rough 0.5em, checked against the cell each string is drawn in
const TEXT = [
  ["action column", "Move the highlight", 20, S.actionWidth - S.rowInset],
  ["keyboard column", "A  D   or   Left / Right arrow", 20, S.keyboardWidth],
  ["keyboard column", "Arrow keys   or   W A S D", 20, S.keyboardWidth],
  ["gamepad column", "D-pad   or   Left stick", 20, S.gamepadWidth],
  ["sound label", "ENVIRONMENT", 18, S.soundLabelWidth],
  ["sound step", "MEDIUM", 15, S.soundButton[0]],
  ["camera mix", "CAMERA MIX   OFF", 15, S.cameraMix[0]],
  ["camera mix note", "Keeps the engine steady from any camera.", 15, S.safe[0] - S.cameraMixNoteX],
  ["prompt", "Difficulty changed - restart to play this level at the new one.", 19, S.prompt[0] - 80],
  ["prompt button", "RESTART LEVEL", 16, S.reload[0]],
  ["prompt button", "KEEP PLAYING", 16, S.reload[0]],
  ["caption", "DIFFICULTY", 18, S.noteX - 20],
];
for (const [where, text, size, box2] of TEXT) {
  const width = text.length * size * 0.5;
  if (width > box2) problems.push(`TEXT the ${where} needs ~${width.toFixed(0)}px in ${box2.toFixed(0)}px`);
}
// the two wrapping notes
const wraps = [
  ["settings note", "Your choice is saved and applied straight away.", 16, S.noteWidth, 44],
  ["sound note", "SOUNDTRACK is the music, ENGINE your own truck, EFFECTS everything it hits or is told to do, and ENVIRONMENT is the world around the road.", 15, S.noteWidth, 96],
];
for (const [where, text, size, width, height] of wraps) {
  const needed = Math.ceil((text.length * size * 0.5) / width) * (size * 1.3);
  if (needed > height) problems.push(`TEXT the ${where} needs ~${needed.toFixed(0)}px of height in ${height}px`);
}

console.log(problems.length ? "\nPROBLEMS:\n  " + problems.join("\n  ") : "\nNo overflow, no collisions, every string fits its box.");
