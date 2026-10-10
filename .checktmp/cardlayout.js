// The card's grid, exactly as TutorialIntro.Layout and the build methods use it.
const CARD = { w: 1240, h: 660 };
const Edge = 40, Gap = 40, InnerHalfWidth = 602;
const PictureWidth = 764, NoteWidth = 320;
const StageHeight = 360, MiddleRow = 10, TitleRow = 252, CaptionRow = -200, ControlsRow = -272;

const PictureCentre = -InnerHalfWidth + Edge + PictureWidth / 2;
const NoteCentre = InnerHalfWidth - Edge - NoteWidth / 2;

const box = (name, cx, cy, w, h) => ({ name, left: cx - w / 2, right: cx + w / 2, bottom: cy - h / 2, top: cy + h / 2 });

const parts = [
  box('title', 0, TitleRow, CARD.w - 120, 84),
  box('picture', PictureCentre, MiddleRow, PictureWidth, StageHeight),
  box('count', NoteCentre, MiddleRow, NoteWidth, StageHeight),
  box('caption', PictureCentre, CaptionRow, PictureWidth, 52),
  box('start', -InnerHalfWidth + Edge + 150, ControlsRow, 300, 76),
  box('switch row', InnerHalfWidth - Edge - 280, ControlsRow, 560, 76),
  box('plus one', NoteCentre, MiddleRow + StageHeight / 2 + 45, 220, 70),
];

const inner = { left: -InnerHalfWidth, right: InnerHalfWidth, bottom: -312, top: 312 };

console.log('card', CARD.w + 'x' + CARD.h, ' inner rule', inner.left + '..' + inner.right);
console.log('picture centre', PictureCentre, ' count centre', NoteCentre);
console.log('\nrectangles');
for (const p of parts) {
  const out =
    p.left < inner.left - 0.01 ? 'LEFT OF RULE' :
    p.right > inner.right + 0.01 ? 'RIGHT OF RULE' :
    p.bottom < inner.bottom - 0.01 ? 'BELOW RULE' :
    p.top > inner.top + 0.01 ? 'ABOVE RULE' : 'inside';
  console.log(`  ${p.name.padEnd(11)} x ${p.left.toFixed(0).padStart(5)}..${p.right.toFixed(0).padStart(4)}` +
    `  y ${p.bottom.toFixed(0).padStart(5)}..${p.top.toFixed(0).padStart(4)}   ${out}`);
}

console.log('\nalignment');
console.log('  picture left edge', parts[1].left, ' = start button left edge', parts[4].left,
  parts[1].left === parts[4].left ? 'ALIGNED' : 'OFF');
console.log('  count right edge', parts[2].right, ' = switch row right edge', parts[5].right,
  parts[2].right === parts[5].right ? 'ALIGNED' : 'OFF');
console.log('  margin left of picture', parts[1].left - inner.left, ' right of count', inner.right - parts[2].right);
console.log('  gap between the columns', parts[2].left - parts[1].right);

const overlaps = [];
for (let i = 0; i < parts.length; i++) {
  for (let j = i + 1; j < parts.length; j++) {
    const a = parts[i], b = parts[j];
    if (a.left < b.right && b.left < a.right && a.bottom < b.top && b.bottom < a.top) {
      overlaps.push(`${a.name} / ${b.name}`);
    }
  }
}
console.log('  overlapping rows:', overlaps.length ? overlaps.join(', ') : 'none (the title and the +1 share a band, but their words do not)');
