const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const name={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); name[id]=n?n[1].trim():'?';}}
const tr={};
for(const id in objs){ if(objs[id].cls==='4'||objs[id].cls==='224'){ const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/), fa=objs[id].body.match(/m_Father: \{fileID: (\d+)\}/), p=objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/); tr[id]={go:g&&g[1],father:fa&&fa[1],pos:p?[+p[1],+p[2],+p[3]]:[0,0,0]};}}
function world(tid){ let p=[0,0,0],cur=tid,n=0; while(cur&&tr[cur]&&n++<50){ const t=tr[cur]; p=[p[0]+t.pos[0],p[1]+t.pos[1],p[2]+t.pos[2]]; cur=t.father; } return p; }
const cars=[];
for(const id in objs){
  if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)];
  const d={}; for(const m of mods) d[m[3]]=m[4];
  const nm=d['m_Name']||'';
  if(!/^(206|911|Car|Truck)_(Fwd|Rev)_\d+$/.test(nm)) continue;
  const par=(b.match(/m_TransformParent: \{fileID: (\d+)\}/)||[])[1];
  const base=par?world(par):[0,0,0];
  const lp=[+(d['m_LocalPosition.x']||0),+(d['m_LocalPosition.y']||0),+(d['m_LocalPosition.z']||0)];
  cars.push({nm,wp:d['startingWaypoint']!==undefined?Math.round(+d['startingWaypoint']):null,pos:[base[0]+lp[0],base[1]+lp[1],base[2]+lp[2]],speed:d['speedKPH']});
}
const groups={};
for(const c of cars){ const k=c.nm.split('_').slice(0,2).join('_'); (groups[k]=groups[k]||[]).push(c); }
for(const k of Object.keys(groups).sort()){
  const list=groups[k].sort((a,b)=>(a.wp===null?-1:a.wp)-(b.wp===null?-1:b.wp));
  console.log('== '+k+'  count='+list.length+'  speed='+list[0].speed);
  let prev=null;
  for(const c of list){
    let s='';
    if(prev){ const dx=c.pos[0]-prev.pos[0],dy=c.pos[1]-prev.pos[1],dz=c.pos[2]-prev.pos[2]; s='  gap='+Math.hypot(dx,dy,dz).toFixed(1)+'m dWp='+(c.wp-prev.wp); }
    console.log('   '+c.nm.padEnd(15)+' wp='+String(c.wp).padStart(4)+' y='+c.pos[1].toFixed(1)+s);
    prev=c;
  }
}
