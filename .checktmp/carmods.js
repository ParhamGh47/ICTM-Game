const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
let shown=0;
for(const id in objs){
  if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)];
  const d={}; for(const m of mods) d[m[3]]=m[4];
  const nm=d['m_Name']||'';
  if(!/^(206|911|Car|Truck)_(Fwd|Rev)_\d+$/.test(nm)) continue;
  if(shown++>=4) break;
  console.log('=== '+nm+' guid='+(b.match(/guid: ([0-9a-f]+)/)||[])[1]);
  for(const m of mods) console.log('   '+m[3]+' = '+m[4]);
}
