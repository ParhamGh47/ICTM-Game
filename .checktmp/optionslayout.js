// Mirrors the y-cursor arithmetic in Assets/Scripts/UI/OptionsScreen.cs so the layout can be checked
// without opening Unity. Reference resolution is 1920x1080, y measured downwards from the top (the screen's
// page uses negative y, growing downwards; this file keeps Unity's signs).
const src = require("fs").readFileSync("Assets/Scripts/UI/OptionsScreen.cs", "utf8");

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

const contentTopY = num("contentTopY");
const headerHeight = num("headerHeight");
const rowHeight = num("rowHeight");
const rowGap = num("rowGap");
const groupGap = num("groupGap");
const captionSize = num("captionSize");
const noteSize = num("noteSize");
const bodySize = num("bodySize");
const sideMargin = num("sideMargin");
const preset = vec("presetButtonSize");
const swtch = vec("switchSize");
const switchGap = num("switchGap");
const tabTopY = num("tabTopY");
const tab = vec("tabSize");
const tabGap = num("tabGap");
const titleTopY = num("titleTopY");
const titleSize = num("titleSize");
const noteX = num("noteX");
const noteWidth = num("noteWidth");
const sectionGap = num("sectionGap");
const sectionRuleWidth = num("sectionRuleWidth");
const sectionRuleHeight = num("sectionRuleHeight");
const soundButton = vec("soundButtonSize");
const soundButtonGap = num("soundButtonGap");
const soundRowGap = num("soundRowGap");
const soundLabelWidth = num("soundLabelWidth");
const soundFirstButtonX = num("soundFirstButtonX");
const cameraMixNoteGap = num("cameraMixNoteGap");

const contentWidth = 1920 - sideMargin * 2;
const captionHeight = captionSize * 1.5;
const row = rowHeight + rowGap;
const rects = [];
const add = (page, name, x, y, w, h) => rects.push({ page, name, x, y, w, h });

// header + tabs
add("both", "title", sideMargin, titleTopY, 900, titleSize * 1.4);
const tabs = [...src.matchAll(/CreateTab\(root, "([A-Za-z ]+)"/g)].map((m) => m[1]);
tabs.forEach((name, i) =>
  add("both", "tab " + name, sideMargin + i * (tab[0] + tabGap), tabTopY, tab[0], tab[1])
);

// ---- controls page
let y = contentTopY;
add("controls", "column headers", sideMargin, y, 1200, headerHeight);
y -= headerHeight;

const groups = [...src.matchAll(/new ControlGroup\("([A-Z]+)"/g)].map((m) => m[1]);
const counts = [...src.matchAll(/new ControlGroup\("[A-Z]+",([\s\S]*?)\)\)/g)].map(
  (m) => (m[1].match(/new ControlBinding\(/g) || []).length
);

for (let g = 0; g < groups.length; g++) {
  add("controls", "caption " + groups[g], sideMargin, y, contentWidth, captionHeight);
  y -= captionSize * 1.5 + 10 + 4;
  add("controls", groups[g] + " rows (" + counts[g] + ")", sideMargin, y, contentWidth, counts[g] * row);
  y -= counts[g] * row + groupGap;
}
const controlsBottom = y;

// ---- settings page
let s = contentTopY;

// the one note plate, in the right-hand column. Its height comes from a rough measure of the text at 0.5em,
// the way the checker has always done it.
const noteText = "Your choice is saved and applied straight away.";
const noteLines = Math.ceil((noteText.length * noteSize * 0.5) / (noteWidth - 52));
const notePlate = { x: noteX, y: contentTopY, w: noteWidth, h: 26 + Math.max(noteSize * 2, noteLines * noteSize * 1.25) + 26 };
add("settings", "settings note", notePlate.x, notePlate.y, notePlate.w, notePlate.h);
const notesBottom = notePlate.y - notePlate.h;

// a section heading: the caption, the accent rule under it, and the gap down to the content
const heading = (name, width) => {
  add("settings", name, sideMargin, s, width, captionHeight);
  add("settings", name + " rule", sideMargin, s - captionSize * 1.5 - 4, sectionRuleWidth, sectionRuleHeight);
  s = s - captionSize * 1.5 - 4 - sectionRuleHeight - 12;
};

heading("caption graphics", contentWidth);
for (let i = 0; i < 3; i++) add("settings", "preset " + i, sideMargin + i * (preset[0] + 16), s, preset[0], preset[1]);
s -= preset[1] + groupGap;
add("settings", "shadows", sideMargin, s, swtch[0], swtch[1]);
s -= swtch[1] + switchGap;
add("settings", "motion blur", sideMargin, s, swtch[0], swtch[1]);
s -= swtch[1] + sectionGap;
heading("caption difficulty", contentWidth);
for (let i = 0; i < 3; i++)
  add("settings", "difficulty " + i, sideMargin + i * (preset[0] + 16), s, preset[0], preset[1]);
const difficultiesBottom = s - preset[1];
s -= preset[1] + sectionGap;

// the sound group
const soundCaptionHeight = captionSize * 1.5;
const headingTop = s;
heading("caption sound", soundLabelWidth - 20);
const switchY = headingTop + Math.max(0, (swtch[1] - soundCaptionHeight) * 0.5);
add("settings", "camera mix", sideMargin + soundLabelWidth, switchY, swtch[0], swtch[1]);
const mixNoteX = sideMargin + soundLabelWidth + swtch[0] + cameraMixNoteGap;
add("settings", "camera mix note", mixNoteX, switchY - (swtch[1] - noteSize * 1.6) * 0.5, contentWidth - (mixNoteX - sideMargin), swtch[1]);
s = Math.min(s, switchY - swtch[1]) - 12;

const firstSoundRow = s;

for (let i = 0; i < 5; i++) {
  add("settings", "channel label " + i, sideMargin, s - (soundButton[1] - bodySize * 1.5) * 0.5, soundLabelWidth, bodySize * 1.5);
  for (let step = 0; step < 4; step++)
    add("settings", `step ${i}/${step}`, sideMargin + soundFirstButtonX + step * (soundButton[0] + soundButtonGap), s, soundButton[0], soundButton[1]);
  s -= soundButton[1] + soundRowGap;
}
const settingsBottom = s;
const soundRowsRight = sideMargin + soundFirstButtonX + 4 * soundButton[0] + 3 * soundButtonGap;

// ---- report
const problems = [];
if (controlsBottom < -1080) problems.push(`the controls page runs off the bottom (${controlsBottom})`);
if (settingsBottom < -1080) problems.push(`the settings page runs off the bottom (${settingsBottom})`);
if (soundRowsRight > 1920 - sideMargin) problems.push(`the sound rows run past the right margin (${soundRowsRight})`);

// the notes must be clear of everything the left column draws, and the sound rows must start below them
for (const r of rects) {
  if (r.x + r.w > 1920 - sideMargin + 0.01 || r.x < sideMargin - 0.01)
    problems.push(`${r.name} breaks the side margin: x ${r.x}..${r.x + r.w}`);
  if (r.y > 0 || r.y - r.h < -1080) problems.push(`${r.name} is off screen: y ${r.y}..${r.y - r.h}`);
}
if (soundRowsRight <= noteX)
  problems.push("the sound rows do not reach the note column, so the check below is meaningless");
for (const r of rects) {
  if (r.page !== "settings") continue;
  if (r.name.startsWith("channel label") || r.name.startsWith("step") || r.name.includes("rule")) {
    if (r.x + r.w > noteX && r.y > notesBottom && r.y - r.h < notePlate.y + 0.01)
      problems.push(`${r.name} runs into the note column (${r.x}..${r.x + r.w} at ${r.y})`);
  }
}
// a heading's rule must not run under the switch on its own line, and neither may its lettering
if (sectionRuleWidth + sideMargin > sideMargin + soundLabelWidth)
  problems.push("the section rule reaches past the sound heading's own column");

// the BACK button, bottom-right
const back = { x: 1660, y: -980, w: 200, h: 54 };
for (const r of rects) {
  if (r.x < back.x + back.w && back.x < r.x + r.w && r.y - r.h < back.y + back.h && back.y < r.y)
    problems.push(`${r.name} overlaps BACK`);
}

console.log("tabs:", tabs.join(", "));
console.log("controls page ends at y " + controlsBottom.toFixed(0) + " (bottom margin " + (1080 + controlsBottom).toFixed(0) + "px)");
console.log("settings: difficulty row ends " + difficultiesBottom.toFixed(0) + ", sound rows " + (s + 5 * (soundButton[1] + soundRowGap) - soundRowGap).toFixed(0) + " .. " + settingsBottom.toFixed(0) + " (bottom margin " + (1080 + settingsBottom).toFixed(0) + "px)");
console.log("note plate ends at y " + notesBottom.toFixed(0) + "; sound rows start at y " + firstSoundRow.toFixed(0));
console.log("sound rows end at x " + soundRowsRight.toFixed(0) + " of " + (1920 - sideMargin) + "; camera mix note x " + mixNoteX.toFixed(0) + ".." + (1920 - sideMargin));

// one-line strings against the box each is drawn in, at the same rough 0.5em measure
const cells = [
  ["preset (MEDIUM)", "MEDIUM", captionSize, preset[0]],
  ["switch", "MOTION BLUR   OFF", bodySize, swtch[0]],
  ["channel label", "ENVIRONMENT", bodySize, soundLabelWidth],
  ["step button", "MEDIUM", bodySize, soundButton[0]],
  ["camera mix", "CAMERA MIX   OFF", bodySize, swtch[0]],
  ["camera mix note", "Keeps the engine at the same level whichever camera you drive from.", noteSize, contentWidth - (mixNoteX - sideMargin)],
];
for (const [where, text, size, box] of cells) {
  const needed = text.length * size * 0.5;
  if (needed > box) problems.push(`the ${where} needs ~${needed.toFixed(0)}px in ${box.toFixed(0)}px`);
}

console.log(problems.length ? "\nPROBLEMS:\n  " + problems.join("\n  ") : "\nNo overflow, no collisions, every string fits its box.");
