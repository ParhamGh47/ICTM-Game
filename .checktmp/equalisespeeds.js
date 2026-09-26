// Gives every painted passing car in a Core scene the same cruising speed.
//
// The two painted paths share both lanes, so while the reverse cars were painted ~10% faster than the forward
// ones the faster platoon slowly overtook the slower one in the same lane: within a minute of play a car was
// tailgating the one in front of it, and shortly after that shunting it. One speed freezes the fleet's
// relative positions, which is what the painter does now for anything painted later.
//
// Only the value line that belongs to a `propertyPath: speedKPH` is touched, and every rewrite is counted and
// checked before anything is written.
const fs = require("fs");

const files = process.argv.slice(2).filter((a) => a.endsWith(".unity"));
const dryRun = process.argv.includes("--dry-run");

for (const file of files) {
  const text = fs.readFileSync(file, "utf8");
  const eol = text.includes("\r\n") ? "\r\n" : "\n";
  const lines = text.split(/\r?\n/);

  // Every speedKPH override in the scene, as (line index of the value, value).
  const found = [];
  for (let i = 0; i < lines.length - 1; i++) {
    if (!/^\s*propertyPath: speedKPH\s*$/.test(lines[i])) continue;
    const m = lines[i + 1].match(/^(\s*value: )([-\d.eE]+)\s*$/);
    if (m) found.push({ line: i + 1, value: +m[2], indent: m[1] });
  }

  if (found.length === 0) {
    console.log(file.split("/").slice(-2).join("/") + ": no painted-car speeds found");
    continue;
  }

  const values = [...new Set(found.map((f) => f.value))].sort((a, b) => a - b);
  const target = Math.round((values.reduce((a, b) => a + b, 0) / values.length) * 10) / 10;

  // Sanity: the speed values must not appear anywhere else in the file, or a plain replace would be wrong.
  for (const v of values) {
    const hits = lines.filter((l) => new RegExp("value: " + String(v).replace(".", "\\.") + "\\s*$").test(l)).length;
    const expected = found.filter((f) => f.value === v).length;
    if (hits !== expected) {
      console.log(
        "   WARNING: " + v + " appears on " + hits + " value lines but " + expected + " speedKPH overrides"
      );
    }
  }

  console.log(
    file.split("/").slice(-2).join("/") +
      ": " + found.length + " cars, speeds " + values.join("/") + " -> " + target
  );

  if (dryRun) continue;

  const before = lines.slice();
  for (const f of found) lines[f.line] = f.indent + target;

  // Nothing but those lines may differ.
  let changed = 0;
  for (let i = 0; i < lines.length; i++) if (lines[i] !== before[i]) changed++;
  if (changed !== found.length) {
    console.log("   REFUSING: " + changed + " lines would change, expected " + found.length);
    continue;
  }

  fs.writeFileSync(file, lines.join(eol));
  console.log("   written (" + changed + " lines)");
}
