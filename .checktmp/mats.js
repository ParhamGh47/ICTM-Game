const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const name={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); name[id]=n?n[1].trim():'?';}}
const mats={}; // guid+fileid -> name
for(const id in objs){
  const c=objs[id].cls, b=objs[id].body;
  if(c==='23'||c==='21'){ // MeshRenderer / Material
    const g=b.match(/m_GameObject: \{fileID: (\d+)\}/);
    const nm=g?(name[g[1]]||'?'):'?';
    const list=[...b.matchAll(/- \{fileID: (\d+), guid: ([0-9a-f]+), type: 2\}/g)].map(x=>({cls:'', id:x[1], guid:x[2]}));
    if(list.length) console.log('Renderer '+(nm+'').padEnd(22)+' mats: '+list.map(x=>x.guid.slice(0,8)+'/'+x.id).join(', '));
  }
  if(c==='21'){ const n=b.match(/m_Name: (.*)/); mats[id]=n&&n[1].trim(); }
}
