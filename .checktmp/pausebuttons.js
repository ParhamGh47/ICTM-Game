// Dump the pause panel's buttons: labels, onClick targets, plate colours, and the window's own Image.
const fs = require("fs");

const scenePath = process.argv[2] || "Assets/Prefabs/Utils/CanvasUI.prefab";
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

const guids = {
  fe87c0e1cc204ed48ad3b37840f39efc: "Image",
  "4e29b1a8efbd4b44bb3f3716e73f07ff": "Button",
  cfabb0440166ab443bba8876756fdfa9: "TMP?",
  "5f7201a12d95ffc409449d95f23cf332": "Text",
};

function components(goId) {
  const go = objs.get(goId);
  if (!go) return [];
  return (go.part.match(/- component: \{fileID: (\d+)\}/g) || []).map((c) => c.match(/\d+/)[0]);
}

for (const o of objs.values()) {
  if (o.cls !== "114" || !o.part.includes(pauseGuid)) continue;
  console.log("PauseManager script guid " + pauseGuid);
  const fields = ["pausePanel", "controlsPanel", "pauseMusic"];
  for (const key of fields) {
    const id = g(o.part, key);
    const target = id && objs.get(id);
    console.log(`  ${key} = ${id} (${target && target.name})`);
  }
  console.log("  --- raw script fields ---");
  console.log(
    o.part
      .split("\n")
      .filter((l) => /^  [a-zA-Z]/.test(l))
      .join("\n")
  );
}

// Buttons and what they invoke
const buttons = [];
for (const o of objs.values()) {
  if (o.cls !== "114") continue;
  if (!o.part.includes("4e29b1a8efbd4b44bb3f3716e73f07ff")) continue;
  if (o.part.includes(pauseGuid)) continue;
  buttons.push(o);
}

for (const b of buttons) {
  const go = objs.get(b.go);
  const onClick = b.part.match(/m_OnClick:[\s\S]*?m_PersistentCalls:[\s\S]*?m_Calls:([\s\S]*?)\n  m_/);
  const calls = onClick ? onClick[1] : "";
  const method = (calls.match(/m_MethodName: (.*)$/m) || [])[1];
  const targetId = (calls.match(/m_Target: \{fileID: (\d+)\}/) || [])[1];
  const targetGo = targetId && objs.get(targetId);
  console.log(
    `BUTTON ${go.name} -> ${method || "(nothing)"}  target=${targetGo ? targetGo.name : "-"}`
  );
}

// The window plates: images on the pause panel and controls panel
for (const [label, idOfPanel] of [
  ["pausePanel", "6890820110232687943"],
  ["controlsPanel", "6890820110302793911"],
]) {
  const comps = components(idOfPanel);
  console.log(`\n=== ${label} components`);
  for (const c of comps) {
    const co = objs.get(c);
    if (!co) continue;
    const scriptGuid = (co.part.match(/m_Script: \{fileID: 11500000, guid: ([0-9a-f]+)/) || [])[1];
    const kind = co.cls === "224" ? "RectTransform" : guids[scriptGuid] || scriptGuid || co.cls;
    const color = val(co.part, "m_Color");
    console.log(`  ${kind} ${c} color=${color || ""} sprite=${g(co.part, "m_Sprite") || ""}`);
  }
}
