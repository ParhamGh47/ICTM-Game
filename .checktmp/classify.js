// Puts a SoundChannelSource on the named GameObjects of a scene/prefab, so the sound settings can reach every
// sound the game actually plays. Idempotent: a second run updates the channel instead of adding another one.
//
// Usage: node .checktmp/classify.js
const fs = require('fs');

const SCRIPT_GUID = '9af42c6e15d84b7392c0e5a81d67f3b2';   // SoundChannelSource.cs

// SoundChannel: Master 0, Soundtrack 1, Engine 2, Effects 3, Environment 4
const TARGETS = [
  ['Assets/Scenes/Menu.unity', 'Soundtrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/1/Core-1.unity', 'Soundtrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/2/Core-2.unity', 'Soundtrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/3/Core-3.unity', 'Soundtrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/4/Core-4.unity', 'Soundtrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/1/TW-Start-1.unity', 'SoundTrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/1/TW-End-1.unity', 'SoundTrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/2/TW-Start-2.unity', 'SoundTrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/2/TW-End-2.unity', 'SoundTrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/3/TW-Start-3.unity', 'SoundTrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/3/TW-End-3.unity', 'SoundTrack', 1, 0],
  ['Assets/Scenes/Levels Scenes/4/TW-Start-4.unity', 'SoundTrack', 1, 0],
  ['Assets/Prefabs/Utils/CanvasUI.prefab', 'PauseManager', 1, 0],
  ['Assets/Prefabs/Utils/CanvasUI.prefab', 'GameOverManager', 1, 0],
  ['Assets/Prefabs/Utils/Truck (Player).prefab', 'Horn', 3, 0],
  ['Assets/Prefabs/Utils/Truck (Player).prefab', 'ReverseGear', 3, 0],
  ['Assets/Prefabs/Utils/Truck (Player).prefab', 'Collision', 3, 0],
  ['Assets/Prefabs/Utils/Truck (Player).prefab', 'LightClick', 3, 0],
  ['Assets/Prefabs/Adamaks/Adamak1.prefab', 'Body', 3, 0],
  ['Assets/Scenes/Truck.prefab', 'Horn', 3, 0],
  ['Assets/Scenes/Truck.prefab', 'ReverseGear', 3, 0],
  ['Assets/Scenes/Truck.prefab', 'Collision', 3, 0],
  ['Assets/Scenes/Truck.prefab', 'LightClick', 3, 0],
];

const files = {};
for (const t of TARGETS) files[t[0]] = true;

let added = 0;
let updated = 0;
let skipped = 0;

for (const file of Object.keys(files)) {
  let raw = fs.readFileSync(file, 'utf8');
  const eol = raw.includes('\r\n') ? '\r\n' : '\n';
  let text = raw.replace(/\r\n/g, '\n');

  const blocks = [...text.matchAll(/^--- !u!(\d+) &(\d+)(?: stripped)?\n([\s\S]*?)(?=^--- |\Z)/gm)]
    .map((m) => ({ c: m[1], id: m[2], body: m[3] }));

  const goByName = new Map();
  for (const b of blocks) {
    if (b.c !== '1') continue;
    const n = /^  m_Name: (.*)$/m.exec(b.body);
    if (n) goByName.set(n[1], b.id);
  }

  const mine = new Map();   // gameObject fileID -> { id, bodyBlockIndex }
  for (const b of blocks) {
    if (b.c !== '114') continue;
    if (!b.body.includes('guid: ' + SCRIPT_GUID)) continue;
    const go = /^  m_GameObject: \{fileID: (\d+)\}/m.exec(b.body);
    if (go) mine.set(go[1], b.id);
  }

  const usedIds = new Set(blocks.map((b) => b.id));
  let appended = '';

  for (const [targetFile, goName, channel, children] of TARGETS) {
    if (targetFile !== file) continue;

    const goId = goByName.get(goName);
    if (!goId) {
      console.log('  ! ' + file + ': no GameObject named "' + goName + '"');
      continue;
    }

    if (mine.has(goId)) {
      // Already classified: make sure it says what this list says.
      const id = mine.get(goId);
      const before = text;
      text = text.replace(
        new RegExp('(^--- !u!114 &' + id + '\\n[\\s\\S]*?^  channel: )\\d+', 'm'),
        '$1' + channel
      );
      text = text.replace(
        new RegExp('(^--- !u!114 &' + id + '\\n[\\s\\S]*?^  includeChildren: )\\d+', 'm'),
        '$1' + children
      );
      if (before !== text) updated++;
      else skipped++;
      continue;
    }

    let id;
    do {
      id = String(3000000000000000000n + BigInt(Math.floor(Math.random() * 1e15)));
    } while (usedIds.has(id));
    usedIds.add(id);

    // Add the component to the GameObject's list, after the entries already there.
    const listRe = new RegExp('(^--- !u!1 &' + goId + '\\n[\\s\\S]*?^  m_Component:\\n)((?:  - component: \\{fileID: \\d+\\}\\n)*)', 'm');
    if (!listRe.test(text)) {
      console.log('  ! ' + file + ': could not find the component list of "' + goName + '"');
      continue;
    }
    text = text.replace(listRe, '$1$2  - component: {fileID: ' + id + '}' + '\n');

    appended +=
      '--- !u!114 &' + id + '\n' +
      'MonoBehaviour:\n' +
      '  m_ObjectHideFlags: 0\n' +
      '  m_CorrespondingSourceObject: {fileID: 0}\n' +
      '  m_PrefabInstance: {fileID: 0}\n' +
      '  m_PrefabAsset: {fileID: 0}\n' +
      '  m_GameObject: {fileID: ' + goId + '}\n' +
      '  m_Enabled: 1\n' +
      '  m_EditorHideFlags: 0\n' +
      '  m_Script: {fileID: 11500000, guid: ' + SCRIPT_GUID + ', type: 3}\n' +
      '  m_Name: \n' +
      '  m_EditorClassIdentifier: \n' +
      '  channel: ' + channel + '\n' +
      '  includeChildren: ' + children + '\n';

    added++;
  }

  if (appended) {
    if (!text.endsWith('\n')) text += '\n';
    text += appended;
  }

  fs.writeFileSync(file, text.split('\n').join(eol));
  console.log('  ' + file + ': written');
}

console.log('\nadded ' + added + ', updated ' + updated + ', already right ' + skipped);
