// GraphicsQuality indexes its preset array by the enum's own value, so adding presets is exactly where that
// silently breaks. This reads the file and checks the mapping.
const fs = require('fs');

const text = fs.readFileSync('Assets/Scripts/Global/GraphicsQuality.cs', 'utf8').replace(/\r/g, '');

const enumBody = text.match(/public enum GraphicsPreset\s*\{([\s\S]*?)\}/)[1];
const values = {};
for (const m of enumBody.matchAll(/(\w+)\s*=\s*(\d+)\s*,?/g)) values[m[1]] = +m[2];
console.log('enum:', JSON.stringify(values));

const arrayBody = text.match(/private static readonly Settings\[\] Presets =\s*\{([\s\S]*?)\n    \};/)[1];
const entries = [...arrayBody.matchAll(/new Settings\s*\{([\s\S]*?)\n        \}/g)].map((m) => {
  const name = m[1].match(/name = "([^"]+)"/)[1];
  const get = (f) => {
    const hit = m[1].match(new RegExp('\\b' + f + ' = ([-0-9.a-zA-Z]+),'));
    return hit ? hit[1] : '?';
  };
  return {
    name,
    pixel: get('pixelLights'),
    shDist: get('shadowDistance'),
    shRes: get('shadowResolution'),
    aa: get('antiAliasing'),
    lod: get('lodBias'),
    grass: get('detailDistance'),
    grassDensity: get('detailDensity'),
    baseMap: get('basemapDistance'),
    heightErr: get('heightmapPixelError'),
    particles: get('particleScale'),
    blurTaps: get('blurSampleScale'),
  };
});

console.log('\narray order:', entries.map((e) => e.name).join(', '));

let ok = true;
for (const [name, value] of Object.entries(values)) {
  if (entries[value] === undefined || entries[value].name !== name) {
    console.log('MISMATCH: ' + name + ' (' + value + ') -> array slot ' +
      (entries[value] ? entries[value].name : 'missing'));
    ok = false;
  }
}
console.log(ok ? '\nOK: every enum value indexes its own preset' : '\nBROKEN');

const shown = text.match(/public static readonly GraphicsPreset\[\] Ordered =\s*\{([\s\S]*?)\};/);
const order = [...shown[1].matchAll(/GraphicsPreset\.(\w+)/g)].map((m) => m[1]);
console.log('shown order:', order.join(' -> '));

console.table(entries);
