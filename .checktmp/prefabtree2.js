const fs=require('fs');
const f=process.argv[2];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const cls={}, name={}, bodyOf={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(!m) continue; cls[m[2]]=m[1]; bodyOf[m[2]]=b; if(m[1]==='1'){ const n=b.match(/m_Name: (.*)/); name[m[2]]=n?n[1].trim():'?'; } }
const tr={}, goOf={};
for(const id in cls){ if(cls[id]==='4'||cls[id]==='224'){ const g=bodyOf[id].match(/m_GameObject: \{fileID: (\d+)\}/), fa=bodyOf[id].match(/m_Father: \{fileID: (\d+)\}/); tr[id]={go:g&&g[1],father:fa&&fa[1]}; goOf[g&&g[1]]=id; } }
const compsOf={};
for(const id in cls){ const g=(bodyOf[id].match(/m_GameObject: \{fileID: (\d+)\}/)||[])[1]; if(g) (compsOf[g]=compsOf[g]||[]).push(cls[id]); }
function path(gid){ let t=goOf[gid], parts=[], n=0; while(t&&tr[t]&&n++<30){ parts.push(name[tr[t].go]||'?'); t=tr[t].father; } return parts.reverse().join('/'); }
const roots=Object.keys(name).filter(g=>!goOf[g]||!tr[goOf[g]]||!tr[goOf[g]].father);
for(const g of Object.keys(name).sort()) console.log(path(g).padEnd(52)+' comps='+[...new Set(compsOf[g]||[])].join(',')+(name[g]? '' : ''));
