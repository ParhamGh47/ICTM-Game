const fs=require('fs'),cp=require('child_process');
const files=cp.execSync('find Assets -name "*.mat.meta"',{encoding:'utf8',maxBuffer:1e9}).split('\n').filter(Boolean);
const map={};
for(const m of files){ const g=(fs.readFileSync(m,'utf8').match(/guid: ([0-9a-f]+)/)||[])[1]; if(g) map[g]=m.replace(/\.meta$/,'').split('/').pop().replace('.mat',''); }
for(const f of process.argv.slice(2)){
  console.log('===== '+f);
  const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
  const blocks=txt.split(/^--- /m).slice(1);
  const objs={};
  for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
  const name={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); name[id]=n?n[1].trim():'?';}}
  for(const id in objs){
    const c=objs[id].cls,b=objs[id].body;
    if(c!=='23'&&c!=='137') continue;
    const g=b.match(/m_GameObject: \{fileID: (\d+)\}/);
    const mats=[...b.matchAll(/- \{fileID: (-?\d+), guid: ([0-9a-f]+), type: 2\}/g)].map(x=>(map[x[2]]||x[2].slice(0,8))+'#'+x[1]);
    console.log('  '+(c==='23'?'Mesh':'Skinned')+' '+(g&&name[g[1]]||'?').padEnd(24)+' -> '+mats.join(', ')+'  enabled='+((b.match(/m_Enabled: (\d)/)||[])[1]));
  }
  // nested prefab refs
  const nested=new Set([...txt.matchAll(/"m_SourcePrefab": \{fileID: 100100000, guid: ([0-9a-f]+)/g)].map(m=>m[1]));
  for(const m of txt.matchAll(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+), type: 3\}/g)) nested.add(m[1]);
  console.log('  nested prefab guids: '+[...nested].map(g=>g.slice(0,8)).join(', '));
}
