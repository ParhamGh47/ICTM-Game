const fs=require('fs'),cp=require('child_process');
const files=cp.execSync('find Assets -name "*.prefab.meta" -o -name "*.fbx.meta" -o -name "*.mat.meta"',{encoding:'utf8',maxBuffer:1e9}).split('\n').filter(Boolean);
const want=new Set(process.argv.slice(2));
for(const m of files){ const g=(fs.readFileSync(m,'utf8').match(/guid: ([0-9a-f]+)/)||[])[1]; if(g&&want.has(g)) console.log(g+' -> '+m.replace(/\.meta$/,'')); }
