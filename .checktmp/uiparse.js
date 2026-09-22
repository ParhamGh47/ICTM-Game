const fs = require("fs");
const file = process.argv[2];
const raw = fs.readFileSync(file, "utf8");
const CHUNKS = raw.split(/\r?\n(?=--- !u!)/);
const recs = [];
const byId = {};

for (const chunk of CHUNKS) {
  const head = chunk.split(/\r?\n/)[0];
  const m = /--- !u!(\d+) &(-?\d+)/.exec(head);
  if (!m) continue;
  const body = chunk.replace(/^.*?\r?\n/, "");
  const rec = {
    cls: m[1],
    id: m[2],
    body,
    name: (/^  m_Name: (.*)$/m.exec(body) || [])[1] || null,
    go: (/^  m_GameObject: \{fileID: (-?\d+)\}/m.exec(body) || [])[1] || null,
  };
  recs.push(rec);
  byId[rec.id] = rec;
}

const objs = {};
for (const r of recs) if (r.cls === "1") objs[r.id] = r;
console.log("objects:", Object.keys(objs).length);

const parentOf = {};
for (const r of recs) {
  if (r.cls !== "4" && r.cls !== "224") continue;
  const father = /m_Father: \{fileID: (-?\d+)\}/.exec(r.body);
  const myGo = /m_GameObject: \{fileID: (-?\d+)\}/.exec(r.body);
  if (!father || !myGo) continue;
  const fr = byId[father[1]];
  if (fr && fr.cls === "4") { /* Transform parent, not GO */ }
  if (fr) {
    const fgo = fr.cls === "1" ? fr.id : fr.go;
    if (fgo) parentOf[myGo[1]] = fgo;
  }
}
function depth(gid) { let d=0,cur=gid,g=0; while(parentOf[cur]&&g++<50){d++;cur=parentOf[cur];} return d; }

// guid of scripts
const guidName = {};
for (const r of recs) {
  if (r.cls !== "114") continue;
  const s = /m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32})/.exec(r.body);
  if (s) guidName[s[1]] = (guidName[s[1]]||0)+1;
}

console.log("--- hierarchy (name [class comps]) ---");
for (const r of recs) {
  if (r.cls !== "1") continue;
  const pad = "  ".repeat(depth(r.id));
  const comps = r.body.split(/\r?\n/).filter(l=>/^  - component:/.test(l)).map(l=>l.match(/fileID: (-?\d+)/)[1]);
  const cnames = comps.map(c=>{const cr=byId[c]; if(!cr) return "?"; if(cr.cls==="114"){const s=/guid: ([0-9a-f]{32})/.exec(cr.body);return "M("+(s?s[1].slice(0,6):"")+")";} return cr.cls==="222"?"CR":cr.cls==="223"?"MR":cr.cls==="224"?"RT":cr.cls==="4"?"T":cr.cls;}); 
  console.log(`${pad}${r.name}  [${cnames.join(",")}]  id=${r.id}`);
  // emit text + button info
  for (const c of comps) {
    const cr = byId[c];
    if (!cr || cr.cls !== "114") continue;
    const t = /m_Text: (.*)$/.exec(cr.body);
    if (t) console.log(`${pad}    TEXT: ${t[1].trim()}`);
    const meth = [...cr.body.matchAll(/m_MethodName: (.*)$/gm)].map(x=>x[1].trim());
    if (meth.length) {
      const tgt = [...cr.body.matchAll(/m_Target: \{fileID: (-?\d+)\}/g)].map(x=>x[1]);
      const inter = /m_Interactable: (-?\d+)/.exec(cr.body);
      console.log(`${pad}    CLICK methods=${JSON.stringify(meth)} targets=${JSON.stringify(tgt)} interactable=${inter?inter[1]:"?"}`);
    }
  }
}
