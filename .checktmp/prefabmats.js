const fs=require('fs');
const mats={};
for(const p of require('child_process').execSync("find Assets -name '*.mat'",{encoding:'utf8'}).trim().split('\n')){
  const g=(fs.readFileSync(p+'.meta','utf8').match(/guid: ([0-9a-f]{32})/)||[])[1];
  if(g) mats[g]=p.split('/').pop();
}
for(const f of process.argv.slice(2)){
  const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
  const blocks=txt.split(/^--- /m).slice(1);
  const name={};
  for(const b of blocks){ const m=b.match(/^!u!1 &(\d+)/); if(m){ const n=b.match(/m_Name: (.*)/); name[m[1]]=n?n[1].trim():'?'; } }
  console.log('== '+f);
  for(const b of blocks){
    if(!/^!u!(23|137) &/.test(b)) continue;
    const g=b.match(/m_GameObject: \{fileID: (\d+)\}/);
    const en=b.match(/m_Enabled: (\d)/);
    const guids=[...b.matchAll(/guid: ([0-9a-f]{32}), type: 2/g)].map(m=>m[1]);
    console.log('   '+(name[g[1]]||'?').padEnd(22)+' enabled='+(en?en[1]:'-')+'  ['+guids.map(x=>mats[x]||('?'+x.slice(0,6))).join(', ')+']');
  }
}
