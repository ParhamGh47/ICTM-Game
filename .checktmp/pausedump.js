// Dumps the pause panel hierarchy of a level scene so the new options content can be fitted into it.
const fs = require("fs");

const scenePath = process.argv[2] || "Assets/Scenes/Levels Scenes/1/Core-1.unity";
const pauseGuid = "c7d2837cbc61ba4449ec51b8788ca18c";

const text = fs.readFileSync(scenePath, "utf8").replace(/\r/g, "");
const objs = new Map();
for (const part of text.split(/^--- /m)) {
  const m = part.match(/^!u!(\d+) &(\d+)/);
  if (!m) continue;
  const name = (part.match(/^  m_Name: (.*)$/m) || [])[1];
  const go = (part.match(/^  m_GameObject: \{fileID: (\d+)\}/m) || [])[1];
  objs.set(m[2], { cls: m[1], id: m[2], name, go, part });
}

const g = (part, key) => (part.match(new RegExp("^  " + key + ": \\{fileID: (\\d+)\\}", "m")) || [])[1];
const val = (part, key) => (part.match(new RegExp("^  " + key + ": ?(.*)$", "m")) || [])[1];

// children of a transform id
const isTransform = (o) => o.cls === "4" || o.cls === "224";

function children(transformId) {
  const out = [];
  for (const o of objs.values()) {
    if (isTransform(o) && g(o.part, "m_Father") === transformId) out.push(o);
  }
  return out;
}

function rectInfo(transformId) {
  const t = objs.get(transformId);
  if (!t) return "no transform";
  const p = t.part;
  const anchorMin = val(p, "m_AnchorMin");
  const anchorMax = val(p, "m_AnchorMax");
  const size = val(p, "m_SizeDelta");
  const pos = val(p, "m_AnchoredPosition");
  return `anchors ${anchorMin} -> ${anchorMax}  size ${size}  pos ${pos}`;
}

function components(goId) {
  const go = objs.get(goId);
  if (!go) return [];
  return (go.part.match(/- component: \{fileID: (\d+)\}/g) || []).map((c) => c.match(/\d+/)[0]);
}

function describe(goId, depth) {
  const go = objs.get(goId);
  if (!go) return;
  const comps = components(goId);
  const transform = comps.find((c) => objs.get(c) && isTransform(objs.get(c)));
  const texts = comps
    .filter((c) => objs.get(c) && ["114"].includes(objs.get(c).cls))
    .map((c) => {
      const t = objs.get(c);
      const txt = val(t.part, "m_Text");
      const script = (t.part.match(/m_Script: \{fileID: 11500000, guid: ([0-9a-f]+)/) || [])[1];
      return script === "5f7201a12d95ffc409449d95f23cf332" || txt ? `text="${txt}"` : `mb:${script}`;
    });

  console.log(
    "  ".repeat(depth) + "- " + (go.name || "?").padEnd(28) + rectInfo(transform) + "   " + texts.join(" ")
  );

  for (const child of children(transform)) describe(child.go, depth + 1);
}

// the PauseMenu components and what they point at
for (const o of objs.values()) {
  if (o.cls !== "114" || !o.part.includes(pauseGuid)) continue;
  const go = objs.get(o.go);
  console.log("PauseMenu on: " + (go && go.name));
  console.log("  pausePanel    -> " + val(o.part, "pausePanel"));
  console.log("  controlsPanel -> " + val(o.part, "controlsPanel"));

  for (const key of ["pausePanel", "controlsPanel"]) {
    const id = g(o.part, key);
    if (!id) continue;
    const panelGo = objs.get(id);
    const comps = components(id);
    const transform = comps.find((c) => objs.get(c) && isTransform(objs.get(c)));
    console.log("\n=== " + key + " = " + (panelGo && panelGo.name) + " (go " + id + ")");
    console.log("    " + rectInfo(transform));
    for (const child of children(transform)) describe(child.go, 1);
  }
}
