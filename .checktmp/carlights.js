const fs=require('fs');
const lightIds=new Set(['8174147078309233629','4671931410639258155','2558027002474730461','752591824608996764','7644403523489890606','8092968937842716263','3508509015668210891','2958668588470120651']);
for(const f of process.argv.slice(2)){
  const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
  const blocks=txt.split(/^--- /m).slice(1);
  let hits=0, samples=[];
  for(const b of blocks){
    const mm=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)];
    for(const m of mm){ if(lightIds.has(m[1])){ hits++; const nm=(b.match(/value: ((206|911|Car|Truck)_(Fwd|Rev)_\d+)/)||[])[1]; if(samples.length<6) samples.push((nm||'?')+' '+m[3]+'='+m[4]); } }
  }
  console.log(f.split('/').slice(-2).join('/')+': '+hits+' light modifications  e.g. '+samples.join(' | '));
}
