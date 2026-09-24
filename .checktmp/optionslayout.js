// Mirrors the y-cursor arithmetic in Assets/Scripts/UI/OptionsScreen.cs so the layout can be checked
// without opening Unity. Reference resolution is 1920x1080, y measured downwards from the top.
const src = require("fs").readFileSync("Assets/Scripts/UI/OptionsScreen.cs", "utf8");

function num(name) {
  const m = src.match(new RegExp(name + "\\s*=\\s*(-?[0-9.]+)f"));
  if (!m) throw new Error("not found: " + name);
  return parseFloat(m[1]);
}

const contentTopY = num("contentTopY");
const headerHeight = num("headerHeight");
const rowHeight = num("rowHeight");
const rowGap = num("rowGap");
const groupGap = num("groupGap");
const captionSize = num("captionSize");
const noteSize = num("noteSize");
const sideMargin = num("sideMargin");
const presetButtonH = parseFloat(src.match(/presetButtonSize = new Vector2\(([0-9.]+)f, ([0-9.]+)f\)/)[2]);
const switchH = parseFloat(src.match(/switchSize = new Vector2\(([0-9.]+)f, ([0-9.]+)f\)/)[2]);
const tabTopY = num("tabTopY");
const tabH = parseFloat(src.match(/tabSize = new Vector2\(([0-9.]+)f, ([0-9.]+)f\)/)[2]);
const titleTopY = num("titleTopY");
const titleSize = num("titleSize");

const captionHeight = captionSize * 1.5;
const row = rowHeight + rowGap;
const out = [];
const mark = (name, top, height) => out.push({ name, top, bottom: top - height });

// header + tabs + title
mark("title", titleTopY, titleSize * 1.4);
mark("tabs", tabTopY, tabH);

// controls page
let y = contentTopY;
mark("column headers", y, headerHeight);
y -= headerHeight;

const groups = [...src.matchAll(/new ControlGroup\("([A-Z]+)"/g)].map((m) => m[1]);
const counts = [...src.matchAll(/new ControlGroup\("[A-Z]+",([\s\S]*?)\)\)/g)].map(
  (m) => (m[1].match(/new ControlBinding\(/g) || []).length
);

for (let g = 0; g < groups.length; g++) {
  mark("caption " + groups[g], y, captionHeight);
  y -= captionHeight + 10 + 4;
  mark(groups[g] + " rows (" + counts[g] + ")", y, counts[g] * row);
  y -= counts[g] * row + groupGap;
}

for (let i = 0; i < 2; i++) {
  const h = Math.max(noteSize * 1.6, noteSize * 1.3);
  mark("note " + (i + 1), y, h);
  y -= h + 8;
}
const controlsBottom = y;

const switchGap = num("switchGap");
const noteWidth = num("noteWidth");
const noteX = num("noteX");
const contentWidth = 1920 - sideMargin * 2;

// settings page. BuildCaption places the caption at y and returns y - size*1.5 - 10, which is where the
// row under it goes.
let s = contentTopY;
const noteBody = Math.max(noteSize * 2, noteSize * 1.25);
mark("settings note plate", s, 26 + noteBody + 26);

const caption = (name) => {
  mark(name, s, captionHeight);
  s -= captionHeight + 10;
};

caption("caption graphics");
mark("presets", s, presetButtonH);
s -= presetButtonH + groupGap;
mark("shadows", s, switchH);
s -= switchH + switchGap;
mark("motion blur", s, switchH);
s -= switchH + groupGap;
caption("caption difficulty");
mark("difficulties", s, presetButtonH);
const difficultiesBottom = s - presetButtonH;
mark("difficulty note", s - presetButtonH - 12, Math.max(noteSize * 1.6, noteSize * 1.3));
const settingsBottom = s - presetButtonH - 12 - Math.max(noteSize * 1.6, noteSize * 1.3);

// no plate or row may run into the settings note plate or into BACK, and no string may overrun its box
const clashes = [];
const notePlate = { top: contentTopY, bottom: contentTopY - (26 + noteBody + 26), left: noteX, right: noteX + noteWidth };
if (difficultiesBottom > notePlate.bottom && noteX < sideMargin + 3 * 165 + 2 * 12)
  clashes.push("the difficulty row runs under the settings note plate");
const longest = "How long a level gives you, and how many targets it asks for. It lands on the next level you start.";
const noteBox = contentWidth;
if (longest.length * noteSize * 0.5 > noteBox)
  clashes.push("the difficulty note needs ~" + (longest.length * noteSize * 0.5).toFixed(0) + "px in " + noteBox + "px");

for (const o of out) {
  const fits = o.bottom >= -1080;
  console.log(
    (o.name + " ").padEnd(34) +
      ("y " + o.top.toFixed(0) + " .. " + o.bottom.toFixed(0)).padEnd(22) +
      (o.top <= 0 && fits ? "ok" : "OFF SCREEN")
  );
}

// the BACK button, bottom-right: top = -(1080 - 46 - 54), bottom = -(1080 - 46)
const backTop = -(1080 - 46 - 54);
const backBottom = -(1080 - 46);
console.log("\nBACK button".padEnd(34) + ("y " + backTop + " .. " + backBottom).padEnd(22) + "x 1660..1860");
console.log("controls content ends at y " + controlsBottom.toFixed(0) + " (bottom margin " + (1080 + controlsBottom).toFixed(0) + "px)");
console.log("settings content ends at y " + settingsBottom.toFixed(0) + " (bottom margin " + (1080 + settingsBottom).toFixed(0) + "px)");
console.log("difficulty row ends at y " + difficultiesBottom.toFixed(0) + "; note plate ends at y " + (contentTopY - (26 + noteBody + 26)).toFixed(0));
console.log(clashes.length ? "CLASHES: " + clashes.join("; ") : "no clashes on the settings page");
console.log("notes are placed " + (sideMargin + (1800 - 420)) + "px wide, so they stop well left of BACK");
