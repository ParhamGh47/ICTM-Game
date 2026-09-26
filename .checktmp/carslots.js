const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
for(const b of blocks){
  if(!/!u!1001/.test(b.slice(0,12))) continue;
  const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)].map(m=>({t:m[1],g:m[2],p:m[3],v:m[4]}));
  const nm=(mods.find(m=>m.p==='m_Name')||{}).v;
  if(!/^(206|911|Car|Truck)_(Fwd|Rev)_\d+$/.test(nm||'')) continue;
  const paths={};
  for(const m of mods) paths[m.p]=(paths[m.p]||0)+1;
  console.log(nm.padEnd(15)+' guid='+(b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/)||[])[1].slice(0,8)+'  '+Object.keys(paths).join(', '));
}
