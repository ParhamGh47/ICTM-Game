// Reads a TypeWriter scene and lays its UI out the way Unity would, so the text rect and the buttons can be
// compared. Coordinates are the canvas's own (1920x1080 by default, y up from the bottom).
const fs = require("fs");

const file = process.argv[2];
const canvasSize = { x: 1920, y: 1080 };

const src = fs.readFileSync(file, "utf8").replace(/\r/g, "");
const blocks = src.split(/\n--- /).map((b) => "--- " + b);

const byId = new Map();
for (const b of blocks) {
  const m = b.match(/^--- !u!(\d+) &(\d+)/);
  if (m) byId.set(m[2], { cls: m[1], body: b });
}

const field = (body, name) => {
  const m = body.match(new RegExp("^\\s*" + name + ":\\s*(.*)$", "m"));
  return m ? m[1].trim() : null;
};
const vec = (body, name) => {
  const v = field(body, name);
  if (!v) return null;
  const m = v.match(/x:\s*(-?[\d.e+-]+),\s*y:\s*(-?[\d.e+-]+)/);
  return m ? { x: parseFloat(m[1]), y: parseFloat(m[2]) } : null;
};

// GameObject name by fileID
const names = new Map();
for (const [id, b] of byId) {
  if (b.cls === "1") names.set(id, field(b.body, "m_Name"));
}

// RectTransforms: id -> {go, parent, anchorMin, anchorMax, sizeDelta, anchoredPosition, pivot}
const rects = new Map();
for (const [id, b] of byId) {
  if (b.cls !== "224") continue;
  const go = (b.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const parent = (b.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
  rects.set(id, {
    id,
    go,
    parent,
    anchorMin: vec(b.body, "m_AnchorMin"),
    anchorMax: vec(b.body, "m_AnchorMax"),
    sizeDelta: vec(b.body, "m_SizeDelta"),
    anchoredPosition: vec(b.body, "m_AnchoredPosition"),
    pivot: vec(b.body, "m_Pivot"),
  });
}
// gameObject -> its RectTransform
const rectOfGo = new Map();
for (const [id, r] of rects) rectOfGo.set(r.go, r);

function layout(id, parentSize) {
  const r = rects.get(id);
  if (!r) return null;

  const span = { x: (r.anchorMax.x - r.anchorMin.x) * parentSize.x, y: (r.anchorMax.y - r.anchorMin.y) * parentSize.y };
  const size = { x: r.sizeDelta.x + span.x, y: r.sizeDelta.y + span.y };
  const pivotPos = {
    x: r.anchorMin.x * parentSize.x + r.anchoredPosition.x + span.x * r.pivot.x,
    y: r.anchorMin.y * parentSize.y + r.anchoredPosition.y + span.y * r.pivot.y,
  };

  r.size = size;
  r.min = { x: pivotPos.x - size.x * r.pivot.x, y: pivotPos.y - size.y * r.pivot.y };
  r.max = { x: r.min.x + size.x, y: r.min.y + size.y };

  for (const child of rects.values()) {
    if (child.parent === id) layout(child.id, size);
  }

  return r;
}

// The canvas: the RectTransform with no parent (or whose parent is not a UI rect at all)
const root =
  [...rects.values()].find((r) => !r.parent || r.parent === "0") ||
  [...rects.values()].find((r) => !rects.has(r.parent));

if (!root) {
  console.log("rect transforms:", [...rects.values()].map((r) => `${names.get(r.go)}=${r.id} parent ${r.parent}`).join(", "));
  process.exit(1);
}

layout(root.id, canvasSize);

const interesting = (name) => /text|continue|speed|skip|button|panel|cover/i.test(name || "");

console.log("=== " + file.split(/[\\/]/).pop());
for (const r of rects.values()) {
  if (!r.size) continue;
  const name = names.get(r.go) || "?";
  if (!interesting(name)) continue;
  console.log(
    `${name.padEnd(22)} x ${r.min.x.toFixed(0).padStart(5)}..${r.max.x.toFixed(0).padStart(5)}  y ${r.min.y.toFixed(0).padStart(5)}..${r.max.y.toFixed(0).padStart(5)}  ` +
      `size ${r.size.x.toFixed(0)}x${r.size.y.toFixed(0)}`
  );
}

// the TypeWriter's own text object, and what it must clear
const tw = [...byId.values()].find((b) => b.cls === "114" && /StoryTypeWriter/.test(b.body));
if (tw) {
  const go = (tw.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const rect = rectOfGo.get(go);
  console.log(`\nStoryTypeWriter is on '${names.get(go)}' (rect ${rect.id}): y ${rect.min.y.toFixed(0)}..${rect.max.y.toFixed(0)}, x ${rect.min.x.toFixed(0)}..${rect.max.x.toFixed(0)}`);
  const text = field(tw.body, "startText") || "";
  console.log(`  characters: ${(tw.body.match(/startText:/) ? text.length : 0)}`);
}
