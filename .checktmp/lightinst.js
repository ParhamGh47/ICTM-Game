const fs=require('fs'),cp=require('child_process');
const map=JSON.parse(fs.readFileSync('.checktmp/guidmap.json','utf8'));
for(const f of process.argv.slice(2)){
  const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
  const counts={};
  for(const m of txt.matchAll(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+), type: 3\}/g)){
    const p=map[m[1]]||m[1];
    counts[p]=(counts[p]||0)+1;
  }
  let lightPrefabs=0;
  const n=fs.readFileSync(f,'utf8').replace(/\r/g,'').split(/^--- /m).slice(1);
  for(const b of n){ const m=b.match(/^!u!108 &/); if(m) lightPrefabs++; }
  console.log('== '+f.split('/').slice(-2).join('/')+'  scene-authored lights='+lightPrefabs);
  for(const p in counts) if(/Signs\/Lights|StreetLight|TrafficLight|TunnelLight|Bilboards|Cars\//.test(p)) console.log('    '+counts[p]+' x '+p);
}
