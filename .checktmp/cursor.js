// Checks the CanvasUI prefab after hanging GameplayCursor off its PauseManager object: every block id is
// unique, the GameObject lists exactly the components that exist, and the new component points at the right
// object and script. Also reports which scenes carry the prefab, since the cursor's rule is the Core-n name.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const file = path.join(root, 'Assets/Prefabs/Utils/CanvasUI.prefab');
const text = fs.readFileSync(file, 'utf8').replace(/\r/g, '');

const blocks = [];
let current = null;
for (const line of text.split('\n')) {
  const header = line.match(/^--- !u!(\d+) &(\d+)/);
  if (header) {
    current = { cls: header[1], id: header[2], lines: [] };
    blocks.push(current);
  } else if (current) current.lines.push(line);
}

const seen = new Map();
let duplicates = 0;
for (const b of blocks) {
  if (seen.has(b.id)) duplicates++;
  seen.set(b.id, b);
}
console.log('blocks:', blocks.length, 'duplicate ids:', duplicates);

const CURSOR_GUID = '15e752534b8d05c91c956c707a3fa301';
const TARGET_OBJECT = '6890820110899190687';      // PauseManager

const cursorBlock = blocks.find((b) => b.lines.join('\n').includes(CURSOR_GUID));
if (!cursorBlock) {
  console.log('FAIL: no block references the GameplayCursor script');
  process.exit(1);
}

const cursorText = cursorBlock.lines.join('\n');
const cursorObject = (cursorText.match(/m_GameObject: \{fileID: (\d+)\}/) || [])[1];
console.log('cursor component id:', cursorBlock.id, '-> object', cursorObject);

const pauseManager = seen.get(TARGET_OBJECT);
const componentIds = (pauseManager.lines.join('\n').match(/component: \{fileID: (\d+)\}/g) || [])
  .map((s) => s.match(/(\d+)/)[1]);

console.log('PauseManager component count:', componentIds.length);
console.log('lists the cursor component:', componentIds.includes(cursorBlock.id));
console.log('object match:', cursorObject === TARGET_OBJECT);

const missing = componentIds.filter((id) => !seen.has(id));
console.log('listed components with no block:', missing.length ? missing.join(',') : 'none');

// every fileID referenced from a component list must exist, and the object must name the field itself
const field = (cursorText.match(/gameplayScenePrefix: (.*)/) || [])[1];
console.log('gameplayScenePrefix:', JSON.stringify(field));

const scenes = fs.readdirSync(path.join(root, 'Assets/Scenes'), { recursive: true })
  .filter((f) => String(f).endsWith('.unity'))
  .map((f) => path.join(root, 'Assets/Scenes', String(f)))
  .filter((f) => fs.readFileSync(f, 'utf8').includes('81c7d71664e97214bb3397d11772ac21'))
  .map((f) => path.basename(f));
console.log('scenes carrying the CanvasUI prefab:', scenes.join(', '));
console.log('of those, Core-n (cursor hidden):', scenes.filter((s) => /^Core-\d/.test(s)).join(', '));
