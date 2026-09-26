const fs=require('fs'),cp=require('child_process');
const metas=cp.execSync('find Assets -name "*.prefab.meta"',{encoding:'utf8',maxBuffer:1e9}).split('\n').filter(Boolean);
const map={};
for(const m of metas){ const t=fs.readFileSync(m,'utf8'); const g=(t.match(/guid: ([0-9a-f]+)/)||[])[1]; if(g) map[g]=m.replace(/\.meta$/,''); }
fs.writeFileSync('.checktmp/guidmap.json',JSON.stringify(map));
console.log('entries',Object.keys(map).length);
