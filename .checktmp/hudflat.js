// Flat dump of the CanvasUI prefab's hierarchy: every RectTransform with its anchor/offsets/size, plus
// each Text's wording, font size, alignment, colour and shadow. Read-only.
const fs = require('fs');

const PREFAB = process.argv[2] || 'Assets/Prefabs/Utils/CanvasUI.prefab';
const raw = fs.readFileSync(PREFAB, 'utf8');
const docs = raw.split(/\r?\n(?=--- !u!\d+ &)/);

const byId = new Map();
for (const text of docs) {
  const m = text.match(/^--- !u!(\d+) &(\d+)/);
  if (!m) continue;
  byId.set(m[2], { cls: m[1], text });
}

const field = (block, name) => {
  const m = block.match(new RegExp('^ *' + name + ': ?(.*)$', 'm'));
  return m ? m[1].trim() : null;
};

const compsOf = id => {
  const d = byId.get(id);
  if (!d || d.cls !== '1') return [];
  return [...d.text.matchAll(/- component: \{fileID: (\d+)\}/g)].map(m => m[1]);
};

const rtOf = goId => compsOf(goId).map(c => byId.get(c)).find(d => d && d.cls === '224');

const childrenOf = goId => {
  const rt = rtOf(goId);
  if (!rt) return [];
  const m = rt.text.match(/m_Children:\r?\n((?:  - \{fileID: \d+\}\r?\n)*)/);
  if (!m) return [];
  return [...m[1].matchAll(/fileID: (\d+)/g)].map(x => byId.get(x[1]))
    .filter(d => d && d.cls === '224')
    .map(d => (field(d.text, 'm_GameObject') || '').match(/\d+/))
    .filter(Boolean)
    .map(x => x[0]);
};

function rect(goId) {
  const rt = rtOf(goId);
  if (!rt) return null;
  const g = n => field(rt.text, n);
  return {
    anchorMin: g('m_AnchorMin'), anchorMax: g('m_AnchorMax'),
    pos: g('m_AnchoredPosition'), size: g('m_SizeDelta'),
    pivot: g('m_Pivot'), scale: g('m_LocalScale'), rot: g('m_LocalRotation'),
  };
}

const CLASS = { '1': 'GO', '224': 'RT', '222': 'CR', '114': 'MB', '223': 'CANVAS', '20': 'CAM' };
const classesOf = id => compsOf(id).map(c => (CLASS[(byId.get(c) || {}).cls] || (byId.get(c) || {}).cls)).join('/');

function details(goId) {
  const out = [];
  for (const c of compsOf(goId)) {
    const d = byId.get(c);
    if (!d || d.cls !== '114') continue;
    const size = field(d.text, 'm_FontSize');
    if (size) {
      out.push(`TEXT "${field(d.text, 'm_Text')}" size=${size} align=${field(d.text, 'm_Alignment')} hOv=${field(d.text, 'm_HorizontalOverflow')} vOv=${field(d.text, 'm_VerticalOverflow')} bestFit=${field(d.text, 'm_BestFit')}`);
      out.push(`colour=${field(d.text, 'm_Color')} font=${field(d.text, 'm_Font')} style=${field(d.text, 'm_FontStyle')} spacing=${field(d.text, 'm_LineSpacing')}`);
    }
    const label = field(d.text, 'label');
    if (label !== null) out.push(`DISPLAY label=${JSON.stringify(label)}`);
    const sprite = field(d.text, 'm_Sprite');
    if (sprite && sprite !== '{fileID: 0}') out.push(`IMAGE sprite=${sprite} colour=${field(d.text, 'm_Color')} type=${field(d.text, 'm_Type')} preserveAspect=${field(d.text, 'm_PreserveAspect')}`);
    const eff = field(d.text, 'm_EffectColor');
    if (eff) out.push(`shadow=${eff} dist=${field(d.text, 'm_EffectDistance')} useShadow=${field(d.text, 'm_UseGraphicAlpha')}`);
  }
  return out;
}

function walk(goId, depth) {
  const d = byId.get(goId);
  if (!d) { console.log(' '.repeat(depth) + '?' + goId); return; }
  const pad = '  '.repeat(depth);
  const r = rect(goId);
  const parts = [];
  if (r) {
    parts.push(`min${r.anchorMin} max${r.anchorMax}`, `pos${r.pos}`, `size${r.size}`);
    if (r.pivot !== '{x: 0.5, y: 0.5}') parts.push(`pivot${r.pivot}`);
    if (r.scale !== '{x: 1, y: 1, z: 1}') parts.push(`scale${r.scale}`);
    if (r.rot !== '{x: 0, y: 0, z: 0, w: 1}') parts.push(`rot${r.rot}`);
  }
  console.log(`${pad}${classesOf(goId).padEnd(16)} ${field(d.text, 'm_Name')}  ${parts.join('  ')}`);
  for (const x of details(goId)) console.log(`${pad}    . ${x}`);
  for (const c of childrenOf(goId)) walk(c, depth + 1);
}

const ROOT = process.argv[3] || '6890820111944379311';
console.log('root:', field(byId.get(ROOT).text, 'm_Name'), JSON.stringify(rect(ROOT)),
  'canvasScaler=', JSON.stringify(compsOf(ROOT).map(c => (byId.get(c) || {}).cls)));
for (const c of childrenOf(ROOT)) walk(c, 0);
