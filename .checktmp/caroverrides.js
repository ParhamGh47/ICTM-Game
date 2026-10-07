const fs = require('fs');
const cars = ['46bdbac3208241b409bba27b8072fcf1','4cd592c6a69223c4c85cec6a5185bf1a','776a8b37009d79b4b8511c7e03c67828','9ce62f70048d6bd4986fd0550705b7f7'];
for (const f of process.argv.slice(2)) {
  const text = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');
  const blocks = [...text.matchAll(/^--- !u!1001 &(\d+)\n([\s\S]*?)(?=^--- |\Z)/gm)];
  const paths = {};
  for (const b of blocks) {
    const src = (b[2].match(/m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)/) || [])[1];
    if (!cars.includes(src)) continue;
    for (const m of b[2].matchAll(/propertyPath: (.*)/g)) paths[m[1]] = (paths[m[1]] || 0) + 1;
  }
  console.log(f.replace(/.*Scenes\//, ''));
  for (const [k, v] of Object.entries(paths).sort((a,b)=>b[1]-a[1])) console.log('   ', v, k);
}
