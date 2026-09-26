const fs=require('fs');
for(const f of process.argv.slice(2)){
  const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
  const blocks=txt.split(/^--- /m).slice(1);
  let point=0,dir=0,spot=0;
  for(const b of blocks){ const m=b.match(/^!u!108 &/); if(!m) continue; const t=(b.match(/m_Type: (\d+)/)||[])[1]; if(t==='2')point++; else if(t==='1')dir++; else if(t==='0')spot++; }
  console.log(f.split('/').slice(-2).join('/')+': point='+point+' spot='+spot+' directional='+dir+' (scene-authored only)');
}
