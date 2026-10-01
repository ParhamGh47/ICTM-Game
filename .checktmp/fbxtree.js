// Minimal binary FBX reader: prints the model (object) tree with local translation/rotation/scale,
// so the truck's ice cream can be seen as Unity will see it.
const fs = require('fs');
const path = require('path');

const file = path.join(process.cwd(), 'Assets/Objects/Truck/Truck.fbx');
const buf = fs.readFileSync(file);

function u32(p) { return buf.readUInt32LE(p); }
function u8(p) { return buf[p]; }

// 23-byte magic + 4-byte version
const version = u32(23);
console.log('FBX version', version, 'file bytes', buf.length);

function readProperty(p, type) {
  switch (type) {
    case 'Y': return { v: buf.readInt16LE(p), size: 2 };
    case 'C': return { v: buf[p] !== 0, size: 1 };
    case 'I': return { v: buf.readInt32LE(p), size: 4 };
    case 'F': return { v: buf.readFloatLE(p), size: 4 };
    case 'D': return { v: buf.readDoubleLE(p), size: 8 };
    case 'L': return { v: Number(buf.readBigInt64LE(p)), size: 8 };
    case 'S': case 'R': {
      const len = u32(p);
      return { v: buf.slice(p + 4, p + 4 + len).toString('utf8'), size: 4 + len };
    }
    case 'f': case 'd': case 'l': case 'i': case 'b': {
      const len = u32(p), enc = u32(p + 4), comp = u32(p + 8);
      let size = 4 + 4 + 4 + len * (type === 'f' || type === 'i' ? 4 : 8);
      if (comp) size += len * (type === 'f' || type === 'i' ? 4 : 8);
      return { v: `[array ${type} len ${len} enc ${enc} comp ${comp}]`, size };
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
    const propLen = u32(p + 8);
    const nameLen = u8(p + 12);
    const name = buf.slice(p + 13, p + 13 + nameLen).toString('utf8');
    let q = p + 13 + nameLen;
    const props = [];
    for (let i = 0; i < numProps; i++) {
      const t = String.fromCharCode(buf[q]);
      const r = readProperty(q + 1, t);
      props.push(r.v);
      q += 1 + r.size;
    }
    const nodeEnd = endOffset;
    const children = q < nodeEnd ? parseNodeList(q, nodeEnd) : [];
    nodes.push({ name, props, children, start: p, end: nodeEnd });
    p = nodeEnd;
  }
  return nodes;
}

const top = parseNodeList(27, buf.length);
console.log('top-level nodes:', top.map((n) => n.name).join(', '));

function find(node, name) {
  for (const c of node.children) if (c.name === name) return c;
  return null;
}
function findAll(node, name, out = []) {
  for (const c of node.children) {
    if (c.name === name) out.push(c);
  }
  return out;
}

const objects = find(top[0], 'Objects') || top.find((n) => n.name === 'Objects');
if (!objects) { console.log('no Objects node'); process.exit(0); }

const models = findAll(objects, 'Model');
console.log('models:', models.length);
for (const m of models) {
  const name = m.props[1];
  const type = m.props[2];
  let t = null, r = null, s = null;
  const props = find(m, 'Properties70');
  if (props) {
    for (const p of props.children) {
      const key = p.props[0];
      if (key === 'Lcl Translation') t = p.props.slice(4, 7);
      if (key === 'Lcl Rotation') r = p.props.slice(4, 7);
      if (key === 'Lcl Scaling') s = p.props.slice(4, 7);
    }
  }
  console.log(`  id ${m.props[0]}  "${name}"  type ${type}  T ${JSON.stringify(t)}  R ${JSON.stringify(r)}`);
}

// connections: child -> parent
const connections = find(top[0], 'Connections');
const parents = new Map();
if (connections) {
  for (const c of connections.children) {
    if (c.props[0] !== 'OO') continue;
    parents.set(c.props[1], c.props[2]);
  }
}
const byId = new Map(models.map((m) => [m.props[0], m]));
console.log('\nhierarchy of the model nodes:');
function label(id) {
  const m = byId.get(id);
  return m ? m.props[1] : `#${id}`;
}
for (const m of models) {
  const chain = [];
  let id = m.props[0];
  const seen = new Set();
  while (parents.has(id) && !seen.has(id)) { seen.add(id); id = parents.get(id); chain.unshift(label(id)); }
  console.log('  ', chain.join(' / ') + (chain.length ? ' / ' : '') + label(m.props[0]));
}
