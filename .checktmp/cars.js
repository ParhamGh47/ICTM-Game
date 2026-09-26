const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const name={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); name[id]=n?n[1].trim():'?';}}
const tr={};
for(const id in objs){ if(objs[id].cls==='4'||objs[id].cls==='224'){ const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/), fa=objs[id].body.match(/m_Father: \{fileID: (\d+)\}/), p=objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/); tr[id]={go:g&&g[1],father:fa&&fa[1],pos:p?[+p[1],+p[2],+p[3]]:null};}}
const trOfGo={}; for(const id in tr) trOfGo[tr[id].go]=id;
function path(trId){ const parts=[]; let cur=trId,g=0; while(cur&&g++<20){ const t=tr[cur]; if(!t) break; parts.push(name[t.go]); cur=t.father; } return parts.reverse(); }
const roots=[]; let target=null;
for(const id in tr){ if(name[tr[id].go]===(process.argv[3]||'PassingCars') && !tr[id].father) target=id; }
if(target===undefined||target===null){ console.log('no parent found'); process.exit(0); }
function world(trId){ let p=[0,0,0], cur=trId; while(cur){ const t=tr[cur]; if(t&&t.pos){ p[0]+=t.pos[0]; p[1]+=t.pos[1]; p[2]+=t.pos[2]; } cur=t&&t.father; } return p; }
function kids(trId){ return Object.keys(tr).filter(k=>tr[k].father===trId); }
for(const child of kids(target)){
  console.log('# '+name[tr[child].go]);
  let prev=null;
  for(const g of kids(child)){
    const go=tr[g].go;
    const nm=name[go];
    const w=world(g);
    let d='';
    if(prev){ const dx=w[0]-prev[0],dy=w[1]-prev[1],dz=w[2]-prev[2]; d=' d='+Math.sqrt(dx*dx+dy*dy+dz*dz).toFixed(2); }
    console.log('  '+nm.padEnd(22)+' ['+w.map(v=>v.toFixed(2)).join(', ')+']'+d);
    prev=w;
  }
}
