// Rewrite the old single-value difficulty fields (targetTimeSeconds / targetKills) into the three
// per-difficulty fields, in both the tracker prefab and every level scene. Idempotent: a file whose old
// field is already gone is left alone, so it can be re-run.
const fs = require("fs");

const tables = {
  "Assets/Prefabs/Utils/CanvasUI.prefab": { time: [250, 200, 160], kills: [7, 10, 14] },
  "Assets/Scenes/Levels Scenes/1/Core-1.unity": { time: [300, 240, 192] },
  "Assets/Scenes/Levels Scenes/2/Core-2.unity": { time: [200, 160, 128] },
  "Assets/Scenes/Levels Scenes/3/Core-3.unity": { time: [225, 180, 144], kills: [2, 3, 4] },
  "Assets/Scenes/Levels Scenes/4/Core-4.unity": { time: [263, 210, 168], kills: [5, 7, 9] },
  "Assets/Scenes/Template.unity": { time: [38, 30, 24] },
};

const timeNames = ["easyTargetTimeSeconds", "mediumTargetTimeSeconds", "hardTargetTimeSeconds"];
const killNames = ["easyTargetKills", "mediumTargetKills", "hardTargetKills"];

function patchPrefab(text, table) {
  let out = text;
  if (table.time) {
    out = out.replace(
      /^( *)targetTimeSeconds: .*$/m,
      (_, ind) => timeNames.map((n, i) => `${ind}${n}: ${table.time[i]}`).join("\n")
    );
  }
  if (table.kills) {
    out = out.replace(
      /^( *)targetKills: .*$/m,
      (_, ind) => killNames.map((n, i) => `${ind}${n}: ${table.kills[i]}`).join("\n")
    );
  }
  return out;
}

function patchScene(text, table) {
  const block = (src, prop, names, values) => {
    const re = new RegExp(
      "( *)- target: \\{fileID: (-?\\d+), guid: ([0-9a-f]+), type: 3\\}\n" +
        " *propertyPath: " + prop + "\n" +
        " *value: [^\\n]*\n" +
        " *objectReference: \\{fileID: -?\\d+\\}"
    );
    return src.replace(re, (m, indent, fileID, guid) => {
      return names
        .map(
          (n, i) =>
            `${indent}- target: {fileID: ${fileID}, guid: ${guid}, type: 3}\n` +
            `${indent}  propertyPath: ${n}\n` +
            `${indent}  value: ${values[i]}\n` +
            `${indent}  objectReference: {fileID: 0}`
        )
        .join("\n");
    });
  };

  let out = text;
  if (table.time) out = block(out, "targetTimeSeconds", timeNames, table.time);
  if (table.kills) out = block(out, "targetKills", killNames, table.kills);
  return out;
}

for (const [file, table] of Object.entries(tables)) {
  const raw = fs.readFileSync(file, "utf8");
  const patched = file.endsWith(".prefab") ? patchPrefab(raw, table) : patchScene(raw, table);
  if (patched === raw) {
    console.log(`-- ${file}: already done`);
    continue;
  }
  fs.writeFileSync(file, patched, "utf8");
  console.log(`ok ${file}`);
}
