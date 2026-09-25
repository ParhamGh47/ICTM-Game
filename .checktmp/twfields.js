// Prints each typewriter scene's StoryTypeWriter fields (text, button references) the way the inspector
// shows them, so the changes below can be checked against what the scenes actually hold.
const fs = require('fs'), path = require('path');
const root = process.argv[2] || '.';
const guid = fs.readFileSync(path.join(root, 'Assets/Scripts/UI/StoryTypeWriter.cs.meta'), 'utf8').match(/guid: ([0-9a-f]+)/)[1];

const scenes = [];
for (const dir of fs.readdirSync(path.join(root, 'Assets/Scenes/Levels Scenes'))) {
  const full = path.join(root, 'Assets/Scenes/Levels Scenes', dir);
  if (!fs.statSync(full).isDirectory()) continue;
  for (const f of fs.readdirSync(full)) if (/^TW-.*\.unity$/.test(f)) scenes.push(path.join(full, f));
}
scenes.sort();

for (const file of scenes) {
  const s = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
  const parts = s.split(/^--- /m);
  const objs = {};
  for (const p of parts) {
    const m = p.match(/^!u!(\d+) &(\d+)/);
    if (m) objs[m[2]] = { cls: m[1], body: p };
  }
  const nameOf = id => {
    const o = objs[id];
    if (!o) return '?';
    const m = o.body.match(/m_Name: (.*)/);
    return m ? m[1] : '(unnamed)';
  };
  const owner = body => nameOf((body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1]);
  console.log('===== ' + file.replace(/\\/g, '/'));
  for (const id in objs) {
    const o = objs[id];
    if (o.cls !== '114' || !o.body.includes(guid)) continue;
    console.log('  on ' + owner(o.body));
    const text = o.body.match(/startText: (.*)/);
    console.log('    cps=' + (o.body.match(/charactersPerSecond: (.*)/) || [])[1] +
      '  speedUpMultiplier=' + (o.body.match(/speedUpMultiplier: (.*)/) || [])[1]);
    const cb = o.body.match(/continueButton: \{fileID: (-?\d+)\}/);
    const sb = o.body.match(/speedUpButton: \{fileID: (-?\d+)\}/);
    console.log('    continueButton=' + (cb ? nameOf(cb[1]) + ' (' + cb[1] + ')' : 'null') +
      '  speedUpButton=' + (sb ? nameOf(sb[1]) + ' (' + sb[1] + ')' : 'null'));
    if (text) console.log('    startText: ' + JSON.stringify(text[1]).slice(0, 140) + (text[1].length > 120 ? '…' : ''));
  }
}
