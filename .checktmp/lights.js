const fs=require('fs');
const path=process.argv[2];
const txt=fs.readFileSync(path,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){
  const m=b.match(/^!u!(\d+) &(\d+)/);
  if(!m) continue;
  const cls=m[1], id=m[2];
  objs[id]={cls, body:b, id};
}
// GO names
const goName={};
for(const id in objs){ if(objs[id].cls==='1'){ const n=objs[id].body.match(/m_Name: (.*)/); goName[id]=n?n[1].trim():'?'; } }
// Transform parents
const tr={};
for(const id in objs){ if(objs[id].cls==='4'||objs[id].cls==='224'){ const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/); const f=objs[id].body.match(/m_Father: \{fileID: (\d+)\}/); tr[id]={go:g?g[1]:null, father:f?f[1]:null}; } }
function pathOf(trId){ const parts=[]; let cur=trId, guard=0; while(cur && guard++<30){ const t=tr[cur]||tr['&'+cur]; if(!t) break; parts.push(goName[t.go]||'?'); cur=t.father; } return parts.reverse().join('/'); }
for(const id in objs){
  if(objs[id].cls!=='108') continue;
  const t=objs[id].body.match(/m_Type: (\d+)/);
  const r=objs[id].body.match(/m_Range: ([\d.]+)/);
  const int=objs[id].body.match(/m_Intensity: ([\d.]+)/);
  const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/);
  const trId=Object.keys(tr).find(k=>tr[k].go===g[1]);
  console.log('Light',id,'type',t&&t[1],'range',r&&r[1],'int',int&&int[1],'at',trId?pathOf(trId):'?');
}
