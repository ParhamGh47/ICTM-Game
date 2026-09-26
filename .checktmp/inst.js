const fs=require('fs');
const f=process.argv[2], guid=process.argv[3];
const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
for(const b of blocks){
  if(!b.startsWith('!u!1001')) continue;
  if(!b.includes('guid: '+guid)) continue;
  console.log('---- prefab instance ----');
  console.log(b.split('\n').slice(0,2000).join('\n'));
}
