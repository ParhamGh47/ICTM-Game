const fs=require('fs');
const map=JSON.parse(fs.readFileSync('.checktmp/guidmap.json','utf8'));
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const gname={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); gname[id]=n?n[1].trim():'?';}}
const tr={};
for(const id in objs){ if(objs[id].cls==='4'||objs[id].cls==='224'){ const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/), fa=objs[id].body.match(/m_Father: \{fileID: (\d+)\}/), p=objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/); tr[id]={go:g&&g[1],father:fa&&fa[1],pos:p?[+p[1],+p[2],+p[3]]:[0,0,0]};}}
function world(tid){ let p=[0,0,0],cur=tid,n=0; while(cur&&tr[cur]&&n++<50){ const t=tr[cur]; p=[p[0]+t.pos[0],p[1]+t.pos[1],p[2]+t.pos[2]]; cur=t.father; } return p; }
const out=[];
for(const id in objs){
  if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  const src=(b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/)||[])[1];
  const re=/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\s*\n\s*propertyPath: ([^\n]+)\s*\n\s*value: ([^\n]*)/g;
  let m; const groups={};
  while((m=re.exec(b))){ (groups[m[1]]=groups[m[1]]||{})[m[3]]=m[4]; }
  let root=null;
  for(const t in groups){ if(groups[t]['m_LocalPosition.x']!==undefined){ root=groups[t]; break; } }
  if(!root) continue;
  const par=(b.match(/m_TransformParent: \{fileID: (\d+)\}/)||[])[1];
  const base=par?world(par):[0,0,0];
  const w=[base[0]+ +(root['m_LocalPosition.x']||0), base[1]+ +(root['m_LocalPosition.y']||0), base[2]+ +(root['m_LocalPosition.z']||0)];
  out.push({nm:root['m_Name']||map[src]||src, path:map[src]||'', pos:w});
}
const truck=[36.201,2.643,435.091];
out.sort((a,b)=>Math.hypot(a.pos[0]-truck[0],a.pos[2]-truck[2])-Math.hypot(b.pos[0]-truck[0],b.pos[2]-truck[2]));
for(const o of out.slice(0,25)) console.log(String(Math.hypot(o.pos[0]-truck[0],o.pos[2]-truck[2]).toFixed(1)).padStart(7)+'m  '+(o.nm+'').padEnd(34)+o.path+'  y='+o.pos[1].toFixed(1));
