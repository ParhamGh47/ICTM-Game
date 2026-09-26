const fs=require('fs');
const txt=fs.readFileSync(process.argv[2],'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const goName={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); goName[id]=n?n[1].trim():'?';}}
const tr={}; for(const id in objs){ if(objs[id].cls==='4'||objs[id].cls==='224'){ const g=objs[id].body.match(/m_GameObject: \{fileID: (\d+)\}/), f=objs[id].body.match(/m_Father: \{fileID: (\d+)\}/), p=objs[id].body.match(/m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/), r=objs[id].body.match(/m_LocalRotation: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+), w: ([-\d.e]+)\}/), s=objs[id].body.match(/m_LocalScale: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}/); tr[id]={go:g?g[1]:null,father:f?f[1]:null,pos:p?[+p[1],+p[2],+p[3]]:null,rot:r?[+r[1],+r[2],+r[3],+r[4]]:null,scale:s?[+s[1],+s[2],+s[3]]:null};}}
const want=process.argv.slice(3);
function find(name){ for(const id in tr){ if(goName[tr[id].go]===name) return id; } return null; }
function walk(id,depth){ const t=tr[id]; console.log(' '.repeat(depth)+goName[t.go],'pos',t.pos,'rot',t.rot,'scale',t.scale); for(const c in tr){ if(tr[c].father===id) walk(c,depth+1);} }
for(const name of want){ const id=find(name); console.log('== '+name+' =='); if(id){ let root=id; while(tr[root].father && tr[root].father!=='0') root=tr[root].father; walk(root,0);} }
