const fs = require('fs');

const cars = {
  '46bdbac3208241b409bba27b8072fcf1': '206',
  '4cd592c6a69223c4c85cec6a5185bf1a': '911',
  '776a8b37009d79b4b8511c7e03c67828': 'Car',
  '9ce62f70048d6bd4986fd0550705b7f7': 'Truck',
};

const scenes = process.argv.slice(2);

for (const f of scenes) {
  const text = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');
  // Each PrefabInstance block.
  const blocks = [...text.matchAll(/^--- !u!1001 &(\d+)\n([\s\S]*?)(?=^--- |\Z)/gm)];
  const counts = {};
  let withLights = 0, without = 0;
  const parents = {};

  for (const b of blocks) {
    const src = (b[2].match(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)/) || [])[1];
    const car = cars[src];
    if (!car) continue;
    counts[car] = (counts[car] || 0) + 1;
    if (/propertyPath: lightsOn/.test(b[2])) withLights++; else without++;
  }

  console.log(f.replace(/.*Scenes\//, ''));
  console.log('   cars:', JSON.stringify(counts), ' lightsOn-overridden:', withLights, ' no-override(->prefab 1):', without);
}
