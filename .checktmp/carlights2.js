const fs=require('fs');
const lightIds=new Set(['8174147078309233629','4671931410639258155','2558027002474730461','752591824608996764','7644403523489890606','8092968937842716263','3508509015668210891','2958668588470120651']);
for(const f of process.argv.slice(2)){
  const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
  const blocks=txt.split(/^--- /m).slice(1);
  const paths={};
  for(const b of blocks){
    const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)];
    for(const m of mods){ if(lightIds.has(m[1])) paths[m[3]]=(paths[m[3]]||0)+1; }
  }
  console.log('== '+f);
  for(const p in paths) console.log('   '+p+' x'+paths[p]);
}
