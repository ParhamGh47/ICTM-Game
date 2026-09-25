// The speed-up button and everything under it, component by component, so its art can be tinted with
// certainty rather than by guesswork.
const fs = require('fs'), path = require('path');
const root = process.argv[2] || '.';
const file = path.join(root, 'Assets/Scenes/Levels Scenes/1/TW-Start-1.unity');
const s = fs.readFileSync(file, 'utf8').replace(/\r/g, '');
const parts = s.split(/^--- /m);
const objs = {};
for (const p of parts) {
  const m = p.match(/^!u!(\d+) &(\d+)/);
  if (m) objs[m[2]] = { cls: m[1], body: p };
}
const nameOf = id => (objs[id] ? (objs[id].body.match(/m_Name: (.*)/) || [])[1] : '?');
const goOfComp = id => (objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];

// find the gameobject named speedup that has a child of the same name
let btnGo = null;
for (const id in objs) {
  if (objs[id].cls !== '1') continue;
  if (nameOf(id) !== 'speedup') continue;
  btnGo = id;
}
console.log('gameobjects named speedup: ' + Object.keys(objs).filter(i => objs[i].cls === '1' && nameOf(i) === 'speedup').join(', '));

for (const goId of Object.keys(objs).filter(i => objs[i].cls === '1' && nameOf(i) === 'speedup')) {
  console.log('\n########## GameObject ' + goId + ' (' + nameOf(goId) + ')');
  const comps = objs[goId].body.split('m_Component:')[1].split('m_Layer:')[0];
  const ids = [...comps.matchAll(/fileID: (\d+)/g)].map(m => m[1]);
  for (const cid of ids) {
    const c = objs[cid];
    if (!c) continue;
    console.log('--- comp ' + cid + ' class ' + c.cls);
    let body = c.body;
    if (c.cls === '114') {
      const g = body.match(/m_Script: \{fileID: (-?\d+), guid: ([0-9a-f]+)/);
      const known = {
        'fe87c0e1cc204ed48ad3b37840f39efc': 'Image', '4e29b1a8c1e64e34a97cf9f39b7c3f3f': 'Button',
        'cfabb0440166ab443bba8876756fdfa9': 'Shadow', 'f4688fdb7df04437aeb418b961361dc5': 'TextMeshProUGUI',
        '5f7201a12d95ffc409449d95f23cf332': '?5f7201', '9ae57e584f7e8b5479449a1e049ae432': 'StoryTypeWriter',
      };
      console.log('    script ' + (known[g[2]] || g[2]));
    }
    const keep = /m_Sprite|m_Color|m_Type|m_Material|m_Text|m_FontSize|m_Enabled|m_RaycastTarget|m_Interactable|m_Transition|m_TargetGraphic|m_NormalColor|m_HighlightedColor|m_PressedColor|m_SelectedColor|m_DisabledColor|m_ColorMultiplier|m_FadeDuration|m_Script|m_Maskable|m_EffectColor|m_EffectDistance|m_UseGraphicAlpha|m_SizeDelta|m_AnchoredPosition|m_AnchorMin|m_AnchorMax|m_Pivot|m_LocalRotation/;
    console.log(body.split('\n').filter(l => keep.test(l)).join('\n'));
  }
}
