const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const name={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); name[id]=n?n[1].trim():'?';}}
const tr={};
for(const id in objs){ if(objs[id].cls==='4'||objs[id].cls==='224'){ const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/), fa=objs[id].body.match(/m_Father: \{fileID: (\d+)\}/), p=objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/), s=objs[id].body.match(/m_LocalScale: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/); tr[id]={go:g&&g[1],father:fa&&fa[1],pos:p?[+p[1],+p[2],+p[3]]:[0,0,0],scale:s?[+s[1],+s[2],+s[3]]:[1,1,1]};}}
function world(tid){ let p=[0,0,0],s=[1,1,1],cur=tid,n=0; while(cur&&tr[cur]&&n++<50){ const t=tr[cur]; p=[p[0]+t.pos[0],p[1]+t.pos[1],p[2]+t.pos[2]]; cur=t.father; } return p; }
console.log('--- objects and colliders/renderers ---');
for(const id in objs){
  const c=objs[id].cls, b=objs[id].body;
  const g=b.match(/m_GameObject: \{fileID: (\d+)\}/);
  const nm = g? (name[g[1]]||'?') : '?';
  if(c==='65') console.log(`BoxCollider  at ${nm}: m_Size ${(b.match(/m_Size: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/)||[]).slice(1).join(', ')}  center ${(b.match(/m_Center: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/)||[]).slice(1).join(', ')}`);
  if(c==='54') console.log(`Rigidbody at ${nm}: mass ${(b.match(/m_Mass: ([-\d.e]+)/)||[])[1]} drag ${(b.match(/m_Drag: ([-\d.e]+)/)||[])[1]} kinematic ${(b.match(/m_IsKinematic: (\d)/)||[])[1]}`);
  if(c==='136') console.log(`CapsuleCollider at ${nm}: radius ${(b.match(/m_Radius: ([-\d.e]+)/)||[])[1]} height ${(b.match(/m_Height: ([-\d.e]+)/)||[])[1]}`);
  if(c==='135') console.log(`SphereCollider at ${nm}: radius ${(b.match(/m_Radius: ([-\d.e]+)/)||[])[1]}`);
  if(c==='108') console.log(`Light at ${nm}: type ${(b.match(/m_Type: (\d+)/)||[])[1]} range ${(b.match(/m_Range: ([-\d.e]+)/)||[])[1]} angle ${(b.match(/m_SpotAngle: ([-\d.e]+)/)||[])[1]}`);
}
