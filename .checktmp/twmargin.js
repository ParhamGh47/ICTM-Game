// Gives every typewriter scene's story text a bottom margin, so the last line of a page stops above the row
// of buttons instead of running behind it.
//
// The numbers, in the canvas' own coordinates: the text's rect is 972x913 at y 11 (so it spans -446..468),
// the three buttons are 121 tall with their tops at -345, and the TMP margins are the rect minus the margin -
// so the text's bottom edge is the rect's bottom plus w. The authored w was -18.29, which pushed the text
// *past* the rect and left the last lines sitting across the button row. 126 puts the bottom edge at -320:
// 25 px of air above the plates, and the top edge and the first line are untouched, so only pagination changes.
const fs = require('fs'), path = require('path');
const root = process.argv[2] || '.';

const from = 'm_margin: {x: 0, y: -2.6524048, z: -275.1338, w: -18.291046}';
const to = 'm_margin: {x: 0, y: -2.6524048, z: -275.1338, w: 126}';

const scenes = [];
for (const dir of fs.readdirSync(path.join(root, 'Assets/Scenes/Levels Scenes'))) {
  const full = path.join(root, 'Assets/Scenes/Levels Scenes', dir);
  if (!fs.statSync(full).isDirectory()) continue;
  for (const f of fs.readdirSync(full)) if (/^TW-.*\.unity$/.test(f)) scenes.push(path.join(full, f));
}
scenes.sort();

for (const file of scenes) {
  let text = fs.readFileSync(file, 'utf8');
  const before = text.split(from).length - 1;
  if (before !== 1) {
    console.log('SKIPPED ' + file + ' (' + before + ' matches)');
    continue;
  }
  text = text.replace(from, to);
  fs.writeFileSync(file, text);
  console.log('updated ' + file);
}
