const fs=require('fs');
const txt=fs.readFileSync(process.argv[2],'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const goName={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); goName[id]=n?n[1].trim():'?';}}
for(const id in objs){
  if(objs[id].cls!=='1') continue;
  if(goName[id]!==process.argv[3]) continue;
  console.log('GO', id, objs[id].body.match(/m_Component:[\s\S]*?(?=\n  m_Layer)/)[0]);
  // list component types on this GO
  const comps=[...objs[id].body.matchAll(/- component: \{fileID: (\d+)\}/g)].map(x=>x[1]);
  for(const c of comps){ if(objs[c]) console.log('  comp', c, 'class', objs[c].cls, (objs[c].body.match(/m_Script: \{fileID: \d+, guid: ([0-9a-f]+)/)||[])[1]||''); }
}
