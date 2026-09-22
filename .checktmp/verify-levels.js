// Verification pass over everything the level 5 / 6 setup added.
const fs = require('fs');
const path = require('path');

const PROJECT = path.resolve(__dirname, '..');
const ASSETS = path.join(PROJECT, 'Assets');

const guidToPath = new Map();
const pathToGuid = new Map();
(function scan(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) scan(p);
    else if (p.endsWith('.meta')) {
      const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(p, 'utf8'));
      if (m) {
        const rel = path.relative(ASSETS, p.slice(0, -5)).replace(/\\/g, '/');
        guidToPath.set(m[1], rel);
        pathToGuid.set(rel, m[1]);
      }
    }
  }
})(ASSETS);

const packageGuids = new Set();
(function scan(dir) {
  let entries;
  try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return; }
  for (const e of entries) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) scan(p);
    else if (p.endsWith('.meta')) {
      const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(p, 'utf8'));
      if (m) packageGuids.add(m[1]);
    }
  }
})(path.join(PROJECT, 'Library', 'PackageCache'));

let problems = 0;
const problem = msg => { problems++; console.log('  !! ' + msg); };

// ---------------------------------------------------------------- assets

console.log('=== level assets ===');
for (const level of [5, 6]) {
  const wanted = [
    `Terrains/Level-${level} Terrain.asset`,
    `Unique.Materials/Level-${level}/Ground.mat`,
    `Unique.Materials/Level-${level}/Grass.mat`,
    `Unique.Materials/Level-${level}/Road.mat`,
    `Unique.Materials/Level-${level}/RoadLine.mat`,
    `Unique.Materials/Level-${level}/Shoulder.mat`,
    `Unique.Materials/Level-${level}/Sky.mat`,
    `Prefabs/DetailsTerrain/Level-${level}/Grass.mat`,
    `Prefabs/DetailsTerrain/Level-${level}/Grass.prefab`,
    `Prefabs/DetailsTerrain/Level-${level}/Trees/TreeColor.mat`,
    `Prefabs/DetailsTerrain/Level-${level}/Trees/Materials/TreeColor.mat`,
    `Prefabs/DetailsTerrain/Level-${level}/Buildings/Building-Mat-1.mat`,
  ];

  const missing = wanted.filter(rel => !fs.existsSync(path.join(ASSETS, rel)) || !pathToGuid.has(rel));
  console.log(`level ${level}: ${missing.length ? 'MISSING ' + missing.join(', ') : 'all ' + wanted.length + ' key assets present'}`);
  if (missing.length) problems++;

  // the terrain is a clean copy of an unused one, byte for byte
  const src = level === 5 ? 'Terrains/New Terrain 3.asset' : 'Terrains/New Terrain 4.asset';
  const same = fs.readFileSync(path.join(ASSETS, src)).equals(fs.readFileSync(path.join(ASSETS, `Terrains/Level-${level} Terrain.asset`)));
  console.log(`   terrain is a byte copy of the unused ${path.basename(src)}: ${same}`);
  if (!same) problems++;

  // every file in the level's environment folder must have a meta with a unique guid
  const seen = new Set();
  let count = 0;
  (function walk(d) {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      const p = path.join(d, e.name);
      if (e.isDirectory()) { walk(p); continue; }
      if (e.name.endsWith('.meta')) continue;
      count++;
      if (!fs.existsSync(p + '.meta')) problem('no meta: ' + p);
      const g = pathToGuid.get(path.relative(ASSETS, p).replace(/\\/g, '/'));
      if (!g) problem('no guid: ' + p);
      else if (seen.has(g)) problem('duplicate guid: ' + p);
      else seen.add(g);
    }
  })(path.join(ASSETS, `Prefabs/DetailsTerrain/Level-${level}`));
  console.log(`   environment folder: ${count} assets, all with unique metas`);
}

// ---------------------------------------------------------------- the scenes

const SCENES = [
  ['TW-Start-5', 'Scenes/Levels Scenes/5/TW-Start-5.unity'],
  ['Core-5', 'Scenes/Levels Scenes/5/Core-5.unity'],
  ['TW-End-5', 'Scenes/Levels Scenes/5/TW-End-5.unity'],
  ['TW-Start-6', 'Scenes/Levels Scenes/6/TW-Start-6.unity'],
  ['Core-6', 'Scenes/Levels Scenes/6/Core-6.unity'],
  ['TW-End-6', 'Scenes/Levels Scenes/6/TW-End-6.unity'],
];

console.log('\n=== scenes ===');
for (const [name, rel] of SCENES) {
  const text = fs.readFileSync(path.join(ASSETS, rel), 'utf8');
  const docs = text.split(/^--- /m).slice(1).map(d => '--- ' + d);
  const ids = new Set(docs.map(d => /^--- !u!\d+ &(\d+)/.exec(d)[1]));

  const notes = [];

  // no unresolved guids
  const unknown = new Set();
  for (const m of text.matchAll(/guid: ([0-9a-f]{32})/g)) {
    if (m[1].startsWith('0000000000000000')) continue;
    if (!guidToPath.has(m[1]) && !packageGuids.has(m[1])) unknown.add(m[1]);
  }
  if (unknown.size === 1 && unknown.has('9bbfa0ffbab56fa468b13e96e2d6c87b')) {
    notes.push('1 pre-existing guid (same as levels 1/2/4 start scenes)');
  } else if (unknown.size) problem(`${name}: unresolved guids ${[...unknown].join(', ')}`);

  // no dangling scene references
  const dangling = new Set();
  for (const m of text.matchAll(/objectReference: \{fileID: (-?\d+)\}/g)) {
    if (m[1] !== '0' && !ids.has(m[1])) dangling.add(m[1]);
  }
  if (dangling.size) problem(`${name}: dangling references ${[...dangling].join(', ')}`);

  // duplicate fileIDs
  const all = [...text.matchAll(/^--- !u!\d+ &(\d+)/gm)].map(m => m[1]);
  if (new Set(all).size !== all.length) problem(`${name}: duplicate fileIDs`);

  // what the scene is made of
  const prefabs = new Set();
  for (const d of docs) {
    if (!/^--- !u!1001/.test(d)) continue;
    const g = /m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32})/.exec(d);
    if (g) prefabs.add(path.basename(guidToPath.get(g[1]) || g[1]));
  }

  if (name.startsWith('Core')) {
    const sky = /m_SkyboxMaterial: \{fileID: \d+, guid: ([0-9a-f]{32})/.exec(text);
    const terrain = /m_TerrainData: \{fileID: \d+, guid: ([0-9a-f]{32})/.exec(text);
    const ground = /m_MaterialTemplate: \{fileID: \d+, guid: ([0-9a-f]{32})/.exec(text);
    const music = /m_audioClip: \{fileID: \d+, guid: ([0-9a-f]{32})/.exec(text);
    const light = /m_Enabled: 1[\s\S]{0,400}?m_Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)/.exec(text);
    const end = /endScene: (.+)/.exec(text);
    const wanted = { sky: `Unique.Materials/Level-${name.slice(-1)}/Sky.mat`, terrain: `Terrains/Level-${name.slice(-1)} Terrain.asset`, ground: `Unique.Materials/Level-${name.slice(-1)}/Ground.mat` };

    for (const [what, expect] of Object.entries(wanted)) {
      const got = guidToPath.get({ sky, terrain, ground }[what][1]);
      if (got !== expect) problem(`${name}: ${what} is '${got}', expected '${expect}'`);
    }

    const borrowed = [...new Set([...text.matchAll(/guid: ([0-9a-f]{32})/g)]
      .map(m => guidToPath.get(m[1]))
      .filter(p => p && /Level-[1-4]\/|Adamaks|Triggers\/(Checkpoint|Finish)|RoadArchitectSystem|InvisibleWalls/.test(p)))];

    notes.push(`sky=${path.basename(guidToPath.get(sky[1]) || '?')} terrain=${path.basename(guidToPath.get(terrain[1]) || '?')} ground=${path.basename(guidToPath.get(ground[1]) || '?')}`);
    notes.push(`music=${path.basename(guidToPath.get(music[1]) || '?')} ${end ? 'endScene=' + end[1].trim() : 'NO END SCENE'}`);
    notes.push(`sun: ${light ? `rgb(${light[1]},${light[2]},${light[3]})` : 'not enabled'}`);
    notes.push(`prefabs: ${[...prefabs].join(', ')}`);
    if (borrowed.length) notes.push(`references other levels' assets: ${borrowed.join(', ')}`);
    if (!end) problems++;
    if (!light) problems++;
  } else {
    const loader = /gameplaySceneName: (.*)/.exec(text);
    notes.push(`prefabs: ${[...prefabs].join(', ') || '(none - hand-built scene, like the other Start/End scenes)'}`);
    notes.push(`gameplaySceneName: '${loader ? loader[1].trim() : ''}'`);
    notes.push(`story text: ${JSON.stringify((/startText: (.*)/.exec(text) || [])[1] || '').slice(0, 60)}`);
  }

  console.log(`${name}  (${docs.length} docs)`);
  for (const n of notes) console.log('   ' + n);
}

// ---------------------------------------------------------------- the wiring

console.log('\n=== wiring ===');

const build = fs.readFileSync(path.join(PROJECT, 'ProjectSettings/EditorBuildSettings.asset'), 'utf8');
for (const [name, rel] of SCENES) {
  const guid = pathToGuid.get(rel);
  if (!build.includes(rel) || !build.includes(guid)) problem(`build settings missing ${name}`);
}
console.log(`build settings: ${SCENES.filter(([n, r]) => build.includes(r)).length}/6 new scenes listed`);

const menu = fs.readFileSync(path.join(ASSETS, 'Scripts/UI/Main Menu/menuBTN.cs'), 'utf8');
for (const level of [5, 6]) {
  if (!new RegExp(`public void level${level === 5 ? 'Five' : 'Six'}\\(\\)`).test(menu)) problem('menuBTN has no level' + level);
}
console.log(`menuBTN: ${/levelFive/.test(menu) && /levelSix/.test(menu) ? 'levelFive + levelSix present' : 'MISSING'}`);

const progress = fs.readFileSync(path.join(ASSETS, 'Scripts/Global/LevelProgress.cs'), 'utf8');
const count = /LevelCount = (\d+)/.exec(progress)[1];
console.log(`LevelProgress.LevelCount = ${count}`);
if (count !== '6') problems++;

const levels = fs.readFileSync(path.join(ASSETS, 'Scenes/Levels.unity'), 'utf8');
for (const [method, want] of [['levelFive', 1], ['levelSix', 1]]) {
  const n = [...levels.matchAll(new RegExp('m_MethodName: ' + method, 'g'))].length;
  if (n !== want) problem(`level list: ${method} called ${n} times, expected ${want}`);
}
const stillOption = [...levels.matchAll(/m_MethodName: optionBtn/g)].length;
console.log(`level list: levelFive + levelSix wired; optionBtn still used by ${stillOption} button(s)`);

console.log(problems ? `\n${problems} problem(s)` : '\nno problems found');
