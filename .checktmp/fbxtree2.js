// Reads a binary FBX and prints its model (object) tree, nested, with each node's local transform.
// Usage: node .checktmp/fbxtree2.js Assets/Objects/Cars/porsche.fbx
const fs = require('fs');
const path = require('path');

const buf = fs.readFileSync(path.join(process.cwd(), process.argv[2]));
const u32 = (p) => buf.readUInt32LE(p);

function readProperty(p, type) {
  switch (type) {
    case 'Y': return { v: buf.readInt16LE(p), size: 2 };
    case 'C': return { v: buf[p] !== 0, size: 1 };
    case 'I': return { v: buf.readInt32LE(p), size: 4 };
    case 'F': return { v: buf.readFloatLE(p), size: 4 };
    case 'D': return { v: buf.readDoubleLE(p), size: 8 };
    case 'L': return { v: Number(buf.readBigInt64LE(p)), size: 8 };
    case 'S': case 'R': { const len = u32(p); return { v: buf.slice(p + 4, p + 4 + len).toString('utf8'), size: 4 + len }; }
    case 'f': case 'd': case 'l': case 'i': case 'b': {
      const len = u32(p), enc = u32(p + 4), comp = u32(p + 8);
      let size = 12 + len * (type === 'f' || type === 'i' ? 4 : 8);
      if (comp) size += len * (type === 'f' || type === 'i' ? 4 : 8);
      return { v: `[array ${type} len ${len}]`, size };
    }
    default: throw new Error('unknown property type ' + type + ' at ' + p);
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
    for (let i = 0; i < numProps; i++) { const t = String.fromCharCode(buf[q]); const r = readProperty(q + 1, t); props.push(r.v); q += 1 + r.size; }
    const nodeEnd = endOffset;
    const children = q < nodeEnd ? parseNodeList(q, nodeEnd) : [];
    nodes.push({ name, props, children, end: nodeEnd });
    p = nodeEnd;
  }
  return nodes;
}

const top = parseNodeList(27, buf.length);
const find = (n, name) => n.children.find((c) => c.name === name) || null;
const findAll = (n, name) => n.children.filter((c) => c.name === name);

const objects = top.find((n) => n.name === 'Objects') || find(top[0], 'Objects');
if (!objects) { console.log('no Objects'); process.exit(0); }

const models = findAll(objects, 'Model');
const byId = new Map(models.map((m) => [m.props[0], m]));

const connections = top.find((n) => n.name === 'Connections');
const parentOf = new Map();
if (connections) for (const c of connections.children) if (c.props[0] === 'OO') parentOf.set(c.props[1], c.props[2]);

const childrenOf = new Map();
for (const m of models) {
  const p = parentOf.get(m.props[0]);
  if (p == null || !byId.has(p)) continue;
  if (!childrenOf.has(p)) childrenOf.set(p, []);
  childrenOf.get(p).push(m.props[0]);
}

const local = (m) => {
  let t = null, r = null, s = null;
  const props = find(m, 'Properties70');
  if (props) for (const p of props.children) {
    if (p.props[0] === 'Lcl Translation') t = p.props.slice(4, 7);
    if (p.props[0] === 'Lcl Rotation') r = p.props.slice(4, 7);
    if (p.props[0] === 'Lcl Scaling') s = p.props.slice(4, 7);
  }
  return { t, r, s };
};

console.log('=== ' + process.argv[2] + ': ' + models.length + ' nodes ===');
const roots = models.filter((m) => { const p = parentOf.get(m.props[0]); return p == null || !byId.has(p); });
const walk = (m, depth) => {
  const l = local(m);
  const fmt = (v) => (v ? v.map((x) => (typeof x === 'number' ? x.toFixed(2) : x)).join(',') : '-');
  console.log('  '.repeat(depth) + m.props[1] + '   T(' + fmt(l.t) + ') R(' + fmt(l.r) + ') S(' + fmt(l.s) + ')');
  for (const c of childrenOf.get(m.props[0]) || []) walk(byId.get(c), depth + 1);
};
for (const r of roots) walk(r, 0);
