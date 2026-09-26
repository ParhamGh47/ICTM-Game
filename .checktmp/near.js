const fs=require('fs');
const f=process.argv[2], wantGuid=process.argv[3], radius=parseFloat(process.argv[4]||'12');
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const name={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); name[id]=n?n[1].trim():'?';}}
const tr={};
for(const id in objs){ if(objs[id].cls==='4'||objs[id].cls==='224'){ const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/), fa=objs[id].body.match(/m_Father: \{fileID: (\d+)\}/), p=objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/); tr[id]={go:g&&g[1],father:fa&&fa[1],pos:p?[+p[1],+p[2],+p[3]]:[0,0,0]};}}
function world(tid){ let p=[0,0,0],cur=tid,n=0; while(cur&&tr[cur]&&n++<50){ const t=tr[cur]; p=[p[0]+t.pos[0],p[1]+t.pos[1],p[2]+t.pos[2]]; cur=t.father; } return p; }
let truck=null;
for(const id in objs){
  if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  const src=(b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/)||[])[1];
  if(src!==wantGuid) continue;
  const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)];
  const d={}; for(const m of mods) d[m[3]]=m[4];
  const par=(b.match(/m_TransformParent: \{fileID: (\d+)\}/)||[])[1];
  const base=par?world(par):[0,0,0];
  const lp=[+(d['m_LocalPosition.x']||0),+(d['m_LocalPosition.y']||0),+(d['m_LocalPosition.z']||0)];
  truck={id,pos:[base[0]+lp[0],base[1]+lp[1],base[2]+lp[2]],name:d['m_Name'],parent:par};
}
console.log('truck instance:',JSON.stringify(truck));
if(!truck) process.exit(0);
const P=truck.pos;
const near=[];
for(const id in tr){
  const w=world(id);
  const d=Math.hypot(w[0]-P[0],w[2]-P[2]);
  if(d<=radius) near.push({name:name[tr[id].go]||'?',d:Math.round(d*10)/10,dy:Math.round((w[1]-P[1])*100)/100,cls:tr[id].go?Object.keys(objs).length:0});
}
near.sort((a,b)=>a.d-b.d);
for(const n of near.slice(0,60)) console.log('  '+String(n.d).padStart(6)+'m  dy='+String(n.dy).padStart(8)+'  '+n.name);
// prefab instances near
for(const id in objs){
  if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)];
  const d={}; for(const m of mods) d[m[3]]=m[4];
  const par=(b.match(/m_TransformParent: \{fileID: (\d+)\}/)||[])[1];
  const base=par?world(par):[0,0,0];
  const lp=[+(d['m_LocalPosition.x']||0),+(d['m_LocalPosition.y']||0),+(d['m_LocalPosition.z']||0)];
  const w=[base[0]+lp[0],base[1]+lp[1],base[2]+lp[2]];
  const dist=Math.hypot(w[0]-P[0],w[2]-P[2]);
  if(dist<=radius*2) console.log('  PREFAB '+(d['m_Name']||'?')+' d='+dist.toFixed(1)+' guid='+(b.match(/guid: ([0-9a-f]+)/)||[])[1].slice(0,8));
}
