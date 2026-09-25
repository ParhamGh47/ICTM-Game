// Prints the UI hierarchy of a scene: each object's name, its class, and the fields that decide how it looks
// (sprite guid, colour, text, script guid). Used to see what the story scenes' buttons are made of.
const fs = require("fs");

const file = process.argv[2];
const src = fs.readFileSync(file, "utf8").replace(/\r/g, "");
const blocks = src.split(/\n--- /).map((b) => "--- " + b);

const byId = new Map();
for (const b of blocks) {
  const m = b.match(/^--- !u!(\d+) &(\d+)/);
  if (m) byId.set(m[2], { cls: m[1], body: b });
}

const field = (body, name) => {
  const m = body.match(new RegExp("^[ \\t]*" + name + ": ?(.*)$", "m"));
  return m ? m[1].trim() : null;
};

const scripts = new Map(); // script guid -> name, for the ones we know
scripts.set("9ae57e584f7e8b5479449a1e049ae432", "StoryTypeWriter");
scripts.set("f4688fdb7df04437aeb418b961361dc5", "TextMeshProUGUI");
scripts.set("4e29b1a8efbd4b44bb3f3716e73f07ff", "Button");

// -- the m_Children list of a RectTransform: the "- {fileID: n}" lines under it
const childrenOf = (id) => {
  const r = byId.get(id);
  if (!r) return [];

  const lines = r.body.split("\n");
  const at = lines.findIndex((l) => /^[ \t]*m_Children:/.test(l));
  if (at < 0) return [];

  const out = [];
  for (let i = at + 1; i < lines.length; i++) {
    const m = lines[i].match(/^[ \t]*- \{fileID: (\d+)\}/);
    if (!m) break;
    out.push(m[1]);
  }

  return out.filter((c) => byId.get(c) && byId.get(c).cls === "224");
};

// -- find a RectTransform's id by the GameObject's name
const rectForName = (wanted) => {
  for (const [id, b] of byId) {
    if (b.cls !== "224") continue;
    const go = (b.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
    const g = byId.get(go);
    if (g && field(g.body, "m_Name") === wanted) return id;
  }
  return null;
};

function walk(rectId, depth) {
  const r = byId.get(rectId);
  if (!r) return;
  const go = (r.body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const g = byId.get(go);
  const name = g ? field(g.body, "m_Name") : "?";
  const active = g ? field(g.body, "m_ActiveSelf") : null;

  console.log("  ".repeat(depth) + `- ${name} [${go}]`);

  if (g) {
    const comps = [...g.body.matchAll(/- component: \{fileID: (\d+)\}/g)].map((m) => m[1]);
    for (const c of comps) {
      const cc = byId.get(c);
      if (!cc || cc.cls !== "114") continue;
      const script = (field(cc.body, "m_Script") || "").match(/guid: (\w+)/);
      const known = script ? scripts.get(script[1]) || script[1].slice(0, 6) : "?";
      const text = field(cc.body, "m_text");
      const colour = field(cc.body, "m_Color");
      const sprite = (field(cc.body, "m_Sprite") || "").match(/guid: (\w+)/);
      const type = field(cc.body, "m_Type");
      console.log(
        "  ".repeat(depth + 1) +
          `${known}${text !== null ? ` text="${text}"` : ""}${sprite ? ` sprite=${sprite[1].slice(0, 8)}` : ""}${
            colour ? ` colour=${colour}` : ""
          }${type !== null ? ` type=${type}` : ""}`
      );
    }
  }

  for (const child of childrenOf(rectId)) walk(child, depth + 1);
}

console.log("=== " + file.split(/[\\/]/).pop());

const roots = [...byId.values()].filter((b) => b.cls === "224").filter((b) => {
  const parent = (b.body.match(/m_Father: \{fileID: (\d+)\}/) || [])[1];
  return !parent || parent === "0" || !byId.has(parent);
});

for (const r of roots) {
  const id = (r.body.match(/^--- !u!224 &(\d+)/) || [])[1];
  walk(id, 1);
}

for (const name of ["speedup", "skip"]) {
  const rect = rectForName(name);
  if (rect) {
    console.log(`\n=== '${name}' subtree`);
    walk(rect, 1);
  }
}
