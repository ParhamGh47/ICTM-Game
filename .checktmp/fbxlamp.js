// Works out which part of each car model is where, by reading the models' own geometry:
// each Geometry record's vertices, its per-polygon material index, and the Connections that say
// which material and which Model node it belongs to. Bounds per material are then printed, so a
// lamp can be identified from where the geometry actually is rather than from what it is called.
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

function readTree(buf) {
  const version = buf.readUInt32LE(23);
  const wide = version >= 7500;
  let pos = 27;
  const nodes = [];
  const pathStack = [];

  const readNode = () => {
    const start = pos;
    const endOffset = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const numProps = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const propLen = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos); pos += wide ? 8 : 4;
    const nameLen = buf.readUInt8(pos); pos += 1;
    if (endOffset === 0 && numProps === 0 && propLen === 0 && nameLen === 0) return null;
    const name = buf.slice(pos, pos + nameLen).toString('latin1'); pos += nameLen;
    const props = [];
    for (let i = 0; i < numProps; i++) {
      const t = String.fromCharCode(buf.readUInt8(pos)); pos += 1;
      if (t === 'Y') { props.push(buf.readInt16LE(pos)); pos += 2; }
      else if (t === 'C') { props.push(!!buf.readUInt8(pos)); pos += 1; }
      else if (t === 'I') { props.push(buf.readInt32LE(pos)); pos += 4; }
      else if (t === 'F') { props.push(buf.readFloatLE(pos)); pos += 4; }
      else if (t === 'D') { props.push(buf.readDoubleLE(pos)); pos += 8; }
      else if (t === 'L') { props.push(Number(buf.readBigInt64LE(pos))); pos += 8; }
      else if (t === 'S' || t === 'R') { const l = buf.readUInt32LE(pos); pos += 4; props.push(buf.slice(pos, pos + l).toString('latin1')); pos += l; }
      else if ('fdlbic'.includes(t)) {
        const len = buf.readUInt32LE(pos); const enc = buf.readUInt32LE(pos + 4); const compLen = buf.readUInt32LE(pos + 8); pos += 12;
        const unit = t === 'b' ? 1 : (t === 'd' || t === 'l' ? 8 : 4);
        const raw = buf.slice(pos, pos + (enc ? compLen : len * unit)); pos += raw.length;
        // An array that came in compressed is inflated here: the big ones (vertices, indices) are
        // always stored that way, so without this they read as empty and every bound comes out blank.
        const bytes = enc ? zlib.inflateSync(raw) : raw;
        const values = [];
        for (let k = 0; k < len; k++) {
          values.push(t === 'b' ? !!bytes[k]
            : t === 'i' ? bytes.readInt32LE(k * 4)
            : t === 'f' ? bytes.readFloatLE(k * 4)
            : t === 'l' ? Number(bytes.readBigInt64LE(k * 8))
            : bytes.readDoubleLE(k * 8));
        }
        props.push({ type: t, len, enc, values });
      } else throw new Error('unknown property type ' + JSON.stringify(t) + ' at ' + pos);
    }
    pathStack.push(name);
    const children = [];
    while (pos < endOffset) {
      const save = pos;
      const childEnd = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos);
      if (childEnd === 0) { pos = save + (wide ? 25 : 13); break; }
      const child = readNode();
      if (child) children.push(child);
    }
    pathStack.pop();
    if (endOffset > pos) pos = endOffset;

    const node = { name, props, children, parent: pathStack.length ? pathStack[pathStack.length - 1] : null };
    nodes.push(node);
    return node;
  };

  const roots = [];
  while (pos < buf.length - 13) {
    const peekEnd = wide ? Number(buf.readBigUInt64LE(pos)) : buf.readUInt32LE(pos);
    if (peekEnd === 0) break;
    let node = null;
    try { node = readNode(); } catch (e) { node = null; }
    if (!node) break;
    roots.push(node);
    if (node.start === pos) break;
  }
  return { version, roots, nodes };
}

const child = (node, name) => node.children.find(c => c.name === name);

for (const file of process.argv.slice(2)) {
  const { nodes } = readTree(fs.readFileSync(file));
  console.log('\n===================== ' + path.basename(file));

  const byName = {};
  for (const n of nodes) (byName[n.name] = byName[n.name] || []).push(n);

  // Objects live in their own block; find it by looking for Geometry records under it.
  const objects = nodes.filter(n => n.name === 'Geometry');
  const connections = nodes.filter(n => n.name === 'C' && n.props[0] === 'OO');

  // The material list of each Model, in the order the connections name them.
  const materialsByModel = {};
  for (const c of connections) {
    const childId = String(c.props[1]); const parentId = String(c.props[2]);
    const from = nodes.find(n => String(n.props[0]) === childId && n.name !== 'C');
    const to = nodes.find(n => String(n.props[0]) === parentId && n.name !== 'C');
    if (!from || !to) continue;
    if (to.name === 'Model' || to.name === 'Geometry') {
      (materialsByModel[to.props[0]] = materialsByModel[to.props[0]] || []).push({ childName: from.name, label: from.props.find(p => typeof p === 'string'), id: childId });
    }
  }

  for (const geom of objects) {
    const verts = child(geom, 'Vertices').props[0].values;
    const polys = child(geom, 'PolygonVertexIndex').props[0].values;
    const lem = child(geom, 'LayerElementMaterial');
    const matIdx = lem && child(lem, 'Materials') ? child(lem, 'Materials').props[0].values : [0];

    // Per-polygon bounds, grouped by the material index the polygon carries.
    const bounds = {};
    let poly = 0;
    let current = [];
    const flush = () => {
      const m = matIdx.length === 1 ? matIdx[0] : (matIdx[poly] !== undefined ? matIdx[poly] : 0);
      const b = bounds[m] = bounds[m] || { min: [Infinity, Infinity, Infinity], max: [-Infinity, -Infinity, -Infinity], tris: 0 };
      for (const vi of current) {
        const i = vi < 0 ? -vi - 1 : vi;
        for (let k = 0; k < 3; k++) {
          const v = verts[i * 3 + k];
          if (!Number.isFinite(v)) continue;
          if (v < b.min[k]) b.min[k] = v;
          if (v > b.max[k]) b.max[k] = v;
        }
      }
      b.tris++;
      current = [];
      poly++;
    };
    for (const vi of polys) {
      current.push(vi);
      if (vi < 0) flush();
    }

    const owner = connections.find(c => String(c.props[1]) === String(geom.props[0]));
    const model = owner ? nodes.find(n => String(n.props[0]) === String(owner.props[2]) && n.name === 'Model') : null;
    const mats = model ? (materialsByModel[model.props[0]] || []).filter(m => m.childName === 'Material') : [];

    console.log(`  Geometry ${geom.props[0]}  (model ${model ? model.props.find(p => typeof p === 'string') : '?'}, ${mats.length} materials)`);
    for (const [m, b] of Object.entries(bounds)) {
      const size = [0, 1, 2].map(k => (b.max[k] - b.min[k]));
      const centre = [0, 1, 2].map(k => (b.max[k] + b.min[k]) / 2);
      const label = mats[m] ? mats[m].label : '?';
      console.log(`     slot ${m}  ${label}  tris=${b.tris}  centre=[${centre.map(v => v.toFixed(2)).join(', ')}]  size=[${size.map(v => v.toFixed(2)).join(', ')}]`);
    }
  }
}
