// Template.unity is CRLF, so it needs the newline-aware version of the tracker patch.
const fs = require("fs");
const file = "Assets/Scenes/Template.unity";
let text = fs.readFileSync(file, "utf8");
const nl = text.includes("\r\n") ? "\r\n" : "\n";

const names = ["easyTargetTimeSeconds", "mediumTargetTimeSeconds", "hardTargetTimeSeconds"];
const values = [38, 30, 24];

const re = new RegExp(
  "( *)- target: \\{fileID: (-?\\d+), guid: ([0-9a-f]+), type: 3\\}\\r?\\n" +
    " *propertyPath: targetTimeSeconds\\r?\\n" +
    " *value: [^\\r\\n]*\\r?\\n" +
    " *objectReference: \\{fileID: -?\\d+\\}"
);

const before = text;
text = text.replace(re, (m, indent, fileID, guid) =>
  names
    .map(
      (n, i) =>
        `${indent}- target: {fileID: ${fileID}, guid: ${guid}, type: 3}${nl}` +
        `${indent}  propertyPath: ${n}${nl}` +
        `${indent}  value: ${values[i]}${nl}` +
        `${indent}  objectReference: {fileID: 0}`
    )
    .join(nl)
);

if (text === before) {
  console.log("!! Template: no change");
} else {
  fs.writeFileSync(file, text, "utf8");
  console.log("ok Template");
}
