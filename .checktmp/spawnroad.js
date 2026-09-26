const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const pos={};
for(const id in objs){ const m=objs[id].body.match(/^  pos: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/m); if(m) pos[id]=[+m[1],+m[2],+m[3]]; }
// truck root position (from the truck prefab instance's root target group)
let truck=null;
for(const id in objs){
  if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  if(!b.includes('guid: d3631120a8772da41ac0a07b6ee0b9e6')) continue;
  const re=/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g;
  let m, groups={};
  while((m=re.exec(b))){ (groups[m[1]]=groups[m[1]]||{})[m[3]]=m[4]; }
  for(const t in groups){ const g=groups[t]; if(g['m_LocalPosition.x']!==undefined){ 
     // parent local? instance is root (m_TransformParent: 0) usually
     truck={x:+g['m_LocalPosition.x'],y:+g['m_LocalPosition.y'],z:+g['m_LocalPosition.z'], parent:(b.match(/m_TransformParent: \{fileID: (\d+)\}/)||[])[1]};
     break; } }
}
console.log('truck spawn:',JSON.stringify(truck));
// roads
const roads=[];
for(const id in objs){
  const b=objs[id].body;
  if(!/^  nodes:/m.test(b)) continue;
  const list=[...b.matchAll(/^  - \{fileID: (\d+)\}$/gm)].map(m=>m[1]);
  const pts=list.map(i=>pos[i]).filter(Boolean);
  if(pts.length<3) continue;
  roads.push({id,pts});
}
for(const r of roads){
  let best=null;
  for(const p of r.pts){ const d=Math.hypot(p[0]-truck.x,p[2]-truck.z); if(!best||d<best.d) best={d,p,p2:p}; }
  const i=r.pts.indexOf(best.p);
  console.log('spline',r.id,'nodes='+r.pts.length,'nearest node '+best.d.toFixed(1)+'m away, y='+best.p[1].toFixed(2)+'  (truck y='+truck.y.toFixed(2)+' -> '+(truck.y-best.p[1]).toFixed(2)+')');
  for(let k=Math.max(0,i-1);k<=Math.min(r.pts.length-1,i+1);k++) console.log('     node',k,r.pts[k].map(v=>v.toFixed(2)).join(', '));
}
