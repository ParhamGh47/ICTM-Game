const fs=require('fs'), cp=require('child_process');
const files=cp.execSync('find Assets -name "*.prefab" -o -name "*.unity"',{encoding:'utf8',maxBuffer:1e9}).trim().split('\n');
for(const f of files){
  const txt=fs.readFileSync(f,'utf8').replace(/\r/g,'');
  const blocks=txt.split(/^--- /m).slice(1);
  let out=[];
  for(const b of blocks){
    const m=b.match(/^!u!(\d+) &(\d+)/); if(!m||m[1]!=='108') continue;
    const t=b.match(/m_Type: (\d+)/), r=b.match(/m_Range: ([\d.]+)/), i=b.match(/m_Intensity: ([\d.]+)/), a=b.match(/m_SpotAngle: ([\d.]+)/), g=b.match(/m_GameObject: \{fileID: (\d+)\}/);
    out.push(`type=${t&&t[1]} range=${r&&r[1]} int=${i&&i[1]} angle=${a&&a[1]}`);
  }
  if(out.length) console.log(f+' :: '+out.join(' | '));
}
