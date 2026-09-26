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
const byDir={Fwd:[],Rev:[]};
for(const id in objs){
  if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g)];
  const d={}; for(const m of mods) d[m[3]]=m[4];
  const nm=d['m_Name']||'';
  const mm=nm.match(/^(206|911|Car|Truck)_(Fwd|Rev)_(\d+)$/);
  if(!mm) continue;
  const par=(b.match(/m_TransformParent: \{fileID: (\d+)\}/)||[])[1];
  const base=par?world(par):[0,0,0];
  const lp=[+(d['m_LocalPosition.x']||0),+(d['m_LocalPosition.y']||0),+(d['m_LocalPosition.z']||0)];
  byDir[mm[2]].push({nm,model:mm[1],wp:d['startingWaypoint']!==undefined?Math.round(+d['startingWaypoint']):-1,pos:[base[0]+lp[0],base[1]+lp[1],base[2]+lp[2]],speed:d['speedKPH'],root:d['waypointsRoot']});
}
for(const dir of ['Fwd','Rev']){
  const list=byDir[dir].sort((a,b)=>a.wp-b.wp);
  console.log('=== '+dir+' count='+list.length+'  waypointRoot='+(list[0]&&list[0].root)+'  speed='+(list[0]&&list[0].speed));
  let prev=null, minGap=1e9, minPair=null;
  for(const c of list){
    let s='';
    if(prev){ const g=Math.hypot(c.pos[0]-prev.pos[0],c.pos[1]-prev.pos[1],c.pos[2]-prev.pos[2]); s='  gap='+g.toFixed(1)+'m dWp='+(c.wp-prev.wp); if(g<minGap){minGap=g;minPair=[prev.nm,c.nm];} }
    console.log('   '+c.nm.padEnd(14)+' wp='+String(c.wp).padStart(4)+s);
    prev=c;
  }
  console.log('  min gap '+minGap.toFixed(1)+'m between '+minPair);
}
