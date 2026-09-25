// Checks every typewriter scene's story text against the row of buttons under it.
//
// The arithmetic is TMP's own: m_marginWidth = rect.width - margin.x - margin.z and
// m_marginHeight = rect.height - margin.y - margin.w (TMP_Text.ComputeMarginSize), the text is placed from the
// rect's top-left corner, and it paginates when a line would take it past m_marginHeight. So the text's box is
// the rect inset by each margin - a negative margin grows it past the rect - and the last line of a page sits
// on the box's bottom edge. Everything below is in the canvas' coordinates, which is what the scene's numbers
// are written in.
const fs = require('fs'), path = require('path');
const root = process.argv[2] || '.';

const scenes = [];
for (const dir of fs.readdirSync(path.join(root, 'Assets/Scenes/Levels Scenes'))) {
  const full = path.join(root, 'Assets/Scenes/Levels Scenes', dir);
  if (!fs.statSync(full).isDirectory()) continue;
  for (const f of fs.readdirSync(full)) if (/^TW-.*\.unity$/.test(f)) scenes.push(path.join(full, f));
}
scenes.sort();

let worst = Infinity;

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
    const m = o.body.match(/m_Name: (.*)/);
    return m ? m[1] : '(unnamed)';
  };
  const goOf = id => (objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
  const rectOf = goId => Object.keys(objs).find(id => objs[id].cls === '224' && goOf(id) === goId);
  const vec = (body, key) => {
    const m = body.match(new RegExp('^  ' + key + ': \\{x: ([-0-9.e]+), y: ([-0-9.e]+)\\}', 'm'));
    return m ? [Number(m[1]), Number(m[2])] : null;
  };
  const box = goId => {
    const r = objs[rectOf(goId)];
    if (!r) return null;
    const size = vec(r.body, 'm_SizeDelta'), pos = vec(r.body, 'm_AnchoredPosition');
    return {
      left: pos[0] - size[0] / 2, right: pos[0] + size[0] / 2,
      top: pos[1] + size[1] / 2, bottom: pos[1] - size[1] / 2,
    };
  };

  console.log('===== ' + file.replace(/\\/g, '/'));

  let textBottom = null, textTop = null;
  for (const id in objs) {
    if (objs[id].cls !== '114' || !/guid: f4688fdb/.test(objs[id].body)) continue;    // TextMeshProUGUI
    const b = box(goOf(id));
    const mg = objs[id].body.match(/m_margin: \{x: ([-0-9.]+), y: ([-0-9.]+), z: ([-0-9.]+), w: ([-0-9.]+)\}/).slice(1).map(Number);
    textTop = b.top - mg[1];
    textBottom = b.bottom + mg[3];
    console.log('  text box  x ' + b.left.toFixed(0) + '..' + b.right.toFixed(0) + '  y ' + b.bottom.toFixed(0) + '..' + b.top.toFixed(0) +
      '   margins L/T/R/B ' + mg.join('/') + '   -> text area y ' + textBottom.toFixed(1) + '..' + textTop.toFixed(1));
  }

  // The plates, told apart from the labels under them by the Button component itself.
  let buttonTop = -Infinity, buttonLeft = Infinity;
  const plates = [];
  for (const id in objs) {
    if (objs[id].cls !== '1') continue;                                  // GameObject
    if (!/^(cont|speedup|skip)$/.test(nameOf(id))) continue;
    const comps = [...objs[id].body.split('m_Component:')[1].split('m_Layer:')[0].matchAll(/fileID: (\d+)/g)].map(m => m[1]);
    const isButton = comps.some(c => objs[c] && /guid: 4e29b1a8/.test(objs[c].body));
    if (!isButton) continue;
    const b = box(id);
    plates.push(nameOf(id) + ' x ' + b.left.toFixed(0) + '..' + b.right.toFixed(0) + ' y ' + b.bottom.toFixed(0) + '..' + b.top.toFixed(0));
    if (b.top > buttonTop) buttonTop = b.top;
    if (b.left < buttonLeft) buttonLeft = b.left;
  }
  for (const p of plates) console.log('  plate     ' + p);
  console.log('  buttons   ' + plates.length + ' plates, top edge y ' + buttonTop.toFixed(1) + ', left edge x ' + buttonLeft.toFixed(0));

  // Up is +y, so the text's bottom edge being above the buttons' top edge is textBottom > buttonTop.
  const gap = textBottom - buttonTop;
  if (gap < worst) worst = gap;
  console.log('  ' + (gap > 0 ? 'clear' : 'OVERLAP') + ': the last line ends ' + Math.abs(gap).toFixed(1) +
    ' px ' + (gap > 0 ? 'above' : 'below') + ' the top of the button row');
}

console.log('\nworst clearance across the scenes: ' + worst.toFixed(1) + ' px');
