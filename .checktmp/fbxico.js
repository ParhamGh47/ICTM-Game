// Reads TruckIceCream.fbx: model nodes, their pivots, and the bounds of each mesh's vertices,
// so the pivot can be compared with the geometry it is supposed to be at.
const fs = require('fs');
const path = require('path');

const file = path.join(process.cwd(), 'Assets/Objects/Truck/IceCream/TruckIceCream.fbx');
const buf = fs.readFileSync(file);

function u32(p) { return buf.readUInt32LE(p); }
const version = u32(23);

function readProperty(p, type) {
  switch (type) {
    case 'Y': return { v: buf.readInt16LE(p), size: 2, t: type };
    case 'C': return { v: buf[p] !== 0, size: 1, t: type };
    case 'I': return { v: buf.readInt32LE(p), size: 4, t: type };
    case 'F': return { v: buf.readFloatLE(p), size: 4, t: type };
    case 'D': return { v: buf.readDoubleLE(p), size: 8, t: type };
    case 'L': return { v: Number(buf.readBigInt64LE(p)), size: 8, t: type };
    case 'S': case 'R': {
      const len = u32(p);
      return { v: buf.slice(p + 4, p + 4 + len).toString('utf8'), size: 4 + len, t: type };
    }
    case 'f': case 'd': case 'l': case 'i': case 'b': {
      const len = u32(p), enc = u32(p + 4), comp = u32(p + 8);
      const elem = type === 'f' || type === 'i' ? 4 : 8;
      let size = 12 + len * elem;
      if (comp) size += len * elem;
      return { v: { array: type, len, enc, comp, at: p + 12 }, size, t: type };
    }
    default:
      throw new Error('unknown property type ' + type + ' at ' + p);
  }
}

function parseNodeList(start, end) {
  const nodes = [];
  let p = start;
  while (p < end) {
    const endOffset = u32(p);
    if (endOffset === 0) break;
    const numProps = u32(p + 4);
    const nameLen = buf[p + 12];
    const name = buf.slice(p + 13, p + 13 + nameLen).toString('utf8');
    let q = p + 13 + nameLen;
    const props = [];
    for (let i = 0; i < numProps; i++) {
      const t = String.fromCharCode(buf[q]);
      const r = readProperty(q + 1, t);
      props.push(r.v);
      q += 1 + r.size;
    }
    const children = q < endOffset ? parseNodeList(q, endOffset) : [];
    nodes.push({ name, props, children });
    p = endOffset;
  }
  return nodes;
}

const top = parseNodeList(27, buf.length);
const objects = top.find((n) => n.name === 'Objects');
const connections = top.find((n) => n.name === 'Connections');
const global = top.find((n) => n.name === 'GlobalSettings');
let unitScale = 1;
if (global) {
  const props = global.children.find((n) => n.name === 'Properties70');
  if (props) for (const p of props.children) if (p.props[0] === 'UnitScaleFactor') unitScale = p.props[4];
}
console.log('file', path.basename(file), 'version', version, 'UnitScaleFactor', unitScale);

const models = objects.children.filter((n) => n.name === 'Model');
const geometries = objects.children.filter((n) => n.name === 'Geometry');
const geoById = new Map(geometries.map((g) => [g.props[0], g]));
const geoOfModel = new Map();
// Connections: props are ['OO', childId, parentId]
const parents = new Map();
for (const c of connections.children) {
  if (c.props[0] !== 'OO') continue;
  const child = c.props[1], parent = c.props[2];
  if (geoById.has(child)) geoOfModel.set(parent, child);
  else parents.set(child, parent);
}

function localProps(m) {
  const out = { t: null, r: null, s: null };
  const props = m.children.find((n) => n.name === 'Properties70');
  if (props) {
    for (const p of props.children) {
      if (p.props[0] === 'Lcl Translation') out.t = p.props.slice(4, 7);
      if (p.props[0] === 'Lcl Rotation') out.r = p.props.slice(4, 7);
      if (p.props[0] === 'Lcl Scaling') out.s = p.props.slice(4, 7);
    }
  }
  return out;
}

function vertices(geo) {
  const v = geo.children.find((n) => n.name === 'Vertices');
  if (!v) return null;
  const info = v.props[0];
  const at = info.at;
  const n = info.len;
  const doubles = info.array === 'd';
  const out = [];
  if (info.comp === 0) {
    for (let i = 0; i < n; i++) {
      out.push(doubles ? buf.readDoubleLE(at + i * 8) : buf.readFloatLE(at + i * 4));
    }
  } else {
    // deflate-compressed array
    const zlib = require('zlib');
    const raw = zlib.inflateSync(buf.slice(at, at + n * (doubles ? 8 : 4)));
    for (let i = 0; i < n; i++) out.push(doubles ? raw.readDoubleLE(i * 8) : raw.readFloatLE(i * 4));
  }
  return out;
}

console.log('\nmodels and their geometry:');
for (const m of models) {
  const name = m.props[1];
  const lp = localProps(m);
  const gid = geoOfModel.get(m.props[0]);
  const geo = gid ? geoById.get(gid) : null;
  let bounds = null, centroid = null;
  if (geo) {
    const v = vertices(geo);
    if (v && v.length >= 3) {
      const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
      const sum = [0, 0, 0];
      for (let i = 0; i + 2 < v.length; i += 3) {
        for (let k = 0; k < 3; k++) {
          min[k] = Math.min(min[k], v[i + k]);
          max[k] = Math.max(max[k], v[i + k]);
          sum[k] += v[i + k];
        }
      }
      const count = Math.floor(v.length / 3);
      bounds = { min, max };
      centroid = sum.map((s) => s / count);
    }
  }
  const chain = [];
  let id = m.props[0];
  const seen = new Set();
  const label = (x) => { const mm = models.find((y) => y.props[0] === x); return mm ? mm.props[1] : `#${x}`; };
  while (parents.has(id) && !seen.has(id)) { seen.add(id); id = parents.get(id); chain.unshift(label(id)); }
  console.log(`\n  "${name}"`);
  console.log(`    hierarchy: ${chain.join(' / ')}${chain.length ? ' / ' : ''}${name}`);
  console.log(`    pivot (Lcl Translation, cm): ${JSON.stringify(lp.t)}   rot: ${JSON.stringify(lp.r)}   scale: ${JSON.stringify(lp.s)}`);
  if (bounds) {
    const f = (a) => a.map((x) => (x * unitScale).toFixed(1)).join(', ');
    const g = (a) => a.map((x) => x.toFixed(1)).join(', ');
    console.log(`    mesh bounds, in the mesh's own units: min (${f(bounds.min)})  max (${f(bounds.max)})`);
    console.log(`    mesh centre (cm, model units): (${g(centroid)})   size (cm): (${bounds.max.map((x, k) => (x - bounds.min[k]).toFixed(1)).join(', ')})`);
    const t = lp.t || [0, 0, 0];
    // The lever arm: the mesh centre in the parent's space, minus the pivot.
    const s = lp.s || [1, 1, 1];
    const r = lp.r || [0, 0, 0];
    const deg = (x) => (x * Math.PI) / 180;
    const [rx, ry, rz] = r.map(deg);
    const cx = centroid[0] * s[0], cy = centroid[1] * s[1], cz = centroid[2] * s[2];
    // rotation XYZ order (as FBX uses for Lcl Rotation)
    const crx = Math.cos(rx), srx = Math.sin(rx), cry = Math.cos(ry), sry = Math.sin(ry), crz = Math.cos(rz), srz = Math.sin(rz);
    const m00 = cry * crz, m01 = srx * sry * crz - crx * srz, m02 = crx * sry * crz + srx * srz;
    const m10 = cry * srz, m11 = srx * sry * srz + crx * crz, m12 = crx * sry * srz - srx * crz;
    const m20 = -sry, m21 = srx * cry, m22 = crx * cry;
    const ox = m00 * cx + m01 * cy + m02 * cz;
    const oy = m10 * cx + m11 * cy + m12 * cz;
    const oz = m20 * cx + m21 * cy + m22 * cz;
    const arm = Math.hypot(ox, oy, oz);
    console.log(`    mesh centre relative to its own pivot (cm): (${ox.toFixed(1)}, ${oy.toFixed(1)}, ${oz.toFixed(1)})  -> lever arm ${arm.toFixed(1)} cm`);
  } else {
    console.log('    (no geometry)');
  }
}
