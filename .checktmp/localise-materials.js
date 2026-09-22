// Levels 1-4 share a few environment materials between their folders: every level's
// Building7 uses level 1's material, Tree-3 uses level 2's, and the tree canopy and
// grass mesh come from Prefabs/Environments. Copying level 4's folder faithfully
// therefore drags another level's colours into the new levels - which for a
// black-and-white level 6 is the difference between the level working and not.
//
// So the new levels get their own copy of each of those materials, in their own
// palette, and their prefabs are pointed at it. Nothing outside the new level
// folders is touched.
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

const guidOf = rel => {
  const g = pathToGuid.get(rel);
  if (!g) throw new Error('no guid for ' + rel);
  return g;
};

const newGuid = () => {
  let g;
  do { g = require('crypto').randomBytes(16).toString('hex'); } while (guidToPath.has(g));
  guidToPath.set(g, '(new)');
  return g;
};

const fmt = v => v.toFixed(7).replace(/0+$/, '').replace(/\.$/, '');

function setColour(text, rgb) {
  return text.replace(/(- _Color: \{r: )[-\d.]+(, g: )[-\d.]+(, b: )[-\d.]+(, a: [-\d.]+\})/,
    (_, a, b, c, d) => a + fmt(rgb[0]) + b + fmt(rgb[1]) + c + fmt(rgb[2]) + d);
}

const colourOf = text => {
  const m = /- _Color: \{r: ([-\d.]+), g: ([-\d.]+), b: ([-\d.]+)/.exec(text);
  return [+m[1], +m[2], +m[3]];
};

/** The materials the shared environment prefabs borrow from other levels. */
const BORROWED = (level) => ({
  // source (other level)                                   -> this level's own file
  'Prefabs/DetailsTerrain/Level-1/Buildings/Building-Mat-1 3.mat': `Prefabs/DetailsTerrain/Level-${level}/Buildings/Building-Mat-1 3.mat`,
  'Prefabs/DetailsTerrain/Level-1/Buildings/orange - buildig.mat': `Prefabs/DetailsTerrain/Level-${level}/Buildings/orange - buildig.mat`,
  'Prefabs/DetailsTerrain/Level-2/Trees/TreeColor-third 1.mat': `Prefabs/DetailsTerrain/Level-${level}/Trees/TreeColor-third 1.mat`,
  'Prefabs/Environments/Grass.mat': `Prefabs/DetailsTerrain/Level-${level}/Grass.mat`,
});

/** The shared tree canopy, which gets a copy of its own inside the level's tree folder. */
const canopyCopy = level => `Prefabs/DetailsTerrain/Level-${level}/Trees/Materials/TreeColor.mat`;

for (const level of [5, 6]) {
  const dir = path.join(ASSETS, `Prefabs/DetailsTerrain/Level-${level}`);

  // the canopy copy, coloured like this level's own trees
  const treeColour = colourOf(fs.readFileSync(path.join(dir, 'Trees', 'TreeColor.mat'), 'utf8'));

  const canopySrc = 'Prefabs/Environments/Trees/Level-1/Materials/TreeColor.mat';
  const canopyDst = canopyCopy(level);

  // the folder it goes in is empty in the level that was copied, so it has to be made
  fs.mkdirSync(path.dirname(path.join(ASSETS, canopyDst)), { recursive: true });
  fs.writeFileSync(path.join(ASSETS, canopyDst), setColour(fs.readFileSync(path.join(ASSETS, canopySrc), 'utf8'), treeColour));

  const canopyMeta = fs.readFileSync(path.join(ASSETS, canopySrc + '.meta'), 'utf8')
    .replace(/^guid: [0-9a-f]{32}/m, 'guid: ' + newGuid());
  fs.writeFileSync(path.join(ASSETS, canopyDst + '.meta'), canopyMeta);

  // the canopy copy was written just now, so its guid comes off the meta rather than the map
  const canopyGuid = /^guid: ([0-9a-f]{32})/m.exec(canopyMeta)[1];
  guidToPath.set(canopyGuid, canopyDst);
  pathToGuid.set(canopyDst, canopyGuid);

  // shared guid -> this level's guid
  const remap = new Map(Object.entries(BORROWED(level)).map(([src, dst]) => [guidOf(src), guidOf(dst)]));
  remap.set(guidOf(canopySrc), canopyGuid);

  let touched = 0, replaced = 0;

  (function walk(d) {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      const p = path.join(d, e.name);
      if (e.isDirectory()) { walk(p); continue; }
      if (!p.endsWith('.prefab')) continue;

      const text = fs.readFileSync(p, 'utf8');
      let next = text;
      for (const [from, to] of remap) next = next.split(from).join(to);

      if (next !== text) {
        fs.writeFileSync(p, next);
        touched++;
        replaced += [...text.matchAll(/guid: [0-9a-f]{32}/g)].length - [...next.matchAll(/guid: [0-9a-f]{32}/g)].length;
      }
    }
  })(dir);

  console.log(`level ${level}: canopy ${canopyDst}, ${touched} prefabs re-pointed at this level's materials`);

  // report what is left borrowed
  const borrowedLeft = new Map();
  (function walk(d) {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      const p = path.join(d, e.name);
      if (e.isDirectory()) { walk(p); continue; }
      if (!p.endsWith('.prefab')) continue;
      for (const m of fs.readFileSync(p, 'utf8').matchAll(/guid: ([0-9a-f]{32})/g)) {
        const q = guidToPath.get(m[1]);
        if (q && /DetailsTerrain\/Level-[1-4]|Environments\//.test(q)) borrowedLeft.set(q, (borrowedLeft.get(q) || 0) + 1);
      }
    }
  })(dir);

  console.log(borrowedLeft.size
    ? '   still borrowed: ' + [...borrowedLeft.keys()].join(', ')
    : '   nothing borrowed from other levels');
}
