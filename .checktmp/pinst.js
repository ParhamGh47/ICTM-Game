const fs=require('fs');
const txt=fs.readFileSync(process.argv[2],'utf8').replace(/\r/g,'');
const blocks=txt.split(/^--- /m).slice(1);
const objs={};
for(const b of blocks){ const m=b.match(/^!u!(\d+) &(\d+)/); if(m) objs[m[2]]={cls:m[1],body:b}; }
const name={}; for(const id in objs){ if(objs[id].cls==='1'){const n=objs[id].body.match(/m_Name: (.*)/); name[id]=n?n[1].trim():'?';}}
const inst=[];
for(const id in objs){ if(objs[id].cls!=='1001') continue;
  const b=objs[id].body;
  const parent=(b.match(/m_TransformParent: \{fileID: (\d+)\}/)||[])[1];
  const mods=[...b.matchAll(/- target: \{fileID: (\d+), guid: ([0-9a-f]+), type: 3\}\n      propertyPath: ([^\n]+)\n      value: ([^\n]*)/g)];
  const o={id,parent,guid:(b.match(/m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+)/)||[])[1],mods:mods.map(m=>({t:m[1],guid:m[2],prop:m[3],val:m[4]}))};
  inst.push(o);
}
const want=process.argv[3];
for(const o of inst){
  const nm=o.mods.find(m=>m.prop==='m_Name');
  if(want && (!nm || !nm.val.includes(want))) continue;
  const pos=o.mods.filter(m=>m.prop.startsWith('m_LocalPosition.'));
  const pv=pos.map(m=>m.prop.split('.').pop()+'='+m.val).join(',');
  const parentName = name[o.parent] || (objs[o.parent]? 'class'+objs[o.parent].cls : '?');
  console.log(`${(nm?nm.val:'?').padEnd(20)} guid=${(o.guid||'').slice(0,8)} parent=${parentName} ${pv}`);
}
