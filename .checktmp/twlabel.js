// The lettering on the three buttons, so the mode readout can be checked against what the scenes actually hold:
// the legacy Text's font, size, alignment, overflow and its own rect.
const fs = require('fs'), path = require('path');
const root = process.argv[2] || '.';

const scenes = [];
for (const dir of fs.readdirSync(path.join(root, 'Assets/Scenes/Levels Scenes'))) {
  const full = path.join(root, 'Assets/Scenes/Levels Scenes', dir);
  if (!fs.statSync(full).isDirectory()) continue;
  for (const f of fs.readdirSync(full)) if (/^TW-.*\.unity$/.test(f)) scenes.push(path.join(full, f));
}
scenes.sort();

for (const file of scenes) {
  const s = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
  const objs = {};
  for (const p of s.split(/^--- /m)) {
    const m = p.match(/^!u!(\d+) &(\d+)/);
    if (m) objs[m[2]] = { cls: m[1], body: p };
  }
  const nameOf = id => {
    const o = objs[id];
    if (!o) return '?';
    return (o.body.match(/m_Name: (.*)/) || [])[1] || '(unnamed)';
  };
  const goOf = id => (objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];

  console.log('===== ' + file.replace(/\\/g, '/'));
  for (const id in objs) {
    const o = objs[id];
    if (o.cls !== '114' || !/guid: 5f7201a12d95ffc409449d95f23cf332/.test(o.body)) continue;   // UnityEngine.UI.Text
    const go = goOf(id);
    const font = o.body.match(/m_Font: \{fileID: (-?\d+), guid: ([0-9a-f]+)/);
    const rect = Object.keys(objs).find(c => objs[c].cls === '224' && goOf(c) === go);
    const size = objs[rect].body.match(/m_SizeDelta: \{x: ([-0-9.e]+), y: ([-0-9.e]+)\}/);
    const anchor = objs[rect].body.match(/m_AnchorMin: \{x: ([-0-9.e]+), y: ([-0-9.e]+)\}/);
    const label = o.body.match(/m_Text: (.*)/);
    console.log('  "' + (label ? label[1] : '') + '"  on ' + nameOf(go) + '  rect ' +
      Number(size[1]).toFixed(0) + 'x' + Number(size[2]).toFixed(0) + ' anchor ' + anchor[1] + ',' + anchor[2] +
      '  font ' + (font ? font[2].slice(0, 8) : '?') +
      '  ' + (o.body.match(/m_FontSize: (.*)/) || [])[1] + 'pt' +
      '  align ' + (o.body.match(/m_Alignment: (.*)/) || [])[1] +
      '  hOverflow ' + (o.body.match(/m_HorizontalOverflow: (.*)/) || [])[1] +
      '  vOverflow ' + (o.body.match(/m_VerticalOverflow: (.*)/) || [])[1] +
      '  bestFit ' + (o.body.match(/m_ResizeTextForBestFit: (.*)/) || [])[1] +
      '  color ' + (o.body.match(/m_Color: \{r: ([-0-9.]+), g: ([-0-9.]+), b: ([-0-9.]+), a: ([-0-9.]+)\}/) || []).slice(1, 5).join(','));
  }
}
