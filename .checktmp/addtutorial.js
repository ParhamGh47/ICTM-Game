// Put the tutorial card into a level scene: a root GameObject with the TutorialIntro script, carrying the same
// values as the Tutorial prefab. Every public field is written out, because a value left out of a scene is zero
// on load rather than the script's own default.
const fs = require('fs');
const path = require('path');
const { read } = require('./scenelib');

const root = path.resolve(__dirname, '..');
const file = path.join(root, process.argv[2] || 'Assets/Scenes/Levels Scenes/3/Core-3.unity');

const SCRIPT_GUID = 'b1c2d3e4f5061728394a5b6c7d8e9f01';
const FONT_GUID = '78ee06b01dcb834478974581b7900780';

let text = read(file);

if (text.includes('m_Name: Tutorial')) {
  console.log('already present - nothing added');
  process.exit(0);
}

// The next free root order.
const orders = [...text.matchAll(/m_RootOrder: (\d+)/g)].map((m) => +m[1]);
const rootOrder = Math.max(...orders, 0) + 1;

// Ids that certainly do not collide.
function freeId(seed) {
  let id = BigInt(seed);
  while (new RegExp('&' + id + '\\b').test(text)) id++;
  return String(id);
}

const goId = freeId(9200000000000000001);
const trId = freeId(BigInt(goId) + 1n);
const mbId = freeId(BigInt(trId) + 1n);

const doc = `--- !u!1 &${goId}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: ${trId}}
  - component: {fileID: ${mbId}}
  m_Layer: 0
  m_Name: Tutorial
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &${trId}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${goId}}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_RootOrder: ${rootOrder}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &${mbId}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: ${goId}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${SCRIPT_GUID}, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  lesson: 0
  tutorialName: 
  alwaysShow: 0
  titleText: TARGETS
  captionText: SMASH THEM ON THE ROAD
  hitCaptionText: '+1 KILL'
  targetCount: 10
  startCount: 3
  font: {fileID: 12800000, guid: ${FONT_GUID}, type: 3}
  backdropColor: {r: 0.02, g: 0.03, b: 0.06, a: 0.72}
  paperColor: {r: 0.482, g: 0.796, b: 0.894, a: 1}
  inkColor: {r: 0.078, g: 0.412, b: 0.541, a: 1}
  accentColor: {r: 0.918, g: 0.196, b: 0.224, a: 1}
  roadColor: {r: 0.184, g: 0.204, b: 0.243, a: 1}
  lineColor: {r: 0.94, g: 0.97, b: 0.99, a: 0.9}
  cardSize: {x: 1240, y: 660}
  cycleSeconds: 5.2
  fadeInSeconds: 0.4
  fadeOutSeconds: 0.3
  idleDismissSeconds: 30
  startLabel: START
  hideLabel: DON'T SHOW THIS AGAIN
`;

const newline = text.includes('\r\n') ? '\r\n' : '\n';
const document = newline === '\r\n' ? doc.replace(/\n/g, '\r\n') : doc;

if (!text.endsWith('\n')) text += newline;

fs.writeFileSync(file, text + document, 'utf8');

console.log('added Tutorial to', path.relative(root, file));
console.log('ids', goId, trId, mbId, 'rootOrder', rootOrder, 'newlines', newline === '\r\n' ? 'CRLF' : 'LF');
