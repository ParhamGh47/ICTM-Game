// The customize screen is built downwards from a single y, so its layout can be checked exactly without
// running Unity. This reproduces that arithmetic with the values the scene actually stores and reports where
// every piece lands, flagging anything off the bottom of the 1080-tall reference screen or overlapping.
//
// Run: node .checktmp/customizelayout.js

const REFERENCE_HEIGHT = 1080;

// From Assets/Scenes/Customize.unity.
const v = {
  partTopY: -156,
  columnTopY: -200,
  captionSize: 22,
  captionGap: 8,
  sectionGap: 24,
  styleHeight: 46,
  styleGap: 10,
  styleColumns: 3,
  styleCount: 6,
  swatchSize: 68,
  swatchGap: 10,
  paletteColumns: 5,
  paletteTotal: 20,          // 19 preset colours plus Default
  sideMargin: 60,
  previewSwatchHeight: 30,
  pickerFieldHeight: 120,
  pickerHueHeight: 22,
  pickerGap: 12,
  numberRowGap: 8,
  numberCaptionSize: 15,
  numberFieldHeight: 34,
  partHeight: 48,
  partGap: 10,
  partCount: 9,
  noteGap: 26,
  noteHeight: 30,
};

const columnWidth = v.paletteColumns * v.swatchSize + (v.paletteColumns - 1) * v.swatchGap;

// -- the column -----------------------------------------------------------------------------------------
const marks = [];
let y = v.columnTopY;

const caption = (name) => {
  const height = v.captionSize * 1.5;
  marks.push({ what: name, top: y, bottom: y - height });
  y -= height + v.captionGap;
  return y;
};

caption('caption FINISH');
{
  const rows = Math.ceil(v.styleCount / v.styleColumns);
  const height = rows * v.styleHeight + (rows - 1) * v.styleGap;
  marks.push({ what: 'finish grid (' + rows + ' rows)', top: y, bottom: y - height });
  y -= height;
}

y -= v.sectionGap;
caption('caption COLOUR');
{
  const rows = Math.ceil(v.paletteTotal / v.paletteColumns);
  const height = rows * v.swatchSize + (rows - 1) * v.swatchGap;
  marks.push({ what: 'palette (' + rows + ' rows)', top: y, bottom: y - height });
  y -= height;
}

y -= v.sectionGap;
{
  const height = v.captionSize * 1.5;
  marks.push({ what: 'caption YOUR COLOUR', top: y, bottom: y - height });
  marks.push({
    what: 'preview swatch',
    top: y - (height - v.previewSwatchHeight),
    bottom: y - height,
  });
  y -= height + v.captionGap;
}

{
  const height = v.pickerFieldHeight + v.pickerGap + v.pickerHueHeight;
  marks.push({ what: 'picker field + hue', top: y, bottom: y - height });
  y -= height;
}

y -= v.numberRowGap;
{
  const height = v.numberCaptionSize * 1.35 + v.numberFieldHeight;
  marks.push({ what: 'RGB / HEX row', top: y, bottom: y - height });
  y -= height;
}

const columnBottom = y;
marks.push({ what: 'note', top: columnBottom - v.noteGap, bottom: columnBottom - v.noteGap - v.noteHeight });

const partsBottom = v.partTopY - (v.partCount * v.partHeight + (v.partCount - 1) * v.partGap);
const resetTop = -(REFERENCE_HEIGHT - 46 - 54);

console.log('ColumnWidth = ' + columnWidth);
console.log('RGB/HEX box width each = ' + ((columnWidth - 140 - 3 * 8) / 3).toFixed(1) + ',  hex box = 140');
console.log('parts list:  ' + v.partTopY + ' .. ' + partsBottom);
console.log('RESET/BACK: top ' + resetTop.toFixed(0) + ' (bottom ' + (resetTop - 54).toFixed(0) + ')');
console.log('');

for (const m of marks) {
  const offScreen = m.bottom < -REFERENCE_HEIGHT;
  console.log(
    '  ' + m.what.padEnd(26) + ' top ' + m.top.toFixed(1).padStart(8) +
    '   bottom ' + m.bottom.toFixed(1).padStart(8) +
    (offScreen ? '   *** OFF SCREEN ***' : '')
  );
}

// Overlaps within the column. The preview swatch is deliberately drawn on the caption's own band - it sits at
// the right of it - so that one pair is not a fault.
let overlaps = 0;
for (let i = 1; i < marks.length; i++) {
  if (marks[i].what === 'preview swatch') continue;

  if (marks[i].top > marks[i - 1].bottom) {
    overlaps++;
    console.log('  OVERLAP: ' + marks[i].what + ' top ' + marks[i].top + ' > ' + marks[i - 1].what + ' bottom ' + marks[i - 1].bottom);
  }
}

const margin = REFERENCE_HEIGHT - Math.abs(marks[marks.length - 1].bottom);
console.log('\nbottom margin: ' + margin.toFixed(1) + ' px of ' + REFERENCE_HEIGHT);
console.log(overlaps === 0 && margin > 0 ? 'LAYOUT OK' : 'LAYOUT PROBLEM');
