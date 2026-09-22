// Builds levels 5 and 6: terrain, materials, environment prefabs and the six
// scenes. Everything is copied from the level the project already has and then
// recoloured / re-pointed, so the new levels follow the existing pattern exactly.
const fs = require('fs');
const path = require('path');

const PROJECT = path.resolve(__dirname, '..');
const ASSETS = path.join(PROJECT, 'Assets');

// ------------------------------------------------------------------ helpers

const used = new Set();

function guidMap() {
  const map = new Map();
  const walk = dir => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) walk(p);
      else if (p.endsWith('.meta')) {
        const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(p, 'utf8'));
        if (m) { map.set(m[1], p); used.add(m[1]); }
      }
    }
  };
  walk(ASSETS);
  return map;
}

const GUIDS = guidMap();

function newGuid() {
  let g;
  do {
    const b = require('crypto').randomBytes(16);
    g = b.toString('hex');
  } while (used.has(g));
  used.add(g);
  return g;
}

const read = rel => fs.readFileSync(path.join(ASSETS, rel), 'utf8');
const write = (rel, text) => {
  fs.mkdirSync(path.dirname(path.join(ASSETS, rel)), { recursive: true });
  fs.writeFileSync(path.join(ASSETS, rel), text);
};

const guidCache = new Map();

function guidOf(rel) {
  if (guidCache.has(rel)) return guidCache.get(rel);

  const p = path.join(ASSETS, rel) + '.meta';
  if (!fs.existsSync(p)) throw new Error('no meta for ' + rel);

  const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(p, 'utf8'));
  if (!m) throw new Error('no guid in ' + rel + '.meta');

  guidCache.set(rel, m[1]);
  return m[1];
}

/**
 * Copies one asset and its .meta, giving the copy a fresh guid. Binary assets
 * (the terrain data) are copied as bytes - reading them as text corrupts them.
 */
function copyAsset(srcRel, dstRel, edit) {
  if (!edit) {
    fs.mkdirSync(path.dirname(path.join(ASSETS, dstRel)), { recursive: true });
    fs.copyFileSync(path.join(ASSETS, srcRel), path.join(ASSETS, dstRel));
  } else {
    write(dstRel, edit(read(srcRel)));
  }

  const meta = read(srcRel + '.meta').replace(/^guid: [0-9a-f]{32}/m, 'guid: ' + newGuid());
  write(dstRel + '.meta', meta);
  return dstRel;
}

/**
 * Copies a whole folder, giving every file a fresh guid and re-pointing the
 * references that stayed inside the copy - so a copied prefab still finds its
 * copied materials, and anything shared (meshes, textures) stays shared.
 */
function copyTree(srcDir, dstDir, perFile) {
  const remap = new Map();
  const collected = [];

  const walk = dir => {
    for (const e of fs.readdirSync(path.join(ASSETS, dir), { withFileTypes: true })) {
      const rel = dir + '/' + e.name;
      if (e.isDirectory()) {
        const dstRel = dstDir + '/' + rel.slice(srcDir.length + 1);
        write(dstRel + '.meta', read(rel + '.meta').replace(/^guid: [0-9a-f]{32}/m, 'guid: ' + newGuid()));
        walk(rel);
      } else if (e.name.endsWith('.meta')) {
        continue;
      } else {
        const old = guidOf(rel);
        const fresh = newGuid();
        remap.set(old, fresh);
        collected.push({ rel, old, fresh });
      }
    }
  };
  walk(srcDir);

  for (const f of collected) {
    const dst = dstDir + '/' + f.rel.slice(srcDir.length + 1);

    let text = read(f.rel);

    if (perFile && dst.endsWith('.mat')) text = perFile(dst, text);
    text = text.replace(/guid: [0-9a-f]{32}/g, m => remap.get(m.slice(6)) || m);

    write(dst, text);
    write(dst + '.meta', read(f.rel + '.meta').replace(/^guid: [0-9a-f]{32}/m, 'guid: ' + f.fresh));
  }

  return remap;
}

const fmt = v => {
  const s = v.toFixed(7).replace(/0+$/, '').replace(/\.$/, '');
  return s === '-0' ? '0' : s;
};

/** Replaces the _Color of a Standard material, leaving the alpha alone. */
function setColour(text, rgb) {
  return text.replace(/(- _Color: \{r: )[-\d.]+(, g: )[-\d.]+(, b: )[-\d.]+(, a: [-\d.]+\})/,
    (_, a, b, c, d) => a + fmt(rgb[0]) + b + fmt(rgb[1]) + c + fmt(rgb[2]) + d);
}

function setFloat(text, name, value) {
  return text.replace(new RegExp('(- ' + name + ': )[^\\r\\n]+'), '$1' + fmt(value));
}

function setColourProp(text, name, rgb) {
  return text.replace(new RegExp('(- ' + name + ': \\{r: )[^}]*\\}'),
    (_, a) => a + fmt(rgb[0]) + ', g: ' + fmt(rgb[1]) + ', b: ' + fmt(rgb[2]) + ', a: 1}');
}

const c = hex => {
  const n = parseInt(hex.replace('#', ''), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
};

// ------------------------------------------------------------------ palettes

const LEVELS = {
  5: {
    name: 'level 5 - lilac night',
    terrainSrc: 'Terrains/New Terrain 3.asset',
    soundtrack: 'Audio/Music/Levels/Level-4/4-core-.mp3',
    startText: '[Level 5 opening narration goes here.]',
    hud: { targetTimeSeconds: 240, targetKills: 0 },
    sun: { colour: c('#F2D9FF'), intensity: 1, bounce: 1 },
    sky: { tint: c('#9A6BE8'), ground: c('#1A1340'), atmosphere: 1.85, exposure: 1.3, sunSize: 0.45 },
    materials: {
      Ground: c('#9A66C1'),
      Grass: c('#C287F2'),
      Road: c('#4C4760'),
      RoadLine: c('#E6ACF6'),
      Shoulder: c('#241C57'),
    },
    // Environment prefabs, by file name inside the copied level folder.
    env: {
      'Grass.mat': c('#B781EA'),
      'Trees/TreeColor.mat': c('#B86BF2'),
      'Trees/TreeColor 1.mat': c('#4C2E9E'),
      'Trees/TreeColor-sec.mat': c('#1A1240'),
      'Trees/TreeColor-sec 1.mat': c('#8C4DD9'),
      'Trees/TreeColor-third.mat': c('#E6ACF6'),
      'Trees/TreeColor-third 1.mat': c('#6B42B8'),
      'Trees/Orange - tree.mat': c('#CC94F2'),
      'Trees/Orange - tree 1.mat': c('#9E61E6'),
      'Trees/Yelllow-Tree.mat': c('#D9A8FA'),
      'Trees/Yelllow-Tree 1.mat': c('#7A48C7'),
      'Trees/Yelllow-Tree 2.mat': c('#A874E8'),
      'Buildings/Building-Mat-1.mat': c('#1A1240'),
      'Buildings/Building-Mat-1 1.mat': c('#E6ACF6'),
      'Buildings/Building-Mat-1 2.mat': c('#8557C7'),
      'Buildings/Building-Mat-1 3.mat': c('#B39BD9'),
      'Buildings/orange - buildig.mat': c('#573399'),
    },
  },
  6: {
    name: 'level 6 - black and white',
    terrainSrc: 'Terrains/New Terrain 4.asset',
    soundtrack: 'Audio/Music/Levels/Level-4/4-core-.mp3',
    startText: '[Level 6 opening narration goes here.]',
    hud: { targetTimeSeconds: 240, targetKills: 0 },
    sun: { colour: c('#FFFFFF'), intensity: 0.9, bounce: 1 },
    sky: { tint: c('#DCDEE2'), ground: c('#4D4D4F'), atmosphere: 1.25, exposure: 1.25, sunSize: 0.5 },
    materials: {
      Ground: c('#A8A8AC'),
      Grass: c('#D2D2D4'),
      Road: c('#5C5C60'),
      RoadLine: c('#FAFAFA'),
      Shoulder: c('#38383B'),
    },
    env: {
      'Grass.mat': c('#D6D6D8'),
      'Trees/TreeColor.mat': c('#DCDCDC'),
      'Trees/TreeColor 1.mat': c('#575757'),
      'Trees/TreeColor-sec.mat': c('#292929'),
      'Trees/TreeColor-sec 1.mat': c('#949494'),
      'Trees/TreeColor-third.mat': c('#F2F2F2'),
      'Trees/TreeColor-third 1.mat': c('#707070'),
      'Trees/Orange - tree.mat': c('#C7C7C7'),
      'Trees/Orange - tree 1.mat': c('#474747'),
      'Trees/Yelllow-Tree.mat': c('#EBEBEB'),
      'Trees/Yelllow-Tree 1.mat': c('#666666'),
      'Trees/Yelllow-Tree 2.mat': c('#A8A8A8'),
      'Buildings/Building-Mat-1.mat': c('#333333'),
      'Buildings/Building-Mat-1 1.mat': c('#EBEBEB'),
      'Buildings/Building-Mat-1 2.mat': c('#808080'),
      'Buildings/Building-Mat-1 3.mat': c('#BDBDBD'),
      'Buildings/orange - buildig.mat': c('#616161'),
    },
  },
};

// ------------------------------------------------------------------ the level's assets

const built = {};

for (const level of [5, 6]) {
  const cfg = LEVELS[level];
  console.log(`\n=== ${cfg.name} ===`);

  // 1. terrain (binary - copied byte for byte) ---------------------------
  const terrain = copyAsset(cfg.terrainSrc, `Terrains/Level-${level} Terrain.asset`);
  console.log('  terrain      Terrains/Level-' + level + ' Terrain.asset');

  // 2. the level's own materials ---------------------------------------
  const editSky = t => {
    let s = setColourProp(t, '_SkyTint', cfg.sky.tint);
    s = setColourProp(s, '_GroundColor', cfg.sky.ground);
    s = setFloat(s, '_AtmosphereThickness', cfg.sky.atmosphere);
    s = setFloat(s, '_Exposure', cfg.sky.exposure);
    return setFloat(s, '_SunSize', cfg.sky.sunSize);
  };

  for (const [name, rgb] of Object.entries(cfg.materials)) {
    copyAsset(`Unique.Materials/Level-4/${name}.mat`, `Unique.Materials/Level-${level}/${name}.mat`,
      t => setColour(t, rgb));
  }
  copyAsset('Unique.Materials/Level-4/Sky.mat', `Unique.Materials/Level-${level}/Sky.mat`, editSky);
  write(`Unique.Materials/Level-${level}.meta`, read('Unique.Materials/Level-4.meta').replace(/^guid: [0-9a-f]{32}/m, 'guid: ' + newGuid()));
  console.log('  materials    Unique.Materials/Level-' + level + '/ (6 materials)');

  // 3. the environment prefabs -----------------------------------------
  copyTree('Prefabs/DetailsTerrain/Level-4', `Prefabs/DetailsTerrain/Level-${level}`, (dst, text) => {
    const key = dst.slice(`Prefabs/DetailsTerrain/Level-${level}/`.length);
    const rgb = cfg.env[key];
    if (!rgb) throw new Error('no colour for ' + key);
    return setColour(text, rgb);
  });
  write(`Prefabs/DetailsTerrain/Level-${level}.meta`, read('Prefabs/DetailsTerrain/Level-4.meta').replace(/^guid: [0-9a-f]{32}/m, 'guid: ' + newGuid()));
  console.log('  environment  Prefabs/DetailsTerrain/Level-' + level + '/');

  built[level] = {
    terrain: guidOf(terrain),
    ground: guidOf(`Unique.Materials/Level-${level}/Ground.mat`),
    sky: guidOf(`Unique.Materials/Level-${level}/Sky.mat`),
  };
}

console.log('\n' + JSON.stringify(built, null, 2));
