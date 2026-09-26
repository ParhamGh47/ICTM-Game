// Measures a binary FBX's geometry: every mesh's own bounds and the whole model's box, in the file's units.
//
// The player truck's visual is a nested FBX instance (scale 0.1, at the prefab root), so the model's own box
// is the only honest answer to "how big is the truck" - Unity's mesh bounds live in the Library, not in the
// project, and the numbers have to come from the file itself.
//
// Binary FBX 7.4 (version 7400) stores node records as 13-byte headers and arrays uncompressed, which is all
// this needs: no zlib, no pivots, no animation. Transforms are the node's own T * R * S with the geometric
// transform applied to the mesh after it.
const fs = require('fs');
const zlib = require('zlib');

function readNodes(buffer, offset, version) {
  const nodes = [];
  const header = version >= 7500 ? 25 : 13;

  for (;;) {
    if (offset + header > buffer.length) break;

    const endOffset = buffer.readUInt32LE(offset);
    const numProperties = buffer.readUInt32LE(offset + 4);
    const propertyListLen = buffer.readUInt32LE(offset + 8);
    const nameLen = buffer.readUInt8(offset + 12);

    if (endOffset === 0) {
      offset += header;
      break;                        // null record: end of this list
    }

    const name = buffer.toString('utf8', offset + 13, offset + 13 + nameLen);
    let p = offset + 13 + nameLen;

    const props = [];
    for (let i = 0; i < numProperties; i++) {
      const type = String.fromCharCode(buffer.readUInt8(p));
      p += 1;
      switch (type) {
        case 'Y': props.push(buffer.readInt16LE(p)); p += 2; break;
        case 'C': props.push(buffer.readUInt8(p) !== 0); p += 1; break;
        case 'I': props.push(buffer.readInt32LE(p)); p += 4; break;
        case 'F': props.push(buffer.readFloatLE(p)); p += 4; break;
        case 'D': props.push(buffer.readDoubleLE(p)); p += 8; break;
        case 'L': props.push(Number(buffer.readBigInt64LE(p))); p += 8; break;
        case 'f': case 'd': case 'l': case 'i': case 'b': {
          const len = buffer.readUInt32LE(p);
          const encoding = buffer.readUInt32LE(p + 4);
          const compressed = buffer.readUInt32LE(p + 8);
          p += 12;
          const bytes = encoding === 1 ? zlib.inflateSync(buffer.subarray(p, p + compressed)) : buffer.subarray(p, p + len * ({ f: 4, i: 4, d: 8, l: 8, b: 1 }[type]));
          p += encoding === 1 ? compressed : len * ({ f: 4, i: 4, d: 8, l: 8, b: 1 }[type]);
          const size = { f: 4, i: 4, d: 8, l: 8, b: 1 }[type];
          const read = { f: 'readFloatLE', i: 'readInt32LE', d: 'readDoubleLE', l: 'readBigInt64LE', b: 'readUInt8' }[type];
          const out = new Array(len);
          for (let k = 0; k < len; k++) {
            const v = size === 1 ? bytes.readUInt8(k) : bytes[read](k * size);
            out[k] = typeof v === 'bigint' ? Number(v) : v;
          }
          props.push(out);
          break;
        }
        case 'S': case 'R': {
          const len = buffer.readUInt32LE(p);
          p += 4;
          props.push(buffer.toString('utf8', p, p + len));
          p += len;
          break;
        }
        default:
          throw new Error('unknown property type ' + type + ' at ' + p);
      }
    }

    const children = [];
    let cursor = p;
    while (cursor < endOffset) {
      const sub = readNodes(buffer, cursor, version);
      for (const n of sub.nodes) children.push(n);
      cursor = sub.offset;
    }

    nodes.push({ name, props, children });
    offset = endOffset;
  }

  return { nodes, offset };
}

const path = process.argv[2];
const buffer = fs.readFileSync(path);
const version = buffer.readUInt32LE(23);
const parsed = readNodes(buffer, 27, version);
console.log(path, 'version', version, 'top-level nodes:', parsed.nodes.map((n) => n.name).join(', '));

const objects = parsed.nodes.find((n) => n.name === 'Objects');
const connections = parsed.nodes.find((n) => n.name === 'Connections');
if (!objects) { console.log('no Objects'); process.exit(1); }

// The transform properties are not child nodes: they are "P" records inside the node's Properties70, whose
// props are [name, type, subtype, flags, ...values]. Reading them from where they actually are is the whole
// difference between a model that is laid out correctly and one that looks as if every part sat at the origin.
function properties(node) {
  const out = {};
  const p70 = (node.children || []).find((c) => c.name === 'Properties70');
  if (!p70) return out;
  for (const p of p70.children) {
    if (p.name !== 'P') continue;
    const values = p.props.slice(4).filter((v) => typeof v !== 'object');
    out[p.props[0]] = values;
  }
  return out;
}

const models = [];
const geometries = [];
for (const node of objects.children) {
  if (node.name === 'Model') {
    const id = node.props[0];
    const props = properties(node);
    const name = String(node.props[1]).replace(/[\u0000-\u001f].*/g, '').replace('\u0000', '');
    models.push({ id, name, node, props, get: (key) => (props[key] ? { props: [props[key].map(Number)] } : null) });
  } else if (node.name === 'Geometry') {
    geometries.push({ id: node.props[0], name: node.props[1], node, verts: (node.children.find((c) => c.name === 'Vertices') || {}).props[0] || [] });
  }
}

console.log('\n=== models ===');
for (const m of models) {
  const t = (m.props['Lcl Translation'] || [0, 0, 0]).map(Number);
  const r = (m.props['Lcl Rotation'] || [0, 0, 0]).map(Number);
  const s = (m.props['Lcl Scaling'] || [1, 1, 1]).map(Number);
  console.log(m.id, m.name.padEnd(20), 'T', t.map((v) => v.toFixed(3)).join(','), 'R', r.map((v) => v.toFixed(2)).join(','), 'S', s.map((v) => v.toFixed(4)).join(','));
}

const parentOf = {};
if (connections) {
  for (const c of connections.children) {
    if (c.name !== 'C') continue;
    const kind = c.props[0];
    const child = c.props[1];
    const parent = c.props[2];
    if (kind === 'OO') parentOf[child] = parent;
  }
}

// ---- matrices: translation * rotation (euler XYZ, degrees) * scale
function mat(translation, rotation, scaling) {
  const [rx, ry, rz] = rotation.map((d) => (d * Math.PI) / 180);
  const cx = Math.cos(rx), sx = Math.sin(rx), cy = Math.cos(ry), sy = Math.sin(ry), cz = Math.cos(rz), sz = Math.sin(rz);
  // Blender/FBX default order: R = Rz * Ry * Rx (X applied first)
  const r = [
    [cy * cz, cx * sz + sx * sy * cz, sx * sz - cx * sy * cz],
    [-cy * sz, cx * cz - sx * sy * sz, sx * cz + cx * sy * sz],
    [sy, -sx * cy, cx * cy],
  ];
  const m = [
    [r[0][0] * scaling[0], r[0][1] * scaling[1], r[0][2] * scaling[2], translation[0]],
    [r[1][0] * scaling[0], r[1][1] * scaling[1], r[1][2] * scaling[2], translation[1]],
    [r[2][0] * scaling[0], r[2][1] * scaling[1], r[2][2] * scaling[2], translation[2]],
    [0, 0, 0, 1],
  ];
  return m;
}

function multiply(a, b) {
  const out = [[0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 1]];
  for (let i = 0; i < 3; i++)
    for (let j = 0; j < 4; j++) {
      let s = 0;
      for (let k = 0; k < 4; k++) s += a[i][k] * b[k][j];
      out[i][j] = s;
    }
  return out;
}

function apply(m, v) {
  return [
    m[0][0] * v[0] + m[0][1] * v[1] + m[0][2] * v[2] + m[0][3],
    m[1][0] * v[0] + m[1][1] * v[1] + m[1][2] * v[2] + m[1][3],
    m[2][0] * v[0] + m[2][1] * v[1] + m[2][2] * v[2] + m[2][3],
  ];
}

const arr = (child, fallback) => (child && Array.isArray(child.props[0]) ? child.props[0] : fallback);

const worldCache = {};
function worldOf(model) {
  if (worldCache[model.id]) return worldCache[model.id];
  const t = arr(model.get('Lcl Translation'), [0, 0, 0]);
  const r = arr(model.get('Lcl Rotation'), [0, 0, 0]);
  const s = arr(model.get('Lcl Scaling'), [1, 1, 1]);
  const local = mat(t, r, s);
  const parentId = parentOf[model.id];
  const parentModel = models.find((m) => m.id === parentId);
  const m = parentModel ? multiply(worldOf(parentModel), local) : local;
  worldCache[model.id] = m;
  return m;
}

const overall = { min: [Infinity, Infinity, Infinity], max: [-Infinity, -Infinity, -Infinity] };
const rows = [];
for (const g of geometries) {
  if (!g.verts.length) continue;
  const owner = models.find((m) => m.id === parentOf[g.id]);
  const world = owner ? worldOf(owner) : mat([0, 0, 0], [0, 0, 0], [1, 1, 1]);
  const geo = multiply(world,
    multiply(
      mat(arr(owner && owner.get('GeometricTranslation'), [0, 0, 0]), arr(owner && owner.get('GeometricRotation'), [0, 0, 0]), [1, 1, 1]),
      mat([0, 0, 0], [0, 0, 0], arr(owner && owner.get('GeometricScaling'), [1, 1, 1]))));
  const min = [Infinity, Infinity, Infinity];
  const max = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < g.verts.length; i += 3) {
    const v = apply(geo, [g.verts[i], g.verts[i + 1], g.verts[i + 2]]);
    for (let k = 0; k < 3; k++) {
      if (v[k] < min[k]) min[k] = v[k];
      if (v[k] > max[k]) max[k] = v[k];
      if (v[k] < overall.min[k]) overall.min[k] = v[k];
      if (v[k] > overall.max[k]) overall.max[k] = v[k];
    }
  }
  rows.push({ name: g.name, model: owner ? owner.name : '?', min, max, verts: g.verts.length / 3 });
}

rows.sort((a, b) => (b.max[1] - b.min[1]) - (a.max[1] - a.min[1]));
console.log('\nmeshes:', rows.length);
for (const r of rows.slice(0, 40)) {
  console.log(
    String(r.verts).padStart(6), (r.model + '/' + r.name).padEnd(52),
    'size ' + [0, 1, 2].map((k) => (r.max[k] - r.min[k]).toFixed(3).padStart(9)).join(' '),
    '| centre ' + [0, 1, 2].map((k) => ((r.max[k] + r.min[k]) / 2).toFixed(3).padStart(9)).join(' ')
  );
}

const size = [0, 1, 2].map((k) => overall.max[k] - overall.min[k]);
const centre = [0, 1, 2].map((k) => (overall.max[k] + overall.min[k]) / 2);
console.log('\nWHOLE MODEL (file units)');
console.log('  size  ', size.map((v) => v.toFixed(4)).join(', '));
console.log('  centre', centre.map((v) => v.toFixed(4)).join(', '));
console.log('  min   ', overall.min.map((v) => v.toFixed(4)).join(', '), ' max', overall.max.map((v) => v.toFixed(4)).join(', '));
const scale = parseFloat(process.argv[3] || '0.1');
console.log('\nSCALED by ' + scale + ' (the prefab\'s model scale)');
console.log('  size  ', size.map((v) => (v * scale).toFixed(4)).join(', '));
console.log('  centre', centre.map((v) => (v * scale).toFixed(4)).join(', '));
console.log('  y     ', (overall.min[1] * scale).toFixed(4), '..', (overall.max[1] * scale).toFixed(4));
console.log('  x     ', (overall.min[0] * scale).toFixed(4), '..', (overall.max[0] * scale).toFixed(4));
console.log('  z     ', (overall.min[2] * scale).toFixed(4), '..', (overall.max[2] * scale).toFixed(4));
