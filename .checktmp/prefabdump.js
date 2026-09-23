// Dump a prefab's GameObject / component structure compactly.
const fs = require("fs");
const path = require("path");
const file = process.argv[2];
const raw = fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n");
const chunks = raw.split(/\n(?=--- !u!)/);
const by = {};
const order = [];
for (const c of chunks) {
  const h = c.split("\n")[0];
  const m = /--- !u!(\d+) &(-?\d+)( stripped)?/.exec(h);
  if (!m) continue;
  const body = c.slice(c.indexOf("\n") + 1);
  by[m[2]] = { cls: m[1], body, name: (/^  m_Name: (.*)$/m.exec(body) || [])[1] };
  order.push(m[2]);
}
const clsName = { 1: "GameObject", 4: "Transform", 33: "MeshFilter", 23: "MeshRenderer", 114: "MonoBehaviour", 54: "Rigidbody", 65: "BoxCollider", 136: "CapsuleCollider", 135: "SphereCollider", 143: "MeshCollider", 114: "MonoBehaviour", 224: "RectTransform", 222: "CanvasRenderer", 223: "Canvas", 82: "AudioSource", 198: "ParticleSystem", 199: "ParticleSystemRenderer", 108: "Light", 20: "Camera" };

for (const id of order) {
  const r = by[id];
  if (r.cls !== "1") continue;
  const comps = [];
  const re = /^  - component: \{fileID: (-?\d+)\}/gm;
  let mm;
  while ((mm = re.exec(r.body))) {
    const c = by[mm[1]];
    if (!c) { comps.push("?"); continue; }
    let extra = "";
    if (c.cls === "114") {
      extra = " script=" + (/m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})/.exec(c.body) || [])[1];
    }
    if (c.cls === "65" || c.cls === "135" || c.cls === "136") {
      const s = /m_Radius: ([-0-9.]+)/.exec(c.body);
      const sz = /m_Size: \{x: ([-0-9.]+), y: ([-0-9.]+), z: ([-0-9.]+)\}/.exec(c.body);
      if (s) extra = " radius=" + s[1];
      else if (sz) extra = ` size=${sz[1]}x${sz[2]}x${sz[3]}`;
      extra += " isTrigger=" + (/m_IsTrigger: (\d)/.exec(c.body) || [])[1];
    }
    if (c.cls === "54") {
      extra = " mass=" + ((/m_Mass: ([-0-9.]+)/.exec(c.body) || [])[1] ?? "?") +
              " kinematic=" + ((/m_IsKinematic: (\d)/.exec(c.body) || [])[1] ?? "?") +
              " gravity=" + ((/m_UseGravity: (\d)/.exec(c.body) || [])[1] ?? "?");
    }
    comps.push((clsName[c.cls] || c.cls) + extra);
  }
  const tag = (/m_TagString: (.*)$/m.exec(r.body) || [])[1];
  const layer = (/m_Layer: (\d+)/.exec(r.body) || [])[1];
  const active = (/m_IsActive: (\d)/.exec(r.body) || [])[1];
  console.log(`${r.name || "<unnamed>"}  [tag=${tag} layer=${layer} active=${active}]\n    ${comps.join("\n    ")}`);
}
