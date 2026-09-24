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

// settings page
let s = contentTopY;
mark("caption graphics", s, captionHeight);
s -= captionHeight + 10;
mark("presets", s, presetButtonH);
s -= presetButtonH + groupGap;
mark("shadows", s, switchH);
s -= switchH + 14;
mark("motion blur", s, switchH);
s -= switchH + groupGap;
mark("settings note", s, Math.max(noteSize * 1.6, noteSize * 1.3));
const settingsBottom = s - noteSize * 1.6;

// the blurb panel on the settings page (height depends on wrapped text, so it is approximated)
const blurbText = (src.match(/presetsBlurb =([\s\S]*?);\n/) || [])[1] || "";
const blurbChars = (blurbText.match(/"([^"]*)"/g) || []).map((x) => x.length).reduce((a, b) => a + b, 0);
const blurbInner = 740 - 52;
const blurbLines = Math.ceil(blurbChars / (blurbInner / (noteSize * 0.52)));
const blurbHeight = 26 + captionHeight + 14 + blurbLines * noteSize * 1.25 + 26;
mark("blurb panel (approx)", contentTopY, blurbHeight);

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
console.log("notes are placed " + (sideMargin + (1800 - 420)) + "px wide, so they stop well left of BACK");
