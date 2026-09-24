// Adds the NoImpactParticles marker component to the root GameObject of a prefab, keeping CRLF endings.
// Usage: node .checktmp/addmarker.js
const fs = require('fs');

const GUID = '4b7c9e21f8a34d60b5c2e73a91d4f806';

const targets = [
  { file: 'Assets/Prefabs/Adamaks/Adamak1.prefab', go: '3923136064189347077', comp: '940000000000000001' },
  { file: 'Assets/Prefabs/Signs/Signs/Blinder.prefab', go: '7323471414049666626', comp: '940000000000000002' },
  { file: 'Assets/Prefabs/Signs/Signs/ShareTheRoad.prefab', go: '6530950631188464442', comp: '940000000000000003' },
  { file: 'Assets/Prefabs/Signs/Signs/Stop.prefab', go: '7205068904007956044', comp: '940000000000000004' },
];

for (const t of targets) {
  let text = fs.readFileSync(t.file, 'utf8');

  if (text.includes('&' + t.comp + '\n') || text.includes('&' + t.comp + '\r\n')) {
    console.log(t.file + ': marker already present, skipped');
    continue;
  }

  // The GameObject's own block.
  const blockRe = new RegExp('--- !u!1 &' + t.go + '\nGameObject:\n[\\s\\S]*?(?=\n--- )');
  const block = blockRe.exec(text);

  if (!block) {
    console.log(t.file + ': GameObject ' + t.go + ' not found!');
    continue;
  }

  const listRe = /( {2}m_Component:\n(?: {2}- component: \{fileID: \d+\}\n)+)/;
  const list = listRe.exec(block[0]);

  if (!list) {
    console.log(t.file + ': no component list on GameObject ' + t.go + '!');
    continue;
  }

  const withMarker = list[1] + '  - component: {fileID: ' + t.comp + '}\n';
  text = text.replace(block[0], block[0].replace(list[1], withMarker));

  const component =
    '--- !u!114 &' + t.comp + '\n' +
    'MonoBehaviour:\n' +
    '  m_ObjectHideFlags: 0\n' +
    '  m_CorrespondingSourceObject: {fileID: 0}\n' +
    '  m_PrefabInstance: {fileID: 0}\n' +
    '  m_PrefabAsset: {fileID: 0}\n' +
    '  m_GameObject: {fileID: ' + t.go + '}\n' +
    '  m_Enabled: 1\n' +
    '  m_EditorHideFlags: 0\n' +
    '  m_Script: {fileID: 11500000, guid: ' + GUID + ', type: 3}\n' +
    '  m_Name: \n' +
    '  m_EditorClassIdentifier: \n';

  if (!text.endsWith('\n')) text += '\n';
  text += component;

  fs.writeFileSync(t.file, text);

  console.log(t.file + ': marker added');
}
