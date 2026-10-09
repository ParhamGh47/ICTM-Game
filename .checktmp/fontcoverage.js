// What can the HUD font actually draw?
//
// The boost note is a legacy UnityEngine.UI.Text, which renders with the font it is given and nothing else -
// there is no OS emoji fallback in a build. So whether the rocket emoji works is not a matter of opinion: it
// is whether the font file has the glyph. Read the cmap and the advance widths out of the font itself.
const fs = require('fs');
const path = require('path');

function u16(b, o) { return b.readUInt16BE(o); }
function u32(b, o) { return b.readUInt32BE(o); }

function tables(buf) {
  const out = new Map();
  // A .otf with a CFF outline is still an sfnt: same table directory.
  const num = u16(buf, 4);
  for (let i = 0; i < num; i++) {
    const o = 12 + i * 16;
    const tag = buf.toString('ascii', o, o + 4);
    out.set(tag, { offset: u32(buf, o + 8), length: u32(buf, o + 12) });
  }
  return out;
}

function cmapLookup(buf, t) {
  const cm = t.get('cmap');
  if (!cm) return null;
  const n = u16(buf, cm.offset + 2);
  const subs = [];
  for (let i = 0; i < n; i++) {
    const o = cm.offset + 4 + i * 8;
    const off = cm.offset + u32(buf, o + 4);
    subs.push({
      platform: u16(buf, o),
      encoding: u16(buf, o + 2),
      off,
      format: u16(buf, off),   // read from the subtable itself, not from the record
    });
  }
  // Prefer a full Unicode subtable (format 12 covers beyond the BMP), then a BMP one.
  const pick = subs.find((s) => s.format === 12) || subs.find((s) => s.format === 4) || subs[0];
  if (!pick) return null;

  const base = pick.off;

  if (pick.format === 12) {
    const groups = u32(buf, base + 12);
    return (cp) => {
      for (let i = 0; i < groups; i++) {
        const o = base + 16 + i * 12;
        const s = u32(buf, o);
        const e = u32(buf, o + 4);
        if (cp >= s && cp <= e) return u32(buf, o + 8) + (cp - s);
      }
      return 0;
    };
  }

  if (pick.format === 4) {
    const segX2 = u16(buf, base + 6);
    const segs = segX2 / 2;
    const endO = base + 14;
    const startO = endO + segX2 + 2;
    const deltaO = startO + segX2;
    const rangeO = deltaO + segX2;
    return (cp) => {
      if (cp > 0xffff) return 0;
      for (let i = 0; i < segs; i++) {
        const end = u16(buf, endO + i * 2);
        if (cp > end) continue;
        const start = u16(buf, startO + i * 2);
        if (cp < start) return 0;
        const delta = buf.readInt16BE(deltaO + i * 2);
        const ro = u16(buf, rangeO + i * 2);
        if (ro === 0) return (cp + delta) & 0xffff;
        const g = u16(buf, rangeO + i * 2 + ro + (cp - start) * 2);
        return g === 0 ? 0 : (g + delta) & 0xffff;
      }
      return 0;
    };
  }

  return null;
}

function advance(buf, t, gid) {
  const hm = t.get('hmtx');
  const hh = t.get('hhea');
  if (!hm || !hh) return null;
  const numH = u16(buf, hh.offset + 34);
  if (gid < numH) return u16(buf, hm.offset + gid * 4);
  // Beyond the metrics: the last advance repeats (as the spec says).
  return u16(buf, hm.offset + (numH - 1) * 4);
}

const WANT = {
  '🚀 rocket': 0x1f680,
  '▶ play': 0x25b6,
  '★ star': 0x2605,
  '⚡ bolt': 0x26a1,
  '» guillemet': 0x00bb,
  '• bullet': 0x2022,
  '→ arrow': 0x2192,
  '⬆ up arrow': 0x2b06,
  '× multiply': 0x00d7,
  '✕ cross': 0x2715,
  '➤ arrowhead': 0x27a4,
  '»» double': 0x00bb,
};

const files = process.argv.slice(2);
for (const f of files) {
  const buf = fs.readFileSync(path.join(process.cwd(), f));
  const t = tables(buf);
  const look = cmapLookup(buf, t);
  const head = t.get('head');
  const upem = head ? u16(buf, head.offset + 18) : 1000;
  console.log('=== ' + f + '   unitsPerEm=' + upem + '  tables=' + [...t.keys()].join(','));
  if (!look) { console.log('  (no usable cmap)\n'); continue; }

  const has = [];
  const missing = [];
  for (const [name, cp] of Object.entries(WANT)) {
    const gid = look(cp);
    (gid ? has : missing).push(name + (gid ? ' (gid ' + gid + ')' : ''));
  }
  console.log('  has:     ' + (has.join(', ') || '(none of the candidates)'));
  console.log('  missing: ' + (missing.join(', ') || '(none)'));

  // Width of a candidate label at the note's font size, against the note's 115 px plate.
  const SIZE = 36;
  const width = (s) => {
    let w = 0;
    for (const ch of s) {
      const gid = look(ch.codePointAt(0));
      const a = gid ? advance(buf, t, gid) : 0;
      w += (a * SIZE) / upem;
    }
    return w;
  };
  console.log('  at size ' + SIZE + ' (the note is 115 px wide):');
  for (const s of ['x 3', 'x3', '×3', 'BOOST 3', 'BOOST', '▶ 3', '»3', 'BOOST\n3'])
    console.log('    ' + JSON.stringify(s).padEnd(14) + width(s.replace('\n', '')).toFixed(1) + ' px');

  // A caption small and the count big, in one line: the arrangement being considered, since no glyph that
  // means "boost" exists in the font. Sizes are per run, exactly as a rich-text <size> tag would render them.
  const runWidth = (text, size) => (width(text) * size) / SIZE;
  console.log('  as a composed line (caption small, count at ' + SIZE + '):');
  const composed = [
    ['<size=20>BOOST</size> 3', runWidth('BOOST', 20) + runWidth(' 3', 36)],
    ['<size=24>BOOST</size> 3', runWidth('BOOST', 24) + runWidth(' 3', 36)],
    ['<size=20>BOOST</size> 12', runWidth('BOOST', 20) + runWidth(' 12', 36)],
    ['<size=18>BOOST</size> 12', runWidth('BOOST', 18) + runWidth(' 12', 36)],
  ];
  for (const [s, w] of composed)
    console.log('    ' + s.padEnd(30) + w.toFixed(1) + ' px' + (w <= 115 ? '  fits' : '  TOO WIDE'));

  const hh = t.get('hhea');
  if (hh) {
    const asc = buf.readInt16BE(hh.offset + 4);
    const desc = buf.readInt16BE(hh.offset + 6);
    const gap = buf.readInt16BE(hh.offset + 8);
    const line = ((asc - desc + gap) * SIZE) / upem;
    console.log(
      '  hhea asc=' + asc + ' desc=' + desc + ' gap=' + gap +
        ' -> one line at ' + SIZE + ' is ' + line.toFixed(1) + ' px tall (the note is 77 px)'
    );
  }
  console.log('');

  void head;
}
