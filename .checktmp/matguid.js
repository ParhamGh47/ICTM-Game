const fs=require('fs'),cp=require('child_process');
const files=cp.execSync('find Assets -name "*.mat.meta"',{encoding:'utf8',maxBuffer:1e9}).split('\n').filter(Boolean);
const map={};
for(const m of files){ const g=(fs.readFileSync(m,'utf8').match(/guid: ([0-9a-f]+)/)||[])[1]; if(g) map[g]=m.replace(/\.meta$/,''); }
for(const g of process.argv.slice(2)) console.log(g+' -> '+(map[g]||'?'));
