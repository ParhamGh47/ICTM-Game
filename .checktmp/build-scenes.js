// Builds the six scenes of levels 5 and 6 out of the levels the project already
// has: a Core scene assembled from the extracted level skeleton (Core-4), and the
// Start/End scenes copied from level 4 / level 3 with only the level-specific bits
// changed. Core scenes in this project use LF line endings and the Start/End scenes
// CRLF, so each new scene keeps the newlines of the scene it came from.
const fs = require('fs');
const path = require('path');

const PROJECT = path.resolve(__dirname, '..');
const ASSETS = path.join(PROJECT, 'Assets');

const read = rel => fs.readFileSync(path.join(ASSETS, rel), 'utf8');
const writeRel = (rel, text) => {
  fs.mkdirSync(path.dirname(path.join(ASSETS, rel)), { recursive: true });
  fs.writeFileSync(path.join(ASSETS, rel), text);
};

/** The newline a file uses - this project has some scenes with LF and some with CRLF. */
const newlineOf = text => (text.includes('\r\n') ? '\r\n' : '\n');

// ------------------------------------------------------------------ guids

const used = new Set();
const byPath = new Map();
const guidToPath = new Map();

(function scanAssets(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) scanAssets(p);
    else if (p.endsWith('.meta')) {
      const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(p, 'utf8'));
      if (m) {
        used.add(m[1]);
        byPath.set(p.slice(0, -5), m[1]);
        guidToPath.set(m[1], p.slice(0, -5));
      }
    }
  }
})(ASSETS);

// Package scripts (Cinemachine, Post Processing) live outside Assets; they are
// collected only so the checks at the end can resolve them.
const packageGuids = new Set();
(function scanPackages(dir) {
  let entries;
  try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const e of entries) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) scanPackages(p);
    else if (p.endsWith('.meta')) {
      const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(p, 'utf8'));
      if (m) packageGuids.add(m[1]);
    }
  }
})(path.join(PROJECT, 'Library', 'PackageCache'));

const newGuid = () => {
  let g;
  do { g = require('crypto').randomBytes(16).toString('hex'); } while (used.has(g));
  used.add(g);
  return g;
};

const guidOf = rel => {
  const g = byPath.get(path.join(ASSETS, rel));
  if (!g) throw new Error('no guid for ' + rel);
  return g;
};

const copySceneMeta = (fromRel, toRel) =>
  writeRel(toRel + '.meta', read(fromRel + '.meta').replace(/^guid: [0-9a-f]{32}/m, 'guid: ' + newGuid()));

// ------------------------------------------------------------------ the Core scene

const HEADER = fs.readFileSync(path.join(__dirname, 'out/core-header.txt'), 'utf8');
const BODY = fs.readFileSync(path.join(__dirname, 'out/core-body.txt'), 'utf8');
const CORE_NL = newlineOf(BODY);

const L4 = {
  sky: guidOf('Unique.Materials/Level-4/Sky.mat'),
  ground: guidOf('Unique.Materials/Level-4/Ground.mat'),
  terrain: guidOf('New Terrain 1.asset'),
};

const CANVAS_PREFAB = '81c7d71664e97214bb3397d11772ac21';
const LEVEL_LOADER_SCRIPT = 'c4b1585b316e7a04989302c57ad9e633';
const CANVAS_INSTANCE = '6890820110328195566';
const PROGRESS_DISPLAY = '6890820111668650642';
const LOADER_OBJECT = '6890820110328195568';
const CORE_LIGHT = '1960672447';

/** Applies an edit to one document of a scene, by class and fileID. */
function editDoc(text, cls, id, fn) {
  const parts = text.split(/^--- /m);
  const header = parts.shift();
  const docs = parts.map(p => '--- ' + p);

  const at = docs.findIndex(d => new RegExp('^--- !u!' + cls + ' &' + id + '\\b').test(d));
  if (at < 0) throw new Error(`no doc !u!${cls} &${id}`);

  docs[at] = fn(docs[at]);
  return header + docs.join('');
}

const escapeRe = s => s.replace(/[.[\]*+?^${}()|\\]/g, '\\$&');

const setMod = (doc, nl, propertyPath, value) => {
  const re = new RegExp('(propertyPath: ' + escapeRe(propertyPath) + nl + '      value: )[^' + nl + ']*');
  if (!re.test(doc)) throw new Error('no modification for ' + propertyPath);
  return doc.replace(re, '$1' + value);
};

const colour = rgb => `{r: ${rgb[0]}, g: ${rgb[1]}, b: ${rgb[2]}, a: 1}`;

/** The loader the finish line reaches: a component added to the HUD canvas, as in levels 1-3. */
const loaderCluster = (level, nl) => [
  `--- !u!1 &6890820110328195567 stripped`,
  `GameObject:`,
  `  m_CorrespondingSourceObject: {fileID: 6890820111668650649, guid: ${CANVAS_PREFAB}, type: 3}`,
  `  m_PrefabInstance: {fileID: ${CANVAS_INSTANCE}}`,
  `  m_PrefabAsset: {fileID: 0}`,
  `--- !u!114 &${LOADER_OBJECT}`,
  `MonoBehaviour:`,
  `  m_ObjectHideFlags: 0`,
  `  m_CorrespondingSourceObject: {fileID: 0}`,
  `  m_PrefabInstance: {fileID: 0}`,
  `  m_PrefabAsset: {fileID: 0}`,
  `  m_GameObject: {fileID: 6890820110328195567}`,
  `  m_Enabled: 1`,
  `  m_EditorHideFlags: 0`,
  `  m_Script: {fileID: 11500000, guid: ${LEVEL_LOADER_SCRIPT}, type: 3}`,
  `  m_Name: `,
  `  m_EditorClassIdentifier: `,
  `  gameplaySceneName: `,
  `  endScene: TW-End-${level}`,
  ``,
  ``,
].join(nl);

function buildCore(level, cfg) {
  const sky = guidOf(`Unique.Materials/Level-${level}/Sky.mat`);
  const ground = guidOf(`Unique.Materials/Level-${level}/Ground.mat`);
  const terrain = guidOf(`Terrains/Level-${level} Terrain.asset`);

  const nl = CORE_NL;
  let text = HEADER + BODY;

  // the level's own terrain, ground and sky
  text = text.replace(L4.sky, sky);
  text = text.replace(L4.ground, ground);
  text = text.split(L4.terrain).join(terrain);

  // no baked navigation data or reflection probe carried over from the level this was cut from
  text = text.replace(/m_NavMeshData: \{[^}]*\}/, 'm_NavMeshData: {fileID: 0}');

  // the sun: off in the level this skeleton came from
  text = editDoc(text, 108, CORE_LIGHT, d => d
    .replace(/m_Enabled: 0/, 'm_Enabled: 1')
    .replace(/m_Color: \{[^}]*\}/, 'm_Color: ' + colour(cfg.sun.colour))
    .replace(/m_Intensity: [\d.]+/, 'm_Intensity: ' + cfg.sun.intensity)
    .replace(/m_BounceIntensity: [\d.]+/, 'm_BounceIntensity: ' + cfg.sun.bounce));

  // the HUD: this level's target, no checkpoints yet, and the end scene wired up
  text = editDoc(text, 1001, CANVAS_INSTANCE, d => {
    const dataMod = new RegExp(nl + '    - target: \\{[^' + nl + ']*\\}' + nl +
      '      propertyPath: checkpoints\\.Array\\.data\\[\\d+\\]' + nl +
      '      value: [^' + nl + ']*' + nl +
      '      objectReference: \\{[^' + nl + ']*\\}', 'g');

    let s = d.replace(dataMod, '');
    s = setMod(s, nl, 'checkpoints.Array.size', '0');
    s = setMod(s, nl, 'targetTimeSeconds', String(cfg.hud.targetTimeSeconds));
    s = setMod(s, nl, 'targetKills', String(cfg.hud.targetKills));

    const loader = [
      `    - target: {fileID: ${PROGRESS_DISPLAY}, guid: ${CANVAS_PREFAB}, type: 3}`,
      `      propertyPath: loader`,
      `      value: `,
      `      objectReference: {fileID: ${LOADER_OBJECT}}`,
      ``,
    ].join(nl);

    return s.replace('    m_RemovedComponents: []', loader + '    m_RemovedComponents: []');
  });

  // the loader object itself, right after the canvas it hangs off
  const anchor = `  m_SourcePrefab: {fileID: 100100000, guid: ${CANVAS_PREFAB}, type: 3}${nl}`;
  if (!text.includes(anchor)) throw new Error('no canvas instance anchor');
  text = text.replace(anchor, anchor + loaderCluster(level, nl));

  const scene = `Scenes/Levels Scenes/${level}/Core-${level}.unity`;
  writeRel(scene, text);
  copySceneMeta('Scenes/Levels Scenes/4/Core-4.unity', scene);

  return scene;
}

// ------------------------------------------------------------------ the Start / End scenes

/** Replaces the story text, which is the last field of its component. */
function setStoryText(text, value) {
  const nl = newlineOf(text);
  const at = text.indexOf(nl + '  startText: ');
  if (at < 0) throw new Error('no startText');

  const end = text.indexOf(nl + '--- ', at + 1);
  const rest = end < 0 ? '' : text.slice(end);

  return text.slice(0, at) + nl + `  startText: '${value.replace(/'/g, "''")}'` + rest;
}

function buildStart(level, cfg) {
  let text = read('Scenes/Levels Scenes/4/TW-Start-4.unity');

  if (!text.includes('gameplaySceneName: Core-4')) throw new Error('level 4 start scene moved on');
  text = text.replace('gameplaySceneName: Core-4', 'gameplaySceneName: Core-' + level);
  text = setStoryText(text, cfg.startText);

  const scene = `Scenes/Levels Scenes/${level}/TW-Start-${level}.unity`;
  writeRel(scene, text);
  copySceneMeta('Scenes/Levels Scenes/4/TW-Start-4.unity', scene);

  return scene;
}

function buildEnd(level, cfg) {
  let text = read('Scenes/Levels Scenes/3/TW-End-3.unity');
  text = setStoryText(text, cfg.endText);

  const scene = `Scenes/Levels Scenes/${level}/TW-End-${level}.unity`;
  writeRel(scene, text);
  copySceneMeta('Scenes/Levels Scenes/3/TW-End-3.unity', scene);

  return scene;
}

// ------------------------------------------------------------------ run

const LEVELS = {
  5: {
    sun: { colour: [1, 0.851, 1], intensity: 1, bounce: 1 },
    hud: { targetTimeSeconds: 240, targetKills: 0 },
    startText: '[Level 5 opening narration goes here.]',
    endText: '[Level 5 closing narration goes here.]',
  },
  6: {
    sun: { colour: [1, 1, 1], intensity: 0.9, bounce: 1 },
    hud: { targetTimeSeconds: 240, targetKills: 0 },
    startText: '[Level 6 opening narration goes here.]',
    endText: '[Level 6 closing narration goes here.]',
  },
};

const made = [];
for (const level of [5, 6]) {
  made.push(buildCore(level, LEVELS[level]));
  made.push(buildStart(level, LEVELS[level]));
  made.push(buildEnd(level, LEVELS[level]));
}

// ------------------------------------------------------------------ checks

console.log('=== the new scenes ===');
for (const rel of made) {
  const text = read(rel);
  const nl = newlineOf(text);
  const docs = text.split(/^--- /m).slice(1).map(d => '--- ' + d);
  const ids = new Set(docs.map(d => /^--- !u!\d+ &(\d+)/.exec(d)[1]));

  const unknown = new Set();
  const referenced = new Map();
  for (const m of text.matchAll(/guid: ([0-9a-f]{32})/g)) {
    const g = m[1];
    if (g.startsWith('0000000000000000')) continue;
    if (!used.has(g) && !packageGuids.has(g)) unknown.add(g);
    if (guidToPath.has(g)) referenced.set(g, guidToPath.get(g));
  }

  const dangling = new Set();
  for (const m of text.matchAll(/objectReference: \{fileID: (-?\d+)\}/g)) {
    if (m[1] !== '0' && !ids.has(m[1])) dangling.add(m[1]);
  }

  const loader = /endScene: (TW-End-\d)/.exec(text);
  const levelAssets = [...new Set([...referenced.values()]
    .filter(p => /Level-[56]|Level-[56] Terrain/.test(p))
    .map(p => p.slice(ASSETS.length + 1)))];

  console.log(`${rel}
   ${docs.length} docs, ${text.split(nl).length} lines, ${nl === '\r\n' ? 'CRLF' : 'LF'}
   unknown guids: ${unknown.size ? [...unknown].join(', ') : 'none'}
   dangling scene references: ${dangling.size ? [...dangling].join(', ') : 'none'}
   end scene: ${loader ? loader[1] : 'NONE'}
   this level's assets: ${levelAssets.join(', ') || 'NONE'}`);
}
