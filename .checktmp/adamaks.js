const fs = require("fs");
const path = require("path");

const scene = process.argv[2];
const root = path.resolve(__dirname, "..");

// map prefab guid -> path for Adamak prefabs
const adamakDir = path.join(root, "Assets/Prefabs/Adamaks");
const guidToName = {};
for (const f of fs.readdirSync(adamakDir)) {
  if (!f.endsWith(".prefab.meta")) continue;
  const m = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(path.join(adamakDir, f), "utf8"));
  if (m) guidToName[m[1]] = f.replace(".prefab.meta", "");
}
console.log("Adamak prefab guids:", Object.keys(guidToName).length);

const raw = fs.readFileSync(scene, "utf8");
const chunks = raw.split(/\r?\n(?=--- !u!)/);

let count = 0;
for (const c of chunks) {
  const head = c.split(/\r?\n/)[0];
  if (!/1001/.test(head)) continue;               // PrefabInstance
  const src = /m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})/.exec(c);
  if (!src || !guidToName[src[1]]) continue;
  count++;
  const pos = /propertyPath: m_LocalPosition\.(x|y|z)\s*\r?\n\s*value: (-?[0-9.eE+-]+)/g;
  const p = {};
  let m;
  while ((m = pos.exec(c))) p[m[1]] = parseFloat(m[2]);
  const name = /propertyPath: m_Name\s*\r?\n\s*value: (.*)/.exec(c);
  const kill = /propertyPath: killDisplay[\s\S]{0,120}?objectReference: \{fileID: (-?\d+)/.exec(c);
  const scale = /propertyPath: m_LocalScale\.(x|y|z)\s*\r?\n\s*value: (-?[0-9.eE+-]+)/g;
  const s = {};
  while ((m = scale.exec(c))) s[m[1]] = parseFloat(m[2]);
  console.log(
    `${guidToName[src[1]]} pos=(${p.x ?? "?"},${p.y ?? "?"},${p.z ?? "?"}) ` +
    `scale=(${s.x ?? "-"},${s.y ?? "-"},${s.z ?? "-"}) ` +
    `killDisplay=${kill ? kill[1] : "inherit"}`
  );
}
console.log("total Adamak instances:", count);
