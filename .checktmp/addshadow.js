// Append the Shadow Racer object to a level scene: a root GameObject with the ShadowRacer script, its
// prefab field pointing at "Shadow Truck.prefab". Every public field is written out, because a value left
// out of a scene is zero on load rather than the script's own default.
const fs = require('fs');
const path = require('path');
const { read } = require('./scenelib');

const root = path.resolve(__dirname, '..');
const file = path.join(root, process.argv[2] || 'Assets/Scenes/Levels Scenes/4/Core-4.unity');

const SCRIPT_GUID = 'a1c9f4b27e3d4a5b8c6d9e0f1a2b3c4d';
const PREFAB_GUID = 'd4e7a1b35c9f42e8a0b6c2d3e4f5a6b7';
const PREFAB_ROOT = '5411794254173547900';

let text = read(file);

if (text.includes('m_Name: Shadow Racer')) {
  console.log('already present - nothing added');
  process.exit(0);
}

// The next free root order.
const orders = [...text.matchAll(/m_RootOrder: (\d+)/g)].map((m) => +m[1]);
const rootOrder = Math.max(...orders, 0) + 1;

// Ids that certainly do not collide: check and bump if they do.
function freeId(seed) {
  let id = seed;
  while (new RegExp('&' + id + '\\b').test(text)) id++;
  return String(id);
}

const goId = freeId(9100000000000000001);
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
  m_Name: Shadow Racer
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
  truckPrefab: {fileID: ${PREFAB_ROOT}, guid: ${PREFAB_GUID}, type: 3}
  player: {fileID: 0}
  easySpeedKPH: 82
  mediumSpeedKPH: 102
  hardSpeedKPH: 118
  acceleration: 5
  maxLinkDistance: 170
  jumpGapMin: 4
  minJumpArc: 1.4
  maxJumpArc: 16
  rideHeight: 0.15
  weaveAmplitude: 2.6
  weaveWavelength: 46
  weaveSecondAmplitude: 1.2
  weaveSecondWavelength: 15
  weavePhase: 0
  shadowTint: {r: 0.055, g: 0.05, b: 0.075, a: 1}
  maxBank: 6
  bankResponse: 2.2
  turnResponse: 9
  particleColour: {r: 0.07, g: 0.06, b: 0.11, a: 0.55}
  particleRate: 30
  particleMax: 110
  particleRadius: 2.4
  particleHeight: 1.1
`;

if (!text.endsWith('\n')) text += '\n';
text += doc;

fs.writeFileSync(file, text, 'utf8');

console.log('added Shadow Racer to ' + path.basename(file));
console.log('  go=' + goId + ' transform=' + trId + ' script=' + mbId + ' rootOrder=' + rootOrder);
