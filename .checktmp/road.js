const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const pos={};
for(const id in objs){ const m=objs[id].body.match(/^  pos: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/m); if(m) pos[id]=[+m[1],+m[2],+m[3]]; }
const roads=[];
for(const id in objs){
  const b=objs[id].body;
  if(!/^  nodes:/.test(b.replace(/^[\s\S]*?\n  m_EditorClassIdentifier: /m,''))) continue;
  const list=[...b.matchAll(/^  - \{fileID: (\d+)\}$/gm)].map(m=>m[1]);
  if(list.length<3) continue;
  const pts=list.map(i=>pos[i]).filter(Boolean);
  if(pts.length<3) continue;
  roads.push({id,pts});
}
const T=[+process.argv[3],+process.argv[4]];
for(const r of roads){
  let best=null;
  for(const p of r.pts){ const d=Math.hypot(p[0]-T[0],p[2]-T[1]); if(!best||d<best.d) best={d,p}; }
  console.log('spline',r.id,'nodes',r.pts.length,'nearest node d='+best.d.toFixed(1)+'m at ('+best.p.map(v=>v.toFixed(2)).join(', ')+')');
  // print the 3 nodes around the nearest, in order
  const idx=r.pts.indexOf(best.p);
  for(let i=Math.max(0,idx-2);i<=Math.min(r.pts.length-1,idx+2);i++) console.log('    node',i,r.pts[i].map(v=>v.toFixed(2)).join(', '));
}
